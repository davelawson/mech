namespace Mech;

public enum EnemyPilotRank { Green, Regular, Veteran, Elite }

/// <summary>One BattleMech of a mission's opposition and the rank of its unnamed pilot.</summary>
public sealed record EnemyMech(string MechId, EnemyPilotRank Rank);

/// <summary>Generated briefing terms. The opposition is exactly the force that will be fielded; rewards are fixed.</summary>
public sealed record MissionOffer(string Id, string Name, string Briefing, string Objective, string SystemId, DateOnly Posted,
    EnemyMech[] Opposition, long Payment, int SalvagePicks, long InsuranceLimit);

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
    public const long CBillsPerDifficulty = 4_000;
    public const double RewardVariation = .2;
    public const double MinPaymentShare = .4;
    public const long CBillsPerSalvagePick = 100_000;
    public const int MaxSalvagePicks = 6;
    public const long RewardRounding = 1_000;
    public const string Objective = "Defeat the defending force.";
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
    private static readonly string[] Kinds = ["Raid on", "Strike at", "Assault on", "Sweep of", "Ambush at", "Recon in force at"];

    public static MissionOffer Generate(MissionRandom random, int number, DateOnly date, string originSystemId, StarChart chart)
    {
        var systems = chart.WithinJumps(originSystemId, MissionTuning.LocationJumps);
        var system = chart[systems[random.Next(systems.Count)]];
        var count = MissionTuning.MinOpposition + random.Next(MissionTuning.MaxOpposition - MissionTuning.MinOpposition + 1);
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
        return new($"offer-{number}", $"{Kinds[random.Next(Kinds.Length)]} {system.Name}",
            $"Placeholder briefing. A client needs the force holding {system.Name} dealt with. Further details will follow.",
            MissionTuning.Objective, system.Id, date, opposition, Round(budget * paymentShare), salvagePicks, Round(budget * insuranceShare));
    }

    /// <summary>Enemy tonnage, each mech weighted by its pilot's rank.</summary>
    public static double Difficulty(IEnumerable<EnemyMech> opposition) =>
        opposition.Sum(item => DraftCatalog.Mech(item.MechId).Tonnage * MissionTuning.RankMultiplier[(int)item.Rank]);

    private static long Round(double value) => (long)Math.Round(value / MissionTuning.RewardRounding) * MissionTuning.RewardRounding;
}

public sealed record MissionSnapshot(MissionOffer[] Offers, MissionOffer? Current, DateOnly? Deadline, string[] SeenMissionIds,
    int NextOfferNumber, ulong RandomState);

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

    private MissionBoard(ulong seed, StarChart chart) { _random = new(seed); _chart = chart; }

    public static MissionBoard Create(ulong seed, DateOnly date, string originSystemId, StarChart? chart = null)
    {
        var board = new MissionBoard(seed, chart ?? StarChart.Instance);
        board.Refill(date, originSystemId);
        return board;
    }

    public MissionOffer? Find(string id) => Current?.Id == id ? Current : _available.SingleOrDefault(item => item.Id == id);

    /// <summary>Each day the oldest offer may be taken off the board; the board is then refilled.</summary>
    internal void AdvanceDay(DateOnly date, string originSystemId)
    {
        if (_available.Count > 0 && _random.NextDouble() < MissionTuning.DailyRemovalChance)
        {
            _seen.Remove(_available[0].Id);
            _available.RemoveAt(0);
        }
        Refill(date, originSystemId);
    }

    private void Refill(DateOnly date, string originSystemId)
    {
        while (_available.Count < MissionTuning.BoardSize)
            _available.Add(MissionGenerator.Generate(_random, _nextOfferNumber++, date, originSystemId, _chart));
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

    public MissionSnapshot Snapshot() => new(_available.ToArray(), Current, Deadline,
        _seen.Order(StringComparer.Ordinal).ToArray(), _nextOfferNumber, _random.State);

    public static MissionBoard Restore(MissionSnapshot snapshot, DateOnly date, StarChart? chart = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var board = new MissionBoard(snapshot.RandomState, chart ?? StarChart.Instance);
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
        && offer.Opposition is { Length: >= MissionTuning.MinOpposition and <= MissionTuning.MaxOpposition }
        && offer.Opposition.All(item => item is not null && Enum.IsDefined(item.Rank)
            && DraftCatalog.AllMechs.Any(mech => mech.Id == item.MechId))
        && offer.Payment > 0 && offer.SalvagePicks is >= 0 and <= MissionTuning.MaxSalvagePicks && offer.InsuranceLimit >= 0;
}
