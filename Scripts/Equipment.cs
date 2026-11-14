using System.Collections.Immutable;

namespace Mech;

public enum SlotKind { Empty, Weapon, Ammo, HeatSink, JumpJet, Engine, Gyro, Actuator, Cockpit }
public enum StorageGroup { Energy, Ballistic, Missile, Ammo, Equipment }

/// <summary>A run of critical slots in one location. A split item has one block in each location it occupies.</summary>
public sealed record CriticalBlock(int Start, int Length, SlotKind Kind, string? ItemId, string Name, int ItemSlots);

/// <summary>Equipment mass and critical-slot rules; weapon sizes come from <see cref="WeaponDefinitions"/>.</summary>
public static class EquipmentRules
{
    public static int Slots(WeaponKind kind) => WeaponDefinitions.Of(kind).Slots;
    public static float Tons(WeaponKind kind) => WeaponDefinitions.Of(kind).Tons;
    public static StorageGroup Group(WeaponKind kind) => WeaponDefinitions.Of(kind).Group;
    public static int HeatSinkSlots(HeatSinkKind kind) => kind == HeatSinkKind.Double ? 3 : 1;
    /// <summary>Jump jet mass follows the carrying mech's weight: up to 55 t, 60–85 t, and 90 t or more.</summary>
    public static float JumpJetTons(int mechTonnage) => mechTonnage <= 55 ? .5f : mechTonnage <= 85 ? 1 : 2;
    public static int Capacity(MechLocation location) =>
        location is MechLocation.Head or MechLocation.LeftLeg or MechLocation.RightLeg ? 6 : 12;
    /// <summary>The most armor a location can carry: twice its structure, or three times for the head (9 stock points).</summary>
    public static int ArmorLimit(LocationSpec location) => location.Structure * (location.Location == MechLocation.Head ? 3 : 2);
    /// <summary>Stock armor points per ton of standard armor.</summary>
    public const int ArmorPointsPerTon = 16;
    public static bool HasRearArmor(MechLocation location) => location is MechLocation.LeftTorso or MechLocation.CenterTorso or MechLocation.RightTorso;
    /// <summary>Heat sinks the engine holds without using critical slots.</summary>
    public static int EngineHeatSinks(MechSpec spec) => spec.HeatSinks - spec.Components.Sum(item => item.HeatSinkCount);
    public static int JumpJets(MechSpec spec) => spec.Components.Where(item => item.Kind == ComponentKind.JumpJet).Sum(item => item.Slots);
}

/// <summary>Places a variant's equipment into critical slots: fixed systems first, then weapons, heat sinks and jump jets, then ammunition,
/// then items installed in the hangar in installation order. Empty slots are always last.</summary>
public static class CriticalLayout
{
    private sealed record Item(SlotKind Kind, string Id, string Name, int Slots);

    public static ImmutableDictionary<MechLocation, ImmutableArray<CriticalBlock>> For(MechSpec spec)
    {
        var items = Enum.GetValues<MechLocation>().ToDictionary(location => location, _ => new List<Item>());
        static bool Stock(string id) => !Loadout.Installed(id);
        foreach (var c in spec.Components.Where(c => c.Kind is not (ComponentKind.HeatSink or ComponentKind.JumpJet)))
            items[c.Location].Add(new(Kind(c.Kind), c.Id, c.Name, c.Slots));
        Item Weapon(WeaponSpec w) => new(SlotKind.Weapon, w.Id, w.Rear ? $"{w.Name} (R)" : w.Name, EquipmentRules.Slots(w.Kind));
        foreach (var w in spec.Weapons.Where(w => Stock(w.Id))) items[w.Location].Add(Weapon(w));
        foreach (var c in spec.Components.Where(c => c.Kind is ComponentKind.HeatSink or ComponentKind.JumpJet && Stock(c.Id)))
            items[c.Location].Add(new(Kind(c.Kind), c.Id, c.Name, c.Slots));
        foreach (var a in spec.Ammo.Where(a => Stock(a.Id))) items[a.Location].Add(new(SlotKind.Ammo, a.Id, a.Name, 1));
        var installed = spec.Weapons.Where(w => !Stock(w.Id)).Select(w => (w.Location, Item: Weapon(w)))
            .Concat(spec.Components.Where(c => !Stock(c.Id)).Select(c => (c.Location, Item: new Item(Kind(c.Kind), c.Id, c.Name, c.Slots))))
            .Concat(spec.Ammo.Where(a => !Stock(a.Id)).Select(a => (a.Location, Item: new Item(SlotKind.Ammo, a.Id, a.Name, 1))));
        foreach (var (location, item) in installed.OrderBy(entry => Loadout.InstallOrder(entry.Item.Id))) items[location].Add(item);

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

/// <summary>A location's weapon mounts as the hangar shows them: the free ones (Ballistic, then Energy, then Missile) and, by item,
/// the ones other items cover.</summary>
public sealed record LocationMounts(ImmutableArray<HardpointClass> Free, ImmutableDictionary<string, ImmutableArray<HardpointClass>> Covered)
{
    public static readonly LocationMounts None = new([], ImmutableDictionary<string, ImmutableArray<HardpointClass>>.Empty);
}

/// <summary>Weapon mounts are critical slots. A location's mount slots are its hardpoints; the rest are regular slots. Each weapon
/// takes one mount of its class while one is left. An installed item dropped on a mount slot covers that mount. Every other
/// slot fills regular slots first, then covers free mounts from the bottom up: Missile, then Energy, then Ballistic.</summary>
public static class MountLayout
{
    public static readonly ImmutableArray<HardpointClass> Classes = [HardpointClass.Ballistic, HardpointClass.Energy, HardpointClass.Missile];

    public static ImmutableDictionary<MechLocation, LocationMounts> For(MechSpec spec, IReadOnlyList<InstalledItem> installed) =>
        For(spec, installed, CriticalLayout.For(spec));

    public static ImmutableDictionary<MechLocation, LocationMounts> For(MechSpec spec, IReadOnlyList<InstalledItem> installed,
        ImmutableDictionary<MechLocation, ImmutableArray<CriticalBlock>> layout)
    {
        var hardpoints = PreparedMechModels.Hardpoints(spec.Id);
        var chosen = installed.Where(item => item.Mount is not null).ToDictionary(item => item.Id, item => item.Mount!.Value, StringComparer.Ordinal);
        return layout.ToImmutableDictionary(pair => pair.Key, pair => Location(spec, pair.Key, pair.Value, hardpoints, chosen));
    }

    private static LocationMounts Location(MechSpec spec, MechLocation location, ImmutableArray<CriticalBlock> blocks,
        IReadOnlyList<Hardpoint> hardpoints, IReadOnlyDictionary<string, HardpointClass> chosen)
    {
        var free = Classes.ToDictionary(type => type, type => hardpoints.Count(item => item.Location == location && item.Class == type));
        var regular = Math.Max(0, EquipmentRules.Capacity(location) - free.Values.Sum());
        var items = blocks.Where(block => block.ItemId is not null).ToArray();
        var own = new HashSet<string>(StringComparer.Ordinal);
        var covered = new Dictionary<string, List<HardpointClass>>(StringComparer.Ordinal);
        bool Take(HardpointClass type) { if (free.GetValueOrDefault(type) <= 0) return false; free[type]--; return true; }
        // Weapons claim their mounts before anything may cover them. A split weapon's far half has no mount of its own.
        var weapons = spec.Weapons.Where(item => item.Location == location).ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var block in items)
            if (weapons.TryGetValue(block.ItemId!, out var weapon) && Take(WeaponMounts.ClassOf(weapon.Kind))) own.Add(block.ItemId!);
        foreach (var block in items)
            if (!own.Contains(block.ItemId!) && chosen.TryGetValue(block.ItemId!, out var mount) && Take(mount))
            { own.Add(block.ItemId!); covered[block.ItemId!] = [mount]; }

        var overflow = Math.Max(0, items.Sum(block => block.Length - (own.Contains(block.ItemId!) ? 1 : 0)) - regular);
        var taken = new Queue<HardpointClass>();
        foreach (var type in Classes.Reverse())
            while (overflow > 0 && Take(type)) { taken.Enqueue(type); overflow--; }
        // The covered mounts go to the bottom-most items.
        foreach (var block in items.AsEnumerable().Reverse())
            for (var slot = own.Contains(block.ItemId!) ? 1 : 0; slot < block.Length && taken.Count > 0; slot++)
            {
                if (!covered.TryGetValue(block.ItemId!, out var list)) covered[block.ItemId!] = list = [];
                list.Add(taken.Dequeue());
            }
        return new([.. Classes.SelectMany(type => Enumerable.Repeat(type, free[type]))],
            covered.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray(), StringComparer.Ordinal));
    }
}

/// <summary>A spare from storage installed on a mech in the hangar. Ids are `fit:1`, `fit:2`, … in installation order. Mount is the
/// weapon mount a non-weapon was dropped on, which it covers.</summary>
public sealed record InstalledItem(string Id, string StorageId, MechLocation Location, HardpointClass? Mount = null);

/// <summary>A location's installed armor, front and rear, where the hangar has changed it from stock (tenths of a stock point).</summary>
public sealed record ArmorFit(MechLocation Location, int Armor, int RearArmor);

/// <summary>A mech's loadout as the stock items removed, the storage items installed, in installation order, and the locations
/// whose armor differs from stock.</summary>
public sealed record LoadoutPlan(ImmutableHashSet<string> Removed, ImmutableArray<InstalledItem> Installed)
{
    public ImmutableArray<ArmorFit> Armor { get; init; } = [];
    public static LoadoutPlan Removing(IEnumerable<string> removed) => new(removed.ToImmutableHashSet(StringComparer.Ordinal), []);
    public bool SameAs(LoadoutPlan other) => Removed.SetEquals(other.Removed) && Installed.SequenceEqual(other.Installed)
        && Armor.Length == other.Armor.Length && Armor.All(other.Armor.Contains);
}

/// <summary>An owned mech's installed equipment: its stock design less the items removed in the hangar, plus the items installed there.</summary>
public static class Loadout
{
    private const string InstalledPrefix = "fit:";
    public static bool Installed(string id) => id.StartsWith(InstalledPrefix, StringComparison.Ordinal);
    public static int InstallOrder(string id) =>
        Installed(id) && int.TryParse(id.AsSpan(InstalledPrefix.Length), System.Globalization.NumberStyles.None, null, out var order) && order > 0 ? order : -1;
    /// <summary>The id for the next installed item, after every id these items have ever used.</summary>
    public static string NextId(IEnumerable<InstalledItem> used) => InstalledPrefix + (used.Select(item => InstallOrder(item.Id)).DefaultIfEmpty(0).Max() + 1);

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

    /// <summary>The design with these stock items removed and these storage items installed: each installed heat sink adds one
    /// sink's cooling and each jump jet one jump MP. Installed weapons face forward; installed bins hold the catalog's full load.</summary>
    public static MechSpec Build(MechSpec stock, IReadOnlySet<string> removed, IReadOnlyList<InstalledItem> installed, IReadOnlyList<ArmorFit>? armor = null)
    {
        var spec = Install(Strip(stock, removed), installed);
        if (armor is null || armor.Count == 0) return spec;
        return spec with
        {
            Locations = [.. spec.Locations.Select(item => armor.FirstOrDefault(fit => fit.Location == item.Location) is { } fit
                ? item with { Armor = fit.Armor, RearArmor = fit.RearArmor } : item)]
        };
    }
    /// <summary>Why these armor fits are invalid for the design, or null: each names a distinct location, differs from stock,
    /// is not negative, carries rear armor only on a torso and stays within the location's armor limit.</summary>
    public static string? ArmorProblem(MechSpec stock, IReadOnlyList<ArmorFit> armor)
    {
        if (armor.Any(fit => fit is null || !Enum.IsDefined(fit.Location)) || armor.Select(fit => fit.Location).Distinct().Count() != armor.Count) return "Invalid armor locations";
        foreach (var fit in armor)
        {
            var location = stock.Locations.Single(item => item.Location == fit.Location);
            if (fit.Armor < 0 || fit.RearArmor < 0 || fit.RearArmor > 0 && !EquipmentRules.HasRearArmor(fit.Location)
                || fit.Armor + fit.RearArmor > EquipmentRules.ArmorLimit(location)) return "Invalid armor";
            if (fit.Armor == location.Armor && fit.RearArmor == location.RearArmor) return "Armor matches stock";
        }
        return null;
    }
    private static MechSpec Install(MechSpec spec, IReadOnlyList<InstalledItem> installed)
    {
        if (installed.Count == 0) return spec;
        var weapons = spec.Weapons.ToBuilder(); var components = spec.Components.ToBuilder(); var ammo = spec.Ammo.ToBuilder();
        var sinks = 0; var jets = 0;
        foreach (var item in installed)
        {
            var parts = item.StorageId.Split(':');
            switch (parts[0])
            {
                case "weapon":
                    weapons.Add(StorageCatalog.Weapon(Enum.Parse<WeaponKind>(parts[1]))! with { Id = item.Id, Location = item.Location, Rear = false }); break;
                case "ammo":
                    ammo.Add(StorageCatalog.Bin(Enum.Parse<AmmoKind>(parts[1]))! with { Id = item.Id, Location = item.Location }); break;
                case "heat-sink":
                    var kind = Enum.Parse<HeatSinkKind>(parts[1]);
                    components.Add(new(item.Id, StorageCatalog.Describe(item.StorageId)!.Name, item.Location, EquipmentRules.HeatSinkSlots(kind), ComponentKind.HeatSink, 1));
                    sinks++; break;
                case "jump-jet":
                    components.Add(new(item.Id, "Jump Jet", item.Location, 1, ComponentKind.JumpJet, 0)); jets++; break;
                default: throw new ArgumentException($"{item.StorageId} cannot be installed.");
            }
        }
        return spec with
        {
            Weapons = weapons.ToImmutable(), Components = components.ToImmutable(), Ammo = ammo.ToImmutable(),
            HeatSinks = spec.HeatSinks + sinks, Jump = spec.Jump + jets
        };
    }

    /// <summary>Hardpoints of a class in a location neither taken by its weapons of that class nor covered by other items (see
    /// <see cref="MountLayout"/>). Stock weapons beyond the hardpoints (or in a location with none) are kept, but leave no free mount.</summary>
    public static int FreeMounts(MechSpec spec, MechLocation location, HardpointClass type, IReadOnlyList<InstalledItem>? installed = null) =>
        MountLayout.For(spec, installed ?? [])[location].Free.Count(item => item == type);

    /// <summary>Why this storage item cannot be installed in a location of the mech as shown, or null when it can. Mass is no
    /// obstacle here: an overweight loadout can be built but not applied.</summary>
    public static string? InstallProblem(OwnedMech mech, string storageId, MechLocation location)
    {
        if (StorageCatalog.Describe(storageId) is not { } item) return "Cannot be installed";
        var spec = mech.Spec;
        if (!mech.LocationIntact(location)) return "Location destroyed";
        switch (storageId.Split(':')[0])
        {
            case "weapon":
                var type = WeaponMounts.ClassOf(Enum.Parse<WeaponKind>(storageId.Split(':')[1]));
                if (FreeMounts(spec, location, type, mech.Installed) <= 0) return $"No free {type.ToString().ToLowerInvariant()} mount";
                break;
            case "heat-sink" when storageId != StorageCatalog.HeatSinkId(spec.HeatSinkType):
                return $"Takes {DefinitionText.HeatSinks(spec.HeatSinkType)} heat sinks only";
            case "jump-jet":
                if (location is MechLocation.Head or MechLocation.LeftArm or MechLocation.RightArm) return "Jump jets: torso or legs only";
                if (storageId != StorageCatalog.JumpJetId(spec.Tonnage)) return $"Wrong jump jets for {spec.Tonnage} t";
                if (spec.Jump >= spec.Walk) return "Jump MP cannot exceed walking MP";
                break;
        }
        var free = EquipmentRules.Capacity(location) - CriticalLayout.Used(CriticalLayout.For(spec)[location]);
        return item.Slots > free ? $"Needs {item.Slots} free {(item.Slots == 1 ? "slot" : "slots")}" : null;
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

    /// <summary>The catalog's first weapon of this kind, the pattern for spares of it.</summary>
    public static WeaponSpec? Weapon(WeaponKind kind) => DraftCatalog.AllMechs.SelectMany(mech => mech.Weapons).FirstOrDefault(item => item.Kind == kind);
    /// <summary>The catalog's first bin of this ammunition, the pattern for spares of it.</summary>
    public static AmmoSpec? Bin(AmmoKind kind) => DraftCatalog.AllMechs.SelectMany(mech => mech.Ammo).FirstOrDefault(item => item.AmmoKind == kind);

    public static StorageItem? Describe(string id)
    {
        var parts = id.Split(':');
        if (parts.Length != 2) return null;
        switch (parts[0])
        {
            case "weapon" when Enum.TryParse<WeaponKind>(parts[1], out var kind) && Enum.IsDefined(kind) && parts[1] == kind.ToString():
                var weapon = Weapon(kind);
                return weapon is null ? null : new(id, weapon.Name, EquipmentRules.Group(kind), EquipmentRules.Tons(kind), EquipmentRules.Slots(kind), weapon.Heat);
            case "ammo" when Enum.TryParse<AmmoKind>(parts[1], out var ammo) && Enum.IsDefined(ammo) && parts[1] == ammo.ToString():
                var bin = Bin(ammo);
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

    /// <summary>Every item storage can hold: each weapon and ammunition the catalog carries, both heat sinks and every jump jet class.</summary>
    public static readonly ImmutableArray<StorageItem> All = Enum.GetValues<WeaponKind>().Select(WeaponId)
        .Concat(Enum.GetValues<AmmoKind>().Select(AmmoId)).Concat(Enum.GetValues<HeatSinkKind>().Select(HeatSinkId))
        .Concat(["jump-jet:0.5", "jump-jet:1", "jump-jet:2"]).Select(Describe).OfType<StorageItem>().ToImmutableArray();

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
