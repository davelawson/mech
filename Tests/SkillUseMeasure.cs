using Godot;

namespace Mech.Tests;

/// <summary>Average skill uses per friendly pilot over simulated lance battles against randomized opposition, each friendly attacking
/// the nearest enemy it can see: the rates the skill gain lottery's weights divide by (<see cref="SkillGrowth"/>).</summary>
internal static class SkillUseMeasure
{
    public static string Run(int battles)
    {
        var lists = new List<double[]>();
        string[][] lances = [["firestarter", "griffin", "catapult", "awesome"], ["jenner", "shadow-hawk", "thunderbolt", "victor"],
            ["panther", "wolverine", "crusader", "zeus"]];
        var durations = new List<double>();
        for (var b = 0; b < battles; b++)
        {
            var lance = ForceRoster.Create("Lance", lances[b % 3].Select(DraftCatalog.Mech), TestPilots.Warriors.Skip(4 * (b % 20)).Take(4));
            for (var i = 0; i < 4; i++) lance.Assign(lance.Mechs[i].Id, lance.Warriors[i].Id);
            var fight = CombatSimulation.Deploy(lance, lance.Mechs.Select(item => item.Id), 1000 + b, 2000 + b, SimulationOpponent.Randomized,
                (Biome)(b % Enum.GetValues<Biome>().Length));
            fight.Paused = false;
            fight.Move(fight.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => item.Id), Vector2.Zero);
            for (var second = 0; second < 1500 && fight.Outcome == CombatOutcome.Ongoing; second++)
            {
                foreach (var shooter in fight.Units.Where(item => item.Side == CombatSide.Friendly && item.Active))
                {
                    var target = fight.Units.Where(item => item.Side == CombatSide.Enemy && item.Active && fight.Detected(shooter.Side, item.Id))
                        .OrderBy(item => item.Position.DistanceSquaredTo(shooter.Position)).FirstOrDefault();
                    if (target is not null) fight.Attack([shooter.Id], target.Id);
                }
                fight.Advance(1);
            }
            durations.Add(fight.Elapsed);
            foreach (var unit in fight.Units.Where(item => item.Side == CombatSide.Friendly))
            {
                var u = unit.Use;
                var defeated = u.Identified.Count(id => fight.Unit(id).Defeated);
                lists.Add([unit.Hits, u.PilotingChecks, u.MetresInContact / 100, u.Identified.Count, u.SoleSpotterSeconds / 10, u.PressuredNerveChecks,
                    unit.Kills, u.FootingKept, defeated, u.HeldOn + (u.Stressed && (unit.Fighting || unit.Extracted) ? 1 : 0), unit.WarriorKilled ? 1 : 0]);
            }
        }
        string[] names = ["hits", "piloting checks", "100 m in contact", "first identified", "10 s sole spotter", "pressured nerve", "kills", "footing kept",
            "identified defeated", "grit extra", "killed"];
        return $"battles {battles}, mean {durations.Average():0}s\n" + string.Join("\n", names.Select((name, i) =>
            $"{name}: mean {lists.Average(row => row[i]):0.00}, zero {lists.Count(row => row[i] == 0) / (double)lists.Count:P0}"));
    }
}
