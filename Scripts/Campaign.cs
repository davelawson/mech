using System.Globalization;

namespace Mech;

public enum CampaignEventKind { Arrival, RepairCompletion, MissionResult, ContractDeadline, InjuryHealed, QuarterlyPayment }

public sealed record CampaignEvent(string Id, DateOnly Date, CampaignEventKind Kind, string Title);
/// <summary>A dated event ahead on the calendar, as the upcoming-events panel lists it.</summary>
public sealed record UpcomingEvent(DateOnly Date, CampaignEventKind Kind, string Title);
public sealed record DayResult(bool Advanced, bool QuarterReached, IReadOnlyList<CampaignEvent> Events);
/// <summary>What settling a mission did: payment received, reputation changes in tenths, MechWarriors killed, and the day it took.
/// Insurance is what the contract paid towards the battle's damage, InsuranceClaim the full cost of that damage. Salvage lists the
/// storage ids taken into storage and the ids of mechs taken whole; SalvagePicks counts the picks used.</summary>
public sealed record MissionSettlement(MissionOffer Mission, MissionOutcome Outcome, long Payment, IReadOnlyDictionary<Faction, int> Reputation,
    string[] Killed, DayResult Day)
{
    public long Insurance { get; init; }
    public long InsuranceClaim { get; init; }
    public string[] SalvagedItems { get; init; } = [];
    public string[] SalvagedMechs { get; init; } = [];
    public int SalvagePicks { get; init; }
    public int ExtraPicks => Math.Max(0, SalvagePicks - Mission.SalvagePicks);
    /// <summary>Hostages taken from surrendered enemies, ransomed after a victory, and what their ransom paid.</summary>
    public EnemyPilotRank[] Hostages { get; init; } = [];
    public long Ransom { get; init; }
}
public sealed record CampaignSnapshot(int Version, DateOnly Date, long Cash, long PaymentDue,
    long DeferredDebt, DateOnly NextQuarter, bool Bankrupt, CampaignEvent[] Events, RosterSnapshot? Roster = null, MissionSnapshot? Missions = null,
    TravelSnapshot? Travel = null, int AccruedTransitDays = 0, int BilledTransitDays = 0, ReputationSnapshot? Reputation = null,
    string[]? PlayerNames = null, MarketSnapshot[]? Markets = null, ulong? MarketSeed = null, long BilledSalaries = 0,
    RecruitmentSnapshot? Recruitment = null);

/// <summary>Campaign time and obligations, independent of the scene tree and animation.</summary>
public sealed class Campaign
{
    public static readonly DateOnly StartDate = new(3025, 1, 1);
    public const long StartingCash = 2_500_000;
    public const long QuarterlyCharge = 100_000;
    public const long TransitDailyCharge = 2_000;
    public Travel Travel { get; private set; } = new(StarChart.Instance);
    public int AccruedTransitDays { get; private set; }
    public int BilledTransitDays { get; private set; }
    public long AccruedTransitCost => AccruedTransitDays * TransitDailyCharge;
    public long BilledTransitCost => BilledTransitDays * TransitDailyCharge;
    /// <summary>Salaries on the bill now due; they pay for the quarter it opens.</summary>
    public long BilledSalaries { get; private set; }
    /// <summary>The next quarterly bill as things stand: salaries are billed in advance, for every MechWarrior alive on the due date.</summary>
    public long ProjectedBill => QuarterlyCharge + DeferredDebt + AccruedTransitCost + (Roster?.Salaries ?? 0);
    private readonly List<CampaignEvent> _events = [];

    public DateOnly Date { get; private set; } = StartDate;
    public long Cash { get; private set; } = StartingCash;
    public long PaymentDue { get; private set; }
    public long DeferredDebt { get; private set; }
    public DateOnly NextQuarter { get; private set; } = new(3025, 4, 1);
    public bool Bankrupt { get; private set; }
    public bool PaymentPending => PaymentDue > 0;
    public ForceRoster? Roster { get; private set; }
    public bool RequiresDraft => Roster is null;
    public MissionBoard Missions { get; private set; }
    public Reputation Reputation { get; private set; } = new();
    public const int SaveVersion = 8;

    /// <summary>Markets viewed so far, by system.</summary>
    private readonly Dictionary<string, SystemMarket> _markets = new(StringComparer.Ordinal);
    /// <summary>Seeds market stock, so the same campaign stocks the same way.</summary>
    public ulong MarketSeed { get; private set; }

    /// <summary>A seeded campaign stocks its markets from the same seed, so it plays out the same way.</summary>
    public Campaign(ulong? missionSeed = null)
    {
        MarketSeed = missionSeed is { } seed ? seed ^ 0x9E3779B97F4A7C15UL : NewSeed();
        Missions = MissionBoard.Create(missionSeed ?? NewSeed(), Date, MissionOrigin, Reputation);
    }
    private static ulong NewSeed() => (ulong)Random.Shared.NextInt64();

    /// <summary>Where mission offers are generated around: the orbited system, or the destination of the current jump.</summary>
    public string MissionOrigin => Travel.InOrbit ? Travel.OrbitSystemId : Travel.NextSystemId!;

    /// <summary>Days until the DropShip could orbit a system, finishing the current jump first; null if unreachable.</summary>
    public int? TravelDaysTo(string systemId)
    {
        var days = Travel.InOrbit ? 0 : Travel.DaysRemaining;
        if (MissionOrigin == systemId) return days;
        var route = StarChart.Instance.Plot(MissionOrigin, systemId);
        return route is null ? null : days + StarChart.Instance.RouteDays(route);
    }

    /// <summary>Days after acceptance by which an offer must be completed.</summary>
    public int? DeadlineDays(MissionOffer offer) => TravelDaysTo(offer.SystemId) + MissionTuning.DeadlineSlackDays;

    public void AcceptMission(string id)
    {
        var offer = Missions.Find(id) ?? throw new ArgumentException("The mission is not available.", nameof(id));
        var days = DeadlineDays(offer) ?? throw new InvalidOperationException("The mission location cannot be reached.");
        Missions.Accept(id, Date.AddDays(days));
    }
    public void EstablishCompany(InitialDraft draft)
    {
        if (!RequiresDraft) throw new InvalidOperationException("The initial draft is already complete.");
        Roster = draft.Establish();
        StartWith(draft.Scenario);
        RecordFounding();
    }
    /// <summary>Forms a co-op company from each player's draft, one lance per player, under the players' names.</summary>
    public void EstablishCompany(IReadOnlyList<InitialDraft> drafts, IReadOnlyList<string> names)
    {
        if (!RequiresDraft) throw new InvalidOperationException("The initial draft is already complete.");
        if (names.Count != drafts.Count || names.Any(name => !ValidPlayerName(name))) throw new ArgumentException("Every player needs a name.");
        Roster = InitialDraft.EstablishCompany(drafts);
        StartWith(drafts[0].Scenario);
        RecordFounding();
        PlayerNames = [.. names];
        // The offers so far were for one lance; the company's board is made for all of them.
        Missions = MissionBoard.Create(NewSeed(), Date, MissionOrigin, Reputation, lances: Players);
    }
    /// <summary>Every drafted MechWarrior's history begins as a founding member of the company.</summary>
    private void RecordFounding()
    {
        foreach (var warrior in RequireRoster().Warriors) warrior.Record(new(Date, ServiceEventKind.Founded) { Place = Roster!.ForceName });
    }
    /// <summary>A scenario starting with less than the standard cash takes the difference from the company's cash.</summary>
    private void StartWith(StartingScenario scenario) => Cash = Math.Max(0, Cash + Scenarios.StartingCash(scenario) - StartingCash);
    /// <summary>The players' names by slot in a co-op campaign, as last played; null in single-player.</summary>
    public IReadOnlyList<string>? PlayerNames { get; private set; }
    public static bool ValidPlayerName(string? name) => !string.IsNullOrWhiteSpace(name) && name == name.Trim() && name.Length <= 24 && !name.Any(char.IsControl);
    /// <summary>Records the names of the players resuming a co-op campaign.</summary>
    public void RenamePlayers(IReadOnlyList<string> names)
    {
        if (PlayerNames is null || names.Count != Players || names.Any(name => !ValidPlayerName(name))) throw new ArgumentException("Every player needs a name.");
        PlayerNames = [.. names];
    }
    public bool CanAdvance => !RequiresDraft && !PaymentPending && !Bankrupt && Date < new DateOnly(9998, 12, 31);
    public bool CanPay => !RequiresDraft && PaymentPending && !Bankrupt && Cash >= PaymentDue;
    public bool CanDefer => !RequiresDraft && PaymentPending && !Bankrupt && DeferredDebt == 0;
    public int DaysUntilQuarter => NextQuarter.DayNumber - Date.DayNumber;
    public string DateText => Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
    /// <summary>"“Ace” Jane Doe", as the calendar names a MechWarrior.</summary>
    private static string WarriorName(RosterWarrior warrior) => $"“{warrior.Spec.Callsign}” {warrior.Spec.Name}";

    /// <summary>Every dated event still ahead, soonest first: the next quarterly payment, each remaining arrival of the journey,
    /// started repairs and refits, injured MechWarriors' recoveries, the current mission's deadline and anything else scheduled.
    /// Events on one date follow the order they are handled in: payment, arrival, repair or refit, healing, deadline.</summary>
    public IReadOnlyList<UpcomingEvent> UpcomingEvents
    {
        get
        {
            var all = new List<UpcomingEvent>
            {
                new(NextQuarter, CampaignEventKind.QuarterlyPayment,
                    $"Quarterly payment · {ProjectedBill.ToString("N0", CultureInfo.InvariantCulture)} C-bills (projected)")
            };
            if (Travel.Journey is { } journey)
            {
                var stops = journey.Count - 1;
                var arrival = Date.AddDays(Travel.DaysToArrival);
                for (var leg = Travel.LegIndex + 1; leg <= stops; leg++)
                {
                    if (leg > Travel.LegIndex + 1) arrival = arrival.AddDays(StarChart.Instance.JumpDays(journey[leg - 1], journey[leg]));
                    var name = StarChart.Instance[journey[leg]].Name;
                    all.Add(new(arrival, CampaignEventKind.Arrival, leg == stops ? $"Arrive at {name}" : $"Arrive at {name} · stop {leg} of {stops}"));
                }
            }
            foreach (var mech in Roster?.Mechs ?? [])
                if (mech.Repair is { Completes: { } completes } order)
                    all.Add(new(completes, CampaignEventKind.RepairCompletion, $"{(order.Refit ? "Refit" : "Repair")} complete · {mech.Spec.DisplayName}"));
            foreach (var warrior in Roster?.Warriors ?? [])
                if (warrior is { Health: WarriorHealth.Injured, RecoveryDate: { } recovers })
                    all.Add(new(recovers, CampaignEventKind.InjuryHealed, $"Injury healed · {WarriorName(warrior)}"));
            if (Missions.Current is { } mission && Missions.Deadline is { } deadline && deadline > Date)
                all.Add(new(deadline, CampaignEventKind.ContractDeadline, $"Mission deadline · {mission.Name}"));
            all.AddRange(_events.Where(item => item.Date > Date).Select(item => new UpcomingEvent(item.Date, item.Kind, item.Title)));
            return all.OrderBy(item => item.Date).ThenBy(item => Rank(item.Kind)).ToArray();
        }
    }

    private static int Rank(CampaignEventKind kind) => kind switch
    {
        CampaignEventKind.QuarterlyPayment => 0,
        CampaignEventKind.Arrival => 1,
        CampaignEventKind.RepairCompletion => 2,
        CampaignEventKind.InjuryHealed => 3,
        CampaignEventKind.ContractDeadline => 4,
        _ => 5
    };

    public DateOnly NextEventDate => UpcomingEvents[0].Date;

    /// <summary>The leader's player slot; the other slots number on from it, one per lance.</summary>
    public const int LeaderSlot = 0;
    /// <summary>Player slots: one per lance, fixed when the campaign begins.</summary>
    public int Players => Roster?.LanceCount ?? 1;

    /// <summary>Carries out a player slot's command, refusing leader-only commands from other slots.</summary>
    public void Execute(CampaignCommand command, int slot = LeaderSlot) => Run(command, slot);
    public TResult Execute<TResult>(CampaignCommand<TResult> command, int slot = LeaderSlot) => (TResult)Run(command, slot)!;
    private object? Run(CampaignCommand command, int slot)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (slot < 0 || slot >= Players) throw new ArgumentOutOfRangeException(nameof(slot), "The campaign has no such player slot.");
        if (command.LeaderOnly && slot != LeaderSlot) throw new InvalidOperationException("Only the leader can do that.");
        return command.Run(this, slot);
    }
    internal ForceRoster RequireRoster() => Roster ?? throw new InvalidOperationException("No company has been established.");
    internal static void RequireControl(ForceRoster roster, int slot, string mechId)
    {
        var mech = roster.Mechs.SingleOrDefault(item => item.Id == mechId) ?? throw new ArgumentException("Unknown owned BattleMech.");
        if (!roster.Controls(slot, mech)) throw new InvalidOperationException("That BattleMech belongs to another player's lance.");
    }

    public bool PlotJourney(string destination) { RequireTravelAccess(); return Travel.Plot(destination); }
    public void Embark() { RequireTravelAccess(); Travel.Embark(); }
    public void CancelJourney() { RequireTravelAccess(); Travel.Cancel(); }
    private void RequireTravelAccess()
    {
        if (RequiresDraft || Bankrupt) throw new InvalidOperationException("An established, solvent company is required.");
    }

    public void Schedule(CampaignEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title)
            || item.Date <= Date || item.Date.Year >= 9999 || !Enum.IsDefined(item.Kind)
            || _events.Any(existing => existing.Id == item.Id))
            throw new ArgumentException("An event needs a unique ID, a title and a future date.", nameof(item));
        _events.Add(item);
    }

    public DayResult AdvanceOneDay()
    {
        if (!CanAdvance) return new(false, false, Array.Empty<CampaignEvent>());
        Date = Date.AddDays(1);
        foreach (var warrior in Roster?.Recover(Date) ?? [])
        {
            // The recovery counts from the battle whose injury or ejection set it.
            var injury = warrior.History.LastOrDefault(item => item.Kind is ServiceEventKind.Injured or ServiceEventKind.Ejected && item.Days > 0);
            warrior.Record(new(Date, ServiceEventKind.Recovered) { Days = injury is null ? 0 : Date.DayNumber - injury.Date.DayNumber });
            _events.Add(new($"healed-{warrior.Id}-{Date.DayNumber}", Date, CampaignEventKind.InjuryHealed,
                $"Injury healed · {WarriorName(warrior)} · back on the active roster"));
        }
        CompleteRepairs();
        if (Travel.HasJourney)
        {
            AccruedTransitDays++;
            var arrived = Travel.AdvanceDay();
            if (arrived is not null) _events.Add(new($"travel-arrival-{Date.DayNumber}", Date,
                CampaignEventKind.Arrival, $"Arrived at {StarChart.Instance[arrived].Name}. In orbit for today."));
        }
        Missions.AdvanceDay(Date, MissionOrigin, Reputation);
        if (Missions.Current is { } mission && Missions.Deadline == Date)
            _events.Add(new($"deadline-{mission.Id}", Date, CampaignEventKind.ContractDeadline, $"Mission deadline · {mission.Name}"));
        ViewRecruits();
        var quarterReached = Date == NextQuarter;
        // Accounting is evaluated before any other event on the same date.
        if (quarterReached)
        {
            BilledTransitDays = AccruedTransitDays;
            AccruedTransitDays = 0;
            BilledSalaries = Roster?.Salaries ?? 0;
            PaymentDue = QuarterlyCharge + DeferredDebt + BilledTransitCost + BilledSalaries;
            NextQuarter = NextQuarter.AddMonths(3);
            Bankrupt = DeferredDebt > 0 && Cash < PaymentDue;
        }
        // Retain same-day commitments until the blocking quarterly payment is resolved.
        var due = PaymentPending ? Array.Empty<CampaignEvent>() : TakeDueEvents();
        return new(true, quarterReached, due);
    }

    public IReadOnlyList<CampaignEvent> PayNow()
    {
        if (!CanPay) throw new InvalidOperationException("The full payment must be affordable.");
        Cash -= PaymentDue;
        PaymentDue = 0;
        BilledTransitDays = 0;
        BilledSalaries = 0;
        DeferredDebt = 0;
        return TakeDueEvents();
    }

    /// <summary>Orders a refit of a mech to a loadout, paying now for the items it buys from the market, which join storage.
    /// The spares it installs are reserved from storage. The mech keeps its loadout until a repair bay finishes the refit.
    /// Nothing changes unless the purchase is affordable and the loadout is valid.</summary>
    public void OrderRefit(string mechId, LoadoutPlan plan, IReadOnlyCollection<string> bought)
    {
        var roster = RequireRoster();
        var mech = roster.Mechs.SingleOrDefault(item => item.Id == mechId) ?? throw new ArgumentException("Unknown owned BattleMech.");
        if (bought.Any(id => StorageCatalog.Describe(id) is null)) throw new ArgumentException("Unknown equipment.");
        var cost = bought.Sum(EquipmentMarket.Price);
        if (cost > Cash) throw new InvalidOperationException("The purchase must be affordable.");
        if (bought.Count > 0 && OrbitMarket is null) throw new InvalidOperationException("There is no market here.");
        if (mech.Repair is not null) throw new InvalidOperationException("A BattleMech with a repair or refit order cannot change its loadout.");
        if (bought.Count > 0 && OrbitMarket!.Shortage(bought) is { } shortage) throw new InvalidOperationException(shortage + ".");
        var estimate = Repairs.RefitEstimate(mech, plan) ?? throw new InvalidOperationException("That loadout changes nothing.");
        if (mech.WithLoadout(plan).OverTonnage) throw new ArgumentException("The loadout is over tonnage.");
        var reserved = mech.StorageChange(plan).Where(change => change.Count < 0).Select(change => change.Id).ToArray();
        if (reserved.GroupBy(id => id, StringComparer.Ordinal).Any(group => roster.Stored(group.Key) + bought.Count(id => id == group.Key) < group.Count()))
            throw new ArgumentException("Storage lacks the spares this loadout installs.");
        foreach (var id in bought) roster.Store(id, 1);
        OrbitMarket?.Sell(bought);
        Cash -= cost;
        foreach (var id in reserved) roster.Store(id, -1);
        var sequence = roster.Mechs.Select(item => item.Repair?.Sequence ?? -1).Max() + 1;
        mech.Repair = new(RepairScope.Refit, estimate.Days, 0, sequence, null, reserved.Length == 0 ? null : reserved, null, plan);
        StartRepairs();
    }

    /// <summary>The market of a system, as last viewed; null when it has never been viewed or the world has none.</summary>
    public SystemMarket? Market(string systemId) => _markets.GetValueOrDefault(systemId);
    /// <summary>The market where the DropShip is in orbit, as last viewed; null in transit, before it is first viewed, or on a world with none.</summary>
    public SystemMarket? OrbitMarket => Travel.InOrbit ? Market(Travel.OrbitSystemId) : null;

    /// <summary>A player viewing the market where the DropShip orbits: stocks it on its first view, and otherwise rolls it over to today.</summary>
    public void ViewMarket()
    {
        if (!Travel.InOrbit || !StarChart.Instance.Contains(Travel.OrbitSystemId)) return;
        var system = StarChart.Instance[Travel.OrbitSystemId];
        if (!SystemRatings.HasMarket(system)) return;
        var random = MarketRandom(system.Id);
        if (_markets.TryGetValue(system.Id, out var market)) market.Refresh(Date, random);
        else _markets[system.Id] = SystemMarket.Generate(system, Date, random);
    }
    private Random MarketRandom(string systemId)
    {
        // FNV-1a keeps the seed stable across runs, unlike string.GetHashCode.
        var hash = 14695981039346656037UL ^ MarketSeed ^ (ulong)Date.DayNumber * 1099511628211UL;
        foreach (var c in systemId) hash = (hash ^ c) * 1099511628211UL;
        return new Random((int)(hash ^ hash >> 32));
    }

    /// <summary>Replaces a market's stock with a saved one, as a test fixture.</summary>
    internal void PlaceMarket(MarketSnapshot save) => _markets[save.SystemId] = SystemMarket.Restore(save, StarChart.Instance, Date);

    /// <summary>Buys items from the orbited market straight into storage, paying for them at once.</summary>
    public void BuyToStorage(string storageId, int count)
    {
        var roster = RequireRoster();
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        if (OrbitMarket is not { } market) throw new InvalidOperationException("There is no market here.");
        var cost = EquipmentMarket.Price(storageId) * count;
        if (cost > Cash) throw new InvalidOperationException("The purchase must be affordable.");
        market.Sell([.. Enumerable.Repeat(storageId, count)]);
        roster.Store(storageId, count);
        Cash -= cost;
    }

    /// <summary>Every system's hiring list.</summary>
    public Recruitment Recruits { get; private set; } = new();
    /// <summary>The MechWarriors for hire where the DropShip is in orbit; none in transit or before the list is first seen.</summary>
    public IReadOnlyList<WarriorSpec> OrbitRecruits => Travel.InOrbit ? Recruits.Listed(Travel.OrbitSystemId) : [];

    /// <summary>Brings the hiring lists up to today and rolls the orbited system's empty slots, making its list on a first visit.</summary>
    public void ViewRecruits()
    {
        if (Roster is null || !Travel.InOrbit || !StarChart.Instance.Contains(Travel.OrbitSystemId)) return;
        Recruits.Refresh(StarChart.Instance[Travel.OrbitSystemId], Date, Roster, RecruitRandom);
    }
    private Random RecruitRandom(string systemId, DateOnly day, int salt)
    {
        var hash = 14695981039346656037UL ^ MarketSeed ^ 0xA5A5_5A5A_C3C3_3C3CUL ^ ((ulong)day.DayNumber * 1099511628211UL) ^ ((ulong)salt << 56);
        foreach (var c in systemId) hash = (hash ^ c) * 1099511628211UL;
        return new Random((int)(hash ^ hash >> 32));
    }

    /// <summary>Lists exactly these MechWarriors for hire where the DropShip orbits, as a test fixture.</summary>
    internal void PlaceRecruits(params WarriorSpec[] warriors) => Recruits.Place(Travel.OrbitSystemId, Date, warriors);

    /// <summary>What hiring a MechWarrior costs today: the rest of the quarter's salary, prorated by days and rounded up to
    /// <see cref="PilotTuning.HireRounding"/>, and a signing bonus of a full quarter's salary.</summary>
    public HireCost HireCost(WarriorSpec warrior)
    {
        var quarterStart = NextQuarter.AddMonths(-3);
        var days = NextQuarter.DayNumber - quarterStart.DayNumber; var left = NextQuarter.DayNumber - Date.DayNumber;
        var salary = warrior.Salary;
        var prorated = (salary * left + days * PilotTuning.HireRounding - 1) / (days * PilotTuning.HireRounding) * PilotTuning.HireRounding;
        return new(salary, prorated, left, days);
    }
    /// <summary>Why a MechWarrior cannot be hired now, or null when they can.</summary>
    public string? HireProblem(WarriorSpec warrior) =>
        Roster is null ? "No company has been established."
        : !Travel.InOrbit ? "MechWarriors can only be hired in orbit."
        : OrbitRecruits.All(item => item.Id != warrior.Id) ? "That MechWarrior is not for hire here."
        : HireCost(warrior).Total > Cash ? $"The company cannot afford {HireCost(warrior).Total.ToString("N0", CultureInfo.InvariantCulture)} C-bills." : null;
    /// <summary>Hires a MechWarrior listed where the DropShip orbits, paying the prorated salary and signing bonus at once.</summary>
    public void Recruit(string warriorId)
    {
        var warrior = OrbitRecruits.SingleOrDefault(item => item.Id == warriorId) ?? throw new InvalidOperationException("That MechWarrior is not for hire here.");
        if (HireProblem(warrior) is { } problem) throw new InvalidOperationException(problem);
        var cost = HireCost(warrior).Total;
        Recruits.Hire(Travel.OrbitSystemId, warriorId);
        RequireRoster().Hire(warrior).Record(new(Date, ServiceEventKind.Hired) { Place = StarChart.Instance[Travel.OrbitSystemId].Name });
        Cash -= cost;
    }
    /// <summary>Dismisses a MechWarrior for good: no refund, and they are gone from the game.</summary>
    public void Dismiss(string warriorId) => RequireRoster().Dismiss(warriorId);

    /// <summary>Repair orders working in a bay: at most <see cref="RepairTuning.Bays"/>.</summary>
    public int ActiveRepairs => Roster?.Mechs.Count(item => item.UnderRepair) ?? 0;
    /// <summary>Queued orders that start before this mech's.</summary>
    public int RepairsAhead(OwnedMech mech) => mech.Repair is { Started: null } order
        ? RequireRoster().Mechs.Count(item => item.Repair is { Started: null } other && other.Sequence < order.Sequence) : 0;

    /// <summary>Orders a repair of a mech, fixing its days and cost now. It starts, and is paid for, as soon as a repair bay is free.</summary>
    /// <summary>Orders a repair of a mech, fixing its days and cost now and taking the spares it needs from storage, only as many
    /// as <paramref name="available"/> allows (spares claimed by pending loadout changes are not available). It starts, and is paid
    /// for, as soon as a repair bay is free.</summary>
    public void OrderRepair(string mechId, RepairScope scope, IReadOnlyDictionary<string, int>? available = null)
    {
        var roster = RequireRoster();
        var mech = roster.Mechs.SingleOrDefault(item => item.Id == mechId) ?? throw new ArgumentException("Unknown owned BattleMech.");
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if (mech.Repair is not null) throw new InvalidOperationException("That BattleMech already has a repair order.");
        var estimate = Repairs.Estimate(mech, scope, id => Math.Min(roster.Stored(id), available is null ? int.MaxValue : available.GetValueOrDefault(id)));
        if (estimate is not { Doable: true }) throw new InvalidOperationException("That BattleMech has nothing to repair.");
        if (estimate.Cost > Cash) throw new InvalidOperationException("The repair must be affordable.");
        var sequence = roster.Mechs.Select(item => item.Repair?.Sequence ?? -1).Max() + 1;
        foreach (var part in estimate.Parts) roster.Store(part, -1);
        mech.Repair = new(scope, estimate.Days, estimate.Cost, sequence, null,
            estimate.Parts.Count == 0 ? null : [.. estimate.Parts], estimate.Missing.Count == 0 ? null : [.. estimate.Missing]);
        StartRepairs();
    }

    /// <summary>Cancels a mech's repair or refit order. A queued order costs nothing; a repair already started forfeits its payment
    /// and leaves the damage, and a refit leaves the old loadout.</summary>
    public void CancelRepair(string mechId)
    {
        var mech = RequireRoster().Mechs.SingleOrDefault(item => item.Id == mechId) ?? throw new ArgumentException("Unknown owned BattleMech.");
        if (mech.Repair is not { } order) throw new InvalidOperationException("That BattleMech has no repair order.");
        // The parts were never fitted, so they go back to storage whether or not work had started.
        foreach (var part in order.Parts ?? []) RequireRoster().Store(part, 1);
        mech.Repair = null;
        StartRepairs();
    }

    /// <summary>Free bays take queued orders, oldest first, paying for each; an order the company cannot afford waits, holding up
    /// those behind it. A refit costs nothing, so it never waits on cash.</summary>
    private void StartRepairs()
    {
        if (Roster is null) return;
        while (ActiveRepairs < RepairTuning.Bays
            && Roster.Mechs.Where(item => item.Repair is { Started: null }).MinBy(item => item.Repair!.Sequence) is { } next
            && (next.Repair!.Cost == 0 || next.Repair.Cost <= Cash))
        {
            Cash -= next.Repair.Cost;
            next.Repair = next.Repair with { Started = Date };
        }
    }

    /// <summary>Finished repairs restore their mechs, and finished refits change their loadouts, fitting the reserved spares;
    /// each reports, and their bays take the next orders the same day.</summary>
    private void CompleteRepairs()
    {
        if (Roster is null) return;
        foreach (var mech in Roster.Mechs.Where(item => item.Repair?.Completes <= Date).ToArray())
        {
            var order = mech.Repair!;
            if (order.Refit)
            {
                foreach (var part in order.Parts ?? []) Roster.Store(part, 1);
                Roster.ApplyLoadout(mech.Id, order.Plan!);
            }
            else mech.Repaired(order.Scope, order.Missing);
            mech.Repair = null;
            _events.Add(new($"repair-{mech.Id}-{Date.DayNumber}", Date, CampaignEventKind.RepairCompletion,
                $"{(order.Refit ? "Refit" : "Repair")} complete · {mech.Spec.DisplayName}"));
        }
        StartRepairs();
    }

    /// <summary>Settles the current mission from its battle: deployed mechs keep their damage and refill their ammunition,
    /// MechWarriors whose head was destroyed or cockpit struck die, injured ones recover after their injury days, survivors bank their
    /// successful skill gain checks (a skill rises at its threshold, and rank and salary with it), success pays,
    /// insurance pays for the battle's damage up to its limit, a victory's chosen salvage joins the company, reputation changes,
    /// and the mission's day passes.</summary>
    public MissionSettlement SettleMission(MissionReport report)
    {
        if (Roster is null || Missions.Current is not { } offer) throw new InvalidOperationException("There is no current mission to settle.");
        if (!CanAdvance) throw new InvalidOperationException("Resolve the quarterly payment before settling a mission.");
        if (report.Mechs.Length == 0 || report.Mechs.Any(item => Roster.Mechs.All(mech => mech.Id != item.MechId) || item.InjuryDays < 0))
            throw new ArgumentException("The report must name deployed BattleMechs.", nameof(report));
        if (report.Mechs.Select(item => item.MechId).Distinct(StringComparer.Ordinal).Count() != report.Mechs.Length)
            throw new ArgumentException("The report names a BattleMech twice.", nameof(report));
        if (report.Enemies.Length > 0 && (report.Enemies.Select(item => item.UnitId).Distinct(StringComparer.Ordinal).Count() != report.Enemies.Length
            || !report.Enemies.Select(item => item.MechId).Order(StringComparer.Ordinal).SequenceEqual(offer.Opposition.Select(item => item.MechId).Order(StringComparer.Ordinal))))
            throw new ArgumentException("The report's enemies are not the mission's opposition.", nameof(report));
        if (report.Mechs.Any(item => item.SkillGains is { } gains && (gains.Distinct().Count() != gains.Length || gains.Any(skill => !Enum.IsDefined(skill))
            || (item.WarriorKilled && gains.Length > 0) || (Roster.Pilot(Roster.Mechs.Single(mech => mech.Id == item.MechId)) is { } warrior
                ? gains.Any(skill => SkillGrowth.Maxed(SkillGrowth.Rating(warrior.Spec, skill))) : gains.Length > 0))))
            throw new ArgumentException("The report's skill gains do not fit its MechWarriors.", nameof(report));
        if (report.Mechs.Any(item => item.KillLog is null || item.InjuryCauses is null || item.KillLog.Any(kill => kill is null
            || string.IsNullOrWhiteSpace(kill.Enemy) || !Enum.IsDefined(kill.Manner)) || item.KillLog.Length > item.Kills
            || item.InjuryCauses.Any(cause => !Enum.IsDefined(cause)) || item.Death is { Manner: { } manner } && !Enum.IsDefined(manner)))
            throw new ArgumentException("The report's service records do not fit its MechWarriors.", nameof(report));
        if (report.Outcome != MissionOutcome.Success && report.Salvage.Length > 0) throw new ArgumentException("Only a victory takes salvage.", nameof(report));
        var pool = Salvage.Pool(report);
        var picks = Salvage.Validate(pool, report.Salvage, offer.SalvagePicks);
        var killed = new List<string>(); long claim = 0;
        // Each deployed mech's MechWarrior, found before the fallen are unassigned, and those who died with when they fell.
        var pilots = report.Mechs.Select(item => Roster.Pilot(Roster.Mechs.Single(mech => mech.Id == item.MechId))).ToArray();
        var fallen = report.Mechs.Zip(pilots).Where(pair => pair.First.WarriorKilled && pair.Second is not null)
            .Select(pair => (Warrior: pair.Second!, Time: pair.First.Death?.Time ?? double.MaxValue)).ToArray();
        for (var i = 0; i < report.Mechs.Length; i++)
        {
            var item = report.Mechs[i];
            var mech = Roster.Mechs.Single(owned => owned.Id == item.MechId);
            var pilot = pilots[i];
            var before = Insurance.RepairValue(mech);
            mech.ApplyBattle(item.Locations, item.Equipment);
            claim += Math.Max(0, Insurance.RepairValue(mech) - before);
            if (pilot is not null)
            {
                RecordBattle(pilot, item, mech, offer.Name, fallen);
                pilot.Deployments++; pilot.Kills += Math.Max(0, item.Kills);
                var rank = pilot.Spec.Rank;
                foreach (var skill in item.SkillGains ?? [])
                    if (pilot.GainSkill(skill))
                        pilot.Record(new(Date, ServiceEventKind.SkillAdvanced) { Skill = skill, Rating = SkillGrowth.Rating(pilot.Spec, skill) });
                if (pilot.Spec.Rank > rank) pilot.Record(new(Date, ServiceEventKind.Promoted) { Rank = pilot.Spec.Rank });
            }
            if (item.WarriorKilled && pilot is not null) { Roster.SetHealth(pilot.Id, WarriorHealth.Dead, null, Date); killed.Add(pilot.Id); }
            else if (item.InjuryDays > 0 && pilot is not null) Roster.SetHealth(pilot.Id, WarriorHealth.Injured, Date.AddDays(item.InjuryDays), Date);
        }
        var payment = report.Outcome == MissionOutcome.Success ? offer.Payment : 0;
        var insurance = Math.Min(claim, offer.InsuranceLimit);
        // Hostages are ransomed only after a victory; a lost battle leaves them behind.
        var hostages = report.Outcome != MissionOutcome.Success ? [] : report.Enemies.Where(item => item.Status == CombatStatus.Surrendered)
            .Select(item => Hostage(offer, item.UnitId)).ToArray();
        var ransom = hostages.Sum(SalvageTuning.RansomFor);
        Cash += payment + insurance + ransom;
        var deployedIds = report.Mechs.Select(item => Roster.Mechs.Single(mech => mech.Id == item.MechId).Definition.Id).ToArray();
        var items = new List<string>(); var mechs = new List<string>();
        foreach (var option in report.Salvage.Select(claimed => pool.Single(option => option.Claim == claimed)))
        {
            if (option.StorageId is { } storageId) { Roster.Store(storageId, 1); items.Add(storageId); continue; }
            var wreck = report.Enemies.Single(enemy => enemy.UnitId == option.UnitId);
            mechs.Add(Roster.AddSalvagedMech(DraftCatalog.Mech(wreck.MechId), wreck.Locations, wreck.Equipment).Id);
        }
        var conduct = new MissionConduct(report.Outcome, deployedIds, report.Mechs.Sum(item => item.ArmorLost), report.EnemyWarriorsSpared,
            report.Enemies.Count(item => item.Status == CombatStatus.Fled), picks, 0);
        var reputation = Reputation.ApplyMissionOutcome(offer, conduct, StarChart.Instance);
        Missions.Complete();
        return new(offer, report.Outcome, payment, reputation, killed.ToArray(), AdvanceOneDay())
        {
            Insurance = insurance, InsuranceClaim = claim, SalvagedItems = [.. items], SalvagedMechs = [.. mechs], SalvagePicks = picks,
            Hostages = hostages, Ransom = ransom
        };
    }
    /// <summary>Writes a mission into a deployed MechWarrior's service history, in the order things happened: the mech or lance they
    /// took into it when either differs from their previous deployment, then their kills (the ace's fifth among them) and the comrades
    /// they saw fall, by battle time, then how they died, ejected, lost their mech or were injured.</summary>
    private void RecordBattle(RosterWarrior pilot, DeployedMechReport item, OwnedMech mech, string mission, (RosterWarrior Warrior, double Time)[] fallen)
    {
        ServiceEvent At(ServiceEventKind kind) => new(Date, kind) { Place = mission };
        var newMech = pilot.LastMech != mech.Id;
        var newLance = mech.Lance is { } lance && pilot.LastLance != lance;
        if (newMech || newLance)
            pilot.Record(At(ServiceEventKind.Deployed) with
            {
                Mech = newMech ? mech.Spec.DisplayName : null, Lance = newLance ? mech.Lance : null,
                Rejoined = newLance && pilot.History.Any(entry => entry.Kind == ServiceEventKind.Deployed && entry.Lance == mech.Lance)
            });
        pilot.LastMech = mech.Id; pilot.LastLance = mech.Lance ?? pilot.LastLance;
        var moments = new List<(double Time, ServiceEvent Event)>();
        var kills = pilot.Kills; var ace = false;
        foreach (var kill in item.KillLog)
        {
            moments.Add((kill.Time, At(ServiceEventKind.Kill) with { Enemy = kill.Enemy, Weapon = kill.Weapon, Manner = kill.Manner }));
            if (++kills == ServiceRecord.AceKills) { moments.Add((kill.Time, At(ServiceEventKind.Ace))); ace = true; }
        }
        if (!ace && pilot.Kills < ServiceRecord.AceKills && pilot.Kills + item.Kills >= ServiceRecord.AceKills) moments.Add((double.MaxValue, At(ServiceEventKind.Ace)));
        if (!item.WarriorKilled)
            foreach (var (comrade, time) in fallen)
                if (comrade != pilot) moments.Add((time, At(ServiceEventKind.ComradeKilled) with { Comrade = WarriorName(comrade) }));
        foreach (var (_, moment) in moments.OrderBy(entry => entry.Time)) pilot.Record(moment);
        if (item.WarriorKilled)
        {
            pilot.Record(At(ServiceEventKind.Killed) with { Enemy = item.Death?.Enemy, Weapon = item.Death?.Weapon, Manner = item.Death?.Manner, Exploded = item.Death?.Exploded });
            return;
        }
        // An ejection carries the battle's whole recovery time; other injuries then only name their causes.
        if (item.Ejected)
            pilot.Record(At(ServiceEventKind.Ejected) with
            { Mech = mech.Spec.DisplayName, Days = item.InjuryDays, Destroyed = item.MechDestroyed, AsDestroyed = item.MechDestroyed && item.AutoEjected });
        else if (item.MechDestroyed) pilot.Record(At(ServiceEventKind.MechLost) with { Mech = mech.Spec.DisplayName });
        if (item.InjuryCauses.Length > 0 || !item.Ejected && item.InjuryDays > 0)
            pilot.Record(At(ServiceEventKind.Injured) with { Causes = item.InjuryCauses.Length > 0 ? [.. item.InjuryCauses] : null, Days = item.Ejected ? 0 : item.InjuryDays });
    }
    /// <summary>The rank of the pilot of enemy "enemy-N", the Nth mech of the opposition.</summary>
    private static EnemyPilotRank Hostage(MissionOffer offer, string unitId) =>
        int.TryParse(unitId.AsSpan("enemy-".Length), out var number) && unitId.StartsWith("enemy-", StringComparison.Ordinal)
            && number >= 1 && number <= offer.Opposition.Length ? offer.Opposition[number - 1].Rank
            : throw new ArgumentException("The report names an enemy that is not in the opposition.");

    public IReadOnlyList<CampaignEvent> DeferPayment()
    {
        if (!CanDefer) throw new InvalidOperationException("A deferred bill cannot be deferred again.");
        DeferredDebt = PaymentDue;
        PaymentDue = 0;
        BilledTransitDays = 0;
        BilledSalaries = 0;
        return TakeDueEvents();
    }

    private CampaignEvent[] TakeDueEvents()
    {
        var due = _events.Where(item => item.Date <= Date)
            .OrderBy(item => item.Date).ThenBy(item => Rank(item.Kind)).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        _events.RemoveAll(item => item.Date <= Date);
        return due;
    }

    public CampaignSnapshot Snapshot() => new(SaveVersion, Date, Cash, PaymentDue, DeferredDebt, NextQuarter, Bankrupt, _events.ToArray(), Roster?.Snapshot(), Missions.Snapshot(), Travel.Snapshot(), AccruedTransitDays, BilledTransitDays, Reputation.Snapshot(), PlayerNames?.ToArray(),
        _markets.Count == 0 ? null : [.. _markets.Values.OrderBy(item => item.SystemId, StringComparer.Ordinal).Select(item => item.Snapshot())], MarketSeed,
        BilledSalaries, Recruits.Snapshot());

    public static Campaign Restore(CampaignSnapshot save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var date = save.Date;
        var quarterStart = date == default ? StartDate : new DateOnly(date.Year, (date.Month - 1) / 3 * 3 + 1, 1);
        if (save.Version is < 1 or > SaveVersion || date < StartDate || date.Year >= 9999 || save.Cash < 0
            || save.DeferredDebt < 0
            || save.DeferredDebt != 0 && save.DeferredDebt < QuarterlyCharge
            || save.BilledSalaries < 0 || save.PaymentDue == 0 && save.BilledSalaries != 0
            || save.AccruedTransitDays < 0 || save.AccruedTransitDays > date.DayNumber - quarterStart.DayNumber
            || save.BilledTransitDays is < 0 or > 92
            || save.PaymentDue == 0 && save.BilledTransitDays != 0
            || save.PaymentDue > 0 && save.AccruedTransitDays != 0
            || save.PaymentDue != 0 && save.PaymentDue != QuarterlyCharge + save.DeferredDebt + save.BilledTransitDays * TransitDailyCharge + save.BilledSalaries
            || save.NextQuarter != quarterStart.AddMonths(3)
            || save.PaymentDue > 0 && (date.Day != 1 || (date.Month - 1) % 3 != 0 || date == StartDate)
            || save.Bankrupt != (save.PaymentDue > 0 && save.DeferredDebt > 0 && save.Cash < save.PaymentDue)
            || save.Events is null || save.Version == 1 && save.Roster is not null || save.Version >= 2 && save.Roster is null
            || save.Version < 3 && save.Missions is not null || save.Version >= 3 && save.Missions is null
            || save.Version >= 4 && save.Travel is null || save.Version >= 7 && save.Reputation is null)
            throw new ArgumentException("The campaign save contains invalid state.");

        var campaign = new Campaign
        {
            Date = date, Cash = save.Cash, PaymentDue = save.PaymentDue,
            DeferredDebt = save.DeferredDebt, NextQuarter = save.NextQuarter, Bankrupt = save.Bankrupt,
            AccruedTransitDays = save.AccruedTransitDays, BilledTransitDays = save.BilledTransitDays, BilledSalaries = save.BilledSalaries
        };
        if (save.Version >= 4) campaign.Travel = Mech.Travel.Restore(save.Travel!, StarChart.Instance);
        // Saves before version 5 counted whole stock points of armor and structure.
        if (save.Roster is not null) campaign.Roster = ForceRoster.Restore(save.Version < 5 ? ForceRoster.Rescale(save.Roster, CombatRules.DamageScale) : save.Roster, date);
        // Saves before version 7 had no reputation, so every faction starts neutral.
        if (save.Version >= 7) campaign.Reputation = Mech.Reputation.Restore(save.Reputation!);
        // Saves before version 6 held the single authored offer, and offers before version 8 had no employer or opponent;
        // they receive a freshly generated board.
        campaign.Missions = save.Version >= 8 ? MissionBoard.Restore(save.Missions!, date)
            : MissionBoard.Create(NewSeed(), date, campaign.MissionOrigin, campaign.Reputation, lances: campaign.Roster?.LanceCount ?? 1);
        if (campaign.Missions.Lances != (campaign.Roster?.LanceCount ?? 1)) throw new ArgumentException("The mission save contains invalid state.");
        if (save.PlayerNames is { } names)
        {
            if (campaign.Roster is null || names.Length != campaign.Roster.LanceCount || names.Length < 2 || names.Any(name => !ValidPlayerName(name)))
                throw new ArgumentException("The campaign save contains invalid players.");
            campaign.PlayerNames = names;
        }
        // Saves from before markets stock each market when first viewed.
        if (save.MarketSeed is { } seed) campaign.MarketSeed = seed;
        foreach (var saved in save.Markets ?? [])
        {
            var market = SystemMarket.Restore(saved, StarChart.Instance, date);
            if (!campaign._markets.TryAdd(market.SystemId, market)) throw new ArgumentException("The campaign save contains an invalid market.");
        }
        campaign.Recruits = Recruitment.Restore(save.Recruitment, StarChart.Instance, date, campaign.Roster);
        foreach (var item in save.Events)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title)
                || item.Date < date || item.Date == date && !campaign.PaymentPending
                || item.Date.Year >= 9999 || !Enum.IsDefined(item.Kind)
                || campaign._events.Any(existing => existing.Id == item.Id))
                throw new ArgumentException("The campaign save contains an invalid event.");
            campaign._events.Add(item);
        }
        return campaign;
    }
}
