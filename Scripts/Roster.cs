using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mech;

public enum MechLocation { Head, CenterTorso, LeftTorso, RightTorso, LeftArm, RightArm, LeftLeg, RightLeg }
public enum WarriorHealth { Healthy, Injured, Dead }
public enum ComponentCondition { Operational, Damaged, Destroyed }
public sealed record LocationSpec(MechLocation Location, int Armor, int RearArmor, int Structure);
public sealed record WeaponSpec(string Id, string Name, MechLocation Location, bool Rear, int Heat, string Damage,
    int Minimum, int Short, int Medium, int Long, string? AmmoType);
public sealed record ComponentSpec(string Id, string Name, MechLocation Location, int Slots);
public sealed record AmmoSpec(string Id, string Name, MechLocation Location, string AmmoType, int Shots);
public sealed record MechSpec(string Id, string Chassis, string Variant, int Tonnage, string WeightClass, int Introduced,
    string Role, int Walk, int Run, int Jump, int HeatSinks, string Engine, LocationSpec[] Locations,
    WeaponSpec[] Weapons, ComponentSpec[] Components, AmmoSpec[] Ammo, string Source)
{
    public string DisplayName => $"{Chassis} {Variant}";
    public string Artwork => $"res://Assets/Roster/{Id}.png";
    public int ArmorTotal => Locations.Sum(item => item.Armor + item.RearArmor);
    public int StructureTotal => Locations.Sum(item => item.Structure);
}
public sealed record WarriorSpec(string Id, string Name, string Callsign, int Gunnery, int Piloting, string Experience)
{
    public string DisplayName => $"{Name}  “{Callsign}”";
    public string Artwork => $"res://Assets/Roster/{Id}.png";
}

public static class DraftCatalog
{
    private sealed record CatalogFile(MechSpec[] Mechs);
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };
    public static readonly IReadOnlyList<MechSpec> AllMechs = LoadMechs();
    // The initial draft is an explicit, stable subset of the complete catalog.
    public static readonly IReadOnlyList<MechSpec> Mechs = Array.AsReadOnly(new[]
    {
        "firestarter", "jenner", "panther", "griffin", "shadow-hawk", "wolverine",
        "catapult", "crusader", "thunderbolt", "awesome", "victor", "zeus"
    }.Select(id => AllMechs.Single(mech => mech.Id == id)).ToArray());
    public static readonly IReadOnlyList<WarriorSpec> Warriors = Array.AsReadOnly(new WarriorSpec[]
    {
        new("mara-vale", "Mara Vale", "Kestrel", 4, 5, "Regular"),
        new("ivo-chen", "Ivo Chen", "Needle", 3, 6, "Regular · Gunnery"),
        new("sana-idris", "Sana Idris", "Drift", 5, 4, "Regular · Piloting"),
        new("tomas-reed", "Tomas Reed", "Rook", 4, 5, "Regular"),
        new("elena-voss", "Elena Voss", "Flint", 3, 6, "Regular · Gunnery"),
        new("amir-solano", "Amir Solano", "Switch", 5, 4, "Regular · Piloting"),
        new("niko-ward", "Niko Ward", "Anchor", 4, 5, "Regular"),
        new("hana-sato", "Hana Sato", "Glint", 3, 6, "Regular · Gunnery"),
        new("leila-darzi", "Leila Darzi", "Kite", 5, 4, "Regular · Piloting"),
        new("renata-kovac", "Renata Kovac", "Iron", 3, 4, "Veteran"),
        new("gideon-price", "Gideon Price", "Crosshair", 2, 5, "Veteran · Gunnery"),
        new("oskar-lind", "Oskar Lind", "Skid", 4, 3, "Veteran · Piloting")
    });
    public static readonly string[] PoolNames = ["Light · 35 tons", "Medium · 55 tons", "Heavy · 65 tons", "Assault · 80 tons",
        "MechWarriors · I", "MechWarriors · II", "MechWarriors · III", "MechWarriors · Veterans"];

    private static IReadOnlyList<MechSpec> LoadMechs()
    {
        using var stream = typeof(DraftCatalog).Assembly.GetManifestResourceStream("Mech.Data.mech-catalog.json")
            ?? throw new InvalidOperationException("The stock BattleMech catalog is missing.");
        return Array.AsReadOnly(JsonSerializer.Deserialize<CatalogFile>(stream, Options)!.Mechs);
    }
    public static MechSpec Mech(string id) => AllMechs.SingleOrDefault(item => item.Id == id)
        ?? throw new ArgumentException("Unknown BattleMech variant.");
    public static WarriorSpec Warrior(string id) => Warriors.SingleOrDefault(item => item.Id == id)
        ?? throw new ArgumentException("Unknown MechWarrior.");
    public static string LocationName(MechLocation location) => location switch
    {
        MechLocation.CenterTorso => "Center torso", MechLocation.LeftTorso => "Left torso", MechLocation.RightTorso => "Right torso",
        MechLocation.LeftArm => "Left arm", MechLocation.RightArm => "Right arm", MechLocation.LeftLeg => "Left leg", MechLocation.RightLeg => "Right leg", _ => "Head"
    };
    public static string Abbreviation(MechLocation location) => location switch
    {
        MechLocation.CenterTorso => "CT", MechLocation.LeftTorso => "LT", MechLocation.RightTorso => "RT",
        MechLocation.LeftArm => "LA", MechLocation.RightArm => "RA", MechLocation.LeftLeg => "LL", MechLocation.RightLeg => "RL", _ => "HD"
    };
}

/// <summary>A reversible selection; nothing reaches the campaign until all eight pools and its name are confirmed.</summary>
public sealed class InitialDraft
{
    private static readonly string[] NamePrefixes = ["Iron", "Ashen", "Crimson", "Silver", "Steel", "Ember", "Storm", "Obsidian"];
    private static readonly string[] NameSuffixes = ["Jackals", "Lancers", "Falcons", "Wolves", "Sentinels", "Dragoons", "Marauders", "Wardens"];
    private readonly int[] _choices = Enumerable.Repeat(-1, 8).ToArray();

    public static InitialDraft CreateRandom(Random? random = null)
    {
        random ??= Random.Shared;
        var draft = new InitialDraft
        {
            ForceName = $"{NamePrefixes[random.Next(NamePrefixes.Length)]} {NameSuffixes[random.Next(NameSuffixes.Length)]}"
        };
        for (var pool = 0; pool < draft._choices.Length; pool++) draft.Select(pool, random.Next(3));
        return draft;
    }

    public string ForceName { get; set; } = "";
    public int SelectedCount => _choices.Count(item => item >= 0);
    public bool Complete => ValidName(ForceName) && SelectedCount == 8;
    public int Choice(int pool) => _choices[pool];
    public void Select(int pool, int choice)
    {
        if (pool is < 0 or > 7 || choice is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(choice));
        _choices[pool] = choice;
    }
    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 40
        && !name.Any(char.IsControl);
    public ForceRoster Establish()
    {
        if (!Complete) throw new InvalidOperationException("Name the company and select one candidate from each pool.");
        return ForceRoster.Create(ForceName.Trim(), Enumerable.Range(0, 4).Select(pool => DraftCatalog.Mechs[pool * 3 + _choices[pool]]),
            Enumerable.Range(4, 4).Select(pool => DraftCatalog.Warriors[(pool - 4) * 3 + _choices[pool]]));
    }
}

public sealed record LocationCondition(MechLocation Location, int Armor, int RearArmor, int Structure);
public sealed record EquipmentCondition(string Id, ComponentCondition Condition);
public sealed record AmmoCondition(string Id, int Shots);
public sealed record MechSnapshot(string Id, string DefinitionId, string? WarriorId, LocationCondition[] Locations,
    EquipmentCondition[] Equipment, AmmoCondition[] Ammo);
public sealed record WarriorSnapshot(string DefinitionId, WarriorHealth Health, DateOnly? RecoveryDate);
public sealed record RosterSnapshot(string ForceName, MechSnapshot[] Mechs, WarriorSnapshot[] Warriors);

public sealed class OwnedMech
{
    public string Id { get; }
    public MechSpec Spec { get; }
    public string? WarriorId { get; internal set; }
    public IReadOnlyList<LocationCondition> Locations { get; }
    public IReadOnlyList<EquipmentCondition> Equipment { get; }
    public IReadOnlyList<AmmoCondition> Ammo { get; }
    internal OwnedMech(MechSnapshot save)
    {
        Id = save.Id; Spec = DraftCatalog.Mech(save.DefinitionId); WarriorId = save.WarriorId;
        Locations = Array.AsReadOnly(save.Locations.ToArray()); Equipment = Array.AsReadOnly(save.Equipment.ToArray()); Ammo = Array.AsReadOnly(save.Ammo.ToArray());
    }
    public ComponentCondition Condition(string id) => Equipment.Single(item => item.Id == id).Condition;
    public bool LocationIntact(MechLocation location) => Locations.Single(item => item.Location == location).Structure > 0;
    private bool Functional(string name) => Spec.Components.Where(item => item.Name == name)
        .All(item => Condition(item.Id) != ComponentCondition.Destroyed && LocationIntact(item.Location));
    public bool WeaponUsable(WeaponSpec weapon) => Condition(weapon.Id) != ComponentCondition.Destroyed && LocationIntact(weapon.Location)
        && (weapon.AmmoType is null || Spec.Ammo.Any(bin => bin.AmmoType == weapon.AmmoType && LocationIntact(bin.Location)
            && Condition(bin.Id) != ComponentCondition.Destroyed && Ammo.Single(item => item.Id == bin.Id).Shots > 0));
    public bool Operational => Functional("Cockpit") && Functional("Fusion Engine") && Functional("Gyro")
        && Functional("Hip") && Functional("Upper Leg Actuator") && Functional("Lower Leg Actuator")
        && LocationIntact(MechLocation.LeftLeg) && LocationIntact(MechLocation.RightLeg) && Spec.Weapons.Any(WeaponUsable);
    public bool Damaged => Locations.Any(item => { var spec = Spec.Locations.Single(loc => loc.Location == item.Location); return item.Armor < spec.Armor || item.RearArmor < spec.RearArmor || item.Structure < spec.Structure; })
        || Equipment.Any(item => item.Condition != ComponentCondition.Operational);
    public string Status => !Operational ? "Not operational" : Damaged ? "Operational · damaged" : "Operational";
    internal MechSnapshot Snapshot() => new(Id, Spec.Id, WarriorId, Locations.ToArray(), Equipment.ToArray(), Ammo.ToArray());
}

public sealed class RosterWarrior
{
    public WarriorSpec Spec { get; }
    public string Id => Spec.Id;
    public WarriorHealth Health { get; internal set; }
    public DateOnly? RecoveryDate { get; internal set; }
    internal RosterWarrior(WarriorSnapshot save) { Spec = DraftCatalog.Warrior(save.DefinitionId); Health = save.Health; RecoveryDate = save.RecoveryDate; }
    public bool Ready => Health == WarriorHealth.Healthy;
    public string Status => Health switch { WarriorHealth.Healthy => "Healthy · ready for deployment", WarriorHealth.Injured => $"Injured · recovers {RecoveryDate:dd MMM yyyy}", _ => "In memoriam" };
    internal WarriorSnapshot Snapshot() => new(Id, Health, RecoveryDate);
}

/// <summary>Owned assets and personnel; assignments are independent of any future mission deployment.</summary>
public sealed class ForceRoster
{
    public string ForceName { get; }
    public IReadOnlyList<OwnedMech> Mechs { get; }
    public IReadOnlyList<RosterWarrior> Warriors { get; }
    public int ReadyWarriors => Warriors.Count(item => item.Ready);
    private ForceRoster(string name, IEnumerable<OwnedMech> mechs, IEnumerable<RosterWarrior> warriors)
    { ForceName = name; Mechs = new ReadOnlyCollection<OwnedMech>(mechs.ToArray()); Warriors = new ReadOnlyCollection<RosterWarrior>(warriors.ToArray()); }
    internal static ForceRoster Create(string name, IEnumerable<MechSpec> mechs, IEnumerable<WarriorSpec> warriors)
    {
        var units = mechs.Select((spec, i) => new OwnedMech(new($"mech-{i + 1}", spec.Id, null,
            spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)).ToArray(),
            spec.Weapons.Select(item => item.Id).Concat(spec.Components.Select(item => item.Id)).Concat(spec.Ammo.Select(item => item.Id))
                .Select(id => new EquipmentCondition(id, ComponentCondition.Operational)).ToArray(),
            spec.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)).ToArray())));
        return new(name, units, warriors.Select(spec => new RosterWarrior(new(spec.Id, WarriorHealth.Healthy, null))));
    }
    public OwnedMech? Assignment(string warriorId) => Mechs.SingleOrDefault(item => item.WarriorId == warriorId);
    public RosterWarrior? Pilot(OwnedMech mech) => Warriors.SingleOrDefault(item => item.Id == mech.WarriorId);
    public void Assign(string mechId, string? warriorId, bool confirmSwap = false)
    {
        var target = Mechs.SingleOrDefault(item => item.Id == mechId) ?? throw new ArgumentException("Unknown owned BattleMech.");
        if (warriorId == target.WarriorId) return;
        if (warriorId is null) { target.WarriorId = null; return; }
        var warrior = Warriors.SingleOrDefault(item => item.Id == warriorId) ?? throw new ArgumentException("Unknown owned MechWarrior.");
        if (warrior.Health == WarriorHealth.Dead) throw new InvalidOperationException("A deceased MechWarrior cannot be assigned.");
        var previous = Assignment(warriorId);
        if (previous is not null && !confirmSwap) throw new InvalidOperationException("Confirm the transfer or swap before changing an occupied assignment.");
        if (previous is not null) previous.WarriorId = target.WarriorId;
        target.WarriorId = warriorId;
    }
    public void SetHealth(string warriorId, WarriorHealth health, DateOnly? recoveryDate, DateOnly today)
    {
        var warrior = Warriors.SingleOrDefault(item => item.Id == warriorId) ?? throw new ArgumentException("Unknown owned MechWarrior.");
        if (!Enum.IsDefined(health) || (health == WarriorHealth.Injured ? recoveryDate is null || recoveryDate <= today : recoveryDate is not null))
            throw new ArgumentException("An injury needs a future recovery date; other health states cannot have one.");
        if (warrior.Health == WarriorHealth.Dead && health != WarriorHealth.Dead) throw new InvalidOperationException("Death is permanent.");
        warrior.Health = health; warrior.RecoveryDate = recoveryDate;
        if (health == WarriorHealth.Dead && Assignment(warriorId) is { } mech) mech.WarriorId = null;
    }
    internal void Recover(DateOnly today)
    {
        foreach (var warrior in Warriors.Where(item => item.Health == WarriorHealth.Injured && item.RecoveryDate <= today))
        { warrior.Health = WarriorHealth.Healthy; warrior.RecoveryDate = null; }
    }
    public RosterSnapshot Snapshot() => new(ForceName, Mechs.Select(item => item.Snapshot()).ToArray(), Warriors.Select(item => item.Snapshot()).ToArray());
    public static ForceRoster Restore(RosterSnapshot save, DateOnly date)
    {
        if (!InitialDraft.ValidName(save.ForceName) || save.ForceName != save.ForceName.Trim() || save.Mechs is null || save.Warriors is null
            || save.Mechs.Length == 0 || save.Warriors.Length == 0)
            throw new ArgumentException("Invalid roster.");
        var warriorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var warrior in save.Warriors)
        {
            if (warrior is null || !warriorIds.Add(warrior.DefinitionId) || !Enum.IsDefined(warrior.Health)
                || (warrior.Health == WarriorHealth.Injured ? warrior.RecoveryDate is null || warrior.RecoveryDate <= date : warrior.RecoveryDate is not null))
                throw new ArgumentException("Invalid MechWarrior state.");
            _ = DraftCatalog.Warrior(warrior.DefinitionId);
        }
        var mechIds = new HashSet<string>(StringComparer.Ordinal); var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mech in save.Mechs)
        {
            if (mech is null || string.IsNullOrWhiteSpace(mech.Id) || !mechIds.Add(mech.Id)) throw new ArgumentException("Invalid BattleMech identity.");
            var spec = DraftCatalog.Mech(mech.DefinitionId);
            if (mech.WarriorId is { } pilot && (!warriorIds.Contains(pilot) || !assigned.Add(pilot)
                || save.Warriors.Single(item => item.DefinitionId == pilot).Health == WarriorHealth.Dead)) throw new ArgumentException("Invalid pilot assignment.");
            if (mech.Locations is null || mech.Locations.Length != spec.Locations.Length || mech.Locations.Any(item => item is null)
                || mech.Locations.Select(item => item.Location).Distinct().Count() != spec.Locations.Length) throw new ArgumentException("Invalid location condition.");
            foreach (var loc in mech.Locations)
            {
                var maximum = spec.Locations.SingleOrDefault(item => item.Location == loc.Location) ?? throw new ArgumentException("Unknown location.");
                if (loc.Armor < 0 || loc.Armor > maximum.Armor || loc.RearArmor < 0 || loc.RearArmor > maximum.RearArmor
                    || loc.Structure < 0 || loc.Structure > maximum.Structure) throw new ArgumentException("Invalid armor or structure.");
            }
            var equipmentIds = spec.Weapons.Select(item => item.Id).Concat(spec.Components.Select(item => item.Id)).Concat(spec.Ammo.Select(item => item.Id)).ToHashSet();
            if (mech.Equipment is null || mech.Equipment.Length != equipmentIds.Count || mech.Equipment.Any(item => item is null || !Enum.IsDefined(item.Condition))
                || !equipmentIds.SetEquals(mech.Equipment.Select(item => item.Id)) || mech.Equipment.Select(item => item.Id).Distinct().Count() != equipmentIds.Count)
                throw new ArgumentException("Invalid component condition.");
            if (mech.Ammo is null || mech.Ammo.Any(item => item is null) || mech.Ammo.Length != spec.Ammo.Length
                || !mech.Ammo.Select(item => item.Id).ToHashSet().SetEquals(spec.Ammo.Select(item => item.Id))
                || mech.Ammo.Select(item => item.Id).Distinct().Count() != spec.Ammo.Length) throw new ArgumentException("Invalid ammunition bins.");
            foreach (var bin in mech.Ammo)
                if (bin.Shots < 0 || bin.Shots > spec.Ammo.Single(item => item.Id == bin.Id).Shots) throw new ArgumentException("Invalid ammunition quantity.");
        }
        return new(save.ForceName, save.Mechs.Select(item => new OwnedMech(item)), save.Warriors.Select(item => new RosterWarrior(item)));
    }
}
