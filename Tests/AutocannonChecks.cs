using Godot;
using System.Collections.Immutable;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    // An AC/20 with lengthened brackets so a 30-hex shot stays in range; the leg bin holds fewer shots.
    private static CombatSimulation AutocannonFixture(int seed = 41, int gunnery = -10, float distance = 30, WeaponKind kind = WeaponKind.AC20,
        AmmoKind ammo = AmmoKind.AC20, int damage = 200)
    {
        var spec = DraftCatalog.Mech("awesome") with
        {
            Weapons = [new("cannon", "Test Cannon", MechLocation.RightArm, false, 0, new(DamageKind.Direct, damage, 1, 0), 0, 20, 25, 30, ammo, kind)],
            Components = [], HeatSinks = 0,
            Ammo = [new("bin-a", "Ammo A", MechLocation.LeftTorso, ammo, 3), new("bin-b", "Ammo B", MechLocation.LeftLeg, ammo, 1)]
        };
        var units = Enum.GetValues<CombatSide>().Select(side =>
        {
            var unit = new CombatUnit(side.ToString(), spec, DraftCatalog.Warriors[0] with { Gunnery = gunnery }, side,
                spec.Locations.Select(location => new LocationCondition(location.Location, location.Armor, location.RearArmor, location.Structure)),
                spec.Weapons.Select(weapon => weapon.Id).Concat(spec.Ammo.Select(bin => bin.Id))
                    .Select(id => new EquipmentCondition(id, ComponentCondition.Operational)),
                spec.Ammo.Select(bin => new AmmoCondition(bin.Id, bin.Shots)), side == CombatSide.Friendly ? Vector2.Zero : new Vector2(distance, 0));
            unit.Facing = side == CombatSide.Friendly ? Vector2.Right : Vector2.Left;
            return unit;
        }).ToArray();
        units[1].Firing = FiringMode.HoldFire;
        return new(units, seed, BattlefieldTerrain.Flat) { EnemyAiEnabled = false, Paused = false };
    }

    private static void CheckAutocannons(Action<bool, string> check)
    {
        check(Math.Abs(CombatRules.ShellFlightSeconds(60) - 1) < 1e-9, "a shell takes one second to fly 60 hexes");

        var sim = AutocannonFixture(); var friendly = Friendly(sim); var enemy = Enemy(sim);
        Steps(sim, 1);
        var discharge = sim.History.Single().Shot;
        check(discharge.Hit && discharge.InFlight && Math.Abs(discharge.FlightSeconds - .5) < 1e-6
                && enemy.ArmorLost + enemy.StructureLost == 0 && friendly.DamageDealt == 0,
            "an autocannon hit is rolled at discharge but deals no damage while in flight");
        check(sim.History[0].DebugText.Contains("in flight 0.50s") && sim.History[0].DebugText.Contains("dmg 200 on impact")
                && !sim.History[0].DebugText.Contains("armour"),
            "the discharge entry reports the shell in flight");
        check(friendly.Shots("bin-b") == 0 && friendly.Shots("bin-a") == 3, "each discharge spends a shot from the emptiest bin");
        Steps(sim, 9);
        check(sim.History.Count == 1, "the shell has not landed before its flight time");
        Steps(sim, 1);
        var impact = sim.History[^1].Shot;
        check(sim.History.Count == 2 && impact.Impact && impact.Sequence == discharge.Sequence && impact.Location == discharge.Location
                && impact.DamageApplied.Sum(part => part.Armor + part.RearArmor + part.Structure) == 200
                && enemy.ArmorLost + enemy.StructureLost == 200 && friendly.DamageDealt == 200 && friendly.ShotsFired == 1,
            "the shell applies its damage on impact as a separate entry");
        check(sim.History[^1].DebugText.Contains("impact") && !sim.History[^1].DebugText.Contains("chance"),
            "impact entries inherit their discharge roll");

        sim.Advance(40);
        friendly = Friendly(sim);
        check(friendly.ShotsFired == 4 && friendly.AmmoRemaining(AmmoKind.AC20) == 0 && !friendly.WeaponUsable(friendly.Weapons[0])
                && friendly.Status == CombatStatus.Active,
            "an emptied autocannon stops firing without disabling its mech");
        check(sim.WeaponStatuses(friendly, Enemy(sim), null, Enemy(sim))[0] == "Out of ammunition",
            "the weapon readout explains an empty autocannon");
        check(CombatScreen.SystemsReadout(friendly).Contains("Ammo A: Operational · Empty")
                && CombatScreen.SystemsReadout(Enemy(sim)).Contains("Ammo A: Operational · 3 shots"),
            "friendly inspection shows remaining shots per bin");
        var hidden = CombatScreen.SystemsReadout(friendly, enemy: true);
        check(hidden.Contains("Test Cannon: Operational") && hidden.Contains("Ammo A: Operational\n") && !hidden.Contains("Empty")
                && !hidden.Contains("shots") && !hidden.Contains("Unavailable"),
            "enemy inspection never reveals ammunition depletion");

        static void Destroy(CombatUnit unit, MechLocation location)
        { var part = unit.Locations[location]; unit.ApplyDamage(location, part.Armor + part.Structure); }
        sim = AutocannonFixture(); friendly = Friendly(sim);
        Destroy(friendly, MechLocation.LeftTorso); Destroy(friendly, MechLocation.LeftLeg);
        check(friendly.AmmoRemaining(AmmoKind.AC20) == 0 && friendly.Status == CombatStatus.Disabled,
            "bins in destroyed locations leave the pool, and losing them all is physical weapon loss");
        sim = AutocannonFixture(); friendly = Friendly(sim);
        Destroy(friendly, MechLocation.LeftTorso);
        check(friendly.AmmoRemaining(AmmoKind.AC20) == 1 && friendly.WeaponUsable(friendly.Weapons[0]), "surviving bins still feed the weapon");

        sim = AutocannonFixture(); Steps(sim, 1);
        Enemy(sim).ApplyDamage(MechLocation.CenterTorso, 100000); var lost = Enemy(sim).ArmorLost + Enemy(sim).StructureLost;
        Steps(sim, 10);
        check(sim.History.Count == 2 && sim.History[^1].Shot is { Wreck: true, Damage: 0 } && Enemy(sim).ArmorLost + Enemy(sim).StructureLost == lost
                && sim.History[^1].DebugText.Contains("no damage, target destroyed") && Friendly(sim).DamageDealt == 0,
            "a shell reaching a destroyed target explodes on the wreck without damage");

        sim = AutocannonFixture(); Steps(sim, 1);
        Friendly(sim).ApplyDamage(MechLocation.Head, 100000); Steps(sim, 10);
        check(sim.History.Count(entry => entry.Shot.Impact) == 1 && Enemy(sim).ArmorLost + Enemy(sim).StructureLost == 200,
            "a shell lands even after its shooter is destroyed");
        check(sim.WindingDown && sim.Outcome == CombatOutcome.Ongoing, "shells in flight land during the wind-down");

        // Armor is chosen from the bearing and the target's facing at impact.
        var seed = Enumerable.Range(1, 400).First(candidate =>
        {
            var probe = AutocannonFixture(candidate); Steps(probe, 1);
            return probe.History.Single().Shot.Location == MechLocation.CenterTorso;
        });
        sim = AutocannonFixture(seed); Steps(sim, 1);
        Enemy(sim).Facing = Vector2.Right; Enemy(sim).TorsoTwist = 0; Steps(sim, 10);
        check(sim.History[^1].Shot.DamageApplied.Any(part => part.Location == MechLocation.CenterTorso && part.RearArmor > 0),
            "a target that turns its back during the flight takes the shell on rear armor");

        sim = AutocannonFixture(gunnery: 8, seed: Enumerable.Range(1, 400).First(candidate =>
        {
            var probe = AutocannonFixture(candidate, 8); Steps(probe, 1);
            return !probe.History.Single().Shot.Hit;
        }));
        Steps(sim, 30);
        check(sim.History.Count == 1 && !sim.History[0].Shot.Hit && Friendly(sim).Shots("bin-b") == 0,
            "a missed shell spends ammunition and has no impact entry");

    }
}
