using System.Collections.ObjectModel;
using System.Collections.Immutable;

namespace Mech;

public static class WarriorSkills
{
    /// <summary>Untrained (7) is one step worse than Novice (Green, 6); only Green MechWarriors may have an Untrained skill, and it
    /// gives back one point to spend on another.</summary>
    public const int Expert = 2, Novice = 6, Untrained = 7;
    public const int GreenBudget = 4, RegularBudget = 8, VeteranBudget = 10;
    public static int Cost(int rating) => rating is >= Expert and <= Untrained ? Novice - rating
        : throw new ArgumentOutOfRangeException(nameof(rating), "Pilot skills must be between 2 and 7.");
    /// <summary>The word shown for a skill rating: 7 Untrained, 6 Green through 2 Legendary. Ratings outside 2–7 (test-only units) take the nearest word.</summary>
    public static string Descriptor(int rating) => Math.Clamp(rating, Expert, Untrained) switch
    {
        7 => "Untrained", 6 => "Green", 5 => "Regular", 4 => "Veteran", 3 => "Elite", _ => "Legendary",
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
        new("mara-vale", "Mara Vale", "Kestrel", 4, 4, 4, 4, WarriorExperience.Regular, WarriorSpecialty.None),
        new("ivo-chen", "Ivo Chen", "Needle", 2, 5, 4, 5, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("sana-idris", "Sana Idris", "Drift", 5, 3, 4, 4, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("tomas-reed", "Tomas Reed", "Rook", 4, 4, 2, 6, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("elena-voss", "Elena Voss", "Flint", 2, 4, 5, 5, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("amir-solano", "Amir Solano", "Switch", 4, 2, 6, 4, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("niko-ward", "Niko Ward", "Anchor", 4, 4, 5, 3, WarriorExperience.Regular, WarriorSpecialty.None),
        new("hana-sato", "Hana Sato", "Glint", 2, 5, 3, 6, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("leila-darzi", "Leila Darzi", "Kite", 5, 2, 4, 5, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("renata-kovac", "Renata Kovac", "Iron", 3, 3, 5, 3, WarriorExperience.Veteran, WarriorSpecialty.None),
        new("gideon-price", "Gideon Price", "Crosshair", 2, 3, 4, 5, WarriorExperience.Veteran, WarriorSpecialty.Gunnery),
        new("oskar-lind", "Oskar Lind", "Skid", 3, 2, 3, 6, WarriorExperience.Veteran, WarriorSpecialty.Piloting),
        new("ada-moreno", "Ada Moreno", "Sparrow", 5, 4, 2, 5, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("malik-stone", "Malik Stone", "Hawk", 2, 5, 5, 4, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("yara-ortiz", "Yara Ortiz", "Vector", 4, 2, 4, 6, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("lucas-chen", "Lucas Chen", "Scope", 6, 4, 2, 4, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("imani-brooks", "Imani Brooks", "Viper", 2, 4, 5, 5, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("erik-holm", "Erik Holm", "Dash", 4, 2, 4, 6, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("mei-tanaka", "Mei Tanaka", "Beacon", 4, 6, 2, 4, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("rafiq-nasir", "Rafiq Nasir", "Bolt", 2, 5, 3, 6, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("fiona-kerr", "Fiona Kerr", "Slip", 5, 2, 4, 5, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("vera-holt", "Vera Holt", "Bulwark", 4, 5, 2, 3, WarriorExperience.Veteran, WarriorSpecialty.Sensors),
        new("anton-mercer", "Anton Mercer", "Longshot", 2, 3, 4, 5, WarriorExperience.Veteran, WarriorSpecialty.Gunnery),
        new("zara-okoro", "Zara Okoro", "Dancer", 3, 2, 5, 4, WarriorExperience.Veteran, WarriorSpecialty.Piloting),
        // Entry-level MechWarriors, added for hiring and the Just Starting Out draft; their portraits are still placeholders.
        new("pell-avery", "Pell Avery", "Sprout", 5, 5, 5, 5, WarriorExperience.Green, WarriorSpecialty.None),
        new("juno-marsh", "Juno Marsh", "Tinder", 4, 5, 5, 6, WarriorExperience.Green, WarriorSpecialty.Gunnery),
        new("coby-vance", "Coby Vance", "Skitter", 5, 4, 5, 6, WarriorExperience.Green, WarriorSpecialty.Piloting),
        new("wren-hollis", "Wren Hollis", "Static", 6, 5, 4, 5, WarriorExperience.Green, WarriorSpecialty.Sensors),
        new("dario-fenn", "Dario Fenn", "Chalk", 4, 6, 6, 4, WarriorExperience.Green, WarriorSpecialty.Gunnery),
        new("tess-okafor", "Tess Okafor", "Pebble", 6, 4, 6, 4, WarriorExperience.Green, WarriorSpecialty.Piloting),
        new("bram-kettle", "Bram Kettle", "Shaky", 3, 7, 5, 5, WarriorExperience.Green, WarriorSpecialty.Gunnery),
        new("ines-calder", "Ines Calder", "Wobble", 5, 3, 7, 5, WarriorExperience.Green, WarriorSpecialty.Piloting),
        new("otto-varga", "Otto Varga", "Squint", 7, 5, 3, 5, WarriorExperience.Green, WarriorSpecialty.Sensors),
        new("lina-brask", "Lina Brask", "Jitters", 4, 4, 5, 7, WarriorExperience.Green, WarriorSpecialty.None),
        new("remy-stroud", "Remy Stroud", "Hotshot", 2, 6, 5, 7, WarriorExperience.Green, WarriorSpecialty.Gunnery),
        new("kaia-dunmore", "Kaia Dunmore", "Fumble", 7, 3, 6, 4, WarriorExperience.Green, WarriorSpecialty.Piloting),
        new("sofia-brandt", "Sofia Brandt", "Ledger", 4, 4, 4, 4, WarriorExperience.Regular, WarriorSpecialty.None),
        new("marcus-thorne", "Marcus Thorne", "Tack", 3, 4, 5, 4, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("priya-nandakumar", "Priya Nandakumar", "Swerve", 5, 3, 4, 4, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("joel-barrett", "Joel Barrett", "Lantern", 5, 5, 2, 4, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("noor-haddad", "Noor Haddad", "Spindle", 3, 4, 4, 5, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("ezra-cole", "Ezra Cole", "Gravel", 4, 4, 5, 3, WarriorExperience.Regular, WarriorSpecialty.None),
        new("ana-lucero", "Ana Lucero", "Glide", 4, 2, 5, 5, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("callum-reyes", "Callum Reyes", "Radar", 5, 4, 2, 5, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("hiro-matsuda", "Hiro Matsuda", "Pinpoint", 2, 5, 5, 4, WarriorExperience.Regular, WarriorSpecialty.Gunnery),
        new("greta-sorensen", "Greta Sorensen", "Trail", 5, 3, 5, 3, WarriorExperience.Regular, WarriorSpecialty.Piloting),
        new("obi-mensah", "Obi Mensah", "Halo", 4, 5, 3, 4, WarriorExperience.Regular, WarriorSpecialty.Sensors),
        new("dmitri-volkov", "Dmitri Volkov", "Granite", 4, 5, 5, 2, WarriorExperience.Regular, WarriorSpecialty.None)
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

/// <summary>How a new company begins: the full established draft, or two light mechs and two junior MechWarriors on little money.</summary>
public enum StartingScenario { Established, StartingOut }

public static class Scenarios
{
    public static readonly StartingScenario[] All = [StartingScenario.StartingOut, StartingScenario.Established];
    public static string Name(StartingScenario scenario) => scenario == StartingScenario.StartingOut ? "Just starting out" : "Established";
    public static string Description(StartingScenario scenario) => scenario == StartingScenario.StartingOut
        ? "Two light BattleMechs, a Regular and a Green MechWarrior, and ₡ 500,000."
        : "One BattleMech of each weight class, three Regular MechWarriors and a Veteran, and ₡ 2,500,000.";
    public static long StartingCash(StartingScenario scenario) => scenario == StartingScenario.StartingOut ? 500_000 : Campaign.StartingCash;
    /// <summary>The weight class of each mech pool, in column order.</summary>
    public static MechWeightClass[] MechPools(StartingScenario scenario) => scenario == StartingScenario.StartingOut
        ? [MechWeightClass.Light, MechWeightClass.Light] : [MechWeightClass.Light, MechWeightClass.Medium, MechWeightClass.Heavy, MechWeightClass.Assault];
    /// <summary>The rank of each MechWarrior pool, in column order; the warrior in a column pilots that column's mech.</summary>
    public static WarriorExperience[] WarriorPools(StartingScenario scenario) => scenario == StartingScenario.StartingOut
        ? [WarriorExperience.Regular, WarriorExperience.Green]
        : [WarriorExperience.Regular, WarriorExperience.Regular, WarriorExperience.Regular, WarriorExperience.Veteran];
}

/// <summary>A draft as sent to the player who makes it: candidate ids pool by pool (active pools only), the current choices, whether
/// this draft names the company, and the scenario it drafts for.</summary>
public sealed record DraftOffer(string[] Mechs, string[] Warriors, int[] Choices, string ForceName, bool NamesCompany,
    StartingScenario Scenario = StartingScenario.Established);

/// <summary>A reversible selection; nothing reaches the campaign until every pool and its name are confirmed. Pools 0–3 hold mechs and
/// 4–7 MechWarriors; a scenario with fewer columns leaves the rest of each row unused.</summary>
public sealed class InitialDraft
{
    private const int Candidates = 3, Pools = 8;
    private static readonly string[] NamePrefixes = ["Iron", "Ashen", "Crimson", "Silver", "Steel", "Ember", "Storm", "Obsidian"];
    private static readonly string[] NameSuffixes = ["Jackals", "Lancers", "Falcons", "Wolves", "Sentinels", "Dragoons", "Marauders", "Wardens"];
    private readonly int[] _choices = Enumerable.Repeat(-1, Pools).ToArray();
    private readonly MechSpec?[,] _mechs = new MechSpec?[4, Candidates];
    private readonly WarriorSpec?[,] _warriors = new WarriorSpec?[4, Candidates];

    public InitialDraft(StartingScenario scenario = StartingScenario.Established)
    {
        Scenario = scenario;
        // Fixed placeholder candidates, in catalog order, each MechWarrior offered once.
        var mechs = Scenarios.MechPools(scenario).Distinct().ToDictionary(weight => weight,
            weight => new Queue<MechSpec>(scenario == StartingScenario.Established ? DraftCatalog.Mechs.Where(m => m.WeightClass == weight) : DraftCatalog.AllMechs.Where(m => m.WeightClass == weight)));
        var warriors = Scenarios.WarriorPools(scenario).Distinct().ToDictionary(rank => rank, rank => new Queue<WarriorSpec>(DraftCatalog.Warriors.Where(w => w.Rank == rank)));
        for (var p = 0; p < Columns; p++) for (var c = 0; c < Candidates; c++)
        { _mechs[p, c] = mechs[MechClass(p)].Dequeue(); _warriors[p, c] = warriors[WarriorRank(p + 4)].Dequeue(); }
    }
    public StartingScenario Scenario { get; }
    /// <summary>Columns of the draft: one mech pool over one MechWarrior pool each.</summary>
    public int Columns => Scenarios.MechPools(Scenario).Length;
    public bool Active(int pool) => pool is >= 0 and < Pools && pool % 4 < Columns;
    public IEnumerable<int> ActivePools => Enumerable.Range(0, Pools).Where(Active);
    public MechWeightClass MechClass(int pool) => Scenarios.MechPools(Scenario)[pool];
    public WarriorExperience WarriorRank(int pool) => Scenarios.WarriorPools(Scenario)[pool - 4];
    public string PoolName(int pool) => Scenario == StartingScenario.Established ? DraftCatalog.PoolNames[pool]
        : pool < 4 ? $"{MechClass(pool)} · {(pool == 0 ? "I" : "II")}" : $"MechWarriors · {WarriorRank(pool)}";
    public MechSpec MechCandidate(int pool, int choice) => _mechs[pool, choice] ?? throw new ArgumentOutOfRangeException(nameof(pool), "That pool is not in this draft.");
    public WarriorSpec WarriorCandidate(int pool, int choice) => _warriors[pool - 4, choice] ?? throw new ArgumentOutOfRangeException(nameof(pool), "That pool is not in this draft.");
    private static T[] Shuffle<T>(IEnumerable<T> source, Random random)
    {
        var values = source.ToArray();
        for (var i = values.Length - 1; i > 0; i--) { var j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
        return values;
    }

    private static int Needed(StartingScenario scenario, WarriorExperience rank) => Scenarios.WarriorPools(scenario).Count(item => item == rank) * Candidates;
    private static int Available(WarriorExperience rank) => DraftCatalog.Warriors.Count(w => w.Rank == rank);
    /// <summary>The most players whose drafts can all offer different MechWarriors: each draft needs three candidates of its own per
    /// MechWarrior pool.</summary>
    public static int MaxPlayers(StartingScenario scenario = StartingScenario.Established) => Math.Min(ForceRoster.MaxLances,
        Scenarios.WarriorPools(scenario).Distinct().Min(rank => Available(rank) / Needed(scenario, rank)));
    /// <summary>Why a company of this many players cannot be drafted, or null when it can.</summary>
    public static string? CompanyProblem(int players, StartingScenario scenario = StartingScenario.Established)
    {
        if (players is < 1 or > ForceRoster.MaxLances) return "A company has one to three players.";
        if (players <= MaxPlayers(scenario)) return null;
        var ranks = Scenarios.WarriorPools(scenario).Distinct().ToArray();
        return $"{players} players need {string.Join(" and ", ranks.Select(rank => $"{players * Needed(scenario, rank)} {rank.ToString().ToLowerInvariant()}"))} MechWarriors in the catalog; it has "
            + $"{string.Join(" and ", ranks.Select(Available))}.";
    }

    /// <summary>One draft per player slot. Mech candidates are drawn for each player separately; MechWarrior candidates never repeat
    /// between players, so every lance ends with MechWarriors of its own. Only the leader's draft names the company.</summary>
    public static IReadOnlyList<InitialDraft> CreateCompany(int players, Random? random = null, StartingScenario scenario = StartingScenario.Established)
    {
        if (CompanyProblem(players, scenario) is { } problem) throw new ArgumentOutOfRangeException(nameof(players), problem);
        random ??= Random.Shared;
        var queues = Scenarios.WarriorPools(scenario).Distinct().ToDictionary(rank => rank,
            rank => new Queue<WarriorSpec>(Shuffle(DraftCatalog.Warriors.Where(w => w.Rank == rank), random)));
        return Enumerable.Range(0, players).Select(slot =>
        {
            var draft = CreateRandom(random, scenario);
            for (var p = 0; p < draft.Columns; p++) for (var c = 0; c < Candidates; c++) draft._warriors[p, c] = queues[draft.WarriorRank(p + 4)].Dequeue();
            if (slot > 0) { draft.NamesCompany = false; draft.ForceName = ""; }
            return draft;
        }).ToArray();
    }

    /// <summary>Whether this draft names the company: the leader's does; the other players' drafts only choose their lances.</summary>
    public bool NamesCompany { get; private set; } = true;
    public DraftOffer Offer() => new(Enumerable.Range(0, Columns).SelectMany(p => Enumerable.Range(0, Candidates).Select(c => MechCandidate(p, c).Id)).ToArray(),
        Enumerable.Range(0, Columns).SelectMany(p => Enumerable.Range(0, Candidates).Select(c => WarriorCandidate(p + 4, c).Id)).ToArray(), [.. _choices], ForceName, NamesCompany, Scenario);
    public static InitialDraft FromOffer(DraftOffer offer)
    {
        if (offer is null || !Enum.IsDefined(offer.Scenario)) throw new ArgumentException("Invalid draft.");
        var draft = new InitialDraft(offer.Scenario) { NamesCompany = offer.NamesCompany, ForceName = offer.ForceName ?? "" };
        var columns = draft.Columns;
        if (offer.Mechs is null || offer.Mechs.Length != columns * Candidates || offer.Warriors is null || offer.Warriors.Length != columns * Candidates
            || offer.Choices is not { Length: Pools }) throw new ArgumentException("Invalid draft.");
        for (var p = 0; p < columns; p++) for (var c = 0; c < Candidates; c++)
        {
            draft._mechs[p, c] = DraftCatalog.Mech(offer.Mechs[p * Candidates + c]);
            draft._warriors[p, c] = DraftCatalog.Warrior(offer.Warriors[p * Candidates + c]);
            if (draft._mechs[p, c]!.WeightClass != draft.MechClass(p) || draft._warriors[p, c]!.Rank != draft.WarriorRank(p + 4))
                throw new ArgumentException("Invalid draft.");
        }
        foreach (var pool in draft.ActivePools) if (offer.Choices[pool] >= 0) draft.Select(pool, offer.Choices[pool]);
        return draft;
    }
    /// <summary>Takes a player's confirmed choices, and the company name if this draft names it. Pools outside the draft are ignored.</summary>
    public void Accept(int[] choices, string? forceName)
    {
        if (choices is not { Length: Pools } || ActivePools.Any(pool => choices[pool] is < 0 or >= Candidates)) throw new ArgumentException("Select one candidate from each pool.");
        foreach (var pool in ActivePools) Select(pool, choices[pool]);
        if (NamesCompany) ForceName = forceName ?? "";
        if (!Complete) throw new ArgumentException("Name the company and select one candidate from each pool.");
    }

    private IEnumerable<MechSpec> ChosenMechs => Enumerable.Range(0, Columns).Select(pool => MechCandidate(pool, _choices[pool]));
    private IEnumerable<WarriorSpec> ChosenWarriors => Enumerable.Range(4, Columns).Select(pool => WarriorCandidate(pool, _choices[pool]));

    /// <summary>Forms a company of one lance per draft, in slot order, each mech piloted by the MechWarrior drafted in its column.</summary>
    public static ForceRoster EstablishCompany(IReadOnlyList<InitialDraft> drafts)
    {
        if (drafts.Count is < 1 or > ForceRoster.MaxLances || drafts.Any(draft => !draft.Complete) || !drafts[0].NamesCompany
            || drafts.Any(draft => draft.Scenario != drafts[0].Scenario))
            throw new InvalidOperationException("Every player must complete their draft, and the leader must name the company.");
        var mechs = drafts.SelectMany(draft => draft.ChosenMechs).ToArray();
        var warriors = drafts.SelectMany(draft => draft.ChosenWarriors).ToArray();
        if (warriors.Select(item => item.Id).Distinct().Count() != warriors.Length) throw new InvalidOperationException("Two lances drafted the same MechWarrior.");
        var lances = drafts.SelectMany((draft, slot) => Enumerable.Repeat<int?>(slot, draft.Columns)).ToArray();
        var roster = ForceRoster.Create(drafts[0].ForceName.Trim(), mechs, warriors, drafts.Count, lances);
        for (var i = 0; i < mechs.Length; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        return roster;
    }

    public static InitialDraft CreateRandom(Random? random = null, StartingScenario scenario = StartingScenario.Established)
    {
        random ??= Random.Shared;
        var draft = new InitialDraft(scenario)
        {
            ForceName = $"{NamePrefixes[random.Next(NamePrefixes.Length)]} {NameSuffixes[random.Next(NameSuffixes.Length)]}"
        };
        // Pools of the same weight class share one shuffle, so their candidates are all different.
        var mechs = Scenarios.MechPools(scenario).Distinct().ToDictionary(weight => weight,
            weight => new Queue<MechSpec>(Shuffle(DraftCatalog.AllMechs.Where(m => m.WeightClass == weight), random)));
        for (var p = 0; p < draft.Columns; p++) for (var c = 0; c < Candidates; c++) draft._mechs[p, c] = mechs[draft.MechClass(p)].Dequeue();
        var warriors = Scenarios.WarriorPools(scenario).Distinct().ToDictionary(rank => rank,
            rank => new Queue<WarriorSpec>(Shuffle(DraftCatalog.Warriors.Where(w => w.Rank == rank), random)));
        for (var p = 0; p < draft.Columns; p++) for (var c = 0; c < Candidates; c++) draft._warriors[p, c] = warriors[draft.WarriorRank(p + 4)].Dequeue();
        foreach (var pool in draft.ActivePools) draft.Select(pool, random.Next(Candidates));
        return draft;
    }

    public string ForceName { get; set; } = "";
    public int SelectedCount => ActivePools.Count(pool => _choices[pool] >= 0);
    public int PoolCount => ActivePools.Count();
    public bool Complete => (!NamesCompany || ValidName(ForceName)) && SelectedCount == PoolCount;
    public int Choice(int pool) => _choices[pool];
    public void Select(int pool, int choice)
    {
        if (!Active(pool) || choice is < 0 or >= Candidates) throw new ArgumentOutOfRangeException(nameof(choice));
        _choices[pool] = choice;
    }
    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 40
        && !name.Any(char.IsControl);
    public ForceRoster Establish()
    {
        if (!Complete) throw new InvalidOperationException("Name the company and select one candidate from each pool.");
        var roster = ForceRoster.Create(ForceName.Trim(), ChosenMechs, ChosenWarriors);
        for (var column = 0; column < Columns; column++)
            roster.Assign(roster.Mechs[column].Id, roster.Warriors[column].Id);
        return roster;
    }
}

public sealed record LocationCondition(MechLocation Location, int Armor, int RearArmor, int Structure);
/// <summary>Hits counts damage levels taken by a damaged item; null for working or destroyed items, and for saves from before hits
/// were counted (one hit).</summary>
public sealed record EquipmentCondition(string Id, ComponentCondition Condition, int? Hits = null);
public sealed record AmmoCondition(string Id, int Shots);
/// <summary>Removed lists the stock items taken out in the hangar, Installed the storage items put in, in installation order, and
/// Armor the locations whose installed armor differs from stock; each is null when empty.</summary>
/// <summary>Lance is the zero-based lance the mech belongs to, or null in the lance pool. Repair is its repair order, if any.</summary>
public sealed record MechSnapshot(string Id, string DefinitionId, string? WarriorId, LocationCondition[] Locations,
    EquipmentCondition[] Equipment, AmmoCondition[] Ammo, string[]? Removed = null, InstalledItem[]? Installed = null, ArmorFit[]? Armor = null,
    int? Lance = null, RepairOrder? Repair = null);
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
    /// <summary>The mech's repair order, queued or in a repair bay; the mech cannot deploy or change its loadout meanwhile.</summary>
    public RepairOrder? Repair { get; internal set; }
    public bool UnderRepair => Repair?.Started is not null;
    internal OwnedMech(MechSnapshot save) : this(save, DraftCatalog.Mech(save.DefinitionId)) { }
    internal OwnedMech(MechSnapshot save, MechSpec definition)
    {
        if (definition.Id != save.DefinitionId) throw new ArgumentException("Definition identity does not match owned state.");
        Id = save.Id; Definition = definition; WarriorId = save.WarriorId; Lance = save.Lance; Repair = save.Repair;
        Removed = (save.Removed ?? []).ToImmutableHashSet(StringComparer.Ordinal); Installed = [.. save.Installed ?? []]; Armor = [.. save.Armor ?? []];
        Spec = Loadout.Build(definition, Removed, Installed, Armor);
        Locations = Array.AsReadOnly(save.Locations.ToArray()); Equipment = Array.AsReadOnly(save.Equipment.ToArray()); Ammo = Array.AsReadOnly(save.Ammo.ToArray());
    }
    /// <summary>Rated tonnage less the mass of removed items plus that of installed ones and of armor added (16 stock points a ton);
    /// stock designs are taken to be exactly at their rating. Heat sinks count as a whole: a ton for each beyond the free ten, so
    /// removing or adding one changes the mass only above ten.</summary>
    public float Tons => Definition.Tonnage - Removed.Where(id => !HeatSink(Definition, id)).Sum(id => Loadout.Tons(Definition, id) ?? 0)
        + Installed.Where(item => !HeatSink(Spec, item.Id)).Sum(item => Loadout.Tons(Spec, item.Id) ?? 0)
        + EquipmentRules.HeatSinkTons(Spec) - EquipmentRules.HeatSinkTons(Definition)
        + (Spec.ArmorTotal - Definition.ArmorTotal) / (float)(EquipmentRules.ArmorPointsPerTon * CombatRules.DamageScale);
    private static bool HeatSink(MechSpec spec, string id) => spec.Components.Any(item => item.Id == id && item.Kind == ComponentKind.HeatSink);
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
    /// <summary>Completes a repair: armor (on intact locations, for an armor repair) returns to the installed amount; a full
    /// repair also rebuilds structure, returns every component, weapon and bin to working order and refills the bins.</summary>
    internal void Repaired(RepairScope scope, IReadOnlyCollection<string>? missing = null)
    {
        var all = scope == RepairScope.All;
        Locations = Array.AsReadOnly(Locations.Select(state =>
        {
            if (!all && state.Structure <= 0) return state;
            var full = Spec.Locations.Single(item => item.Location == state.Location);
            return state with { Armor = full.Armor, RearArmor = full.RearArmor, Structure = all ? full.Structure : state.Structure };
        }).ToArray());
        if (!all) return;
        // Destroyed items with no spare stay as they were.
        Equipment = Array.AsReadOnly(Equipment.Select(item => missing?.Contains(item.Id) == true ? item
            : item with { Condition = ComponentCondition.Operational, Hits = null }).ToArray());
        Ammo = Array.AsReadOnly(Spec.Ammo.Select(bin => new AmmoCondition(bin.Id, bin.Shots)).ToArray());
    }
    public ComponentCondition Condition(string id) => Equipment.Single(item => item.Id == id).Condition;
    /// <summary>"Damaged", "Critically damaged" and the like; with <paramref name="levels"/> a critical system's level count follows.</summary>
    public string ConditionText(string id, bool levels = false) => CriticalText.Condition(Spec, Equipment.Single(item => item.Id == id), levels);
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
    // A struck cockpit kills its MechWarrior, so the mech stays out of action until it is repaired.
    public bool Operational => Functional(ComponentKind.Cockpit)
        && Spec.Components.Where(item => item.Kind == ComponentKind.Cockpit).All(item => Condition(item.Id) == ComponentCondition.Operational)
        && Functional(ComponentKind.FusionEngine) && Functional(ComponentKind.Gyro)
        && Functional(ComponentKind.Hip) && Functional(ComponentKind.UpperLegActuator) && Functional(ComponentKind.LowerLegActuator)
        && LocationIntact(MechLocation.LeftLeg) && LocationIntact(MechLocation.RightLeg) && Spec.Weapons.Any(WeaponUsable);
    private LocationSpec LocationSpec(MechLocation location) => Spec.Locations.Single(loc => loc.Location == location);
    /// <summary>Front or rear armor below the installed amount somewhere.</summary>
    public bool ArmorDamaged => Locations.Any(item => item.Armor < LocationSpec(item.Location).Armor || item.RearArmor < LocationSpec(item.Location).RearArmor);
    /// <summary>Internal structure below stock somewhere.</summary>
    public bool InternalsDamaged => Locations.Any(item => item.Structure < LocationSpec(item.Location).Structure);
    /// <summary>A component, weapon or ammunition bin damaged or destroyed. Spent ammunition is not damage.</summary>
    public bool SystemsDamaged => Equipment.Any(item => item.Condition != ComponentCondition.Operational);
    public bool Damaged => ArmorDamaged || InternalsDamaged || SystemsDamaged;
    public string Status => !Operational ? "Not operational" : Damaged ? "Operational · damaged" : "Operational";
    internal MechSnapshot Snapshot() => new(Id, Definition.Id, WarriorId, Locations.ToArray(), Equipment.ToArray(), Ammo.ToArray(),
        Removed.Count == 0 ? null : Removed.Order(StringComparer.Ordinal).ToArray(), Installed.IsEmpty ? null : Installed.ToArray(),
        Armor.IsEmpty ? null : Armor.OrderBy(item => item.Location).ToArray(), Lance, Repair);
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
    private readonly List<OwnedMech> _mechs;
    public IReadOnlyList<RosterWarrior> Warriors => _warriors.AsReadOnly();
    private readonly List<RosterWarrior> _warriors;
    /// <summary>Spare equipment, stocked once when the company forms (or when an older save first loads).</summary>
    public IReadOnlyList<StorageEntry> Storage => _storage.AsReadOnly();
    private readonly List<StorageEntry> _storage;
    public int ReadyWarriors => Warriors.Count(item => item.Ready);
    public const int LanceSize = 4, MaxLances = 3;
    /// <summary>One lance per player slot, fixed when the company forms.</summary>
    public int LanceCount { get; }
    private ForceRoster(string name, IEnumerable<OwnedMech> mechs, IEnumerable<RosterWarrior> warriors, IEnumerable<StorageEntry>? storage = null, int lances = 1)
    {
        ForceName = name; _mechs = mechs.ToList(); Mechs = _mechs.AsReadOnly(); _warriors = warriors.ToList();
        _storage = (storage ?? StorageCatalog.Stock(Mechs.Select(item => item.Definition))).ToList();
        LanceCount = lances;
    }
    /// <summary>A new company: its mechs fill the lances in order, four to a lance, and any beyond them start in the lance pool;
    /// <paramref name="lanceOf"/>, when given, names each mech's lance instead.</summary>
    internal static ForceRoster Create(string name, IEnumerable<MechSpec> mechs, IEnumerable<WarriorSpec> warriors, int lances = 1, int?[]? lanceOf = null)
    {
        if (lances is < 1 or > MaxLances) throw new ArgumentOutOfRangeException(nameof(lances));
        if (lanceOf?.Any(lance => lance is { } value && (value < 0 || value >= lances)) == true
            || lanceOf?.Where(lance => lance is not null).GroupBy(lance => lance).Any(group => group.Count() > LanceSize) == true)
            throw new ArgumentOutOfRangeException(nameof(lanceOf));
        var units = mechs.Select((spec, i) => new OwnedMech(new($"mech-{i + 1}", spec.Id, null,
            spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)).ToArray(),
            spec.Weapons.Select(item => item.Id).Concat(spec.Components.Select(item => item.Id)).Concat(spec.Ammo.Select(item => item.Id))
                .Select(id => new EquipmentCondition(id, ComponentCondition.Operational)).ToArray(),
            spec.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)).ToArray(),
            Lance: lanceOf is not null ? (i < lanceOf.Length ? lanceOf[i] : null) : i < lances * LanceSize ? i / LanceSize : null)));
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
    /// <summary>A hired MechWarrior joins the company healthy and unassigned.</summary>
    internal RosterWarrior Hire(WarriorSpec spec)
    {
        if (Warriors.Any(item => item.Id == spec.Id)) throw new InvalidOperationException("That MechWarrior is already in the company.");
        var warrior = new RosterWarrior(new(spec.Id, WarriorHealth.Healthy, null));
        _warriors.Add(warrior);
        return warrior;
    }
    /// <summary>A dismissed MechWarrior leaves the company, and their mech, for good. The deceased stay in memoriam.</summary>
    internal void Dismiss(string warriorId)
    {
        var warrior = Warriors.SingleOrDefault(item => item.Id == warriorId) ?? throw new ArgumentException("Unknown owned MechWarrior.");
        if (warrior.Health == WarriorHealth.Dead) throw new InvalidOperationException("A deceased MechWarrior cannot be dismissed.");
        if (Assignment(warriorId) is { } mech) mech.WarriorId = null;
        _warriors.Remove(warrior);
    }
    /// <summary>The company's quarterly salaries: every living MechWarrior, injured or not.</summary>
    public long Salaries => Warriors.Where(item => item.Health != WarriorHealth.Dead).Sum(item => item.Spec.Salary);
    public int SalariedWarriors => Warriors.Count(item => item.Health != WarriorHealth.Dead);
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
        foreach (var (id, count) in changes) Store(id, count);
    }
    /// <summary>Adds spares to storage, or takes them out with a negative count; storage never goes below zero.</summary>
    internal void Store(string storageId, int count)
    {
        if (StorageCatalog.Describe(storageId) is null) throw new ArgumentException("Unknown equipment.");
        var index = _storage.FindIndex(item => item.Id == storageId);
        if ((index >= 0 ? _storage[index].Count : 0) + count < 0) throw new InvalidOperationException("Storage lacks those spares.");
        if (index >= 0) _storage[index] = _storage[index] with { Count = _storage[index].Count + count };
        else _storage.Add(new(storageId, count));
        _storage.RemoveAll(item => item.Count == 0);
        _storage.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }
    /// <summary>A stock mech taken as salvage joins the lance pool with no pilot, in the condition it was taken, its bins full.
    /// It takes the next free mech id.</summary>
    internal OwnedMech AddSalvagedMech(MechSpec stock, LocationCondition[] locations, EquipmentCondition[] equipment)
    {
        var number = _mechs.Count + 1;
        while (_mechs.Any(item => item.Id == $"mech-{number}")) number++;
        var mech = new OwnedMech(new($"mech-{number}", stock.Id, null,
            stock.Locations.Select(loc => locations.Single(item => item.Location == loc.Location)).ToArray(),
            stock.Weapons.Select(item => item.Id).Concat(stock.Components.Select(item => item.Id)).Concat(stock.Ammo.Select(item => item.Id))
                .Select(id => equipment.Single(item => item.Id == id)).ToArray(),
            stock.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)).ToArray()), stock);
        _mechs.Add(mech);
        return mech;
    }
    public int Stored(string storageId) => _storage.SingleOrDefault(item => item.Id == storageId)?.Count ?? 0;
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
            || save.Mechs.Length == 0 || save.Lances is < 1 or > MaxLances)
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
            if (mech.Equipment is null || mech.Equipment.Length != equipmentIds.Count || mech.Equipment.Any(item => item is null || !Enum.IsDefined(item.Condition)
                || item.Hits is { } hits && (item.Condition != ComponentCondition.Damaged || hits < 1))
                || !equipmentIds.SetEquals(mech.Equipment.Select(item => item.Id)) || mech.Equipment.Select(item => item.Id).Distinct().Count() != equipmentIds.Count)
                throw new ArgumentException("Invalid component condition.");
            if (mech.Ammo is null || mech.Ammo.Any(item => item is null) || mech.Ammo.Length != spec.Ammo.Length
                || !mech.Ammo.Select(item => item.Id).ToHashSet().SetEquals(spec.Ammo.Select(item => item.Id))
                || mech.Ammo.Select(item => item.Id).Distinct().Count() != spec.Ammo.Length) throw new ArgumentException("Invalid ammunition bins.");
            foreach (var bin in mech.Ammo)
                if (bin.Shots < 0 || bin.Shots > spec.Ammo.Single(item => item.Id == bin.Id).Shots) throw new ArgumentException("Invalid ammunition quantity.");
            if (mech.Repair is { } order && (!Enum.IsDefined(order.Scope) || order.Days < 1 || order.Cost < 0 || order.Sequence < 0
                || (order.Refit ? order.Plan is null || order.Cost != 0 || order.Missing is not null || !ValidRefit(mech, order.Plan) : order.Plan is not null)
                || order.Started is { } started && (started > date || order.Completes <= date)
                || order.Parts?.Any(id => id is null || StorageCatalog.Describe(id) is null) == true
                || order.Missing?.Any(id => id is null || !equipmentIds.Contains(id)) == true))
                throw new ArgumentException("Invalid repair order.");
        }
        var orders = mechs.Select(item => item.Repair).OfType<RepairOrder>().ToArray();
        if (orders.Count(item => item.Started is not null) > RepairTuning.Bays || orders.Select(item => item.Sequence).Distinct().Count() != orders.Length)
            throw new ArgumentException("Invalid repair order.");
        if (save.Storage is { } storage && (storage.Any(item => item is null || item.Count <= 0 || item.Id is null || StorageCatalog.Describe(item.Id) is null)
            || storage.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != storage.Length))
            throw new ArgumentException("Invalid equipment storage.");
        return new(save.ForceName, mechs.Select(item => new OwnedMech(item)), save.Warriors.Select(item => new RosterWarrior(item)), save.Storage, lances);
    }
    /// <summary>A saved refit's plan must be a loadout the mech can change to.</summary>
    private static bool ValidRefit(MechSnapshot mech, LoadoutPlan plan)
    {
        try { return !new OwnedMech(mech).WithLoadout(plan).OverTonnage; }
        catch (ArgumentException) { return false; }
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
