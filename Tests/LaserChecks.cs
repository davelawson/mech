using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static WeaponSpec TestLaser(string id = "laser") =>
        new(id, "Medium Laser", MechLocation.RightArm, false, 3, new(DamageKind.Direct, 5, 1, 0), 0, 20, 25, 30, null, WeaponKind.MediumLaser);

    // Friendly carries an enabled laser; the enemy holds fire unless a check arms its lethal cannon.
    // By default the enemy is twisting its torso back to center, so laser burns spread across locations.
    private static CombatSimulation LaserFixture(int seed = 41, bool twisting = true)
    {
        var sim = HeatFixture([TestLaser(), HeatGun("cannon", 0, 1000)], seed: seed);
        Friendly(sim).Weapons[1].Enabled = false;
        Enemy(sim).Weapons[0].Enabled = false; Enemy(sim).Firing = FiringMode.HoldFire;
        if (twisting) Enemy(sim).TorsoTwist = .5f;
        sim.Paused = false;
        return sim;
    }

    private static void Steps(CombatSimulation sim, int count)
    { for (var i = 0; i < count; i++) sim.Advance(CombatRules.StepSeconds); }

    private static void CheckLasers(Action<bool, string> check)
    {
        check(MechLocations().All(location => CombatRules.AdjacentLocations(location)
                .All(other => CombatRules.AdjacentLocations(other).Contains(location))),
            "laser adjacency is symmetric");
        check(CombatRules.LaserFirstHalf(3) == 2 && CombatRules.LaserFirstHalf(5) == 3 && CombatRules.LaserFirstHalf(8) == 4,
            "the first half of a laser burn rounds up");

        var sim = LaserFixture(); var friendly = Friendly(sim); var enemy = Enemy(sim);
        var armor = enemy.ArmorLost + enemy.StructureLost;
        Steps(sim, 1);
        var discharge = sim.History.Single().Shot;
        check(discharge.Hit && discharge.Damage == 3 && discharge.SecondDamage == 2 && discharge.SecondLocation is { } second
                && CombatRules.AdjacentLocations(discharge.Location).Contains(second)
                && enemy.ArmorLost + enemy.StructureLost - armor == 3 && friendly.DamageDealt == 3,
            "a laser hit on a twisting target applies its first half at discharge and rolls an adjacent second location");
        check(sim.History[0].DebugText.Contains($"dmg 3, burn +2 {DraftCatalog.Abbreviation(discharge.SecondLocation!.Value)}"),
            "the discharge entry announces the pending burn");
        Steps(sim, 9);
        check(sim.History.Count == 1, "the second half waits for half the burn");
        Steps(sim, 1);
        var burn = sim.History[^1];
        check(sim.History.Count == 2 && burn.Shot.Burn && burn.Shot.Sequence == discharge.Sequence
                && burn.Shot.Location == discharge.SecondLocation && burn.Shot.Damage == 2 && burn.Shot.GeneratedHeat == 0
                && burn.Shot.DamageApplied.Sum(part => part.Armor + part.RearArmor + part.Structure) == 2
                && enemy.ArmorLost + enemy.StructureLost - armor == 5 && friendly.DamageDealt == 5 && friendly.ShotsFired == 1,
            "the second half lands at half the burn as a separate continuation entry");
        check(burn.DebugText.Contains("burn continues") && !burn.DebugText.Contains("chance"),
            "burn entries inherit their discharge roll");

        sim = LaserFixture(twisting: false); enemy = Enemy(sim); armor = enemy.ArmorLost + enemy.StructureLost;
        check(!enemy.InMotion, "a target standing still with a centered torso is not in motion");
        Steps(sim, 1);
        discharge = sim.History.Single().Shot;
        check(discharge.Hit && !enemy.InMotion && discharge.SecondLocation == discharge.Location
                && sim.History[0].DebugText.Contains($"burn +2 {DraftCatalog.Abbreviation(discharge.Location)}"),
            "a laser burn on a stationary target stays on the struck location");
        Steps(sim, 10);
        check(sim.History.Count == 2 && sim.History[^1].Shot is { Burn: true } stayed && stayed.Location == discharge.Location
                && enemy.ArmorLost + enemy.StructureLost - armor == 5,
            "the second half of a stationary target's burn lands on the same location");
        sim = LaserFixture(twisting: false); enemy = Enemy(sim);
        sim.Move([enemy.Id], enemy.Position + new Vector2(0, 20)); Steps(sim, 1);
        discharge = sim.History.Single().Shot;
        check(enemy.InMotion && !enemy.TorsoTurning && discharge.SecondLocation is { } spread
                && CombatRules.AdjacentLocations(discharge.Location).Contains(spread),
            "a laser burn spreads across a target that is walking or turning");
        sim = HeatFixture([TestLaser()], seed: 1, gunnery: 8); Enemy(sim).Firing = FiringMode.HoldFire;
        sim.Paused = false; Steps(sim, 20);
        check(sim.History.Count == 1 && !sim.History[0].Shot.Hit && sim.History[0].Shot.SecondLocation is null,
            "a laser miss has no burn continuation");

        sim = LaserFixture(); Steps(sim, 1);
        Enemy(sim).ApplyDamage(MechLocation.CenterTorso, 10000); Steps(sim, 10);
        check(sim.History.Count == 1, "a destroyed target loses the second half");

        sim = LaserFixture(); Steps(sim, 1);
        Friendly(sim).ApplyDamage(MechLocation.Head, 10000); Steps(sim, 10);
        check(sim.History.Count == 1, "destroying the shooter cuts off its burn");

        // The enemy's lethal return shot lands in the same step the burn comes due: both resolve.
        sim = LaserFixture(); Steps(sim, 10);
        Enemy(sim).Firing = FiringMode.FireAtWill; Steps(sim, 1);
        check(Friendly(sim).Status == CombatStatus.Destroyed && sim.History.Any(entry => entry.Shot.Burn),
            "a burn due in the step its shooter is destroyed still lands");

        // A disabled shooter's burn continues.
        sim = LaserFixture(); friendly = Friendly(sim); Steps(sim, 1);
        var arm = friendly.Locations[MechLocation.RightArm];
        friendly.ApplyDamage(MechLocation.RightArm, arm.Armor + arm.Structure); Steps(sim, 10);
        check(friendly.Status == CombatStatus.Disabled && sim.History.Count(entry => entry.Shot.Burn) == 1,
            "a disabled shooter's burn still lands");

        CheckWindDown(check);
    }

    private static void CheckWindDown(Action<bool, string> check)
    {
        // A burn landing during the wind-down can turn the result into a draw.
        var seed = Enumerable.Range(1, 400).First(candidate =>
        {
            var probe = LaserFixture(candidate); Steps(probe, 1);
            var shot = probe.History.Single().Shot;
            return shot.Hit && shot.Location is not (MechLocation.CenterTorso or MechLocation.Head)
                && shot.SecondLocation == MechLocation.CenterTorso;
        });
        var sim = LaserFixture(seed); var friendly = Friendly(sim); var enemy = Enemy(sim);
        var center = enemy.Locations[MechLocation.CenterTorso]; center.Armor = center.RearArmor = 0; center.Structure = 2;
        Steps(sim, 1);
        var friendlyArm = friendly.Locations[MechLocation.RightArm];
        friendly.ApplyDamage(MechLocation.RightArm, friendlyArm.Armor + friendlyArm.Structure); Steps(sim, 1);
        check(sim.WindingDown && enemy.Status != CombatStatus.Destroyed && sim.Outcome == CombatOutcome.Ongoing,
            "a side's defeat begins the wind-down without deciding the outcome");
        Steps(sim, 9);
        check(enemy.Status == CombatStatus.Destroyed && sim.Outcome == CombatOutcome.Ongoing, "pending burns land during the wind-down");
        sim.Advance(CombatRules.WindDownSeconds);
        check(sim.Outcome == CombatOutcome.Draw, "the outcome is decided when the wind-down ends");
    }

    private static IEnumerable<MechLocation> MechLocations() => Enum.GetValues<MechLocation>();
}
