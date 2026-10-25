namespace Mech;

public sealed record TravelSnapshot(string OrbitSystemId, string[]? Candidate, string[]? Journey,
    int LegIndex, int DaysRemaining);

/// <summary>An embarked journey stays in orbit until a day advances; every arrival permits replanning.</summary>
public sealed class Travel
{
    private readonly StarChart _chart;
    private string[]? _candidate;
    private string[]? _journey;
    public string OrbitSystemId { get; private set; }
    public IReadOnlyList<string>? Candidate => _candidate is null ? null : Array.AsReadOnly(_candidate);
    public IReadOnlyList<string>? Journey => _journey is null ? null : Array.AsReadOnly(_journey);
    public int LegIndex { get; private set; }
    public int DaysRemaining { get; private set; }
    public bool InOrbit => DaysRemaining == 0;
    public bool HasJourney => _journey is not null;
    public string? NextSystemId => _journey?[LegIndex + 1];
    public int DaysToArrival => !HasJourney ? 0 : InOrbit
        ? _chart.JumpDays(OrbitSystemId, NextSystemId!) : DaysRemaining;
    public int JourneyDaysRemaining => !HasJourney ? 0 : DaysToArrival
        + Enumerable.Range(LegIndex + 2, _journey!.Length - LegIndex - 2)
            .Sum(i => _chart.JumpDays(_journey[i - 1], _journey[i]));
    public string LocationText => InOrbit ? $"In orbit · {_chart[OrbitSystemId].Name}"
        : $"In transit · {_chart[OrbitSystemId].Name} → {_chart[NextSystemId!].Name} · {DaysRemaining}d remaining";

    public Travel(StarChart chart, string? startingSystem = null)
    {
        _chart = chart;
        OrbitSystemId = startingSystem ?? chart.StartingSystemId;
        _ = chart[OrbitSystemId];
    }

    public bool Plot(string destination)
    {
        if (!InOrbit) throw new InvalidOperationException("Routes can only be plotted in orbit.");
        _candidate = _chart.Plot(OrbitSystemId, destination);
        return _candidate is not null;
    }
    public void Embark()
    {
        if (!InOrbit || _candidate is null) throw new InvalidOperationException("Select a route while in orbit first.");
        _journey = (string[])_candidate.Clone();
        _candidate = null; LegIndex = 0; DaysRemaining = 0;
    }
    public void Cancel()
    {
        if (!InOrbit || !HasJourney) throw new InvalidOperationException("An embarked journey can only be cancelled in orbit.");
        _journey = null; LegIndex = 0;
    }

    // Returns the arrived system, if any. The caller accrues one transit day whenever HasJourney was true.
    internal string? AdvanceDay()
    {
        if (_journey is null) return null;
        if (InOrbit)
        {
            _candidate = null;
            DaysRemaining = _chart.JumpDays(OrbitSystemId, NextSystemId!);
        }
        DaysRemaining--;
        if (DaysRemaining > 0) return null;
        OrbitSystemId = NextSystemId!;
        LegIndex++;
        if (LegIndex == _journey.Length - 1) { _journey = null; LegIndex = 0; }
        return OrbitSystemId;
    }

    public TravelSnapshot Snapshot() => new(OrbitSystemId, _candidate?.ToArray(), _journey?.ToArray(), LegIndex, DaysRemaining);
    public static Travel Restore(TravelSnapshot save, StarChart chart)
    {
        if (save is null || save.OrbitSystemId is null || !chart.Contains(save.OrbitSystemId)
            || save.Candidate is not null && (!chart.ValidRoute(save.Candidate) || save.Candidate[0] != save.OrbitSystemId)
            || save.Journey is null && (save.LegIndex != 0 || save.DaysRemaining != 0)
            || save.Journey is not null && (!chart.ValidRoute(save.Journey) || save.LegIndex < 0
                || save.LegIndex >= save.Journey.Length - 1 || save.Journey[save.LegIndex] != save.OrbitSystemId
                || save.DaysRemaining < 0 || save.DaysRemaining >= chart.JumpDays(save.OrbitSystemId, save.Journey[save.LegIndex + 1]))
            || save.DaysRemaining > 0 && save.Candidate is not null)
            throw new ArgumentException("The campaign save contains invalid travel state.");
        return new Travel(chart, save.OrbitSystemId)
        {
            _candidate = save.Candidate?.ToArray(), _journey = save.Journey?.ToArray(),
            LegIndex = save.LegIndex, DaysRemaining = save.DaysRemaining
        };
    }
}
