namespace Mech;

public enum Biome { Valley, Arctic, Lunar, Highlands, Desert, Tropical, Grassland, Archipelago, Volcanic, Toxic }

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
        new(Biome.Highlands, "Highlands", 0, .75, "Thin-aired mountains, passes, scrub and tarns. Cool, but the thin air weakens heat sinks."),
        new(Biome.Desert, "Desert", 20, .85, "Dunes, tall rock outcrops and rare oases. Hot and dry."),
        new(Biome.Tropical, "Tropical", 15, .8, "Dense jungle, rivers and broken hills. Hot and humid."),
        new(Biome.Grassland, "Grassland", 10, 1.1, "Open, windy plains with scattered copses and ponds. Long sight lines."),
        new(Biome.Archipelago, "Archipelago", 5, 1.2, "Islands between deep channels, crossed by shallows. Cool sea winds."),
        new(Biome.Volcanic, "Volcanic", 30, .8, "Volcanic cones, lava flows and basalt. Very hot."),
        new(Biome.Toxic, "Toxic", 15, .6, "Poisonous air over eroded hills, toxic pools and fungal thickets. Corroded sinks shed poorly."),
    ];
    public static BiomeSpec Spec(Biome biome) => All.Single(spec => spec.Biome == biome);
}
