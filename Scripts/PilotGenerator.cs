using System.Collections.Immutable;
using System.Text.Json;

namespace Mech;

/// <summary>Whose face a portrait shows, so a generated MechWarrior's first name matches it. Either suits a portrait that could be
/// anyone's.</summary>
public enum PortraitGender { Man, Woman, Either }

public sealed record PilotPortrait(string Id, PortraitGender Gender);

/// <summary>What MechWarriors are generated from: the portraits in Assets/Roster and the name and callsign lists, from
/// Data/pilots.json.</summary>
public sealed record PilotData(ImmutableArray<PilotPortrait> Portraits, ImmutableArray<string> MaleNames, ImmutableArray<string> FemaleNames,
    ImmutableArray<string> Surnames, ImmutableArray<string> Callsigns)
{
    public static PilotData Instance { get; } = Load();

    private sealed record Source(SourcePortrait[]? Portraits, string[]? MaleNames, string[]? FemaleNames, string[]? Surnames, string[]? Callsigns);
    private sealed record SourcePortrait(string? Id, string? Gender);

    private static PilotData Load()
    {
        using var stream = typeof(PilotData).Assembly.GetManifestResourceStream("Mech.Data.pilots.json")
            ?? throw new InvalidOperationException("The MechWarrior name lists are missing.");
        return Read(stream);
    }

    internal static PilotData Read(Stream stream)
    {
        var source = JsonSerializer.Deserialize<Source>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid pilots: empty file.");
        var portraits = (source.Portraits ?? throw Invalid("portraits", "missing")).Select(item =>
            item?.Id is { Length: > 0 } id && Enum.TryParse<PortraitGender>(item.Gender, true, out var gender)
                ? new PilotPortrait(id, gender) : throw Invalid("portraits", "each needs an id and a gender of man, woman or either")).ToImmutableArray();
        return Validate(new(portraits, List(source.MaleNames, "maleNames"), List(source.FemaleNames, "femaleNames"),
            List(source.Surnames, "surnames"), List(source.Callsigns, "callsigns")));
    }

    internal static PilotData Validate(PilotData data)
    {
        if (data.Portraits.IsEmpty || data.Portraits.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != data.Portraits.Length)
            throw Invalid("portraits", "must be distinct and not empty");
        foreach (var (list, name) in new[] { (data.MaleNames, "maleNames"), (data.FemaleNames, "femaleNames"), (data.Surnames, "surnames"), (data.Callsigns, "callsigns") })
            if (list.IsEmpty || list.Any(string.IsNullOrWhiteSpace) || list.Distinct(StringComparer.Ordinal).Count() != list.Length)
                throw Invalid(name, "must be distinct and not empty");
        return data;
    }

    private static ImmutableArray<string> List(string[]? values, string name) => values?.Select(item => item?.Trim() ?? "").ToImmutableArray()
        ?? throw Invalid(name, "missing");
    private static InvalidOperationException Invalid(string path, string message) => new($"Invalid pilots {path}: {message}.");

    public bool HasPortrait(string id) => Portraits.Any(item => item.Id == id);
}

/// <summary>The MechWarriors who exist now (on the roster, living or dead, on hiring lists and among draft candidates), so a new one
/// gets a portrait, name and callsign of their own while any are left.</summary>
public sealed class PilotsInUse
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal), _portraits = new(StringComparer.Ordinal),
        _names = new(StringComparer.Ordinal), _callsigns = new(StringComparer.Ordinal);

    public PilotsInUse(IEnumerable<WarriorSpec>? warriors = null) { foreach (var warrior in warriors ?? []) Add(warrior); }
    public void Add(WarriorSpec warrior)
    {
        _ids.Add(warrior.Id); _portraits.Add(warrior.Portrait); _names.Add(warrior.Name); _callsigns.Add(warrior.Callsign);
    }
    public bool HasId(string id) => _ids.Contains(id);
    public bool HasPortrait(string id) => _portraits.Contains(id);
    public bool HasName(string name) => _names.Contains(name);
    public bool HasCallsign(string callsign) => _callsigns.Contains(callsign);
}

/// <summary>Makes a MechWarrior when one is needed: a free portrait, a name to match and a callsign, and skills bought one level at a
/// time from a rolled number of points.</summary>
public static class PilotGenerator
{
    private const int NameAttempts = 64;
    private const int Skills = 4;

    /// <summary>A new MechWarrior, recorded in <paramref name="inUse"/>. A draft pool passes its rank, which starts the roll at the
    /// bottom of the rank's bracket and caps it at the top; spending can still stop short, so the pilot may come out a rank lower.
    /// Without a rank, the roll starts at <see cref="PilotTuning.StartingPoints"/> and is capped at <see cref="PilotTuning.PointCap"/>.</summary>
    public static WarriorSpec Generate(Random random, PilotsInUse inUse, WarriorExperience? pool = null, PilotData? data = null)
    {
        data ??= PilotData.Instance;
        var free = data.Portraits.Where(item => !inUse.HasPortrait(item.Id)).ToArray();
        var portrait = Pick(random, free.Length > 0 ? free : [.. data.Portraits]);
        var gender = portrait.Gender == PortraitGender.Either ? (random.Next(2) == 0 ? PortraitGender.Man : PortraitGender.Woman) : portrait.Gender;
        var firstNames = gender == PortraitGender.Man ? data.MaleNames : data.FemaleNames;
        var name = "";
        for (var attempt = 0; attempt < NameAttempts; attempt++)
        {
            name = $"{Pick(random, firstNames)} {Pick(random, data.Surnames)}";
            if (!inUse.HasName(name)) break;
        }
        var callsigns = data.Callsigns.Where(item => !inUse.HasCallsign(item)).ToArray();
        var callsign = Pick(random, callsigns.Length > 0 ? callsigns : [.. data.Callsigns]);
        string id;
        do id = $"pilot-{random.NextInt64():x16}"; while (inUse.HasId(id));
        var (start, cap) = pool is { } rank ? WarriorSkills.Bracket(rank) : (PilotTuning.StartingPoints, PilotTuning.PointCap);
        var skills = BuySkills(random, RollPoints(random, Math.Max(start, PilotTuning.StartingPoints), cap));
        var warrior = new WarriorSpec(id, name, callsign, portrait.Id, skills[0], skills[1], skills[2], skills[3]);
        inUse.Add(warrior);
        return warrior;
    }

    /// <summary>Starts at <paramref name="start"/> and adds a point at <see cref="PilotTuning.ExtraPointChance"/> each roll, until a roll
    /// fails or <paramref name="cap"/> is reached.</summary>
    public static int RollPoints(Random random, int start, int cap)
    {
        var points = Math.Min(start, cap);
        while (points < cap && random.NextDouble() < PilotTuning.ExtraPointChance) points++;
        return points;
    }

    /// <summary>Gunnery, Piloting, Sensors and Grit, each starting Untrained. A skill is picked by weight and raised one level at that
    /// level's cost; each raise multiplies its weight by <see cref="PilotTuning.RaiseWeightFactor"/>, and a skill at 2 is no longer
    /// picked. Buying stops when the picked skill costs more than the points left; the rest are lost.</summary>
    public static int[] BuySkills(Random random, int points)
    {
        var ratings = Enumerable.Repeat(WarriorSkills.Untrained, Skills).ToArray();
        var weights = Enumerable.Repeat(1.0, Skills).ToArray();
        while (true)
        {
            var open = Enumerable.Range(0, Skills).Where(i => ratings[i] > WarriorSkills.Expert).ToArray();
            if (open.Length == 0) break;
            var roll = random.NextDouble() * open.Sum(i => weights[i]);
            var skill = open[^1];
            foreach (var i in open) { if (roll < weights[i]) { skill = i; break; } roll -= weights[i]; }
            var cost = WarriorSkills.RaiseCost(ratings[skill]);
            if (cost > points) break;
            points -= cost; ratings[skill]--; weights[skill] *= PilotTuning.RaiseWeightFactor;
        }
        return ratings;
    }

    /// <summary>Whether a MechWarrior from a save or another player is well formed: named, skilled 2–7, with a known portrait.</summary>
    public static bool Valid(WarriorSpec? warrior, PilotData? data = null) => warrior is not null
        && !string.IsNullOrWhiteSpace(warrior.Id) && !string.IsNullOrWhiteSpace(warrior.Name) && !string.IsNullOrWhiteSpace(warrior.Callsign)
        && warrior.Portrait is not null && (data ?? PilotData.Instance).HasPortrait(warrior.Portrait)
        && new[] { warrior.Gunnery, warrior.Piloting, warrior.Sensors, warrior.Grit }.All(v => v is >= WarriorSkills.Expert and <= WarriorSkills.Untrained);

    private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];
}
