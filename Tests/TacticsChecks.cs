using Godot;

namespace Mech.Tests;

/// <summary>The enemy AI's group tactics: lances, route-search throttling, what the AI may know, focus fire and target switching,
/// movement, cohesion, pulling back and routing. Engine-free; flat ground unless a check needs terrain.</summary>
internal static class TacticsChecks
{
    private static WarriorSpec Pilot(EnemyPilotRank rank = EnemyPilotRank.Lieutenant, int number = 1) => MissionTuning.EnemyPilot(rank, number);

    private static CombatUnit Mech(string id, string chassis, CombatSide side, Vector2 position, WarriorSpec? pilot = null, int? group = null, int? wave = null)
    {
        var spec = DraftCatalog.Mech(chassis);
        return new CombatUnit(id, spec, pilot ?? (side == CombatSide.Enemy ? Pilot() : TestPilots.Warriors[0]), side,
            spec.Locations.Select(part => new LocationCondition(part.Location, part.Armor, part.RearArmor, part.Structure)),
            spec.Weapons.Select(item => item.Id).Concat(spec.Components.Select(item => item.Id)).Concat(spec.Ammo.Select(item => item.Id))
                .Select(item => new EquipmentCondition(item, ComponentCondition.Operational)),
            spec.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)), position, side == CombatSide.Friendly ? 0 : null, group) { Wave = wave };
    }
    private static CombatUnit Enemy(string chassis, Vector2 position, EnemyPilotRank rank = EnemyPilotRank.Lieutenant, int number = 1) =>
        Mech($"enemy-{number}", chassis, CombatSide.Enemy, position, Pilot(rank, number));
    private static CombatUnit Friend(string chassis, Vector2 position, int number = 1) =>
        Mech($"mech-{number}", chassis, CombatSide.Friendly, position, TestPilots.Warriors[number - 1]);

    /// <summary>A running battle on flat ground with steady pilots, the company holding its fire. Enemies get the highest heat limit,
    /// so firing never makes them walk and each check sees only the rule it is about.</summary>
    private static CombatSimulation Battle(params CombatUnit[] units)
    {
        foreach (var unit in units)
        {
            unit.Facing = (units.Where(other => other.Side != unit.Side).Select(other => other.Position).DefaultIfEmpty(Vector2.Zero).First() - unit.Position).Normalized();
            if (unit.Side == CombatSide.Friendly) unit.Firing = FiringMode.HoldFire;
            else unit.HeatLimit = CombatRules.MaxHeatLimit;
        }
        var sim = new CombatSimulation(units, 41) { NerveChecksEnabled = false, Paused = false };
        sim.RefreshDetection();
        return sim;
    }

    public static void Run(Action<bool, string> check)
    {
        CheckGroups(check);
        CheckRouteThrottle(check);
        CheckKnowledge(check);
        CheckFocus(check);
        CheckSwitching(check);
        CheckMovement(check);
        CheckCohesion(check);
        CheckPullBack(check);
        CheckRout(check);
        CheckRanks(check);
    }

    private static void CheckGroups(Action<bool, string> check)
    {
        // Heaviest first, dealt into as few lances of four as hold them: Awesome, Catapult, Griffin, Jenner, Locust go A, B, A, B, A.
        var dealt = Battle(Enemy("locust", new(0, 0), number: 1), Enemy("griffin", new(100, 0), number: 2), Enemy("awesome", new(200, 0), number: 3),
            Enemy("jenner", new(300, 0), number: 4), Enemy("catapult", new(400, 0), number: 5));
        var lances = dealt.Units.GroupBy(unit => unit.TacticalGroup).ToDictionary(lance => lance.Key!.Value, lance => lance.Select(unit => unit.Spec.Chassis).ToArray());
        check(lances.Count == 2 && lances[0].SequenceEqual(["Locust", "Griffin", "Awesome"]) && lances[1].SequenceEqual(["Jenner", "Catapult"]),
            $"five enemies make two lances, dealt heaviest first ({string.Join(" / ", lances.Values.Select(lance => string.Join(", ", lance)))})");
        check(dealt.Units.All(unit => unit.Group is null), "a standard battle's lances leave the patrol group unset, so its results keep one Opposition table");

        var patrol = Battle(Mech("enemy-1", "locust", CombatSide.Enemy, new(0, 0), group: 1), Mech("enemy-2", "jenner", CombatSide.Enemy, new(50, 0), group: 0),
            Mech("enemy-3", "griffin", CombatSide.Enemy, new(100, 0), group: 1));
        check(patrol.Units[0].TacticalGroup == patrol.Units[2].TacticalGroup && patrol.Units[1].TacticalGroup != patrol.Units[0].TacticalGroup
            && patrol.Units.All(unit => unit.Group is not null), "a patrol's enemy groups are its tactical groups, unchanged");

        var hold = Battle([.. Enumerable.Range(1, 5).Select(i => Mech($"enemy-{i}", "jenner", CombatSide.Enemy, new(i * 50, 0), Pilot(number: i), wave: 0)),
            Mech("enemy-6", "locust", CombatSide.Enemy, new(0, 400), Pilot(number: 6), wave: 1), Mech("enemy-7", "locust", CombatSide.Enemy, new(50, 400), Pilot(number: 7), wave: 1)]);
        var garrison = hold.Units.Where(unit => unit.Wave == 0).Select(unit => unit.TacticalGroup).Distinct().ToArray();
        var wave = hold.Units.Where(unit => unit.Wave == 1).Select(unit => unit.TacticalGroup).Distinct().ToArray();
        check(garrison.Length == 2 && wave.Length == 1 && !garrison.Contains(wave[0]), "a garrison of five forms two lances, and each wave its own");

        // A mission's opposition of six lands as two lances, each around its own drop site.
        var roster = ForceRoster.Create("Lances", [DraftCatalog.Mech("griffin")], TestPilots.Warriors.Take(1));
        roster.Assign(roster.Mechs[0].Id, roster.Warriors[0].Id);
        EnemyMech[] six = [.. new[] { "locust", "jenner", "griffin", "wolverine", "catapult", "awesome" }.Select(mech => new EnemyMech(mech, EnemyPilotRank.Sergeant))];
        var offer = new MissionOffer("offer-1", "Raid", "Briefing.", "Destroy", StarChart.Instance.StartingSystemId, Campaign.StartDate, six, 100_000, 0, 0,
            Party.Of(Faction.Steiner), Party.Pirates, MissionKind.Raid);
        var raid = CombatSimulation.DeployMission(roster, [roster.Mechs[0].Id], offer, 5, 183, Biome.Grassland);
        var landed = raid.Units.Where(unit => unit.Side == CombatSide.Enemy).GroupBy(unit => unit.TacticalGroup).ToArray();
        check(landed.Length == 2 && landed.All(lance => lance.All(unit => lance.All(other => other.Position.DistanceTo(unit.Position) <= CombatRules.DropSiteRadius * 2 + 1))),
            "each lance of a mission's opposition lands together on its own drop site");
    }

    private static void CheckRouteThrottle(Action<bool, string> check)
    {
        // Standing inside a rock, a mech can find no route anywhere.
        var rock = new BattlefieldTerrain(1, [new(TerrainKind.Rock, Vector2.Zero, 60, 120)], []);
        var stuck = Friend("griffin", Vector2.Zero);
        var sim = new CombatSimulation([stuck, Enemy("griffin", new(0, 2800))], 41, rock) { EnemyAiEnabled = false, NerveChecksEnabled = false, Paused = false };
        // A Move order resolves its goal to where the mech stands; the AI's goals are not resolved first.
        stuck.Order = CombatOrder.Move; stuck.Destination = new Vector2(500, 0);
        sim.Advance(10);
        check(stuck.PathPlans is >= 9 and <= 11, $"a mech that can find no route searches about once a second, not every step ({stuck.PathPlans} searches in 10 s)");
        var free = Friend("griffin", new(-500, 0));
        var open = new CombatSimulation([free, Enemy("griffin", new(0, 2800))], 41, rock) { EnemyAiEnabled = false, NerveChecksEnabled = false, Paused = false };
        open.Move([free.Id], new Vector2(500, 0)); open.Advance(.05);
        check(free.PathPlans == 1 && free.Route.Count > 0, "a mech with a route plans it at once");
    }

    private static void CheckKnowledge(Action<bool, string> check)
    {
        // A Lieutenant's sensors identify an Awesome out to about 2,300 m and see its condition within half that.
        var shooter = Enemy("griffin", new(0, 1700)); var target = Friend("awesome", Vector2.Zero);
        var sim = Battle(shooter, target); sim.EnemyAiEnabled = false;
        check(sim.Detected(CombatSide.Enemy, target.Id) && !sim.DetailsVisible(CombatSide.Enemy, target.Id), "the target is identified but not seen in detail");
        var score = sim.ViewOf(shooter).Score(shooter, target, null);
        target.ApplyDamage(MechLocation.LeftTorso, 400); target.Stress = StressLevel.Panicked;
        foreach (var bin in target.Ammo.ToArray()) while (target.AmmoRemaining(target.Spec.Ammo.Single(item => item.Id == bin.Id).AmmoKind) > 0 && target.ConsumeAmmo(target.Spec.Ammo.Single(item => item.Id == bin.Id).AmmoKind)) { }
        check(sim.ViewOf(shooter).Score(shooter, target, null) == score, "damage, stress and ammunition of an identified target do not change its score");
        shooter.Position = new Vector2(0, 900); sim.RefreshDetection();
        var detailed = sim.ViewOf(shooter).Score(shooter, target, null);
        var fresh = Friend("awesome", Vector2.Zero); var compare = Battle(Enemy("griffin", new(0, 900)), fresh); compare.EnemyAiEnabled = false;
        check(sim.DetailsVisible(CombatSide.Enemy, target.Id) && detailed > compare.ViewOf(compare.Units[0]).Score(compare.Units[0], fresh, null),
            "a target seen in detail scores higher for its damage");
    }

    private static void CheckFocus(Action<bool, string> check)
    {
        // Each enemy is nearest a different company mech; the lance still fires on one.
        var left = Enemy("griffin", new(-100, 0), number: 1); var right = Enemy("griffin", new(100, 0), number: 2);
        var near = Friend("awesome", new(-200, -600), 1); var far = Friend("awesome", new(200, -600), 2);
        var sim = Battle(left, right, near, far);
        sim.Advance(.05);
        check(left.AttackTarget is not null && left.AttackTarget == right.AttackTarget && sim.TacticalGroups[0].Focus == left.AttackTarget,
            $"a lance concentrates its fire on its focus target ({left.AttackTarget}, {right.AttackTarget})");
        check(sim.Events.Any(item => !item.Public && item.Text.Contains("leads its group onto")) && !sim.Events.Any(item => item.Public && item.Text.Contains("leads its group")),
            "the focus is noted in the debug log only");
    }

    private static void CheckSwitching(Action<bool, string> check)
    {
        CombatSimulation Duel(float current, float other, double since, out CombatUnit shooter)
        {
            shooter = Enemy("griffin", Vector2.Zero);
            var sim = Battle(shooter, Friend("awesome", new(0, -current), 1), Friend("awesome", new(0, other), 2));
            shooter.TacticalTarget = "mech-1"; shooter.TargetSince = since;
            sim.Advance(.05);
            return sim;
        }
        // Scores: 160 in reach + 8 for 80 tons − 4 per 100 m. 1000 m scores 128 and 300 m scores 156: under a quarter better.
        Duel(1000, 300, -100, out var steady);
        check(steady.TacticalTarget == "mech-1" && steady.AttackTarget == "mech-1", "a target less than a quarter better does not take over");
        Duel(1000, 100, -100, out var swapped);
        check(swapped.TacticalTarget == "mech-2", "a target more than a quarter better takes over");
        Duel(1000, 100, 0, out var held);
        check(held.TacticalTarget == "mech-1", "a target is kept for at least 8 s");
        Duel(2000, 600, 0, out var lost);
        check(lost.TacticalTarget == "mech-2", "a target leaving reach is dropped at once for one in reach");
    }

    private static void CheckMovement(Action<bool, string> check)
    {
        var strafer = Enemy("griffin", Vector2.Zero); var mark = Friend("awesome", new(0, -strafer.PreferredDistance));
        var sim = Battle(strafer, mark);
        sim.Advance(6);
        var lateral = Math.Abs(strafer.Position.X);
        check(strafer.AiMove == AiMovement.Point && lateral > 40 && Math.Abs(strafer.Position.DistanceTo(mark.Position) - strafer.PreferredDistance) <= CombatRules.RangeBand
            && strafer.Movement == MovementMode.Walk, $"in range and not under fire, a mech walks sideways around its target ({lateral:0} m)");

        mark.Firing = FiringMode.FireAtWill; sim.Attack([mark.Id], strafer.Id);
        sim.Advance(3);
        check(strafer.Movement == MovementMode.Run, "under fire it runs");
        strafer.HeatLimit = CombatRules.DefaultHeatLimit; strafer.AddHeat(CombatRules.DefaultHeatLimit + 5 - strafer.Heat); sim.Advance(.5);
        check(strafer.Movement == MovementMode.Walk, "too hot to run, it walks even under fire");

        var corporal = Enemy("griffin", Vector2.Zero, EnemyPilotRank.Corporal); var target = Friend("awesome", new(0, -corporal.PreferredDistance));
        var line = Battle(corporal, target); line.Advance(3);
        check(corporal.AiMove == AiMovement.Keep && Math.Abs(corporal.Position.X) < 5, "a Corporal holds its range rather than strafing");

        // A Catapult told to stand off 1,200 m from a Locust, whose longest weapon reaches 810 m, stands still to fire.
        var sniper = Enemy("catapult", Vector2.Zero); sniper.PreferredDistance = 1200; var locust = Friend("locust", new(0, -1200));
        var range = Battle(sniper, locust); range.Advance(3);
        check(sniper.AiMove == AiMovement.Still && sniper.Position.Length() < 5 && sniper.ShotsFired > 0,
            "beyond every visible enemy's reach, a mech stands still to fire");

        var closer = Enemy("griffin", Vector2.Zero); var distant = Friend("awesome", new(0, -1500));
        var closing = Battle(closer, distant); closing.Advance(.5);
        check(closer.AiMove == AiMovement.Keep && closer.AiRangeGoal is not null && closer.Movement == MovementMode.Run, "closing on a distant target, it runs");
    }

    private static void CheckCohesion(Action<bool, string> check)
    {
        // The Awesome anchors the lance; the Griffin has strayed 800 m from it. Their target is in sight but beyond their weapons.
        var anchor = Enemy("awesome", Vector2.Zero, number: 1); var stray = Enemy("griffin", new(800, 0), number: 2);
        var sim = Battle(anchor, stray, Friend("awesome", new(0, -2100)));
        sim.Advance(.05);
        check(stray.Order == CombatOrder.Move && stray.Destination.DistanceTo(anchor.Position) <= CombatRules.CohesionRadius / 2 && stray.Movement == MovementMode.Run
            && anchor.Order == CombatOrder.Attack, "a member separated from its group runs back to it rather than starting a fight of its own");
        sim.Advance(40);
        check(stray.Order == CombatOrder.Attack && stray.Position.DistanceTo(anchor.Position) <= CombatRules.CohesionRadius,
            "back with its group, it engages again");

        // Out of contact, a lance of an Awesome and a Jenner advances at the Awesome's walking speed.
        var slow = Enemy("awesome", new(0, 1500), number: 1); var fast = Enemy("jenner", new(200, 1500), number: 2);
        var advance = Battle(slow, fast, Friend("locust", new(0, -1500)));
        advance.Advance(1);
        check(!advance.Detected(CombatSide.Enemy, "mech-1") && fast.Order == CombatOrder.Move && Math.Abs(fast.DesiredSpeed - slow.CurrentWalkingSpeed) < .01f
            && fast.Destination.Y == 0 && Math.Abs(fast.Destination.X - slow.Destination.X - CombatRules.AdvanceSpacing) < 1,
            "out of contact, a lance advances side by side in its anchor's lane, at its slowest member's walking speed");

        // Lances A (Awesome, Griffin, Locust) and B (Catapult, Jenner): with the Jenner gone, the Catapult joins A.
        var units = new[] { Enemy("awesome", new(0, 0), number: 1), Enemy("catapult", new(300, 0), number: 2), Enemy("griffin", new(-150, 0), number: 3),
            Enemy("jenner", new(450, 0), number: 4), Enemy("locust", new(-300, 0), number: 5), Friend("awesome", new(0, -900)) };
        var join = Battle(units);
        var lanceA = units[0].TacticalGroup; var lanceB = units[1].TacticalGroup;
        units[3].ApplyDamage(MechLocation.CenterTorso, 100_000);
        join.Advance(.05);
        check(lanceA != lanceB && units[1].TacticalGroup == lanceA && join.Events.Any(item => !item.Public && item.Text.Contains("joins another group")),
            "the last survivor of a lance joins the nearest other lance");
        var alone = new[] { Enemy("awesome", new(-2000, 0), number: 1), Enemy("catapult", new(2000, 0), number: 2), Enemy("griffin", new(-2000, 100), number: 3),
            Enemy("jenner", new(2000, 100), number: 4), Enemy("locust", new(-2000, 200), number: 5), Friend("awesome", new(0, -900)) };
        var apart = Battle(alone);
        alone[3].ApplyDamage(MechLocation.CenterTorso, 100_000); apart.Advance(.05);
        check(alone[1].TacticalGroup != alone[0].TacticalGroup, "a survivor with no other lance within reach fights on alone");
    }

    private static void CheckPullBack(Action<bool, string> check)
    {
        CombatSimulation Pair(EnemyPilotRank rank, out CombatUnit hurt, out CombatUnit mate, out CombatUnit foe)
        {
            hurt = Enemy("griffin", new(-60, 0), rank, 1); mate = Enemy("griffin", new(60, 0), rank, 2); foe = Friend("awesome", new(0, -600));
            return Battle(hurt, mate, foe);
        }
        var sim = Pair(EnemyPilotRank.Lieutenant, out var hurt, out var mate, out var foe);
        sim.Advance(.05);
        hurt.Stress = StressLevel.Stressed; sim.Advance(.5);
        check(hurt.PullingBack && hurt.Order == CombatOrder.Move && hurt.Destination.DistanceTo(foe.Position) >= mate.Position.DistanceTo(foe.Position) + CombatRules.PullBackDistance - 30
            && hurt.Movement == MovementMode.Run && hurt.PriorityTarget == foe.Id, "a Stressed pilot runs back behind its group's front and keeps firing");
        check(sim.Events.Any(item => !item.Public && item.Text.Contains("pulls back (stressed)")), "the pull-back is noted in the debug log only");
        hurt.Stress = StressLevel.Excited; sim.Advance(.5);
        check(hurt.PullingBack, "it stays back until 20 s pass without a hit");
        sim.Advance(CombatRules.PullBackQuietSeconds);
        check(!hurt.PullingBack && hurt.Order == CombatOrder.Attack && sim.Events.Any(item => item.Text.Contains("returns to the line")),
            "Excited or calmer after 20 s back without a hit, it returns to the line");

        var corporals = Pair(EnemyPilotRank.Corporal, out var corporal, out _, out _); corporals.Advance(.05);
        corporal.Stress = StressLevel.Stressed; corporals.Advance(.5);
        var stressedHolds = !corporal.PullingBack;
        corporal.Stress = StressLevel.Panicked; corporals.Advance(.5);
        check(stressedHolds && corporal.PullingBack, "a Corporal pulls back only once Panicked");

        // A Lieutenant pulls back after losing a quarter of what it had in 15 s.
        var damage = Pair(EnemyPilotRank.Lieutenant, out var battered, out _, out _); damage.Advance(.05);
        var light = (int)(battered.Toughness * .2f);
        battered.RecentDamage.Add((damage.Elapsed, light)); damage.Advance(.5);
        var holds = !battered.PullingBack;
        battered.RecentDamage.Add((damage.Elapsed, light)); damage.Advance(.5);
        check(holds && battered.PullingBack, "heavy damage in a short time pulls a mech back");

        var breach = Pair(EnemyPilotRank.Lieutenant, out var stripped, out _, out _); breach.Advance(.05);
        stripped.Locations[MechLocation.CenterTorso].RearArmor = 0; breach.Advance(.5);
        var rearIgnored = !stripped.PullingBack;
        stripped.Locations[MechLocation.CenterTorso].Armor = 0; breach.Advance(.5);
        check(rearIgnored && stripped.PullingBack && breach.Events.Any(item => item.Text.Contains("armor breached")),
            "a torso stripped on the side facing its attacker pulls the mech back; its far side does not");

        var solo = Battle(Enemy("griffin", Vector2.Zero), Friend("awesome", new(0, -600)));
        solo.Units[0].Stress = StressLevel.Panicked; solo.Advance(.5);
        check(!solo.Units[0].PullingBack, "the last of a group never pulls back");
    }

    private static void Destroy(CombatUnit unit, MechLocation location) =>
        unit.ApplyDamage(location, unit.Locations[location].Armor + unit.Locations[location].Structure);

    private static void CheckRout(Action<bool, string> check)
    {
        // A Griffin missing an arm faces four Awesomes: some six times its strength.
        CombatSimulation Odds(int foes, out CombatUnit griffin, EnemyPilotRank rank = EnemyPilotRank.Lieutenant)
        {
            griffin = Enemy("griffin", new(0, 600), rank);
            var sim = Battle([griffin, .. Enumerable.Range(1, foes).Select(i => Friend("awesome", new(i * 100 - 250, -400), i))]);
            sim.NerveRollOverride = () => 2;
            return sim;
        }
        var whole = Odds(4, out var fresh); fresh.Stress = StressLevel.Panicked; whole.NerveCheck(fresh);
        check(!fresh.Fleeing, "an undamaged mech never routs, however outmatched");
        var losing = Odds(4, out var beaten); Destroy(beaten, MechLocation.LeftArm);
        losing.NerveCheck(beaten);
        check(beaten.Fleeing && beaten.Routed && !beaten.Fighting && !beaten.SurrenderOffered && beaten.Weapons.Any(beaten.WeaponIntact),
            "badly damaged and clearly outmatched, an armed mech routs and stops fighting");
        check(losing.Events.Any(item => !item.Public && item.Text.Contains("routs")) && losing.Events.Any(item => item.Public && item.Text.EndsWith("flees", StringComparison.Ordinal)),
            "the rout is noted in the debug log, and the combat log says it flees");
        var even = Odds(1, out var matched); Destroy(matched, MechLocation.LeftArm);
        even.NerveCheck(matched);
        var stays = !matched.Fleeing;
        // A roll that would push a Panicked pilot further, who holds on: the check leaves them Panicked.
        matched.Stress = StressLevel.Panicked; even.NerveRollOverride = () => 12; even.PanicEjectOverride = () => false; even.NerveCheck(matched);
        check(stays && matched.Fleeing, "a damaged Lieutenant facing 1.6 times its strength fights on, but routs once Panicked");

        // Its side has nobody left fighting: the battle winds down, and the router gets away.
        losing.Advance(CombatRules.WindDownSeconds + .1);
        var report = losing.MissionReport();
        check(losing.Outcome == CombatOutcome.Victory && beaten.Status == CombatStatus.Fled && Salvage.Pool(report).All(option => option.UnitId != beaten.Id)
            && report.EnemyWarriorsSpared == 1, "a router still running as the battle ends escapes as Fled: not salvaged, pilot spared");

        // An unarmed mech whose surrender was refused, the last enemy, stays on the field to be salvaged.
        var unarmed = Battle(Enemy("griffin", new(0, 600)), Friend("awesome", new(0, -400)));
        var disarmed = unarmed.Units[0];
        foreach (var location in disarmed.Weapons.Select(item => item.Spec.Location).Distinct().ToArray())
            disarmed.ApplyDamage(location, disarmed.Locations[location].Armor + disarmed.Locations[location].Structure);
        unarmed.Advance(CombatRules.WindDownSeconds + .2);
        check(unarmed.Outcome == CombatOutcome.Victory && !disarmed.Routed && disarmed.Status != CombatStatus.Fled
            && Salvage.Pool(unarmed.MissionReport()).Any(option => option.UnitId == disarmed.Id), "an unarmed flight still leaves the last enemy to be salvaged");
    }

    private static void CheckRanks(Action<bool, string> check)
    {
        var ranks = Enum.GetValues<EnemyPilotRank>().Select(rank => Pilot(rank)).ToArray();
        check(ranks.Select(CombatRules.TacticalRank).SequenceEqual([WarriorExperience.Corporal, WarriorExperience.Sergeant, WarriorExperience.Lieutenant, WarriorExperience.Captain]),
            "enemy pilot ranks map onto the four tactical ranks");
        check(ranks.Select(CombatRules.FocusFactor).SequenceEqual([.25f, .5f, 1, 1.25f]) && ranks.Select(CombatRules.PullBackShare).SequenceEqual([.35f, .3f, .25f, .2f])
            && ranks.Select(CombatRules.RoutRatio).SequenceEqual([2f, 2.5f, 3, 3.5f]), "the rank table: focus, pull-back share and rout ratio");
        check(CombatRules.TacticalRank(TestPilots.Warriors[0] with { Gunnery = 6, Piloting = 6, Sensors = 6, Grit = 6 }) == WarriorExperience.Corporal,
            "a pilot below Corporal fights as one");
    }
}
