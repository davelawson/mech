using System.Globalization;

namespace Mech;

/// <summary>The kinds of moment a MechWarrior's service history records (see docs/design.md, "Service history").</summary>
public enum ServiceEventKind { Founded, Hired, Deployed, Kill, Ace, Ejected, MechLost, Injured, Recovered, ComradeKilled, SkillAdvanced, Promoted, Killed }
/// <summary>How a hit took an enemy out of the fight, or killed a MechWarrior: the mech destroyed or disabled, or the pilot killed by
/// a strike to the cockpit or the loss of the head.</summary>
public enum KillManner { Destroyed, Disabled, Cockpit, Head }
/// <summary>A battle injury other than ejecting.</summary>
public enum InjuryCause { AmmoExplosion, LifeSupport }

/// <summary>One moment in a MechWarrior's service history, holding the names it needs as they were when it happened; <see
/// cref="ServiceRecord.Describe"/> words it. Place is the mission it happened in, the company founded or the system hired at. Mech is
/// the MechWarrior's own BattleMech, Enemy the enemy's. Days is the recovery time an injury or ejection set, or that a recovery took.</summary>
public sealed record ServiceEvent(DateOnly Date, ServiceEventKind Kind)
{
    public string? Place { get; init; }
    public string? Mech { get; init; }
    /// <summary>A deployment's lance, when it differs from the MechWarrior's previous deployment; Rejoined when they had fought with it before.</summary>
    public int? Lance { get; init; }
    public bool Rejoined { get; init; }
    public string? Enemy { get; init; }
    public string? Weapon { get; init; }
    public KillManner? Manner { get; init; }
    /// <summary>The ammunition whose explosion killed the MechWarrior.</summary>
    public string? Exploded { get; init; }
    /// <summary>The fallen comrade, named as "“Ace” Jane Doe".</summary>
    public string? Comrade { get; init; }
    public int Days { get; init; }
    /// <summary>An ejection's mech was destroyed: as the pilot ejected (AsDestroyed), or afterwards.</summary>
    public bool Destroyed { get; init; }
    public bool AsDestroyed { get; init; }
    public InjuryCause[]? Causes { get; init; }
    public WarriorSkill? Skill { get; init; }
    public int? Rating { get; init; }
    public WarriorExperience? Rank { get; init; }
}

/// <summary>An enemy BattleMech a MechWarrior's hit took out of the fight, or whose MechWarrior it killed. Credited kills are the ones
/// the Kills record counts; killing the MechWarrior of a mech already out of the fight is not credited.</summary>
public sealed record BattleKill(double Time, string Enemy, string Weapon, KillManner Manner, bool Credited = true);
/// <summary>How a friendly MechWarrior died: the enemy mech and weapon whose hit killed them, by cockpit or head, or the ammunition that
/// exploded after it. Enemy and Weapon are null when no enemy hit is to blame.</summary>
public sealed record BattleDeath(double Time, string? Enemy, string? Weapon, KillManner? Manner, string? Exploded = null);

public static class ServiceRecord
{
    /// <summary>The kill that makes a MechWarrior an ace.</summary>
    public const int AceKills = 5;

    public static string DateText(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

    /// <summary>"No service record before 25 NOV 3025": the note on a MechWarrior from a save made before service histories.</summary>
    public static string EarlierNote(DateOnly since) => $"No service record before {DateText(since)}";

    /// <summary>One sentence for the event, as the rolodex's History tab shows it.</summary>
    public static string Describe(ServiceEvent item)
    {
        var place = item.Place ?? "an unrecorded mission";
        var recover = item.Days > 0 ? $"; {Days(item.Days)} to recover" : "";
        return item.Kind switch
        {
            ServiceEventKind.Founded => $"Founding member of {item.Place}.",
            ServiceEventKind.Hired => $"Hired at {item.Place}.",
            ServiceEventKind.Deployed => item.Mech is { } mech
                ? $"Took the {mech} into {place}{(item.Lance is { } lance ? $" with {ForceRoster.LanceName(lance)}" : "")}."
                : item.Rejoined ? $"Rejoined {ForceRoster.LanceName(item.Lance ?? 0)} for {place}."
                : $"Fought with {ForceRoster.LanceName(item.Lance ?? 0)} for the first time in {place}.",
            ServiceEventKind.Kill => item.Manner switch
            {
                KillManner.Cockpit => $"Killed the MechWarrior of an enemy {item.Enemy} with {Article(item.Weapon)} strike to the cockpit during {place}.",
                KillManner.Head => $"Killed the MechWarrior of an enemy {item.Enemy} by destroying the head with {Article(item.Weapon)} during {place}.",
                KillManner.Disabled => $"Disabled an enemy {item.Enemy} with {Article(item.Weapon)} during {place}.",
                _ => $"Destroyed an enemy {item.Enemy} with {Article(item.Weapon)} during {place}.",
            },
            ServiceEventKind.Ace => "Became an ace with their fifth kill.",
            ServiceEventKind.Ejected => item.AsDestroyed ? $"Ejected as their {item.Mech} was destroyed during {place}{recover}."
                : item.Destroyed ? $"Ejected from their {item.Mech} during {place}, and it was later destroyed{recover}."
                : $"Ejected from their {item.Mech} during {place}{recover}.",
            ServiceEventKind.MechLost => $"Their {item.Mech} was destroyed during {place}; they walked away.",
            ServiceEventKind.Injured => $"Injured{Causes(item.Causes)} during {place}{recover}.",
            ServiceEventKind.Recovered => item.Days > 0 ? $"Returned to duty after {Days(item.Days)}' recovery." : "Returned to duty.",
            ServiceEventKind.ComradeKilled => $"Saw {item.Comrade} killed during {place}.",
            ServiceEventKind.SkillAdvanced => $"{item.Skill} advanced to {WarriorSkills.Descriptor(item.Rating ?? 7)}.",
            ServiceEventKind.Promoted => $"Promoted to {WarriorSkills.RankName(item.Rank ?? WarriorExperience.Private)}.",
            ServiceEventKind.Killed => item.Enemy is not { } enemy || item.Weapon is not { } weapon ? $"Killed during {place}."
                : item.Exploded is { } bin ? $"Killed during {place} when their {bin} exploded after a hit from an enemy {enemy}'s {weapon}."
                : item.Manner == KillManner.Head ? $"Killed during {place} when an enemy {enemy}'s {weapon} destroyed the head."
                : $"Killed during {place} when an enemy {enemy}'s {weapon} struck the cockpit.",
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };
    }

    private static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    /// <summary>" by an ammunition explosion", " by two ammunition explosions and overheating with damaged life support".</summary>
    private static string Causes(InjuryCause[]? causes)
    {
        var parts = new List<string>();
        var explosions = causes?.Count(item => item == InjuryCause.AmmoExplosion) ?? 0;
        if (explosions > 0) parts.Add(explosions == 1 ? "an ammunition explosion" : $"{Count(explosions)} ammunition explosions");
        if (causes?.Contains(InjuryCause.LifeSupport) == true) parts.Add("overheating with damaged life support");
        return parts.Count == 0 ? "" : " by " + string.Join(" and ", parts);
    }
    private static string Count(int count) => count switch { 2 => "two", 3 => "three", 4 => "four", _ => count.ToString(CultureInfo.InvariantCulture) };

    /// <summary>"a Medium Laser", "a PPC", "an LRM 20", "an AC/10": an abbreviation takes the article its first letter is spoken with.</summary>
    public static string Article(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "a weapon";
        var word = name.Split(' ', '/')[0];
        var spelled = word.Length > 1 && word.All(char.IsUpper);
        var vowel = spelled ? "AEFHILMNORSX".Contains(word[0]) : "AEIOU".Contains(char.ToUpperInvariant(word[0]));
        return $"{(vowel ? "an" : "a")} {name}";
    }

    /// <summary>Whether a saved event could have been recorded: a known kind, no later than the save's date, with what its sentence names.</summary>
    internal static bool Valid(ServiceEvent? item, DateOnly today) => item is not null && Enum.IsDefined(item.Kind) && item.Date <= today && item.Days >= 0
        && (item.Manner is not { } manner || Enum.IsDefined(manner)) && (item.Causes?.All(Enum.IsDefined) ?? true)
        && (item.Skill is not { } skill || Enum.IsDefined(skill)) && (item.Rank is not { } rank || Enum.IsDefined(rank))
        && (item.Lance is not { } lance || lance is >= 0 and < ForceRoster.MaxLances) && (item.Rating is not { } rating || rating is >= 2 and <= 7)
        && item.Kind switch
        {
            ServiceEventKind.Founded or ServiceEventKind.Hired => !string.IsNullOrWhiteSpace(item.Place),
            ServiceEventKind.Deployed => item.Mech is not null || item.Lance is not null,
            ServiceEventKind.Kill => item.Enemy is not null && item.Manner is not null,
            ServiceEventKind.Ejected or ServiceEventKind.MechLost => item.Mech is not null,
            ServiceEventKind.ComradeKilled => item.Comrade is not null,
            ServiceEventKind.SkillAdvanced => item.Skill is not null && item.Rating is not null,
            ServiceEventKind.Promoted => item.Rank is not null,
            _ => true,
        };
}
