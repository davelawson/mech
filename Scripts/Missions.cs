using System.Text.Json.Serialization;

namespace Mech;

public enum EnemyPilotRank { Green, Regular, Veteran, Elite }

/// <summary>One BattleMech of a mission's opposition and the rank of its unnamed pilot.</summary>
public sealed record EnemyMech(string MechId, EnemyPilotRank Rank);

public enum PartyKind { Faction, Pirates, LocalForces }

/// <summary>A side in a contract: a tracked faction, a pirate band, or local forces. Only tracked factions hold a reputation.</summary>
public sealed record Party(PartyKind Kind, Faction? Faction, string Name)
{
    public static Party Of(Faction faction) => new(PartyKind.Faction, faction, Factions.Name(faction));
    public static Party Pirates { get; } = new(PartyKind.Pirates, null, "Pirates");
    [JsonIgnore] public string ShortName => Faction is { } faction ? Factions.Spec(faction).ShortName : Name;
    /// <summary>How a briefing refers to the party mid-sentence.</summary>
    [JsonIgnore] public string Description => Kind == PartyKind.Pirates ? "a pirate band" : Name;
    [JsonIgnore] public bool Valid => !string.IsNullOrWhiteSpace(Name) && Enum.IsDefined(Kind) && (Kind == PartyKind.Faction) == (Faction is not null)
        && (Faction is not { } faction || Enum.IsDefined(faction) && Name == Factions.Name(faction)) && (Kind != PartyKind.Pirates || Name == "Pirates");
}

/// <summary>Generated briefing terms. The opposition is exactly the force that will be fielded; rewards are fixed.
/// On a raid the employer attacks the opponent; otherwise the force defends the location for the employer.</summary>
public sealed record MissionOffer(string Id, string Name, string Briefing, string Objective, string SystemId, DateOnly Posted,
    EnemyMech[] Opposition, long Payment, int SalvagePicks, long InsuranceLimit, Party Employer, Party Opponent, bool Raid);

/// <summary>Numbers for mission generation, recorded in docs/design.md.</summary>
public static class MissionTuning
{
    public const int BoardSize = 10;
    public const double DailyRemovalChance = .5;
    public const int LocationJumps = 3;
    public const int DeadlineSlackDays = 14;
    public const int MinOpposition = 2, MaxOpposition = 4;
    // Odds by MechWeightClass and EnemyPilotRank order.
    public static readonly int[] WeightOdds = [40, 35, 18, 7];
    public static readonly int[] RankOdds = [20, 50, 22, 8];
    public static readonly double[] RankMultiplier = [.8, 1, 1.25, 1.5];
    /// <summary>Gunnery, piloting and sensors of an enemy pilot, by EnemyPilotRank order.</summary>
    public static readonly int[] EnemySkill = [5, 4, 3, 2];
    public const long CBillsPerDifficulty = 4_000;
    public const double RewardVariation = .2;
    public const double MinPaymentShare = .4;
    public const long CBillsPerSalvagePick = 100_000;
    public const int MaxSalvagePicks = 6;
    public const long RewardRounding = 1_000;
    public const string RaidObjective = "Defeat the defending force.";
    public const string DefenseObjective = "Defeat the attacking force.";
    // Employer odds: Great House, ComStar, pirates.
    public static readonly int[] EmployerOdds = [75, 10, 15];
    // Opponent odds for a Great House employer: another House, local forces, pirates, ComStar.
    public static readonly int[] HouseOpponentOdds = [50, 20, 20, 10];
    // Opponent odds for ComStar: pirates, local forces, a Great House.
    public static readonly int[] ComStarOpponentOdds = [40, 30, 30];
    // Opponent odds for pirates: local forces, the system's Great House.
    public static readonly int[] PirateOpponentOdds = [60, 40];
    public const double RaidChance = .5;
    /// <summary>Two Houses border each other at a location when the other holds a system this many jumps from it.</summary>
    public const int BorderJumps = 2;

    /// <summary>The unnamed pilot of the numbered enemy mech, skilled by rank.</summary>
    public static WarriorSpec EnemyPilot(EnemyPilotRank rank, int number)
    {
        var skill = EnemySkill[(int)rank];
        return new($"enemy-pilot-{number}", $"{rank} pilot", $"{rank} {number}", skill, skill, skill, WarriorExperience.Regular, WarriorSpecialty.None);
    }
}

/// <summary>SplitMix64: its whole state is one number, so a save reproduces every later roll.</summary>
public sealed class MissionRandom(ulong state)
{
    public ulong State { get; private set; } = state;
    public ulong NextULong()
    {
        var z = State += 0x9E3779B97F4A7C15;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
    public int Next(int max) => (int)(NextDouble() * max);
    public int Pick(IReadOnlyList<int> odds)
    {
        var roll = Next(odds.Sum());
        for (var i = 0; i < odds.Count; i++) { if (roll < odds[i]) return i; roll -= odds[i]; }
        return odds.Count - 1;
    }
}

public static class MissionGenerator
{
    private static readonly string[] RaidKinds = ["Raid on", "Strike at", "Assault on", "Sweep of", "Ambush at", "Recon in force at"];
    private static readonly string[] DefenseKinds = ["Defense of", "Relief of", "Holding action at", "Garrison duty at", "Counterattack at"];
    private static readonly string[] LocalForceNames = ["{0} Planetary Guard", "{0} Militia", "Governor {1}'s Household Troops", "Duke {1}'s Retainers", "The {1} Family Guard"];
    private static readonly string[] Surnames = ["Halloran", "Vasquez", "Okonkwo", "Brandt", "Szabo", "Takeda", "Moreau", "Ivanova", "Castellan", "Whitlock"];

    /// <summary>An offer for a company of this many lances: each lance faces its own two to four enemy mechs, and the reward follows
    /// the whole opposition's difficulty.</summary>
    public static MissionOffer Generate(MissionRandom random, int number, DateOnly date, string originSystemId, StarChart chart, Reputation? reputation = null, int lances = 1)
    {
        var area = chart.WithinJumps(originSystemId, MissionTuning.LocationJumps);
        var (systemId, employer, opponent, raid) = new Sides(random, chart, area, reputation ?? new()).Choose();
        var system = chart[systemId];
        var count = 0;
        for (var lance = 0; lance < lances; lance++) count += MissionTuning.MinOpposition + random.Next(MissionTuning.MaxOpposition - MissionTuning.MinOpposition + 1);
        var opposition = new EnemyMech[count];
        for (var i = 0; i < count; i++)
        {
            var weight = (MechWeightClass)random.Pick(MissionTuning.WeightOdds);
            var candidates = DraftCatalog.AllMechs.Where(mech => mech.WeightClass == weight).ToArray();
            opposition[i] = new(candidates[random.Next(candidates.Length)].Id, (EnemyPilotRank)random.Pick(MissionTuning.RankOdds));
        }
        var budget = Difficulty(opposition) * MissionTuning.CBillsPerDifficulty
            * (1 - MissionTuning.RewardVariation + 2 * MissionTuning.RewardVariation * random.NextDouble());
        var paymentShare = MissionTuning.MinPaymentShare + (1 - MissionTuning.MinPaymentShare) * random.NextDouble();
        var salvageShare = (1 - paymentShare) * random.NextDouble();
        var insuranceShare = 1 - paymentShare - salvageShare;
        var salvagePicks = Math.Clamp((int)Math.Round(budget * salvageShare / MissionTuning.CBillsPerSalvagePick), 0, MissionTuning.MaxSalvagePicks);
        var kinds = raid ? RaidKinds : DefenseKinds;
        var briefing = raid ? $"Placeholder briefing. {employer.Name} is hiring a force to strike {opponent.Description} on {system.Name}."
            : $"Placeholder briefing. {employer.Name} needs {system.Name} held against {opponent.Description}.";
        return new($"offer-{number}", $"{kinds[random.Next(kinds.Length)]} {system.Name}", briefing + " Further details will follow.",
            raid ? MissionTuning.RaidObjective : MissionTuning.DefenseObjective, system.Id, date, opposition,
            Round(budget * paymentShare), salvagePicks, Round(budget * insuranceShare), employer, opponent, raid);
    }

    /// <summary>Chooses who hires the force, who it fights and where, among the systems within reach.
    /// A faction that hates the force never hires it.</summary>
    private sealed class Sides(MissionRandom random, StarChart chart, IReadOnlyList<string> area, Reputation reputation)
    {
        private bool Hires(Faction faction) => !reputation.Hates(faction);
        private Faction? House(string id) => chart[id].Faction is { } faction && Factions.Spec(faction).GreatHouse ? faction : null;
        private IEnumerable<string> HeldBy(Faction faction) => area.Where(id => chart[id].Faction == faction);
        /// <summary>Great Houses other than the given one holding a system within the border distance of a location.</summary>
        private Faction[] Neighbors(string id, Faction except) => chart.WithinJumps(id, MissionTuning.BorderJumps)
            .Select(House).OfType<Faction>().Where(house => house != except).Distinct().Order().ToArray();
        private T Pick<T>(IReadOnlyList<T> items) => items[random.Next(items.Count)];
        private bool Roll(double chance) => random.NextDouble() < chance;

        public (string SystemId, Party Employer, Party Opponent, bool Raid) Choose() => random.Pick(MissionTuning.EmployerOdds) switch
        {
            0 => ForHouse(),
            1 => ForComStar(),
            _ => ForPirates(),
        } ?? Fallback();

        private (string, Party, Party, bool)? ForHouse()
        {
            var houses = Factions.GreatHouses.Where(house => Hires(house) && HeldBy(house).Any()).ToArray();
            if (houses.Length == 0) return null;
            var employer = Pick(houses);
            var home = HeldBy(employer).ToArray();
            switch (random.Pick(MissionTuning.HouseOpponentOdds))
            {
                case 0 when Roll(MissionTuning.RaidChance):
                    var targets = area.Where(id => House(id) is { } owner && owner != employer
                        && chart.WithinJumps(id, MissionTuning.BorderJumps).Any(near => chart[near].Faction == employer)).ToArray();
                    if (targets.Length == 0) break;
                    var target = Pick(targets);
                    return (target, Party.Of(employer), Party.Of(House(target)!.Value), true);
                case 0:
                    var fronts = home.Where(id => Neighbors(id, employer).Length > 0).ToArray();
                    if (fronts.Length == 0) break;
                    var front = Pick(fronts);
                    return (front, Party.Of(employer), Party.Of(Pick(Neighbors(front, employer))), false);
                case 1:
                    var foreign = area.Where(id => chart[id].Faction != employer).ToArray();
                    if (foreign.Length == 0) break;
                    var place = Pick(foreign);
                    return (place, Party.Of(employer), LocalForces(place), true);
                case 3:
                    // Removing a ComStar presence from one of the House's own worlds.
                    return (Pick(home), Party.Of(employer), Party.Of(Faction.ComStar), true);
            }
            return (Pick(home), Party.Of(employer), Party.Pirates, false);
        }

        private (string, Party, Party, bool)? ForComStar()
        {
            if (!Hires(Faction.ComStar)) return null;
            var held = area.Where(id => chart[id].Owner is not null).ToArray();
            if (held.Length == 0) return null;
            var place = Pick(held);
            var raid = Roll(MissionTuning.RaidChance);
            switch (random.Pick(MissionTuning.ComStarOpponentOdds))
            {
                case 1: return (place, Party.Of(Faction.ComStar), LocalForces(place), raid);
                case 2:
                    var houses = House(place) is { } owner ? new[] { owner } : Neighbors(place, Faction.ComStar);
                    if (houses.Length > 0) return (place, Party.Of(Faction.ComStar), Party.Of(Pick(houses)), raid);
                    break;
            }
            return (place, Party.Of(Faction.ComStar), Party.Pirates, raid);
        }

        private (string, Party, Party, bool)? ForPirates()
        {
            var targets = area.Where(id => House(id) is not null).ToArray();
            if (targets.Length == 0) return null;
            var target = Pick(targets);
            return (target, Party.Pirates, random.Pick(MissionTuning.PirateOpponentOdds) == 0 ? LocalForces(target) : Party.Of(House(target)!.Value), true);
        }

        /// <summary>Nothing suitable within reach: the location's owner defends against pirates, or pirates raid local forces.</summary>
        private (string, Party, Party, bool) Fallback()
        {
            var place = Pick(area);
            return chart[place].Faction is { } owner && Hires(owner) ? (place, Party.Of(owner), Party.Pirates, false)
                : (place, Party.Pirates, LocalForces(place), true);
        }

        private Party LocalForces(string id) =>
            new(PartyKind.LocalForces, null, string.Format(Pick(LocalForceNames), chart[id].Name, Pick(Surnames)));
    }

    /// <summary>Enemy tonnage, each mech weighted by its pilot's rank.</summary>
    public static double Difficulty(IEnumerable<EnemyMech> opposition) =>
        opposition.Sum(item => DraftCatalog.Mech(item.MechId).Tonnage * MissionTuning.RankMultiplier[(int)item.Rank]);

    private static long Round(double value) => (long)Math.Round(value / MissionTuning.RewardRounding) * MissionTuning.RewardRounding;
}

/// <summary>Lances is the company's lance count when above one; offers are generated for it.</summary>
public sealed record MissionSnapshot(MissionOffer[] Offers, MissionOffer? Current, DateOnly? Deadline, string[] SeenMissionIds,
    int NextOfferNumber, ulong RandomState, int? Lances = null);

/// <summary>The offer board, browsing history, and single accepted mission, without scene dependencies.
/// Offers are kept oldest first.</summary>
public sealed class MissionBoard
{
    private readonly List<MissionOffer> _available = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly StarChart _chart;
    private MissionRandom _random;
    private int _nextOfferNumber = 1;
    public IReadOnlyList<MissionOffer> Available => _available.AsReadOnly();
    public MissionOffer? Current { get; private set; }
    public DateOnly? Deadline { get; private set; }
    public string? SelectedMissionId => Current?.Id;
    public bool HasUnseenOffers => _available.Any(item => !_seen.Contains(item.Id));
    /// <summary>The company's lances, which every offer's opposition and reward scale with.</summary>
    public int Lances { get; }

    private MissionBoard(ulong seed, StarChart chart, int lances) { _random = new(seed); _chart = chart; Lances = lances; }

    public static MissionBoard Create(ulong seed, DateOnly date, string originSystemId, Reputation reputation, StarChart? chart = null, int lances = 1)
    {
        if (lances is < 1 or > ForceRoster.MaxLances) throw new ArgumentOutOfRangeException(nameof(lances));
        var board = new MissionBoard(seed, chart ?? StarChart.Instance, lances);
        board.Refill(date, originSystemId, reputation);
        return board;
    }

    public MissionOffer? Find(string id) => Current?.Id == id ? Current : _available.SingleOrDefault(item => item.Id == id);

    /// <summary>Each day the oldest offer may be taken off the board; the board is then refilled.</summary>
    internal void AdvanceDay(DateOnly date, string originSystemId, Reputation reputation)
    {
        if (_available.Count > 0 && _random.NextDouble() < MissionTuning.DailyRemovalChance)
        {
            _seen.Remove(_available[0].Id);
            _available.RemoveAt(0);
        }
        Refill(date, originSystemId, reputation);
    }

    private void Refill(DateOnly date, string originSystemId, Reputation reputation)
    {
        while (_available.Count < MissionTuning.BoardSize)
            _available.Add(MissionGenerator.Generate(_random, _nextOfferNumber++, date, originSystemId, _chart, reputation, Lances));
    }

    public void MarkOffersSeen()
    {
        foreach (var offer in _available) _seen.Add(offer.Id);
    }

    /// <summary>The accepted offer leaves the board; its place is filled on the next day.</summary>
    public void Accept(string id, DateOnly deadline)
    {
        if (Current is not null) throw new InvalidOperationException("Abandon the current mission before accepting another.");
        var offer = _available.SingleOrDefault(item => item.Id == id) ?? throw new ArgumentException("The mission is not available.", nameof(id));
        _available.Remove(offer);
        _seen.Add(id);
        Current = offer; Deadline = deadline;
    }

    /// <summary>An abandoned mission is gone for good.</summary>
    public void Abandon()
    {
        if (Current is null) throw new InvalidOperationException("There is no current mission to abandon.");
        _seen.Remove(Current.Id);
        Current = null; Deadline = null;
    }

    /// <summary>A settled mission is finished; it cannot be settled again.</summary>
    public void Complete()
    {
        if (Current is null) throw new InvalidOperationException("There is no current mission to complete.");
        _seen.Remove(Current.Id);
        Current = null; Deadline = null;
    }

    public MissionSnapshot Snapshot() => new(_available.ToArray(), Current, Deadline,
        _seen.Order(StringComparer.Ordinal).ToArray(), _nextOfferNumber, _random.State, Lances > 1 ? Lances : null);

    public static MissionBoard Restore(MissionSnapshot snapshot, DateOnly date, StarChart? chart = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Lances is < 2 or > ForceRoster.MaxLances) throw new ArgumentException("The mission save contains invalid state.", nameof(snapshot));
        var board = new MissionBoard(snapshot.RandomState, chart ?? StarChart.Instance, snapshot.Lances ?? 1);
        var offers = snapshot.Offers ?? [];
        var all = snapshot.Current is null ? offers : offers.Append(snapshot.Current).ToArray();
        if (snapshot.Offers is null || offers.Length > MissionTuning.BoardSize
            || all.Any(item => !board.Valid(item, date, snapshot.NextOfferNumber))
            || all.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != all.Length
            || offers.Zip(offers.Skip(1)).Any(pair => pair.Second.Posted < pair.First.Posted)
            || (snapshot.Current is null) != (snapshot.Deadline is null)
            || snapshot.Deadline is { } deadline && deadline < snapshot.Current!.Posted
            || snapshot.SeenMissionIds is null
            || snapshot.SeenMissionIds.Any(id => id is null || !all.Any(item => item.Id == id))
            || snapshot.SeenMissionIds.Distinct(StringComparer.Ordinal).Count() != snapshot.SeenMissionIds.Length
            || snapshot.Current is not null && !snapshot.SeenMissionIds.Contains(snapshot.Current.Id, StringComparer.Ordinal))
            throw new ArgumentException("The mission save contains invalid state.", nameof(snapshot));
        board._available.AddRange(offers);
        board.Current = snapshot.Current; board.Deadline = snapshot.Deadline;
        board._seen.UnionWith(snapshot.SeenMissionIds);
        board._nextOfferNumber = snapshot.NextOfferNumber;
        return board;
    }

    private bool Valid(MissionOffer? offer, DateOnly date, int nextOfferNumber) => offer is not null
        && offer.Id is not null && offer.Id.StartsWith("offer-", StringComparison.Ordinal)
        && int.TryParse(offer.Id["offer-".Length..], out var number) && number >= 1 && number < nextOfferNumber
        && !string.IsNullOrWhiteSpace(offer.Name) && !string.IsNullOrWhiteSpace(offer.Briefing) && !string.IsNullOrWhiteSpace(offer.Objective)
        && offer.SystemId is not null && _chart.Contains(offer.SystemId) && offer.Posted <= date
        && offer.Opposition is { Length: var count } && count >= MissionTuning.MinOpposition * Lances && count <= MissionTuning.MaxOpposition * Lances
        && offer.Opposition.All(item => item is not null && Enum.IsDefined(item.Rank)
            && DraftCatalog.AllMechs.Any(mech => mech.Id == item.MechId))
        && offer.Payment > 0 && offer.SalvagePicks is >= 0 and <= MissionTuning.MaxSalvagePicks && offer.InsuranceLimit >= 0
        && offer.Employer is { Valid: true, Kind: not PartyKind.LocalForces } && offer.Opponent is { Valid: true } && offer.Employer != offer.Opponent;
}
