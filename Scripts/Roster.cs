using System.Collections.ObjectModel;
using System.Collections.Immutable;

namespace Mech;

public static class WarriorSkills
{
    public const int Expert = 2, Novice = 6;
    public const int RegularBudget = 6, VeteranBudget = 8;
    public static int Cost(int rating) => rating is >= Expert and <= Novice ? Novice - rating
        : throw new ArgumentOutOfRangeException(nameof(rating), "Pilot skills must be between 2 and 6.");
}

public static class DraftCatalog
{
    private static readonly GameCatalog Catalog = LoadMechs();
    internal static CatalogMetadata Metadata => Catalog.Metadata;
    public static readonly ImmutableArray<MechSpec> AllMechs = Catalog.Mechs;
    // The initial draft is an explicit, stable subset of the complete catalog.
    public static readonly ImmutableArray<MechSpec> Mechs = new[]
    {
        "firestarter", "jenner", "panther", "griffin", "shadow-hawk", "wolverine",
        "catapult", "crusader", "thunderbolt", "awesome", "victor", "zeus"
    }.Select(id => AllMechs.Single(mech => mech.Id == id)).ToImmutableArray();
    public static readonly ImmutableArray<WarriorSpec> Warriors = CatalogLoader.ValidateWarriors(new WarriorSpec[]
    {
        new("mara-vale", "Mara Vale", "Kestrel", 4, 4, 4, WarriorExperience.Regular, WarriorSpecialty.None),
        new("ivo-chen", "Ivo Chen", "Needle", 3, 5, 4, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("sana-idris", "Sana Idris", "Drift", 5, 3, 4, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("tomas-reed", "Tomas Reed", "Rook", 5, 5, 2, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("elena-voss", "Elena Voss", "Flint", 2, 4, 6, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("amir-solano", "Amir Solano", "Switch", 4, 2, 6, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("niko-ward", "Niko Ward", "Anchor", 4, 4, 4, WarriorExperience.Regular, WarriorSpecialty.None),
        new("hana-sato", "Hana Sato", "Glint", 3, 5, 4, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("leila-darzi", "Leila Darzi", "Kite", 5, 3, 4, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("renata-kovac", "Renata Kovac", "Iron", 3, 3, 4, WarriorExperience.Veteran, WarriorSpecialty.None),
        new("gideon-price", "Gideon Price", "Crosshair", 2, 4, 4, WarriorExperience.Veteran, WarriorSpecialty.Gunnery),
        new("oskar-lind", "Oskar Lind", "Skid", 4, 2, 4, WarriorExperience.Veteran, WarriorSpecialty.Piloting),
        new("ada-moreno", "Ada Moreno", "Sparrow", 5, 4, 3, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("malik-stone", "Malik Stone", "Hawk", 2, 5, 5, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("yara-ortiz", "Yara Ortiz", "Vector", 5, 2, 5, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("lucas-chen", "Lucas Chen", "Scope", 6, 4, 2, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("imani-brooks", "Imani Brooks", "Viper", 3, 4, 5, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("erik-holm", "Erik Holm", "Dash", 4, 3, 5, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("mei-tanaka", "Mei Tanaka", "Beacon", 4, 6, 2, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("rafiq-nasir", "Rafiq Nasir", "Bolt", 2, 6, 4, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("fiona-kerr", "Fiona Kerr", "Slip", 6, 2, 4, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("vera-holt", "Vera Holt", "Bulwark", 4, 4, 2, WarriorExperience.Veteran, WarriorSpecialty.Sensors),
        new("anton-mercer", "Anton Mercer", "Longshot", 2, 3, 5, WarriorExperience.Veteran, WarriorSpecialty.Gunnery),
        new("zara-okoro", "Zara Okoro", "Dancer", 3, 2, 5, WarriorExperience.Veteran, WarriorSpecialty.Piloting)
    });
    public static readonly ImmutableArray<string> PoolNames = ["Light · 20–35 tons", "Medium · 40–55 tons", "Heavy · 60–75 tons", "Assault · 80–100 tons",
        "MechWarriors · I", "MechWarriors · II", "MechWarriors · III", "MechWarriors · Veterans"];

    private static GameCatalog LoadMechs()
    {
        using var stream = typeof(DraftCatalog).Assembly.GetManifestResourceStream("Mech.Data.mech-catalog.json")
            ?? throw new InvalidOperationException("The stock BattleMech catalog is missing.");
        return CatalogLoader.Read(stream);
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
    private readonly MechSpec[,] _mechs = new MechSpec[4, 3];
    private readonly WarriorSpec[,] _warriors = new WarriorSpec[4, 3];

    public InitialDraft()
    {
        for (var p = 0; p < 4; p++) for (var c = 0; c < 3; c++)
        { _mechs[p, c] = DraftCatalog.Mechs[p * 3 + c]; _warriors[p, c] = DraftCatalog.Warriors[p * 3 + c]; }
    }
    public MechSpec MechCandidate(int pool, int choice) => _mechs[pool, choice];
    public WarriorSpec WarriorCandidate(int pool, int choice) => _warriors[pool - 4, choice];
    private static T[] Shuffle<T>(IEnumerable<T> source, Random random)
    {
        var values = source.ToArray();
        for (var i = values.Length - 1; i > 0; i--) { var j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
        return values;
    }

    public static InitialDraft CreateRandom(Random? random = null)
    {
        random ??= Random.Shared;
        var draft = new InitialDraft
        {
            ForceName = $"{NamePrefixes[random.Next(NamePrefixes.Length)]} {NameSuffixes[random.Next(NameSuffixes.Length)]}"
        };
        for (var p = 0; p < 4; p++)
        {
            var pool = p;
            var candidates = Shuffle(DraftCatalog.AllMechs.Where(m => (int)m.WeightClass == pool), random);
            for (var c = 0; c < 3; c++) draft._mechs[p, c] = candidates[c];
        }
        var regulars = Shuffle(DraftCatalog.Warriors.Where(w => w.Rank == WarriorExperience.Regular), random);
        var veterans = Shuffle(DraftCatalog.Warriors.Where(w => w.Rank == WarriorExperience.Veteran), random);
        for (var p = 0; p < 4; p++) for (var c = 0; c < 3; c++)
            draft._warriors[p, c] = p == 3 ? veterans[c] : regulars[p * 3 + c];
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
        var roster = ForceRoster.Create(ForceName.Trim(), Enumerable.Range(0, 4).Select(pool => MechCandidate(pool, _choices[pool])),
            Enumerable.Range(4, 4).Select(pool => WarriorCandidate(pool, _choices[pool])));
        for (var column = 0; column < 4; column++)
            roster.Assign(roster.Mechs[column].Id, roster.Warriors[column].Id);
        return roster;
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
    internal OwnedMech(MechSnapshot save) : this(save, DraftCatalog.Mech(save.DefinitionId)) { }
    internal OwnedMech(MechSnapshot save, MechSpec definition)
    {
        if (definition.Id != save.DefinitionId) throw new ArgumentException("Definition identity does not match owned state.");
        Id = save.Id; Spec = definition; WarriorId = save.WarriorId;
        Locations = Array.AsReadOnly(save.Locations.ToArray()); Equipment = Array.AsReadOnly(save.Equipment.ToArray()); Ammo = Array.AsReadOnly(save.Ammo.ToArray());
    }
    public ComponentCondition Condition(string id) => Equipment.Single(item => item.Id == id).Condition;
    public bool LocationIntact(MechLocation location) => Locations.Single(item => item.Location == location).Structure > 0;
    private bool Functional(ComponentKind kind) => Spec.Components.Where(item => item.Kind == kind)
        .All(item => Condition(item.Id) != ComponentCondition.Destroyed && LocationIntact(item.Location));
    public bool WeaponUsable(WeaponSpec weapon) => Condition(weapon.Id) != ComponentCondition.Destroyed && LocationIntact(weapon.Location)
        && (weapon.AmmoKind is null || Spec.Ammo.Any(bin => bin.AmmoKind == weapon.AmmoKind && LocationIntact(bin.Location)
            && Condition(bin.Id) != ComponentCondition.Destroyed && Ammo.Single(item => item.Id == bin.Id).Shots > 0));
    public bool Operational => Functional(ComponentKind.Cockpit) && Functional(ComponentKind.FusionEngine) && Functional(ComponentKind.Gyro)
        && Functional(ComponentKind.Hip) && Functional(ComponentKind.UpperLegActuator) && Functional(ComponentKind.LowerLegActuator)
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
    /// <summary>Multiplies saved armor and structure, leaving malformed entries for Restore to reject.</summary>
    internal static RosterSnapshot Rescale(RosterSnapshot save, int factor)
    {
        int Scale(int value) => value >= 0 && value <= int.MaxValue / factor ? value * factor : -1;
        return save with
        {
            Mechs = save.Mechs?.Select(mech => mech is null ? null! : mech with
            {
                Locations = mech.Locations?.Select(loc => loc is null ? null! : loc with
                    { Armor = Scale(loc.Armor), RearArmor = Scale(loc.RearArmor), Structure = Scale(loc.Structure) }).ToArray()!
            }).ToArray()!
        };
    }
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
