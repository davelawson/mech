using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static CombatUnit SensorUnit(string id, string definition, int sensors, CombatSide side, Vector2 position)
    {
        var spec = DraftCatalog.Mech(definition);
        return new(id, spec, DraftCatalog.Warriors[0] with { Sensors = sensors }, side,
            spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)),
            spec.Weapons.Select(w => w.Id).Concat(spec.Components.Select(c => c.Id)).Concat(spec.Ammo.Select(a => a.Id))
                .Select(item => new EquipmentCondition(item, ComponentCondition.Operational)),
            spec.Ammo.Select(a => new AmmoCondition(a.Id, a.Shots)), position);
    }
    private static void CheckSensors(Action<bool, string> check)
    {
        foreach (var (sensors, tonnage, expected) in new[]
        {
            (6, 20, 12.5f), (6, 60, 18.75f), (6, 100, 25f),
            (4, 20, 18.75f), (4, 60, 28.125f), (4, 100, 37.5f),
            (2, 20, 25f), (2, 60, 37.5f), (2, 100, 50f)
        })
            check(CombatRules.DetectionRadius(sensors, tonnage) == expected, $"Sensors {sensors}, {tonnage}t gives {expected} hexes");
        foreach (var sensors in Enumerable.Range(2, 5))
        foreach (var definition in new[] { "locust", "dragon", "atlas" })
        {
            var observer = SensorUnit("observer", "awesome", sensors, CombatSide.Friendly, Vector2.Zero);
            var target = SensorUnit("target", definition, 6, CombatSide.Enemy, Vector2.Zero);
            var sim = new CombatSimulation([observer, target], 41);
            var radius = CombatRules.DetectionRadius(observer, target);
            target.Position = Vector2.Right * radius; sim.RefreshDetection();
            check(sim.Detected(observer.Side, target.Id) && !sim.DetailsVisible(observer.Side, target.Id),
                $"{sensors}/{definition}: full-radius boundary identifies model only");
            target.Position = Vector2.Right * (radius + .01f); sim.RefreshDetection();
            check(!sim.Detected(observer.Side, target.Id), $"{sensors}/{definition}: beyond full radius is hidden");
            target.Position = Vector2.Right * (radius * .5f); sim.RefreshDetection();
            check(sim.DetailsVisible(observer.Side, target.Id), $"{sensors}/{definition}: half-radius boundary reveals details");
            target.Position += Vector2.Right * .01f; sim.RefreshDetection();
            check(sim.Detected(observer.Side, target.Id) && !sim.DetailsVisible(observer.Side, target.Id),
                $"{sensors}/{definition}: leaving half radius immediately hides live details");
        }
        var novice = SensorUnit("novice", "atlas", 6, CombatSide.Friendly, Vector2.Zero);
        var scout = SensorUnit("scout", "locust", 2, CombatSide.Friendly, new Vector2(15, 0));
        var enemy = SensorUnit("enemy", "locust", 6, CombatSide.Enemy, new Vector2(20, 0));
        var shared = new CombatSimulation([novice, scout, enemy], 41);
        check(shared.Detected(CombatSide.Enemy, scout.Id) && shared.Detected(CombatSide.Enemy, novice.Id)
            && shared.DetailsVisible(CombatSide.Friendly, enemy.Id), "both sides calculate target mass and share their best current observation");
        scout.Position = new Vector2(0, 10); shared.RefreshDetection();
        check(shared.Detected(novice.Side, enemy.Id) && !shared.DetailsVisible(novice.Side, enemy.Id),
            "shared detailed contact downgrades when the scout leaves detailed range");
        var lastKnown = shared.Contacts(novice.Side)[enemy.Id].LastKnownPosition;
        scout.ApplyDamage(MechLocation.CenterTorso, 1000); shared.RefreshDetection();
        check(!shared.Detected(novice.Side, enemy.Id) && !shared.DetailsVisible(novice.Side, enemy.Id)
            && shared.Contacts(novice.Side)[enemy.Id].LastKnownPosition == lastKnown, "inactive observers lose both information tiers while retaining last-known position");
        enemy.Position = new Vector2(5, 0); enemy.ApplyDamage(MechLocation.CenterTorso, 1000); shared.RefreshDetection();
        check(shared.DetailsVisible(novice.Side, enemy.Id), "destroyed targets remain observable within detailed range");
        var damaged = SensorUnit("damaged", "awesome", 4, CombatSide.Enemy, Vector2.Zero);
        var weapon = damaged.Weapons.First(w => w.Spec.Location is MechLocation.LeftArm or MechLocation.RightArm);
        damaged.ApplyDamage(weapon.Spec.Location, damaged.Locations[weapon.Spec.Location].Armor + damaged.Locations[weapon.Spec.Location].Structure);
        check(damaged.Equipment[weapon.Spec.Id] == ComponentCondition.Operational && !damaged.WeaponUsable(weapon)
            && CombatScreen.SystemsReadout(damaged).Contains($"{weapon.Spec.Name}: Lost"), "systems inspection derives lost equipment from location integrity");
    }
}
