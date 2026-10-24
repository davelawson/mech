using System.Globalization;

namespace Mech;

public enum CampaignEventKind { Arrival, RepairCompletion, MissionResult, ContractDeadline }

public sealed record CampaignEvent(string Id, DateOnly Date, CampaignEventKind Kind, string Title);
public sealed record DayResult(bool Advanced, bool QuarterReached, IReadOnlyList<CampaignEvent> Events);
public sealed record CampaignSnapshot(int Version, DateOnly Date, long Cash, long PaymentDue,
    long DeferredDebt, DateOnly NextQuarter, bool Bankrupt, CampaignEvent[] Events);

/// <summary>Campaign time and obligations, independent of the scene tree and animation.</summary>
public sealed class Campaign
{
    public static readonly DateOnly StartDate = new(3025, 1, 1);
    public const long StartingCash = 2_500_000;
    public const long QuarterlyCharge = 100_000;
    private readonly List<CampaignEvent> _events = [];

    public DateOnly Date { get; private set; } = StartDate;
    public long Cash { get; private set; } = StartingCash;
    public long PaymentDue { get; private set; }
    public long DeferredDebt { get; private set; }
    public DateOnly NextQuarter { get; private set; } = new(3025, 4, 1);
    public bool Bankrupt { get; private set; }
    public bool PaymentPending => PaymentDue > 0;
    public bool CanAdvance => !PaymentPending && !Bankrupt && Date < new DateOnly(9998, 12, 31);
    public bool CanPay => PaymentPending && !Bankrupt && Cash >= PaymentDue;
    public bool CanDefer => PaymentPending && !Bankrupt && DeferredDebt == 0;
    public int DaysUntilQuarter => NextQuarter.DayNumber - Date.DayNumber;
    public string DateText => Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
    public DateOnly NextEventDate => _events.Aggregate(NextQuarter, (date, item) => item.Date < date ? item.Date : date);

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
        var quarterReached = Date == NextQuarter;
        // Accounting is evaluated before any other event on the same date.
        if (quarterReached)
        {
            PaymentDue = QuarterlyCharge + DeferredDebt;
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
        DeferredDebt = 0;
        return TakeDueEvents();
    }

    public IReadOnlyList<CampaignEvent> DeferPayment()
    {
        if (!CanDefer) throw new InvalidOperationException("A deferred bill cannot be deferred again.");
        DeferredDebt = PaymentDue;
        PaymentDue = 0;
        return TakeDueEvents();
    }

    private CampaignEvent[] TakeDueEvents()
    {
        var due = _events.Where(item => item.Date <= Date)
            .OrderBy(item => item.Date).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        _events.RemoveAll(item => item.Date <= Date);
        return due;
    }

    public CampaignSnapshot Snapshot() => new(1, Date, Cash, PaymentDue, DeferredDebt, NextQuarter, Bankrupt, _events.ToArray());

    public static Campaign Restore(CampaignSnapshot save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var date = save.Date;
        if (save.Version != 1 || date < StartDate || date.Year >= 9999 || save.Cash < 0
            || save.DeferredDebt is not (0 or QuarterlyCharge)
            || save.PaymentDue != 0 && save.PaymentDue != QuarterlyCharge + save.DeferredDebt
            || save.NextQuarter != new DateOnly(date.Year, (date.Month - 1) / 3 * 3 + 1, 1).AddMonths(3)
            || save.PaymentDue > 0 && (date.Day != 1 || (date.Month - 1) % 3 != 0 || date == StartDate)
            || save.Bankrupt != (save.PaymentDue > 0 && save.DeferredDebt > 0 && save.Cash < save.PaymentDue)
            || save.Events is null)
            throw new ArgumentException("The campaign save contains invalid state.");

        var campaign = new Campaign
        {
            Date = date, Cash = save.Cash, PaymentDue = save.PaymentDue,
            DeferredDebt = save.DeferredDebt, NextQuarter = save.NextQuarter, Bankrupt = save.Bankrupt
        };
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
