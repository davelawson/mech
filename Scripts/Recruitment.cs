namespace Mech;

/// <summary>What MechWarriors cost and how often they come and go from systems' hiring lists.</summary>
public static class PilotTuning
{
    public const long SalaryPerSkillPoint = 5_000;
    /// <summary>Hiring costs are rounded up to this.</summary>
    public const long HireRounding = 1_000;
    /// <summary>A quarter's salary per net skill point spent: Green 20,000, Regular 40,000, Veteran 50,000.</summary>
    public static long Salary(WarriorSpec warrior) => Math.Max(0, warrior.SkillPoints) * SalaryPerSkillPoint;
    /// <summary>The chance each empty slot of a system's hiring list fills when it rolls.</summary>
    public const double SlotFillChance = 2.0 / 3;
    /// <summary>The chance, each day, that a listed MechWarrior leaves the list.</summary>
    public const double DailyDeparture = .05;
    /// <summary>Days after leaving a list before a MechWarrior can be listed anywhere again.</summary>
    public const int RelistDays = 7;
    /// <summary>Slots on a hiring list: none on an uninhabited world, one below 100 million people, two below a billion, and three from a
    /// billion up.</summary>
    public static int Slots(long population) => population <= 0 ? 0 : population < 100_000_000 ? 1 : population < 1_000_000_000 ? 2 : 3;
    public static int Slots(StarSystem system) => Slots(system.Climate?.Population ?? 0);
}

/// <summary>What hiring a MechWarrior costs today: the rest of this quarter's salary, prorated by days, plus a signing bonus of one
/// quarter's salary.</summary>
public sealed record HireCost(long Salary, long Prorated, int DaysLeft, int QuarterDays)
{
    public long SigningBonus => Salary;
    public long Total => Prorated + SigningBonus;
}

/// <summary>A saved hiring list: who it lists, the day its departures were last worked out, and the day its empty slots last rolled.</summary>
public sealed record RecruitListSnapshot(string SystemId, DateOnly Refreshed, DateOnly Rolled, string[] Warriors);
/// <summary>A MechWarrior who left a hiring list, and when; they are listed nowhere for <see cref="PilotTuning.RelistDays"/> days.</summary>
public sealed record RecruitDeparture(string WarriorId, DateOnly Left);
public sealed record RecruitmentSnapshot(RecruitListSnapshot[] Lists, RecruitDeparture[] Departed, string[] Dismissed);

/// <summary>Every system's hiring list, as last seen. A list is made on the first visit, and its empty slots roll on each arrival and
/// each day the DropShip stays in orbit. Listed MechWarriors leave at <see cref="PilotTuning.DailyDeparture"/> a day, worked out for
/// the days away too. A MechWarrior is on one list at most, and dismissed ones are never listed again.</summary>
public sealed class Recruitment
{
    private sealed class RecruitList(string systemId, DateOnly refreshed, DateOnly rolled, IEnumerable<string> warriors)
    {
        public string SystemId { get; } = systemId;
        public DateOnly Refreshed { get; set; } = refreshed;
        public DateOnly Rolled { get; set; } = rolled;
        public List<string> Warriors { get; } = [.. warriors];
    }
    private readonly Dictionary<string, RecruitList> _lists = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateOnly> _departed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dismissed = new(StringComparer.Ordinal);

    /// <summary>The MechWarriors listed at a system, as last brought up to date; empty before its first visit.</summary>
    public IReadOnlyList<WarriorSpec> Listed(string systemId) =>
        _lists.TryGetValue(systemId, out var list) ? [.. list.Warriors.Select(DraftCatalog.Warrior)] : [];
    public bool Visited(string systemId) => _lists.ContainsKey(systemId);
    public bool IsDismissed(string warriorId) => _dismissed.Contains(warriorId);

    /// <summary>Brings every list's departures up to <paramref name="date"/>, then rolls the empty slots of the orbited system's list,
    /// making it on a first visit. A list already rolled today does not roll again.</summary>
    internal void Refresh(StarSystem system, DateOnly date, ForceRoster roster, Func<string, DateOnly, int, Random> random)
    {
        var slots = PilotTuning.Slots(system);
        if (slots == 0) return;
        foreach (var list in _lists.Values) Settle(list, date, random);
        foreach (var (id, left) in _departed.ToArray()) if (left.AddDays(PilotTuning.RelistDays) <= date) _departed.Remove(id);
        if (_lists.TryGetValue(system.Id, out var orbit) && orbit.Rolled >= date) return;
        if (orbit is null) _lists[system.Id] = orbit = new(system.Id, date, date, []);
        orbit.Rolled = date;
        var roll = random(system.Id, date, 1);
        for (var slot = orbit.Warriors.Count; slot < slots; slot++)
        {
            if (roll.NextDouble() >= PilotTuning.SlotFillChance) continue;
            var eligible = Eligible(roster).ToArray();
            if (eligible.Length == 0) break;
            orbit.Warriors.Add(eligible[roll.Next(eligible.Length)]);
        }
    }

    /// <summary>Catalog MechWarriors who could be listed: not in the company, not dismissed, on no list and not recently departed.</summary>
    private IEnumerable<string> Eligible(ForceRoster roster)
    {
        var listed = _lists.Values.SelectMany(list => list.Warriors).ToHashSet(StringComparer.Ordinal);
        return DraftCatalog.Warriors.Select(item => item.Id).Where(id => roster.Warriors.All(item => item.Id != id)
            && !_dismissed.Contains(id) && !listed.Contains(id) && !_departed.ContainsKey(id));
    }

    /// <summary>Each day since the list was last seen, each listed MechWarrior may leave.</summary>
    private void Settle(RecruitList list, DateOnly date, Func<string, DateOnly, int, Random> random)
    {
        for (var day = list.Refreshed.AddDays(1); day <= date && list.Warriors.Count > 0; day = day.AddDays(1))
        {
            var roll = random(list.SystemId, day, 0);
            foreach (var id in list.Warriors.ToArray())
                if (roll.NextDouble() < PilotTuning.DailyDeparture) { list.Warriors.Remove(id); _departed[id] = day; }
        }
        if (list.Refreshed < date) list.Refreshed = date;
    }

    /// <summary>A hired MechWarrior leaves the list for the company.</summary>
    internal void Hire(string systemId, string warriorId)
    {
        if (!_lists.TryGetValue(systemId, out var list) || !list.Warriors.Remove(warriorId))
            throw new InvalidOperationException("That MechWarrior is not for hire here.");
    }
    internal void Dismiss(string warriorId) => _dismissed.Add(warriorId);
    /// <summary>Replaces a system's list with these MechWarriors, rolled today, as a test fixture.</summary>
    internal void Place(string systemId, DateOnly date, IEnumerable<string> warriorIds)
    {
        foreach (var list in _lists.Values) list.Warriors.RemoveAll(warriorIds.Contains);
        foreach (var id in warriorIds) _departed.Remove(id);
        _lists[systemId] = new(systemId, date, date, warriorIds);
    }

    internal RecruitmentSnapshot? Snapshot() => _lists.Count == 0 && _dismissed.Count == 0 ? null : new(
        [.. _lists.Values.OrderBy(item => item.SystemId, StringComparer.Ordinal).Select(item => new RecruitListSnapshot(item.SystemId, item.Refreshed, item.Rolled, [.. item.Warriors]))],
        [.. _departed.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new RecruitDeparture(item.Key, item.Value))],
        [.. _dismissed.Order(StringComparer.Ordinal)]);

    internal static Recruitment Restore(RecruitmentSnapshot? save, StarChart chart, DateOnly date, ForceRoster? roster)
    {
        var recruitment = new Recruitment();
        if (save is null) return recruitment;
        if (save.Lists is null || save.Departed is null || save.Dismissed is null) throw new ArgumentException("Invalid hiring lists.");
        bool Known(string? id) => id is not null && DraftCatalog.Warriors.Any(item => item.Id == id);
        bool Hired(string id) => roster?.Warriors.Any(item => item.Id == id) == true;
        foreach (var id in save.Dismissed)
            if (!Known(id) || Hired(id) || !recruitment._dismissed.Add(id)) throw new ArgumentException("Invalid hiring lists.");
        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in save.Lists)
        {
            if (list?.SystemId is null || !chart.Contains(list.SystemId) || list.Refreshed > date || list.Rolled > list.Refreshed || list.Warriors is null
                || list.Warriors.Length > PilotTuning.Slots(chart[list.SystemId]) || PilotTuning.Slots(chart[list.SystemId]) == 0
                || list.Warriors.Any(id => !Known(id) || Hired(id) || recruitment._dismissed.Contains(id) || !listed.Add(id))
                || !recruitment._lists.TryAdd(list.SystemId, new(list.SystemId, list.Refreshed, list.Rolled, list.Warriors)))
                throw new ArgumentException("Invalid hiring lists.");
        }
        foreach (var departure in save.Departed)
        {
            if (departure is null || !Known(departure.WarriorId) || departure.Left > date || listed.Contains(departure.WarriorId)
                || !recruitment._departed.TryAdd(departure.WarriorId, departure.Left))
                throw new ArgumentException("Invalid hiring lists.");
        }
        return recruitment;
    }
}
