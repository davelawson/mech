namespace Mech;

public enum Biome { Valley, Arctic, Lunar, Highlands, Desert, Tropical, Grassland, Archipelago, Volcanic, Toxic }

/// <summary>A star system's primary-world climate in 3025, imported into Data/system-climate.json.</summary>
public sealed record SystemClimate(string Pressure, string Atmosphere, int Temperature, int Water, long Population, string SocioIndustrial);

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

    /// <summary>Systems at least this populous are urban: denser settlements in their climate's biome.</summary>
    public const long UrbanPopulation = 4_000_000_000;

    /// <summary>A system's biome from its primary world's climate; the first matching rule wins.
    /// Temperatures are equatorial °C, so the thresholds sit relative to Terra's 30 °C. Recorded in docs/combat-tuning.md.</summary>
    public static Biome ForClimate(SystemClimate climate)
    {
        var (temperature, water) = (climate.Temperature, climate.Water);
        if (climate.Pressure is "VACUUM" or "TRACE") return Biome.Lunar;
        if (climate.Atmosphere.StartsWith("TOXIC", StringComparison.Ordinal)) return Biome.Toxic;
        if (temperature > 50) return Biome.Volcanic;
        if (temperature <= 24) return Biome.Arctic;
        if (water >= 85) return Biome.Archipelago;
        if (temperature > 40 || temperature > 30 && water < 30) return water < 50 ? Biome.Desert : Biome.Tropical;
        if (temperature > 30 && water >= 50) return Biome.Tropical;
        if (climate.Pressure == "THIN") return Biome.Highlands;
        if (water < 40) return Biome.Grassland;
        return Biome.Valley;
    }
}
