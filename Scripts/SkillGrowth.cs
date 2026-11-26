namespace Mech;

public enum WarriorSkill { Gunnery, Piloting, Sensors, Grit }

/// <summary>Successful skill gain checks a MechWarrior has banked toward each skill's next level.</summary>
public sealed record SkillProgress(int Gunnery = 0, int Piloting = 0, int Sensors = 0, int Grit = 0)
{
    public static readonly SkillProgress None = new();
    public int this[WarriorSkill skill] => skill switch
    {
        WarriorSkill.Gunnery => Gunnery, WarriorSkill.Piloting => Piloting, WarriorSkill.Sensors => Sensors, WarriorSkill.Grit => Grit,
        _ => throw new ArgumentOutOfRangeException(nameof(skill)),
    };
    public SkillProgress With(WarriorSkill skill, int value) => skill switch
    {
        WarriorSkill.Gunnery => this with { Gunnery = value }, WarriorSkill.Piloting => this with { Piloting = value },
        WarriorSkill.Sensors => this with { Sensors = value }, WarriorSkill.Grit => this with { Grit = value },
        _ => throw new ArgumentOutOfRangeException(nameof(skill)),
    };
}

/// <summary>What a MechWarrior did with each skill in one battle, counted by the simulation. Hits and kills come from the unit's own
/// tallies. Metres in contact are moved while the side identifies an enemy; sole-spotter seconds are spent as the only friendly
/// identifying an enemy that a friendly is aiming at. FirstIdentified lists the enemies this pilot was first on the side to identify.
/// LedAdvanceAt is when the pilot, first into a Take and Hold's objective, held it until it was taken.</summary>
public sealed record SkillUse(int PilotingChecks = 0, int FootingKept = 0, float MetresInContact = 0, string[]? FirstIdentified = null,
    double SoleSpotterSeconds = 0, int PressuredNerveChecks = 0, int HeldOn = 0, bool Stressed = false, double? LedAdvanceAt = null)
{
    public static readonly SkillUse None = new();
    public IReadOnlyList<string> Identified => FirstIdentified ?? [];
}

/// <summary>Skill gain checks, rolled for each surviving MechWarrior once a mission battle is over (see docs/design.md, "Skill
/// growth"). Each pilot gets one check on a skill drawn by a lottery weighted by how they used their skills, and extra checks for
/// particular deeds. A check succeeds when 1d6 rolls below the skill's rating; a skill at Legendary loses its checks. Only one
/// success per skill counts in a mission, and a skill rises one level after <see cref="Threshold"/> successes.</summary>
public static class SkillGrowth
{
    public static readonly WarriorSkill[] Skills = Enum.GetValues<WarriorSkill>();
    /// <summary>A nerve check with a result at least this high counts as Grit used under pressure.</summary>
    public const int PressuredNerveResult = CombatRules.CalmingResult + 1;
    public const float MetresPerPilotingUse = 100;
    public const double SoleSpotterSecondsPerUse = 10;
    // Each use's lottery weight is 1 ÷ how often it happens in an average mission battle, measured from simulated missions (see
    // docs/combat-tuning.md), so an ordinary mission gives each skill about the same chance. A skill with two kinds of use gives each
    // half. Revisit these whenever a new kind of skill use is added.
    public const double HitWeight = 1 / 17.0;
    public const double PilotingCheckWeight = .5 / .28, MovementWeight = .5 / 24.5;
    public const double FirstIdentifiedWeight = .5 / 1.0, SoleSpotterWeight = .5 / 4.0;
    public const double PressuredNerveWeight = 1 / 61.0;

    /// <summary>Successes needed to raise a skill from this rating: 2 from Untrained, one more for each level above it.</summary>
    public static int Threshold(int rating) => WarriorSkills.Untrained + 2 - rating;
    public static int Rating(WarriorSpec warrior, WarriorSkill skill) => skill switch
    {
        WarriorSkill.Gunnery => warrior.Gunnery, WarriorSkill.Piloting => warrior.Piloting, WarriorSkill.Sensors => warrior.Sensors,
        WarriorSkill.Grit => warrior.Grit, _ => throw new ArgumentOutOfRangeException(nameof(skill)),
    };
    public static WarriorSpec WithRating(WarriorSpec warrior, WarriorSkill skill, int rating) => skill switch
    {
        WarriorSkill.Gunnery => warrior with { Gunnery = rating }, WarriorSkill.Piloting => warrior with { Piloting = rating },
        WarriorSkill.Sensors => warrior with { Sensors = rating }, WarriorSkill.Grit => warrior with { Grit = rating },
        _ => throw new ArgumentOutOfRangeException(nameof(skill)),
    };
    public static bool Maxed(int rating) => rating <= WarriorSkills.Expert;
    /// <summary>Banked successes are 0 up to one short of the threshold, and 0 for a skill at Legendary.</summary>
    public static bool ValidProgress(WarriorSpec warrior, SkillProgress progress) => Skills.All(skill =>
        progress[skill] >= 0 && (Maxed(Rating(warrior, skill)) ? progress[skill] == 0 : progress[skill] < Threshold(Rating(warrior, skill))));

    /// <summary>Each skill's lottery weight for the guaranteed check.</summary>
    public static double[] Scores(SkillUse use, int hits) =>
    [
        hits * HitWeight,
        use.PilotingChecks * PilotingCheckWeight + use.MetresInContact / MetresPerPilotingUse * MovementWeight,
        use.Identified.Count * FirstIdentifiedWeight + use.SoleSpotterSeconds / SoleSpotterSecondsPerUse * SoleSpotterWeight,
        use.PressuredNerveChecks * PressuredNerveWeight,
    ];
    /// <summary>Extra checks earned by deeds: kills for Gunnery, Piloting checks passed and leading the advance on a Take and Hold's
    /// objective for Piloting, enemies this pilot first identified that
    /// were later defeated for Sensors, and for Grit each time a Panicked pilot held on, plus one for finishing the battle still
    /// fighting after being Stressed or worse.</summary>
    public static int[] ExtraChecks(SkillUse use, int kills, int identifiedDefeated, bool fightingAtEnd) =>
        [kills, use.FootingKept + (use.LedAdvanceAt is null ? 0 : 1), identifiedDefeated, use.HeldOn + (use.Stressed && fightingAtEnd ? 1 : 0)];

    /// <summary>The skill the lottery draws for the guaranteed check: weighted by score, or any of the four alike when nothing scored.</summary>
    public static WarriorSkill Draw(Random random, IReadOnlyList<double> scores)
    {
        var total = scores.Sum();
        if (!(total > 0)) return Skills[random.Next(Skills.Length)];
        var draw = random.NextDouble() * total;
        for (var i = 0; i < Skills.Length; i++) { draw -= scores[i]; if (draw < 0) return Skills[i]; }
        return Skills[Array.FindLastIndex(scores.ToArray(), score => score > 0)];
    }

    /// <summary>Rolls the mission's checks and returns the skills that succeeded, at most once each.</summary>
    public static WarriorSkill[] Roll(Random random, WarriorSpec warrior, IReadOnlyList<double> scores, IReadOnlyList<int> extra)
    {
        var drawn = Draw(random, scores);
        var gains = new List<WarriorSkill>();
        foreach (var skill in Skills)
        {
            var checks = extra[(int)skill] + (skill == drawn ? 1 : 0);
            var rating = Rating(warrior, skill);
            if (Maxed(rating)) continue;
            for (var i = 0; i < checks; i++)
                if (random.Next(1, 7) < rating) { gains.Add(skill); break; }
        }
        return [.. gains];
    }

    /// <summary>A success banked: the skill rises one level at its threshold, and its count starts again.</summary>
    public static (WarriorSpec Warrior, SkillProgress Progress, bool Advanced) Gain(WarriorSpec warrior, SkillProgress progress, WarriorSkill skill)
    {
        var rating = Rating(warrior, skill);
        if (Maxed(rating)) throw new ArgumentException("A skill at Legendary cannot improve.", nameof(skill));
        var count = progress[skill] + 1;
        return count >= Threshold(rating) ? (WithRating(warrior, skill, rating - 1), progress.With(skill, 0), true) : (warrior, progress.With(skill, count), false);
    }

    /// <summary>"Showed progress in their Gunnery skills", or "Gunnery skills advanced to Veteran" when the success reaches the threshold.</summary>
    public static string Describe(WarriorSpec warrior, SkillProgress progress, WarriorSkill skill)
    {
        var (after, _, advanced) = Gain(warrior, progress, skill);
        return advanced ? $"{skill} skills advanced to {WarriorSkills.Descriptor(Rating(after, skill))}" : $"Showed progress in their {skill} skills";
    }
}
