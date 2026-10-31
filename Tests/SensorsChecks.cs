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
        CheckCircleUnions(check);
        foreach (var (sensors, tonnage, expected) in new[]
        {
            (6, 20, 25f), (6, 60, 37.5f), (6, 100, 50f),
            (4, 20, 37.5f), (4, 60, 56.25f), (4, 100, 75f),
            (2, 20, 50f), (2, 60, 75f), (2, 100, 100f)
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
        var scout = SensorUnit("scout", "locust", 2, CombatSide.Friendly, new Vector2(30, 0));
        var enemy = SensorUnit("enemy", "locust", 6, CombatSide.Enemy, new Vector2(40, 0));
        var shared = new CombatSimulation([novice, scout, enemy], 41);
        check(shared.Detected(CombatSide.Enemy, scout.Id) && shared.Detected(CombatSide.Enemy, novice.Id)
            && shared.DetailsVisible(CombatSide.Friendly, enemy.Id), "both sides calculate target mass and share their best current observation");
        scout.Position = new Vector2(0, 20); shared.RefreshDetection();
        check(shared.Detected(novice.Side, enemy.Id) && !shared.DetailsVisible(novice.Side, enemy.Id),
            "shared detailed contact downgrades when the scout leaves detailed range");
        var lastKnown = shared.Contacts(novice.Side)[enemy.Id].LastKnownPosition;
        scout.ApplyDamage(MechLocation.CenterTorso, 10000); shared.RefreshDetection();
        check(!shared.Detected(novice.Side, enemy.Id) && !shared.DetailsVisible(novice.Side, enemy.Id)
            && shared.Contacts(novice.Side)[enemy.Id].LastKnownPosition == lastKnown, "inactive observers lose both information tiers while retaining last-known position");
        enemy.Position = new Vector2(5, 0); enemy.ApplyDamage(MechLocation.CenterTorso, 10000); shared.RefreshDetection();
        check(shared.DetailsVisible(novice.Side, enemy.Id), "destroyed targets remain observable within detailed range");
        var damaged = SensorUnit("damaged", "awesome", 4, CombatSide.Enemy, Vector2.Zero);
        var weapon = damaged.Weapons.First(w => w.Spec.Location is MechLocation.LeftArm or MechLocation.RightArm);
        damaged.ApplyDamage(weapon.Spec.Location, damaged.Locations[weapon.Spec.Location].Armor + damaged.Locations[weapon.Spec.Location].Structure);
        check(damaged.Equipment[weapon.Spec.Id] == ComponentCondition.Operational && !damaged.WeaponUsable(weapon)
            && CombatScreen.SystemsReadout(damaged).Contains($"{weapon.Spec.Name}: Lost"), "systems inspection derives lost equipment from location integrity");
    }

    private static void CheckCircleUnions(Action<bool, string> check)
    {
        void Boundary(string name, CircleUnion.Circle[] circles, float? expectedLength = null)
        {
            var arcs = CircleUnion.Boundary(circles).ToArray();
            if (expectedLength is { } length)
                check(Math.Abs(arcs.Sum(arc => arc.Radius * (arc.End - arc.Start)) - length) < .0001f,
                    $"{name}: union has the expected exposed perimeter without duplicate strokes");
            var correct = true;
            foreach (var circle in circles)
            for (var sample = 0; sample < 360; sample++)
            {
                var angle = (sample + .37f) * Mathf.Tau / 360;
                var point = circle.Center + Vector2.FromAngle(angle) * circle.Radius;
                var exposed = !circles.Any(other => other != circle && point.DistanceTo(other.Center) < other.Radius - .00001f);
                var drawn = arcs.Any(arc => arc.Center == circle.Center && arc.Radius == circle.Radius && angle >= arc.Start && angle <= arc.End);
                correct &= exposed == drawn;
            }
            check(correct, $"{name}: boundary includes every uncovered arc and excludes covered arcs");
        }
        Boundary("single circle", [new(Vector2.Zero, 1)], Mathf.Tau);
        Boundary("disjoint circles", [new(Vector2.Zero, 1), new(new(3, 0), 1)], 2 * Mathf.Tau);
        Boundary("overlapping circles", [new(Vector2.Zero, 1), new(Vector2.Right, 1)], 8 * Mathf.Pi / 3);
        Boundary("externally tangent circles", [new(Vector2.Zero, 1), new(new(2, 0), 1)], 2 * Mathf.Tau);
        Boundary("contained circles", [new(Vector2.Zero, 2), new(new(.5f, 0), 1)], 2 * Mathf.Tau);
        Boundary("internally tangent circles", [new(Vector2.Zero, 2), new(Vector2.Right, 1)], 2 * Mathf.Tau);
        Boundary("concentric circles", [new(Vector2.Zero, 1), new(Vector2.Zero, 2)], 2 * Mathf.Tau);
        Boundary("coincident circles", [new(Vector2.Zero, 1), new(Vector2.Zero, 1)], Mathf.Tau);
        Boundary("unequal overlapping circles", [new(Vector2.Zero, 2), new(new(2, 0), 1)]);
        CircleUnion.Circle[] ring = Enumerable.Range(0, 3).Select(index => new CircleUnion.Circle(Vector2.FromAngle(index * Mathf.Tau / 3) * 2, 1.8f)).ToArray();
        Boundary("three-circle hole", ring);
        check(CircleUnion.Boundary(ring).Any(arc => (arc.Center + Vector2.FromAngle((arc.Start + arc.End) * .5f) * arc.Radius).Length() < .5f),
            "union preserves the inner boundary of an uncovered hole");
        CircleUnion.Circle[] surrounded = [new(Vector2.Zero, 1), new(Vector2.Right, 1.1f), new(Vector2.Up, 1.1f), new(Vector2.Left, 1.1f), new(Vector2.Down, 1.1f)];
        Boundary("collectively covered circle", surrounded);
        check(CircleUnion.Boundary(surrounded).All(arc => arc.Center != Vector2.Zero), "several circles can cover an entire internal boundary");
        check(!CircleUnion.Boundary([]).Any(), "empty selection has no range boundary");
    }
}
