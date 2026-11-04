namespace Mech;

public enum Biome { Valley, Arctic, Lunar }

/// <summary>A battlefield's climate. Numbers are recorded in docs/combat-tuning.md.</summary>
public sealed record BiomeSpec(Biome Biome, string Name, double Ambient, double CoolingEfficiency, string Description)
{
    public string EfficiencyText => FormattableString.Invariant($"{CoolingEfficiency * 100:0}%");
    public string ClimateText => FormattableString.Invariant($"Ambient {Ambient:0} · Cooling efficiency {EfficiencyText}");
}

public static class Biomes
{
    public static IReadOnlyList<BiomeSpec> All { get; } =
    [
        new(Biome.Valley, "Valley", 10, 1, "Temperate hills, forests, streams and farmland."),
        new(Biome.Arctic, "Arctic", -20, 1.5, "Cold, windy ridges, sparse conifers and frozen lakes."),
        new(Biome.Lunar, "Lunar", -50, .25, "Airless craters and boulder fields. Cold, but heat sinks shed little."),
    ];
    public static BiomeSpec Spec(Biome biome) => All.Single(spec => spec.Biome == biome);
}
