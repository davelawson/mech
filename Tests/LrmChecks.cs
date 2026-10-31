using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    // An LRM-15 (or SRM-6) shooter 25 hexes from its target, with optional friendly spotters whose own weapons are switched off.
    private static CombatSimulation LrmFixture(int seed = 41, int gunnery = 2, BattlefieldTerrain? terrain = null, bool srm = false, params Vector2[] spotters)
    {
        var ammo = srm ? AmmoKind.SRM6 : AmmoKind.LRM15;
        WeaponSpec rack = srm ? new("rack", "Test SRM", MechLocation.RightTorso, false, 0, new(DamageKind.Missile, 2, 6, 0), 0, 7, 14, 21, ammo, WeaponKind.SRM6)
            : new("rack", "Test LRM", MechLocation.RightTorso, false, 0, new(DamageKind.Missile, 1, 15, 0), 6, 7, 14, 21, ammo, WeaponKind.LRM15);
        var spec = DraftCatalog.Mech("awesome") with
        {
            Weapons = [rack], Components = [], HeatSinks = 0,
            Ammo = [new("bin-a", "Ammo A", MechLocation.LeftTorso, ammo, 3), new("bin-b", "Ammo B", MechLocation.LeftLeg, ammo, 1)]
        };
        CombatUnit Create(string id, CombatSide side, Vector2 position, Vector2 facing)
        {
            var unit = new CombatUnit(id, spec, DraftCatalog.Warriors[0] with { Gunnery = gunnery }, side,
                spec.Locations.Select(location => new LocationCondition(location.Location, location.Armor, location.RearArmor, location.Structure)),
                spec.Weapons.Select(weapon => weapon.Id).Concat(spec.Ammo.Select(bin => bin.Id))
                    .Select(id => new EquipmentCondition(id, ComponentCondition.Operational)),
                spec.Ammo.Select(bin => new AmmoCondition(bin.Id, bin.Shots)), position) { Facing = facing };
            return unit;
        }
        var units = new List<CombatUnit> { Create("shooter", CombatSide.Friendly, Vector2.Zero, Vector2.Right) };
        for (var i = 0; i < spotters.Length; i++)
        {
            var spotter = Create($"spotter-{i + 1}", CombatSide.Friendly, spotters[i], Vector2.Down);
            spotter.Weapons[0].Enabled = false; units.Add(spotter);
        }
        units.Add(Create("target", CombatSide.Enemy, new Vector2(25, 0), Vector2.Left));
        units[^1].Firing = FiringMode.HoldFire;
        return new(units, seed, terrain ?? BattlefieldTerrain.Flat) { EnemyAiEnabled = false, Paused = false };
    }

    private static void CheckLrms(Action<bool, string> check)
    {
        check(Math.Abs(CombatRules.MissileFlightSeconds(25) - 1) < 1e-9, "LRMs fly 25 hexes per second");
        check(Math.Abs(CombatRules.SrmFlightSeconds(27) - 1.08) < 1e-9 && Math.Abs(CombatRules.SrmFlightSeconds(9) - .5432) < 1e-3
                && CombatRules.SrmFlightSeconds(0) == 0,
            "SRMs accelerate from launch and still reach their 27-hex long range in 1.08 seconds");
        var srmHalf = CombatRules.SrmFlightSeconds(27) / 2;
        check(CombatRules.SrmLaunchSpeed * srmHalf + CombatRules.SrmAcceleration * srmHalf * srmHalf / 2 < 27 / 2.0,
            "an SRM covers less than half its range in the first half of its flight");
        check(CombatRules.FiresSalvo(WeaponKind.LRM20) && CombatRules.FiresSalvo(WeaponKind.SRM2) && !CombatRules.FiresSalvo(WeaponKind.AC20),
            "LRM and SRM racks fire missile salvos");

        var sim = LrmFixture(); var shooter = Friendly(sim); var target = Enemy(sim);
        Steps(sim, 1);
        var discharge = sim.History.Single().Shot;
        var hits = discharge.Missiles.Count(missile => missile.Hit);
        check(discharge.Missiles.Count == 15 && discharge.Missiles.All(missile => missile.Hit == missile.Roll < discharge.Chance)
                && discharge.Missiles.Select(missile => missile.Roll).Distinct().Count() == 15,
            "each missile in a salvo makes its own hit roll at the salvo's chance");
        check(hits is > 0 and < 15 && discharge.Hit && discharge.Damage == hits && discharge.MissileDamage == 1,
            "a salvo hits when any missile hits, and its damage totals the hits");
        check(discharge.Missiles.Where(missile => missile.Hit).Select(missile => missile.Location).Distinct().Count() > 1,
            "missiles roll their own locations, spreading a salvo's damage");
        check(shooter.Shots("bin-b") == 0 && shooter.Shots("bin-a") == 3, "a salvo spends one shot");
        check(discharge.InFlight && Math.Abs(discharge.FlightSeconds - 1) < 1e-6 && target.ArmorLost + target.StructureLost == 0
                && shooter.DamageDealt == 0, "salvo damage waits for the missiles to land");
        var debug = sim.History[0].DebugText;
        check(debug.Contains($"{hits}/15 HIT") && debug.Contains("rolls ") && debug.Contains("in flight 1.00s")
                && debug.Contains("hits ") && debug.Contains($"dmg {hits} on impact"),
            "the discharge entry reports missile hits, rolls and locations");
        check(sim.History[0].CombatText is { } combat && combat.Contains($"{hits}/15 HIT") && !combat.Contains("rolls "),
            "per-missile rolls appear only in debug");
        Steps(sim, 19);
        check(sim.History.Count == 1, "the salvo has not landed before its flight time");
        Steps(sim, 1);
        var impact = sim.History[^1].Shot;
        var landed = impact.DamageApplied.ToDictionary(part => part.Location, part => part.Armor + part.RearArmor + part.Structure);
        check(sim.History.Count == 2 && impact.Impact && impact.Sequence == discharge.Sequence && landed.Values.Sum() == hits
                && discharge.Missiles.Where(missile => missile.Hit).GroupBy(missile => missile.Location)
                    .All(group => landed.GetValueOrDefault(group.Key) == group.Count())
                && target.ArmorLost + target.StructureLost == hits && shooter.DamageDealt == hits
                && shooter.Hits == 1 && shooter.ShotsFired == 1,
            "every hit missile lands on its own location in one impact entry");

        sim = LrmFixture(); Steps(sim, 1);
        Enemy(sim).ApplyDamage(MechLocation.CenterTorso, 10000); var lost = Enemy(sim).ArmorLost + Enemy(sim).StructureLost;
        Steps(sim, 20);
        check(sim.History.Count == 2 && sim.History[^1].Shot is { Wreck: true, Damage: 0 } && Enemy(sim).ArmorLost + Enemy(sim).StructureLost == lost
                && Friendly(sim).DamageDealt == 0, "missiles reaching a destroyed target burst on the wreck without damage");
        sim = LrmFixture(); Steps(sim, 1); hits = sim.History[0].Shot.Damage;
        Friendly(sim).ApplyDamage(MechLocation.Head, 10000); Steps(sim, 20);
        check(sim.History.Count(entry => entry.Shot.Impact) == 1 && Enemy(sim).ArmorLost + Enemy(sim).StructureLost == hits,
            "missiles land even after their shooter is destroyed");

        var seed = Enumerable.Range(1, 400).First(candidate =>
        {
            var probe = LrmFixture(candidate, 7); Steps(probe, 1);
            return probe.History.Single().Shot is { Hit: false, Missiles.Count: 15 };
        });
        sim = LrmFixture(seed, 7); Steps(sim, 40);
        check(sim.History.Count == 1 && sim.History[0].Shot.Damage == 0 && Friendly(sim).Shots("bin-b") == 0
                && sim.History[0].DebugText.Contains("0/15 MISS"), "a salvo whose missiles all miss spends its shot and has no impact entry");

        // A rock hides the target from the shooter; spotters stand clear of it.
        var rock = new BattlefieldTerrain(1, [new(TerrainKind.Rock, new Vector2(12, 0), 2, 4)], []);
        sim = LrmFixture(terrain: rock, spotters: [new Vector2(25, 12)]); shooter = Friendly(sim); target = Enemy(sim); sim.RefreshDetection();
        var rack = shooter.Weapons[0];
        check(sim.Detected(CombatSide.Friendly, target.Id) && !sim.Terrain.ClearSight(shooter.Position, target.Position),
            "the spotter detects a target the shooter cannot see");
        Steps(sim, 1);
        check(sim.History.Count == 0 && sim.TerrainHitChance(shooter, target, rack) == 0
                && sim.WeaponStatus(shooter, rack, target) == "Line of fire blocked",
            "LRMs never fire indirectly at an opportunistic target");
        sim.SetPriority([shooter.Id], target.Id);
        var spotter = sim.Units[1];
        check(sim.IndirectSpotter(shooter, target, rack) == spotter
                && Math.Abs(sim.TerrainHitChance(shooter, target, rack) - CombatSimulation.HitChance(shooter, target, rack, CombatRules.IndirectFireModifier)) < 1e-9
                && sim.TerrainHitChance(shooter, target, rack) < CombatSimulation.HitChance(shooter, target, rack),
            "indirect fire at a priority target adds the indirect modifier");
        Steps(sim, 1);
        check(sim.History.Single().Shot is { Indirect: true, Missiles.Count: 15 } indirect && indirect.SpotterId == spotter.Id
                && sim.History[0].CombatText!.Contains("indirect, spotted by F2"), "the salvo fires indirectly and the log names its spotter");

        sim = LrmFixture(terrain: rock, spotters: [new Vector2(25, 12)]); shooter = Friendly(sim); target = Enemy(sim); sim.RefreshDetection();
        shooter.Firing = FiringMode.HoldFire; sim.Attack([shooter.Id], target.Id);
        check(sim.IndirectSpotter(shooter, target, shooter.Weapons[0]) is not null && sim.CanFire(shooter, target, shooter.Weapons[0]),
            "an explicit Attack order permits indirect fire, even under Hold fire");
        sim.Units[1].ApplyDamage(MechLocation.Head, 10000); sim.RefreshDetection();
        check(sim.IndirectSpotter(shooter, target, shooter.Weapons[0]) is null && !sim.CanFire(shooter, target, shooter.Weapons[0]),
            "indirect fire needs an active spotter with its own clear sight");

        sim = LrmFixture(srm: true); Steps(sim, 1);
        discharge = sim.History.Single().Shot; hits = discharge.Missiles.Count(missile => missile.Hit);
        check(discharge.Missiles.Count == 6 && discharge.Missiles.All(missile => missile.Hit == missile.Roll < discharge.Chance)
                && discharge.InFlight && Math.Abs(discharge.FlightSeconds - CombatRules.SrmFlightSeconds(25)) < 1e-6 && discharge.Damage == 2 * hits && discharge.MissileDamage == 2
                && Friendly(sim).Shots("bin-b") == 0 && Enemy(sim).ArmorLost + Enemy(sim).StructureLost == 0,
            "an SRM salvo rolls each missile, spends one shot and lands after its accelerating flight");
        Steps(sim, 22);
        check(sim.History.Count == (hits > 0 ? 2 : 1) && Enemy(sim).ArmorLost + Enemy(sim).StructureLost == 2 * hits,
            "SRM missiles each deal their stock damage on impact");
        sim = LrmFixture(terrain: rock, srm: true, spotters: [new Vector2(25, 12)]); shooter = Friendly(sim); target = Enemy(sim); sim.RefreshDetection();
        shooter.Firing = FiringMode.HoldFire; sim.Attack([shooter.Id], target.Id); sim.SetPriority([shooter.Id], target.Id);
        check(sim.IndirectSpotter(shooter, target, shooter.Weapons[0]) is null && !sim.CanFire(shooter, target, shooter.Weapons[0])
                && sim.TerrainHitChance(shooter, target, shooter.Weapons[0]) == 0,
            "SRMs never fire indirectly, even at an Attack or priority target");

        // The nearer spotter looks through vegetation; the farther one is clear and wins.
        var screened = new BattlefieldTerrain(2, [new(TerrainKind.Rock, new Vector2(12, 0), 2, 4),
            new(TerrainKind.Vegetation, new Vector2(25, 4), 1.5f)], []);
        sim = LrmFixture(terrain: screened, spotters: [new Vector2(25, 8), new Vector2(25, -16)]); shooter = Friendly(sim); target = Enemy(sim);
        sim.RefreshDetection(); sim.SetPriority([shooter.Id], target.Id); rack = shooter.Weapons[0];
        check(sim.IndirectSpotter(shooter, target, rack) == sim.Units[2], "the spotter with the least vegetation is chosen before the nearest");
        sim.Units[2].ApplyDamage(MechLocation.Head, 10000);
        check(sim.IndirectSpotter(shooter, target, rack) == sim.Units[1]
                && Math.Abs(sim.TerrainHitChance(shooter, target, rack) - CombatSimulation.HitChance(shooter, target, rack, 1 + CombatRules.IndirectFireModifier)) < 1e-9,
            "indirect fire uses the spotter's vegetation modifier");
    }
}
