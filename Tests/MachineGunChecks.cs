using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    // The stock machine gun at short range; the enemy holds fire.
    private static CombatSimulation MachineGunFixture(int seed = 41, int gunnery = -10, float distance = 3)
    {
        var gun = DraftCatalog.Mech("firestarter").Weapons.First(weapon => weapon.Kind == WeaponKind.MachineGun) with { Id = "gun", Location = MechLocation.RightArm, Rear = false };
        var spec = DraftCatalog.Mech("awesome") with
        {
            Weapons = [gun], Components = [], HeatSinks = 0,
            Ammo = [new("bin-a", "Machine Gun ammunition", MechLocation.LeftTorso, AmmoKind.MachineGun, 200)]
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

    private static void CheckMachineGuns(Action<bool, string> check)
    {
        var stock = DraftCatalog.Mech("firestarter").Weapons.First(weapon => weapon.Kind == WeaponKind.MachineGun);
        check(CombatRules.Damage(stock) == 5 && CombatRules.Cooldown(stock) == 2 && stock.Heat == 0,
            "a machine gun burst deals 5 (half a stock point) every two seconds without heat");
        check(Math.Abs(CombatRules.BulletFlightSeconds(120) - 1) < 1e-9 && CombatRules.MachineGunBurstSeconds == 1,
            "tracers fly 120 hexes per second in a one-second burst");

        var sim = MachineGunFixture(); var friendly = Friendly(sim); var enemy = Enemy(sim);
        Steps(sim, 1);
        var discharge = sim.History.Single().Shot;
        check(discharge.Hit && discharge.InFlight && Math.Abs(discharge.FlightSeconds - 3 / 120.0) < 1e-6
                && enemy.ArmorLost + enemy.StructureLost == 0 && friendly.Shots("bin-a") == 199,
            "a burst makes one hit roll and spends one shot, and its damage waits for the first round to arrive");
        check(sim.History[0].DebugText.Contains("dmg 5 on impact"), "the discharge entry reports the burst's damage");
        Steps(sim, 1);
        var impact = sim.History[^1].Shot;
        check(sim.History.Count == 2 && impact.Impact && impact.Sequence == discharge.Sequence
                && impact.DamageApplied.Sum(part => part.Armor + part.RearArmor + part.Structure) == 5 && friendly.DamageDealt == 5,
            "the burst's damage lands with its first round as a separate impact entry");
        Steps(sim, 43);
        var bursts = sim.History.Where(entry => !entry.Shot.Impact).ToArray();
        check(bursts.Length == 2 && Math.Abs(bursts[1].Time - bursts[0].Time - 2) < CombatRules.StepSeconds + 1e-6 && friendly.Shots("bin-a") == 198,
            "the next burst starts two seconds after the first");

        sim = MachineGunFixture(); Steps(sim, 1);
        Enemy(sim).ApplyDamage(MechLocation.CenterTorso, 100000); var lost = Enemy(sim).ArmorLost + Enemy(sim).StructureLost;
        Steps(sim, 1);
        check(sim.History[^1].Shot is { Impact: true, Wreck: true, Damage: 0 } && Enemy(sim).ArmorLost + Enemy(sim).StructureLost == lost,
            "rounds reaching a destroyed target do no damage");
    }
}
