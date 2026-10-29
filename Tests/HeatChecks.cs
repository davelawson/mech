using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static WeaponSpec HeatGun(string id, int heat, int damage) =>
        new(id, id, MechLocation.RightArm, false, heat, damage.ToString(), 0, 20, 25, 30, null);

    private static CombatSimulation HeatFixture(WeaponSpec[] weapons, int sinks = 0, string sinkType = "Single",
        ComponentSpec[]? components = null, ComponentCondition sinkCondition = ComponentCondition.Operational,
        int seed = 41, int gunnery = -10)
    {
        var spec = DraftCatalog.Mech("awesome") with
        { Weapons = weapons, HeatSinks = sinks, HeatSinkType = sinkType, Components = components ?? [], Ammo = [] };
        var units = Enum.GetValues<CombatSide>().Select(side =>
        {
            var unit = new CombatUnit(side.ToString(), spec, DraftCatalog.Warriors[0] with { Gunnery = gunnery }, side,
                spec.Locations.Select(location => new LocationCondition(location.Location, location.Armor, location.RearArmor, location.Structure)),
                spec.Weapons.Select(weapon => new EquipmentCondition(weapon.Id, ComponentCondition.Operational))
                    .Concat(spec.Components.Select(component => new EquipmentCondition(component.Id, sinkCondition))),
                [], side == CombatSide.Friendly ? Vector2.Zero : new Vector2(2, 0));
            unit.Facing = side == CombatSide.Friendly ? Vector2.Right : Vector2.Left;
            return unit;
        });
        return new(units, seed, BattlefieldTerrain.Flat) { EnemyAiEnabled = false };
    }

    private static void CheckHeat(Action<bool, string> check)
    {
        CheckHeatRates(check);
        CheckHeatSelection(check);
        CheckHeatShutdown(check);
        CheckFlamerHeat(check);
    }

    private static void CheckHeatRates(Action<bool, string> check)
    {
        var sim = HeatFixture([HeatGun("test", 7, 1)]);
        var f = Friendly(sim); var e = Enemy(sim); e.Firing = FiringMode.HoldFire;
        check(sim.Units.All(unit => unit.Heat == 0 && unit.HeatPolicy == HeatPolicy.Normal), "both sides deploy cold with Normal heat policy");
        sim.Paused = false; sim.Advance(.05);
        check(f.Heat == 7 && f.ShotsFired == 1 && e.Heat == 0, "weapon adds stock heat immediately on discharge");
        sim.Paused = true; sim.Advance(20);
        check(f.Heat == 7 && sim.Elapsed == .05, "pause freezes heat and cooling");

        foreach (var (sinkType, expected) in new[] { ("Single", 14d), ("Double", 13d) })
        {
            sim = HeatFixture([HeatGun("test", 0, 1)], 10, sinkType);
            foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
            f = Friendly(sim); f.AddHeat(15); sim.Paused = false; sim.Advance(1);
            check(Math.Abs(f.Heat - expected) < .00001, $"{sinkType} sinks cool continuously on the ten-second clock");
            sim.Advance(20);
            check(f.Heat == 0, "cooling clamps heat to zero");
        }

        sim = HeatFixture([HeatGun("test", 0, 1)]); f = Friendly(sim);
        f.Moving = true; f.CurrentSpeed = f.UnheatedWalkingSpeed; f.Movement = MovementMode.Run;
        f.AdvanceHeat(10);
        check(f.Heat == 1, "walking-speed actual travel generates one heat per ten seconds despite Run selection");
        f.CurrentSpeed = -f.UnheatedWalkingSpeed; f.AdvanceHeat(10);
        check(f.Heat == 2, "reverse generates walking heat");
        f.CurrentSpeed = f.UnheatedRunningSpeed; f.Movement = MovementMode.Walk; f.AdvanceHeat(10);
        check(f.Heat == 4, "actual running speed generates running heat even while braking into Walk mode");
        f.Moving = false; f.AngularVelocity = 1; f.AdvanceHeat(10);
        check(f.Heat == 4, "stationary rotation produces no heat");

        var sink = new ComponentSpec("sinks", "Heat Sink", MechLocation.LeftArm, 3);
        foreach (var condition in Enum.GetValues<ComponentCondition>())
        {
            sim = HeatFixture([HeatGun("test", 0, 1)], 10, components: [sink], sinkCondition: condition); f = Friendly(sim);
            check(f.EffectiveHeatSinks == (condition == ComponentCondition.Destroyed ? 7 : 10), $"{condition} sink-group condition has agreed cooling output");
            f.ApplyDamage(MechLocation.LeftArm, f.Locations[MechLocation.LeftArm].Armor + f.Locations[MechLocation.LeftArm].Structure);
            check(f.EffectiveHeatSinks == 7 && Math.Abs(f.CoolingRate - .7) < .00001, "lost sink group subtracts grouped sinks once, preserving internal remainder");
        }
        sim = Battle(); f = Friendly(sim);
        var lostSinks = f.Spec.Components.Where(component => component.Name == "Heat Sink"
            && component.Location is MechLocation.RightTorso or MechLocation.RightArm).Sum(component => component.Slots);
        f.ApplyDamage(MechLocation.RightTorso, f.Locations[MechLocation.RightTorso].Armor + f.Locations[MechLocation.RightTorso].Structure);
        check(f.EffectiveHeatSinks == f.Spec.HeatSinks - lostSinks, "torso loss also removes heat sinks in the attached arm");
        sim = Battle(definition: "mauler");
        check(Friendly(sim).EffectiveHeatSinks == 11 && Math.Abs(Friendly(sim).CoolingRate - 2.2) < .00001, "Mauler internal double sinks cool without explicit component entries");

        sim = HeatFixture([HeatGun("test", 0, 1)], gunnery: 4); f = Friendly(sim); e = Enemy(sim);
        var speed = f.WalkingSpeed; var acceleration = f.Acceleration; var chance = CombatSimulation.HitChance(f, e, f.Weapons[0]);
        f.AddHeat(9.99);
        check(f.HeatBand == "Cool" && f.WalkingSpeed == speed && CombatSimulation.HitChance(f, e, f.Weapons[0]) == chance, "heat below ten has no penalties");
        f.AddHeat(.01);
        check(f.HeatBand == "Warm" && Math.Abs(f.WalkingSpeed - speed * .9f) < .00001
            && Math.Abs(CombatSimulation.HitChance(f, e, f.Weapons[0]) - 15d / 36) < .00001, "ten heat applies 90% speed and +1 to the 2d6 target number");
        f.AddHeat(10);
        check(f.HeatBand == "Hot" && Math.Abs(f.WalkingSpeed - speed * .75f) < .00001
            && Math.Abs(CombatSimulation.HitChance(f, e, f.Weapons[0]) - 10d / 36) < .00001, "twenty heat applies 75% speed and +2 to the 2d6 target number");
        check(f.Acceleration == acceleration, "heat speed limits preserve authored acceleration and braking");
    }

    private static void CheckHeatSelection(Action<bool, string> check)
    {
        var sim = HeatFixture([HeatGun("first", 10, 10), HeatGun("second", 10, 10), HeatGun("third", 10, 10)]);
        var f = Friendly(sim); var e = Enemy(sim);
        check(sim.SelectHeatVolley(f, e, null).Select(shot => shot.Weapon.Spec.Id).SequenceEqual(new[] { "first" }), "Normal excludes the twenty-heat boundary and breaks equal-value ties by catalog order");
        f.HeatPolicy = HeatPolicy.Aggressive;
        check(sim.SelectHeatVolley(f, e, null).Count == 2, "Aggressive permits a stronger volley but excludes the thirty-heat boundary");
        f.AddHeat(.1);
        check(sim.SelectHeatVolley(f, e, null).Count == 2, "fractional heat correctly budgets integer per-shot costs");
        f.AddHeat(9.9);
        check(sim.SelectHeatVolley(f, e, null).Count == 1, "exact current heat respects exclusive policy ceiling");

        sim = HeatFixture([HeatGun("expensive", 12, 11), HeatGun("efficient-a", 9, 9), HeatGun("efficient-b", 9, 9)]);
        f = Friendly(sim); e = Enemy(sim);
        check(sim.SelectHeatVolley(f, e, null).Select(shot => shot.Weapon.Spec.Id).SequenceEqual(new[] { "efficient-a", "efficient-b" }), "volley optimizer selects the best combination rather than first or largest weapon");
        sim.Paused = false; e.Firing = FiringMode.HoldFire; sim.Advance(.05);
        check(f.ShotsFired == 2 && f.Heat == 18 && f.Weapons[0].Remaining == 0, "withheld weapon stays ready and unspent while selected weapons generate heat");
        check(sim.WeaponStatus(f, f.Weapons[0], e).StartsWith("Cooling"), "weapon status explains withholding for heat");

        sim = HeatFixture([HeatGun("more-heat", 9, 5), HeatGun("less-heat", 8, 5)]); f = Friendly(sim); e = Enemy(sim); f.AddHeat(5);
        check(sim.SelectHeatVolley(f, e, null).Single().Weapon.Spec.Id == "less-heat", "equal expected damage favors less heat before catalog order");
        sim = HeatFixture([HeatGun("first", 12, 5), HeatGun("last", 12, 10) with { Short = 1, Medium = 1 }]);
        f = Friendly(sim); e = Enemy(sim);
        check(sim.SelectHeatVolley(f, e, null).Single().Weapon.Spec.Id == "last", "guaranteed-hit higher damage wins when both weapons fit individually");
        sim = HeatFixture([HeatGun("accurate", 12, 5), HeatGun("inaccurate", 12, 10) with { Short = 1, Medium = 1 }], gunnery: 4);
        f = Friendly(sim); e = Enemy(sim);
        check(sim.SelectHeatVolley(f, e, null).Single().Weapon.Spec.Id == "accurate", "optimizer accounts for range-dependent hit chance rather than raw damage");

        sim = HeatFixture([HeatGun("free-a", 0, 1), HeatGun("free-b", 0, 1), HeatGun("hot", 12, 1)]); f = Friendly(sim); e = Enemy(sim); f.AddHeat(19.5);
        check(sim.SelectHeatVolley(f, e, null).Count == 2, "zero-heat weapons each fire once when no positive-heat weapon fits");
        f.Weapons[0].Enabled = false; f.Weapons[1].Remaining = 1;
        check(sim.SelectHeatVolley(f, e, null).Count == 0, "heat selection preserves weapon toggles and independent cooldowns");
        f.AddHeat(.5);
        check(sim.SelectHeatVolley(f, e, null).Count == 0, "Normal waits to cool when already at its ceiling");

        // An exhaustive small-loadout oracle checks the optimizer against every legal subset.
        var random = new Random(173);
        var optimal = true;
        for (var sample = 0; sample < 40; sample++)
        {
            var weapons = Enumerable.Range(0, 8).Select(index => HeatGun($"gun-{index}", random.Next(0, 13), random.Next(1, 21))).ToArray();
            sim = HeatFixture(weapons); f = Friendly(sim); e = Enemy(sim);
            f.HeatPolicy = sample % 2 == 0 ? HeatPolicy.Normal : HeatPolicy.Aggressive;
            f.AddHeat(random.Next(0, 38) / 2d);
            var expectedDamage = 0; var expectedHeat = 0;
            for (var mask = 0; mask < 1 << weapons.Length; mask++)
            {
                var subset = weapons.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
                var heat = subset.Sum(weapon => weapon.Heat); var damage = subset.Sum(CombatRules.Damage);
                if (f.Heat + heat >= CombatRules.HeatLimit(f.HeatPolicy)) continue;
                if (damage > expectedDamage || damage == expectedDamage && heat < expectedHeat)
                { expectedDamage = damage; expectedHeat = heat; }
            }
            var actual = sim.SelectHeatVolley(f, e, null);
            optimal &= actual.Sum(shot => shot.Weapon.Damage) == expectedDamage
                && actual.Sum(shot => shot.Weapon.Spec.Heat) == expectedHeat;
        }
        check(optimal, "heat-budget optimizer matches exhaustive subset selection across mixed loadouts and policies");
        sim = HeatFixture([HeatGun("impossible", 3, 1)], gunnery: 20); f = Friendly(sim); e = Enemy(sim);
        check(sim.SelectHeatVolley(f, e, null).Count == 0 && sim.WeaponStatus(f, f.Weapons[0], e) == "Cannot hit",
            "zero-probability weapons explain their firing solution rather than falsely reporting cooling");
    }

    private static void CheckHeatShutdown(Action<bool, string> check)
    {
        var sim = HeatFixture([HeatGun("free", 0, 1)], 10); var f = Friendly(sim); var e = Enemy(sim);
        foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
        f.Facing = Vector2.Left; f.CurrentSpeed = 1; f.Moving = true; f.AddHeat(35); sim.RefreshDetection();
        check(f.Shutdown && f.Status == CombatStatus.Shutdown && !f.Active && !f.Defeated
            && f.CurrentSpeed == 0 && !f.Moving && f.WalkingSpeed == 0, "shutdown stops travel and is distinct from physical defeat");
        check(!sim.Detected(f.Side, e.Id) && sim.Detected(e.Side, f.Id), "shutdown removes sensor contribution but remains detectable and targetable");
        e.Firing = FiringMode.FireAtWill;
        check(sim.SelectTarget(e) == f && sim.CanFire(e, f, e.Weapons[0]), "active enemies can target and fire on a thermal shutdown");
        e.Firing = FiringMode.HoldFire;
        sim.Move([f.Id], new Vector2(-10, 0));
        check(f.Order == CombatOrder.Move && f.Destination == new Vector2(-10, 0), "shutdown accepts queued movement orders");
        sim.Paused = false; sim.Advance(24.9);
        check(f.Shutdown && f.Position == Vector2.Zero && f.Heat > 10 && sim.Outcome == CombatOutcome.Ongoing, "shutdown persists above restart threshold without counting as defeat");
        sim.Advance(.2);
        check(f.Active && f.Heat < 10 && f.Position != Vector2.Zero && f.Order == CombatOrder.Move, "automatic restart below ten resumes retained movement");

        sim = HeatFixture([HeatGun("free", 0, 1)], 10); f = Friendly(sim);
        foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
        f.AddHeat(35); sim.Paused = false; sim.Advance(25);
        check(f.Heat == 10 && f.Shutdown, "exactly ten heat does not restart due to numerical drift");
        sim.Advance(.05);
        check(f.Active && Math.Abs(f.Heat - 9.95) < .000001, "restart occurs on the first step strictly below ten");

        sim = HeatFixture([HeatGun("free", 0, 1)], 10);
        foreach (var unit in sim.Units) { unit.AddHeat(40); unit.Firing = FiringMode.HoldFire; }
        f = Friendly(sim); f.AddHeat(15); sim.Paused = false; sim.Advance(1);
        check(sim.Outcome == CombatOutcome.Ongoing && f.Heat > 53.9 && sim.Units.All(unit => unit.Shutdown), "both sides can shut down without a draw and excess heat extends cooling");
        f.ApplyDamage(MechLocation.Head, 100); sim.Advance(.05);
        check(sim.Outcome == CombatOutcome.Defeat && f.Status == CombatStatus.Destroyed, "physical defeat still ends combat during a thermal shutdown");

        sim = Battle(2); sim.EnemyAiEnabled = false;
        foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        friends[0].AddHeat(35); sim.Follow([friends[1].Id], friends[0].Id);
        sim.Paused = false; sim.Advance(.05);
        check(friends[1].Order == CombatOrder.Follow && friends[1].FollowTarget == friends[0].Id, "temporary shutdown preserves Follow leaders and orders");
    }

    private static void CheckFlamerHeat(Action<bool, string> check)
    {
        foreach (var reverse in new[] { false, true })
        {
            var flamers = Enumerable.Range(0, 4).Select(index => HeatGun($"flamer-{index}", 3, 2) with { Name = "Flamer" }).ToArray();
            var fixture = HeatFixture([.. flamers, HeatGun("return", 0, 1)]);
            var sim = reverse ? new CombatSimulation(fixture.Units.Reverse(), 41, BattlefieldTerrain.Flat) { EnemyAiEnabled = false } : fixture;
            var f = Friendly(sim); var e = Enemy(sim);
            f.Weapons[^1].Enabled = false;
            foreach (var weapon in e.Weapons.Take(4)) weapon.Enabled = false;
            e.HeatPolicy = HeatPolicy.Aggressive; e.AddHeat(28);
            var shots = new List<CombatShot>(); sim.ShotFired += shots.Add;
            sim.Paused = false; sim.Advance(.05);
            check(f.ShotsFired == 4 && e.ShotsFired == 1 && e.Heat == 36 && e.Shutdown && f.Heat == 12,
                $"four flamers stack eight heat above shutdown while preserving simultaneous return fire, reversed={reverse}");
            check(shots.Where(shot => shot.AttackerId == f.Id).All(shot => shot.Hit && shot.Damage == 2 && shot.TargetHeat == 2), "flamers retain ordinary damage and heat through armor");
        }
        var missSeed = Enumerable.Range(1, 100).First(seed => new Random(seed).NextDouble() >= 21d / 36);
        var miss = HeatFixture([HeatGun("flamer", 3, 2) with { Name = "Flamer" }], seed: missSeed, gunnery: 4);
        Enemy(miss).Firing = FiringMode.HoldFire; miss.Paused = false; miss.Advance(.05);
        check(Friendly(miss).ShotsFired == 1 && Friendly(miss).Hits == 0 && Friendly(miss).Heat == 3 && Enemy(miss).Heat == 0,
            "missed flamer generates firing heat but no target heat");

        var snapshotAccuracy = true;
        for (var seed = 0; seed < 30; seed++)
        {
            var heated = HeatFixture([HeatGun("a", 6, 1), HeatGun("b", 6, 1)], seed: seed, gunnery: 4);
            var control = HeatFixture([HeatGun("a", 0, 1), HeatGun("b", 0, 1)], seed: seed, gunnery: 4);
            foreach (var battle in new[] { heated, control })
            {
                Friendly(battle).AddHeat(9); Friendly(battle).HeatPolicy = HeatPolicy.Aggressive;
                Enemy(battle).Firing = FiringMode.HoldFire;
                battle.Paused = false; battle.Advance(.05);
            }
            snapshotAccuracy &= Friendly(heated).Heat == 21 && Friendly(heated).ShotsFired == 2
                && Friendly(heated).Hits == Friendly(control).Hits;
        }
        check(snapshotAccuracy, "crossing both penalty bands in one volley preserves start-of-exchange accuracy for every shot");

        var a = HeatFixture([HeatGun("hot", 12, 1), HeatGun("warm", 7, 1)], 10);
        var b = HeatFixture([HeatGun("hot", 12, 1), HeatGun("warm", 7, 1)], 10);
        a.Paused = b.Paused = false; a.Advance(5);
        for (var i = 0; i < 50; i++) b.Advance(.1);
        check(a.Units.Zip(b.Units).All(pair => Math.Abs(pair.First.Heat - pair.Second.Heat) < .000001
            && pair.First.ShotsFired == pair.Second.ShotsFired && pair.First.Shutdown == pair.Second.Shutdown), "heat and policy selection remain independent of frame partitioning");
    }
}
