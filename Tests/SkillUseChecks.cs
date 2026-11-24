using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    /// <summary>What the battle counts toward skill gain checks, and the checks it rolls once it is over.</summary>
    private static void CheckSkillUse(Action<bool, string> check)
    {
        // Piloting checks, and the footing kept.
        var sim = Battle(1, "griffin"); Arrange(sim); var unit = Friendly(sim);
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        sim.PilotingRollOverride = () => 12; sim.RecordKnockdownDamage(unit, 111);
        sim.Paused = false; sim.Advance(1.05);
        sim.PilotingRollOverride = () => 2; sim.RecordKnockdownDamage(unit, 111);
        check(unit.Use is { PilotingChecks: 2, FootingKept: 1 }, "each Piloting check counts, and each passed one earns an extra check");

        // Nerve checks under pressure, holding on while Panicked, and having been Stressed.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        sim.NerveRollOverride = () => 2; sim.NerveCheck(unit);
        check(unit.Use.PressuredNerveChecks == 0, "a calm nerve check is not Grit used under pressure");
        unit.Stress = StressLevel.Panicked; sim.NerveRollOverride = () => 12; sim.NerveCheck(unit);
        check(unit.Use is { PressuredNerveChecks: 1, HeldOn: 1, Stressed: true }, "a check of 12 or more uses Grit, and a Panicked pilot holding on earns an extra check");

        // Sensors: the first identification, and spotting alone for a target the side aims at; moving in contact counts for Piloting.
        sim = Battle(1, "griffin"); Arrange(sim, 900); unit = Friendly(sim); var enemy = Enemy(sim);
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        sim.Paused = false; sim.Attack([unit.Id], enemy.Id); sim.Advance(10);
        check(unit.Use.Identified.SequenceEqual([enemy.Id]) && enemy.Use.Identified.Count == 0 && enemy.Use.MetresInContact == 0,
            "the friendly pilot who first identifies an enemy is credited, and enemies count nothing");
        check(Math.Abs(unit.Use.SoleSpotterSeconds - 10) < .2, $"spotting alone for an aimed-at enemy counts its seconds ({unit.Use.SoleSpotterSeconds:0.0})");
        check(unit.Use.MetresInContact > 100, $"metres moved while an enemy is identified count ({unit.Use.MetresInContact:0} m)");

        // The checks after the battle.
        sim.End();
        var gains = sim.SkillGains(unit);
        check(gains.Length is >= 1 and <= 4 && gains.Distinct().Count() == gains.Length && gains.SequenceEqual(sim.SkillGains(unit))
            && sim.MissionReport().Mechs.Single().SkillGains!.SequenceEqual(gains) && sim.SkillGains(enemy).Length == 0,
            "a surviving friendly's checks are rolled the same each time they are asked for and go into the mission report; enemies roll none");
        unit.Locations[MechLocation.Head].Structure = 0;
        check(sim.SkillGains(unit).Length == 0, "a pilot killed in battle makes no skill gain checks");
    }
}
