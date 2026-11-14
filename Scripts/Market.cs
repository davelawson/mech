namespace Mech;

/// <summary>How advanced the equipment a system's market can sell is, lowest first.</summary>
public enum TechLevel { Primitive, Regressed, Standard, Advanced, StarLeague }

/// <summary>How much a system's market holds, smallest first.</summary>
public enum IndustrialCapacity { Backwater, Developing, Industrial, MajorIndustrial, FactoryWorld }

/// <summary>Each system's ratings come from its USILR socio-industrial code (technology, industrial development, raw materials,
/// industrial output, agriculture, graded A–F, X for uninhabited worlds): Technological Level from the technology grade and
/// Industrial Capacity from the output grade. A few named worlds are raised above their grades.</summary>
public static class SystemRatings
{
    /// <summary>Former Star League worlds: one technology level above their grade.</summary>
    public static readonly IReadOnlySet<string> StarLeagueWorlds = new HashSet<string>(StringComparer.Ordinal)
        { "Terra", "Tharkad", "New Avalon", "Luthien", "Sian", "Atreus" };
    /// <summary>BattleMech factory worlds: Factory World capacity whatever their output grade.</summary>
    public static readonly IReadOnlySet<string> FactoryWorlds = new HashSet<string>(StringComparer.Ordinal)
        { "Hesperus II", "Coventry", "New Avalon", "Luthien", "Alshain", "Gibson", "Irian", "Kathil", "Galax" };

    private static char? Grade(StarSystem system, int index) =>
        system.Climate?.SocioIndustrial is { Length: 9 } code && code[index] is >= 'A' and <= 'F' ? code[index] : null;

    /// <summary>The technology level, or null for a world with no market (uninhabited, or no socio-industrial code).</summary>
    public static TechLevel? Tech(StarSystem system)
    {
        if (Grade(system, 0) is not { } grade || Grade(system, 6) is null) return null;
        var level = grade switch { 'A' => TechLevel.StarLeague, 'B' => TechLevel.Advanced, 'C' => TechLevel.Standard, 'D' => TechLevel.Regressed, _ => TechLevel.Primitive };
        return StarLeagueWorlds.Contains(system.Name) && level < TechLevel.StarLeague ? level + 1 : level;
    }

    /// <summary>The industrial capacity, or null for a world with no market.</summary>
    public static IndustrialCapacity? Capacity(StarSystem system)
    {
        if (Grade(system, 6) is not { } grade || Grade(system, 0) is null) return null;
        if (FactoryWorlds.Contains(system.Name)) return IndustrialCapacity.FactoryWorld;
        return grade switch { 'A' or 'B' => IndustrialCapacity.MajorIndustrial, 'C' => IndustrialCapacity.Industrial, 'D' => IndustrialCapacity.Developing, _ => IndustrialCapacity.Backwater };
    }

    public static bool HasMarket(StarSystem system) => Tech(system) is not null;

    public static string Name(TechLevel level) => level switch { TechLevel.StarLeague => "Star League", _ => level.ToString() };
    public static string Name(IndustrialCapacity capacity) => capacity switch
    { IndustrialCapacity.MajorIndustrial => "Major Industrial", IndustrialCapacity.FactoryWorld => "Factory World", _ => capacity.ToString() };

    /// <summary>"Standard tech · Industrial", or "No market" for an uninhabited world.</summary>
    public static string Text(StarSystem system) => Tech(system) is { } tech && Capacity(system) is { } capacity
        ? $"{Name(tech)} tech · {Name(capacity)}" : "No market";
}

/// <summary>What markets stock, how often, and what it costs.</summary>
public static class MarketTuning
{
    /// <summary>Items a full market holds at each capacity.</summary>
    public static int Size(IndustrialCapacity capacity) => capacity switch
    {
        IndustrialCapacity.Backwater => 20, IndustrialCapacity.Developing => 50, IndustrialCapacity.Industrial => 100,
        IndustrialCapacity.MajorIndustrial => 180, IndustrialCapacity.FactoryWorld => 300, _ => throw new ArgumentOutOfRangeException(nameof(capacity))
    };
    /// <summary>Share of a market's stock that stays from one day to the next.</summary>
    public const double DailyRetention = .95;
    public const int Common = 10, Uncommon = 5, Rare = 2;
    /// <summary>Ammunition is bought most, so a bin draws at twice its weapon's weight.</summary>
    public const int AmmoWeightFactor = 2;

    private static readonly Dictionary<WeaponKind, (TechLevel Tech, int Weight, long Price)> Weapons = new()
    {
        [WeaponKind.MachineGun] = (TechLevel.Primitive, Common, 5_000), [WeaponKind.Flamer] = (TechLevel.Primitive, Uncommon, 7_500),
        [WeaponKind.AC2] = (TechLevel.Primitive, Uncommon, 75_000), [WeaponKind.AC5] = (TechLevel.Primitive, Uncommon, 125_000),
        [WeaponKind.SRM2] = (TechLevel.Primitive, Common, 10_000),
        [WeaponKind.SmallLaser] = (TechLevel.Regressed, Common, 11_250), [WeaponKind.MediumLaser] = (TechLevel.Regressed, Common, 40_000),
        [WeaponKind.SRM4] = (TechLevel.Regressed, Common, 60_000), [WeaponKind.LRM5] = (TechLevel.Regressed, Uncommon, 30_000),
        [WeaponKind.LRM10] = (TechLevel.Regressed, Uncommon, 100_000), [WeaponKind.AC10] = (TechLevel.Regressed, Uncommon, 200_000),
        [WeaponKind.LargeLaser] = (TechLevel.Standard, Uncommon, 100_000), [WeaponKind.SRM6] = (TechLevel.Standard, Uncommon, 80_000),
        [WeaponKind.LRM15] = (TechLevel.Standard, Uncommon, 175_000), [WeaponKind.PPC] = (TechLevel.Standard, Rare, 200_000),
        [WeaponKind.AC20] = (TechLevel.Advanced, Rare, 300_000), [WeaponKind.LRM20] = (TechLevel.Advanced, Rare, 250_000),
        [WeaponKind.ERLargeLaser] = (TechLevel.StarLeague, Rare, 200_000),
    };
    private static readonly Dictionary<AmmoKind, long> AmmoPrices = new()
    {
        [AmmoKind.MachineGun] = 1_000, [AmmoKind.AC2] = 1_000, [AmmoKind.AC5] = 4_500, [AmmoKind.AC10] = 6_000, [AmmoKind.AC20] = 10_000,
        [AmmoKind.LRM5] = 30_000, [AmmoKind.LRM10] = 30_000, [AmmoKind.LRM15] = 30_000, [AmmoKind.LRM20] = 30_000,
        [AmmoKind.SRM2] = 27_000, [AmmoKind.SRM4] = 27_000, [AmmoKind.SRM6] = 27_000,
    };
    private static WeaponKind Weapon(AmmoKind ammo) => Enum.Parse<WeaponKind>(ammo.ToString());

    /// <summary>The lowest technology level that sells an item, its draw weight and its base price; null for an unknown item.</summary>
    public static (TechLevel Tech, int Weight, long Price)? Spec(string storageId)
    {
        var parts = storageId.Split(':');
        if (parts.Length != 2 || StorageCatalog.Describe(storageId) is null) return null;
        switch (parts[0])
        {
            case "weapon": return Weapons[Enum.Parse<WeaponKind>(parts[1])];
            case "ammo":
                var ammo = Enum.Parse<AmmoKind>(parts[1]); var weapon = Weapons[Weapon(ammo)];
                return (weapon.Tech, weapon.Weight * AmmoWeightFactor, AmmoPrices[ammo]);
            case "heat-sink":
                return Enum.Parse<HeatSinkKind>(parts[1]) == HeatSinkKind.Double ? (TechLevel.StarLeague, Rare, 6_000) : (TechLevel.Primitive, Common, 2_000);
            case "jump-jet":
                return (TechLevel.Regressed, Uncommon, parts[1] switch { "0.5" => 10_000, "1" => 20_000, _ => 40_000 });
            default: return null;
        }
    }

    /// <summary>Items a market of this technology level can stock, with their draw weights.</summary>
    public static IReadOnlyList<(string Id, int Weight)> Stock(TechLevel tech) =>
        [.. StorageCatalog.All.Select(item => (item.Id, Spec: Spec(item.Id)!.Value)).Where(item => item.Spec.Tech <= tech).Select(item => (item.Id, item.Spec.Weight))];
}

public static class EquipmentMarket
{
    /// <summary>An item's base price, the same in every market.</summary>
    public static long Price(string storageId) => MarketTuning.Spec(storageId)?.Price ?? throw new ArgumentException($"{storageId} is not sold.");
}

/// <summary>A saved market: what it held and the day it was last brought up to date.</summary>
public sealed record MarketSnapshot(string SystemId, DateOnly Refreshed, StorageEntry[] Items);

/// <summary>One system's market. Its stock is generated when first viewed; each later view rolls over the days since: a share
/// 1 − 0.95^days of its full size is replaced, filling the gaps purchases left before swapping out random items still held.</summary>
public sealed class SystemMarket
{
    public string SystemId { get; }
    public TechLevel Tech { get; }
    public IndustrialCapacity Capacity { get; }
    public int Size => MarketTuning.Size(Capacity);
    public DateOnly Refreshed { get; private set; }
    // One entry per item held.
    private readonly List<string> _items;
    public int Count => _items.Count;

    private SystemMarket(StarSystem system, DateOnly refreshed, IEnumerable<string> items)
    {
        SystemId = system.Id; Tech = SystemRatings.Tech(system) ?? throw new ArgumentException($"{system.Name} has no market.");
        Capacity = SystemRatings.Capacity(system)!.Value; Refreshed = refreshed; _items = [.. items];
    }

    /// <summary>A newly stocked market, full to its size.</summary>
    internal static SystemMarket Generate(StarSystem system, DateOnly date, Random random)
    {
        var market = new SystemMarket(system, date, []);
        for (var i = 0; i < market.Size; i++) market._items.Add(market.Draw(random));
        return market;
    }

    /// <summary>Items replaced after this many days: the expected share 1 − 0.95^days of the full size, rounded.</summary>
    public static int Rollover(int size, int days) =>
        days <= 0 ? 0 : (int)Math.Round(size * (1 - Math.Pow(MarketTuning.DailyRetention, days)), MidpointRounding.AwayFromZero);

    /// <summary>Brings the market up to a date: refills gaps first, then swaps out random items, and remembers the date.</summary>
    internal void Refresh(DateOnly date, Random random)
    {
        var days = date.DayNumber - Refreshed.DayNumber;
        if (days <= 0) return;
        var replace = Math.Min(Size, Rollover(Size, days));
        var fill = Math.Min(replace, Size - _items.Count);
        for (var i = 0; i < fill; i++) _items.Add(Draw(random));
        for (var i = fill; i < replace; i++) { _items.RemoveAt(random.Next(_items.Count)); _items.Add(Draw(random)); }
        Refreshed = date;
    }

    private string Draw(Random random)
    {
        var stock = MarketTuning.Stock(Tech); var roll = random.Next(stock.Sum(item => item.Weight));
        foreach (var (id, weight) in stock) { if (roll < weight) return id; roll -= weight; }
        return stock[^1].Id;
    }

    /// <summary>How many of an item the market holds.</summary>
    public int Stock(string storageId) => _items.Count(item => item == storageId);
    /// <summary>The market's stock, by item.</summary>
    public IReadOnlyList<StorageEntry> Items => [.. _items.GroupBy(item => item, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new StorageEntry(group.Key, group.Count()))];

    /// <summary>"Only 2 Medium Laser left in the market" when the market lacks some of these items, or null when it holds them all.</summary>
    public string? Shortage(IEnumerable<string> storageIds) =>
        storageIds.GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(group => Stock(group.Key) < group.Count()) is { } short_
            ? $"Only {Stock(short_.Key)} {StorageCatalog.Describe(short_.Key)?.Name ?? short_.Key} left in the market" : null;

    /// <summary>Sells items; refuses, changing nothing, unless the market holds them all.</summary>
    internal void Sell(IReadOnlyCollection<string> storageIds)
    {
        if (Shortage(storageIds) is { } problem) throw new InvalidOperationException(problem + ".");
        foreach (var id in storageIds) _items.Remove(id);
    }

    internal MarketSnapshot Snapshot() => new(SystemId, Refreshed, [.. Items]);

    internal static SystemMarket Restore(MarketSnapshot save, StarChart chart, DateOnly date)
    {
        if (save is null || save.SystemId is null || !chart.Contains(save.SystemId) || !SystemRatings.HasMarket(chart[save.SystemId])
            || save.Refreshed > date || save.Items is null)
            throw new ArgumentException("Invalid market.");
        var market = new SystemMarket(chart[save.SystemId], save.Refreshed, []);
        foreach (var entry in save.Items)
        {
            if (entry is null || entry.Count <= 0 || MarketTuning.Spec(entry.Id) is not { } spec || spec.Tech > market.Tech)
                throw new ArgumentException("Invalid market.");
            for (var i = 0; i < entry.Count; i++) market._items.Add(entry.Id);
        }
        if (market._items.Count > market.Size || save.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != save.Items.Length)
            throw new ArgumentException("Invalid market.");
        return market;
    }
}
