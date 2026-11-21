using Godot;

namespace Mech.Tests;

/// <summary>Grit, stress and nerve checks; ejection; enemy surrender and fleeing; hostages and ransom.</summary>
internal static partial class CombatChecks
{
    private static void CheckStress(Action<bool, string> check)
    {
        check(CombatRules.StressPenalty(StressLevel.Calm) == 0 && CombatRules.StressPenalty(StressLevel.Excited) == 0
            && CombatRules.StressPenalty(StressLevel.Stressed) == 1 && CombatRules.StressPenalty(StressLevel.Panicked) == 2,
            "only Stressed (1) and Panicked (2) worsen skills");

        // The modifiers, one at a time, on a quiet battlefield.
        var sim = Battle(1, "griffin"); Arrange(sim); var unit = Friendly(sim); var enemy = Enemy(sim);
        check(sim.NerveModifiers(unit) is (-1, "not under fire -1"), "a pilot not under fire and not outnumbered gets only −1");
        unit.AddHeat(25);
        check(sim.NerveModifiers(unit).Modifier == 1, "each full 10 heat adds 1");
        unit.Locations[MechLocation.LeftArm].Structure = 0;
        check(sim.NerveModifiers(unit).Modifier == 2, "each lost location adds 1");
        unit.Injuries = 1;
        check(sim.NerveModifiers(unit).Modifier == 4, "a new injury counts twice: once as an injury, once as new");
        unit.NerveDamage = 450;
        check(sim.NerveModifiers(unit).Modifier == 6, "each full 200 damage since the last check adds 1");
        unit.KnockedDownSinceCheck = unit.HeadDamagedSinceCheck = true;
        check(sim.NerveModifiers(unit).Modifier == 8, "a knockdown and a head hit since the last check add 1 each");
        unit.ShotAtSinceCheck = true;
        check(sim.NerveModifiers(unit) is (10, var text) && text.Contains("under fire +1") && text.Contains("heat +2"),
            "being shot at adds 1 instead of the −1 for quiet, and the log names each modifier");
        sim.NerveRollOverride = () => 2; sim.NerveCheck(unit);
        check(sim.NerveModifiers(unit).Modifier == 3, "after a check, an old injury counts once and the window's events clear");

        // Allies lost and being outnumbered.
        sim = Battle(2, "griffin"); sim.EnemyAiEnabled = false;
        var friends = sim.Units.Where(item => item.Side == CombatSide.Friendly).ToArray();
        friends[0].Position = Vector2.Zero; friends[1].Position = new Vector2(200, 0);
        var foes = sim.Units.Where(item => item.Side == CombatSide.Enemy).ToArray();
        foes[0].Position = new Vector2(0, -300); foes[1].Position = new Vector2(150, -300); sim.RefreshDetection();
        friends[1].ApplyDamage(MechLocation.CenterTorso, 10000);
        check(sim.NerveModifiers(friends[0]).Text.Contains("allies down +1") && sim.NerveModifiers(friends[0]).Text.Contains("ally just lost +1")
            && sim.NerveModifiers(friends[0]).Text.Contains("outnumbered +1"),
            "a lost ally counts, once more if lost since the last check, and two seen enemies outnumber one fighting mech");
        sim.NerveRollOverride = () => 2; sim.NerveCheck(friends[0]);
        check(!sim.NerveModifiers(friends[0]).Text.Contains("ally just lost"), "an ally lost before the last check counts only once");
        sim = Battle(3, "griffin"); sim.EnemyAiEnabled = false;
        friends = sim.Units.Where(item => item.Side == CombatSide.Friendly).ToArray();
        friends[1].ApplyDamage(MechLocation.CenterTorso, 10000); friends[2].ApplyDamage(MechLocation.CenterTorso, 10000);
        check(sim.NerveModifiers(friends[0]).Text.Contains("allies down +1") && sim.NerveModifiers(friends[0]).Text.Contains("ally just lost +2"),
            "losses count +1 however many, and each ally lost since the last check adds 1 more: " + sim.NerveModifiers(friends[0]).Text);

        // Thresholds: 11 or less calms, 12–14 holds, 15 or more stresses (Grit 4, only the −1 for quiet).
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        check(unit.Warrior.Grit == 4 && unit.Stress == StressLevel.Calm, "pilots start Calm");
        unit.Stress = StressLevel.Steady;
        sim.NerveRollOverride = () => 8; var result = sim.NerveCheck(unit);
        check(result is { Result: 11, After: StressLevel.Calm } && unit.Stress == StressLevel.Calm, "a result of 11 calms the pilot a step");
        sim.NerveRollOverride = () => 9; unit.Stress = StressLevel.Steady;
        check(sim.NerveCheck(unit) is { Result: 12, After: StressLevel.Steady }, "a result of 12 to 14 leaves the pilot as they are");
        sim.NerveRollOverride = () => 11;
        check(sim.NerveCheck(unit) is { Result: 14, After: StressLevel.Steady }, "a result of 14 still leaves the pilot as they are");
        unit.NerveDamage = 600; sim.NerveRollOverride = () => 9;
        check(sim.NerveCheck(unit) is { Result: 15, After: StressLevel.Excited, Broke: false }, "a result of 15 stresses the pilot a step");
        unit.Stress = StressLevel.Stressed; var gunnery = unit.Gunnery; var piloting = unit.Piloting;
        check(gunnery == unit.Warrior.Gunnery + 1 && piloting == unit.Warrior.Piloting + 1 && unit.Sensors == unit.Warrior.Sensors + 1,
            "Stressed plays Gunnery, Piloting and Sensors one rating worse");
        var target = Enemy(sim); var weapon = unit.Weapons.First(unit.WeaponUsable);
        var stressedChance = CombatSimulation.HitChance(unit, target, weapon); var stressedRadius = CombatRules.DetectionRadius(unit, target);
        unit.Stress = StressLevel.Calm;
        check(CombatSimulation.HitChance(unit, target, weapon) > stressedChance && CombatRules.DetectionRadius(unit, target) > stressedRadius,
            "stress lowers hit chances and shortens detection");
        unit.Stress = StressLevel.Panicked;
        check(unit.Gunnery == unit.Warrior.Gunnery + 2, "Panicked plays skills two ratings worse");
        unit.NerveDamage = 600; sim.NerveRollOverride = () => 9; sim.PanicEjectOverride = () => false;
        var held = sim.NerveCheck(unit);
        check(held is { Broke: false, HeldOn: true, After: StressLevel.Panicked } && !unit.Ejected
            && sim.Events[^1] is { Public: true } holdLine && holdLine.Text.EndsWith("Panicked · holds on"),
            "a Panicked pilot pushed further may hold on instead of ejecting, and the combat log says so");
        unit.NerveDamage = 600; sim.PanicEjectOverride = () => true;
        var broke = sim.NerveCheck(unit);
        check(broke.Broke && unit.Ejected && unit.Status == CombatStatus.Disabled && !unit.WarriorKilled && unit.Injuries == 1
            && sim.Events.Any(item => item.UnitId == unit.Id && item.Text.Contains("panics and ejects")),
            "a Panicked pilot who reaches 15 or more again and loses the even chance ejects, injured, leaving the mech disabled");
        sim.PanicEjectOverride = null;
        var odds = Battle(1, "griffin"); var panicked = Friendly(odds); odds.NerveRollOverride = () => 12; var breaks = 0;
        for (var i = 0; i < 400; i++) { panicked.Stress = StressLevel.Panicked; if (odds.NerveCheck(panicked).Broke) { breaks++; panicked.Ejected = false; panicked.Injuries = 0; } }
        check(breaks is > 160 and < 240, $"a pushed Panicked pilot breaks about half the time ({breaks} of 400)");
        var cockpit = unit.Spec.Components.First(part => part.Kind == ComponentKind.Cockpit).Id;
        check(unit.ReportedEquipment().Single(item => item.Id == cockpit).Condition == ComponentCondition.Damaged,
            "an ejection leaves the cockpit needing repair");

        // The clock: one check per pilot every 5 s from a random offset, frozen while paused.
        sim = Battle(1, "griffin"); Arrange(sim);
        check(sim.Units.All(item => item.NerveRemaining is >= 0 and < CombatRules.NerveCheckSeconds)
            && sim.Units.Select(item => item.NerveRemaining).Distinct().Count() == sim.Units.Count, "each pilot's nerve clock starts at its own offset");
        var calls = 0; sim.NerveRollOverride = () => { calls++; return 2; };
        sim.Advance(10);
        check(calls == 0, "no nerve checks while paused");
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        sim.Paused = false; sim.Advance(CombatRules.NerveCheckSeconds);
        check(calls == sim.Units.Count, "every pilot makes one nerve check every 5 s");
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim); unit.Stress = StressLevel.Stressed;
        sim.NerveRollOverride = () => 12; unit.NerveDamage = 600; sim.NerveCheck(unit);
        check(sim.Events.Any(item => item.Public && item.Text.Contains("Stressed → Panicked")), "a friendly's change of stress is logged");
        var foe = Enemy(sim); foe.Stress = StressLevel.Steady; sim.NerveRollOverride = () => 2; sim.NerveCheck(foe);
        check(sim.Events.Any(item => item.UnitId == foe.Id && !item.Public), "an enemy's change of stress stays out of the combat log");
        unit.Stress = StressLevel.Steady; sim.NerveRollOverride = () => 9; var events = sim.Events.Count;
        var holds = sim.NerveCheck(unit);
        check(holds.After == StressLevel.Steady && sim.Events.Count == events + 1 && sim.Events[^1] is { Public: false } line
            && line.DebugText.Contains($"rolled 9 + Grit {unit.Warrior.Grit}") && line.DebugText.Contains("not under fire -1") && line.DebugText.EndsWith("= 12 · Steady"),
            "every nerve check reaches the debug log with its roll and modifiers, even when the level holds: " + sim.Events[^1].DebugText);

        // Ejecting by order.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        sim.Execute(new EjectCommand([unit.Id]));
        check(unit.Ejected && unit.Defeated && !unit.WarriorKilled, "the Eject order ejects the MechWarrior");
        sim.Paused = false; sim.Advance(CombatRules.WindDownSeconds + .1);
        var report = sim.MissionReport();
        check(report.Mechs.Single() is { WarriorKilled: false, InjuryDays: >= CombatRules.InjuryDaysMinimum }, "an ejected pilot comes home injured");
        // A pilot ejected before the head is lost survives it.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim); sim.Execute(new EjectCommand([unit.Id]));
        unit.ApplyDamage(MechLocation.Head, 1000);
        check(!unit.WarriorKilled, "an empty cockpit kills nobody");
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim); unit.ApplyDamage(MechLocation.Head, 1000);
        sim.Execute(new EjectCommand([unit.Id]));
        check(!unit.Ejected && unit.WarriorKilled, "a dead MechWarrior cannot eject");

        // An unarmed side is beaten; an unarmed mech is not disabled.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        foreach (var location in unit.Weapons.Select(item => item.Spec.Location).Distinct().ToArray())
            unit.ApplyDamage(location, unit.Locations[location].Armor + unit.Locations[location].Structure);
        sim.Paused = false; sim.Advance(CombatRules.WindDownSeconds + .1);
        check(unit.Unarmed && !unit.Defeated && sim.Outcome == CombatOutcome.Defeat, "a side left with only unarmed mechs loses");

        CheckSurrender(check);
        CheckHostages(check);
    }

    private static void Disarm(CombatUnit unit)
    {
        foreach (var location in unit.Weapons.Select(item => item.Spec.Location).Distinct().ToArray())
            unit.ApplyDamage(location, unit.Locations[location].Armor + unit.Locations[location].Structure);
    }

    private static void CheckSurrender(Action<bool, string> check)
    {
        // Losing every weapon: the offer pauses the battle until the leader answers.
        var sim = Battle(1, "griffin"); Arrange(sim); sim.EnemyAiEnabled = true; sim.PromptSurrender = true; sim.NerveChecksEnabled = false;
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        var enemy = Enemy(sim); var friendly = Friendly(sim);
        Disarm(enemy); sim.Paused = false; sim.Advance(.05);
        check(sim.SurrenderOffers.SequenceEqual([enemy.Id]) && sim.Paused && enemy.SurrenderOffered && !enemy.Defeated,
            "an enemy that loses every weapon offers to surrender, pausing the battle");
        var elapsed = sim.Elapsed; sim.Advance(1);
        check(sim.Elapsed == elapsed, "the battle waits for an answer");
        var replica = Battle(1, "griffin"); replica.BecomeReplica(); replica.Apply(sim.State());
        check(replica.SurrenderOffers.SequenceEqual([enemy.Id]) && replica.Paused && replica.Events.Count == sim.Events.Count
            && replica.Unit(enemy.Id).SurrenderOffered, "replicas see the offer, the pause and the battle's events");
        sim.Execute(new SurrenderAnswerCommand(enemy.Id, true));
        check(enemy.Surrendered && enemy.Status == CombatStatus.Surrendered && enemy.Defeated && enemy.Solid && !sim.Paused && friendly.Kills == 0,
            "an accepted surrender lifts the pause, takes the mech out of the battle and credits no kill");
        sim.Advance(CombatRules.WindDownSeconds + .1);
        var report = sim.MissionReport();
        check(sim.Outcome == CombatOutcome.Victory && report.Enemies.Single().Status == CombatStatus.Surrendered && Salvage.Pool(report).Count == 0
            && report.EnemyWarriorsSpared == 1, "a surrendered mech cannot be salvaged, and its pilot is spared");

        // Only the leader answers.
        // Four mechs fill the first lance; the fifth gives the second player slot a lance.
        var roster = ForceRoster.Create("Two lances", Enumerable.Repeat(DraftCatalog.Mech("griffin"), 5), DraftCatalog.Warriors.Take(5), lances: 2);
        for (var i = 0; i < 5; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        sim = Deploy(roster, roster.Mechs.Select(item => item.Id)); sim.PromptSurrender = true; sim.NerveChecksEnabled = false;
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        enemy = sim.Units.First(item => item.Side == CombatSide.Enemy); Disarm(enemy);
        sim.Paused = false; sim.Advance(.05);
        sim.Execute(new SurrenderAnswerCommand(enemy.Id, true), 1);
        check(sim.SurrenderOffers.Count == 1 && !enemy.Surrendered, "another player cannot answer a surrender");
        sim.Execute(new SurrenderAnswerCommand(enemy.Id, false), Campaign.LeaderSlot);
        check(enemy.Fleeing && !enemy.Surrendered && sim.SurrenderOffers.Count == 0, "refused, an unarmed mech flees");

        // A refused unarmed mech runs out of range, then to the edge, while armed allies fight on.
        sim = Battle(2, "griffin"); sim.NerveChecksEnabled = false;
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        var enemies = sim.Units.Where(item => item.Side == CombatSide.Enemy).ToArray(); friendly = Friendly(sim);
        enemies[0].Position = new Vector2(0, -300); friendly.Position = Vector2.Zero; sim.RefreshDetection();
        Disarm(enemies[0]); sim.Paused = false; sim.Advance(.1);
        check(enemies[0].Fleeing && sim.Events.Any(item => item.Text.Contains("surrender refused")), "without a prompt, an offer is refused at once");
        var reach = sim.Units.Where(item => item.Side == CombatSide.Friendly).Max(item => item.Weapons.Where(item.WeaponIntact).Max(w => w.LongRange));
        var goal = sim.FleeGoal(enemies[0]);
        check(sim.Units.Where(item => item.Side == CombatSide.Friendly).All(item => item.Position.DistanceTo(goal) >= reach),
            "a fleeing mech first heads beyond every known enemy's weapon range");
        for (var i = 0; i < 6000 && !enemies[0].Fled; i++) sim.Advance(.1);
        check(enemies[0].Fled && enemies[0].Status == CombatStatus.Fled && !enemies[0].Solid && sim.Outcome == CombatOutcome.Ongoing,
            "it then runs away to the arena edge and leaves the battle");

        // A crippled center torso: surrender when losing, eject when winning, and offer only once.
        sim = Battle(1, "griffin"); Arrange(sim); sim.EnemyAiEnabled = true; sim.NerveRollOverride = () => 2;
        enemy = Enemy(sim); friendly = Friendly(sim);
        var stock = enemy.Spec.Locations.Single(item => item.Location == MechLocation.CenterTorso).Structure;
        enemy.Locations[MechLocation.CenterTorso].Structure = stock / 2 + 1;
        sim.NerveCheck(enemy);
        check(!enemy.SurrenderOffered && !enemy.Ejected, "more than half its center torso structure keeps an enemy fighting");
        enemy.Locations[MechLocation.CenterTorso].Structure = stock / 2;
        check(sim.ExpectsDefeat(CombatSide.Enemy), "a weakened enemy expects defeat against a stronger force it can see");
        sim.NerveCheck(enemy);
        check(enemy.SurrenderOffered && !enemy.Ejected && !enemy.Fleeing && !enemy.Defeated, "crippled and losing, it offers surrender, and fights on when refused");
        sim.NerveCheck(enemy);
        check(sim.Events.Count(item => item.Text.Contains("offers to surrender")) == 1, "an enemy offers to surrender once per battle");
        foreach (var location in new[] { MechLocation.LeftTorso, MechLocation.RightTorso, MechLocation.LeftLeg })
            friendly.Locations[location].Structure = 0;
        friendly.Locations[MechLocation.CenterTorso].Structure = 1;
        var expectsWin = !sim.ExpectsDefeat(CombatSide.Enemy);
        sim.NerveCheck(enemy);
        check(expectsWin && enemy.Ejected && enemy.Status == CombatStatus.Disabled,
            "crippled but expecting to win, it ejects");
        sim = Battle(1, "griffin"); Arrange(sim); sim.EnemyAiEnabled = true; sim.NerveRollOverride = () => 2; enemy = Enemy(sim);
        enemy.Locations[MechLocation.CenterTorso].Structure = 1;
        foreach (var location in new[] { MechLocation.LeftTorso, MechLocation.RightTorso, MechLocation.LeftLeg })
            Friendly(sim).Locations[location].Structure = 0;
        sim.NerveCheck(enemy);
        check(enemy.Ejected && !enemy.SurrenderOffered, "an enemy expecting to win ejects rather than surrender");
        var salvage = Salvage.Pool(new MissionReport(MissionOutcome.Success, [], 0)
            { Enemies = [new("enemy-1", enemy.Spec.Id, enemy.Status, [.. enemy.Locations.Values.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure))], enemy.ReportedEquipment())] });
        check(salvage.Any(option => option.WholeMech), "an ejected enemy's mech can be claimed whole");
    }

    private static void CheckHostages(Action<bool, string> check)
    {
        foreach (var won in new[] { true, false })
        {
            var campaign = CampaignChecks.NewCompany(); var roster = campaign.Roster!;
            var offer = campaign.Missions.Available.FirstOrDefault(item => item.Kind != MissionKind.Patrol && item.Opposition.Length >= 2);
            if (offer is null) { check(false, "a mission with two or more enemies is available for the hostage checks"); return; }
            campaign.AcceptMission(offer.Id);
            var deployed = roster.Mechs.Take(1).ToArray();
            var sim = CombatSimulation.DeployMission(roster, deployed.Select(mech => mech.Id), offer, 5, 183);
            var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
            enemies[0].Surrendered = true; enemies[1].Fled = true;
            foreach (var other in enemies.Skip(2)) other.ApplyDamage(MechLocation.CenterTorso, 100000);
            if (!won) sim.Units.First(unit => unit.Side == CombatSide.Friendly).ApplyDamage(MechLocation.CenterTorso, 100000);
            sim.NerveChecksEnabled = false; foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
            if (won) { sim.Paused = false; sim.Advance(CombatRules.WindDownSeconds + .1); } else sim.End();
            var report = sim.MissionReport();
            check(Salvage.Pool(report).All(option => option.UnitId is not ("enemy-1" or "enemy-2")), "surrendered and fled mechs are not on the field to salvage");
            var cash = campaign.Cash;
            var result = campaign.SettleMission(report);
            var ransom = won ? SalvageTuning.RansomFor(offer.Opposition[0].Rank) : 0;
            check(result.Ransom == ransom && result.Hostages.Length == (won ? 1 : 0) && campaign.Cash == cash + result.Payment + result.Insurance + ransom,
                won ? "a victory ransoms each hostage by rank" : "a lost battle leaves its hostages behind");
            var conduct = new MissionConduct(report.Outcome, deployed.Select(mech => mech.Definition.Id).ToArray(), report.Mechs.Sum(item => item.ArmorLost),
                report.EnemyWarriorsSpared, 1, 0, 0);
            check(!won || result.Reputation.OrderBy(pair => pair.Key).SequenceEqual(Reputation.MissionChanges(offer, conduct).OrderBy(pair => pair.Key)),
                "a fled enemy mech counts toward reputation");
            check(report.EnemyWarriorsSpared >= 2 && SalvageTuning.RansomFor(EnemyPilotRank.Elite) == 150_000, "hostages and fled pilots count as spared");
        }
    }
}
