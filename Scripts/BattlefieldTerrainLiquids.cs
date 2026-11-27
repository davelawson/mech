using Godot;

namespace Mech;

/// <summary>The shape of a body of liquid.</summary>
public enum LiquidShape { Lake, River, Sea }

/// <summary>A lake, river or sea of water, ice or lava. A river's course runs downstream, from where it enters to where it leaves;
/// its fords are the stretches where a deep or lava river can be crossed.</summary>
public sealed record LiquidBody(LiquidShape Shape, TerrainKind Kind, IReadOnlyList<Vector2> Course, int Fords = 0);

/// <summary>Generated liquids on the heightfield grid: each node's liquid kind (-1 for none), surface height (NaN for none) and body.</summary>
internal sealed record LiquidMap(LiquidBody[] Bodies, sbyte[] Kinds, float[] Surfaces, int[] Owners, bool Deep);

/// <summary>Lakes that fill the heightfield's hollows, rivers that carve trenches through it, and the Archipelago's sea.
/// Numbers are recorded in docs/combat-tuning.md.</summary>
public sealed partial class BattlefieldTerrain
{
    /// <summary>Water deeper than this is deep water, in the biomes that have it.</summary>
    public const float DeepWaterDepth = 8;
    /// <summary>The Archipelago's sea surface, and the share of the battlefield below it.</summary>
    public const float SeaLevel = -1, SeaShare = .4f;
    /// <summary>The slope of a river trench's banks, gentle enough to climb.</summary>
    public const float BankSlope = .45f;
    /// <summary>How far a river's surface falls per metre of its course, at least.</summary>
    private const float RiverGrade = .0015f;
    private const float FordLength = 120;

    private readonly sbyte[]? _liquidKind;
    private readonly float[]? _liquidSurface;
    private readonly int[]? _liquidBody;
    /// <summary>Nodes within two cells of lava or deep water, where passability must test the liquid.</summary>
    private readonly bool[]? _hazardNear;
    private readonly float _deepDepth = float.PositiveInfinity;
    /// <summary>Whether any liquid is ice, whose top mechs stand on.</summary>
    private readonly bool _ice;

    /// <summary>Wet nodes keep their surface; each dry node beside a wet one takes its highest neighbour's, so a point's surface
    /// interpolates over every triangle touching liquid and the shoreline follows the ground's contour.</summary>
    private static (sbyte[], float[], int[], bool[], float) LiquidGrid(LiquidMap map, float[] heights)
    {
        var kinds = (sbyte[])map.Kinds.Clone(); var surfaces = (float[])map.Surfaces.Clone(); var owners = (int[])map.Owners.Clone();
        for (var z = 0; z < GridSize; z++) for (var x = 0; x < GridSize; x++)
        {
            var index = Index(x, z);
            if (map.Kinds[index] >= 0) continue;
            var best = -1;
            foreach (var next in Neighbours(x, z))
                if (map.Kinds[next] >= 0 && (best < 0 || map.Surfaces[next] > map.Surfaces[best])) best = next;
            if (best < 0) continue;
            kinds[index] = map.Kinds[best]; surfaces[index] = map.Surfaces[best]; owners[index] = -1;
        }
        var deep = map.Deep ? DeepWaterDepth : float.PositiveInfinity;
        var hazard = new bool[heights.Length];
        for (var index = 0; index < heights.Length; index++)
            if (map.Kinds[index] == (sbyte)TerrainKind.Lava || map.Kinds[index] == (sbyte)TerrainKind.Water && map.Surfaces[index] - heights[index] > deep)
            {
                int x = index % GridSize, z = index / GridSize;
                for (var dz = -2; dz <= 2; dz++) for (var dx = -2; dx <= 2; dx++)
                    if (x + dx is >= 0 and < GridSize && z + dz is >= 0 and < GridSize) hazard[Index(x + dx, z + dz)] = true;
            }
        return (kinds, surfaces, owners, hazard, deep);
    }
    private static IEnumerable<int> Neighbours(int x, int z)
    {
        for (var dz = -1; dz <= 1; dz++) for (var dx = -1; dx <= 1; dx++)
            if ((dx != 0 || dz != 0) && x + dx is >= 0 and < GridSize && z + dz is >= 0 and < GridSize) yield return Index(x + dx, z + dz);
    }

    /// <summary>The generated liquid at a point and its depth; null on dry ground. Water deeper than the biome allows is deep water.
    /// Depth runs down from the surface to the ground, or to a bridge deck; ice keeps its depth though mechs walk on its top.</summary>
    public (TerrainKind Kind, float Depth)? LiquidAt(Vector2 point)
    {
        if (LiquidSurfaceAt(point) is not { } liquid) return null;
        var depth = liquid.Surface - BelowLiquidAt(point);
        if (depth <= 0) return null;
        return (liquid.Kind == TerrainKind.Water && depth > _deepDepth ? TerrainKind.DeepWater : liquid.Kind, depth);
    }
    /// <summary>The top of the ice at a point, where it lies above what is below it.</summary>
    private float? IceTop(Vector2 point, float below) =>
        LiquidSurfaceAt(point) is { Kind: TerrainKind.Ice, Surface: var surface } && surface > below ? surface : null;
    /// <summary>The liquid surface interpolated over a point's triangle and the liquid's kind; null where no liquid reaches it.</summary>
    private (TerrainKind Kind, float Surface)? LiquidSurfaceAt(Vector2 point)
    {
        if (_liquidSurface is null) return null;
        var x = Math.Clamp(point.X / CellSize + HalfCells, 0, GridSize - 1); var z = Math.Clamp(point.Y / CellSize + HalfCells, 0, GridSize - 1);
        var ix = Math.Min((int)x, GridSize - 2); var iz = Math.Min((int)z, GridSize - 2); var u = x - ix; var v = z - iz;
        var lower = u + v <= 1;
        // The same triangle as HeightAt: (a, b, c) below the diagonal, (d, c, b) above it.
        int p = lower ? Index(ix, iz) : Index(ix + 1, iz + 1), q = lower ? Index(ix + 1, iz) : Index(ix, iz + 1), r = lower ? Index(ix, iz + 1) : Index(ix + 1, iz);
        float sp = _liquidSurface[p], sq = _liquidSurface[q], sr = _liquidSurface[r];
        if (float.IsNaN(sp) && float.IsNaN(sq) && float.IsNaN(sr)) return null;
        var fill = MathF.Max(float.IsNaN(sp) ? float.MinValue : sp, MathF.Max(float.IsNaN(sq) ? float.MinValue : sq, float.IsNaN(sr) ? float.MinValue : sr));
        if (float.IsNaN(sp)) sp = fill; if (float.IsNaN(sq)) sq = fill; if (float.IsNaN(sr)) sr = fill;
        var surface = lower ? sp + (sq - sp) * u + (sr - sp) * v : sp + (sq - sp) * (1 - u) + (sr - sp) * (1 - v);
        var kind = _liquidKind![p] >= 0 ? _liquidKind[p] : _liquidKind[q] >= 0 ? _liquidKind[q] : _liquidKind[r];
        return ((TerrainKind)kind, surface);
    }
    /// <summary>Lava and deep water bar a mech whose footprint reaches them.</summary>
    private bool InLiquidBarrier(Vector2 point)
    {
        if (_hazardNear is null || !_hazardNear[Node(point)]) return false;
        if (LiquidAt(point) is { Kind: TerrainKind.Lava or TerrainKind.DeepWater }) return true;
        for (var i = 0; i < 8; i++)
            if (LiquidAt(point + Vector2.FromAngle(i * MathF.Tau / 8) * CombatRules.UnitRadius) is { Kind: TerrainKind.Lava or TerrainKind.DeepWater }) return true;
        return false;
    }
    /// <summary>For rendering: the ground height, the liquid surface (NaN where none reaches) and the owning body (-1 on dry or shore nodes) of a grid node.</summary>
    internal float NodeHeight(int x, int z) => _heights[Index(x, z)];
    internal float NodeSurface(int x, int z) => _liquidSurface?[Index(x, z)] ?? float.NaN;
    internal int NodeBody(int x, int z) => _liquidBody?[Index(x, z)] ?? -1;
    /// <summary>The points of the grid nodes under a body.</summary>
    public IEnumerable<Vector2> LiquidNodes(int body) =>
        Enumerable.Range(0, _heights.Length).Where(index => _liquidBody is not null && _liquidBody[index] == body).Select(Point);

    private sealed record LakeRule(TerrainKind Kind, int Count, float MinRadius, float MaxRadius);
    private static LakeRule[] LakeRules(Biome biome) => biome switch
    {
        Mech.Biome.Arctic => [new(TerrainKind.Ice, 14, 120, 240)],
        Mech.Biome.Highlands => [new(TerrainKind.Water, 8, 60, 120)],
        Mech.Biome.Desert => [new(TerrainKind.Water, 4, 60, 90)],
        Mech.Biome.Tropical => [new(TerrainKind.Water, 18, 90, 180)],
        Mech.Biome.Grassland => [new(TerrainKind.Water, 6, 90, 150)],
        Mech.Biome.Volcanic => [new(TerrainKind.Lava, 12, 90, 200)],
        Mech.Biome.Toxic => [new(TerrainKind.Water, 18, 90, 180)],
        Mech.Biome.Valley => [new(TerrainKind.Water, 16, 90, 180)],
        _ => [],
    };
    private sealed record RiverRule(TerrainKind Kind, int Min, int Max);
    private static RiverRule[] RiverRules(Biome biome) => biome switch
    {
        Mech.Biome.Valley or Mech.Biome.Tropical => [new(TerrainKind.Water, 1, 2)],
        Mech.Biome.Highlands or Mech.Biome.Grassland => [new(TerrainKind.Water, 0, 1)],
        Mech.Biome.Arctic => [new(TerrainKind.Ice, 0, 1)],
        Mech.Biome.Volcanic => [new(TerrainKind.Lava, 1, 2)],
        _ => [],
    };

    /// <summary>What liquid generation must respect: clear areas, drop zones, settlement pads (the objective's first), volcanic cones,
    /// and the points the sea must leave connected.</summary>
    private sealed record LiquidSite(Func<Vector2, float> ClearDistance, Func<Vector2, float, bool> OutOfDropZones,
        IReadOnlyList<(Vector2 Center, float Radius)> Clusters, bool Objective, Func<float, Vector2> Place, Func<float, float, float> Between,
        IReadOnlyList<(Vector2 Center, float Radius)> Cones, IReadOnlyList<Vector2> Required);

    /// <summary>The generated liquids, and what rocks, vegetation and buildings must keep clear of.</summary>
    private sealed class LiquidPlan(LiquidMap map, float[] dryDistance, bool[] blocked, bool[] channel)
    {
        public LiquidMap Map { get; } = map;
        /// <summary>Nodes a river trench carved.</summary>
        public bool[] Channel { get; } = channel;
        /// <summary>Distance from a point to the nearest liquid or river trench.</summary>
        public float DryDistance(Vector2 point) => dryDistance[Node(point)];
        /// <summary>A building whose footprint reaches liquid or a river trench.</summary>
        public bool Floods(TerrainBuilding building)
        {
            var reach = building.Round ? new Vector2(building.HalfSize.X, building.HalfSize.X) : building.HalfSize;
            var (cos, sin) = (MathF.Cos(building.Rotation), MathF.Sin(building.Rotation));
            return new[] { Vector2.Zero, new(reach.X, reach.Y), new(-reach.X, reach.Y), new(reach.X, -reach.Y), new(-reach.X, -reach.Y) }
                .Any(local => blocked[Node(building.Center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos))]);
        }
    }

    /// <summary>Lakes, rivers and sea for a battlefield, carving the relief in place; null when a required river finds no course.</summary>
    private static LiquidPlan? GenerateLiquids(Biome biome, Random random, float[] relief, LiquidSite site)
    {
        var count = relief.Length;
        var kinds = new sbyte[count]; Array.Fill(kinds, (sbyte)-1);
        var surfaces = new float[count]; Array.Fill(surfaces, float.NaN);
        var owners = new int[count]; Array.Fill(owners, -1);
        var channel = new bool[count];
        var bodies = new List<LiquidBody>();
        var deep = biome is Mech.Biome.Tropical or Mech.Biome.Archipelago;
        var lowGround = relief.Order().ElementAt(relief.Length * 3 / 10);
        static bool Border(int index) => index % GridSize is 0 or GridSize - 1 || index / GridSize is 0 or GridSize - 1;
        // Lakes keep 150 m from clear areas and settlement pads, keep out of the drop zones and never merge with other bodies.
        bool Reserved(int index)
        {
            var point = Point(index);
            return Border(index) || site.ClearDistance(point) < 150 || !site.OutOfDropZones(point, 0)
                || site.Clusters.Any(cluster => cluster.Center.DistanceTo(point) < cluster.Radius + 150);
        }
        bool NearOther(int index, int body)
        {
            int x = index % GridSize, z = index / GridSize;
            for (var dz = -2; dz <= 2; dz++) for (var dx = -2; dx <= 2; dx++)
                if (x + dx is >= 0 and < GridSize && z + dz is >= 0 and < GridSize && owners[Index(x + dx, z + dz)] is >= 0 and var other && other != body) return true;
            return false;
        }
        int Add(LiquidBody body) { bodies.Add(body); return bodies.Count - 1; }
        void Fill(IEnumerable<int> nodes, float surface, TerrainKind kind, int body)
        {
            foreach (var node in nodes) { owners[node] = body; kinds[node] = (sbyte)kind; surfaces[node] = surface; }
        }

        if (biome == Mech.Biome.Archipelago)
        {
            var sea = Add(new(LiquidShape.Sea, TerrainKind.Water, []));
            Fill(Enumerable.Range(0, count).Where(index => relief[index] < SeaLevel), SeaLevel, TerrainKind.Water, sea);
        }

        // Lakes: flood a hollow from its lowest point until it covers its area or reaches its spill point.
        foreach (var rule in LakeRules(biome))
        {
            var cone = 0;
            for (var i = 0; i < rule.Count; i++)
            {
                var radius = site.Between(rule.MinRadius, rule.MaxRadius); var area = MathF.PI * radius * radius;
                for (var tries = 0; tries < 16; tries++)
                {
                    // Lava gathers first in the volcanic calderas.
                    var point = rule.Kind == TerrainKind.Lava && cone < site.Cones.Count ? site.Cones[cone++].Center : site.Place(radius + 150);
                    if (tries < 10 && rule.Kind != TerrainKind.Lava && relief[Node(point)] > lowGround + 1) continue;
                    var body = bodies.Count;
                    bool Blocked(int index) => Reserved(index) || owners[index] >= 0 || NearOther(index, body);
                    var start = Descend(relief, Node(point), Blocked);
                    if (Blocked(start)) continue;
                    var (cells, level) = Flood(relief, start, area, Blocked);
                    // A hollow too shallow for its lake is first sunk into an irregular bowl.
                    if (cells.Count * CellSize * CellSize < area * .4f)
                    {
                        var depth = deep ? site.Between(11, 16) : site.Between(4, 8);
                        Bowl(relief, start, MathF.Sqrt(area / MathF.PI) * 1.1f, depth, random.Next(), Blocked);
                        (cells, level) = Flood(relief, start, area, Blocked);
                    }
                    if (cells.Count < 4) continue;
                    Fill(cells, level, rule.Kind, Add(new(LiquidShape.Lake, rule.Kind, [])));
                    break;
                }
            }
        }

        // Rivers carve trenches between map edges, or from a volcanic cone, through the lakes on their way.
        var cones = site.Cones.ToList();
        var joined = new HashSet<int>();
        foreach (var rule in RiverRules(biome))
        {
            var rivers = rule.Min + random.Next(rule.Max - rule.Min + 1);
            for (var i = 0; i < rivers; i++)
            {
                var placed = false;
                for (var tries = 0; tries < 12 && !placed; tries++)
                    placed = River(rule.Kind);
                if (!placed) return null;
            }
        }
        bool River(TerrainKind kind)
        {
            var barrier = kind == TerrainKind.Lava || deep && kind == TerrainKind.Water;
            var trench = kind == TerrainKind.Lava ? site.Between(6, 10) : barrier ? site.Between(12, 14) : site.Between(6, 12);
            var depth = kind == TerrainKind.Lava ? 3 : barrier ? site.Between(9.5f, 11) : site.Between(2.5f, 4.5f);
            // A deep or lava river's impassable channel spans several grid cells, so it never breaks up between them.
            var width = kind == TerrainKind.Lava ? site.Between(90, 130) : barrier ? site.Between(110, 150) : site.Between(60, 120);
            var halfBed = Math.Max(barrier ? 30 : 10, width / 2 - depth / BankSlope);
            var meanderSeed = random.Next();
            float low = relief.Min(), high = relief.Max();
            // Rivers may run through settlements, whose streets bridge them, but never through a Take and Hold's objective.
            bool Forbidden(int cluster) => cluster == 0 && site.Objective;
            float PadFactor(Vector2 point)
            {
                for (var c = 0; c < site.Clusters.Count; c++)
                    if (site.Clusters[c].Center.DistanceTo(point) < site.Clusters[c].Radius + 60) return Forbidden(c) ? float.PositiveInfinity : 4;
                return 1;
            }
            var pads = Enumerable.Range(0, count).Select(index => PadFactor(Point(index))).ToArray();
            int start; Func<int, bool> goal; Vector2? target = null;
            if (kind == TerrainKind.Lava)
            {
                if (cones.Count == 0) return false;
                var source = cones[random.Next(cones.Count)]; cones.Remove(source);
                start = Node(source.Center);
                // Lava runs from the caldera to the map edge, or into a lava pool well clear of its own cone.
                goal = index => Border(index) || kinds[index] == (sbyte)TerrainKind.Lava && Point(index).DistanceTo(source.Center) > Math.Max(800, source.Radius);
            }
            else
            {
                int Perimeter()
                {
                    var t = random.NextSingle() * 4; var side = (int)t; var along = (int)((.08f + (t - side) * .84f) * (GridSize - 1));
                    return side switch { 0 => Index(along, 0), 1 => Index(GridSize - 1, along), 2 => Index(along, GridSize - 1), _ => Index(0, along) };
                }
                int a = Perimeter(), b = Perimeter();
                if (Point(a).DistanceTo(Point(b)) < 2400) return false;
                // Rivers run downhill: in where the edge is high, out where it is lower.
                (start, var end) = relief[a] >= relief[b] ? (a, b) : (b, a);
                goal = index => index == end; target = Point(end);
            }
            var meander = Enumerable.Range(0, count).Select(index => .3f + 1.6f * Fbm(meanderSeed, Point(index) / 500, 2)).ToArray();
            // Rivers never cross or share a lake: each keeps clear of the others and of lakes another river already joined.
            var otherRivers = DistanceTo(index => owners[index] >= 0 && (bodies[owners[index]].Shape == LiquidShape.River || joined.Contains(owners[index])));
            float Step(int from, int to)
            {
                if (float.IsPositiveInfinity(pads[to]) || otherRivers[to] < width / 2 + 75) return float.PositiveInfinity;
                var point = Point(to);
                var length = Point(from).DistanceTo(point);
                var lake = owners[to] >= 0 && bodies[owners[to]].Shape == LiquidShape.Lake && kinds[to] == (sbyte)kind ? .35f : 1;
                // Rivers leave the edge they enter by rather than running along it.
                var edge = CombatRules.ArenaHalfSize - Math.Max(Math.Abs(point.X), Math.Abs(point.Y));
                var hug = 1 + 3 * Math.Clamp(1 - edge / 360, 0, 1);
                var climb = Math.Max(0, relief[to] - relief[from]);
                return length * (1 + 2.5f * (relief[to] - low) / (high - low + 1)) * meander[to] * lake * pads[to] * hug
                    + climb * (kind == TerrainKind.Lava ? 10 : 6);
            }
            var route = Search(start, goal, target, Step);
            if (route is null) return false;
            var points = route.Where((_, i) => i % 3 == 0 || i == route.Count - 1).Select(Point).ToList();
            Vector2 Outward(int index)
            {
                var point = Point(index);
                return Math.Abs(point.X) >= Math.Abs(point.Y) ? new(Math.Sign(point.X), 0) : new(0, Math.Sign(point.Y));
            }
            // A river leaving the map keeps its trench to the very edge.
            if (Border(route[0])) points.Insert(0, points[0] + Outward(route[0]) * 90);
            if (Border(route[^1])) points.Add(points[^1] + Outward(route[^1]) * 90);
            var smooth = Resample(Smooth(points, 4), 10);
            // Meanders: the course winds from side to side, less so near its ends and near pads it must keep off.
            var winding = new List<Vector2>();
            for (var i = 0; i < smooth.Count; i++)
            {
                var tangent = (smooth[Math.Min(i + 1, smooth.Count - 1)] - smooth[Math.Max(i - 1, 0)]).Normalized();
                var along = i * 10f;
                var taper = Math.Clamp(Math.Min(along, (smooth.Count - 1) * 10f - along) / 250, 0, 1);
                var room = Enumerable.Range(0, site.Clusters.Count).Where(Forbidden)
                    .Select(c => (smooth[i].DistanceTo(site.Clusters[c].Center) - site.Clusters[c].Radius - 60) / 200).DefaultIfEmpty(1)
                    .Append((otherRivers[Node(smooth[i])] - 90 - width) / 200).Min();
                var offset = 130 * (Fbm(meanderSeed + 1, new Vector2(along / 520, 0), 2) * 2 - 1) * taper * Math.Clamp(room, 0, 1);
                winding.Add(smooth[i] + tangent.Orthogonal() * offset);
            }
            var samples = Resample(winding, 10);
            // A course that winds back beside itself would mix two reaches' levels: try another.
            for (var i = 0; i < samples.Count; i++)
                for (var j = i + 40; j < samples.Count; j++)
                    if (samples[i].DistanceSquaredTo(samples[j]) < (width + 90) * (width + 90)) return false;
            var ground = samples.Select(point => Sample(relief, point)).ToArray();
            // A river joins every lake its water reaches.
            int LakeAt(Vector2 point, int reach)
            {
                var (x, z) = Cell(point);
                for (var dz = -reach; dz <= reach; dz++) for (var dx = -reach; dx <= reach; dx++)
                    if (x + dx is >= 0 and < GridSize && z + dz is >= 0 and < GridSize && owners[Index(x + dx, z + dz)] is >= 0 and var owner
                        && bodies[owner].Shape == LiquidShape.Lake && kinds[Index(x + dx, z + dz)] == (sbyte)kind) return owner;
                return -1;
            }
            // Between its first and last touch of a lake the river stands at the lake's level, so leaving and re-entering it never runs uphill.
            int[] Spans()
            {
                var reach = (int)MathF.Ceiling((width / 2 + 45) / CellSize);
                var touch = samples.Select(point => LakeAt(point, reach)).ToArray();
                var span = (int[])touch.Clone();
                foreach (var lake in touch.Where(lake => lake >= 0).Distinct())
                    for (int s = Array.IndexOf(touch, lake), last = Array.LastIndexOf(touch, lake); s <= last; s++) span[s] = lake;
                return span;
            }
            // The surface only falls downstream. In a joined lake it is the lake's level, lowered if the river arrives lower.
            var levels = new Dictionary<int, float>();
            var steepest = kind == TerrainKind.Lava ? .06f : .015f;
            float[] Profile(int[] lakeOf)
            {
                var result = new float[samples.Count]; var previous = float.PositiveInfinity;
                for (var s = 0; s < samples.Count; s++)
                {
                    if (lakeOf[s] >= 0)
                    {
                        if (!levels.TryGetValue(lakeOf[s], out var level))
                            levels[lakeOf[s]] = level = Math.Min(surfaces[Array.IndexOf(owners, lakeOf[s])], previous);
                        result[s] = previous = level; continue;
                    }
                    var cap = ground[s] - trench + depth;
                    result[s] = previous = float.IsPositiveInfinity(previous) ? cap : Math.Min(previous - RiverGrade * 10, cap);
                }
                // Rapids: no stretch falls faster than its steepest grade, so the drop begins upstream, cutting a gorge if it must.
                for (var s = samples.Count - 2; s >= 0; s--)
                    if (lakeOf[s] < 0) result[s] = Math.Min(result[s], result[s + 1] + steepest * 10);
                return result;
            }
            Profile(Spans());
            foreach (var (lake, level) in levels)
                for (var index = 0; index < count; index++)
                    if (owners[index] == lake)
                    {
                        if (relief[index] < level) surfaces[index] = level;
                        else { owners[index] = -1; kinds[index] = -1; surfaces[index] = float.NaN; }
                    }
            // Where a lowered lake drew back, the river runs on in its own trench; only samples inside a lake leave the ground uncarved.
            var surface = Profile(Spans());
            joined.UnionWith(levels.Keys);
            var lakes = samples.Select(point => LakeAt(point, 0)).ToArray();
            // Fords: two or three spread along a deep or lava river, and wherever it crosses a clear area.
            var ford = new float[samples.Count];
            var fords = 0;
            if (barrier)
            {
                var centers = 2 + random.Next(2);
                for (var c = 0; c < centers; c++)
                {
                    var at = (int)(((c + .5f) / centers + (random.NextSingle() - .5f) * .3f / centers) * (samples.Count - 1));
                    var dry = Enumerable.Range(0, samples.Count).Where(s => lakes[s] < 0).OrderBy(s => Math.Abs(s - at)).DefaultIfEmpty(-1).First();
                    if (dry < 0) continue;
                    for (var s = 0; s < samples.Count; s++)
                        ford[s] = Math.Max(ford[s], Mathf.SmoothStep(0, 1, Math.Clamp((FordLength / 2 + 30 - Math.Abs(s - dry) * 10f) / 30, 0, 1)));
                }
                for (var s = 0; s < samples.Count; s++)
                    if (site.ClearDistance(samples[s]) < width / 2 + 90) ford[s] = 1;
                for (var s = 0; s < samples.Count; s++)
                    if (ford[s] > .99f && lakes[s] < 0 && (s == 0 || ford[s - 1] <= .99f || lakes[s - 1] >= 0)) fords++;
            }
            var bed = new float[samples.Count];
            for (var s = 0; s < samples.Count; s++)
                bed[s] = Mathf.Lerp(surface[s] - depth, kind == TerrainKind.Lava ? surface[s] + 1.5f : surface[s] - 2, ford[s]);
            // Carve the trench: the lower envelope of a flat bed with banks rising at BankSlope from every sample.
            var nearest = new int[count]; Array.Fill(nearest, -1);
            var distance = new float[count]; Array.Fill(distance, float.PositiveInfinity);
            for (var s = 0; s < samples.Count; s++)
            {
                if (lakes[s] >= 0) continue;
                var reach = Math.Min(450, halfBed + (Math.Max(ground[s], bed[s]) + 30 - bed[s]) / BankSlope);
                var (minX, minZ) = Cell(samples[s] - new Vector2(reach, reach)); var (maxX, maxZ) = Cell(samples[s] + new Vector2(reach, reach));
                for (var z = minZ; z <= maxZ; z++) for (var x = minX; x <= maxX; x++)
                {
                    var index = Index(x, z);
                    var d = Point(index).DistanceTo(samples[s]);
                    if (d > reach) continue;
                    if (d < distance[index]) { distance[index] = d; nearest[index] = s; }
                    if (float.IsPositiveInfinity(pads[index]) || owners[index] >= 0 && bodies[owners[index]].Shape == LiquidShape.River) continue;
                    var floor = bed[s] + Math.Max(0, d - halfBed) * BankSlope;
                    if (floor < relief[index]) { relief[index] = floor; channel[index] = true; }
                }
            }
            var river = Add(new(LiquidShape.River, kind, samples.Where((point, s) => Inside(point)).ToArray(), fords));
            for (var index = 0; index < count; index++)
                if (nearest[index] >= 0 && owners[index] < 0 && distance[index] <= halfBed + depth / BankSlope + 45 && relief[index] < surface[nearest[index]])
                    Fill([index], surface[nearest[index]], kind, river);
            return true;
        }

        Spill(relief, kinds, surfaces, owners, bodies, Reserved);
        // A lake that a lower river drained entirely is gone.
        var kept = Enumerable.Range(0, bodies.Count).Where(body => owners.Contains(body)).ToArray();
        var renumber = new int[bodies.Count]; Array.Fill(renumber, -1);
        for (var i = 0; i < kept.Length; i++) renumber[kept[i]] = i;
        for (var index = 0; index < count; index++) if (owners[index] >= 0) owners[index] = renumber[owners[index]];
        bodies = kept.Select(body => bodies[body]).ToList();
        // Rocks, vegetation and buildings keep clear of liquid and of river trenches.
        var blocked = new bool[count];
        for (var index = 0; index < count; index++)
            if (kinds[index] >= 0 || channel[index])
            {
                int x = index % GridSize, z = index / GridSize;
                blocked[index] = true;
                foreach (var next in Neighbours(x, z)) blocked[next] = true;
            }
        return new(new([.. bodies], kinds, surfaces, owners, deep), DistanceTo(index => kinds[index] >= 0 || channel[index]), blocked, channel);
    }

    private static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
    {
        var line = to - from; var t = line.LengthSquared() < 1e-6f ? 0 : Math.Clamp((point - from).Dot(line) / line.LengthSquared(), 0, 1);
        return point.DistanceTo(from + line * t);
    }
    private static bool Inside(Vector2 point) => Math.Abs(point.X) <= CombatRules.ArenaHalfSize && Math.Abs(point.Y) <= CombatRules.ArenaHalfSize;
    private static (int X, int Z) Cell(Vector2 point) =>
        (Math.Clamp((int)MathF.Round(point.X / CellSize + HalfCells), 0, GridSize - 1), Math.Clamp((int)MathF.Round(point.Y / CellSize + HalfCells), 0, GridSize - 1));
    /// <summary>A grid sampled like HeightAt.</summary>
    private static float Sample(float[] grid, Vector2 point)
    {
        var x = Math.Clamp(point.X / CellSize + HalfCells, 0, GridSize - 1); var z = Math.Clamp(point.Y / CellSize + HalfCells, 0, GridSize - 1);
        var ix = Math.Min((int)x, GridSize - 2); var iz = Math.Min((int)z, GridSize - 2); var u = x - ix; var v = z - iz;
        var a = grid[Index(ix, iz)]; var b = grid[Index(ix + 1, iz)]; var c = grid[Index(ix, iz + 1)]; var d = grid[Index(ix + 1, iz + 1)];
        return u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
    }

    /// <summary>Follows the steepest way down to a local low point.</summary>
    private static int Descend(float[] relief, int node, Func<int, bool> blocked)
    {
        for (var step = 0; step < 400; step++)
        {
            var lowest = node;
            foreach (var next in Neighbours(node % GridSize, node / GridSize))
                if (relief[next] < relief[lowest] && !blocked(next)) lowest = next;
            if (lowest == node) return node;
            node = lowest;
        }
        return node;
    }

    /// <summary>Pours liquid in at a node: the lowest ground around it floods first, and the level rises until the lake covers its area,
    /// meets ground it may not reach, or would spill over a saddle into lower ground beyond. Returns the flooded nodes and the surface.</summary>
    private static (List<int> Cells, float Level) Flood(float[] relief, int start, float area, Func<int, bool> blocked)
    {
        var open = new PriorityQueue<int, (float, int)>(); var seen = new HashSet<int> { start };
        open.Enqueue(start, (relief[start], start));
        var added = new List<int>(); var level = relief[start]; var top = level;
        var target = area / (CellSize * CellSize);
        while (open.TryDequeue(out var node, out var priority))
        {
            var height = priority.Item1;
            // Lower ground beyond the highest point flooded so far: that point was the saddle, and the lake stops just below it.
            if (height < level - 1e-4f) { top = level - .3f; break; }
            if (blocked(node) || added.Count >= target) { top = Math.Min(height, level + 1); break; }
            level = Math.Max(level, height); added.Add(node); top = level + 1;
            foreach (var next in Neighbours(node % GridSize, node / GridSize))
                if (seen.Add(next)) open.Enqueue(next, (relief[next], next));
        }
        return (added.Where(node => relief[node] < top).ToList(), top);
    }

    /// <summary>Sinks an irregular bowl around a node, deepest at its centre, leaving reserved ground alone.</summary>
    private static void Bowl(float[] relief, int center, float radius, float depth, int seed, Func<int, bool> blocked)
    {
        var middle = Point(center); var floor = relief[center];
        var (minX, minZ) = Cell(middle - new Vector2(radius * 1.3f, radius * 1.3f)); var (maxX, maxZ) = Cell(middle + new Vector2(radius * 1.3f, radius * 1.3f));
        for (var z = minZ; z <= maxZ; z++) for (var x = minX; x <= maxX; x++)
        {
            var index = Index(x, z);
            var offset = Point(index) - middle; var length = offset.Length();
            var direction = length < 1e-3f ? Vector2.Right : offset / length;
            var reach = radius * (.75f + .55f * ValueNoise(seed, direction * 1.7f + new Vector2(5, 5)));
            var s = length / reach;
            if (s >= 1 || blocked(index) && index != center) continue;
            relief[index] = Math.Min(relief[index], floor - depth * (1 - s * s));
        }
    }

    /// <summary>A* over the grid's eight neighbours, from a node to the first goal reached.</summary>
    private static List<int>? Search(int start, Func<int, bool> goal, Vector2? target, Func<int, int, float> step)
    {
        var costs = new float[GridSize * GridSize]; Array.Fill(costs, float.PositiveInfinity);
        var previous = new int[costs.Length]; Array.Fill(previous, -1);
        float Estimate(int index) => target is { } at ? Point(index).DistanceTo(at) * .2f : 0;
        var open = new PriorityQueue<int, float>(); costs[start] = 0; open.Enqueue(start, Estimate(start));
        while (open.TryDequeue(out var current, out var priority))
        {
            if (priority > costs[current] + Estimate(current) + 1e-3f) continue;
            if (current != start && goal(current))
            {
                var route = new List<int>();
                for (var node = current; node >= 0; node = previous[node]) route.Add(node);
                route.Reverse(); return route;
            }
            foreach (var next in Neighbours(current % GridSize, current / GridSize))
            {
                var cost = step(current, next);
                if (float.IsPositiveInfinity(cost) || costs[current] + cost >= costs[next]) continue;
                costs[next] = costs[current] + cost; previous[next] = current; open.Enqueue(next, costs[next] + Estimate(next));
            }
        }
        return null;
    }

    /// <summary>Chaikin corner cutting, keeping both ends.</summary>
    private static List<Vector2> Smooth(List<Vector2> points, int iterations)
    {
        for (var i = 0; i < iterations && points.Count > 2; i++)
        {
            var next = new List<Vector2> { points[0] };
            for (var p = 0; p < points.Count - 1; p++) { next.Add(points[p].Lerp(points[p + 1], .25f)); next.Add(points[p].Lerp(points[p + 1], .75f)); }
            next.Add(points[^1]); points = next;
        }
        return points;
    }
    private static List<Vector2> Resample(List<Vector2> points, float spacing)
    {
        var result = new List<Vector2> { points[0] }; var carried = 0f;
        for (var p = 0; p < points.Count - 1; p++)
        {
            var from = points[p]; var length = from.DistanceTo(points[p + 1]);
            for (var at = spacing - carried; at <= length; at += spacing) result.Add(from.Lerp(points[p + 1], at / length));
            carried = (carried + length) % spacing;
        }
        if (result[^1].DistanceTo(points[^1]) > 1) result.Add(points[^1]);
        return result;
    }

    /// <summary>Where a lake or river stands above neighbouring ground, the liquid spills: small hollows join the body, larger ones fill
    /// as lakes at its level. A spill that would run on into lower liquid, reserved ground or open country meets a raised bank instead.</summary>
    private static void Spill(float[] relief, sbyte[] kinds, float[] surfaces, int[] owners, List<LiquidBody> bodies, Func<int, bool> reserved)
    {
        var seen = new bool[relief.Length];
        for (var source = 0; source < relief.Length; source++)
        {
            if (owners[source] < 0 || bodies[owners[source]].Shape == LiquidShape.Sea) continue;
            foreach (var first in Neighbours(source % GridSize, source / GridSize))
            {
                if (owners[first] >= 0 || seen[first] || relief[first] >= surfaces[source]) continue;
                var level = surfaces[source];
                var region = new List<int> { first }; seen[first] = true;
                var spills = false;
                for (var i = 0; i < region.Count && !spills; i++)
                {
                    if (reserved(region[i]) || region.Count > 220) { spills = true; break; }
                    foreach (var next in Neighbours(region[i] % GridSize, region[i] / GridSize))
                    {
                        if (owners[next] >= 0 && surfaces[next] < level - .25f) { spills = true; break; }
                        if (owners[next] < 0 && !seen[next] && relief[next] < level) { seen[next] = true; region.Add(next); }
                    }
                }
                if (spills)
                {
                    // Raise the bank beside the higher liquid; what lies beyond stays dry or drains to lower liquid.
                    foreach (var node in region)
                        if (Neighbours(node % GridSize, node / GridSize).Any(next => owners[next] >= 0 && surfaces[next] >= level - .25f))
                            relief[node] = Math.Max(relief[node], level + .5f);
                    foreach (var node in region) seen[node] = false;
                    continue;
                }
                var body = region.Count >= 9 ? bodies.Count : owners[source];
                if (region.Count >= 9) bodies.Add(new(LiquidShape.Lake, bodies[owners[source]].Kind, []));
                foreach (var node in region) { owners[node] = body; kinds[node] = kinds[source]; surfaces[node] = level; }
            }
        }
    }

    /// <summary>Raises shallow sand bars across deep sea until every required point shares one wadeable landmass, counting bridges as land.</summary>
    private static void SandBars(float[] relief, IReadOnlyList<Vector2> required, Func<int, bool> bridged)
    {
        var floor = SeaLevel - DeepWaterDepth + 1;
        for (var round = 0; round < 12; round++)
        {
            var labels = new int[relief.Length]; var label = 0;
            for (var index = 0; index < relief.Length; index++)
            {
                if (labels[index] != 0 || relief[index] < floor && !bridged(index)) continue;
                var open = new Queue<int>(); open.Enqueue(index); labels[index] = ++label;
                while (open.TryDequeue(out var node))
                    foreach (var next in Neighbours(node % GridSize, node / GridSize))
                        if (labels[next] == 0 && (relief[next] >= floor || bridged(next))) { labels[next] = label; open.Enqueue(next); }
            }
            var main = labels[Node(required[0])];
            var apart = required.Select(point => labels[Node(point)]).Where(other => other != main).ToHashSet();
            if (apart.Count == 0) return;
            // The cheapest crossing from the first point's land to any land still apart, through the shallowest sea.
            var costs = new float[relief.Length]; Array.Fill(costs, float.PositiveInfinity);
            var previous = new int[relief.Length]; Array.Fill(previous, -1);
            var queue = new PriorityQueue<int, float>();
            for (var index = 0; index < relief.Length; index++) if (labels[index] == main) { costs[index] = 0; queue.Enqueue(index, 0); }
            var reached = -1;
            while (queue.TryDequeue(out var node, out var cost))
            {
                if (cost > costs[node]) continue;
                if (apart.Contains(labels[node])) { reached = node; break; }
                foreach (var next in Neighbours(node % GridSize, node / GridSize))
                {
                    var step = Point(node).DistanceTo(Point(next)) * (relief[next] >= floor ? 1 : 1 + (floor - relief[next]) / 4);
                    if (cost + step < costs[next]) { costs[next] = cost + step; previous[next] = node; queue.Enqueue(next, cost + step); }
                }
            }
            if (reached < 0) return;
            for (var node = reached; node >= 0; node = previous[node])
            {
                if (relief[node] >= floor) continue;
                var middle = Point(node);
                var (minX, minZ) = Cell(middle - new Vector2(90, 90)); var (maxX, maxZ) = Cell(middle + new Vector2(90, 90));
                for (var z = minZ; z <= maxZ; z++) for (var x = minX; x <= maxX; x++)
                {
                    var index = Index(x, z);
                    var bar = SeaLevel - 3 - Math.Max(0, Point(index).DistanceTo(middle) - 30) * .3f;
                    relief[index] = Math.Max(relief[index], bar);
                }
            }
        }
    }

    /// <summary>Approximate distance from every node to the nearest node matching, by a two-pass chamfer transform.</summary>
    private static float[] DistanceTo(Func<int, bool> source)
    {
        const float straight = CellSize, diagonal = CellSize * 1.41421356f;
        var distance = new float[GridSize * GridSize];
        for (var index = 0; index < distance.Length; index++) distance[index] = source(index) ? 0 : float.PositiveInfinity;
        for (var z = 0; z < GridSize; z++) for (var x = 0; x < GridSize; x++)
        {
            var index = Index(x, z); var d = distance[index];
            if (x > 0) d = Math.Min(d, distance[index - 1] + straight);
            if (z > 0) d = Math.Min(d, distance[index - GridSize] + straight);
            if (x > 0 && z > 0) d = Math.Min(d, distance[index - GridSize - 1] + diagonal);
            if (x < GridSize - 1 && z > 0) d = Math.Min(d, distance[index - GridSize + 1] + diagonal);
            distance[index] = d;
        }
        for (var z = GridSize - 1; z >= 0; z--) for (var x = GridSize - 1; x >= 0; x--)
        {
            var index = Index(x, z); var d = distance[index];
            if (x < GridSize - 1) d = Math.Min(d, distance[index + 1] + straight);
            if (z < GridSize - 1) d = Math.Min(d, distance[index + GridSize] + straight);
            if (x < GridSize - 1 && z < GridSize - 1) d = Math.Min(d, distance[index + GridSize + 1] + diagonal);
            if (x > 0 && z < GridSize - 1) d = Math.Min(d, distance[index + GridSize - 1] + diagonal);
            distance[index] = d;
        }
        return distance;
    }
}
