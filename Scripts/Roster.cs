using System.Collections.ObjectModel;
using System.Collections.Immutable;

namespace Mech;

public static class WarriorSkills
{
    public const int Expert = 2, Novice = 6;
    public const int RegularBudget = 6, VeteranBudget = 8;
    public static int Cost(int rating) => rating is >= Expert and <= Novice ? Novice - rating
        : throw new ArgumentOutOfRangeException(nameof(rating), "Pilot skills must be between 2 and 6.");
    /// <summary>The word shown for a skill rating: 6 Green through 2 Legendary. Ratings outside 2–6 (test-only units) take the nearest word.</summary>
    public static string Descriptor(int rating) => Math.Clamp(rating, Expert, Novice) switch
    {
        6 => "Green", 5 => "Regular", 4 => "Veteran", 3 => "Elite", _ => "Legendary",
    };
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
    public static readonly ImmutableArray<string> PoolNames = ["Light", "Medium", "Heavy", "Assault",
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

/// <summary>A draft as sent to the player who makes it: candidate ids pool by pool, the current choices, and whether this draft names
/// the company.</summary>
public sealed record DraftOffer(string[] Mechs, string[] Warriors, int[] Choices, string ForceName, bool NamesCompany);

/// <summary>A reversible selection; nothing reaches the campaign until all eight pools and its name are confirmed.</summary>
public sealed class InitialDraft
{
    private const int RegularPools = 3, Candidates = 3;
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

    /// <summary>The most players whose drafts can all offer different MechWarriors: each draft needs nine regulars and three
    /// veterans of its own.</summary>
    public static int MaxPlayers => Math.Min(ForceRoster.MaxLances, Math.Min(
        DraftCatalog.Warriors.Count(w => w.Rank == WarriorExperience.Regular) / (RegularPools * Candidates),
        DraftCatalog.Warriors.Count(w => w.Rank == WarriorExperience.Veteran) / Candidates));
    /// <summary>Why a company of this many players cannot be drafted, or null when it can.</summary>
    public static string? CompanyProblem(int players) => players is < 1 or > ForceRoster.MaxLances ? "A company has one to three players."
        : players > MaxPlayers ? $"{players} players need {players * RegularPools * Candidates} regular and {players * Candidates} veteran MechWarriors in the catalog; it has "
            + $"{DraftCatalog.Warriors.Count(w => w.Rank == WarriorExperience.Regular)} and {DraftCatalog.Warriors.Count(w => w.Rank == WarriorExperience.Veteran)}." : null;

    /// <summary>One draft per player slot. Mech candidates are drawn for each player separately; MechWarrior candidates never repeat
    /// between players, so every lance ends with MechWarriors of its own. Only the leader's draft names the company.</summary>
    public static IReadOnlyList<InitialDraft> CreateCompany(int players, Random? random = null)
    {
        if (CompanyProblem(players) is { } problem) throw new ArgumentOutOfRangeException(nameof(players), problem);
        random ??= Random.Shared;
        var regulars = Shuffle(DraftCatalog.Warriors.Where(w => w.Rank == WarriorExperience.Regular), random);
        var veterans = Shuffle(DraftCatalog.Warriors.Where(w => w.Rank == WarriorExperience.Veteran), random);
        return Enumerable.Range(0, players).Select(slot =>
        {
            var draft = CreateRandom(random);
            for (var p = 0; p < 4; p++) for (var c = 0; c < Candidates; c++)
                draft._warriors[p, c] = p == 3 ? veterans[slot * Candidates + c] : regulars[slot * RegularPools * Candidates + p * Candidates + c];
            if (slot > 0) { draft.NamesCompany = false; draft.ForceName = ""; }
            return draft;
        }).ToArray();
    }

    /// <summary>Whether this draft names the company: the leader's does; the other players' drafts only choose their lances.</summary>
    public bool NamesCompany { get; private set; } = true;
    public DraftOffer Offer() => new(Enumerable.Range(0, 4).SelectMany(p => Enumerable.Range(0, Candidates).Select(c => _mechs[p, c].Id)).ToArray(),
        Enumerable.Range(0, 4).SelectMany(p => Enumerable.Range(0, Candidates).Select(c => _warriors[p, c].Id)).ToArray(), [.. _choices], ForceName, NamesCompany);
    public static InitialDraft FromOffer(DraftOffer offer)
    {
        if (offer.Mechs is not { Length: 4 * Candidates } || offer.Warriors is not { Length: 4 * Candidates } || offer.Choices is not { Length: 8 })
            throw new ArgumentException("Invalid draft.");
        var draft = new InitialDraft { NamesCompany = offer.NamesCompany, ForceName = offer.ForceName ?? "" };
        for (var p = 0; p < 4; p++) for (var c = 0; c < Candidates; c++)
        {
            draft._mechs[p, c] = DraftCatalog.Mech(offer.Mechs[p * Candidates + c]);
            draft._warriors[p, c] = DraftCatalog.Warrior(offer.Warriors[p * Candidates + c]);
            if ((int)draft._mechs[p, c].WeightClass != p || (draft._warriors[p, c].Rank == WarriorExperience.Veteran) != (p == 3))
                throw new ArgumentException("Invalid draft.");
        }
        for (var pool = 0; pool < 8; pool++) if (offer.Choices[pool] >= 0) draft.Select(pool, offer.Choices[pool]);
        return draft;
    }
    /// <summary>Takes a player's confirmed choices, and the company name if this draft names it.</summary>
    public void Accept(int[] choices, string? forceName)
    {
        if (choices is not { Length: 8 } || choices.Any(choice => choice is < 0 or >= Candidates)) throw new ArgumentException("Select one candidate from each pool.");
        for (var pool = 0; pool < 8; pool++) Select(pool, choices[pool]);
        if (NamesCompany) ForceName = forceName ?? "";
        if (!Complete) throw new ArgumentException("Name the company and select one candidate from each pool.");
    }

    /// <summary>Forms a company of one lance per draft, in slot order, each mech piloted by the MechWarrior drafted in its column.</summary>
    public static ForceRoster EstablishCompany(IReadOnlyList<InitialDraft> drafts)
    {
        if (drafts.Count is < 1 or > ForceRoster.MaxLances || drafts.Any(draft => !draft.Complete) || !drafts[0].NamesCompany)
            throw new InvalidOperationException("Every player must complete their draft, and the leader must name the company.");
        var mechs = drafts.SelectMany(draft => Enumerable.Range(0, 4).Select(pool => draft.MechCandidate(pool, draft._choices[pool]))).ToArray();
        var warriors = drafts.SelectMany(draft => Enumerable.Range(4, 4).Select(pool => draft.WarriorCandidate(pool, draft._choices[pool]))).ToArray();
        if (warriors.Select(item => item.Id).Distinct().Count() != warriors.Length) throw new InvalidOperationException("Two lances drafted the same MechWarrior.");
        var roster = ForceRoster.Create(drafts[0].ForceName.Trim(), mechs, warriors, drafts.Count);
        for (var i = 0; i < mechs.Length; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        return roster;
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
    public bool Complete => (!NamesCompany || ValidName(ForceName)) && SelectedCount == 8;
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
/// <summary>Removed lists the stock items taken out in the hangar, Installed the storage items put in, in installation order, and
/// Armor the locations whose installed armor differs from stock; each is null when empty.</summary>
/// <summary>Lance is the zero-based lance the mech belongs to, or null in the lance pool.</summary>
public sealed record MechSnapshot(string Id, string DefinitionId, string? WarriorId, LocationCondition[] Locations,
    EquipmentCondition[] Equipment, AmmoCondition[] Ammo, string[]? Removed = null, InstalledItem[]? Installed = null, ArmorFit[]? Armor = null,
    int? Lance = null);
/// <summary>Deployments and Kills are the warrior's career record; saves from before it have none.</summary>
public sealed record WarriorSnapshot(string DefinitionId, WarriorHealth Health, DateOnly? RecoveryDate, int Deployments = 0, int Kills = 0);
/// <summary>Lances is the company's lance count; saves from before lances have none.</summary>
public sealed record RosterSnapshot(string ForceName, MechSnapshot[] Mechs, WarriorSnapshot[] Warriors, StorageEntry[]? Storage = null, int? Lances = null);

public sealed class OwnedMech
{
    public string Id { get; }
    /// <summary>The stock catalog design.</summary>
    public MechSpec Definition { get; }
    /// <summary>The installed loadout: the stock design less the removed items, plus the installed ones.</summary>
    public MechSpec Spec { get; private set; }
    /// <summary>Stock weapons, bins, heat sinks and jump jets taken out in the hangar.</summary>
    public ImmutableHashSet<string> Removed { get; private set; }
    /// <summary>Spares from storage put in in the hangar, in installation order.</summary>
    public ImmutableArray<InstalledItem> Installed { get; private set; }
    /// <summary>Locations whose installed armor was changed from stock in the hangar.</summary>
    public ImmutableArray<ArmorFit> Armor { get; private set; }
    public LoadoutPlan Plan => new(Removed, Installed) { Armor = Armor };
    public string? WarriorId { get; internal set; }
    /// <summary>The zero-based lance the mech belongs to, or null in the lance pool.</summary>
    public int? Lance { get; internal set; }
    public IReadOnlyList<LocationCondition> Locations { get; private set; }
    public IReadOnlyList<EquipmentCondition> Equipment { get; private set; }
    public IReadOnlyList<AmmoCondition> Ammo { get; private set; }
    internal OwnedMech(MechSnapshot save) : this(save, DraftCatalog.Mech(save.DefinitionId)) { }
    internal OwnedMech(MechSnapshot save, MechSpec definition)
    {
        if (definition.Id != save.DefinitionId) throw new ArgumentException("Definition identity does not match owned state.");
        Id = save.Id; Definition = definition; WarriorId = save.WarriorId; Lance = save.Lance;
        Removed = (save.Removed ?? []).ToImmutableHashSet(StringComparer.Ordinal); Installed = [.. save.Installed ?? []]; Armor = [.. save.Armor ?? []];
        Spec = Loadout.Build(definition, Removed, Installed, Armor);
        Locations = Array.AsReadOnly(save.Locations.ToArray()); Equipment = Array.AsReadOnly(save.Equipment.ToArray()); Ammo = Array.AsReadOnly(save.Ammo.ToArray());
    }
    /// <summary>Rated tonnage less the mass of removed items plus that of installed ones and of armor added (16 stock points a ton);
    /// stock designs are taken to be exactly at their rating.</summary>
    public float Tons => Definition.Tonnage - Removed.Sum(id => Loadout.Tons(Definition, id) ?? 0) + Installed.Sum(item => Loadout.Tons(Spec, item.Id) ?? 0)
        + (Spec.ArmorTotal - Definition.ArmorTotal) / (float)(EquipmentRules.ArmorPointsPerTon * CombatRules.DamageScale);
    public bool OverTonnage => Tons > Definition.Tonnage;
    /// <summary>A detached copy with this loadout, for previewing pending hangar changes.</summary>
    public OwnedMech WithLoadout(LoadoutPlan plan)
    {
        var copy = new OwnedMech(Snapshot(), Definition); copy.Change(plan); return copy;
    }
    /// <summary>What changing to this loadout does to storage: +1 for each item taken out (destroyed items are scrapped) and
    /// −1 for each spare newly installed.</summary>
    public IEnumerable<(string Id, int Count)> StorageChange(LoadoutPlan plan)
    {
        var kept = plan.Installed.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var installed = Installed.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in plan.Removed.Except(Removed).Order(StringComparer.Ordinal).Where(id => Condition(id) != ComponentCondition.Destroyed))
            yield return (Loadout.StorageId(Definition, id), 1);
        foreach (var item in Installed.Where(item => !kept.Contains(item.Id) && Condition(item.Id) != ComponentCondition.Destroyed))
            yield return (item.StorageId, 1);
        foreach (var item in plan.Installed.Where(item => !installed.Contains(item.Id))) yield return (item.StorageId, -1);
    }
    /// <summary>Changes to this loadout. Removed stock items stay removed; items already installed keep their place; new items
    /// arrive operational, bins full. Armor added arrives intact; armor taken off comes off what is left, so damage is kept.</summary>
    internal void Change(LoadoutPlan plan)
    {
        if (!Removed.IsSubsetOf(plan.Removed) || plan.Removed.Any(id => !Loadout.Removable(Definition, id)))
            throw new ArgumentException("Only installed weapons, ammunition, heat sinks and jump jets can be removed.");
        var current = Installed.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var next = Loadout.NextId(Installed);
        var added = plan.Installed.Where(item => !current.ContainsKey(item.Id)).ToArray();
        if (plan.Installed.Any(item => current.TryGetValue(item.Id, out var kept) && kept != item)
            || added.Any(item => Loadout.InstallOrder(item.Id) < Loadout.InstallOrder(next) || StorageCatalog.Describe(item.StorageId) is null
                || !Enum.IsDefined(item.Location))
            || plan.Installed.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != plan.Installed.Length)
            throw new ArgumentException("Invalid installed equipment.");
        if (Loadout.ArmorProblem(Definition, plan.Armor) is not null) throw new ArgumentException("Invalid armor.");
        var before = Spec.Locations;
        var gone = Installed.Select(item => item.Id).Except(plan.Installed.Select(item => item.Id)).Concat(plan.Removed).ToHashSet(StringComparer.Ordinal);
        Removed = plan.Removed; Installed = plan.Installed; Armor = plan.Armor; Spec = Loadout.Build(Definition, Removed, Installed, Armor);
        Locations = Array.AsReadOnly(Locations.Select(state =>
        {
            var old = before.Single(item => item.Location == state.Location); var now = Spec.Locations.Single(item => item.Location == state.Location);
            return state with
            {
                Armor = Fitted(state.Armor, old.Armor, now.Armor), RearArmor = Fitted(state.RearArmor, old.RearArmor, now.RearArmor)
            };
        }).ToArray());
        // Added armor arrives intact; armor taken off leaves what remains, at most the new installed amount.
        static int Fitted(int current, int before, int after) => after > before ? current + after - before : Math.Min(current, after);
        Equipment = Array.AsReadOnly(Equipment.Where(item => !gone.Contains(item.Id))
            .Concat(added.Select(item => new EquipmentCondition(item.Id, ComponentCondition.Operational))).ToArray());
        Ammo = Array.AsReadOnly(Ammo.Where(item => !gone.Contains(item.Id))
            .Concat(Spec.Ammo.Where(bin => added.Any(item => item.Id == bin.Id)).Select(bin => new AmmoCondition(bin.Id, bin.Shots))).ToArray());
    }
    /// <summary>Keeps a battle's damage. Every installed bin refills for free afterwards.</summary>
    internal void ApplyBattle(IEnumerable<LocationCondition> locations, IEnumerable<EquipmentCondition> equipment)
    {
        var after = locations.ToArray(); var items = equipment.ToArray();
        if (!after.Select(item => item.Location).Order().SequenceEqual(Locations.Select(item => item.Location).Order())
            || !items.Select(item => item.Id).Order(StringComparer.Ordinal).SequenceEqual(Equipment.Select(item => item.Id).Order(StringComparer.Ordinal)))
            throw new ArgumentException("Battle damage does not match the BattleMech.");
        Locations = Array.AsReadOnly(Locations.Select(state => after.Single(item => item.Location == state.Location)).ToArray());
        Equipment = Array.AsReadOnly(Equipment.Select(state => items.Single(item => item.Id == state.Id)).ToArray());
        Ammo = Array.AsReadOnly(Spec.Ammo.Select(bin => new AmmoCondition(bin.Id, bin.Shots)).ToArray());
    }
    public ComponentCondition Condition(string id) => Equipment.Single(item => item.Id == id).Condition;
    public bool LocationIntact(MechLocation location) => Locations.Single(item => item.Location == location).Structure > 0;
    private bool Functional(ComponentKind kind) => Spec.Components.Where(item => item.Kind == kind)
        .All(item => Condition(item.Id) != ComponentCondition.Destroyed && LocationIntact(item.Location));
    /// <summary>Heat sinks still working: the stock count less grouped sinks destroyed or in a lost location, as in combat.</summary>
    public int EffectiveHeatSinks => Math.Max(0, Spec.HeatSinks - Spec.Components
        .Where(item => item.Kind == ComponentKind.HeatSink && (!LocationIntact(item.Location) || Condition(item.Id) == ComponentCondition.Destroyed))
        .Sum(item => item.HeatSinkCount));
    public bool WeaponUsable(WeaponSpec weapon) => Condition(weapon.Id) != ComponentCondition.Destroyed && LocationIntact(weapon.Location)
        && (weapon.AmmoKind is null || Spec.Ammo.Any(bin => bin.AmmoKind == weapon.AmmoKind && LocationIntact(bin.Location)
            && Condition(bin.Id) != ComponentCondition.Destroyed && Ammo.Single(item => item.Id == bin.Id).Shots > 0));
    public bool Operational => Functional(ComponentKind.Cockpit) && Functional(ComponentKind.FusionEngine) && Functional(ComponentKind.Gyro)
        && Functional(ComponentKind.Hip) && Functional(ComponentKind.UpperLegActuator) && Functional(ComponentKind.LowerLegActuator)
        && LocationIntact(MechLocation.LeftLeg) && LocationIntact(MechLocation.RightLeg) && Spec.Weapons.Any(WeaponUsable);
    public bool Damaged => Locations.Any(item => { var spec = Spec.Locations.Single(loc => loc.Location == item.Location); return item.Armor < spec.Armor || item.RearArmor < spec.RearArmor || item.Structure < spec.Structure; })
        || Equipment.Any(item => item.Condition != ComponentCondition.Operational);
    public string Status => !Operational ? "Not operational" : Damaged ? "Operational · damaged" : "Operational";
    internal MechSnapshot Snapshot() => new(Id, Definition.Id, WarriorId, Locations.ToArray(), Equipment.ToArray(), Ammo.ToArray(),
        Removed.Count == 0 ? null : Removed.Order(StringComparer.Ordinal).ToArray(), Installed.IsEmpty ? null : Installed.ToArray(),
        Armor.IsEmpty ? null : Armor.OrderBy(item => item.Location).ToArray(), Lance);
}

public sealed class RosterWarrior
{
    public WarriorSpec Spec { get; }
    public string Id => Spec.Id;
    public WarriorHealth Health { get; internal set; }
    public DateOnly? RecoveryDate { get; internal set; }
    /// <summary>Settled missions this warrior fought in, won or lost.</summary>
    public int Deployments { get; internal set; }
    /// <summary>Enemy BattleMechs this warrior disabled or destroyed.</summary>
    public int Kills { get; internal set; }
    internal RosterWarrior(WarriorSnapshot save)
    {
        Spec = DraftCatalog.Warrior(save.DefinitionId); Health = save.Health; RecoveryDate = save.RecoveryDate;
        Deployments = Math.Max(0, save.Deployments); Kills = Math.Max(0, save.Kills);
    }
    public bool Ready => Health == WarriorHealth.Healthy;
    public string Status => Health switch { WarriorHealth.Healthy => "Healthy · ready for deployment", WarriorHealth.Injured => $"Injured · recovers {RecoveryDate:dd MMM yyyy}", _ => "In memoriam" };
    internal WarriorSnapshot Snapshot() => new(Id, Health, RecoveryDate, Deployments, Kills);
}

/// <summary>Owned assets and personnel; assignments are independent of any future mission deployment.</summary>
public sealed class ForceRoster
{
    public string ForceName { get; }
    public IReadOnlyList<OwnedMech> Mechs { get; }
    public IReadOnlyList<RosterWarrior> Warriors { get; }
    /// <summary>Spare equipment, stocked once when the company forms (or when an older save first loads).</summary>
    public IReadOnlyList<StorageEntry> Storage => _storage.AsReadOnly();
    private readonly List<StorageEntry> _storage;
    public int ReadyWarriors => Warriors.Count(item => item.Ready);
    public const int LanceSize = 4, MaxLances = 3;
    /// <summary>One lance per player slot, fixed when the company forms.</summary>
    public int LanceCount { get; }
    private ForceRoster(string name, IEnumerable<OwnedMech> mechs, IEnumerable<RosterWarrior> warriors, IEnumerable<StorageEntry>? storage = null, int lances = 1)
    {
        ForceName = name; Mechs = new ReadOnlyCollection<OwnedMech>(mechs.ToArray()); Warriors = new ReadOnlyCollection<RosterWarrior>(warriors.ToArray());
        _storage = (storage ?? StorageCatalog.Stock(Mechs.Select(item => item.Definition))).ToList();
        LanceCount = lances;
    }
    /// <summary>A new company: its mechs fill the lances in order, four to a lance, and any beyond them start in the lance pool.</summary>
    internal static ForceRoster Create(string name, IEnumerable<MechSpec> mechs, IEnumerable<WarriorSpec> warriors, int lances = 1)
    {
        if (lances is < 1 or > MaxLances) throw new ArgumentOutOfRangeException(nameof(lances));
        var units = mechs.Select((spec, i) => new OwnedMech(new($"mech-{i + 1}", spec.Id, null,
            spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)).ToArray(),
            spec.Weapons.Select(item => item.Id).Concat(spec.Components.Select(item => item.Id)).Concat(spec.Ammo.Select(item => item.Id))
                .Select(id => new EquipmentCondition(id, ComponentCondition.Operational)).ToArray(),
            spec.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)).ToArray(), Lance: i < lances * LanceSize ? i / LanceSize : null)));
        return new(name, units, warriors.Select(spec => new RosterWarrior(new(spec.Id, WarriorHealth.Healthy, null))), lances: lances);
    }
    public static string LanceName(int lance) => $"Lance {lance + 1}";
    public IEnumerable<OwnedMech> LanceMechs(int lance) => Mechs.Where(item => item.Lance == lance);
    public IEnumerable<OwnedMech> Pool => Mechs.Where(item => item.Lance is null);
    /// <summary>Whether a player slot may act on a mech: one in the slot's own lance or in the lance pool, never another player's lance.
    /// Its MechWarrior goes with it.</summary>
    public bool Controls(int slot, OwnedMech mech) => mech.Lance is null || mech.Lance == slot;
    /// <summary>Moves a mech into a lance with room, or into the lance pool (null). Its pilot assignment is kept.</summary>
    public void SetLance(string mechId, int? lance)
    {
        var mech = Mechs.SingleOrDefault(item => item.Id == mechId) ?? throw new ArgumentException("Unknown owned BattleMech.");
        if (lance == mech.Lance) return;
        if (lance is { } target && (target < 0 || target >= LanceCount)) throw new ArgumentOutOfRangeException(nameof(lance), "The company has no such lance.");
        if (lance is { } full && LanceMechs(full).Count() >= LanceSize) throw new InvalidOperationException($"A lance holds at most {LanceSize} BattleMechs.");
        mech.Lance = lance;
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
    /// <summary>A detached copy with these mechs' loadouts replaced, for a simulated deployment of pending hangar changes.</summary>
    public ForceRoster WithLoadouts(IReadOnlyDictionary<string, LoadoutPlan> plans) =>
        new(ForceName, Mechs.Select(mech => plans.TryGetValue(mech.Id, out var plan) ? mech.WithLoadout(plan) : new OwnedMech(mech.Snapshot(), mech.Definition)),
            Warriors.Select(warrior => new RosterWarrior(warrior.Snapshot())), _storage, LanceCount);
    /// <summary>Commits a mech's loadout: items taken out join storage, except destroyed ones, which are scrapped, and newly
    /// installed items leave it. Bought items join storage first, so they install like spares. An overweight loadout, or one
    /// needing spares storage lacks, is refused.</summary>
    public void ApplyLoadout(string mechId, LoadoutPlan plan, IEnumerable<string>? bought = null)
    {
        var mech = Mechs.SingleOrDefault(item => item.Id == mechId) ?? throw new ArgumentException("Unknown owned BattleMech.");
        if (bought?.Any(id => StorageCatalog.Describe(id) is null) == true) throw new ArgumentException("Unknown equipment.");
        var changes = mech.StorageChange(plan).Concat((bought ?? []).Select(id => (Id: id, Count: 1))).GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Count), StringComparer.Ordinal);
        if (changes.Any(pair => (_storage.SingleOrDefault(item => item.Id == pair.Key)?.Count ?? 0) + pair.Value < 0))
            throw new ArgumentException("Storage lacks the spares this loadout installs.");
        if (mech.WithLoadout(plan).OverTonnage) throw new ArgumentException("The loadout is over tonnage.");
        mech.Change(plan);
        foreach (var (id, count) in changes)
        {
            var index = _storage.FindIndex(item => item.Id == id);
            if (index >= 0) _storage[index] = _storage[index] with { Count = _storage[index].Count + count };
            else _storage.Add(new(id, count));
        }
        _storage.RemoveAll(item => item.Count == 0);
        _storage.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
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
    public RosterSnapshot Snapshot() => new(ForceName, Mechs.Select(item => item.Snapshot()).ToArray(), Warriors.Select(item => item.Snapshot()).ToArray(), Storage.ToArray(), LanceCount);
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
            || save.Mechs.Length == 0 || save.Warriors.Length == 0 || save.Lances is < 1 or > MaxLances)
            throw new ArgumentException("Invalid roster.");
        // Saves from before lances form one lance from the first four mechs; the rest join the lance pool.
        var lances = save.Lances ?? 1;
        if (save.Lances is null) save = save with { Mechs = save.Mechs.Select((mech, i) => mech is null ? null! : mech with { Lance = i < LanceSize ? 0 : null }).ToArray() };
        if (save.Mechs.Any(mech => mech?.Lance is { } lance && (lance < 0 || lance >= lances))
            || save.Mechs.Where(mech => mech?.Lance is not null).GroupBy(mech => mech.Lance).Any(group => group.Count() > LanceSize))
            throw new ArgumentException("Invalid lance.");
        var warriorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var warrior in save.Warriors)
        {
            if (warrior is null || !warriorIds.Add(warrior.DefinitionId) || !Enum.IsDefined(warrior.Health)
                || (warrior.Health == WarriorHealth.Injured ? warrior.RecoveryDate is null || warrior.RecoveryDate <= date : warrior.RecoveryDate is not null))
                throw new ArgumentException("Invalid MechWarrior state.");
            _ = DraftCatalog.Warrior(warrior.DefinitionId);
        }
        var mechIds = new HashSet<string>(StringComparer.Ordinal); var assigned = new HashSet<string>(StringComparer.Ordinal);
        var mechs = new List<MechSnapshot>();
        foreach (var saved in save.Mechs)
        {
            if (saved is null || string.IsNullOrWhiteSpace(saved.Id) || !mechIds.Add(saved.Id)) throw new ArgumentException("Invalid BattleMech identity.");
            var stock = DraftCatalog.Mech(saved.DefinitionId);
            var mech = Migrate(saved, stock); mechs.Add(mech);
            if (mech.Removed is { } removed && (removed.Length == 0 || removed.Any(id => id is null || !Loadout.Removable(stock, id))
                || removed.Distinct(StringComparer.Ordinal).Count() != removed.Length)) throw new ArgumentException("Invalid loadout.");
            if (mech.Installed is { } installed && (installed.Length == 0 || installed.Any(item => item is null || Loadout.InstallOrder(item.Id) <= 0
                || StorageCatalog.Describe(item.StorageId) is null || !Enum.IsDefined(item.Location)
                || item.Mount is { } mount && (!MountLayout.Classes.Contains(mount) || item.StorageId.StartsWith("weapon:", StringComparison.Ordinal)))
                || installed.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != installed.Length)) throw new ArgumentException("Invalid loadout.");
            if (mech.Armor is { } armor && (armor.Length == 0 || Loadout.ArmorProblem(stock, armor) is not null)) throw new ArgumentException("Invalid loadout.");
            var spec = Loadout.Build(stock, (mech.Removed ?? []).ToHashSet(StringComparer.Ordinal), mech.Installed ?? [], mech.Armor);
            var layout = CriticalLayout.For(spec);
            if ((mech.Installed ?? []).Any(item => CriticalLayout.Used(layout[item.Location]) > EquipmentRules.Capacity(item.Location)))
                throw new ArgumentException("Invalid loadout.");
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
        if (save.Storage is { } storage && (storage.Any(item => item is null || item.Count <= 0 || item.Id is null || StorageCatalog.Describe(item.Id) is null)
            || storage.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != storage.Length))
            throw new ArgumentException("Invalid equipment storage.");
        return new(save.ForceName, mechs.Select(item => new OwnedMech(item)), save.Warriors.Select(item => new RosterWarrior(item)), save.Storage, lances);
    }
    /// <summary>Saves from before heat sinks and jump jets were individual items record one condition per group; each item inherits it.</summary>
    private static MechSnapshot Migrate(MechSnapshot mech, MechSpec stock)
    {
        if (mech.Equipment is null) return mech;
        var ids = stock.Weapons.Select(item => item.Id).Concat(stock.Components.Select(item => item.Id)).Concat(stock.Ammo.Select(item => item.Id)).ToHashSet();
        return mech with
        {
            Equipment = mech.Equipment.SelectMany(item =>
            {
                if (item is null || ids.Contains(item.Id)) return [item!];
                var parts = stock.Components.Where(c => c.Id.StartsWith(item.Id + ".", StringComparison.Ordinal)).ToArray();
                return parts.Length == 0 ? [item] : parts.Select(c => item with { Id = c.Id });
            }).ToArray()
        };
    }
}
