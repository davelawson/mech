using Godot;

namespace Mech;

/// <summary>Take and Hold numbers, recorded in docs/combat-tuning.md.</summary>
public static partial class CombatRules
{
    /// <summary>The objective's centre lies within this distance of the arena centre, so a wave has room to arrive on every side.</summary>
    public const float ObjectiveOffset = 800;
    /// <summary>The objective settlement has this many buildings, which keeps it inside its control zone.</summary>
    public const int MinObjectiveBuildings = 15, MaxObjectiveBuildings = 25;
    /// <summary>The garrison's posts keep this far apart.</summary>
    public const float GarrisonSpacing = 40;
    /// <summary>The garrison chases company mechs no further than this beyond the zone's edge before returning to its posts.</summary>
    public const float GarrisonLeash = 250;
    /// <summary>A wave arrives this far beyond the zone's edge, and keeps this far inside the arena's edges.</summary>
    public const float WaveSpawnDistance = 1500, WaveSpawnPadding = 200;
    /// <summary>Each of a wave's lances lands at a site within this distance of the wave's arrival point.</summary>
    public const float WaveLanceSpread = 200;
    /// <summary>The next wave arrives this long after the last one (or the capture), or this long after no enemy is left fighting,
    /// whichever comes first.</summary>
    public const double WaveIntervalSeconds = 180, WaveClearSeconds = 5;
    /// <summary>A wave mech heads for the objective until it is this close to the centre.</summary>
    public const float WaveHoldDistance = 250;
}

/// <summary>Where a Take and Hold is fought: the company's drop edge and the objective settlement, all from the terrain seed.</summary>
public sealed record TakeAndHoldLayout(ArenaEdge DropEdge, Vector2 Objective, int Buildings)
{
    /// <summary>Lays out a Take and Hold and generates its terrain, so every co-op player's replica rebuilds the same battlefield.</summary>
    public static (TakeAndHoldLayout Layout, BattlefieldTerrain Terrain) Create(int terrainSeed, Biome biome)
    {
        var random = new Random(terrainSeed ^ 0x4B1D7A3);
        var edge = DropZone.StandardEdge(terrainSeed);
        var buildings = CombatRules.MinObjectiveBuildings + random.Next(CombatRules.MaxObjectiveBuildings - CombatRules.MinObjectiveBuildings + 1);
        var center = Vector2.FromAngle(random.NextSingle() * Mathf.Tau) * (CombatRules.ObjectiveOffset * MathF.Sqrt(random.NextSingle()));
        center = new(MathF.Round(center.X), MathF.Round(center.Y));
        var terrain = BattlefieldTerrain.Generate(terrainSeed, biome, dropEdge: edge, objective: new(center, buildings));
        return (new(edge, center, buildings), terrain);
    }
}

/// <summary>What a co-op player's replica needs of a Take and Hold: the waves arrived, the capture and the clocks behind the next wave,
/// whether the enemy retook the objective, and the events so far.</summary>
public sealed record TakeAndHoldProgress(int Arrived, double? TakenAt, double? LastArrivalAt, double? ClearSince, bool Retaken, PatrolEvent[] Events);

/// <summary>A Take and Hold in progress (see docs/combat-design.md, "Take and Hold").</summary>
public sealed class TakeAndHoldState(TakeAndHoldLayout layout, int waves)
{
    public TakeAndHoldLayout Layout { get; } = layout;
    /// <summary>Reinforcement waves after the garrison.</summary>
    public int Waves { get; } = waves;
    /// <summary>Waves that have arrived; the next is wave Arrived + 1.</summary>
    public int Arrived { get; internal set; }
    /// <summary>When the company first took the objective.</summary>
    public double? TakenAt { get; internal set; }
    public bool Taken => TakenAt is not null;
    public double? LastArrivalAt { get; internal set; }
    /// <summary>Since when no enemy on the field has been fighting, counted only once the objective is taken.</summary>
    public double? ClearSince { get; internal set; }
    /// <summary>The enemy took the objective back after the company had taken it.</summary>
    public bool Retaken { get; internal set; }
    /// <summary>When the next wave arrives if nothing hurries it; null before the capture and once every wave has arrived.</summary>
    public double? NextWaveAt => TakenAt is not { } taken || Arrived >= Waves ? null
        : Math.Min((LastArrivalAt ?? taken) + CombatRules.WaveIntervalSeconds, ClearSince is { } clear ? clear + CombatRules.WaveClearSeconds : double.PositiveInfinity);
    internal List<PatrolEvent> EventList { get; } = [];
    public IReadOnlyList<PatrolEvent> Events => EventList;
    /// <summary>The first company mech into the zone, while it can still earn the bonus for holding it until the capture.</summary>
    internal string? FirstIn { get; set; }
    internal bool FirstInLost { get; set; }
    /// <summary>Control has risen since the first company mech entered.</summary>
    internal bool ControlRose { get; set; }

    internal TakeAndHoldProgress Progress() => new(Arrived, TakenAt, LastArrivalAt, ClearSince, Retaken, [.. EventList]);
    internal void Apply(TakeAndHoldProgress progress)
    {
        if (progress.Arrived < 0 || progress.Arrived > Waves || progress.Events is null)
            throw new ArgumentException("The battle state does not match this battle.", nameof(progress));
        Arrived = progress.Arrived; TakenAt = progress.TakenAt; LastArrivalAt = progress.LastArrivalAt; ClearSince = progress.ClearSince;
        Retaken = progress.Retaken;
        EventList.Clear(); EventList.AddRange(progress.Events);
    }
}

public sealed partial class CombatSimulation
{
    /// <summary>The Take and Hold this battle fights, or null for any other battle.</summary>
    public TakeAndHoldState? TakeAndHold { get; private set; }
    /// <summary>A Take and Hold's objective: its first control zone.</summary>
    public ControlZone? Objective => TakeAndHold is null ? null : _zones[0];
    private Random? _holdRandom;

    /// <summary>The objective's events or a patrol's, for the battle log.</summary>
    public IReadOnlyList<PatrolEvent> ObjectiveEvents => Patrol?.Events ?? TakeAndHold?.Events ?? (IReadOnlyList<PatrolEvent>)[];

    private static CombatSimulation DeployTakeAndHold(IReadOnlyList<DeployedMech> friendly, IReadOnlyList<DeployedMech> enemy, int seed, int terrainSeed,
        Biome biome, bool dropPhase)
    {
        if (enemy.Any(mech => mech.Wave is not { } wave || wave < 0) || !enemy.Any(mech => mech.Wave == 0))
            throw new ArgumentException("A Take and Hold's opposition must be a garrison and its waves.");
        var (layout, terrain) = TakeAndHoldLayout.Create(terrainSeed, biome);
        var drop = DropZone.Standard(layout.DropEdge);
        var component = terrain.Component(drop.Anchor);
        var random = new Random(terrainSeed ^ 0x6C8E9CF5);
        var units = friendly.Select(mech => new CombatUnit(mech.Id, mech.Spec, mech.Pilot, CombatSide.Friendly, mech.Locations, mech.Equipment,
            mech.Ammo, Vector2.Zero, mech.Lance) { Progress = mech.Progress ?? SkillProgress.None }).ToList();
        // The garrison stands at posts spread across the zone, on ground connected to the company's; the waves wait off the field.
        var posts = new List<Vector2>();
        for (var i = 0; i < enemy.Count; i++)
        {
            var wave = enemy[i].Wave!.Value;
            var position = layout.Objective;
            if (wave == 0)
            {
                var found = false;
                for (var attempt = 0; attempt < 1000 && !found; attempt++)
                {
                    position = layout.Objective + Vector2.FromAngle(random.NextSingle() * Mathf.Tau)
                        * (CombatRules.ControlZoneRadius * .9f * MathF.Sqrt(random.NextSingle()));
                    found = terrain.Passable(position) && terrain.Component(position) == component
                        && posts.All(post => post.DistanceTo(position) >= CombatRules.GarrisonSpacing);
                }
                if (!found) position = terrain.ReachableDestination(drop.Anchor, layout.Objective);
                posts.Add(position);
            }
            units.Add(new CombatUnit($"enemy-{i + 1}", enemy[i].Spec, enemy[i].Pilot, CombatSide.Enemy, enemy[i].Locations, enemy[i].Equipment,
                enemy[i].Ammo, position) { Wave = wave, Inbound = wave > 0, Facing = Vector2.FromAngle(random.NextSingle() * Mathf.Tau) });
        }
        var simulation = new CombatSimulation(units, seed, terrain);
        simulation.TakeAndHold = new TakeAndHoldState(layout, enemy.Max(mech => mech.Wave!.Value));
        simulation._zones.Add(new ControlZone("OBJECTIVE", layout.Objective, CombatRules.ControlZoneRadius, CombatRules.CaptureSeconds, CombatSide.Enemy));
        simulation._holdRandom = new Random(unchecked(seed ^ 0x2C1B3A4D));
        simulation.LandAll(drop, null, dropPhase);
        return simulation;
    }

    /// <summary>The garrison attacks company mechs it detects near the zone and otherwise returns to its posts; waves head for the
    /// objective's centre, firing as they go, and attack once inside the zone.</summary>
    private void UpdateTakeAndHoldAi()
    {
        var zone = Objective!;
        var company = Units.Where(unit => unit.Side == CombatSide.Friendly && !unit.Defeated && Detected(CombatSide.Enemy, unit.Id)).ToArray();
        var reach = zone.Radius + CombatRules.GarrisonLeash;
        var inReach = company.Where(target => target.Position.DistanceTo(zone.Center) <= reach).ToArray();
        (Vector2, float)? leash = (zone.Center, reach);
        var index = 0;
        foreach (var unit in Units.Where(item => item.Side == CombatSide.Enemy && item.Active && !item.Fleeing && !item.Unarmed && item.Wave is not null))
        {
            index++;
            var distance = unit.Position.DistanceTo(zone.Center);
            if (unit.Wave == 0)
            {
                if (distance <= reach && inReach.Length > 0) Engage(unit, inReach, leash);
                else Head(unit, unit.Post);
                continue;
            }
            var goal = zone.Center + Vector2.FromAngle(index * 2.4f) * (40 * MathF.Sqrt(index % 8));
            if (distance > zone.Radius) Head(unit, goal);
            else if (inReach.Length > 0) Engage(unit, inReach, leash);
            else if (distance > CombatRules.WaveHoldDistance) Head(unit, goal);
        }
    }
    private static void AttackWith(CombatUnit unit, CombatUnit target)
    {
        if (unit.Order == CombatOrder.Attack && unit.AttackTarget == target.Id) return;
        unit.Order = CombatOrder.Attack; unit.AttackTarget = target.Id; unit.FollowTarget = null; unit.NextFiringSearch = 0;
    }
    /// <summary>Sends a mech toward a point unless it is already there or on its way.</summary>
    private void Head(CombatUnit unit, Vector2 goal)
    {
        if (unit.Position.DistanceTo(goal) < 30) { if (unit.Order == CombatOrder.Attack) Hold([unit.Id]); return; }
        if (unit.Order == CombatOrder.Move && unit.Destination.DistanceTo(goal) < 150) return;
        Move([unit.Id], goal);
    }

    /// <summary>Tracks the first company mech in, the capture, the waves and the outcome. Victory needs every wave arrived, no enemy
    /// fighting and the objective in the company's hands; the enemy retaking it, or the company being beaten, is a defeat.</summary>
    private void UpdateTakeAndHoldObjectives()
    {
        var hold = TakeAndHold!; var zone = Objective!;
        var change = _zoneChanges[0];
        if (!hold.Taken) TrackFirstIn(hold, zone);
        if (change == ControlZoneChange.Contested) hold.EventList.Add(new(Elapsed, "Objective contested"));
        if (change == ControlZoneChange.OwnerChanged && zone.Owner == CombatSide.Friendly && !hold.Taken)
        {
            hold.TakenAt = Elapsed;
            hold.EventList.Add(new(Elapsed, "Objective taken"));
            if (Units.Where(unit => unit.Side == CombatSide.Friendly && unit.Fighting && zone.Contains(unit.Position))
                .MinBy(unit => unit.Position.DistanceSquaredTo(zone.Center)) is { } taker)
                Highlight(taker, $"{Name(taker)} took the objective.");
            if (hold.FirstIn is { } id && !hold.FirstInLost)
            {
                var leader = Unit(id);
                leader.Use = leader.Use with { LedAdvanceAt = Elapsed };
                Highlight(leader, $"{Name(leader)} led the advance and held the objective.");
                hold.EventList.Add(new(Elapsed, $"{leader.Warrior.Callsign} led the advance and held the objective"));
            }
        }
        else if (change == ControlZoneChange.OwnerChanged && zone.Owner == CombatSide.Enemy && hold.Taken)
        {
            hold.Retaken = true;
            hold.EventList.Add(new(Elapsed, "Objective retaken by the enemy"));
        }
        var friendly = Units.Any(unit => unit.Side == CombatSide.Friendly && unit.Fighting);
        var enemy = Units.Any(unit => unit.Side == CombatSide.Enemy && unit.Fighting);
        if (hold.Taken && !hold.Retaken && WindDownEnds is null && hold.Arrived < hold.Waves)
        {
            if (enemy) hold.ClearSince = null; else hold.ClearSince ??= Elapsed;
            if (hold.NextWaveAt is { } due && Elapsed + 1e-6 >= due) { ArriveWave(hold, zone); enemy = true; }
        }
        var held = hold.Taken && hold.Arrived >= hold.Waves && !enemy && zone.Owner == CombatSide.Friendly;
        if (WindDownEnds is null && (hold.Retaken || !friendly || held)) WindDownEnds = Elapsed + CombatRules.WindDownSeconds;
        if (WindDownEnds is { } ends && Elapsed + 1e-6 >= ends)
        {
            Outcome = hold.Retaken ? CombatOutcome.Defeat : held ? friendly ? CombatOutcome.Victory : CombatOutcome.Draw : CombatOutcome.Defeat;
            Paused = true;
        }
    }

    /// <summary>The first company mech to come into the zone while fighting can lead the advance. It loses the chance by leaving the
    /// zone, being defeated, or the enemy driving control back to nothing before the capture; nobody else takes its place.</summary>
    private void TrackFirstIn(TakeAndHoldState hold, ControlZone zone)
    {
        if (hold.FirstInLost) return;
        if (hold.FirstIn is not { } id)
        {
            hold.FirstIn = Units.Where(unit => unit.Side == CombatSide.Friendly && unit.Fighting && zone.Contains(unit.Position))
                .MinBy(unit => unit.Position.DistanceSquaredTo(zone.Center))?.Id;
            return;
        }
        var first = Unit(id);
        if (zone.Control > 0) hold.ControlRose = true;
        if (first.Defeated || !zone.Contains(first.Position) || hold.ControlRose && zone.Control <= 0) hold.FirstInLost = true;
    }

    private static readonly string[] Compass = ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];
    /// <summary>"north-east": the direction from <paramref name="from"/> to <paramref name="to"/>, north being −Y.</summary>
    public static string Bearing(Vector2 from, Vector2 to)
    {
        var offset = to - from;
        var turns = MathF.Atan2(offset.X, -offset.Y) / Mathf.Tau;
        return Compass[((int)MathF.Round(turns * 8) % 8 + 8) % 8];
    }

    /// <summary>The next wave arrives at a point beyond the zone on a random bearing, its lances landing like a drop around it.</summary>
    private void ArriveWave(TakeAndHoldState hold, ControlZone zone)
    {
        var wave = hold.Arrived + 1;
        var members = Units.Where(unit => unit.Wave == wave).ToArray();
        var anchor = FriendlyZone!.Anchor;
        var component = Terrain.Component(anchor);
        var limit = CombatRules.ArenaHalfSize - CombatRules.WaveSpawnPadding;
        var spawn = zone.Center;
        for (var attempt = 0; attempt < 400; attempt++)
        {
            var point = zone.Center + Vector2.FromAngle(_holdRandom!.NextSingle() * Mathf.Tau) * (zone.Radius + CombatRules.WaveSpawnDistance);
            if (Math.Abs(point.X) > limit || Math.Abs(point.Y) > limit) continue;
            spawn = point;
            if (Terrain.Passable(point) && Terrain.Component(point) == component) break;
        }
        var placed = Units.Where(unit => unit.Solid).Select(unit => unit.Position).ToList();
        foreach (var lance in members.GroupBy(unit => unit.TacticalGroup).Select(lance => lance.ToArray()))
        {
            Vector2[]? points = null;
            for (var attempt = 0; attempt < 400 && points is null; attempt++)
            {
                var site = attempt == 0 ? spawn : spawn + Vector2.FromAngle(_holdRandom!.NextSingle() * Mathf.Tau)
                    * (CombatRules.WaveLanceSpread * MathF.Sqrt(_holdRandom.NextSingle()));
                if (Math.Abs(site.X) > limit || Math.Abs(site.Y) > limit) continue;
                points = ScatterWave(site, lance.Length, component, placed);
            }
            // A battlefield with no room near the arrival point lands the lance on reachable ground toward the objective.
            points ??= lance.Select((_, i) => Terrain.ReachableDestination(anchor, spawn + Vector2.FromAngle(i * 2.4f) * (50 * i))).ToArray();
            for (var i = 0; i < lance.Length; i++)
            {
                var unit = lance[i];
                unit.Inbound = false;
                unit.Position = unit.Destination = unit.Post = points[i]; unit.Route = Array.Empty<Vector2>();
                var facing = zone.Center - points[i];
                unit.Facing = facing.LengthSquared() > 1 ? facing.Normalized() : Vector2.Up;
                unit.Order = CombatOrder.Hold; unit.AttackTarget = unit.FollowTarget = null;
                placed.Add(points[i]);
            }
        }
        hold.Arrived = wave; hold.LastArrivalAt = Elapsed; hold.ClearSince = null;
        var bearing = Bearing(zone.Center, spawn);
        hold.EventList.Add(new(Elapsed, $"Reinforcement wave {wave} of {hold.Waves} arriving from the {bearing}"));
        if (members.Length > 0) Highlight(members[0], $"Reinforcement wave {wave} of {hold.Waves} arrived from the {bearing}.");
    }

    /// <summary>A lance's landing points around a site: passable, connected to the company's ground, and clear of every mech standing.</summary>
    private Vector2[]? ScatterWave(Vector2 site, int count, int component, IReadOnlyList<Vector2> occupied)
    {
        var points = new List<Vector2>();
        for (var attempt = 0; attempt < 300 && points.Count < count; attempt++)
        {
            var point = site + Vector2.FromAngle(_holdRandom!.NextSingle() * Mathf.Tau) * (CombatRules.DropSiteRadius * MathF.Sqrt(_holdRandom.NextSingle()));
            if (!Terrain.Passable(point) || Terrain.Component(point) != component
                || points.Concat(occupied).Any(other => other.DistanceTo(point) < CombatRules.DropMechSpacing)) continue;
            points.Add(point);
        }
        return points.Count == count ? [.. points] : null;
    }

    /// <summary>One line describing the objective, for the combat screen.</summary>
    public string? TakeAndHoldObjectiveText() => TakeAndHold is not { } hold ? null
        : !hold.Taken ? "Take the objective" : hold.Arrived < hold.Waves ? "Hold the objective" : "Destroy the remaining enemies";

    /// <summary>"Reinforcements: wave 1 of 3 in 2:14", for the line under the control bar.</summary>
    public string? ReinforcementText()
    {
        if (TakeAndHold is not { } hold) return null;
        string Waves(int count) => $"{count} {(count == 1 ? "wave" : "waves")}";
        if (!hold.Taken) return $"Reinforcements: {Waves(hold.Waves)} once the objective is taken";
        if (hold.NextWaveAt is { } due)
            return $"Reinforcements: wave {hold.Arrived + 1} of {hold.Waves} in {TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, due - Elapsed))):m\\:ss}";
        return hold.Waves == 1 ? "Reinforcements: the wave has arrived" : $"Reinforcements: all {Waves(hold.Waves)} have arrived";
    }

    /// <summary>The results' line on the objective: held on a victory, retaken when the enemy took it back.</summary>
    public string? TakeAndHoldResultText() => TakeAndHold is not { } hold ? null
        : hold.Retaken ? "The objective was retaken." : Outcome == CombatOutcome.Victory ? "The objective was held." : null;
}
