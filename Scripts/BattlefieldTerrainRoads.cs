using Godot;

namespace Mech;

/// <summary>A road's centreline, from one end to the other, including any bridges along it.</summary>
public sealed record TerrainRoad(IReadOnlyList<Vector2> Course)
{
    public float Length => Course.Zip(Course.Skip(1)).Sum(pair => pair.First.DistanceTo(pair.Second));
}

/// <summary>A straight bridge deck between two ends at their heights, over water, ice or lava.</summary>
public sealed record TerrainBridge(Vector2 From, Vector2 To, float FromHeight, float ToHeight, TerrainKind Over)
{
    public float Length => From.DistanceTo(To);
}

/// <summary>A settlement's levelled pad and street grid: lots on a grid of StreetWidth + BuildingLot, ringed by a street.</summary>
internal readonly record struct StreetGrid(Vector2 Center, Vector2 Half, float Rotation, float Level)
{
    public Vector2 Local(Vector2 point)
    {
        var offset = point - Center; var (cos, sin) = (MathF.Cos(Rotation), MathF.Sin(Rotation));
        return new(offset.X * cos + offset.Y * sin, -offset.X * sin + offset.Y * cos);
    }
    public Vector2 World(Vector2 local)
    {
        var (cos, sin) = (MathF.Cos(Rotation), MathF.Sin(Rotation));
        return Center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
    }
    /// <summary>Within the pad, out to the middle of its outer street plus this margin.</summary>
    public bool Within(Vector2 point, float margin)
    {
        var local = Local(point);
        return Math.Abs(local.X) <= Half.X + margin && Math.Abs(local.Y) <= Half.Y + margin;
    }
}

/// <summary>Roads, bridges and settlement streets for a battlefield.</summary>
internal sealed record RoadMap(TerrainRoad[] Roads, TerrainBridge[] Bridges, StreetGrid[] Streets);

/// <summary>Roads join the settlements and leave the map; bridges carry them, and settlement streets, over liquid.
/// Numbers are recorded in docs/combat-tuning.md.</summary>
public sealed partial class BattlefieldTerrain
{
    public const float RoadSpeed = 1.25f;
    public const float RoadWidth = 40, DeckWidth = 60;
    /// <summary>The steepest grade a road's bed climbs, and its cut and fill banks.</summary>
    private const float RoadGrade = .2f, RoadBank = .5f;
    /// <summary>The longest bridge over a river, and over the sea.</summary>
    private const float RiverSpan = 400, SeaSpan = 1200;
    /// <summary>The longest detour a road takes to avoid building a bridge.</summary>
    private const float BridgeDetour = 1000;
    /// <summary>Metres between the road raster's points.</summary>
    private const float RoadCell = 10;
    private const int RoadCells = (int)(CombatRules.ArenaHalfSize * 2 / RoadCell) + 1;

    /// <summary>A bridge deck: its axis, length and end heights.</summary>
    private readonly record struct Deck(Vector2 From, Vector2 Direction, float Length, float FromHeight, float ToHeight)
    {
        public (float Along, float Across) Local(Vector2 point)
        {
            var offset = point - From;
            return (offset.Dot(Direction), offset.Dot(Direction.Orthogonal()));
        }
    }
    public IReadOnlyList<TerrainRoad> Roads { get; }
    public IReadOnlyList<TerrainBridge> Bridges { get; }
    /// <summary>Settlements' street grids, for rendering.</summary>
    internal IReadOnlyList<StreetGrid> Streets { get; }
    private readonly bool[]? _road;
    private readonly Deck[] _decks = [];
    /// <summary>Grid nodes near a deck, where height and passability must test it.</summary>
    private readonly bool[]? _deckNear;
    /// <summary>Waypoints along a bridge edge of the navigation graph, by its two nodes.</summary>
    private readonly Dictionary<(int, int), Vector2[]> _vias = [];

    /// <summary>On a road, a bridge deck or a settlement's streets.</summary>
    public bool OnRoad(Vector2 point)
    {
        if (_road is null) return false;
        int x = (int)MathF.Round((point.X + CombatRules.ArenaHalfSize) / RoadCell), z = (int)MathF.Round((point.Y + CombatRules.ArenaHalfSize) / RoadCell);
        return x is >= 0 and < RoadCells && z is >= 0 and < RoadCells && _road[z * RoadCells + x];
    }

    private static (bool[] Raster, Deck[] Decks, bool[] Near) RoadGrid(RoadMap map)
    {
        var raster = new bool[RoadCells * RoadCells];
        void Mark(Vector2 point, float radius)
        {
            int minX = Math.Max(0, (int)MathF.Floor((point.X - radius + CombatRules.ArenaHalfSize) / RoadCell)),
                maxX = Math.Min(RoadCells - 1, (int)MathF.Ceiling((point.X + radius + CombatRules.ArenaHalfSize) / RoadCell)),
                minZ = Math.Max(0, (int)MathF.Floor((point.Y - radius + CombatRules.ArenaHalfSize) / RoadCell)),
                maxZ = Math.Min(RoadCells - 1, (int)MathF.Ceiling((point.Y + radius + CombatRules.ArenaHalfSize) / RoadCell));
            for (var z = minZ; z <= maxZ; z++) for (var x = minX; x <= maxX; x++)
                if (new Vector2(x * RoadCell - CombatRules.ArenaHalfSize, z * RoadCell - CombatRules.ArenaHalfSize).DistanceTo(point) <= radius) raster[z * RoadCells + x] = true;
        }
        foreach (var road in map.Roads)
            foreach (var point in Resample(road.Course.ToList(), RoadCell)) Mark(point, RoadWidth / 2);
        var decks = map.Bridges.Select(bridge => new Deck(bridge.From, (bridge.To - bridge.From).Normalized(), bridge.Length, bridge.FromHeight, bridge.ToHeight)).ToArray();
        foreach (var deck in decks)
            for (var along = 0f; along <= deck.Length; along += RoadCell / 2)
                for (var across = -DeckWidth / 2; across <= DeckWidth / 2; across += RoadCell / 2)
                    Mark(deck.From + deck.Direction * along + deck.Direction.Orthogonal() * across, RoadCell / 2);
        for (var z = 0; z < RoadCells; z++) for (var x = 0; x < RoadCells; x++)
        {
            var point = new Vector2(x * RoadCell - CombatRules.ArenaHalfSize, z * RoadCell - CombatRules.ArenaHalfSize);
            if (map.Streets.Any(street => street.Within(point, 30))) raster[z * RoadCells + x] = true;
        }
        var near = new bool[GridSize * GridSize];
        var reach = DeckWidth / 2 + CombatRules.UnitRadius + 1.5f + CellSize * 1.5f;
        for (var index = 0; index < near.Length; index++)
            near[index] = decks.Any(deck => deck.Local(Point(index)) is var (along, across) && along >= -reach && along <= deck.Length + reach && Math.Abs(across) <= reach);
        return (raster, decks, near);
    }

    /// <summary>The deck surface at a point, where a deck spans it.</summary>
    private float? DeckHeight(Vector2 point)
    {
        if (_deckNear is null || !_deckNear[Node(point)]) return null;
        foreach (var deck in _decks)
        {
            var (along, across) = deck.Local(point);
            if (along >= 0 && along <= deck.Length && Math.Abs(across) <= DeckWidth / 2)
                return Mathf.Lerp(deck.FromHeight, deck.ToHeight, along / Math.Max(deck.Length, 1));
        }
        return null;
    }
    /// <summary>On a deck, a mech's footprint must stay between the rails; beside a deck, the rails and the deck itself bar it,
    /// so nothing climbs on from the side or passes beneath. Null away from decks.</summary>
    private bool? DeckFooting(Vector2 point)
    {
        if (_deckNear is null || !_deckNear[Node(point)]) return null;
        var clearance = CombatRules.UnitRadius + 1.5f;
        foreach (var deck in _decks)
        {
            var (along, across) = deck.Local(point);
            if (along < 0 || along > deck.Length) continue;
            if (Math.Abs(across) <= DeckWidth / 2 - clearance) return true;
            if (Math.Abs(across) <= DeckWidth / 2 + clearance) return false;
        }
        return null;
    }
    /// <summary>Bridges join the walkable ground at their two approaches with one navigation edge each way, routed along the deck.</summary>
    private void BridgeEdges()
    {
        foreach (var deck in _decks)
        {
            var near = deck.From - deck.Direction * 25; var far = deck.From + deck.Direction * (deck.Length + 25);
            int Approach(Vector2 end) => Enumerable.Range(0, _walkable.Length)
                .Where(index => _walkable[index] && Point(index).DistanceSquaredTo(end) <= 60 * 60)
                .OrderBy(index => Point(index).DistanceSquaredTo(end)).Where(index => CanTraverse(Point(index), end)).DefaultIfEmpty(-1).First();
            var (from, to) = (Approach(near), Approach(far));
            if (from < 0 || to < 0 || from == to || !CanTraverse(near, far)) continue;
            var cost = (Point(from).DistanceTo(near) + near.DistanceTo(far) + far.DistanceTo(Point(to))) / RoadSpeed;
            _neighbors[from].Add(new(to, cost)); _neighbors[to].Add(new(from, cost));
            _vias[(from, to)] = [near, far]; _vias[(to, from)] = [far, near];
        }
    }

    private static Vector2 Closest(Vector2 point, Vector2 from, Vector2 to)
    {
        var line = to - from; var t = line.LengthSquared() < 1e-6f ? 0 : Math.Clamp((point - from).Dot(line) / line.LengthSquared(), 0, 1);
        return from + line * t;
    }

    /// <summary>The roads, bridges and streets to build, and how far rocks must keep from them.</summary>
    private sealed class RoadPlan(RoadMap map, float[] distance)
    {
        public RoadMap Map { get; } = map;
        public float Distance(Vector2 point) => distance[Node(point)];
    }

    /// <summary>Joins the settlements by roads, a minimum spanning tree with one extra link, and leads one or two roads off the map.
    /// Roads climb gently on a cut-and-filled bed, keep off standing water and cross rivers, and in the Archipelago the sea, on bridges;
    /// streets crossing a river get bridges too.</summary>
    private static RoadPlan GenerateRoads(Random random, float[] relief, IReadOnlyList<StreetGrid> pads, LiquidPlan liquids)
    {
        var count = relief.Length;
        var map = liquids.Map;
        LiquidShape? Shape(int index) => map.Owners[index] >= 0 ? map.Bodies[map.Owners[index]].Shape : null;
        bool Wet(int index) => Shape(index) is LiquidShape.River or LiquidShape.Sea || liquids.Channel[index];
        var lakeNear = new bool[count];
        for (var index = 0; index < count; index++)
            if (Shape(index) == LiquidShape.Lake)
            {
                lakeNear[index] = true;
                foreach (var next in Neighbours(index % GridSize, index / GridSize)) lakeNear[next] = true;
            }
        // Liquid surfaces within two cells: cutting a road never lowers ground beside liquid beneath it.
        var floor = new float[count]; Array.Fill(floor, float.NegativeInfinity);
        for (var index = 0; index < count; index++)
            if (map.Kinds[index] >= 0)
            {
                int x = index % GridSize, z = index / GridSize;
                for (var dz = -2; dz <= 2; dz++) for (var dx = -2; dx <= 2; dx++)
                    if (x + dx is >= 0 and < GridSize && z + dz is >= 0 and < GridSize)
                        floor[Index(x + dx, z + dz)] = Math.Max(floor[Index(x + dx, z + dz)], map.Surfaces[index] + .5f);
            }
        // Roads keep out of settlements' lots, meeting their outer streets.
        var interior = Enumerable.Range(0, count).Select(index => pads.Any(pad => pad.Within(Point(index), -15))).ToArray();
        var street = Enumerable.Range(0, count).Select(index => pads.Any(pad => pad.Within(Point(index), 30))).ToArray();
        bool Interior(int index) => interior[index];
        bool Ring(int index, StreetGrid pad) => street[index] && !interior[index] && pad.Within(Point(index), 30);
        static bool Border(int index) => index % GridSize is 0 or GridSize - 1 || index / GridSize is 0 or GridSize - 1;
        var paved = new bool[count];
        var roads = new List<TerrainRoad>(); var bridges = new List<TerrainBridge>();

        float Step(int from, int to)
        {
            // A road leaves the map edge at once rather than running along it.
            if (lakeNear[to] || Interior(to) || Border(to)) return float.PositiveInfinity;
            var length = Point(from).DistanceTo(Point(to));
            var grade = Math.Abs(relief[to] - relief[from]) / length;
            var edge = CombatRules.ArenaHalfSize - Math.Max(Math.Abs(Point(to).X), Math.Abs(Point(to).Y));
            var cost = length * (1 + 10 * grade + (grade > .3f ? 20 : 0)) * (1 + 2 * Math.Clamp(1 - edge / 300, 0, 1));
            if (paved[to]) return cost * .5f;
            if (Shape(to) == LiquidShape.Sea) cost *= 3;
            else if (Wet(to)) cost *= 4;
            // Each new bridge costs as much as a kilometre of road, so a road takes any shorter detour around the liquid instead.
            return Wet(to) && !Wet(from) ? cost + BridgeDetour : cost;
        }
        List<Vector2>? Route(int start, Func<int, bool> goal, Vector2? target)
        {
            var route = Search(start, goal, target, Step);
            if (route is null) return null;
            // Every other grid node, corners cut four times: the grid's stair-steps become curves.
            var raw = route.Select(Point).ToList();
            var smooth = Resample(Smooth(route.Where((_, i) => i % 2 == 0 || i == route.Count - 1).Select(Point).ToList(), 4), 10);
            // A cut corner that clips liquid the route itself kept clear of returns to the route, rather than needing a bridge.
            var crossings = route.Where(Wet).Select(Point).ToArray();
            for (var s = 0; s < smooth.Count; s++)
                if (Wet(Node(smooth[s])) && !crossings.Any(point => point.DistanceTo(smooth[s]) <= 45))
                    smooth[s] = raw.Zip(raw.Skip(1)).Select(segment => Closest(smooth[s], segment.First, segment.Second)).MinBy(point => point.DistanceTo(smooth[s]));
            return Resample(smooth, 10);
        }

        // Lays a road along its course: bridges where it crosses liquid, a graded bed elsewhere. False when a span is too long.
        bool Lay(List<Vector2> samples)
        {
            var n = samples.Count;
            if (n < 3) return false;
            var wet = samples.Select(point => Wet(Node(point))).ToArray();
            var spans = new List<(int From, int To)>();
            for (var s = 0; s < n; s++)
            {
                if (!wet[s]) continue;
                var start = s;
                while (s + 1 < n && wet[s + 1]) s++;
                if (spans.Count > 0 && start - spans[^1].To <= 6) spans[^1] = (spans[^1].From, s);
                else spans.Add((start, s));
            }
            // A deck lands on dry ground a sample beyond the liquid at each end.
            spans = spans.Select(span => (span.From - 1, span.To + 1)).ToList();
            if (spans.Any(span => span.Item1 < 1 || span.Item2 > n - 2)) return false;
            var sea = spans.Select(span => Enumerable.Range(span.Item1, span.Item2 - span.Item1 + 1).Any(s => Shape(Node(samples[s])) == LiquidShape.Sea)).ToArray();
            for (var i = 0; i < spans.Count; i++)
                if (samples[spans[i].Item1].DistanceTo(samples[spans[i].Item2]) > (sea[i] ? SeaSpan : RiverSpan)) return false;
            var inSpan = new int[n]; Array.Fill(inSpan, -1);
            for (var i = 0; i < spans.Count; i++) for (var s = spans[i].Item1 + 1; s < spans[i].Item2; s++) inSpan[s] = i;
            // The bed follows the ground, smoothed, and climbs no steeper than RoadGrade: half cut through rises, half filled over dips.
            var ground = samples.Select(point => Sample(relief, point)).ToArray();
            var height = (float[])ground.Clone();
            foreach (var (from, to) in spans)
                for (var s = from + 1; s < to; s++) height[s] = Mathf.Lerp(ground[from], ground[to], (s - from) / (float)(to - from));
            for (var pass = 0; pass < 3; pass++)
            {
                var smooth = (float[])height.Clone();
                for (var s = 2; s < n - 2; s++) if (inSpan[s] < 0) smooth[s] = (height[s - 1] + 2 * height[s] + height[s + 1]) / 4;
                height = smooth;
            }
            var cut = (float[])height.Clone(); var fill = (float[])height.Clone();
            for (var s = 1; s < n; s++) { cut[s] = Math.Min(cut[s], cut[s - 1] + RoadGrade * 10); fill[s] = Math.Max(fill[s], fill[s - 1] - RoadGrade * 10); }
            for (var s = n - 2; s >= 0; s--) { cut[s] = Math.Min(cut[s], cut[s + 1] + RoadGrade * 10); fill[s] = Math.Max(fill[s], fill[s + 1] - RoadGrade * 10); }
            for (var s = 0; s < n; s++) height[s] = (cut[s] + fill[s]) / 2;
            // Decks stand at least 4 m over the liquid; their approaches ramp up to them.
            for (var i = 0; i < spans.Count; i++)
            {
                var top = Enumerable.Range(spans[i].Item1, spans[i].Item2 - spans[i].Item1 + 1).Select(s => samples[s])
                    .Select(point => map.Surfaces[Node(point)]).Where(surface => !float.IsNaN(surface)).DefaultIfEmpty(float.NegativeInfinity).Max() + 4;
                height[spans[i].Item1] = Math.Max(height[spans[i].Item1], top); height[spans[i].Item2] = Math.Max(height[spans[i].Item2], top);
            }
            for (var s = 1; s < n; s++) if (inSpan[s] < 0) height[s] = Math.Max(height[s], height[s - 1] - RoadGrade * 10);
            for (var s = n - 2; s >= 0; s--) if (inSpan[s] < 0) height[s] = Math.Max(height[s], height[s + 1] - RoadGrade * 10);
            // Cut and fill the bed, with banks at RoadBank, leaving pads, liquid and river trenches alone.
            var nearest = new int[count]; Array.Fill(nearest, -1);
            var distance = new float[count]; Array.Fill(distance, float.PositiveInfinity);
            var reach = RoadWidth / 2 + 60;
            for (var s = 0; s < n; s++)
            {
                if (inSpan[s] >= 0) continue;
                var (minX, minZ) = Cell(samples[s] - new Vector2(reach, reach)); var (maxX, maxZ) = Cell(samples[s] + new Vector2(reach, reach));
                for (var z = minZ; z <= maxZ; z++) for (var x = minX; x <= maxX; x++)
                {
                    var index = Index(x, z); var d = Point(index).DistanceTo(samples[s]);
                    if (d < distance[index]) { distance[index] = d; nearest[index] = s; }
                }
            }
            for (var index = 0; index < count; index++)
            {
                if (nearest[index] < 0 || distance[index] > reach || map.Kinds[index] >= 0 || liquids.Channel[index] || street[index]) continue;
                var bed = height[nearest[index]]; var bank = Math.Max(0, distance[index] - RoadWidth / 2) * RoadBank;
                relief[index] = Math.Max(Math.Clamp(relief[index], bed - bank, bed + bank), floor[index]);
                paved[index] = paved[index] || distance[index] <= RoadWidth / 2 + CellSize / 2;
            }
            for (var i = 0; i < spans.Count; i++)
            {
                var (from, to) = spans[i];
                var over = Enumerable.Range(from, to - from + 1).Select(s => map.Kinds[Node(samples[s])]).Where(kind => kind >= 0).DefaultIfEmpty((sbyte)TerrainKind.Water).First();
                // A second road over the same crossing shares the bridge already there.
                if (!bridges.Any(bridge => Math.Min(bridge.From.DistanceTo(samples[from]) + bridge.To.DistanceTo(samples[to]),
                        bridge.From.DistanceTo(samples[to]) + bridge.To.DistanceTo(samples[from])) <= 120))
                    bridges.Add(new(samples[from], samples[to], height[from], height[to], (TerrainKind)over));
                foreach (var s in Enumerable.Range(from, to - from + 1)) paved[Node(samples[s])] = true;
            }
            roads.Add(new(samples.Where(Inside).ToArray()));
            return true;
        }

        // The settlements' minimum spanning tree, by distance between pads, and the shortest link left over.
        var links = new List<(int A, int B)>();
        var joined = new List<int> { 0 };
        while (joined.Count < pads.Count)
        {
            var (a, b) = joined.SelectMany(a => Enumerable.Range(0, pads.Count).Where(b => !joined.Contains(b)).Select(b => (a, b)))
                .MinBy(link => pads[link.a].Center.DistanceSquaredTo(pads[link.b].Center));
            links.Add((a, b)); joined.Add(b);
        }
        var extra = Enumerable.Range(0, pads.Count).SelectMany(a => Enumerable.Range(a + 1, pads.Count - a - 1).Select(b => (a, b)))
            .Where(link => !links.Contains(link) && !links.Contains((link.b, link.a)))
            .OrderBy(link => pads[link.a].Center.DistanceSquaredTo(pads[link.b].Center)).Take(1);
        foreach (var (a, b) in links.Concat(extra))
        {
            var target = pads[b];
            var start = Enumerable.Range(0, count).Where(index => Ring(index, pads[a]) && !Wet(index) && !lakeNear[index])
                .OrderBy(index => Point(index).DistanceSquaredTo(target.Center)).DefaultIfEmpty(-1).First();
            if (start < 0) continue;
            var course = Route(start, index => Ring(index, target), target.Center);
            if (course is not null) Lay(course);
        }
        // Through roads: from a map edge to the nearest road or street.
        var through = 1 + random.Next(2);
        for (var tries = 0; tries < 16 && through > 0; tries++)
        {
            var t = random.NextSingle() * 4; var side = (int)t; var along = (int)((.1f + (t - side) * .8f) * (GridSize - 1));
            var start = side switch { 0 => Index(along, 0), 1 => Index(GridSize - 1, along), 2 => Index(along, GridSize - 1), _ => Index(0, along) };
            if (Wet(start) || lakeNear[start]) continue;
            var course = Route(start, index => !Border(index) && (paved[index] || street[index] && !interior[index]), null);
            if (course is null) continue;
            // The road runs on to the very edge.
            var edge = Point(start); var outward = Math.Abs(edge.X) >= Math.Abs(edge.Y) ? new Vector2(Math.Sign(edge.X), 0) : new Vector2(0, Math.Sign(edge.Y));
            course.Insert(0, edge + outward * 20);
            if (Lay(course)) through--;
        }
        // Streets crossing a river get bridges at the pad's level.
        const float pitch = StreetWidth + BuildingLot;
        foreach (var pad in pads)
            for (var axis = 0; axis < 2; axis++)
            {
                var (lines, extent) = axis == 0 ? (pad.Half.X, pad.Half.Y) : (pad.Half.Y, pad.Half.X);
                for (var line = -lines; line <= lines + .1f; line += pitch)
                {
                    var lane = new List<Vector2>();
                    for (var at = -extent - 30; at <= extent + 30; at += 10) lane.Add(pad.World(axis == 0 ? new(line, at) : new(at, line)));
                    var wet = lane.Select(point => Wet(Node(point))).ToArray();
                    for (var s = 0; s < lane.Count; s++)
                    {
                        if (!wet[s]) continue;
                        var start = s;
                        while (s + 1 < lane.Count && wet[s + 1]) s++;
                        if (start < 1 || s > lane.Count - 2) continue;
                        var over = map.Kinds[Node(lane[(start + s) / 2])];
                        bridges.Add(new(lane[start - 1], lane[s + 1], pad.Level, pad.Level, over >= 0 ? (TerrainKind)over : TerrainKind.Water));
                    }
                }
            }
        foreach (var bridge in bridges)
            for (var along = 0f; along <= bridge.Length; along += 10) paved[Node(bridge.From.Lerp(bridge.To, along / Math.Max(bridge.Length, 1)))] = true;
        return new(new([.. roads], [.. bridges], [.. pads]), DistanceTo(index => paved[index] || street[index]));
    }
}
