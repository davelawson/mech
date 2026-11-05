namespace Mech;

/// <summary>The powers the mercenary force holds a reputation with. Pirates and local forces have none.</summary>
public enum Faction { Davion, Kurita, Liao, Marik, Steiner, ComStar }

/// <summary>A tracked faction; <see cref="Code"/> is its owner code in Data/system-factions.json.</summary>
public sealed record FactionSpec(Faction Faction, string Code, string Name, string ShortName, bool GreatHouse);

public static class Factions
{
    public static IReadOnlyList<FactionSpec> All { get; } =
    [
        new(Faction.Davion, "FS", "Federated Suns", "Davion", true),
        new(Faction.Kurita, "DC", "Draconis Combine", "Kurita", true),
        new(Faction.Liao, "CC", "Capellan Confederation", "Liao", true),
        new(Faction.Marik, "FWL", "Free Worlds League", "Marik", true),
        new(Faction.Steiner, "LA", "Lyran Commonwealth", "Steiner", true),
        new(Faction.ComStar, "CS", "ComStar", "ComStar", false),
    ];
    public static IReadOnlyList<Faction> GreatHouses { get; } = All.Where(spec => spec.GreatHouse).Select(spec => spec.Faction).ToArray();
    public static FactionSpec Spec(Faction faction) => All.Single(spec => spec.Faction == faction);
    public static string Name(Faction faction) => Spec(faction).Name;
    public static Faction? ForCode(string code) => All.FirstOrDefault(spec => spec.Code == code)?.Faction;
}
