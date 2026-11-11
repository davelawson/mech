using Godot;

namespace Mech;

public sealed record CombatLocationState(MechLocation Location, int Armor, int RearArmor, int Structure);

/// <summary>Everything about a unit that changes in battle and that a player's screen shows. Pathing and enemy AI state stay
/// on the leader's machine, which alone steps the battle.</summary>
public sealed record CombatUnitState(string Id, Vector2 Position, Vector2 Destination, Vector2 Facing, bool Moving, float CurrentSpeed,
    float AngularVelocity, CombatOrder Order, string? AttackTarget, string? FollowTarget, string? PriorityTarget, string? CurrentTarget,
    string? CurrentRearTarget, MovementMode Movement, FiringMode Firing, float PreferredDistance, int ShotsFired, int Hits, int DamageDealt,
    double InterferenceRemaining, double Heat, HeatPolicy HeatPolicy, bool Shutdown, bool Airborne, Vector2 JumpFrom, Vector2 JumpTo,
    float JumpDuration, float JumpElapsed, float JumpFromHeight, float JumpToHeight, float JumpApexHeight, float Altitude, double JumpRecharge,
    double? LandedAt, float TorsoTwist, bool TorsoTurning, CombatLocationState[] Locations, bool[] WeaponsEnabled, double[] WeaponsRemaining,
    int[] Ammo, int Kills = 0);

public sealed record CombatContactState(CombatSide Side, string UnitId, ContactLevel Level, Vector2 LastKnownPosition, double LastSeenAt);

/// <summary>A battle as the leader's machine last stepped it, with the log entries from <see cref="HistoryStart"/> on.</summary>
public sealed record CombatState(double Elapsed, CombatOutcome Outcome, double? WindDownEnds, int[] PauseHolds, CombatUnitState[] Units,
    CombatContactState[] Contacts, int HistoryStart, CombatLogEntry[] History);

public sealed partial class CombatUnit
{
    internal CombatUnitState State() => new(Id, Position, Destination, Facing, Moving, CurrentSpeed, AngularVelocity, Order, AttackTarget,
        FollowTarget, PriorityTarget, CurrentTarget, CurrentRearTarget, Movement, Firing, PreferredDistance, ShotsFired, Hits, DamageDealt,
        InterferenceRemaining, Heat, HeatPolicy, Shutdown, Airborne, JumpFrom, JumpTo, JumpDuration, JumpElapsed, JumpFromHeight, JumpToHeight,
        JumpApexHeight, Altitude, JumpRecharge, LandedAt, TorsoTwist, TorsoTurning,
        Locations.Values.Select(item => new CombatLocationState(item.Location, item.Armor, item.RearArmor, item.Structure)).ToArray(),
        Weapons.Select(item => item.Enabled).ToArray(), Weapons.Select(item => item.Remaining).ToArray(), _ammo.Select(item => item.Shots).ToArray(), Kills);

    /// <summary>Takes on the leader's state, except the pose, which <see cref="CombatSimulation"/> blends toward it.</summary>
    internal void Apply(CombatUnitState state)
    {
        if (state.Locations.Length != Locations.Count || state.WeaponsEnabled.Length != Weapons.Count
            || state.WeaponsRemaining.Length != Weapons.Count || state.Ammo.Length != _ammo.Length)
            throw new ArgumentException("The battle state does not match this battle.", nameof(state));
        Destination = state.Destination; Moving = state.Moving; CurrentSpeed = state.CurrentSpeed; AngularVelocity = state.AngularVelocity;
        Order = state.Order; AttackTarget = state.AttackTarget; FollowTarget = state.FollowTarget; PriorityTarget = state.PriorityTarget;
        CurrentTarget = state.CurrentTarget; CurrentRearTarget = state.CurrentRearTarget; Movement = state.Movement; Firing = state.Firing;
        PreferredDistance = state.PreferredDistance; ShotsFired = state.ShotsFired; Hits = state.Hits; DamageDealt = state.DamageDealt; Kills = state.Kills;
        InterferenceRemaining = state.InterferenceRemaining; Heat = state.Heat; HeatPolicy = state.HeatPolicy; Shutdown = state.Shutdown;
        Airborne = state.Airborne; JumpFrom = state.JumpFrom; JumpTo = state.JumpTo; JumpDuration = state.JumpDuration;
        JumpElapsed = state.JumpElapsed; JumpFromHeight = state.JumpFromHeight; JumpToHeight = state.JumpToHeight;
        JumpApexHeight = state.JumpApexHeight; JumpRecharge = state.JumpRecharge; LandedAt = state.LandedAt; TorsoTurning = state.TorsoTurning;
        foreach (var location in state.Locations)
        {
            if (!Locations.TryGetValue(location.Location, out var part)) throw new ArgumentException("The battle state does not match this battle.", nameof(state));
            part.Armor = location.Armor; part.RearArmor = location.RearArmor; part.Structure = location.Structure;
        }
        for (var i = 0; i < Weapons.Count; i++) { Weapons[i].Enabled = state.WeaponsEnabled[i]; Weapons[i].Remaining = state.WeaponsRemaining[i]; }
        for (var i = 0; i < _ammo.Length; i++) _ammo[i] = _ammo[i] with { Shots = state.Ammo[i] };
    }
}

/// <summary>Where a unit is drawn: its position, leg and torso headings, and height on a jump.</summary>
internal readonly record struct CombatPose(Vector2 Position, Vector2 Facing, float TorsoTwist, float Altitude)
{
    public static CombatPose Of(CombatUnit unit) => new(unit.Position, unit.Facing, unit.TorsoTwist, unit.Altitude);
    public static CombatPose Of(CombatUnitState state) => new(state.Position, state.Facing, state.TorsoTwist, state.Altitude);
    public CombatPose Blend(CombatPose to, float weight) => weight >= 1 ? to : new(Position.Lerp(to.Position, weight),
        Facing.Slerp(to.Facing, weight).Normalized(), Mathf.LerpAngle(TorsoTwist, to.TorsoTwist, weight), Mathf.Lerp(Altitude, to.Altitude, weight));
    public void Place(CombatUnit unit)
    { unit.Position = Position; unit.Facing = Facing; unit.TorsoTwist = TorsoTwist; unit.Altitude = Altitude; }
}

/// <summary>Co-op battles are stepped only on the leader's machine. Each other player holds a replica: the same deployment, which
/// never steps, and takes on each state the leader sends. Between states its mechs glide toward their latest pose.</summary>
public sealed partial class CombatSimulation
{
    /// <summary>The leader sends the battle this often while it runs.</summary>
    public const double StateInterval = 1 / 15d;
    /// <summary>A replica's clock runs at most this far past the leader's last state, so effects keep moving between states.</summary>
    private const double ReplicaLead = 3 * StateInterval;
    /// <summary>This copy shows the leader's battle rather than fighting its own.</summary>
    public bool Replica { get; private set; }
    private readonly Dictionary<string, (CombatPose From, CombatPose To)> _poses = [];
    private double _sinceState;

    internal void BecomeReplica() { Replica = true; EnemyAiEnabled = false; }

    /// <summary>The battle now, with the log entries from <paramref name="historyStart"/> on.</summary>
    public CombatState State(int historyStart = 0)
    {
        historyStart = Math.Clamp(historyStart, 0, _history.Count);
        return new(Elapsed, Outcome, WindDownEnds, [.. _pauseHolds.Order()], Units.Select(unit => unit.State()).ToArray(),
            _contacts.SelectMany(side => side.Value.Values.Select(contact => new CombatContactState(side.Key, contact.UnitId, contact.Level,
                contact.LastKnownPosition, contact.LastSeenAt))).ToArray(),
            historyStart, _history.Skip(historyStart).ToArray());
    }

    /// <summary>A replica takes on the leader's battle: log entries it lacks are added and their shots fired, in order.</summary>
    public void Apply(CombatState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Replica) throw new InvalidOperationException("Only a replica takes on another battle's state.");
        if (state.Units.Length != Units.Count || state.Units.Any(item => !Units.Any(unit => unit.Id == item.Id))
            || state.HistoryStart > _history.Count || state.Contacts.Any(item => !Units.Any(unit => unit.Id == item.UnitId)))
            throw new ArgumentException("The battle state does not match this battle.", nameof(state));
        var clock = Clock;
        Elapsed = state.Elapsed; Outcome = state.Outcome; WindDownEnds = state.WindDownEnds;
        _accumulator = Math.Clamp(clock - Elapsed, 0, ReplicaLead);
        _pauseHolds.Clear(); _pauseHolds.UnionWith(state.PauseHolds);
        foreach (var item in state.Units)
        {
            var unit = Unit(item.Id); unit.Apply(item);
            _poses[unit.Id] = (CombatPose.Of(unit), CombatPose.Of(item));
        }
        _sinceState = 0;
        // A stopped battle shows exactly where the leader's mechs stand.
        if (Paused || Outcome != CombatOutcome.Ongoing) Blend(1);
        foreach (var side in _contacts.Values) side.Clear();
        foreach (var item in state.Contacts)
            _contacts[item.Side][item.UnitId] = new CombatContact(item.UnitId) { Level = item.Level, LastKnownPosition = item.LastKnownPosition, LastSeenAt = item.LastSeenAt };
        foreach (var entry in state.History.Skip(_history.Count - state.HistoryStart))
        { _history.Add(entry); ShotFired?.Invoke(entry.Shot); }
    }

    private void AdvanceReplica(double delta)
    {
        if (Paused || Outcome != CombatOutcome.Ongoing) { Blend(1); return; }
        _sinceState += delta; _accumulator = Math.Min(ReplicaLead, _accumulator + delta);
        Blend((float)Math.Min(1, _sinceState / StateInterval));
    }

    private void Blend(float weight)
    {
        foreach (var (id, (from, to)) in _poses) from.Blend(to, weight).Place(Unit(id));
    }
}
