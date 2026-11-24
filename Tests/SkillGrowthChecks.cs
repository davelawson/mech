namespace Mech.Tests;

internal static partial class RosterChecks
{
    private static readonly DateOnly Day = new(3025, 1, 1);
    /// <summary>Skill gain checks: thresholds, the 1d6 roll, one success per skill, the lottery, banking and saving progress.</summary>
    private static void CheckSkillGrowth(Action<bool, string> check, Action<Action, string> reject)
    {
        check(new[] { 7, 6, 5, 4, 3 }.Select(SkillGrowth.Threshold).SequenceEqual([2, 3, 4, 5, 6]),
            "a skill rises after 2 successes from Untrained, and one more for each level above it");
        var pilot = new WarriorSpec("pilot-growth", "Ada Okafor", "Anvil", "mara-vale", 7, 6, 4, 2);
        double[] gunneryOnly = [1, 0, 0, 0]; int[] none = [0, 0, 0, 0];
        double SuccessRate(int rating)
        {
            var random = new Random(rating); var spec = pilot with { Gunnery = rating };
            return Enumerable.Range(0, 12_000).Count(_ => SkillGrowth.Roll(random, spec, gunneryOnly, none).Contains(WarriorSkill.Gunnery)) / 12_000.0;
        }
        var rates = new[] { 7, 6, 5, 4, 3, 2 }.Select(SuccessRate).ToArray();
        check(rates[0] == 1 && rates.Skip(1).Take(4).Zip(new[] { 5, 4, 3, 2 }).All(pair => Math.Abs(pair.First - pair.Second / 6.0) < .02) && rates[5] == 0,
            $"a check succeeds when 1d6 rolls below the rating, and a Legendary skill's check is lost ({string.Join(", ", rates.Select(rate => rate.ToString("P0")))})");
        var seeded = new Random(9);
        check(Enumerable.Range(0, 200).All(_ => SkillGrowth.Roll(seeded, pilot with { Gunnery = 3 }, gunneryOnly, [6, 0, 0, 0]).Count(skill => skill == WarriorSkill.Gunnery) <= 1),
            "only one success per skill counts in a mission, however many checks it gets");
        var untrained = pilot with { Gunnery = 7, Piloting = 7, Sensors = 7, Grit = 7 };
        check(SkillGrowth.Roll(seeded, untrained, [0, 0, 0, 0], [1, 1, 1, 1]).SequenceEqual(SkillGrowth.Skills),
            "a pilot can progress in all four skills in one mission");
        check(SkillGrowth.Roll(seeded, untrained, [0, 0, 0, 0], none).Length == 1, "every surviving pilot gets one guaranteed check");
        var draws = Enumerable.Range(0, 20_000).Select(_ => SkillGrowth.Draw(seeded, [0, 0, 0, 0])).ToArray();
        check(SkillGrowth.Skills.All(skill => Math.Abs(draws.Count(item => item == skill) / 20_000.0 - .25) < .02),
            "a pilot who used no skill draws any of the four alike");
        draws = Enumerable.Range(0, 20_000).Select(_ => SkillGrowth.Draw(seeded, [3, 1, 0, 0])).ToArray();
        check(Math.Abs(draws.Count(item => item == WarriorSkill.Gunnery) / 20_000.0 - .75) < .02 && draws.All(item => item is WarriorSkill.Gunnery or WarriorSkill.Piloting),
            "the guaranteed check's skill is drawn in proportion to how the pilot used each skill");
        var use = new SkillUse(PilotingChecks: 2, FootingKept: 1, MetresInContact: 300, FirstIdentified: ["enemy-1", "enemy-2"], SoleSpotterSeconds: 20,
            PressuredNerveChecks: 3, HeldOn: 1, Stressed: true);
        var scores = SkillGrowth.Scores(use, 12);
        check(Math.Abs(scores[0] - 12 * SkillGrowth.HitWeight) < 1e-9
            && Math.Abs(scores[1] - (2 * SkillGrowth.PilotingCheckWeight + 3 * SkillGrowth.MovementWeight)) < 1e-9
            && Math.Abs(scores[2] - (2 * SkillGrowth.FirstIdentifiedWeight + 2 * SkillGrowth.SoleSpotterWeight)) < 1e-9
            && Math.Abs(scores[3] - 3 * SkillGrowth.PressuredNerveWeight) < 1e-9, "each use adds its weight to its skill's lottery score");
        check(SkillGrowth.ExtraChecks(use, 2, 1, true).SequenceEqual([2, 1, 1, 2]) && SkillGrowth.ExtraChecks(use, 0, 0, false).SequenceEqual([0, 1, 0, 1]),
            "kills, footing kept, defeated enemies first identified, holding on and seeing the battle out after stress earn extra checks");

        var (after, progress, advanced) = SkillGrowth.Gain(pilot, SkillProgress.None with { Gunnery = 1 }, WarriorSkill.Gunnery);
        check(advanced && after.Gunnery == 6 && progress == SkillProgress.None, "the threshold success raises the skill and starts its count again");
        (after, progress, advanced) = SkillGrowth.Gain(pilot, SkillProgress.None with { Piloting = 1 }, WarriorSkill.Piloting);
        check(!advanced && after == pilot && progress.Piloting == 2, "a success short of the threshold is banked");
        reject(() => SkillGrowth.Gain(pilot, SkillProgress.None, WarriorSkill.Grit), "a Legendary skill cannot improve");
        check(SkillGrowth.Describe(pilot, SkillProgress.None, WarriorSkill.Piloting) == "Showed progress in their Piloting skills"
            && SkillGrowth.Describe(pilot, SkillProgress.None with { Sensors = 4 }, WarriorSkill.Sensors) == "Sensors skills advanced to Elite",
            "the battle summary reads as progress, or as the skill's new level");
        var levelled = pilot with { Gunnery = 6, Piloting = 6, Sensors = 6, Grit = 6 };
        var promoted = (levelled with { Gunnery = 5 }).Rank;
        check(levelled.Rank == WarriorExperience.PrivateFirstClass && promoted == WarriorExperience.PrivateFirstClass
            && SkillGrowth.Gain(levelled with { Gunnery = 5, Piloting = 5 }, SkillProgress.None with { Sensors = 2 }, WarriorSkill.Sensors).Warrior.Rank == WarriorExperience.Corporal
            && SkillGrowth.Gain(levelled, SkillProgress.None with { Gunnery = 2 }, WarriorSkill.Gunnery).Warrior.Salary > levelled.Salary,
            "a rising skill can promote the pilot, and raises their salary");
        check(SkillGrowth.ValidProgress(pilot, new SkillProgress(1, 2, 4, 0)) && !SkillGrowth.ValidProgress(pilot, new SkillProgress(2, 0, 0, 0))
            && !SkillGrowth.ValidProgress(pilot, new SkillProgress(0, 0, 0, 1)) && !SkillGrowth.ValidProgress(pilot, new SkillProgress(-1, 0, 0, 0)),
            "banked successes stay below the threshold, and a Legendary skill banks none");

        // Saves keep progress; saves from before skill growth have none.
        var roster = ForceRoster.Create("Growth", DraftCatalog.Mechs.Take(2), [pilot, TestPilots.Warriors[0]]);
        var growing = roster.Warriors[0];
        check(growing.Progress == SkillProgress.None && !growing.GainSkill(WarriorSkill.Piloting) && growing.GainSkill(WarriorSkill.Gunnery) == false
            && growing.GainSkill(WarriorSkill.Gunnery) && growing.Spec.Gunnery == 6 && growing.Progress == new SkillProgress(0, 1, 0, 0),
            "a roster MechWarrior banks successes and their skill rises at the threshold");
        var saved = roster.Snapshot();
        check(ForceRoster.Restore(saved, Day).Warriors[0] is { } back && back.Spec == growing.Spec && back.Progress == growing.Progress
            && saved.Warriors[1].Progress is null, "saves keep each MechWarrior's progress, and leave it out when there is none");
        check(ForceRoster.Restore(saved with { Warriors = [.. saved.Warriors.Select(w => w with { Progress = null })] }, Day).Warriors.All(w => w.Progress == SkillProgress.None),
            "MechWarriors saved before skill growth start with no progress");
        reject(() => ForceRoster.Restore(saved with { Warriors = [saved.Warriors[0] with { Progress = new SkillProgress(Piloting: 3) }, saved.Warriors[1]] }, Day),
            "saved progress must be below the skill's threshold");
        reject(() => ForceRoster.Restore(saved with { Warriors = [saved.Warriors[0] with { Progress = new SkillProgress(Grit: 1) }, saved.Warriors[1]] }, Day),
            "a Legendary skill saves no progress");
    }
}
