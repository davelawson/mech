using System.Globalization;

namespace Mech;

public enum CampaignEventKind { Arrival, RepairCompletion, MissionResult, ContractDeadline }

public sealed record CampaignEvent(string Id, DateOnly Date, CampaignEventKind Kind, string Title);
public sealed record DayResult(bool Advanced, bool QuarterReached, IReadOnlyList<CampaignEvent> Events);
public sealed record CampaignSnapshot(int Version, DateOnly Date, long Cash, long PaymentDue,
    long DeferredDebt, DateOnly NextQuarter, bool Bankrupt, CampaignEvent[] Events, RosterSnapshot? Roster = null, MissionSnapshot? Missions = null,
    TravelSnapshot? Travel = null, int AccruedTransitDays = 0, int BilledTransitDays = 0);

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
    public long ProjectedBill => QuarterlyCharge + DeferredDebt + AccruedTransitCost;
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
    public MissionBoard Missions { get; private set; } = new();
    public void EstablishCompany(InitialDraft draft)
    {
        if (!RequiresDraft) throw new InvalidOperationException("The initial draft is already complete.");
        Roster = draft.Establish();
    }
    public bool CanAdvance => !RequiresDraft && !PaymentPending && !Bankrupt && Date < new DateOnly(9998, 12, 31);
    public bool CanPay => !RequiresDraft && PaymentPending && !Bankrupt && Cash >= PaymentDue;
    public bool CanDefer => !RequiresDraft && PaymentPending && !Bankrupt && DeferredDebt == 0;
    public int DaysUntilQuarter => NextQuarter.DayNumber - Date.DayNumber;
    public string DateText => Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
    public DateOnly NextEventDate
    {
        get
        {
            var next = _events.Aggregate(NextQuarter, (date, item) => item.Date < date ? item.Date : date);
            return Travel.HasJourney && Travel.DaysToArrival < next.DayNumber - Date.DayNumber
                ? Date.AddDays(Travel.DaysToArrival) : next;
        }
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
        Roster?.Recover(Date);
        if (Travel.HasJourney)
        {
            AccruedTransitDays++;
            var arrived = Travel.AdvanceDay();
            if (arrived is not null) _events.Add(new($"travel-arrival-{Date.DayNumber}", Date,
                CampaignEventKind.Arrival, $"Arrived at {StarChart.Instance[arrived].Name}. In orbit for today."));
        }
        var quarterReached = Date == NextQuarter;
        // Accounting is evaluated before any other event on the same date.
        if (quarterReached)
        {
            BilledTransitDays = AccruedTransitDays;
            AccruedTransitDays = 0;
            PaymentDue = QuarterlyCharge + DeferredDebt + BilledTransitCost;
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
        DeferredDebt = 0;
        return TakeDueEvents();
    }

    public IReadOnlyList<CampaignEvent> DeferPayment()
    {
        if (!CanDefer) throw new InvalidOperationException("A deferred bill cannot be deferred again.");
        DeferredDebt = PaymentDue;
        PaymentDue = 0;
        BilledTransitDays = 0;
        return TakeDueEvents();
    }

    private CampaignEvent[] TakeDueEvents()
    {
        var due = _events.Where(item => item.Date <= Date)
            .OrderBy(item => item.Date).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        _events.RemoveAll(item => item.Date <= Date);
        return due;
    }

    public CampaignSnapshot Snapshot() => new(4, Date, Cash, PaymentDue, DeferredDebt, NextQuarter, Bankrupt, _events.ToArray(), Roster?.Snapshot(), Missions.Snapshot(), Travel.Snapshot(), AccruedTransitDays, BilledTransitDays);

    public static Campaign Restore(CampaignSnapshot save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var date = save.Date;
        var quarterStart = date == default ? StartDate : new DateOnly(date.Year, (date.Month - 1) / 3 * 3 + 1, 1);
        if (save.Version is not (1 or 2 or 3 or 4) || date < StartDate || date.Year >= 9999 || save.Cash < 0
            || save.DeferredDebt < 0 || save.DeferredDebt > 284_000
            || save.DeferredDebt != 0 && (save.DeferredDebt < QuarterlyCharge || save.DeferredDebt % TransitDailyCharge != 0)
            || save.AccruedTransitDays < 0 || save.AccruedTransitDays > date.DayNumber - quarterStart.DayNumber
            || save.BilledTransitDays is < 0 or > 92
            || save.PaymentDue == 0 && save.BilledTransitDays != 0
            || save.PaymentDue > 0 && save.AccruedTransitDays != 0
            || save.PaymentDue != 0 && save.PaymentDue != QuarterlyCharge + save.DeferredDebt + save.BilledTransitDays * TransitDailyCharge
            || save.NextQuarter != quarterStart.AddMonths(3)
            || save.PaymentDue > 0 && (date.Day != 1 || (date.Month - 1) % 3 != 0 || date == StartDate)
            || save.Bankrupt != (save.PaymentDue > 0 && save.DeferredDebt > 0 && save.Cash < save.PaymentDue)
            || save.Events is null || save.Version == 1 && save.Roster is not null || save.Version >= 2 && save.Roster is null
            || save.Version < 3 && save.Missions is not null || save.Version >= 3 && save.Missions is null
            || save.Version == 4 && save.Travel is null)
            throw new ArgumentException("The campaign save contains invalid state.");

        var campaign = new Campaign
        {
            Date = date, Cash = save.Cash, PaymentDue = save.PaymentDue,
            DeferredDebt = save.DeferredDebt, NextQuarter = save.NextQuarter, Bankrupt = save.Bankrupt,
            AccruedTransitDays = save.AccruedTransitDays, BilledTransitDays = save.BilledTransitDays
        };
        if (save.Version >= 4) campaign.Travel = Mech.Travel.Restore(save.Travel!, StarChart.Instance);
        if (save.Roster is not null) campaign.Roster = ForceRoster.Restore(save.Roster, date);
        if (save.Missions is not null) campaign.Missions = MissionBoard.Restore(save.Missions);
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
