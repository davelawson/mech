using System.Collections.Immutable;

namespace Mech;

public enum SlotKind { Empty, Weapon, Ammo, HeatSink, JumpJet, Engine, Gyro, Actuator, Cockpit }
public enum StorageGroup { Energy, Ballistic, Missile, Ammo, Equipment }

/// <summary>A run of critical slots in one location. A split item has one block in each location it occupies.</summary>
public sealed record CriticalBlock(int Start, int Length, SlotKind Kind, string? ItemId, string Name, int ItemSlots);

/// <summary>Stock BattleTech mass and critical-slot sizes for weapons, which the catalog does not carry.</summary>
public static class EquipmentRules
{
    public static int Slots(WeaponKind kind) => kind switch
    {
        WeaponKind.SmallLaser or WeaponKind.MediumLaser or WeaponKind.Flamer or WeaponKind.MachineGun or WeaponKind.AC2
            or WeaponKind.LRM5 or WeaponKind.SRM2 or WeaponKind.SRM4 => 1,
        WeaponKind.LargeLaser or WeaponKind.ERLargeLaser or WeaponKind.LRM10 or WeaponKind.SRM6 => 2,
        WeaponKind.PPC or WeaponKind.LRM15 => 3,
        WeaponKind.AC5 => 4, WeaponKind.LRM20 => 5, WeaponKind.AC10 => 7, WeaponKind.AC20 => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static float Tons(WeaponKind kind) => kind switch
    {
        WeaponKind.SmallLaser or WeaponKind.MachineGun => .5f,
        WeaponKind.MediumLaser or WeaponKind.Flamer or WeaponKind.SRM2 => 1,
        WeaponKind.LRM5 or WeaponKind.SRM4 => 2, WeaponKind.SRM6 => 3,
        WeaponKind.LargeLaser or WeaponKind.ERLargeLaser or WeaponKind.LRM10 => 5,
        WeaponKind.AC2 => 6, WeaponKind.PPC or WeaponKind.LRM15 => 7, WeaponKind.AC5 => 8,
        WeaponKind.LRM20 => 10, WeaponKind.AC10 => 12, WeaponKind.AC20 => 14,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static StorageGroup Group(WeaponKind kind) => kind switch
    {
        WeaponKind.SmallLaser or WeaponKind.MediumLaser or WeaponKind.LargeLaser or WeaponKind.ERLargeLaser
            or WeaponKind.PPC or WeaponKind.Flamer => StorageGroup.Energy,
        WeaponKind.MachineGun or WeaponKind.AC2 or WeaponKind.AC5 or WeaponKind.AC10 or WeaponKind.AC20 => StorageGroup.Ballistic,
        _ => StorageGroup.Missile
    };
    public static int HeatSinkSlots(HeatSinkKind kind) => kind == HeatSinkKind.Double ? 3 : 1;
    /// <summary>Jump jet mass follows the carrying mech's weight: up to 55 t, 60–85 t, and 90 t or more.</summary>
    public static float JumpJetTons(int mechTonnage) => mechTonnage <= 55 ? .5f : mechTonnage <= 85 ? 1 : 2;
    public static int Capacity(MechLocation location) =>
        location is MechLocation.Head or MechLocation.LeftLeg or MechLocation.RightLeg ? 6 : 12;
    /// <summary>The most armor a location can carry: twice its structure, or three times for the head (9 stock points).</summary>
    public static int ArmorLimit(LocationSpec location) => location.Structure * (location.Location == MechLocation.Head ? 3 : 2);
    /// <summary>Heat sinks the engine holds without using critical slots.</summary>
    public static int EngineHeatSinks(MechSpec spec) => spec.HeatSinks - spec.Components.Sum(item => item.HeatSinkCount);
    public static int JumpJets(MechSpec spec) => spec.Components.Where(item => item.Kind == ComponentKind.JumpJet).Sum(item => item.Slots);
}

/// <summary>Places a variant's equipment into critical slots: fixed systems first, then weapons, heat sinks and jump jets, then ammunition.</summary>
public static class CriticalLayout
{
    private sealed record Item(SlotKind Kind, string Id, string Name, int Slots);

    public static ImmutableDictionary<MechLocation, ImmutableArray<CriticalBlock>> For(MechSpec spec)
    {
        var items = Enum.GetValues<MechLocation>().ToDictionary(location => location, _ => new List<Item>());
        foreach (var c in spec.Components.Where(c => c.Kind is not (ComponentKind.HeatSink or ComponentKind.JumpJet)))
            items[c.Location].Add(new(Kind(c.Kind), c.Id, c.Name, c.Slots));
        foreach (var w in spec.Weapons) items[w.Location].Add(new(SlotKind.Weapon, w.Id, w.Rear ? $"{w.Name} (R)" : w.Name, EquipmentRules.Slots(w.Kind)));
        foreach (var c in spec.Components.Where(c => c.Kind is ComponentKind.HeatSink or ComponentKind.JumpJet))
            items[c.Location].Add(new(Kind(c.Kind), c.Id, c.Name, c.Slots));
        foreach (var a in spec.Ammo) items[a.Location].Add(new(SlotKind.Ammo, a.Id, a.Name, 1));

        // Stock designs may split one large weapon between neighbouring locations (the King Crab's AC/20s, for example).
        var blocks = items.Keys.ToDictionary(location => location, _ => new List<CriticalBlock>());
        var spills = new List<(MechLocation Location, Item Item, int Slots)>();
        foreach (var (location, list) in items)
        {
            var capacity = EquipmentRules.Capacity(location);
            var overflow = list.Sum(item => item.Slots) - capacity;
            Item? split = null;
            if (overflow > 0 && Neighbour(location) is { } next
                && list.Where(item => item.Kind == SlotKind.Weapon && item.Slots > overflow).MaxBy(item => item.Slots) is { } largest)
            { split = largest; list.Remove(largest); list.Add(largest); spills.Add((next, largest, overflow)); }
            var start = 0;
            foreach (var item in list)
            {
                var length = item == split ? item.Slots - overflow : item.Slots;
                blocks[location].Add(new(start, length, item.Kind, item.Id, item.Name, item.Slots)); start += length;
            }
        }
        foreach (var (location, item, slots) in spills)
        {
            var start = blocks[location].Sum(block => block.Length);
            blocks[location].Add(new(start, slots, item.Kind, item.Id, item.Name, item.Slots));
        }
        return blocks.ToImmutableDictionary(pair => pair.Key, pair =>
        {
            var used = pair.Value.Sum(block => block.Length);
            return pair.Value.Concat(Enumerable.Range(used, Math.Max(0, EquipmentRules.Capacity(pair.Key) - used))
                .Select(index => new CriticalBlock(index, 1, SlotKind.Empty, null, "—", 1))).ToImmutableArray();
        });
    }
    public static int Used(ImmutableArray<CriticalBlock> blocks) => blocks.Where(block => block.Kind != SlotKind.Empty).Sum(block => block.Length);
    private static MechLocation? Neighbour(MechLocation location) => location switch
    {
        MechLocation.LeftTorso => MechLocation.LeftArm, MechLocation.RightTorso => MechLocation.RightArm,
        MechLocation.LeftArm => MechLocation.LeftTorso, MechLocation.RightArm => MechLocation.RightTorso,
        MechLocation.LeftLeg => MechLocation.LeftTorso, MechLocation.RightLeg => MechLocation.RightTorso, _ => null
    };
    private static SlotKind Kind(ComponentKind kind) => kind switch
    {
        ComponentKind.FusionEngine => SlotKind.Engine, ComponentKind.Gyro => SlotKind.Gyro,
        ComponentKind.Cockpit or ComponentKind.LifeSupport or ComponentKind.Sensors => SlotKind.Cockpit,
        ComponentKind.HeatSink => SlotKind.HeatSink, ComponentKind.JumpJet => SlotKind.JumpJet,
        _ => SlotKind.Actuator
    };
}

/// <summary>An owned mech's installed equipment: its stock design less the items removed in the hangar.</summary>
public static class Loadout
{
    public static bool Removable(SlotKind kind) => kind is SlotKind.Weapon or SlotKind.Ammo or SlotKind.HeatSink or SlotKind.JumpJet;
    /// <summary>Whether a stock item of this design is a weapon, bin, heat sink or jump jet that the hangar may remove.</summary>
    public static bool Removable(MechSpec stock, string id) => stock.Weapons.Any(item => item.Id == id) || stock.Ammo.Any(item => item.Id == id)
        || stock.Components.Any(item => item.Id == id && item.Kind is ComponentKind.HeatSink or ComponentKind.JumpJet);

    /// <summary>The design with these stock items removed: one heat sink fewer per removed sink and one jump MP fewer per removed jet.</summary>
    public static MechSpec Strip(MechSpec stock, IReadOnlySet<string> removed)
    {
        if (removed.Count == 0) return stock;
        var components = stock.Components.Where(item => removed.Contains(item.Id)).ToArray();
        return stock with
        {
            Weapons = stock.Weapons.Where(item => !removed.Contains(item.Id)).ToImmutableArray(),
            Components = stock.Components.Where(item => !removed.Contains(item.Id)).ToImmutableArray(),
            Ammo = stock.Ammo.Where(item => !removed.Contains(item.Id)).ToImmutableArray(),
            HeatSinks = stock.HeatSinks - components.Sum(item => item.HeatSinkCount),
            Jump = Math.Max(0, stock.Jump - components.Where(item => item.Kind == ComponentKind.JumpJet).Sum(item => item.Slots))
        };
    }

    /// <summary>Mass of one installed weapon, bin, heat sink or jump jet, or null for fixed systems.</summary>
    public static float? Tons(MechSpec spec, string id) =>
        spec.Weapons.FirstOrDefault(item => item.Id == id) is { } weapon ? EquipmentRules.Tons(weapon.Kind)
        : spec.Ammo.Any(item => item.Id == id) ? 1
        : spec.Components.FirstOrDefault(item => item.Id == id) is { } component ? component.Kind switch
        {
            ComponentKind.HeatSink => component.HeatSinkCount,
            ComponentKind.JumpJet => component.Slots * EquipmentRules.JumpJetTons(spec.Tonnage),
            _ => null
        } : null;

    /// <summary>The storage line a removed item joins.</summary>
    public static string StorageId(MechSpec spec, string id) =>
        spec.Weapons.FirstOrDefault(item => item.Id == id) is { } weapon ? StorageCatalog.WeaponId(weapon.Kind)
        : spec.Ammo.FirstOrDefault(item => item.Id == id) is { } bin ? StorageCatalog.AmmoId(bin.AmmoKind)
        : spec.Components.FirstOrDefault(item => item.Id == id) is { Kind: ComponentKind.HeatSink } ? StorageCatalog.HeatSinkId(spec.HeatSinkType)
        : spec.Components.FirstOrDefault(item => item.Id == id) is { Kind: ComponentKind.JumpJet } ? StorageCatalog.JumpJetId(spec.Tonnage)
        : throw new ArgumentException($"{id} cannot be stored.");

    /// <summary>Weapons with no bin of their ammunition anywhere on the mech, and bins no weapon fires, regardless of location or damage.</summary>
    public static ImmutableHashSet<string> Suspect(MechSpec spec) =>
        spec.Weapons.Where(item => item.AmmoKind is { } kind && spec.Ammo.All(bin => bin.AmmoKind != kind)).Select(item => item.Id)
            .Concat(spec.Ammo.Where(bin => spec.Weapons.All(item => item.AmmoKind != bin.AmmoKind)).Select(bin => bin.Id))
            .ToImmutableHashSet(StringComparer.Ordinal);
}

public sealed record StorageItem(string Id, string Name, StorageGroup Group, float Tons, int Slots, int Heat);
public sealed record StorageEntry(string Id, int Count);

/// <summary>Spare weapons, ammunition, heat sinks and jump jets held by the company. Armor and fixed systems are never stored.</summary>
public static class StorageCatalog
{
    public static string WeaponId(WeaponKind kind) => $"weapon:{kind}";
    public static string AmmoId(AmmoKind kind) => $"ammo:{kind}";
    public static string HeatSinkId(HeatSinkKind kind) => $"heat-sink:{kind}";
    public static string JumpJetId(int mechTonnage) => $"jump-jet:{EquipmentRules.JumpJetTons(mechTonnage).ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static StorageItem? Describe(string id)
    {
        var parts = id.Split(':');
        if (parts.Length != 2) return null;
        switch (parts[0])
        {
            case "weapon" when Enum.TryParse<WeaponKind>(parts[1], out var kind) && Enum.IsDefined(kind) && parts[1] == kind.ToString():
                var weapon = DraftCatalog.AllMechs.SelectMany(mech => mech.Weapons).FirstOrDefault(item => item.Kind == kind);
                return weapon is null ? null : new(id, weapon.Name, EquipmentRules.Group(kind), EquipmentRules.Tons(kind), EquipmentRules.Slots(kind), weapon.Heat);
            case "ammo" when Enum.TryParse<AmmoKind>(parts[1], out var ammo) && Enum.IsDefined(ammo) && parts[1] == ammo.ToString():
                var bin = DraftCatalog.AllMechs.SelectMany(mech => mech.Ammo).FirstOrDefault(item => item.AmmoKind == ammo);
                return bin is null ? null : new(id, $"{bin.Name} ({bin.Shots})", StorageGroup.Ammo, 1, 1, 0);
            case "heat-sink" when Enum.TryParse<HeatSinkKind>(parts[1], out var sink) && Enum.IsDefined(sink) && parts[1] == sink.ToString():
                return new(id, sink == HeatSinkKind.Double ? "Double Heat Sink" : "Heat Sink", StorageGroup.Equipment, 1, EquipmentRules.HeatSinkSlots(sink), 0);
            case "jump-jet" when parts[1] is "0.5" or "1" or "2":
                var tons = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                var range = parts[1] switch { "0.5" => "≤55 t", "1" => "60–85 t", _ => "90+ t" };
                return new(id, $"Jump Jet ({range})", StorageGroup.Equipment, tons, 1, 0);
            default: return null;
        }
    }

    /// <summary>One spare of every weapon, ammunition bin, heat sink (engine-held included) and jump jet mounted on these mechs.</summary>
    public static ImmutableArray<StorageEntry> Stock(IEnumerable<MechSpec> mechs)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        void Add(string id, int count) { if (count > 0) counts[id] = counts.GetValueOrDefault(id) + count; }
        foreach (var mech in mechs)
        {
            foreach (var weapon in mech.Weapons) Add(WeaponId(weapon.Kind), 1);
            foreach (var bin in mech.Ammo) Add(AmmoId(bin.AmmoKind), 1);
            Add(HeatSinkId(mech.HeatSinkType), mech.HeatSinks);
            Add(JumpJetId(mech.Tonnage), EquipmentRules.JumpJets(mech));
        }
        return counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new StorageEntry(pair.Key, pair.Value)).ToImmutableArray();
    }
}
