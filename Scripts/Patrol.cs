using Godot;

namespace Mech;

/// <summary>Patrol numbers, recorded in docs/combat-tuning.md.</summary>
public static partial class CombatRules
{
    /// <summary>A company mech this close to the current patrol point visits it.</summary>
    public const float PatrolVisitRadius = 100;
    /// <summary>The extraction zone's radius; its centre lies this far in from its edge, so the zone touches the edge.</summary>
    public const float ExtractionRadius = 150;
    /// <summary>Patrol points keep this far from every edge and, where the arena allows, this far from each other, the start and extraction.</summary>
    public const float PatrolEdgeMargin = 400, PatrolPointSpacing = 800;
    /// <summary>The company deploys this far in from its edge, the depth of the standard deployment line.</summary>
    public const float PatrolStartInset = ArenaHalfSize - DeploymentDepth;
    /// <summary>At most this many mechs share a start row; further rows form 120 m nearer the edge.</summary>
    public const int PatrolRowSize = 8;
    public const float PatrolRowSpacing = 120;
    /// <summary>A group's post is the middle of its leg, moved sideways by up to this much.</summary>
    public const float PatrolGroupOffset = 300;
    /// <summary>Terrain generation keeps these radii clear around each start position, patrol point and the extraction zone.</summary>
    public const float PatrolClearing = 150, ExtractionClearing = 200;
    /// <summary>A passive group wakes this long after an attack on one of its mechs resolves, hit or miss.</summary>
    public const double PatrolReactionSeconds = 1;
    /// <summary>A group that loses the company searches within this distance of where it was last known, for this long after
    /// reaching it, before returning to its posts.</summary>
    public const float PatrolSearchRadius = 500;
    public const double PatrolSearchSeconds = 30;
    /// <summary>A group has reached a last known position once any of its mechs is this close, or all have stopped.</summary>
    public const float PatrolArrival = 60;
}

/// <summary>A side of the square arena. South is +Y, where the standard deployment line stands.</summary>
public enum ArenaEdge { South, North, East, West }

/// <summary>Where a patrol is fought: the company's starting positions, the locations in visiting order with the extraction zone
/// last, and one post per enemy group, on the leg between location g and location g + 1.</summary>
public sealed record PatrolLayout(ArenaEdge StartEdge, ArenaEdge ExtractionEdge, Vector2[] Starts, Vector2[] Locations, Vector2[] Posts)
{
    public Vector2 Extraction => Locations[^1];
    public int PatrolPoints => Locations.Length - 1;
    public Vector2 StartCenter => Starts.Aggregate(Vector2.Zero, (sum, point) => sum + point) / Starts.Length;

    /// <summary>The direction pointing from an edge into the arena.</summary>
    public static Vector2 Inward(ArenaEdge edge) => edge switch
    {
        ArenaEdge.South => Vector2.Up, ArenaEdge.North => Vector2.Down, ArenaEdge.East => Vector2.Left, _ => Vector2.Right,
    };
    private static Vector2 Along(ArenaEdge edge) => edge is ArenaEdge.South or ArenaEdge.North ? Vector2.Right : Vector2.Down;
    /// <summary>A point <paramref name="inset"/> in from an edge, <paramref name="along"/> from the edge's middle.</summary>
    private static Vector2 EdgePoint(ArenaEdge edge, float along, float inset) =>
        -Inward(edge) * (CombatRules.ArenaHalfSize - inset) + Along(edge) * along;

    /// <summary>Lays out a patrol and generates its terrain. Everything follows from the terrain seed, so every co-op player's
    /// replica rebuilds the same battlefield.</summary>
    public static (PatrolLayout Layout, BattlefieldTerrain Terrain) Create(int terrainSeed, Biome biome, int locations, int friendlyCount)
    {
        if (locations is < MissionTuning.MinPatrolLocations or > MissionTuning.MaxPatrolLocations) throw new ArgumentOutOfRangeException(nameof(locations));
        var random = new Random(terrainSeed ^ 0x2F6A3C1);
        float Uniform(float limit) => (random.NextSingle() * 2 - 1) * limit;
        var startEdge = (ArenaEdge)random.Next(4);
        var exits = Enum.GetValues<ArenaEdge>().Where(edge => edge != startEdge).ToArray();
        var extractionEdge = exits[random.Next(exits.Length)];
        // Rows of up to eight, centred at a random point along the start edge.
        var widest = (Math.Min(friendlyCount, CombatRules.PatrolRowSize) - 1) / 2f * CombatRules.DeploymentSpacing;
        var along = Uniform(Math.Max(0, CombatRules.ArenaHalfSize - CombatRules.PatrolEdgeMargin - widest));
        var starts = Enumerable.Range(0, friendlyCount).Select(i =>
        {
            var row = i / CombatRules.PatrolRowSize; var inRow = Math.Min(CombatRules.PatrolRowSize, friendlyCount - row * CombatRules.PatrolRowSize);
            var offset = (i % CombatRules.PatrolRowSize - (inRow - 1) / 2f) * CombatRules.DeploymentSpacing;
            return EdgePoint(startEdge, along + offset, CombatRules.PatrolStartInset - row * CombatRules.PatrolRowSpacing);
        }).ToArray();
        var extraction = EdgePoint(extractionEdge, Uniform(CombatRules.ArenaHalfSize - CombatRules.PatrolEdgeMargin - CombatRules.ExtractionRadius),
            CombatRules.ExtractionRadius);
        var start = starts.Aggregate(Vector2.Zero, (sum, point) => sum + point) / starts.Length;
        // Points spread across the arena; the spacing relaxes only if the arena cannot fit them.
        var points = new List<Vector2>();
        var spacing = CombatRules.PatrolPointSpacing;
        while (points.Count < locations - 1)
        {
            var found = false;
            for (var attempt = 0; attempt < 400 && !found; attempt++)
            {
                var limit = CombatRules.ArenaHalfSize - CombatRules.PatrolEdgeMargin;
                var candidate = new Vector2(Uniform(limit), Uniform(limit));
                if (points.Append(start).Append(extraction).All(other => other.DistanceTo(candidate) >= spacing)) { points.Add(candidate); found = true; }
            }
            if (!found) spacing *= .8f;
        }
        var clearings = starts.Select(point => new BattlefieldTerrain.Clearing(point, CombatRules.PatrolClearing))
            .Concat(points.Select(point => new BattlefieldTerrain.Clearing(point, CombatRules.PatrolClearing)))
            .Append(new BattlefieldTerrain.Clearing(extraction, CombatRules.ExtractionClearing)).ToArray();
        var terrain = BattlefieldTerrain.Generate(terrainSeed, biome, clearings);
        var ordered = ShortestOrder(terrain, start, points, extraction).Append(extraction).ToArray();
        var posts = Enumerable.Range(0, ordered.Length - 1).Select(group =>
        {
            var from = ordered[group]; var to = ordered[group + 1];
            var side = (to - from).Normalized().Orthogonal() * Uniform(CombatRules.PatrolGroupOffset);
            return terrain.ReachableDestination(from, (from + to) / 2 + side);
        }).ToArray();
        return (new(startEdge, extractionEdge, starts, ordered, posts), terrain);
    }

    /// <summary>The visiting order of the patrol points that walks least from the start to the extraction zone. Three points
    /// have six orders, so every one is tried.</summary>
    private static IEnumerable<Vector2> ShortestOrder(BattlefieldTerrain terrain, Vector2 start, IReadOnlyList<Vector2> points, Vector2 extraction)
    {
        var lengths = new Dictionary<(Vector2, Vector2), float>();
        float Walk(Vector2 from, Vector2 to)
        {
            if (lengths.TryGetValue((from, to), out var known)) return known;
            var path = terrain.FindPath(from, to);
            var length = 0f; var at = from;
            foreach (var point in path) { length += at.DistanceTo(point); at = point; }
            // An unreachable leg should not happen on a connected battlefield; it is costed as a long detour.
            if (path.Count == 0) length = from.DistanceTo(to) * 3;
            return lengths[(from, to)] = length;
        }
        IEnumerable<int[]> Orders(int[] remaining) => remaining.Length == 0 ? [[]]
            : remaining.SelectMany(first => Orders(remaining.Where(item => item != first).ToArray()).Select(rest => rest.Prepend(first).ToArray()));
        var best = Orders(Enumerable.Range(0, points.Count).ToArray()).MinBy(order =>
        {
            var at = start; var total = 0f;
            foreach (var index in order) { total += Walk(at, points[index]); at = points[index]; }
            return total + Walk(at, extraction);
        })!;
        return best.Select(index => points[index]);
    }
}

/// <summary>A patrol event worth a line in the battle log.</summary>
public sealed record PatrolEvent(double Time, string Text);

/// <summary>What a co-op player's replica needs of a patrol: progress, which groups are still passive, and the events so far.</summary>
public sealed record PatrolProgress(int Visited, bool[] Passive, PatrolEvent[] Events);

/// <summary>An enemy group of a patrol: passive until it makes its own contact or is fired upon, then hunting.</summary>
public sealed class PatrolGroup
{
    public bool Passive { get; internal set; } = true;
    /// <summary>Where fire came from, once the group has had time to react to it.</summary>
    internal (Vector2 Position, double Time)? Heard { get; set; }
    /// <summary>Attacks on the group, with when the group reacts to each.</summary>
    internal List<(double Time, Vector2 From)> Pending { get; } = [];
    /// <summary>Contacts last seen before this have already been searched for.</summary>
    internal double ClearedAt { get; set; } = double.NegativeInfinity;
    internal (Vector2 Center, double Ends, double Since)? Search { get; set; }
}

/// <summary>A patrol in progress.</summary>
public sealed class PatrolState(PatrolLayout layout, int groups)
{
    public PatrolLayout Layout { get; } = layout;
    public IReadOnlyList<PatrolGroup> Groups { get; } = Enumerable.Range(0, groups).Select(_ => new PatrolGroup()).ToArray();
    /// <summary>Patrol points visited, in order; the next to visit is <c>Layout.Locations[Visited]</c>.</summary>
    public int Visited { get; internal set; }
    public bool AllVisited => Visited >= Layout.PatrolPoints;
    internal List<PatrolEvent> EventList { get; } = [];
    public IReadOnlyList<PatrolEvent> Events => EventList;
    internal PatrolProgress Progress() => new(Visited, Groups.Select(group => group.Passive).ToArray(), [.. EventList]);
    internal void Apply(PatrolProgress progress)
    {
        if (progress.Passive.Length != Groups.Count || progress.Visited < 0 || progress.Visited > Layout.PatrolPoints)
            throw new ArgumentException("The battle state does not match this battle.", nameof(progress));
        Visited = progress.Visited;
        for (var i = 0; i < Groups.Count; i++) Groups[i].Passive = progress.Passive[i];
        EventList.Clear(); EventList.AddRange(progress.Events);
    }
}

public sealed partial class CombatSimulation
{
    /// <summary>The patrol this battle fights, or null for a standard battle.</summary>
    public PatrolState? Patrol { get; private set; }
    private Random? _patrolRandom;

    /// <summary>A passive patrol mech neither moves, turns its torso nor fires.</summary>
    public bool Passive(CombatUnit unit) => Patrol is not null && unit.Group is { } group && Patrol.Groups[group].Passive;

    private static CombatSimulation DeployPatrol(IReadOnlyList<DeployedMech> friendly, IReadOnlyList<DeployedMech> enemy, int seed, int terrainSeed,
        Biome biome, int locations)
    {
        var (layout, terrain) = PatrolLayout.Create(terrainSeed, biome, locations, friendly.Count);
        var random = new Random(terrainSeed ^ 0x51ED270B);
        var inward = PatrolLayout.Inward(layout.StartEdge);
        var units = friendly.Select((mech, i) => new CombatUnit(mech.Id, mech.Spec, mech.Pilot, CombatSide.Friendly, mech.Locations, mech.Equipment,
            mech.Ammo, layout.Starts[i], mech.Lance) { Facing = inward }).ToList();
        var placed = new List<Vector2>();
        for (var i = 0; i < enemy.Count; i++)
        {
            var group = enemy[i].Group ?? throw new ArgumentException("A patrol's opposition must be grouped.");
            if (group < 0 || group >= layout.Posts.Length) throw new ArgumentException("A patrol group has no leg.");
            var post = layout.Posts[group];
            // Members gather around their post on a spiral, each on reachable ground clear of the others.
            var position = post;
            for (var slot = placed.Count(point => point.DistanceTo(post) < 200); slot < 200; slot++)
            {
                position = terrain.ReachableDestination(post, post + (slot == 0 ? Vector2.Zero : Vector2.FromAngle(slot * 2.4f) * 36 * Mathf.Sqrt(slot)));
                if (placed.All(point => point.DistanceTo(position) >= CombatRules.UnitRadius * 3)) break;
            }
            placed.Add(position);
            units.Add(new CombatUnit($"enemy-{i + 1}", enemy[i].Spec, enemy[i].Pilot, CombatSide.Enemy, enemy[i].Locations, enemy[i].Equipment,
                enemy[i].Ammo, position, null, group) { Facing = Vector2.FromAngle(random.NextSingle() * Mathf.Tau) });
        }
        var simulation = new CombatSimulation(units, seed, terrain);
        simulation.Patrol = new PatrolState(layout, layout.Posts.Length); simulation._patrolRandom = new Random(seed ^ 0x7A11);
        return simulation;
    }

    /// <summary>An attack on a group member makes the group react once the attack resolves: when the shot or salvo lands or
    /// reaches its miss point, at once for a laser.</summary>
    internal void HeardFire(CombatUnit attacker, CombatUnit target, CombatShot shot)
    {
        if (target.Group is not { } group || attacker.Side == target.Side) return;
        Patrol!.Groups[group].Pending.Add((Elapsed + shot.FlightSeconds + CombatRules.PatrolReactionSeconds, attacker.Position));
    }

    private IEnumerable<CombatUnit> Members(int group) => Units.Where(unit => unit.Group == group && unit.Active);
    private IEnumerable<CombatUnit> CompanyOnField => Units.Where(unit => unit.Side == CombatSide.Friendly && !unit.Defeated);

    /// <summary>Fire that has resolved alerts its group; a passive group also wakes when one of its own mechs detects the company.
    /// A passive group never uses what the rest of its side has seen.</summary>
    private void UpdatePatrolAwareness()
    {
        for (var index = 0; index < Patrol!.Groups.Count; index++)
        {
            var group = Patrol.Groups[index];
            foreach (var (time, from) in group.Pending.Where(item => item.Time <= Elapsed + 1e-6).ToArray())
            {
                group.Pending.Remove((time, from)); group.Heard = (from, time); group.Passive = false;
            }
            if (!group.Passive) continue;
            var company = CompanyOnField.ToArray();
            if (Members(index).Any(member => company.Any(target => Observes(member, target)))) group.Passive = false;
        }
    }

    private bool Observes(CombatUnit observer, CombatUnit target)
    {
        var radius = CombatRules.DetectionRadius(observer, target);
        return observer.Position.DistanceSquaredTo(target.Position) <= radius * radius && Sight(observer, target);
    }

    /// <summary>Active groups attack the nearest company mech their side detects. With none detected they head for the latest
    /// lead, a last known position or the source of fire, then search around it; after a fruitless search they return to their
    /// posts, still alert.</summary>
    private void UpdatePatrolAi()
    {
        for (var index = 0; index < Patrol!.Groups.Count; index++)
        {
            var group = Patrol.Groups[index];
            var members = Members(index).ToArray();
            if (group.Passive || members.Length == 0) continue;
            var detected = CompanyOnField.Where(unit => Detected(CombatSide.Enemy, unit.Id)).ToArray();
            if (detected.Length > 0)
            {
                group.Search = null;
                foreach (var member in members)
                {
                    var nearest = detected.MinBy(unit => unit.Position.DistanceSquaredTo(member.Position))!;
                    member.Order = CombatOrder.Attack; member.AttackTarget = nearest.Id;
                }
                continue;
            }
            var lead = Lead(group);
            if (group.Search is { } search)
            {
                if (lead is { } newer && newer.Time > search.Since) group.Search = null;
                else if (Elapsed >= search.Ends)
                {
                    group.Search = null; group.Heard = null; group.ClearedAt = Elapsed;
                    foreach (var member in members) Move([member.Id], member.Post);
                    continue;
                }
                else
                {
                    foreach (var member in members.Where(member => member.Order == CombatOrder.Hold))
                    {
                        var point = search.Center + Vector2.FromAngle(_patrolRandom!.NextSingle() * Mathf.Tau)
                            * (CombatRules.PatrolSearchRadius * Mathf.Sqrt(_patrolRandom.NextSingle()));
                        Move([member.Id], point);
                    }
                    continue;
                }
            }
            if (lead is not { } target) continue;
            var arrived = members.Any(member => member.Position.DistanceTo(target.Position) <= CombatRules.PatrolArrival)
                || members.All(member => member.Order == CombatOrder.Hold && member.Destination.DistanceTo(target.Position) < CombatRules.PatrolArrival * 2);
            if (arrived) { group.Search = (target.Position, Elapsed + CombatRules.PatrolSearchSeconds, target.Time); continue; }
            var heading = members.Where(member => member.Order != CombatOrder.Move || member.Destination.DistanceTo(target.Position) > CombatRules.PatrolArrival * 2)
                .Select(member => member.Id).ToArray();
            if (heading.Length > 0) Move(heading, target.Position);
        }
    }

    /// <summary>The freshest trail not yet searched: a company mech's last known position or where fire came from.</summary>
    private (Vector2 Position, double Time)? Lead(PatrolGroup group)
    {
        (Vector2 Position, double Time)? best = group.Heard is { } heard && heard.Time > group.ClearedAt ? heard : null;
        foreach (var contact in Contacts(CombatSide.Enemy).Values)
        {
            if (Unit(contact.UnitId).Defeated || contact.LastSeenAt <= group.ClearedAt) continue;
            if (best is null || contact.LastSeenAt > best.Value.Time) best = (contact.LastKnownPosition, contact.LastSeenAt);
        }
        return best;
    }

    /// <summary>Visits the current patrol point, extracts mechs reaching the zone, and decides the patrol once no company mech
    /// remains on the field: success if every point was visited and someone extracted, a failure otherwise.</summary>
    private void UpdatePatrolObjectives()
    {
        var patrol = Patrol!; var layout = patrol.Layout;
        if (!patrol.AllVisited && CompanyOnField.Any(unit => unit.Position.DistanceTo(layout.Locations[patrol.Visited]) <= CombatRules.PatrolVisitRadius))
        {
            patrol.Visited++;
            patrol.EventList.Add(new(Elapsed, $"Patrol point {patrol.Visited} of {layout.PatrolPoints} reached"
                + (patrol.AllVisited ? " · proceed to extraction" : "")));
        }
        foreach (var unit in CompanyOnField.Where(unit => unit.Position.DistanceTo(layout.Extraction) <= CombatRules.ExtractionRadius).ToArray())
        {
            unit.Extracted = true; unit.Moving = false; unit.CurrentSpeed = unit.AngularVelocity = 0;
            unit.Order = CombatOrder.Hold; unit.AttackTarget = unit.FollowTarget = null; unit.CurrentTarget = unit.CurrentRearTarget = null;
            patrol.EventList.Add(new(Elapsed, $"{unit.Warrior.Callsign} extracted"
                + (patrol.AllVisited ? "" : $" · {layout.PatrolPoints - patrol.Visited} patrol {(layout.PatrolPoints - patrol.Visited == 1 ? "point" : "points")} unvisited")));
        }
        _burns.RemoveAll(burn => Unit(burn.Discharge.AttackerId).Extracted);
        if (WindDownEnds is null && !CompanyOnField.Any()) WindDownEnds = Elapsed + CombatRules.WindDownSeconds;
        if (WindDownEnds is { } ends && Elapsed + 1e-6 >= ends)
        {
            var extracted = Units.Any(unit => unit.Side == CombatSide.Friendly && unit.Extracted);
            Outcome = extracted && patrol.AllVisited ? CombatOutcome.Victory : extracted ? CombatOutcome.Ended : CombatOutcome.Defeat;
            Paused = true;
        }
    }

    /// <summary>One line describing the patrol objective, for the combat screen.</summary>
    public string? PatrolObjectiveText()
    {
        if (Patrol is not { } patrol) return null;
        var company = Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var extracted = company.Count(unit => unit.Extracted);
        var onField = company.Count(unit => !unit.Defeated);
        return patrol.AllVisited
            ? $"Extract: {extracted} out · {onField} still on the field"
            : $"Patrol point {patrol.Visited + 1} of {patrol.Layout.PatrolPoints}" + (extracted > 0 ? $" · {extracted} extracted" : "");
    }
}
