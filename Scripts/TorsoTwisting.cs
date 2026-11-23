using Godot;

namespace Mech;

/// <summary>Which arc a weapon fires through, from its mount.</summary>
public enum FiringArcDirection { Forward, LeftArm, RightArm, Leg, Rear }

public static partial class CombatRules
{
    public static readonly float MaxTorsoTwist = Mathf.DegToRad(90);
    /// <summary>Half of the 120° arc every weapon covers about its mount facing.</summary>
    public static readonly float FiringHalfArc = Mathf.DegToRad(60);
    /// <summary>Arm weapons reach this much further toward their own arm's side.</summary>
    public static readonly float ArmArcExtension = Mathf.DegToRad(60);
    private const float ArcEpsilon = .00001f;
    public static float TorsoTurnRate(MechSpec spec) => Mathf.DegToRad(spec.WeightClass switch
    {
        MechWeightClass.Light => 90, MechWeightClass.Medium => 75, MechWeightClass.Heavy => 60, MechWeightClass.Assault => 45,
        _ => throw new ArgumentException($"Unknown weight class: {spec.WeightClass}", nameof(spec))
    });
    public static bool IsTorso(MechLocation location) => location is
        MechLocation.CenterTorso or MechLocation.LeftTorso or MechLocation.RightTorso;
    public static bool IsLeg(MechLocation location) => location is MechLocation.LeftLeg or MechLocation.RightLeg;
    public static FiringArcDirection ArcDirection(WeaponSpec weapon) => weapon.Rear ? FiringArcDirection.Rear
        : IsLeg(weapon.Location) ? FiringArcDirection.Leg : weapon.Location switch
        {
            MechLocation.LeftArm => FiringArcDirection.LeftArm, MechLocation.RightArm => FiringArcDirection.RightArm,
            _ => FiringArcDirection.Forward
        };
    /// <summary>A weapon's arc: angles From to To (radians, positive toward the mech's right) about Facing.</summary>
    public static (Vector2 Facing, float From, float To) FiringArc(CombatUnit unit, FiringArcDirection direction) => direction switch
    {
        FiringArcDirection.Leg => (unit.Facing, -FiringHalfArc, FiringHalfArc),
        FiringArcDirection.Rear => (-unit.TorsoFacing, -FiringHalfArc, FiringHalfArc),
        FiringArcDirection.LeftArm => (unit.TorsoFacing, -FiringHalfArc - ArmArcExtension, FiringHalfArc),
        FiringArcDirection.RightArm => (unit.TorsoFacing, -FiringHalfArc, FiringHalfArc + ArmArcExtension),
        _ => (unit.TorsoFacing, -FiringHalfArc, FiringHalfArc)
    };
    public static (Vector2 Facing, float From, float To) FiringArc(CombatUnit unit, CombatWeapon weapon) =>
        FiringArc(unit, ArcDirection(weapon.Spec));
    public static bool InFiringArc(CombatUnit unit, CombatUnit target, CombatWeapon weapon)
    {
        var (facing, from, to) = FiringArc(unit, weapon);
        var angle = facing.AngleTo(target.Position - unit.Position);
        return angle >= from - ArcEpsilon && angle <= to + ArcEpsilon;
    }
    /// <summary>The weapons a range overlay counts: switched on, intact and with ammunition.</summary>
    private static IEnumerable<CombatWeapon> ReadyWeapons(CombatUnit unit) =>
        unit.Weapons.Where(weapon => weapon.Enabled && unit.WeaponUsable(weapon));
    /// <summary>One circle per distinct long range, longest first, with the weapon classes reaching it in enum order.</summary>
    public static IReadOnlyList<(float Range, HardpointClass[] Classes)> WeaponRangeCircles(CombatUnit unit) =>
        ReadyWeapons(unit).GroupBy(weapon => weapon.LongRange).OrderByDescending(group => group.Key)
            .Select(group => (group.Key, Classes(group))).ToArray();
    /// <summary>One wedge per arc direction and distinct long range, longest first, with the classes reaching it.</summary>
    public static IReadOnlyList<(FiringArcDirection Direction, float Range, HardpointClass[] Classes)> FiringArcWedges(CombatUnit unit) =>
        ReadyWeapons(unit).GroupBy(weapon => (Direction: ArcDirection(weapon.Spec), weapon.LongRange))
            .OrderByDescending(group => group.Key.LongRange).ThenBy(group => group.Key.Direction)
            .Select(group => (group.Key.Direction, group.Key.LongRange, Classes(group))).ToArray();
    private static HardpointClass[] Classes(IEnumerable<CombatWeapon> weapons) =>
        weapons.Select(weapon => WeaponMounts.ClassOf(weapon.Spec.Kind)).Distinct().Order().ToArray();
    /// <summary>Whether twisting the torso could bring a forward or arm weapon's arc onto the target with the legs held.</summary>
    public static bool ReachableByTwist(CombatUnit unit, CombatUnit target, CombatWeapon weapon)
    {
        var direction = ArcDirection(weapon.Spec);
        if (direction is FiringArcDirection.Rear or FiringArcDirection.Leg) return false;
        var (_, from, to) = FiringArc(unit, direction);
        var angle = unit.Facing.AngleTo(target.Position - unit.Position);
        // An arm's reach can pass straight behind the legs, so the bearing is also tried a full turn either way.
        return new[] { angle, angle - Mathf.Tau, angle + Mathf.Tau }
            .Any(bearing => bearing >= from - MaxTorsoTwist - ArcEpsilon && bearing <= to + MaxTorsoTwist + ArcEpsilon);
    }
}

public sealed partial class CombatUnit
{
    /// <summary>Waist-joint angle in radians relative to leg Facing, not a world-space heading.</summary>
    public float TorsoTwist { get; internal set; }
    public Vector2 TorsoFacing => Facing.Rotated(TorsoTwist);
    /// <summary>The waist turned during the current step.</summary>
    public bool TorsoTurning { get; internal set; }
    /// <summary>Moving, turning or twisting during the current step spreads a laser burn across locations.</summary>
    public bool InMotion => Moving || AngularVelocity != 0 || TorsoTurning;
    public string? CurrentRearTarget { get; internal set; }
    public float TorsoTurnRate => CombatRules.TorsoTurnRate(Spec);
    // Rear 120 degrees, centered opposite torso facing; only torsos have separate rear armor.
    public bool RearHitFrom(Vector2 attackFrom) => (attackFrom - Position).LengthSquared() > 1e-10f
        && TorsoFacing.Dot((attackFrom - Position).Normalized()) <= -.5f + .00001f;
}

public sealed partial class CombatSimulation
{
    private bool ValidEnemy(CombatUnit unit, CombatUnit target) => target.Side != unit.Side
        && !target.Defeated && Detected(unit.Side, target.Id);
    private static bool FirePermitted(CombatUnit unit, CombatUnit target) => unit.Firing != FiringMode.HoldFire
        || (unit.Order == CombatOrder.Attack && unit.AttackTarget == target.Id);

    /// <summary>Ordered contacts can be tracked before a shot is possible, including under Hold Fire.</summary>
    public CombatUnit? SelectAimTarget(CombatUnit unit)
    {
        if (!unit.Active || unit.Prone || Passive(unit)) return null;
        if (unit.Order == CombatOrder.Attack && unit.AttackTarget is { } attack && ValidEnemy(unit, Unit(attack)))
            return Unit(attack);
        if (unit.PriorityTarget is { } priority && ValidEnemy(unit, Unit(priority))) return Unit(priority);
        return SelectTarget(unit);
    }
    private static void UpdateTorso(CombatUnit unit, CombatUnit? target, float delta)
    {
        var desired = target is null ? 0 : Math.Clamp(unit.Facing.AngleTo(target.Position - unit.Position),
            -CombatRules.MaxTorsoTwist, CombatRules.MaxTorsoTwist);
        var twist = Mathf.MoveToward(unit.TorsoTwist, desired, unit.TorsoTurnRate * delta);
        unit.TorsoTurning = twist != unit.TorsoTwist; unit.TorsoTwist = twist;
    }
    public CombatUnit? SelectRearTarget(CombatUnit unit)
    {
        if (!unit.Active || unit.Prone || Passive(unit)) return null;
        bool Eligible(CombatUnit target) => ValidEnemy(unit, target)
            && unit.Weapons.Any(weapon => weapon.Spec.Rear && weapon.Enabled && unit.WeaponUsable(weapon)
                && unit.Position.DistanceTo(target.Position) <= weapon.LongRange && CombatRules.InFiringArc(unit, target, weapon)
                && LineOfFire(unit, target, weapon));
        if (unit.Order == CombatOrder.Attack && unit.AttackTarget is { } attack && Eligible(Unit(attack))) return Unit(attack);
        if (unit.Firing == FiringMode.HoldFire) return null;
        if (unit.PriorityTarget is { } priority && Eligible(Unit(priority))) return Unit(priority);
        if (unit.CurrentRearTarget is { } current && Eligible(Unit(current))) return Unit(current);
        return Units.Where(Eligible).OrderBy(target => unit.Position.DistanceSquaredTo(target.Position)).FirstOrDefault();
    }
    public bool CanFire(CombatUnit unit, CombatUnit target, CombatWeapon weapon) => unit.Active && !unit.Prone && !Passive(unit) && weapon.Enabled
        && unit.WeaponUsable(weapon) && ValidEnemy(unit, target) && FirePermitted(unit, target)
        && unit.Position.DistanceTo(target.Position) <= weapon.LongRange
        && LineOfFire(unit, target, weapon) && CombatRules.InFiringArc(unit, target, weapon);

    public string WeaponStatus(CombatUnit unit, CombatWeapon weapon, CombatUnit? target)
    {
        if (WeaponRestriction(unit, weapon, target) is { } restriction) return restriction;
        var volley = SelectHeatVolley(unit, weapon.Spec.Rear ? SelectAimTarget(unit) : target,
            weapon.Spec.Rear ? target : SelectRearTarget(unit));
        return VolleyStatus(unit, weapon, volley);
    }

    /// <summary>One refresh shares volley selection across weapons, without caching mutable combat state.</summary>
    internal IReadOnlyList<string> WeaponStatuses(CombatUnit unit, CombatUnit? mainTarget,
        CombatUnit? rearTarget, CombatUnit? previewTarget)
    {
        var statuses = new string[unit.Weapons.Count];
        var volleys = new Dictionary<(CombatUnit? Main, CombatUnit? Rear), IReadOnlyList<(CombatWeapon Weapon, CombatUnit Target)>>();
        for (var i = 0; i < unit.Weapons.Count; i++)
        {
            var weapon = unit.Weapons[i];
            var target = (weapon.Spec.Rear ? rearTarget : mainTarget) ?? previewTarget;
            if (WeaponRestriction(unit, weapon, target) is { } restriction)
            {
                statuses[i] = restriction;
                continue;
            }
            // Preview-only targets retain the existing per-mount fallback and shared main/rear heat budget.
            var pair = weapon.Spec.Rear ? (mainTarget, target) : (target, rearTarget);
            if (!volleys.TryGetValue(pair, out var volley))
            {
                volley = SelectHeatVolley(unit, pair.Item1, pair.Item2);
                volleys.Add(pair, volley);
            }
            statuses[i] = VolleyStatus(unit, weapon, volley);
        }
        return statuses;
    }

    private static string VolleyStatus(CombatUnit unit, CombatWeapon weapon,
        IReadOnlyList<(CombatWeapon Weapon, CombatUnit Target)> volley) =>
        volley.Any(shot => shot.Weapon == weapon) || unit.Side != CombatSide.Friendly ? "Ready" : TooHot;
    /// <summary>A friendly weapon held by the heat limit. Enemy readouts never give heat as the reason.</summary>
    public const string TooHot = "Too Hot!";

    // A null result means only the shared heat-budget decision remains.
    private string? WeaponRestriction(CombatUnit unit, CombatWeapon weapon, CombatUnit? target)
    {
        if (unit.Shutdown && !unit.Defeated) return "Thermal shutdown";
        if (!unit.Active || !unit.WeaponIntact(weapon)) return "Unavailable";
        if (unit.Prone) return "Prone";
        if (!unit.WeaponUsable(weapon)) return "Out of ammunition";
        if (!weapon.Enabled) return "Disabled";
        if (target is null) return unit.Firing == FiringMode.HoldFire ? "Hold fire" : "No target";
        if (!FirePermitted(unit, target)) return "Hold fire";
        if (!ValidEnemy(unit, target)) return "No contact";
        if (unit.Position.DistanceTo(target.Position) > weapon.LongRange) return "Out of range";
        if (!LineOfFire(unit, target, weapon)) return "Line of fire blocked";
        if (!CombatRules.InFiringArc(unit, target, weapon))
            return CombatRules.ReachableByTwist(unit, target, weapon) ? "Turning to target" : "Outside firing arc";
        if (weapon.Remaining > .000001) return $"Ready in {weapon.Remaining:0.0}s";
        if (TerrainHitChance(unit, target, weapon) == 0) return "Cannot hit";
        if (SavingAmmo(unit, target, weapon))
            return $"Saving ammo (+{TerrainPenalty(unit, target, weapon)} over +{CombatRules.AmmoAllowance(unit.AmmoEconomy)})";
        return null;
    }
}
