using Godot;

namespace Mech;

public static partial class CombatRules
{
    public static readonly float MaxTorsoTwist = Mathf.DegToRad(90);
    public static readonly float FiringTolerance = Mathf.DegToRad(30);
    private const float ArcEpsilon = .00001f;
    public static float TorsoTurnRate(MechSpec spec) => Mathf.DegToRad(spec.WeightClass switch
    {
        "Light" => 90, "Medium" => 75, "Heavy" => 60, "Assault" => 45,
        _ => throw new ArgumentException($"Unknown weight class: {spec.WeightClass}", nameof(spec))
    });
    public static bool IsTorso(MechLocation location) => location is
        MechLocation.CenterTorso or MechLocation.LeftTorso or MechLocation.RightTorso;
    public static bool IsLeg(MechLocation location) => location is MechLocation.LeftLeg or MechLocation.RightLeg;
    public static Vector2 WeaponFacing(CombatUnit unit, CombatWeapon weapon)
    {
        var facing = IsLeg(weapon.Spec.Location) ? unit.Facing : unit.TorsoFacing;
        return weapon.Spec.Rear ? -facing : facing;
    }
    public static bool InFiringArc(CombatUnit unit, CombatUnit target, CombatWeapon weapon) =>
        Math.Abs(WeaponFacing(unit, weapon).AngleTo(target.Position - unit.Position)) <= FiringTolerance + ArcEpsilon;
}

public sealed partial class CombatUnit
{
    /// <summary>Waist-joint angle in radians relative to leg Facing, not a world-space heading.</summary>
    public float TorsoTwist { get; internal set; }
    public Vector2 TorsoFacing => Facing.Rotated(TorsoTwist);
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
        if (!unit.Active) return null;
        if (unit.Order == CombatOrder.Attack && unit.AttackTarget is { } attack && ValidEnemy(unit, Unit(attack)))
            return Unit(attack);
        if (unit.PriorityTarget is { } priority && ValidEnemy(unit, Unit(priority))) return Unit(priority);
        return SelectTarget(unit);
    }
    private static void UpdateTorso(CombatUnit unit, CombatUnit? target, float delta)
    {
        var desired = target is null ? 0 : Math.Clamp(unit.Facing.AngleTo(target.Position - unit.Position),
            -CombatRules.MaxTorsoTwist, CombatRules.MaxTorsoTwist);
        unit.TorsoTwist = Mathf.MoveToward(unit.TorsoTwist, desired, unit.TorsoTurnRate * delta);
    }
    public CombatUnit? SelectRearTarget(CombatUnit unit)
    {
        if (!unit.Active) return null;
        bool Eligible(CombatUnit target) => ValidEnemy(unit, target)
            && Terrain.ClearSight(unit.Position, target.Position)
            && unit.Weapons.Any(weapon => weapon.Spec.Rear && weapon.Enabled && unit.WeaponUsable(weapon)
                && unit.Position.DistanceTo(target.Position) <= weapon.LongRange && CombatRules.InFiringArc(unit, target, weapon));
        if (unit.Order == CombatOrder.Attack && unit.AttackTarget is { } attack && Eligible(Unit(attack))) return Unit(attack);
        if (unit.Firing == FiringMode.HoldFire) return null;
        if (unit.PriorityTarget is { } priority && Eligible(Unit(priority))) return Unit(priority);
        if (unit.CurrentRearTarget is { } current && Eligible(Unit(current))) return Unit(current);
        return Units.Where(Eligible).OrderBy(target => unit.Position.DistanceSquaredTo(target.Position)).FirstOrDefault();
    }
    public bool CanFire(CombatUnit unit, CombatUnit target, CombatWeapon weapon) => unit.Active && weapon.Enabled
        && unit.WeaponUsable(weapon) && ValidEnemy(unit, target) && FirePermitted(unit, target)
        && unit.Position.DistanceTo(target.Position) <= weapon.LongRange
        && Terrain.ClearSight(unit.Position, target.Position) && CombatRules.InFiringArc(unit, target, weapon);

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
        volley.Any(shot => shot.Weapon == weapon) ? "Ready" : $"Cooling · {unit.HeatPolicy} heat policy";

    // A null result means only the shared heat-budget decision remains.
    private string? WeaponRestriction(CombatUnit unit, CombatWeapon weapon, CombatUnit? target)
    {
        if (unit.Shutdown && !unit.Defeated) return "Thermal shutdown";
        if (!unit.Active || !unit.WeaponUsable(weapon)) return "Unavailable";
        if (!weapon.Enabled) return "Disabled";
        if (target is null) return unit.Firing == FiringMode.HoldFire ? "Hold fire" : "No target";
        if (!FirePermitted(unit, target)) return "Hold fire";
        if (!ValidEnemy(unit, target)) return "No contact";
        if (unit.Position.DistanceTo(target.Position) > weapon.LongRange) return "Out of range";
        if (!Terrain.ClearSight(unit.Position, target.Position)) return "Line of fire blocked";
        if (!CombatRules.InFiringArc(unit, target, weapon))
            return weapon.Spec.Rear || CombatRules.IsLeg(weapon.Spec.Location)
                || Math.Abs(unit.Facing.AngleTo(target.Position - unit.Position)) > CombatRules.MaxTorsoTwist + CombatRules.FiringTolerance
                    ? "Outside firing arc" : "Turning to target";
        if (weapon.Remaining > .000001) return $"Ready in {weapon.Remaining:0.0}s";
        if (TerrainHitChance(unit, target, weapon) == 0) return "Cannot hit";
        return null;
    }
}
