using Godot;

namespace Mech;

public enum TerrainKind { Rock, Vegetation, Water }
public sealed record TerrainPatch(TerrainKind Kind, Vector2 Center, float Radius, float Height = 0);
public sealed record TerrainHill(Vector2 Center, float Radius, float Height);

/// <summary>Immutable terrain shared by simulation, navigation, rendering and picking. X/Y are ground X/Z.</summary>
public sealed class BattlefieldTerrain
{
    public const int GridSize = 97;
    public const float MaxSlope = .65f;
    public const float EyeHeight = 1.5f;
    public const float WaterSpeed = .5f;
    public const float VegetationSpeed = .7f;
    public const int MaxVegetationModifier = 2;
    public int Seed { get; }
    public IReadOnlyList<TerrainPatch> Patches { get; }
    public IReadOnlyList<TerrainHill> Hills { get; }
    private readonly float[] _heights = new float[GridSize * GridSize];
    private readonly bool[] _walkable = new bool[GridSize * GridSize];
    private readonly int[] _components = new int[GridSize * GridSize];
    private readonly List<int>[] _neighbors;
    private readonly bool _flat;
    private readonly TerrainPatch[] _rocks;
    public static BattlefieldTerrain Flat { get; } = new(0, [], []);

    internal BattlefieldTerrain(int seed, IEnumerable<TerrainPatch> patches, IEnumerable<TerrainHill> hills)
    {
        Seed = seed; Patches = Array.AsReadOnly(patches.ToArray()); Hills = Array.AsReadOnly(hills.ToArray());
        _flat = Patches.Count == 0 && Hills.Count == 0;
        _rocks = Patches.Where(patch => patch.Kind == TerrainKind.Rock).ToArray();
        for (var z = 0; z < GridSize; z++) for (var x = 0; x < GridSize; x++)
        {
            var point = new Vector2(x - 48, z - 48);
            var height = Hills.Sum(hill => point.DistanceTo(hill.Center) >= hill.Radius ? 0
                : hill.Height * (1 + MathF.Cos(MathF.PI * point.DistanceTo(hill.Center) / hill.Radius)) * .5f);
            // Water is shallow, with a flat surface and gently blended banks.
            foreach (var water in Patches.Where(patch => patch.Kind == TerrainKind.Water))
                height *= Mathf.SmoothStep(0, 1, Math.Clamp((point.DistanceTo(water.Center) - water.Radius - 1) / 3, 0, 1));
            _heights[Index(x, z)] = height;
        }
        _neighbors = Enumerable.Range(0, _heights.Length).Select(_ => new List<int>(8)).ToArray();
        for (var z = 0; z < GridSize; z++) for (var x = 0; x < GridSize; x++)
            _walkable[Index(x, z)] = Passable(new Vector2(x - 48, z - 48));
        for (var z = 1; z < GridSize - 1; z++) for (var x = 1; x < GridSize - 1; x++)
        {
            var index = Index(x, z);
            if (!_walkable[index]) continue;
            for (var dz = -1; dz <= 1; dz++) for (var dx = -1; dx <= 1; dx++)
            {
                var next = Index(x + dx, z + dz);
                if ((dx != 0 || dz != 0) && _walkable[next] && CanTraverse(Point(index), Point(next)))
                    _neighbors[index].Add(next);
            }
        }
        var component = 0;
        for (var index = 0; index < _heights.Length; index++)
        {
            if (!_walkable[index] || _components[index] != 0) continue;
            var open = new Queue<int>(); open.Enqueue(index); _components[index] = ++component;
            while (open.TryDequeue(out var current)) foreach (var next in _neighbors[current])
                if (_components[next] == 0) { _components[next] = component; open.Enqueue(next); }
        }
    }

    public static BattlefieldTerrain Generate(int seed)
    {
        var random = new Random(seed);
        // Spawn bands and a central rendezvous remain clear for every force size.
        bool Clear(Vector2 point, float radius) => new[] { new Vector2(0, 32), new Vector2(0, -32) }
            .All(spawn => Math.Abs(point.X - spawn.X) > radius + 9 || Math.Abs(point.Y - spawn.Y) > radius + 5)
            && point.Length() > radius + 2;
        Vector2 Place(float radius)
        {
            for (var attempt = 0; attempt < 500; attempt++)
            {
                var point = new Vector2(random.NextSingle() * 76 - 38, random.NextSingle() * 76 - 38);
                if (Clear(point, radius)) return point;
            }
            return new Vector2(30, 0);
        }
        // Retry complete candidates instead of silently accepting disconnected spawn areas.
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var hills = Enumerable.Range(0, 7).Select(_ =>
            {
                var radius = 6 + random.NextSingle() * 5;
                return new TerrainHill(Place(radius + 3), radius, 3 + random.NextSingle() * 4);
            }).ToArray();
            var patches = new List<TerrainPatch>();
            foreach (var kind in Enum.GetValues<TerrainKind>())
            {
                var count = kind == TerrainKind.Rock ? 16 : kind == TerrainKind.Vegetation ? 10 : 4;
                for (var i = 0; i < count; i++)
                {
                    var radius = kind == TerrainKind.Rock ? 1.3f + random.NextSingle() * 1.7f : 3 + random.NextSingle() * 3;
                    Vector2 point;
                    var tries = 0;
                    do { point = Place(radius + 2); tries++; }
                    while (tries < 500 && patches.Any(patch => patch.Center.DistanceTo(point) < patch.Radius + radius + 1));
                    if (patches.Any(patch => patch.Center.DistanceTo(point) < patch.Radius + radius + 1)) continue;
                    patches.Add(new(kind, point, radius, kind == TerrainKind.Rock ? 3 + random.NextSingle() * 3 : 0));
                }
            }
            var terrain = new BattlefieldTerrain(seed, patches, hills);
            var starts = new[] { -6f, -4, -2, 0, 2, 4, 6 }.SelectMany(x => new[] { new Vector2(x, 32), new Vector2(x, -32) }).Append(Vector2.Zero).ToArray();
            var component = terrain.Component(starts[0]);
            if (component != 0 && starts.All(point => terrain.Passable(point) && terrain.Component(point) == component)
                && Enum.GetValues<TerrainKind>().All(kind => patches.Any(patch => patch.Kind == kind))) return terrain;
        }
        throw new InvalidOperationException("Could not generate a connected battlefield.");
    }

    private static int Index(int x, int z) => z * GridSize + x;
    private static Vector2 Point(int index) => new(index % GridSize - 48, index / GridSize - 48);
    private static int Node(Vector2 point) => Index(Math.Clamp((int)MathF.Round(point.X + 48), 1, 95), Math.Clamp((int)MathF.Round(point.Y + 48), 1, 95));
    public float HeightAt(Vector2 point)
    {
        if (_flat) return 0;
        var x = Math.Clamp(point.X + 48, 0, 96); var z = Math.Clamp(point.Y + 48, 0, 96);
        var ix = Math.Min((int)x, 95); var iz = Math.Min((int)z, 95); var u = x - ix; var v = z - iz;
        var a = _heights[Index(ix, iz)]; var b = _heights[Index(ix + 1, iz)];
        var c = _heights[Index(ix, iz + 1)]; var d = _heights[Index(ix + 1, iz + 1)];
        return u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
    }
    public float SlopeAt(Vector2 point)
    {
        var dx = HeightAt(point + new Vector2(.25f, 0)) - HeightAt(point - new Vector2(.25f, 0));
        var dz = HeightAt(point + new Vector2(0, .25f)) - HeightAt(point - new Vector2(0, .25f));
        return new Vector2(dx, dz).Length() * 2;
    }
    public bool Contains(TerrainKind kind, Vector2 point) => Patches.Any(patch => patch.Kind == kind && point.DistanceSquaredTo(patch.Center) <= patch.Radius * patch.Radius);
    public bool Passable(Vector2 point)
    {
        if (CombatRules.Clamp(point).DistanceSquaredTo(point) > 1e-8f) return false;
        foreach (var rock in _rocks)
            if (point.DistanceTo(rock.Center) < rock.Radius + CombatRules.UnitRadius + .1f) return false;
        return SlopeAt(point) <= MaxSlope
            && SlopeAt(point + Vector2.Up * CombatRules.UnitRadius) <= MaxSlope
            && SlopeAt(point + Vector2.Down * CombatRules.UnitRadius) <= MaxSlope
            && SlopeAt(point + Vector2.Left * CombatRules.UnitRadius) <= MaxSlope
            && SlopeAt(point + Vector2.Right * CombatRules.UnitRadius) <= MaxSlope;
    }
    public bool CanTraverse(Vector2 from, Vector2 to)
    {
        if (_flat) return Passable(to);
        var steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) / .25f));
        for (var i = 0; i <= steps; i++) if (!Passable(from.Lerp(to, i / (float)steps))) return false;
        return true;
    }
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
    public bool ClearSight(Vector2 from, Vector2 to)
    {
        if (_flat) return true;
        var startHeight = HeightAt(from) + EyeHeight; var endHeight = HeightAt(to) + EyeHeight;
        foreach (var rock in _rocks)
        {
            if (!SegmentCircle(from, to, rock.Center, rock.Radius, out var enter, out var exit)) continue;
            var low = Mathf.Lerp(startHeight, endHeight, enter); var high = Mathf.Lerp(startHeight, endHeight, exit);
            var ground = HeightAt(rock.Center);
            if (Math.Min(low, high) <= ground + rock.Height && Math.Max(low, high) >= -.5f) return false;
        }
        var steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) / .2f));
        for (var i = 1; i < steps; i++)
        {
            var t = i / (float)steps;
            if (HeightAt(from.Lerp(to, t)) >= Mathf.Lerp(startHeight, endHeight, t)) return false;
        }
        return true;
    }
    public float? RockRayDistance(Vector3 origin, Vector3 direction, float maximum)
    {
        float? nearest = null;
        var from = new Vector2(origin.X, origin.Z); var to = from + new Vector2(direction.X, direction.Z) * maximum;
        foreach (var rock in _rocks)
        {
            if (!SegmentCircle(from, to, rock.Center, rock.Radius, out var enter, out var exit)) continue;
            var low = -.5f; var high = HeightAt(rock.Center) + rock.Height;
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
    /// <summary>Nearest rendered ground or solid rock along a normalized camera ray.</summary>
    public float? RayDistance(Vector3 origin, Vector3 direction, float maximum)
    {
        var rock = RockRayDistance(origin, direction, maximum);
        var limit = rock ?? maximum;
        var previous = 0f;
        for (var distance = .2f; distance <= limit + .2f; distance += .2f)
        {
            var sampleDistance = Math.Min(distance, limit);
            var sample = origin + direction * sampleDistance;
            var at = new Vector2(sample.X, sample.Z);
            if (Math.Abs(at.X) <= 48 && Math.Abs(at.Y) <= 48 && sample.Y <= HeightAt(at))
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
        var open = new PriorityQueue<int, (float, int)>(); var costs = new float[_walkable.Length]; Array.Fill(costs, float.PositiveInfinity);
        var previous = new int[_walkable.Length]; Array.Fill(previous, -1); costs[start] = 0; open.Enqueue(start, (0, start));
        while (open.TryDequeue(out var current, out _))
        {
            if (current == end) break;
            foreach (var next in _neighbors[current])
            {
                var cost = costs[current] + Point(current).DistanceTo(Point(next)) / SpeedFactor(Point(next));
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
    public Vector2 FiringPosition(Vector2 from, Vector2 target, float preferred, float maximumRange)
    {
        var component = Component(from);
        return Enumerable.Range(0, _walkable.Length).Where(index => _components[index] == component)
            .Select(Point).Where(point => point.DistanceTo(target) <= maximumRange && point.DistanceTo(target) >= CombatRules.UnitRadius * 2.5f)
            .OrderBy(point => from.DistanceTo(point) + Math.Abs(point.DistanceTo(target) - preferred) * 1.5f)
            .Where(point => ClearSight(point, target)).DefaultIfEmpty(from).First();
    }
}
