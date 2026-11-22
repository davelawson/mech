using System.Text.Json;
using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static void CheckBattleSummary(Action<bool, string> check)
    {
        // Mech condition reads the final state against the full loadout.
        var sim = Battle(1, "griffin"); var unit = Friendly(sim);
        check(BattleSummary.Condition(unit) == MechCondition.Undamaged && BattleSummary.Pilot(unit) == PilotCondition.Healthy,
            "an untouched mech is Undamaged with a Healthy pilot");
        unit.Locations[MechLocation.LeftArm].Armor -= 5;
        check(BattleSummary.Condition(unit) == MechCondition.Scratched, "armour below the installed amount alone is Scratched");
        unit.Locations[MechLocation.LeftArm].Structure -= 1;
        check(BattleSummary.Condition(unit) == MechCondition.Damaged, "missing structure is Damaged");
        sim = Battle(1, "griffin"); unit = Friendly(sim); Hit(unit, Id(unit, ComponentKind.Gyro));
        check(BattleSummary.Condition(unit) == MechCondition.Damaged, "a damaged system is Damaged");
        unit.Locations[MechLocation.RightLeg].Structure = 0;
        check(BattleSummary.Condition(unit) == MechCondition.Crippled, "a lost location is Crippled");
        unit.Locations[MechLocation.Head].Structure = 0;
        check(BattleSummary.Condition(unit) == MechCondition.Destroyed && BattleSummary.Pilot(unit) == PilotCondition.Killed,
            "a lost head is Destroyed and kills the pilot");
        sim = Battle(1, "griffin"); unit = Friendly(sim); unit.Locations[MechLocation.CenterTorso].Structure = 0;
        check(BattleSummary.Condition(unit) == MechCondition.Destroyed, "a lost center torso is Destroyed");

        // Damage brought into the battle still shows: the condition is the mech's state, not the change.
        var roster = Roster(1, "griffin"); var saved = roster.Snapshot(); var stock = saved.Mechs[0];
        var worn = ForceRoster.Restore(saved with { Mechs = [stock with { Locations = stock.Locations
            .Select(loc => loc.Location == MechLocation.LeftTorso ? loc with { Armor = loc.Armor - 10 } : loc).ToArray() }] }, Campaign.StartDate);
        var carried = CombatSimulation.DeployOnTerrain(worn, [stock.Id], BattlefieldTerrain.Flat, opponent: SimulationOpponent.Randomized);
        check(BattleSummary.Condition(Friendly(carried)) == MechCondition.Scratched && Friendly(carried).ArmorLost == 0,
            "armour missing at deployment reads Scratched, though no damage was taken");

        // Pilot conditions, worst first.
        sim = Battle(1, "griffin"); unit = Friendly(sim); unit.Injuries = 1;
        check(BattleSummary.Pilot(unit) == PilotCondition.Injured, "an injured pilot reads Injured");
        sim.EjectOrdered(unit);
        check(BattleSummary.Pilot(unit) == PilotCondition.Ejected && sim.Highlights.Any(item => item.UnitId == unit.Id
            && item.Text.Contains(unit.Warrior.Callsign) && item.Text.Contains(unit.Spec.DisplayName) && item.Text.Contains("ejected")),
            "an ejection reads Ejected and is a highlight naming mech and pilot");

        // A shutdown is a highlight, once per shutdown.
        sim = Battle(1, "griffin"); Arrange(sim, 3000); unit = Friendly(sim);
        unit.AddHeat(CombatRules.ShutdownHeat); sim.Paused = false; sim.Advance(.2);
        check(sim.Highlights.Count(item => item.UnitId == unit.Id && item.Text == $"{BattleSummary.Name(unit)} shut down from overheating.") == 1,
            "overheating to shutdown is a single highlight");

        // An ammunition explosion is the victim's own damage: the weapon that set it off is not credited with it, but the highlight
        // names that weapon.
        var blasted = false;
        for (var seed = 1; seed <= 40 && !blasted; seed++)
        {
            sim = Battle(1, "catapult", seed); Arrange(sim, 300);
            var shooter = Friendly(sim); var victim = Enemy(sim);
            foreach (var weapon in victim.Weapons) weapon.Enabled = false;
            foreach (var part in victim.Locations.Values) part.Armor = part.RearArmor = 0;
            sim.NerveChecksEnabled = false; sim.SetPriority([shooter.Id], victim.Id); sim.Paused = false;
            for (var step = 0; step < 1200 && !victim.Defeated; step++) sim.Advance(CombatRules.StepSeconds);
            var blast = sim.History.SelectMany(item => item.Shot.Criticals).FirstOrDefault(item => item.Explosion > 0);
            if (blast is null) continue;
            blasted = true;
            var credited = BattleSummary.Total(shooter).DamageDealt;
            check(credited < victim.AbsorbedArmor + victim.AbsorbedStructure && victim.StructureLost >= victim.AbsorbedStructure,
                "damage from an ammunition explosion counts as taken but is not credited to the weapon that set it off");
            check(sim.Highlights.Any(item => item.UnitId == victim.Id && item.Text.StartsWith($"{BattleSummary.Name(victim)}'s ")
                && item.Text.Contains($"exploded for {blast.Explosion} damage after a critical hit from {BattleSummary.Name(shooter)}'s ")),
                "an ammunition explosion is a highlight naming the bin, its location, its damage and the weapon that set it off");
        }
        check(blasted, "a catapult stripped of armour suffers an ammunition explosion within 40 seeds");

        // A fought battle: weapon tallies match the shot log, and damage credited never exceeds what the target lost.
        var explosions = 0;
        var lance = ForceRoster.Create("Mixed lance", new[] { "firestarter", "griffin", "catapult", "awesome" }.Select(DraftCatalog.Mech), DraftCatalog.Warriors.Take(4));
        for (var i = 0; i < 4; i++) lance.Assign(lance.Mechs[i].Id, lance.Warriors[i].Id);
        foreach (var seed in new[] { 183, 7, 11 })
        {
            var fight = Deploy(lance, lance.Mechs.Select(item => item.Id), seed); fight.Paused = false;
            fight.Move(fight.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => item.Id), Vector2.Zero);
            for (var second = 0; second < 1200 && fight.Outcome == CombatOutcome.Ongoing; second++)
            {
                foreach (var shooter in fight.Units.Where(item => item.Side == CombatSide.Friendly && item.Active))
                {
                    var target = fight.Units.Where(item => item.Side == CombatSide.Enemy && item.Active && fight.Detected(shooter.Side, item.Id))
                        .OrderBy(item => item.Position.DistanceSquaredTo(shooter.Position)).FirstOrDefault();
                    if (target is not null) fight.Attack([shooter.Id], target.Id);
                }
                fight.Advance(1);
            }
            check(fight.Outcome != CombatOutcome.Ongoing && fight.Units.Any(item => BattleSummary.Total(item).DamageDealt > 0), $"seed {seed}: the lances fight it out");
            var discharges = fight.History.Where(item => !item.Shot.Burn && !item.Shot.Impact).ToArray();
            check(fight.Units.All(shooter => shooter.Weapons.All(weapon =>
                {
                    var own = discharges.Where(item => item.Shot.AttackerId == shooter.Id && item.Shot.WeaponId == weapon.Spec.Id).ToArray();
                    return weapon.Tally.ShotsFired == own.Sum(item => Math.Max(1, item.Shot.Missiles.Count))
                        && weapon.Tally.Hits == own.Sum(item => item.Shot.Missiles.Count > 0 ? item.Shot.Missiles.Count(missile => missile.Hit) : item.Shot.Hit ? 1 : 0);
                })), $"seed {seed}: each weapon's shots and hits count every missile and every other discharge once");
            check(fight.Units.All(shooter => BattleSummary.Total(shooter).Kills == shooter.Kills),
                $"seed {seed}: a mech's kills are the sum of its weapons'");
            // Tallies do not say whom each hit struck, so damage and lost locations balance side against side.
            foreach (var side in Enum.GetValues<CombatSide>())
            {
                var shooters = fight.Units.Where(item => item.Side == side).ToArray();
                var targets = fight.Units.Where(item => item.Side != side).ToArray();
                var credited = shooters.Sum(item => BattleSummary.Total(item).DamageDealt);
                var absorbed = targets.Sum(item => item.AbsorbedArmor + item.AbsorbedStructure);
                var exploded = fight.History.Count(item => targets.Any(target => target.Id == item.Shot.TargetId) && item.Shot.Criticals.Any(critical => critical.Explosion > 0));
                explosions += exploded;
                check(exploded > 0 ? credited < absorbed : credited == absorbed,
                    $"seed {seed}: {side} weapons are credited with the damage their hits removed, leaving out ammunition explosions");
                check(credited <= targets.Sum(item => item.ArmorLost + item.StructureLost), $"seed {seed}: {side} credited damage never exceeds the losses it caused");
                var located = fight.History.Where(item => targets.Any(target => target.Id == item.Shot.TargetId)).SelectMany(item => item.Shot.DamageApplied).Count(part => part.Destroyed);
                check(shooters.Sum(item => BattleSummary.Total(item).ComponentsDestroyed) == located, $"seed {seed}: every location {side} weapons destroyed is credited");
                check(shooters.Sum(item => BattleSummary.Total(item).SystemsDamaged) == fight.History.Where(item => targets.Any(target => target.Id == item.Shot.TargetId))
                    .Sum(item => item.Shot.Criticals.Count(critical => critical.Explosion == 0)), $"seed {seed}: every critical hit by {side} weapons is a system damaged");
            }
            check(fight.Highlights.Zip(fight.Highlights.Skip(1)).All(pair => pair.First.Time <= pair.Second.Time)
                && fight.Highlights.All(item => item.Text.Contains(fight.Unit(item.UnitId).Warrior.Callsign) && item.Text.EndsWith('.')),
                $"seed {seed}: highlights are in order, one sentence each, naming the pilot");
            foreach (var lost in fight.Units.Where(item => BattleSummary.Condition(item) == MechCondition.Destroyed))
                check(fight.Highlights.Count(item => item.UnitId == lost.Id && item.Text.StartsWith($"{BattleSummary.Name(lost)} was destroyed")) == 1,
                    $"seed {seed}: {lost.Id}'s destruction is one highlight");

            // Co-op replicas take on the tallies and highlights.
            var replica = Deploy(lance, lance.Mechs.Select(item => item.Id), seed); replica.BecomeReplica();
            var state = fight.State(0, 0, 0);
            replica.Apply(JsonSerializer.Deserialize<CombatState>(JsonSerializer.Serialize(state))!);
            var partial = fight.State(0, 0, fight.Highlights.Count / 2);
            replica.Apply(JsonSerializer.Deserialize<CombatState>(JsonSerializer.Serialize(partial))!);
            check(replica.Highlights.SequenceEqual(fight.Highlights) && replica.Units.Zip(fight.Units)
                .All(pair => pair.First.Weapons.Select(item => item.Tally).SequenceEqual(pair.Second.Weapons.Select(item => item.Tally))),
                $"seed {seed}: a replica holds the leader's tallies and each highlight once");
        }
        Console.WriteLine($"Battle summary: {explosions} shots set off ammunition explosions");
    }
}
