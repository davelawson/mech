using System.Collections.Immutable;
using System.Text.Json;
using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static string Id(CombatUnit unit, ComponentKind kind, MechLocation? location = null) =>
        unit.Spec.Components.First(item => item.Kind == kind && (location is null || item.Location == location)).Id;
    private static string Hit(CombatUnit unit, string id)
    {
        var location = unit.Spec.Weapons.FirstOrDefault(item => item.Id == id)?.Location ?? unit.Spec.Ammo.FirstOrDefault(item => item.Id == id)?.Location
            ?? unit.Spec.Components.Single(item => item.Id == id).Location;
        return unit.CriticalHit(location, id, out _) ?? "";
    }

    private static void CheckCriticalHits(Action<bool, string> check)
    {
        check(new[] { 2, 6, 7, 9, 10, 11, 12 }.Select(CombatRules.CriticalHits).SequenceEqual([0, 0, 1, 1, 2, 2, 3]),
            "2d6 gives one potential critical hit on 7–9, two on 10–11 and three on 12");

        // Items: one slot is destroyed outright, two or more are damaged first; critical systems take levels and are never destroyed.
        var sim = Battle(1, "griffin"); Arrange(sim); var unit = Friendly(sim);
        var ppc = unit.Weapons.Single(item => item.Spec.Kind == WeaponKind.PPC);
        var lrm = unit.Weapons.Single(item => item.Spec.Kind == WeaponKind.LRM10);
        var aimBefore = CombatSimulation.HitChance(unit, Enemy(sim), lrm);
        check(Hit(unit, lrm.Spec.Id) == "Critical hit: RT LRM 10 damaged" && unit.Equipment[lrm.Spec.Id] == ComponentCondition.Damaged
            && unit.ItemHits(lrm.Spec.Id) == 1 && unit.CriticalAimModifier(lrm) == CombatRules.DamagedWeaponModifier && unit.WeaponIntact(lrm),
            "a weapon taking two or more slots is damaged by its first critical hit, still fires, and aims 2 worse");
        check(CombatSimulation.HitChance(unit, Enemy(sim), lrm) < aimBefore, "a damaged weapon's hit chance falls");
        check(Hit(unit, lrm.Spec.Id) == "Critical hit: RT LRM 10 destroyed" && unit.Equipment[lrm.Spec.Id] == ComponentCondition.Destroyed
            && !unit.WeaponIntact(lrm) && Hit(unit, lrm.Spec.Id) == "", "a second hit destroys it, and a destroyed item takes no more");
        var bin = unit.Spec.Ammo.First();
        check(Hit(unit, bin.Id).EndsWith("destroyed") && unit.Shots(bin.Id) == 0, "a single-slot item, such as an ammunition bin, is destroyed outright");
        var engine = Id(unit, ComponentKind.FusionEngine);
        var texts = Enumerable.Range(0, 4).Select(_ => Hit(unit, engine)).ToArray();
        check(texts.SequenceEqual(["Critical hit: CT Fusion Engine damaged 1/3", "Critical hit: CT Fusion Engine damaged 2/3",
            "Critical hit: CT Fusion Engine critically damaged", ""]) && unit.Equipment[engine] == ComponentCondition.Damaged && unit.ItemHits(engine) == 3,
            "an engine takes one level per hit up to three, is critically damaged at three and never destroyed");
        check(unit.ConditionText(engine) == "Critically damaged" && CriticalText.Condition(unit.Spec, engine, ComponentCondition.Damaged, 2, levels: true) == "Damaged 2/3"
            && CriticalText.Condition(unit.Spec, ppc.Spec.Id, ComponentCondition.Damaged, 1) == "Damaged",
            "critical systems read Damaged below their maximum and Critically damaged at it");
        check(Math.Abs(unit.EngineHeatRate - 3 * CombatRules.EngineHeatPerLevel / CombatRules.HeatClockSeconds) < 1e-9,
            "each engine level adds 5 heat per heat clock while the mech runs");

        // Mobility, sensors and arm actuators.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        var walk = unit.UnheatedWalkingSpeed; var turn = unit.TurnRate; var jump = unit.JumpRange;
        Hit(unit, Id(unit, ComponentKind.Gyro));
        check(Math.Abs(unit.UnheatedWalkingSpeed - walk * .75f) < 1e-4 && Math.Abs(unit.TurnRate - turn * .75f) < 1e-4
            && Math.Abs(unit.Acceleration - unit.UnheatedRunningSpeed * unit.Spec.Walk / 9f) < 1e-4,
            "each gyro level costs a quarter of top speed, acceleration and turn rate");
        Hit(unit, Id(unit, ComponentKind.Hip, MechLocation.LeftLeg));
        check(Math.Abs(unit.UnheatedWalkingSpeed - walk * .75f * .5f) < 1e-4, "a damaged hip halves ground speed");
        unit.Locations[MechLocation.LeftLeg].Structure = 0;
        check(Math.Abs(unit.UnheatedWalkingSpeed - walk * .75f * .5f) < 1e-4, "a damaged hip does not add to a lost leg");
        check(Math.Abs(unit.JumpRange - jump * .5f) < 1e-3, "gyro and hip damage leave jumping alone; only the lost leg halves it");
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim); walk = unit.UnheatedWalkingSpeed;
        Hit(unit, Id(unit, ComponentKind.UpperLegActuator)); Hit(unit, Id(unit, ComponentKind.FootActuator));
        check(Math.Abs(unit.UnheatedWalkingSpeed - walk * .8f) < 1e-4, "each leg or foot actuator costs a tenth of ground speed");
        var radius = CombatRules.DetectionRadius(unit, Enemy(sim));
        var sensors = Id(unit, ComponentKind.Sensors);
        Hit(unit, sensors);
        ppc = unit.Weapons.Single(item => item.Spec.Kind == WeaponKind.PPC);
        check(Math.Abs(CombatRules.DetectionRadius(unit, Enemy(sim)) - radius * .75f) < 1e-3 && unit.CriticalAimModifier(ppc) == 1,
            "each sensors level costs a quarter of sensor range and adds 1 to every weapon's target number");
        Hit(unit, Id(unit, ComponentKind.LowerArmActuator, ppc.Spec.Location));
        check(unit.CriticalAimModifier(ppc) == 2 && unit.CriticalAimModifier(unit.Weapons.Single(item => item.Spec.Kind == WeaponKind.LRM10)) == 1,
            "an arm actuator adds 1 to the target number of that arm's weapons only");
        Hit(unit, Id(unit, ComponentKind.Shoulder, ppc.Spec.Location));
        check(unit.CriticalAimModifier(ppc) == 4, "a shoulder adds 2 more");

        // Catalog jets and single sinks are one slot each and go at the first hit; a three-slot double sink is damaged first.
        var jet = unit.Spec.Components.First(item => item.Kind == ComponentKind.JumpJet);
        var mp = unit.EffectiveJumpMp;
        check(Hit(unit, jet.Id).EndsWith("destroyed") && unit.EffectiveJumpMp == mp - 1, "a struck jump jet is destroyed and costs one jump");
        var stock = DraftCatalog.Mech("firestarter");
        var single = stock.Components.First(item => item.Kind == ComponentKind.HeatSink);
        var doubled = stock with { HeatSinkType = HeatSinkKind.Double, Components = stock.Components.Select(item => item.Id == single.Id
            ? item with { Location = MechLocation.LeftTorso, Slots = EquipmentRules.HeatSinkSlots(HeatSinkKind.Double) } : item).ToImmutableArray() };
        var cooler = new CombatUnit("cooler", doubled, TestPilots.Warriors[0], CombatSide.Friendly,
            doubled.Locations.Select(item => new LocationCondition(item.Location, item.Armor, item.RearArmor, item.Structure)),
            doubled.Weapons.Select(item => item.Id).Concat(doubled.Components.Select(item => item.Id)).Concat(doubled.Ammo.Select(item => item.Id))
                .Select(id => new EquipmentCondition(id, ComponentCondition.Operational)),
            doubled.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)), Vector2.Zero);
        var cooling = cooler.EffectiveCooling;
        check(Hit(cooler, single.Id).EndsWith("damaged") && cooler.EffectiveCooling == cooling - 1 && cooler.EffectiveHeatSinks == doubled.HeatSinks,
            "a damaged double heat sink cools like a single one");
        Hit(cooler, single.Id);
        check(cooler.EffectiveCooling == cooling - 2, "a destroyed double heat sink cools not at all");

        // Cockpit and Life Support.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        check(Hit(unit, Id(unit, ComponentKind.Cockpit)) == "Critical hit: HD Cockpit critically damaged" && unit.Status == CombatStatus.Disabled
            && unit.WarriorKilled && unit.Intact(MechLocation.Head), "a cockpit hit disables the mech and kills its MechWarrior");
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        Hit(unit, Id(unit, ComponentKind.LifeSupport));
        unit.AddHeat(14);
        check(unit.Injuries == 0, "a damaged Life Support does not injure below 15 heat");
        unit.AddHeat(2); unit.AddHeat(-5); unit.AddHeat(5);
        check(unit.Injuries == 1 && unit.LifeSupportInjury, "reaching 15 heat with Life Support damaged injures the MechWarrior once per battle");

        // Slots: each is struck once, and an exhausted location takes no more.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        var slots = unit.UnstruckSlots(MechLocation.LeftArm).Count;
        check(slots == EquipmentRules.Capacity(MechLocation.LeftArm) && unit.UnstruckSlots(MechLocation.Head).Count == 6,
            "every critical slot of a location can be struck, empty ones included");
        foreach (var (slot, _) in unit.UnstruckSlots(MechLocation.LeftArm)) unit.StrikeSlot(MechLocation.LeftArm, slot);
        unit.Locations[MechLocation.LeftArm].Armor = 0;
        var logs = Enumerable.Range(0, 20).SelectMany(_ => sim.DamageWithCriticals(unit, MechLocation.LeftArm, 1)).ToArray();
        check(logs.Length == 0 && unit.Intact(MechLocation.LeftArm), "once every slot is struck, rolls find nothing more to hit");

        // Rolls happen only when structure is damaged, at about the 2d6 rate.
        var rolled = 0; var any = 0;
        for (var seed = 0; seed < 400; seed++)
        {
            var trial = Battle(1, "griffin", seed); var target = Friendly(trial);
            if (trial.DamageWithCriticals(target, MechLocation.CenterTorso, 10).Count > 0) any++;
            target.Locations[MechLocation.CenterTorso].Armor = 0;
            if (trial.DamageWithCriticals(target, MechLocation.CenterTorso, 1).Count > 0) rolled++;
        }
        check(any == 0, "armor-only damage never rolls for critical hits");
        check(rolled is > 190 and < 275, $"structure damage finds a critical hit, at the 21-in-36 rate of a full location ({rolled} of 400)");

        // Ammunition explosions: half the time, straight into structure, injuring the MechWarrior.
        var exploded = 0; var held = 0;
        for (var seed = 0; seed < 300 && (exploded == 0 || held == 0); seed++)
        {
            var trial = Battle(1, "griffin", seed); var target = Friendly(trial);
            var store = target.Spec.Ammo.First();
            foreach (var (slot, block) in target.UnstruckSlots(store.Location)) if (block.ItemId != store.Id) target.StrikeSlot(store.Location, slot);
            target.Locations[store.Location].Armor = 0;
            var log = trial.DamageWithCriticals(target, store.Location, 1);
            if (log.Count == 0) continue;
            if (log.Any(item => item.Explosion > 0))
            {
                exploded++;
                var blast = log.First(item => item.Explosion > 0);
                check(blast.Explosion == CombatRules.AmmoExplosionDamage(store.AmmoKind, store.Shots) && blast.Explosion == 600
                    && blast.Text == $"Ammo explosion · RT {store.Name} · 600 damage" && target.Injuries >= 1 && !target.Intact(store.Location),
                    "an explosion deals half the stock damage of its remaining shots, wrecks its location and injures the MechWarrior");
            }
            else held++;
        }
        check(exploded > 0 && held > 0, "a struck bin sometimes explodes and sometimes does not");
        sim = Battle(1, "griffin"); unit = Friendly(sim);
        var before = unit.Locations[MechLocation.RightTorso].Armor;
        unit.ApplyInternalDamage(MechLocation.RightTorso, 50);
        check(unit.Locations[MechLocation.RightTorso].Armor == before && unit.Locations[MechLocation.RightTorso].Structure == 80,
            "explosion damage skips the location's armor");
        unit.ApplyInternalDamage(MechLocation.RightTorso, 100);
        check(!unit.Intact(MechLocation.RightTorso) && !unit.Intact(MechLocation.RightArm)
            && unit.Locations[MechLocation.CenterTorso].Armor == unit.Locations[MechLocation.CenterTorso].InitialArmor - 20,
            "what is left carries inward along the usual path, armor first");

        // The mission report carries hits home, counts lost locations as destroyed, and rolls injury days.
        var battle = Battle(1, "griffin"); var friendly = Friendly(battle);
        Hit(friendly, friendly.Weapons.Single(item => item.Spec.Kind == WeaponKind.LRM10).Spec.Id);
        Hit(friendly, Id(friendly, ComponentKind.FusionEngine)); Hit(friendly, Id(friendly, ComponentKind.FusionEngine));
        friendly.Locations[MechLocation.LeftArm].Structure = 0;
        friendly.Injuries = 2;
        battle.End();
        var report = battle.MissionReport().Mechs.Single();
        var lrmId = friendly.Weapons.Single(item => item.Spec.Kind == WeaponKind.LRM10).Spec.Id;
        check(report.Equipment.Single(item => item.Id == lrmId) == new EquipmentCondition(lrmId, ComponentCondition.Damaged, 1)
            && report.Equipment.Single(item => item.Id == Id(friendly, ComponentKind.FusionEngine)).Hits == 2,
            "damaged items report their hits and levels");
        check(friendly.Spec.Components.Where(item => item.Location == MechLocation.LeftArm)
            .All(item => report.Equipment.Single(entry => entry.Id == item.Id).Condition == ComponentCondition.Destroyed),
            "items in a lost location report destroyed");
        check(report.InjuryDays is >= 20 and <= 40 && battle.MissionReport().Mechs.Single().InjuryDays == report.InjuryDays,
            "each injury adds 10–20 recovery days, the same however often the report is asked for");

        // A replica takes on the leader's equipment conditions.
        sim = Battle(1, "griffin"); unit = Friendly(sim);
        Hit(unit, Id(unit, ComponentKind.Gyro)); unit.Injuries = 1;
        var replica = Battle(1, "griffin"); replica.BecomeReplica();
        var json = JsonSerializer.Serialize(sim.State());
        replica.Apply(JsonSerializer.Deserialize<CombatState>(json)!);
        var copy = Friendly(replica);
        check(copy.Equipment[Id(copy, ComponentKind.Gyro)] == ComponentCondition.Damaged && copy.ItemHits(Id(copy, ComponentKind.Gyro)) == 1
            && copy.Injuries == 1, "co-op battle states carry equipment damage and injuries");

        // A fought battle produces critical hits through its shots.
        var fight = Battle(1, "griffin", 7); Arrange(fight, 300); fight.EnemyAiEnabled = true; fight.Paused = false;
        for (var step = 0; step < 6000 && fight.Outcome == CombatOutcome.Ongoing; step++) fight.Advance(CombatRules.StepSeconds);
        check(fight.History.Any(item => item.Shot.Criticals.Count > 0), "shots that reach structure cause critical hits in a fought battle");
        check(fight.History.SelectMany(item => item.Shot.Criticals).All(critical => critical.Destroyed
                == (critical.Explosion == 0 && (critical.Text.EndsWith(" destroyed") || critical.Text.EndsWith(" critically damaged")))),
            "critical hits mark destroyed items and critically damaged systems for the battlefield effects");

        // The log names each critical hit.
        var entry = new CombatLogEntry(1, new CombatShot("a", "b", Vector2.Zero, Vector2.Right, "PPC", true, MechLocation.RightTorso, 100)
        { Criticals = [new(MechLocation.RightTorso, "x", "Critical hit: RT Medium Laser destroyed")] },
            new(CombatSide.Friendly, 1, "A", "A", ContactLevel.Detailed, 0), new(CombatSide.Enemy, 1, "B", "B", ContactLevel.Detailed, 0));
        check(entry.DebugText.Contains("Critical hit: RT Medium Laser destroyed"), "the combat log lists critical hits on the shot");
    }
}
