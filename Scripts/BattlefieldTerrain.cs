using Godot;
using System.Buffers;

namespace Mech;

public enum TerrainKind { Rock, Vegetation, Water, Ice, Lava, DeepWater }
public sealed record TerrainPatch(TerrainKind Kind, Vector2 Center, float Radius, float Height = 0);
public sealed record TerrainHill(Vector2 Center, float Radius, float Height);
public enum BuildingStyle { House, Barn, Block, Silo, Tank, Mast, Dome, Module, Stack }
/// <summary>A solid building. Rectangular footprints are rotated by Rotation radians from the X axis toward Z;
/// round footprints use HalfSize.X as their radius. Height is above the ground at Center.</summary>
public sealed record TerrainBuilding(BuildingStyle Style, int Cluster, Vector2 Center, Vector2 HalfSize, float Rotation, float Height)
{
    public bool Round => IsRound(Style);
    public static bool IsRound(BuildingStyle style) => style is BuildingStyle.Silo or BuildingStyle.Tank or BuildingStyle.Mast or BuildingStyle.Dome or BuildingStyle.Stack;
}

/// <summary>Immutable terrain shared by simulation, navigation, rendering and picking. X/Y are ground X/Z.</summary>
public sealed class BattlefieldTerrain
{
    /// <summary>Metres between heightfield and navigation grid points.</summary>
    public const float CellSize = 30;
    private const int HalfCells = (int)(CombatRules.ArenaHalfSize / CellSize);
    public const int GridSize = HalfCells * 2 + 1;
    public const float MaxSlope = .65f;
    public const float EyeHeight = 22.5f;
    public const float WaterSpeed = .5f;
    public const float VegetationSpeed = .7f;
    public const int MaxVegetationModifier = 2;
    /// <summary>Clear width between neighbouring buildings of a cluster, and the lot each building stands on.</summary>
    public const float StreetWidth = 60, BuildingLot = 50;
    public int Seed { get; }
    /// <summary>The generated biome; hand-built test terrain has none and therefore no climate.</summary>
    public Biome? Biome { get; }
    public IReadOnlyList<TerrainPatch> Patches { get; }
    public IReadOnlyList<TerrainHill> Hills { get; }
    public IReadOnlyList<TerrainBuilding> Buildings { get; }
    private readonly float[] _heights = new float[GridSize * GridSize];
    private readonly bool[] _walkable = new bool[GridSize * GridSize];
    private readonly int[] _components = new int[GridSize * GridSize];
    private readonly record struct NavigationEdge(int Node, float Cost);
    private readonly record struct FiringCandidate(int Node, float Cost) : IComparable<FiringCandidate>
    {
        public int CompareTo(FiringCandidate other)
        {
            var cost = Cost.CompareTo(other.Cost);
            return cost != 0 ? cost : Node.CompareTo(other.Node);
        }
    }
    /// <summary>A rock or building: a vertical prism from Bottom to Top over a circle or rotated rectangle.</summary>
    private readonly record struct Solid(Vector2 Center, Vector2 HalfSize, float Cos, float Sin, bool Round, float Bottom, float Top)
    {
        public Vector2 Local(Vector2 point)
        {
            var offset = point - Center;
            return new(offset.X * Cos + offset.Y * Sin, -offset.X * Sin + offset.Y * Cos);
        }
        public bool Contains(Vector2 point, float margin)
        {
            if (Round) return point.DistanceTo(Center) < HalfSize.X + margin;
            var local = Local(point);
            return Math.Abs(local.X) < HalfSize.X + margin && Math.Abs(local.Y) < HalfSize.Y + margin;
        }
        public float Reach => Round ? HalfSize.X : HalfSize.Length();
    }
    private const float SolidCellSize = 240;
    private const int SolidCells = (int)(CombatRules.ArenaHalfSize * 2 / SolidCellSize) + 1;
    private readonly List<NavigationEdge>[] _neighbors;
    private readonly bool _flat;
    private readonly Solid[] _solids;
    /// <summary>Lava and deep water: flat, open to sight and fire, but no mech may stand in them.</summary>
    private readonly TerrainPatch[] _barriers;
    /// <summary>Solids near each coarse cell, for point queries: every solid within its reach plus mech clearance.</summary>
    private readonly int[][] _solidCells;
    public static BattlefieldTerrain Flat { get; } = new(0, [], []);

    internal BattlefieldTerrain(int seed, IEnumerable<TerrainPatch> patches, IEnumerable<TerrainHill> hills,
        IEnumerable<TerrainBuilding>? buildings = null, Biome? biome = null, float[]? relief = null)
    {
        Seed = seed; Biome = biome; Patches = Array.AsReadOnly(patches.ToArray()); Hills = Array.AsReadOnly(hills.ToArray());
        Buildings = Array.AsReadOnly((buildings ?? []).ToArray());
        _flat = Patches.Count == 0 && Hills.Count == 0 && Buildings.Count == 0 && relief is null;
        for (var z = 0; z < GridSize; z++) for (var x = 0; x < GridSize; x++)
        {
            var point = new Vector2(x - HalfCells, z - HalfCells) * CellSize;
            _heights[Index(x, z)] = (relief?[Index(x, z)] ?? 0) + Hills.Sum(hill => point.DistanceTo(hill.Center) >= hill.Radius ? 0
                : hill.Height * (1 + MathF.Cos(MathF.PI * point.DistanceTo(hill.Center) / hill.Radius)) * .5f);
        }
        // Water and ice are flat at the level of their centre, with gently blended banks. Each point follows only
        // its nearest-flattening patch, so neighbouring banks never tilt another surface.
        var flats = Patches.Where(patch => IsFlat(patch.Kind))
            .Select(patch => (Patch: patch, Level: _heights[Node(patch.Center)])).ToArray();
        if (flats.Length > 0)
            for (var index = 0; index < _heights.Length; index++)
            {
                var blend = 1f; var level = 0f;
                foreach (var (patch, patchLevel) in flats)
                {
                    var weight = Mathf.SmoothStep(0, 1, Math.Clamp((Point(index).DistanceTo(patch.Center) - patch.Radius - 30) / 90, 0, 1));
                    if (weight < blend) { blend = weight; level = patchLevel; }
                }
                _heights[index] = Mathf.Lerp(level, _heights[index], blend);
            }
        _solids = Patches.Where(patch => patch.Kind == TerrainKind.Rock)
            .Select(rock => new Solid(rock.Center, new(rock.Radius, rock.Radius), 1, 0, true,
                Math.Min(-15, HeightAt(rock.Center) - 15), HeightAt(rock.Center) + rock.Height))
            .Concat(Buildings.Select(building => new Solid(building.Center, building.HalfSize, MathF.Cos(building.Rotation),
                MathF.Sin(building.Rotation), building.Round, Math.Min(-15, HeightAt(building.Center) - 15), HeightAt(building.Center) + building.Height)))
            .ToArray();
        _barriers = Patches.Where(patch => patch.Kind is TerrainKind.Lava or TerrainKind.DeepWater).ToArray();
        _solidCells = new int[SolidCells * SolidCells][];
        var cells = Enumerable.Range(0, _solidCells.Length).Select(_ => new List<int>()).ToArray();
        for (var i = 0; i < _solids.Length; i++)
        {
            var reach = _solids[i].Reach + CombatRules.UnitRadius + 1.5f;
            var (minX, minZ) = SolidCell(_solids[i].Center - new Vector2(reach, reach));
            var (maxX, maxZ) = SolidCell(_solids[i].Center + new Vector2(reach, reach));
            for (var z = minZ; z <= maxZ; z++) for (var x = minX; x <= maxX; x++) cells[z * SolidCells + x].Add(i);
        }
        for (var i = 0; i < cells.Length; i++) _solidCells[i] = cells[i].ToArray();
        _neighbors = Enumerable.Range(0, _heights.Length).Select(_ => new List<NavigationEdge>(8)).ToArray();
        // Terrain never changes: evaluate traversal speeds once instead of for every A* expansion. Rows run in
        // parallel: every check only reads the finished heights and solids, and each row writes its own nodes.
        var speeds = new float[_heights.Length];
        Parallel.For(0, GridSize, z =>
        {
            for (var x = 0; x < GridSize; x++)
            {
                var index = Index(x, z);
                var point = Point(index);
                _walkable[index] = Passable(point);
                if (_walkable[index]) speeds[index] = SpeedFactor(point);
            }
        });
        // Each pair of neighbouring nodes is tested once, in one direction, and gives the edge both ways. Edges are kept
        // in the order of a dz-then-dx scan around each node, so A* breaks ties as it always has. Only interior nodes
        // have edges. Every pair owns its two slots, so rows run in parallel and the result never depends on timing.
        var slots = new NavigationEdge?[_heights.Length * 8];
        static int Slot(int dx, int dz) { var slot = (dz + 1) * 3 + dx + 1; return slot > 4 ? slot - 1 : slot; }
        static bool Interior(int x, int z) => x > 0 && z > 0 && x < GridSize - 1 && z < GridSize - 1;
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 1), (0, 1), (1, 1) })
            Parallel.For(0, GridSize, z =>
            {
                for (var x = 0; x < GridSize; x++)
                {
                    int nextX = x + dx, nextZ = z + dz;
                    if (nextX < 0 || nextX >= GridSize || nextZ >= GridSize) continue;
                    int index = Index(x, z), next = Index(nextX, nextZ);
                    bool inner = Interior(x, z), nextInner = Interior(nextX, nextZ);
                    if ((!inner && !nextInner) || !_walkable[index] || !_walkable[next] || !CanTraverseBetweenWalkable(Point(index), Point(next))) continue;
                    var distance = Point(index).DistanceTo(Point(next));
                    if (inner) slots[index * 8 + Slot(dx, dz)] = new(next, distance / speeds[next]);
                    if (nextInner) slots[next * 8 + Slot(-dx, -dz)] = new(index, distance / speeds[index]);
                }
            });
        for (var index = 0; index < _heights.Length; index++)
            for (var slot = 0; slot < 8; slot++) if (slots[index * 8 + slot] is { } edge) _neighbors[index].Add(edge);
        var component = 0;
        for (var index = 0; index < _heights.Length; index++)
        {
            if (!_walkable[index] || _components[index] != 0) continue;
            var open = new Queue<int>(); open.Enqueue(index); _components[index] = ++component;
            while (open.TryDequeue(out var current)) foreach (var edge in _neighbors[current])
                if (_components[edge.Node] == 0) { _components[edge.Node] = component; open.Enqueue(edge.Node); }
        }
    }
    private static (int X, int Z) SolidCell(Vector2 point) =>
        (Math.Clamp((int)((point.X + CombatRules.ArenaHalfSize) / SolidCellSize), 0, SolidCells - 1),
         Math.Clamp((int)((point.Y + CombatRules.ArenaHalfSize) / SolidCellSize), 0, SolidCells - 1));
    private int[] SolidsNear(Vector2 point) { var (x, z) = SolidCell(point); return _solidCells[z * SolidCells + x]; }

    /// <summary>Feature counts and sizes per biome; recorded in docs/combat-tuning.md.</summary>
    private sealed record PatchRule(TerrainKind Kind, int Count, float MinRadius, float MaxRadius, float MinHeight = 0, float MaxHeight = 0);
    private static PatchRule[] PatchRules(Biome biome) => biome switch
    {
        Mech.Biome.Arctic => [new(TerrainKind.Rock, 56, 39, 90, 90, 180), new(TerrainKind.Vegetation, 24, 60, 120), new(TerrainKind.Ice, 14, 120, 240)],
        Mech.Biome.Lunar => [new(TerrainKind.Rock, 84, 24, 60, 30, 100)],
        Mech.Biome.Highlands => [new(TerrainKind.Rock, 64, 39, 90, 90, 180), new(TerrainKind.Vegetation, 28, 60, 120), new(TerrainKind.Water, 8, 60, 120)],
        Mech.Biome.Desert => [new(TerrainKind.Rock, 48, 45, 110, 60, 200), new(TerrainKind.Vegetation, 10, 45, 90), new(TerrainKind.Water, 4, 60, 90)],
        Mech.Biome.Tropical => [new(TerrainKind.Water, 18, 90, 180), new(TerrainKind.Vegetation, 72, 90, 200), new(TerrainKind.Rock, 24, 39, 90, 90, 180)],
        Mech.Biome.Grassland => [new(TerrainKind.Rock, 16, 39, 90, 60, 140), new(TerrainKind.Vegetation, 14, 60, 120), new(TerrainKind.Water, 6, 90, 150)],
        Mech.Biome.Archipelago => [new(TerrainKind.DeepWater, 16, 180, 360), new(TerrainKind.Water, 14, 90, 180), new(TerrainKind.Vegetation, 24, 60, 120),
            new(TerrainKind.Rock, 20, 39, 90, 90, 180)],
        Mech.Biome.Volcanic => [new(TerrainKind.Lava, 12, 90, 200), new(TerrainKind.Rock, 64, 39, 90, 90, 180)],
        Mech.Biome.Toxic => [new(TerrainKind.Water, 18, 90, 180), new(TerrainKind.Vegetation, 20, 60, 120), new(TerrainKind.Rock, 40, 39, 90, 90, 180)],
        _ => [new(TerrainKind.Rock, 40, 39, 90, 90, 180), new(TerrainKind.Vegetation, 48, 90, 180), new(TerrainKind.Water, 16, 90, 180)],
    };
    public static IReadOnlyList<TerrainKind> Kinds(Biome biome) => PatchRules(biome).Select(rule => rule.Kind).ToArray();
    /// <summary>Kinds levelled flat at their centre's height and placed on low ground.</summary>
    public static bool IsFlat(TerrainKind kind) => kind is TerrainKind.Water or TerrainKind.Ice or TerrainKind.Lava or TerrainKind.DeepWater;
    public const int MinClusters = 1, MaxClusters = 6, MinBuildings = 15, MaxBuildings = 50;

    /// <summary>A circle of ground that generation keeps clear and connected, such as a patrol point.</summary>
    public readonly record struct Clearing(Vector2 Center, float Radius);

    public static BattlefieldTerrain Generate(int seed, Biome biome = Mech.Biome.Valley, IReadOnlyList<Clearing>? clearings = null)
    {
        var random = new Random(seed);
        // Spawn bands and a central rendezvous remain clear for every force size, and so do any requested clearings.
        float Standard(Vector2 point) => Math.Min(point.Length() - 60, new[] { CombatRules.DeploymentDepth, -CombatRules.DeploymentDepth }
            .Min(z => new Vector2(Math.Max(0, Math.Abs(point.X) - CombatRules.DeploymentSpacing * 1.5f - 90),
                Math.Max(0, Math.Abs(point.Y - z) - 150)).Length()));
        float ClearDistance(Vector2 point) => clearings is null || clearings.Count == 0 ? Standard(point)
            : Math.Min(Standard(point), clearings.Min(clearing => point.DistanceTo(clearing.Center) - clearing.Radius));
        bool Clear(Vector2 point, float radius) => ClearDistance(point) > radius;
        Vector2 Place(float radius)
        {
            for (var attempt = 0; attempt < 500; attempt++)
            {
                var placementLimit = CombatRules.ArenaHalfSize - 300;
                var point = new Vector2((random.NextSingle() * 2 - 1) * placementLimit, (random.NextSingle() * 2 - 1) * placementLimit);
                if (Clear(point, radius)) return point;
            }
            return new Vector2(CombatRules.ArenaHalfSize * .625f, 0);
        }
        float Between(float min, float max) => min + random.NextSingle() * (max - min);
        // Retry complete candidates instead of silently accepting disconnected spawn areas.
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var relief = Relief(biome, random, Place, Between);
            for (var index = 0; index < relief.Length; index++)
                relief[index] *= Mathf.SmoothStep(0, 1, Math.Clamp(ClearDistance(Point(index)) / 400, 0, 1));
            var clusters = new List<(Vector2 Center, float Radius)>();
            var buildings = Settlements(biome, random, relief, clusters, Place, Between);
            if (buildings is null) continue;
            var patches = new List<TerrainPatch>();
            // Lakes and frozen lakes keep to the lowest ground, so levelling them leaves gentle banks.
            var lowGround = relief.Order().ElementAt(relief.Length * 3 / 10);
            // Banks blend up to 120 m beyond a lake, so lakes keep that far from pads and spawns.
            bool Free(Vector2 point, float radius) => patches.All(patch => patch.Center.DistanceTo(point) >= patch.Radius + radius + 30)
                && clusters.All(cluster => cluster.Center.DistanceTo(point) >= cluster.Radius + radius + 150);
            foreach (var rule in PatchRules(biome))
            {
                // Lunar boulders gather in fields; other features scatter.
                var fields = rule.Kind == TerrainKind.Rock && biome == Mech.Biome.Lunar
                    ? Enumerable.Range(0, 12).Select(_ => Place(300)).ToArray() : null;
                var flat = IsFlat(rule.Kind); var clearance = flat ? 150 : 60;
                for (var i = 0; i < rule.Count; i++)
                {
                    var radius = Between(rule.MinRadius, rule.MaxRadius);
                    Vector2 point;
                    var tries = 0;
                    do
                    {
                        point = fields is null ? Place(radius + clearance)
                            : fields[i % fields.Length] + Vector2.FromAngle(random.NextSingle() * MathF.Tau) * (random.NextSingle() * 300);
                        tries++;
                    }
                    while (tries < 500 && (!Free(point, radius) || !Clear(point, radius + clearance)
                        || (flat && tries < 400 && relief[Node(point)] > lowGround + 1)));
                    if (!Free(point, radius) || !Clear(point, radius + clearance)) continue;
                    patches.Add(new(rule.Kind, point, radius, rule.MaxHeight > 0 ? Between(rule.MinHeight, rule.MaxHeight) : 0));
                }
            }
            var terrain = new BattlefieldTerrain(seed, patches, [], buildings, biome, relief);
            var starts = Enumerable.Range(-3, 7).Select(index => index * CombatRules.DeploymentSpacing / 2)
                .SelectMany(x => new[] { new Vector2(x, CombatRules.DeploymentDepth), new Vector2(x, -CombatRules.DeploymentDepth) })
                .Append(Vector2.Zero).Concat(clearings?.Select(clearing => clearing.Center) ?? []).ToArray();
            var component = terrain.Component(starts[0]);
            if (component != 0 && starts.All(point => terrain.Passable(point) && terrain.Component(point) == component)
                && PatchRules(biome).All(rule => patches.Any(patch => patch.Kind == rule.Kind))) return terrain;
        }
        throw new InvalidOperationException("Could not generate a connected battlefield.");
    }

    /// <summary>Grid heights before patches and settlements, shaped per biome; recorded in docs/combat-tuning.md.</summary>
    private static float[] Relief(Biome biome, Random random, Func<float, Vector2> place, Func<float, float, float> between)
    {
        var noiseSeed = random.Next(); var detailSeed = random.Next();
        // A second large-scale field, derived rather than drawn so existing biomes keep their random sequence.
        var shapeSeed = noiseSeed ^ 0x5BD1E995;
        var relief = new float[GridSize * GridSize];
        for (var index = 0; index < relief.Length; index++)
        {
            var point = Point(index);
            relief[index] = biome switch
            {
                Mech.Biome.Arctic => 270 * MathF.Pow(Ridged(noiseSeed, point / 1200, 3), 2.4f) + 10 * Fbm(detailSeed, point / 420, 2),
                Mech.Biome.Lunar => 45 * Fbm(noiseSeed, point / 1800, 3) + 6 * Fbm(detailSeed, point / 360, 2),
                Mech.Biome.Highlands => 300 * Mathf.SmoothStep(.3f, .85f, Fbm(noiseSeed, point / 1600, 3))
                    + 90 * MathF.Pow(Ridged(shapeSeed, point / 800, 3), 3) + 10 * Fbm(detailSeed, point / 420, 2),
                // Dunes: ridges stretched across the X axis over gently rolling sand.
                Mech.Biome.Desert => 70 * Fbm(shapeSeed, point / 2200, 2) + 22 * MathF.Pow(Ridged(noiseSeed, new Vector2(point.X / 650, point.Y / 260), 2), 2)
                    + 4 * Fbm(detailSeed, point / 300, 2),
                Mech.Biome.Tropical => 170 * Mathf.SmoothStep(.3f, .8f, Fbm(noiseSeed, point / 1000, 3)) + 12 * Fbm(detailSeed, point / 300, 2),
                Mech.Biome.Grassland => 60 * Fbm(noiseSeed, point / 2400, 3) + 4 * Fbm(detailSeed, point / 420, 2),
                Mech.Biome.Archipelago => 150 * Mathf.SmoothStep(.45f, .8f, Fbm(noiseSeed, point / 1100, 3)) + 6 * Fbm(detailSeed, point / 420, 2),
                Mech.Biome.Volcanic => 60 * Ridged(noiseSeed, point / 900, 3) + 8 * Fbm(detailSeed, point / 300, 2),
                Mech.Biome.Toxic => 140 * Mathf.SmoothStep(.35f, .75f, Fbm(noiseSeed, point / 1200, 3)) + 40 * Ridged(shapeSeed, point / 500, 2)
                    + 6 * Fbm(detailSeed, point / 300, 2),
                _ => 230 * Mathf.SmoothStep(.38f, .8f, Fbm(noiseSeed, point / 1400, 3)) + 8 * Fbm(detailSeed, point / 420, 2),
            };
        }
        if (biome == Mech.Biome.Volcanic)
            for (var i = 0; i < 5; i++)
            {
                // Volcanic cones with a sunken caldera at the summit.
                var radius = between(350, 650); var center = place(radius + 120);
                var height = radius * .35f; var caldera = .22f;
                for (var index = 0; index < relief.Length; index++)
                {
                    var d = Point(index).DistanceTo(center) / radius;
                    if (d >= 1) continue;
                    var cone = height * MathF.Pow(1 - Math.Max(d, caldera), 1.4f);
                    relief[index] += d < caldera ? cone - height * .25f * (1 - d / caldera) * (1 - d / caldera) : cone;
                }
            }
        if (biome != Mech.Biome.Lunar) return relief;
        for (var i = 0; i < 40; i++)
        {
            // Many small craters and a few large ones, each a bowl with a raised rim.
            var radius = 60 + 420 * MathF.Pow(random.NextSingle(), 3);
            var center = place(radius * 1.6f + 120);
            var depth = radius * between(.15f, .3f); var rim = radius * .06f;
            for (var index = 0; index < relief.Length; index++)
            {
                var d = Point(index).DistanceTo(center) / radius;
                if (d > 2.2f) continue;
                relief[index] += d < 1 ? -depth + (depth + rim) * MathF.Pow(d, 2.4f) : rim * MathF.Exp(-3 * ((d - 1) / .4f) * ((d - 1) / .4f));
            }
        }
        return relief;
    }

    /// <summary>Building clusters on levelled pads; null when the clusters do not fit.</summary>
    private static List<TerrainBuilding>? Settlements(Biome biome, Random random, float[] relief, List<(Vector2 Center, float Radius)> placed,
        Func<float, Vector2> place, Func<float, float, float> between)
    {
        var clusterCount = MinClusters + random.Next(MaxClusters - MinClusters + 1);
        var total = MinBuildings + random.Next(MaxBuildings - MinBuildings + 1);
        var weights = Enumerable.Range(0, clusterCount).Select(_ => .5f + random.NextSingle()).ToArray();
        var sizes = weights.Select(weight => Math.Max(1, (int)(total * weight / weights.Sum()))).ToArray();
        for (var i = 0; sizes.Sum() < total; i = (i + 1) % clusterCount) sizes[i]++;
        for (var i = 0; sizes.Sum() > total; i = (i + 1) % clusterCount) if (sizes[i] > 1) sizes[i]--;
        const float pitch = StreetWidth + BuildingLot;
        var buildings = new List<TerrainBuilding>();
        for (var cluster = 0; cluster < clusterCount; cluster++)
        {
            var count = sizes[cluster];
            var columns = (int)MathF.Ceiling(MathF.Sqrt(count)); var rows = (int)MathF.Ceiling(count / (float)columns);
            var half = new Vector2(columns, rows) * pitch / 2;
            var radius = half.Length() + 60;
            Vector2 center = default; var fits = false;
            for (var tries = 0; tries < 200 && !fits; tries++)
            {
                center = place(radius + 120);
                fits = Math.Max(Math.Abs(center.X), Math.Abs(center.Y)) <= CombatRules.ArenaHalfSize - radius - 60
                    && placed.All(other => other.Center.DistanceTo(center) >= other.Radius + radius + 120);
            }
            if (!fits) return null;
            placed.Add((center, radius));
            var rotation = random.NextSingle() * MathF.PI;
            var cos = MathF.Cos(rotation); var sin = MathF.Sin(rotation);
            Vector2 World(Vector2 local) => center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
            // Level the pad to its centre's height, blending back into the surrounding relief.
            var level = relief[Node(center)];
            for (var index = 0; index < relief.Length; index++)
            {
                var offset = Point(index) - center;
                var local = new Vector2(offset.X * cos + offset.Y * sin, -offset.X * sin + offset.Y * cos);
                var outside = new Vector2(Math.Max(0, Math.Abs(local.X) - half.X - 30), Math.Max(0, Math.Abs(local.Y) - half.Y - 30)).Length();
                relief[index] = Mathf.Lerp(level, relief[index], Mathf.SmoothStep(0, 1, Math.Clamp(outside / 150, 0, 1)));
            }
            var lots = Enumerable.Range(0, columns * rows).OrderBy(_ => random.Next()).Take(count).Order();
            var town = count >= 10;
            foreach (var lot in lots)
            {
                var lotCenter = new Vector2((lot % columns + .5f) * pitch - half.X, (lot / columns + .5f) * pitch - half.Y);
                var (style, size, height) = BuildingShape(biome, town, random, between);
                var extent = TerrainBuilding.IsRound(style) ? new Vector2(size.X, size.X) : size;
                // Turning a rectangle a quarter keeps the street grid while varying the layout.
                var quarter = random.Next(2) == 1;
                var footprint = quarter ? new Vector2(extent.Y, extent.X) : extent;
                var slack = new Vector2(BuildingLot / 2, BuildingLot / 2) - footprint;
                var jitter = new Vector2(Math.Max(0, slack.X) * (random.NextSingle() * 2 - 1), Math.Max(0, slack.Y) * (random.NextSingle() * 2 - 1));
                buildings.Add(new(style, cluster, World(lotCenter + jitter), extent, rotation + (quarter ? MathF.PI / 2 : 0), height));
            }
        }
        return buildings;
    }
    private static (BuildingStyle Style, Vector2 HalfSize, float Height) BuildingShape(Biome biome, bool town, Random random,
        Func<float, float, float> between)
    {
        var roll = random.NextSingle();
        return biome switch
        {
            Mech.Biome.Arctic => roll < .6f ? (BuildingStyle.Block, new(between(10, 20), between(8, 14)), between(15, 25))
                : roll < .9f ? (BuildingStyle.Tank, new(between(9, 14), 0), between(15, 24))
                : (BuildingStyle.Mast, new(4, 0), between(45, 60)),
            Mech.Biome.Lunar => roll < .45f ? Dome(between(14, 22)) : Module(),
            Mech.Biome.Highlands => roll < .5f ? House()
                : roll < .85f ? (BuildingStyle.Block, new(between(10, 18), between(8, 14)), between(15, 30))
                : (BuildingStyle.Mast, new(4, 0), between(45, 60)),
            Mech.Biome.Desert => roll < .55f ? (BuildingStyle.Block, new(between(10, 20), between(10, 18)), between(15, 25))
                : roll < .85f ? (BuildingStyle.Tank, new(between(8, 12), 0), between(15, 22)) : Dome(between(12, 18)),
            Mech.Biome.Tropical when town => roll < .5f ? (BuildingStyle.Block, new(between(12, 20), between(12, 20)), between(20, 45)) : House(),
            Mech.Biome.Tropical => roll < .6f ? House() : Barn(),
            Mech.Biome.Grassland => roll < .3f ? House() : roll < .6f ? Barn() : (BuildingStyle.Silo, new(between(7, 9), 0), between(30, 45)),
            Mech.Biome.Archipelago when town => roll < .5f ? (BuildingStyle.Block, new(between(12, 20), between(12, 20)), between(20, 45)) : House(),
            Mech.Biome.Archipelago => roll < .7f ? House() : (BuildingStyle.Tank, new(between(9, 14), 0), between(15, 24)),
            Mech.Biome.Volcanic => roll < .5f ? (BuildingStyle.Block, new(between(12, 22), between(10, 16)), between(18, 30))
                : roll < .8f ? (BuildingStyle.Tank, new(between(9, 14), 0), between(15, 24))
                : (BuildingStyle.Stack, new(between(8, 12), 0), between(45, 60)),
            Mech.Biome.Toxic => roll < .35f ? Dome(between(14, 22)) : roll < .75f ? Module() : (BuildingStyle.Tank, new(between(9, 14), 0), between(15, 24)),
            _ when town => roll < .6f ? (BuildingStyle.Block, new(between(12, 22), between(12, 22)), between(25, 60)) : House(),
            _ => roll < .4f ? House() : roll < .8f ? Barn() : (BuildingStyle.Silo, new(between(7, 9), 0), between(30, 45)),
        };
        static (BuildingStyle, Vector2, float) Dome(float radius) => (BuildingStyle.Dome, new(radius, 0), Math.Max(15, radius));
        (BuildingStyle, Vector2, float) House() => (BuildingStyle.House, new(between(9, 12), between(7, 10)), between(15, 20));
        (BuildingStyle, Vector2, float) Barn() => (BuildingStyle.Barn, new(between(14, 22), between(10, 14)), between(20, 28));
        (BuildingStyle, Vector2, float) Module() => (BuildingStyle.Module, new(between(8, 10), between(15, 22)), 15);
    }

    // Seeded value noise: engine-free, so generation stays reproducible in unit tests.
    private static float Lattice(int seed, int x, int y)
    {
        unchecked
        {
            var hash = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;
            hash ^= hash >> 15; hash *= 0x2C1B3C6Du; hash ^= hash >> 12; hash *= 0x297A2D39u; hash ^= hash >> 15;
            return hash / (float)uint.MaxValue;
        }
    }
    private static float ValueNoise(int seed, Vector2 point)
    {
        var x = MathF.Floor(point.X); var y = MathF.Floor(point.Y); var ix = (int)x; var iy = (int)y;
        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        var u = Fade(point.X - x); var v = Fade(point.Y - y);
        return Mathf.Lerp(Mathf.Lerp(Lattice(seed, ix, iy), Lattice(seed, ix + 1, iy), u),
            Mathf.Lerp(Lattice(seed, ix, iy + 1), Lattice(seed, ix + 1, iy + 1), u), v);
    }
    private static float Fbm(int seed, Vector2 point, int octaves)
    {
        float sum = 0, amplitude = 1, total = 0;
        for (var octave = 0; octave < octaves; octave++)
        {
            sum += ValueNoise(seed + octave * 7919, point) * amplitude; total += amplitude;
            amplitude *= .5f; point = Octave(point);
        }
        return sum / total;
    }
    // Rotating and offsetting each octave hides the lattice's axis-aligned grain.
    private static Vector2 Octave(Vector2 point) => new Vector2(point.X * .8f - point.Y * .6f, point.X * .6f + point.Y * .8f) * 2.03f + new Vector2(17.3f, 41.9f);
    private static float Ridged(int seed, Vector2 point, int octaves)
    {
        float sum = 0, amplitude = 1, total = 0;
        for (var octave = 0; octave < octaves; octave++)
        {
            sum += (1 - Math.Abs(ValueNoise(seed + octave * 7919, point) * 2 - 1)) * amplitude; total += amplitude;
            amplitude *= .5f; point = Octave(point);
        }
        return sum / total;
    }

    private static int Index(int x, int z) => z * GridSize + x;
    private static Vector2 Point(int index) => new Vector2(index % GridSize - HalfCells, index / GridSize - HalfCells) * CellSize;
    private static int Node(Vector2 point) => Index(Math.Clamp((int)MathF.Round(point.X / CellSize + HalfCells), 1, GridSize - 2), Math.Clamp((int)MathF.Round(point.Y / CellSize + HalfCells), 1, GridSize - 2));
    public float HeightAt(Vector2 point)
    {
        if (_flat) return 0;
        var x = Math.Clamp(point.X / CellSize + HalfCells, 0, GridSize - 1); var z = Math.Clamp(point.Y / CellSize + HalfCells, 0, GridSize - 1);
        var ix = Math.Min((int)x, GridSize - 2); var iz = Math.Min((int)z, GridSize - 2); var u = x - ix; var v = z - iz;
        var a = _heights[Index(ix, iz)]; var b = _heights[Index(ix + 1, iz)];
        var c = _heights[Index(ix, iz + 1)]; var d = _heights[Index(ix + 1, iz + 1)];
        return u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
    }
    public float SlopeAt(Vector2 point)
    {
        var dx = HeightAt(point + new Vector2(7.5f, 0)) - HeightAt(point - new Vector2(7.5f, 0));
        var dz = HeightAt(point + new Vector2(0, 7.5f)) - HeightAt(point - new Vector2(0, 7.5f));
        return new Vector2(dx, dz).Length() / 15;
    }
    public bool Contains(TerrainKind kind, Vector2 point) => Patches.Any(patch => patch.Kind == kind && point.DistanceSquaredTo(patch.Center) <= patch.Radius * patch.Radius);
    public bool Passable(Vector2 point)
    {
        if (CombatRules.Clamp(point).DistanceSquaredTo(point) > 1e-8f) return false;
        foreach (var solid in SolidsNear(point))
            if (_solids[solid].Contains(point, CombatRules.UnitRadius + 1.5f)) return false;
        foreach (var barrier in _barriers)
            if (point.DistanceSquaredTo(barrier.Center) < (barrier.Radius + CombatRules.UnitRadius) * (barrier.Radius + CombatRules.UnitRadius)) return false;
        return SlopeAt(point) <= MaxSlope
            && SlopeAt(point + Vector2.Up * CombatRules.UnitRadius) <= MaxSlope
            && SlopeAt(point + Vector2.Down * CombatRules.UnitRadius) <= MaxSlope
            && SlopeAt(point + Vector2.Left * CombatRules.UnitRadius) <= MaxSlope
            && SlopeAt(point + Vector2.Right * CombatRules.UnitRadius) <= MaxSlope;
    }
    /// <summary>The highest solid surface a mech passing over this point must clear: ground, rock or roof.</summary>
    public float ObstacleTop(Vector2 point) => SolidTop(point, CombatRules.UnitRadius);
    /// <summary>The highest ground, rock or roof within this distance of the point.</summary>
    public float SolidTop(Vector2 point, float margin = 0)
    {
        var top = HeightAt(point);
        foreach (var solid in SolidsNear(point))
            if (_solids[solid].Contains(point, margin)) top = Math.Max(top, _solids[solid].Top);
        return top;
    }
    public bool CanTraverse(Vector2 from, Vector2 to)
    {
        if (_flat) return Passable(to);
        var steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) / 7.5f));
        for (var i = 0; i <= steps; i++) if (!Passable(from.Lerp(to, i / (float)steps))) return false;
        return true;
    }
    /// <summary><see cref="CanTraverse"/> for two points already known to be passable: samples only between them.</summary>
    private bool CanTraverseBetweenWalkable(Vector2 from, Vector2 to)
    {
        if (_flat) return true;
        var steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) / 7.5f));
        for (var i = 1; i < steps; i++) if (!Passable(from.Lerp(to, i / (float)steps))) return false;
        return true;
    }
    // Ice is flat and gives no cover: it neither slows nor screens.
    public float SpeedFactor(Vector2 point) => (Contains(TerrainKind.Water, point) ? WaterSpeed : Contains(TerrainKind.Vegetation, point) ? VegetationSpeed : 1) / (1 + SlopeAt(point));
    public int VegetationModifier(Vector2 from, Vector2 to) => Math.Min(MaxVegetationModifier,
        Patches.Count(patch => patch.Kind == TerrainKind.Vegetation && SegmentCircle(from, to, patch.Center, patch.Radius, out _, out _)));

    private static bool SegmentCircle(Vector2 from, Vector2 to, Vector2 center, float radius, out float enter, out float exit)
    {
        var direction = to - from; var relative = from - center; var a = direction.LengthSquared();
        if (a < 1e-8f) { enter = 0; exit = 1; return relative.LengthSquared() <= radius * radius; }
        var b = relative.Dot(direction); var c = relative.LengthSquared() - radius * radius; var discriminant = b * b - a * c;
        if (discriminant < 0) { enter = exit = 0; return false; }
        var root = MathF.Sqrt(discriminant); enter = Math.Max(0, (-b - root) / a); exit = Math.Min(1, (-b + root) / a);
        return enter <= exit;
    }
    private static bool SegmentSolid(Vector2 from, Vector2 to, Solid solid, out float enter, out float exit)
    {
        if (solid.Round) return SegmentCircle(from, to, solid.Center, solid.HalfSize.X, out enter, out exit);
        var start = solid.Local(from); var direction = solid.Local(to) - start;
        enter = 0; exit = 1;
        for (var axis = 0; axis < 2; axis++)
        {
            var origin = axis == 0 ? start.X : start.Y; var step = axis == 0 ? direction.X : direction.Y;
            var half = axis == 0 ? solid.HalfSize.X : solid.HalfSize.Y;
            if (Math.Abs(step) < 1e-8f) { if (Math.Abs(origin) > half) return false; continue; }
            var near = (-half - origin) / step; var far = (half - origin) / step;
            enter = Math.Max(enter, Math.Min(near, far)); exit = Math.Min(exit, Math.Max(near, far));
        }
        return enter <= exit;
    }
    /// <summary>Sight between eyes raised this far above the ground at each end, as a jumping mech's are.</summary>
    public bool ClearSight(Vector2 from, Vector2 to, float fromLift = 0, float toLift = 0)
    {
        if (_flat) return true;
        var startHeight = HeightAt(from) + EyeHeight + fromLift; var endHeight = HeightAt(to) + EyeHeight + toLift;
        foreach (var solid in _solids)
        {
            if (!SegmentSolid(from, to, solid, out var enter, out var exit)) continue;
            var low = Mathf.Lerp(startHeight, endHeight, enter); var high = Mathf.Lerp(startHeight, endHeight, exit);
            if (Math.Min(low, high) <= solid.Top && Math.Max(low, high) >= solid.Bottom) return false;
        }
        var steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) / 6));
        for (var i = 1; i < steps; i++)
        {
            var t = i / (float)steps;
            if (HeightAt(from.Lerp(to, t)) >= Mathf.Lerp(startHeight, endHeight, t)) return false;
        }
        return true;
    }
    /// <summary>Nearest rock or building along a normalized ray.</summary>
    public float? SolidRayDistance(Vector3 origin, Vector3 direction, float maximum)
    {
        float? nearest = null;
        var from = new Vector2(origin.X, origin.Z); var to = from + new Vector2(direction.X, direction.Z) * maximum;
        foreach (var solid in _solids)
        {
            if (!SegmentSolid(from, to, solid, out var enter, out var exit)) continue;
            var low = solid.Bottom; var high = solid.Top;
            if (Math.Abs(direction.Y) < 1e-8f)
            { if (origin.Y < low || origin.Y > high) continue; }
            else
            {
                var bottom = (low - origin.Y) / (direction.Y * maximum); var top = (high - origin.Y) / (direction.Y * maximum);
                enter = Math.Max(enter, Math.Min(bottom, top)); exit = Math.Min(exit, Math.Max(bottom, top));
            }
            if (enter <= exit && (nearest is null || enter * maximum < nearest)) nearest = enter * maximum;
        }
        return nearest;
    }
    /// <summary>Nearest rendered ground, rock or building along a normalized camera ray.</summary>
    public float? RayDistance(Vector3 origin, Vector3 direction, float maximum)
    {
        var rock = SolidRayDistance(origin, direction, maximum);
        var limit = rock ?? maximum;
        var previous = 0f;
        for (var distance = 6f; distance <= limit + 6; distance += 6)
        {
            var sampleDistance = Math.Min(distance, limit);
            var sample = origin + direction * sampleDistance;
            var at = new Vector2(sample.X, sample.Z);
            if (Math.Abs(at.X) <= CombatRules.ArenaHalfSize && Math.Abs(at.Y) <= CombatRules.ArenaHalfSize && sample.Y <= HeightAt(at))
            {
                var low = previous; var high = sampleDistance;
                for (var iteration = 0; iteration < 16; iteration++)
                {
                    var middle = (low + high) * .5f;
                    var point = origin + direction * middle;
                    if (point.Y > HeightAt(new Vector2(point.X, point.Z))) low = middle; else high = middle;
                }
                return (low + high) * .5f;
            }
            previous = sampleDistance;
        }
        return rock;
    }
    private int NearestNode(Vector2 point)
    {
        var node = Node(point);
        if (_walkable[node] && CanTraverse(point, Point(node))) return node;
        return Enumerable.Range(0, _walkable.Length).Where(index => _walkable[index])
            .OrderBy(index => Point(index).DistanceSquaredTo(point)).Where(index => CanTraverse(point, Point(index))).DefaultIfEmpty(-1).First();
    }
    private int Component(Vector2 point) { var node = NearestNode(point); return node < 0 ? 0 : _components[node]; }
    public Vector2 ReachableDestination(Vector2 from, Vector2 requested)
    {
        requested = CombatRules.Clamp(requested);
        if (_flat || CanTraverse(from, requested)) return requested;
        var component = Component(from);
        if (component == 0) return from;
        if (Passable(requested) && Component(requested) == component) return requested;
        return Enumerable.Range(0, _walkable.Length).Where(index => _components[index] == component)
            .OrderBy(index => Point(index).DistanceSquaredTo(requested)).Select(Point).DefaultIfEmpty(from).First();
    }
    public IReadOnlyList<Vector2> FindPath(Vector2 from, Vector2 requested)
    {
        var to = ReachableDestination(from, requested);
        if (CanTraverse(from, to)) return [to];
        var start = NearestNode(from); var end = NearestNode(to);
        if (start < 0 || end < 0 || _components[start] != _components[end]) return [];
        // Each query owns its rented buffers, so parallel searches cannot overwrite one another.
        var costs = ArrayPool<float>.Shared.Rent(_walkable.Length);
        var previous = ArrayPool<int>.Shared.Rent(_walkable.Length);
        try
        {
            costs.AsSpan(0, _walkable.Length).Fill(float.PositiveInfinity);
            previous.AsSpan(0, _walkable.Length).Fill(-1);
            var open = new PriorityQueue<int, (float, int)>();
            costs[start] = 0; open.Enqueue(start, (0, start));
            while (open.TryDequeue(out var current, out _))
            {
                if (current == end) break;
                foreach (var edge in _neighbors[current])
                {
                    var next = edge.Node;
                    var cost = costs[current] + edge.Cost;
                    if (cost >= costs[next]) continue;
                    costs[next] = cost; previous[next] = current;
                    open.Enqueue(next, (cost + Point(next).DistanceTo(to), next));
                }
            }
            if (float.IsPositiveInfinity(costs[end])) return [];
            var route = new List<Vector2> { to };
            for (var node = end; node != start; node = previous[node]) route.Add(Point(node));
            route.Add(Point(start)); route.Reverse();
            var smooth = new List<Vector2>(); var anchor = from;
            for (var i = 0; i < route.Count;)
            {
                var last = i;
                while (last + 1 < route.Count && CanTraverse(anchor, route[last + 1])) last++;
                smooth.Add(route[last]); anchor = route[last]; i = last + 1;
            }
            return smooth;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(costs);
            ArrayPool<int>.Shared.Return(previous);
        }
    }

    public Vector2 FiringPosition(Vector2 from, Vector2 target, float preferred, float maximumRange)
    {
        var component = Component(from);
        // Only cells inside the weapon's range can be candidates. Preserve row-major tie order.
        var minX = Math.Clamp((int)MathF.Ceiling((target.X - maximumRange) / CellSize + HalfCells), 0, GridSize - 1);
        var maxX = Math.Clamp((int)MathF.Floor((target.X + maximumRange) / CellSize + HalfCells), 0, GridSize - 1);
        var minZ = Math.Clamp((int)MathF.Ceiling((target.Y - maximumRange) / CellSize + HalfCells), 0, GridSize - 1);
        var maxZ = Math.Clamp((int)MathF.Floor((target.Y + maximumRange) / CellSize + HalfCells), 0, GridSize - 1);
        var candidates = ArrayPool<FiringCandidate>.Shared.Rent(_walkable.Length);
        try
        {
            var count = 0;
            for (var z = minZ; z <= maxZ; z++) for (var x = minX; x <= maxX; x++)
            {
                var index = Index(x, z);
                if (_components[index] != component) continue;
                var point = Point(index);
                var distance = point.DistanceTo(target);
                if (distance > maximumRange || distance < CombatRules.UnitRadius * 2.5f) continue;
                candidates[count++] = new(index, from.DistanceTo(point) + Math.Abs(distance - preferred) * 1.5f);
            }
            candidates.AsSpan(0, count).Sort();
            for (var i = 0; i < count; i++)
            {
                var point = Point(candidates[i].Node);
                if (ClearSight(point, target)) return point;
            }
            return from;
        }
        finally { ArrayPool<FiringCandidate>.Shared.Return(candidates); }
    }
}
