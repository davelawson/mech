using Godot;

namespace Mech;

/// <summary>Control zone numbers, recorded in docs/combat-tuning.md.</summary>
public static partial class CombatRules
{
    /// <summary>A control zone's radius.</summary>
    public const float ControlZoneRadius = 500;
    /// <summary>A lone leading unit moves control from one end to the other in this time; each further leading unit adds as much again.</summary>
    public const double CaptureSeconds = 30;
}

/// <summary>What a step did to a control zone: nothing worth noting, control leaving its owner's end, or the owner changing.</summary>
public enum ControlZoneChange { None, Contested, OwnerChanged }

/// <summary>A control zone as a co-op player's replica needs it.</summary>
public sealed record ControlZoneState(double Control, CombatSide Owner);

/// <summary>A circle of ground two sides fight to control (see docs/combat-design.md, "Control zones"). Control runs from 0, held by the
/// opposition, to 1, held by the company. Only Fighting units inside count: sorted by distance to the centre, the unbroken run of
/// one side's units at the front leads, and control moves toward that side by the run's length every capture time. The owner
/// changes only when control reaches the other end; between the ends the zone is contested. It knows nothing of the mission it
/// serves, which reads its owner and changes.</summary>
public sealed class ControlZone(string name, Vector2 center, float radius, double captureSeconds, CombatSide owner)
{
    public string Name { get; } = name;
    public Vector2 Center { get; } = center;
    public float Radius { get; } = radius;
    public double CaptureSeconds { get; } = captureSeconds;
    /// <summary>0 when the opposition holds the zone outright, 1 when the company does.</summary>
    public double Control { get; private set; } = owner == CombatSide.Friendly ? 1 : 0;
    public CombatSide Owner { get; private set; } = owner;
    /// <summary>Control has moved off its owner's end.</summary>
    public bool Contested => Owner == CombatSide.Friendly ? Control < 1 : Control > 0;
    public bool Contains(Vector2 point) => point.DistanceSquaredTo(Center) <= Radius * Radius;

    /// <summary>The side leading the zone and how many of its units lead, or null with no Fighting unit inside.</summary>
    public (CombatSide Side, int Count)? Leaders(IEnumerable<CombatUnit> units)
    {
        var inside = units.Where(unit => unit.Fighting && Contains(unit.Position)).OrderBy(unit => unit.Position.DistanceSquaredTo(Center)).ToArray();
        if (inside.Length == 0) return null;
        var side = inside[0].Side;
        return (side, inside.TakeWhile(unit => unit.Side == side).Count());
    }

    /// <summary>Moves control for <paramref name="seconds"/> of battle. An empty zone keeps its control.</summary>
    internal ControlZoneChange Advance(IEnumerable<CombatUnit> units, double seconds)
    {
        if (Leaders(units) is not { } lead) return ControlZoneChange.None;
        var contested = Contested;
        Control = Math.Clamp(Control + lead.Count * seconds / CaptureSeconds * (lead.Side == CombatSide.Friendly ? 1 : -1), 0, 1);
        if (Control >= 1 - 1e-9) Control = 1;
        if (Control <= 1e-9) Control = 0;
        var end = Control == 1 ? CombatSide.Friendly : Control == 0 ? CombatSide.Enemy : (CombatSide?)null;
        if (end is { } side && side != Owner) { Owner = side; return ControlZoneChange.OwnerChanged; }
        return !contested && Contested ? ControlZoneChange.Contested : ControlZoneChange.None;
    }

    internal ControlZoneState State() => new(Control, Owner);
    internal void Apply(ControlZoneState state)
    {
        if (!double.IsFinite(state.Control) || state.Control is < 0 or > 1 || !Enum.IsDefined(state.Owner))
            throw new ArgumentException("The battle state does not match this battle.", nameof(state));
        Control = state.Control; Owner = state.Owner;
    }
}

public sealed partial class CombatSimulation
{
    private readonly List<ControlZone> _zones = [];
    /// <summary>The battle's control zones, in the order the mission set them up; none outside missions that use them.</summary>
    public IReadOnlyList<ControlZone> ControlZones => _zones;
    /// <summary>What this step did to each control zone, in zone order, for the mission's objectives to read.</summary>
    private ControlZoneChange[] _zoneChanges = [];

    private void AdvanceControlZones() =>
        _zoneChanges = _zones.Select(zone => zone.Advance(Units, CombatRules.StepSeconds)).ToArray();

    private ControlZoneState[]? ZoneStates() => _zones.Count == 0 ? null : _zones.Select(zone => zone.State()).ToArray();
    private void ApplyZones(ControlZoneState[]? states)
    {
        if ((states?.Length ?? 0) != _zones.Count) throw new ArgumentException("The battle state does not match this battle.", nameof(states));
        for (var i = 0; i < _zones.Count; i++) _zones[i].Apply(states![i]);
    }
}
