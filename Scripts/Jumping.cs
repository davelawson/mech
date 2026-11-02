using Godot;

namespace Mech;

public static partial class CombatRules
{
    /// <summary>Jump range per effective jump MP: half a weapon-range hex.</summary>
    public const float JumpMetresPerMp = 45;
    public const double JumpRechargeSeconds = 10;
    public const int JumpToHitModifier = 3;
    /// <summary>Landings closer than this to the takeoff point are not worth a jump.</summary>
    public const float MinimumJump = 15;
    public static float JumpFlightSeconds(float distance) => 1.5f + distance / 60;
    public static float JumpApex(float distance) => Math.Max(20, .2f * distance);
    /// <summary>One heat per jump MP used, at least three, as on the tabletop.</summary>
    public static int JumpHeat(float distance) => Math.Max(3, (int)Math.Ceiling(distance / JumpMetresPerMp - .0001f));
}

public sealed partial class CombatUnit
{
    public bool Airborne { get; internal set; }
    public Vector2 JumpFrom { get; internal set; }
    public Vector2 JumpTo { get; internal set; }
    public float JumpDuration { get; internal set; }
    public float JumpElapsed { get; internal set; }
    internal float JumpFromHeight { get; set; }
    internal float JumpToHeight { get; set; }
    internal float JumpApexHeight { get; set; }
    /// <summary>Height of the jump arc above the ground beneath the mech; zero on the ground.</summary>
    public float Altitude { get; internal set; }
    /// <summary>Flight progress from takeoff (0) to landing (1), or null on the ground.</summary>
    public float? JumpPhase => Airborne ? Math.Clamp(JumpElapsed / JumpDuration, 0, 1) : null;
    public double JumpRecharge { get; internal set; }
    /// <summary>Simulation time of the last landing, for touchdown effects.</summary>
    public double? LandedAt { get; internal set; }
    // Catalog jets are split into one component per jet; each destroyed or lost jet removes one jump MP.
    public int EffectiveJumpMp => Math.Max(0, Spec.Jump - Spec.Components
        .Where(component => component.Kind == ComponentKind.JumpJet
            && (!Intact(component.Location) || Equipment[component.Id] == ComponentCondition.Destroyed))
        .Sum(component => component.Slots));
    /// <summary>Jets still working in one location, for its exhaust.</summary>
    public int WorkingJumpJets(MechLocation location) => !Intact(location) ? 0 : Spec.Components
        .Where(component => component.Kind == ComponentKind.JumpJet && component.Location == location
            && Equipment[component.Id] != ComponentCondition.Destroyed)
        .Sum(component => component.Slots);
    public float JumpRange => EffectiveJumpMp * CombatRules.JumpMetresPerMp * Mobility;
    /// <summary>Why this unit cannot start the shortest jump now, or null when it can.</summary>
    public string? JumpUnavailableReason => Spec.Jump == 0 ? "No jump jets" : Defeated ? Status.ToString()
        : JumpRange <= 0 ? "Jump jets lost" : Shutdown ? "Shut down" : Airborne ? "Airborne"
        : JumpRecharge > 0 ? "Recharging" : !JumpHeatPermitted(CombatRules.MinimumJump) ? "Too hot" : null;
    public bool JumpHeatPermitted(float distance) => Heat + CombatRules.JumpHeat(distance) < CombatRules.HeatLimit(HeatPolicy);
}

public sealed partial class CombatSimulation
{
    /// <summary>Clear sight between two mechs, from wherever each is on its jump arc.</summary>
    public bool Sight(CombatUnit from, CombatUnit to) => Terrain.ClearSight(from.Position, to.Position, from.Altitude, to.Altitude);

    /// <summary>Sends each able unit on one ballistic jump toward the point, landing on separate free ground within range.
    /// Attack and Follow orders resume on landing; a Move becomes Hold at the landing point.</summary>
    public void Jump(IEnumerable<string> ids, Vector2 point)
    {
        var group = ids.Distinct().Select(Unit).Where(item => item.JumpUnavailableReason is null).ToArray();
        var assigned = new List<Vector2>();
        for (var i = 0; i < group.Length; i++)
        {
            var offset = new Vector2((i % 2 - (group.Length > 1 ? .5f : 0)) * 36, i / 2 * 36);
            if (LandingPoint(group[i], point + offset, assigned) is not { } landing) continue;
            if (TakeOff(group[i], landing)) assigned.Add(landing);
        }
    }

    private Vector2? LandingPoint(CombatUnit unit, Vector2 desired, IReadOnlyList<Vector2> assigned)
    {
        Vector2 Reach(Vector2 point) => unit.Position + (point - unit.Position).LimitLength(unit.JumpRange);
        bool Free(Vector2 point) => Terrain.Passable(point) && unit.Position.DistanceTo(point) >= CombatRules.MinimumJump
            && !assigned.Any(other => other.DistanceTo(point) < CombatRules.UnitRadius * 2.1f)
            && !Units.Any(other => other != unit && other.Status != CombatStatus.Destroyed
                && (other.Airborne ? other.JumpTo : other.Position).DistanceTo(point) < CombatRules.UnitRadius * 2.1f);
        var target = Reach(desired);
        for (var attempt = 0; attempt < 160; attempt++)
        {
            var candidate = attempt == 0 ? target : Reach(target + Vector2.FromAngle(attempt * 2.4f) * (attempt * 4.5f));
            if (Free(candidate)) return candidate;
        }
        return null;
    }

    private bool TakeOff(CombatUnit unit, Vector2 landing)
    {
        var distance = unit.Position.DistanceTo(landing);
        if (unit.JumpUnavailableReason is not null || distance < CombatRules.MinimumJump || !unit.JumpHeatPermitted(distance)) return false;
        unit.JumpFrom = unit.Position; unit.JumpTo = landing;
        unit.JumpFromHeight = Terrain.HeightAt(unit.Position); unit.JumpToHeight = Terrain.HeightAt(landing);
        // Rise at least high enough to clear every rock and ridge beneath the arc.
        var apex = CombatRules.JumpApex(distance);
        var steps = Math.Max(2, (int)Math.Ceiling(distance / 7.5f));
        for (var i = 1; i < steps; i++)
        {
            var t = i / (float)steps;
            var clearance = Terrain.ObstacleTop(unit.JumpFrom.Lerp(landing, t)) + 6 - Mathf.Lerp(unit.JumpFromHeight, unit.JumpToHeight, t);
            apex = Math.Max(apex, clearance / (4 * t * (1 - t)));
        }
        unit.JumpApexHeight = apex;
        unit.JumpDuration = CombatRules.JumpFlightSeconds(distance); unit.JumpElapsed = 0; unit.Airborne = true;
        unit.Facing = (landing - unit.Position).Normalized();
        unit.CurrentSpeed = unit.AngularVelocity = 0; unit.Route = Array.Empty<Vector2>();
        // The jump completes a Move; a new Move given in flight is resumed on landing.
        if (unit.Order == CombatOrder.Move) unit.Order = CombatOrder.Hold;
        unit.AddHeat(CombatRules.JumpHeat(distance));
        return true;
    }

    /// <summary>Flight is ballistic: shutdown, disablement or destruction in the air never stops a landing.</summary>
    private void AdvanceJump(CombatUnit unit, float delta)
    {
        unit.JumpElapsed = Math.Min(unit.JumpDuration, unit.JumpElapsed + delta);
        var t = unit.JumpElapsed / unit.JumpDuration;
        unit.Position = unit.JumpFrom.Lerp(unit.JumpTo, t);
        var height = Mathf.Lerp(unit.JumpFromHeight, unit.JumpToHeight, t) + 4 * unit.JumpApexHeight * t * (1 - t);
        unit.Altitude = Math.Max(0, height - Terrain.HeightAt(unit.Position));
        unit.Moving = true;
        if (t < 1) return;
        unit.Airborne = false; unit.Altitude = 0; unit.Position = unit.JumpTo;
        unit.JumpRecharge = CombatRules.JumpRechargeSeconds; unit.LandedAt = Elapsed;
        if (unit.Order == CombatOrder.Hold) unit.Destination = unit.Position;
    }

    /// <summary>Enemy AI jumps toward its movement goal when walking there means a long detour or it cannot see its target.</summary>
    private bool AiJump(CombatUnit unit, Vector2 goal)
    {
        if (!EnemyAiEnabled || unit.Side != CombatSide.Enemy || unit.JumpUnavailableReason is not null) return false;
        var straight = unit.Position.DistanceTo(goal);
        if (straight < 60) return false;
        var walked = unit.Route.Count == 0 ? straight : unit.Position.DistanceTo(unit.Route[unit.RouteIndex])
            + Enumerable.Range(unit.RouteIndex + 1, Math.Max(0, unit.Route.Count - unit.RouteIndex - 1))
                .Sum(i => unit.Route[i - 1].DistanceTo(unit.Route[i]));
        var blind = unit.Order == CombatOrder.Attack && unit.AttackTarget is { } id && Detected(unit.Side, id) && !Sight(unit, Unit(id));
        if (walked < 1.5f * straight && !blind) return false;
        var landing = LandingPoint(unit, unit.Position + (goal - unit.Position).LimitLength(unit.JumpRange), []);
        return landing is { } point && TakeOff(unit, point);
    }
}
