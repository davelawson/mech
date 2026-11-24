namespace Mech;

/// <summary>What MechWarriors cost and how often they come and go from systems' hiring lists.</summary>
public static class PilotTuning
{
    /// <summary>A quarter's salary for the first skill point spent; each later point costs <see cref="SalaryRamp"/> more than the one
    /// before it.</summary>
    public const long FirstPointSalary = 2_000, SalaryRamp = 400;
    /// <summary>Hiring costs are rounded up to this.</summary>
    public const long HireRounding = 1_000;
    /// <summary>A quarter's salary for n skill points spent, 2,000·n + 200·n·(n − 1): 4 points 10,400, 12 points 50,400, 24 points
    /// 158,400.</summary>
    public static long Salary(int points) => points <= 0 ? 0 : FirstPointSalary * points + SalaryRamp * points * (points - 1) / 2;
    public static long Salary(WarriorSpec warrior) => Salary(warrior.SkillPoints);
    /// <summary>A generated MechWarrior's skill points start here, and each roll adds one more at this chance until a roll fails or the
    /// cap is reached.</summary>
    public const int StartingPoints = 3, PointCap = 40;
    public const double ExtraPointChance = .83;
    /// <summary>Each raise multiplies the chance of that skill being picked again by this.</summary>
    public const double RaiseWeightFactor = 2.0 / 3;
    /// <summary>The chance each empty slot of a system's hiring list fills when it rolls.</summary>
    public const double SlotFillChance = 2.0 / 3;
    /// <summary>The chance, each day, that a listed MechWarrior leaves the list.</summary>
    public const double DailyDeparture = .05;
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
public sealed record RecruitListSnapshot(string SystemId, DateOnly Refreshed, DateOnly Rolled, WarriorSpec[] Warriors);
public sealed record RecruitmentSnapshot(RecruitListSnapshot[] Lists);

/// <summary>Every system's hiring list, as last seen. A list is made on the first visit, and its empty slots roll on each arrival and
/// each day the DropShip stays in orbit; each slot that fills generates a new MechWarrior. Listed MechWarriors leave at
/// <see cref="PilotTuning.DailyDeparture"/> a day, worked out for the days away too, and are gone for good.</summary>
public sealed class Recruitment
{
    private sealed class RecruitList(string systemId, DateOnly refreshed, DateOnly rolled, IEnumerable<WarriorSpec> warriors)
    {
        public string SystemId { get; } = systemId;
        public DateOnly Refreshed { get; set; } = refreshed;
        public DateOnly Rolled { get; set; } = rolled;
        public List<WarriorSpec> Warriors { get; } = [.. warriors];
    }
    private readonly Dictionary<string, RecruitList> _lists = new(StringComparer.Ordinal);

    /// <summary>The MechWarriors listed at a system, as last brought up to date; empty before its first visit.</summary>
    public IReadOnlyList<WarriorSpec> Listed(string systemId) => _lists.TryGetValue(systemId, out var list) ? [.. list.Warriors] : [];
    /// <summary>Every MechWarrior on any list.</summary>
    public IEnumerable<WarriorSpec> AllListed => _lists.Values.SelectMany(list => list.Warriors);
    public bool Visited(string systemId) => _lists.ContainsKey(systemId);

    /// <summary>Brings every list's departures up to <paramref name="date"/>, then rolls the empty slots of the orbited system's list,
    /// making it on a first visit. A list already rolled today does not roll again.</summary>
    internal void Refresh(StarSystem system, DateOnly date, ForceRoster roster, Func<string, DateOnly, int, Random> random)
    {
        var slots = PilotTuning.Slots(system);
        if (slots == 0) return;
        foreach (var list in _lists.Values) Settle(list, date, random);
        if (_lists.TryGetValue(system.Id, out var orbit) && orbit.Rolled >= date) return;
        if (orbit is null) _lists[system.Id] = orbit = new(system.Id, date, date, []);
        orbit.Rolled = date;
        var roll = random(system.Id, date, 1);
        var inUse = new PilotsInUse(roster.Warriors.Select(item => item.Spec).Concat(AllListed));
        for (var slot = orbit.Warriors.Count; slot < slots; slot++)
            if (roll.NextDouble() < PilotTuning.SlotFillChance) orbit.Warriors.Add(PilotGenerator.Generate(roll, inUse));
    }

    /// <summary>Each day since the list was last seen, each listed MechWarrior may leave.</summary>
    private static void Settle(RecruitList list, DateOnly date, Func<string, DateOnly, int, Random> random)
    {
        for (var day = list.Refreshed.AddDays(1); day <= date && list.Warriors.Count > 0; day = day.AddDays(1))
        {
            var roll = random(list.SystemId, day, 0);
            foreach (var warrior in list.Warriors.ToArray())
                if (roll.NextDouble() < PilotTuning.DailyDeparture) list.Warriors.Remove(warrior);
        }
        if (list.Refreshed < date) list.Refreshed = date;
    }

    /// <summary>A hired MechWarrior leaves the list for the company.</summary>
    internal WarriorSpec Hire(string systemId, string warriorId)
    {
        var warrior = _lists.TryGetValue(systemId, out var list) ? list.Warriors.SingleOrDefault(item => item.Id == warriorId) : null;
        if (warrior is null) throw new InvalidOperationException("That MechWarrior is not for hire here.");
        list!.Warriors.Remove(warrior);
        return warrior;
    }
    /// <summary>Replaces a system's list with these MechWarriors, rolled today, as a test fixture.</summary>
    internal void Place(string systemId, DateOnly date, IEnumerable<WarriorSpec> warriors)
    {
        var placed = warriors.ToArray();
        foreach (var list in _lists.Values) list.Warriors.RemoveAll(item => placed.Any(other => other.Id == item.Id));
        _lists[systemId] = new(systemId, date, date, placed);
    }

    internal RecruitmentSnapshot? Snapshot() => _lists.Count == 0 ? null : new(
        [.. _lists.Values.OrderBy(item => item.SystemId, StringComparer.Ordinal).Select(item => new RecruitListSnapshot(item.SystemId, item.Refreshed, item.Rolled, [.. item.Warriors]))]);

    internal static Recruitment Restore(RecruitmentSnapshot? save, StarChart chart, DateOnly date, ForceRoster? roster)
    {
        var recruitment = new Recruitment();
        if (save is null) return recruitment;
        if (save.Lists is null) throw new ArgumentException("Invalid hiring lists.");
        bool Hired(string id) => roster?.Warriors.Any(item => item.Id == id) == true;
        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in save.Lists)
        {
            if (list?.SystemId is null || !chart.Contains(list.SystemId) || list.Refreshed > date || list.Rolled > list.Refreshed || list.Warriors is null
                || list.Warriors.Length > PilotTuning.Slots(chart[list.SystemId]) || PilotTuning.Slots(chart[list.SystemId]) == 0
                || list.Warriors.Any(warrior => !PilotGenerator.Valid(warrior) || Hired(warrior.Id) || !listed.Add(warrior.Id))
                || !recruitment._lists.TryAdd(list.SystemId, new(list.SystemId, list.Refreshed, list.Rolled, list.Warriors)))
                throw new ArgumentException("Invalid hiring lists.");
        }
        return recruitment;
    }
}
