using System.Text.Json;

namespace Mech;

public sealed record StarSystem(string Id, string Name, double X, double Y, double[] LabelBounds, SystemClimate? Climate = null)
{
    /// <summary>The system's biome; null only for hand-built charts without climate.</summary>
    public Biome? Biome => Climate is null ? null : Biomes.ForClimate(Climate);
    public bool Urban => Climate?.Population >= Biomes.UrbanPopulation;
    /// <summary>"Volcanic", "Valley · Urban", or empty without climate.</summary>
    public string BiomeText => Biome is { } biome ? Biomes.Spec(biome).Name + (Urban ? " · Urban" : "") : "";
}
internal sealed record SystemClimateEntry(string Id, string Pressure, string Atmosphere, int Temperature, int Water, long Population, string SocioIndustrial);
internal sealed record SystemClimateData(SystemClimateEntry[] Systems);
public sealed record StarCatalogData(double PixelsPerLightYear, StarSystem[] Systems);

/// <summary>Geography extracted from the pinned map. Coordinates use the 7000 × 6000 image.</summary>
public sealed class StarChart
{
    public static StarChart Instance { get; } = Load();
    public const double MaximumJump = 30;
    private readonly Dictionary<string, StarSystem> _byId;
    private readonly Dictionary<string, string[]> _neighbors;
    public IReadOnlyList<StarSystem> Systems { get; }
    public double PixelsPerLightYear { get; }
    public string StartingSystemId => Systems.Single(system => system.Name == "Galatea").Id;

    public StarChart(IEnumerable<StarSystem> systems, double pixelsPerLightYear)
    {
        Systems = Array.AsReadOnly(systems.ToArray());
        PixelsPerLightYear = pixelsPerLightYear;
        _byId = Systems.ToDictionary(system => system.Id, StringComparer.Ordinal);
        _neighbors = Systems.ToDictionary(system => system.Id, system => Systems
            .Where(other => other.Id != system.Id && Distance(system.Id, other.Id) <= MaximumJump + 1e-8)
            .OrderBy(other => other.Id, StringComparer.Ordinal).Select(other => other.Id).ToArray());
    }

    public StarSystem this[string id] => _byId.TryGetValue(id, out var system)
        ? system : throw new ArgumentException("Unknown star system.", nameof(id));
    public bool Contains(string id) => _byId.ContainsKey(id);
    public double Distance(string from, string to)
    {
        var a = this[from]; var b = this[to];
        return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)) / PixelsPerLightYear;
    }
    public int JumpDays(string from, string to) => Math.Max(1, (int)Math.Ceiling(Distance(from, to) / 6));
    public int RouteDays(IReadOnlyList<string> route) => Enumerable.Range(1, route.Count - 1)
        .Sum(i => JumpDays(route[i - 1], route[i]));

    /// <summary>The origin and every system reachable from it in at most the given number of jumps, ordered by ID.</summary>
    public IReadOnlyList<string> WithinJumps(string origin, int jumps)
    {
        _ = this[origin];
        var reached = new HashSet<string>(StringComparer.Ordinal) { origin };
        var frontier = new List<string> { origin };
        for (var jump = 0; jump < jumps && frontier.Count > 0; jump++)
            frontier = frontier.SelectMany(id => _neighbors[id]).Where(reached.Add).ToList();
        return reached.Order(StringComparer.Ordinal).ToArray();
    }

    // Lexicographic Dijkstra: fewest jumps first, shortest total distance second.
    public string[]? Plot(string origin, string destination)
    {
        _ = this[origin]; _ = this[destination];
        if (origin == destination) return null;
        var best = new Dictionary<string, (int Jumps, double Distance)> { [origin] = (0, 0) };
        var previous = new Dictionary<string, string>();
        var queue = new PriorityQueue<string, (int Jumps, double Distance, string Id)>();
        queue.Enqueue(origin, (0, 0, origin));
        while (queue.TryDequeue(out var current, out var score))
        {
            var known = best[current];
            if (known.Jumps != score.Jumps || known.Distance != score.Distance) continue;
            if (current == destination)
            {
                var route = new List<string> { current };
                while (previous.TryGetValue(current, out var parent)) { route.Add(parent); current = parent; }
                route.Reverse(); return route.ToArray();
            }
            foreach (var next in _neighbors[current])
            {
                var candidate = (Jumps: known.Jumps + 1, Distance: known.Distance + Distance(current, next));
                if (best.TryGetValue(next, out var old) && (old.Jumps < candidate.Jumps
                    || old.Jumps == candidate.Jumps && old.Distance <= candidate.Distance)) continue;
                best[next] = candidate; previous[next] = current;
                queue.Enqueue(next, (candidate.Jumps, candidate.Distance, next));
            }
        }
        return null;
    }

    public bool ValidRoute(string[] route) => route.Length >= 2 && route.Length <= Systems.Count
        && route.All(id => id is not null && Contains(id)) && route.Distinct().Count() == route.Length
        && Enumerable.Range(1, route.Length - 1).All(i => Distance(route[i - 1], route[i]) <= MaximumJump + 1e-8);

    private static StarChart Load()
    {
        using var stream = typeof(StarChart).Assembly.GetManifestResourceStream("Mech.Data.star-systems.json")
            ?? throw new InvalidOperationException("The star system catalog is missing.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<StarCatalogData>(stream, options)
            ?? throw new InvalidOperationException("The star system catalog is empty.");
        using var climateStream = typeof(StarChart).Assembly.GetManifestResourceStream("Mech.Data.system-climate.json")
            ?? throw new InvalidOperationException("The star system climate data is missing.");
        var climates = (JsonSerializer.Deserialize<SystemClimateData>(climateStream, options)
            ?? throw new InvalidOperationException("The star system climate data is empty.")).Systems
            .ToDictionary(entry => entry.Id, entry => new SystemClimate(entry.Pressure, entry.Atmosphere, entry.Temperature, entry.Water,
                entry.Population, entry.SocioIndustrial), StringComparer.Ordinal);
        return new StarChart(data.Systems.Select(system => system with { Climate = climates.TryGetValue(system.Id, out var climate) ? climate
            : throw new InvalidOperationException($"Star system {system.Id} has no climate.") }), data.PixelsPerLightYear);
    }
}
