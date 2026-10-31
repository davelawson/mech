using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    // A long-bracket PPC so a 30-hex shot stays in short range; the enemy holds fire with a weaker gunner.
    private static CombatSimulation PpcFixture(int seed = 41, int gunnery = -10, float distance = 30)
    {
        var spec = DraftCatalog.Mech("awesome") with
        {
            Weapons = [new("ppc", "Test PPC", MechLocation.RightArm, false, 0, new(DamageKind.Direct, 100, 1, 0), 0, 20, 25, 30, null, WeaponKind.PPC)],
            Components = [], HeatSinks = 0, Ammo = []
        };
        var units = Enum.GetValues<CombatSide>().Select(side =>
        {
            var unit = new CombatUnit(side.ToString(), spec, DraftCatalog.Warriors[0] with { Gunnery = side == CombatSide.Friendly ? gunnery : 4 }, side,
                spec.Locations.Select(location => new LocationCondition(location.Location, location.Armor, location.RearArmor, location.Structure)),
                spec.Weapons.Select(weapon => new EquipmentCondition(weapon.Id, ComponentCondition.Operational)),
                [], side == CombatSide.Friendly ? Vector2.Zero : new Vector2(distance, 0));
            unit.Facing = side == CombatSide.Friendly ? Vector2.Right : Vector2.Left;
            return unit;
        }).ToArray();
        units[1].Firing = FiringMode.HoldFire;
        return new(units, seed, BattlefieldTerrain.Flat) { EnemyAiEnabled = false, Paused = false };
    }

    private static void CheckPpcs(Action<bool, string> check)
    {
        check(CombatRules.PpcSplashLocations(MechLocation.CenterTorso).Order().SequenceEqual(
                new[] { MechLocation.LeftTorso, MechLocation.RightTorso, MechLocation.LeftLeg, MechLocation.RightLeg }.Order())
                && CombatRules.PpcSplashLocations(MechLocation.Head).SequenceEqual([MechLocation.CenterTorso])
                && CombatRules.PpcSplashLocations(MechLocation.LeftArm).SequenceEqual([MechLocation.LeftTorso])
                && MechLocations().All(location => !CombatRules.PpcSplashLocations(location).Contains(MechLocation.Head)),
            "PPC splash follows laser adjacency but never reaches the head");
        check(CombatRules.PpcSplash(100) == 50 && CombatRules.PpcSplash(45) == 22, "PPC splash is half the damage, rounded down");
        // Across many hits, each 10-point splash chunk lands on an adjacent non-head location and the chunks spread between them.
        var spread = Enumerable.Range(1, 60).Select(seed => { var probe = PpcFixture(seed * 104729); Steps(probe, 1); return probe.History[0].Shot; }).ToArray();
        check(spread.All(shot => shot.Splash == 50 && shot.Splashes.Count == 5 && shot.Splashes.All(splash => splash.Damage == 10
                    && CombatRules.PpcSplashLocations(shot.Location).Contains(splash.Location)))
                && spread.Any(shot => shot.Splashes.Select(splash => splash.Location).Distinct().Count() > 1)
                && spread.Where(shot => shot.Location == MechLocation.CenterTorso).SelectMany(shot => shot.Splashes).Select(splash => splash.Location).Distinct().Count() > 1,
            "PPC splash shares five 10-point chunks at random among adjacent non-head locations");
        check(Math.Abs(CombatRules.PpcFlightSeconds - .4) < 1e-9, "a PPC bolt takes 0.4 seconds at any range");

        var sim = PpcFixture(); var friendly = Friendly(sim); var enemy = Enemy(sim);
        var weapon = enemy.Weapons[0];
        var clear = CombatSimulation.HitChance(enemy, friendly, weapon);
        Steps(sim, 1);
        var discharge = sim.History.Single().Shot;
        check(discharge.Hit && discharge.InFlight && Math.Abs(discharge.FlightSeconds - .4) < 1e-6 && discharge.Splash == 50
                && enemy.ArmorLost + enemy.StructureLost == 0 && enemy.InterferenceRemaining == 0,
            "a PPC hit is rolled at discharge and deals nothing while the bolt flies");
        check(sim.History[0].DebugText.Contains("dmg 100 + 50 splash on impact"), "the discharge entry reports the splash");
        Steps(sim, 8);
        var impact = sim.History[^1].Shot;
        int Lost(MechLocation location) => impact.DamageApplied.Where(part => part.Location == location).Sum(part => part.Armor + part.RearArmor + part.Structure);
        int Splashed(MechLocation location) => discharge.Splashes.Where(splash => splash.Location == location).Sum(splash => splash.Damage);
        check(impact.Impact && impact.Splashes.SequenceEqual(discharge.Splashes)
                && Lost(discharge.Location) == 100 + Splashed(discharge.Location)
                && discharge.Splashes.Select(splash => splash.Location).Distinct().All(location => Lost(location) == Splashed(location))
                && impact.DamageApplied.All(part => part.Location == discharge.Location || Splashed(part.Location) > 0)
                && impact.DamageApplied.Sum(part => part.Armor + part.RearArmor + part.Structure) == 150 && friendly.DamageDealt == 150,
            $"the impact deals full damage to {discharge.Location} and shares five splash chunks among its rolled neighbors");
        check(sim.History[0].DebugText.Contains("splash " + string.Join(", ", discharge.Splashes.GroupBy(splash => splash.Location).OrderBy(group => group.Key)
                .Select(group => $"{DraftCatalog.Abbreviation(group.Key)} {group.Sum(splash => splash.Damage)}"))),
            "the discharge entry lists where the splash lands");
        check(sim.History[^1].DebugText.Contains("electro-interference 5s"), "the impact entry reports electro-interference");
        var jammed = CombatSimulation.HitChance(enemy, friendly, weapon);
        check(enemy.InterferenceRemaining == CombatRules.InterferenceSeconds && enemy.InterferenceAimModifier == 2 && jammed < clear
                && Math.Abs(jammed - 10 / 36.0) < 1e-9 && Math.Abs(clear - 21 / 36.0) < 1e-9,
            "electro-interference adds 2 to the target's own target number");
        Steps(sim, 99);
        check(enemy.InterferenceAimModifier == 2, "electro-interference lasts the full five seconds");
        Steps(sim, 1);
        check(enemy.InterferenceRemaining == 0 && CombatSimulation.HitChance(enemy, friendly, weapon) == clear,
            "electro-interference ends after five seconds");
        // The next discharge is ready ten seconds after the first.
        while (sim.History.Count(entry => !entry.Shot.Impact) < 2) Steps(sim, 1);
        enemy.InterferenceRemaining = 2;
        while (sim.History.Count(entry => entry.Shot.Impact) < 2) Steps(sim, 1);
        check(enemy.InterferenceRemaining == CombatRules.InterferenceSeconds && enemy.InterferenceAimModifier == 2,
            "a new PPC hit restarts electro-interference without stacking");

        sim = PpcFixture(gunnery: 9); enemy = Enemy(sim);
        Steps(sim, 10);
        check(!sim.History.Single().Shot.Hit && sim.History.Single().Shot.Splash == 0 && enemy.InterferenceRemaining == 0
                && enemy.ArmorLost + enemy.StructureLost == 0,
            "a PPC miss causes no splash or interference");

        sim = PpcFixture(); enemy = Enemy(sim);
        Steps(sim, 1);
        enemy.ApplyDamage(MechLocation.Head, 100000);
        Steps(sim, 8);
        check(sim.History[^1].Shot is { Impact: true, Wreck: true, Splash: 0 } && enemy.InterferenceRemaining == 0,
            "a bolt landing on a wreck causes no interference");
    }
}
