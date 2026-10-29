using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static CombatSimulation MovementBattle(string definition = "awesome", int count = 1)
    {
        var sim = Battle(count, definition); sim.EnemyAiEnabled = false; sim.Paused = false;
        for (var i = 0; i < sim.Units.Count; i++)
        {
            var unit = sim.Units[i]; unit.Position = new(20 + i * 3, -30);
            foreach (var weapon in unit.Weapons) weapon.Enabled = false;
        }
        Friendly(sim).Position = Vector2.Zero; Friendly(sim).Facing = Vector2.Up;
        sim.RefreshDetection(); return sim;
    }
    private static void CheckLocomotion(Action<bool, string> check)
    {
        CheckSharedMovement(check);
        CheckWolverineMovement(check);
        var sim = MovementBattle(); var f = Friendly(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -30)); sim.Advance(.05);
        check(f.CurrentSpeed > 0 && f.CurrentSpeed < f.Speed && f.Position.Y < 0,
            "aligned movement accelerates from rest instead of snapping to maximum speed");
        sim.Advance(2.95);
        check(Math.Abs(f.CurrentSpeed - .9f) < .001f && Math.Abs(f.Position.Y + 1.35f) < .002f,
            "Awesome reaches full run in three seconds with integrated acceleration distance");
        var start = f.Position; sim.Hold([f.Id]); sim.Advance(.05);
        check(f.Order == CombatOrder.Hold && f.CurrentSpeed > 0 && f.CurrentSpeed < .9f && f.Position.Y < start.Y,
            "Stop clears the order immediately but brakes existing momentum");
        sim.Advance(1.45);
        check(Math.Abs(f.CurrentSpeed) < .001f && Math.Abs(f.Position.DistanceTo(start) - .675f) < .002f,
            "Awesome brakes from full run in 1.5 seconds without excessive coasting");
        start = f.Position; sim.Advance(2);
        check(f.Position.IsEqualApprox(start) && !f.Moving, "a completed stop remains stationary");

        sim = MovementBattle(); f = Friendly(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -30)); sim.Advance(3);
        f.Movement = MovementMode.Walk; sim.Advance(.05);
        check(f.CurrentSpeed > f.WalkingSpeed && f.CurrentSpeed < f.RunningSpeed,
            "Run to Walk reduces actual speed through braking");
        sim.Advance(1);
        check(Math.Abs(f.CurrentSpeed - f.WalkingSpeed) < .001f, "Walk eventually enforces its selected speed limit");

        sim = MovementBattle(); f = Friendly(sim); var enemy = Enemy(sim);
        enemy.Position = new(0, -4); f.PreferredDistance = 10; f.Movement = MovementMode.Run;
        sim.RefreshDetection(); sim.Attack([f.Id], enemy.Id); sim.Advance(3);
        check(f.CurrentSpeed < 0 && Math.Abs(f.CurrentSpeed + f.WalkingSpeed) < .001f
            && f.Facing.Dot(Vector2.Up) > .999f && f.Position.Y > 0,
            "Attack retreats in reverse at full walking speed while retaining facing, even in Run mode");

        sim = MovementBattle(); f = Friendly(sim); enemy = Enemy(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -30)); sim.Advance(3);
        enemy.Position = f.Position + Vector2.Up * 3; f.PreferredDistance = 10;
        sim.RefreshDetection(); sim.Attack([f.Id], enemy.Id); sim.Advance(.05);
        check(f.CurrentSpeed > 0 && f.CurrentSpeed < f.RunningSpeed, "forward to reverse brakes before reversing");
        sim.Advance(1.45);
        check(Math.Abs(f.CurrentSpeed) < .001f, "direction reversal spends forward momentum first");
        sim.Advance(2);
        check(f.CurrentSpeed < 0, "reverse acceleration follows braking");
        sim.Move([f.Id], f.Position + Vector2.Up * 10); sim.Advance(.05);
        check(f.CurrentSpeed < 0 && f.CurrentSpeed > -f.WalkingSpeed, "reverse to forward also brakes before accelerating");

        sim = MovementBattle(); f = Friendly(sim); sim.Move([f.Id], new(0, 20)); sim.Advance(.05);
        check(!f.Moving && f.CurrentSpeed == 0 && Math.Abs(f.Facing.AngleTo(Vector2.Up)) > .001f
            && Math.Abs(f.Facing.AngleTo(Vector2.Up)) <= f.TurnRate * .05f + .0001f,
            "sharp orders pivot gradually before beginning forward travel");
        sim.Advance(1);
        check(f.Position == Vector2.Zero, "an Awesome cannot immediately move through a 180 degree turn");
        sim.Advance(5);
        check(f.Position.Y > 0 && f.Facing.Dot(Vector2.Down) > .9f, "pivoting eventually permits forward travel to the new goal");

        sim = MovementBattle(); f = Friendly(sim); enemy = Enemy(sim); enemy.Position = new(0, 2);
        f.Weapons.Single(weapon => weapon.Spec.Name == "Small Laser").Enabled = true;
        sim.RefreshDetection(); sim.Advance(.05);
        check(f.ShotsFired == 0 && f.Position == Vector2.Zero && Math.Abs(f.Facing.AngleTo(Vector2.Up)) < .04f,
            "stationary targeting turns gradually and waits for torso firing alignment");
        sim.Advance(3.95);
        check(f.Facing.Dot(Vector2.Down) > .999f && f.ShotsFired > 0, "stationary Awesome completes its leg turn and fires after torso alignment");

        sim = MovementBattle(); f = Friendly(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -30)); sim.Advance(3);
        sim.Move([f.Id], f.Position + Vector2.Up.Rotated(.2f) * 20); var before = f.Facing; var position = f.Position;
        sim.Advance(.05);
        check(Math.Abs(before.AngleTo(f.Facing)) < f.TurnRate * .05f * .5f && f.Moving
            && Math.Abs((f.Position - position).Normalized().Cross(f.Facing)) < .001f,
            "running limits moving turn rate and displacement follows facing without sideways sliding");
        sim.Move([f.Id], f.Position - f.Facing * 20); var speed = f.CurrentSpeed; sim.Advance(.05);
        check(f.CurrentSpeed < speed, "sharp turns while running cause braking");
        sim.Paused = true; position = f.Position; before = f.Facing; speed = f.CurrentSpeed; sim.Advance(10);
        check(f.Position == position && f.Facing == before && f.CurrentSpeed == speed, "pause freezes velocity and turning as well as position");

        var fast = Friendly(MovementBattle("jenner"));
        check(fast.RunningSpeed / fast.Acceleration < 1.3f && Mathf.Pi / fast.TurnRate < 1.72f,
            "Jenner movement stats produce agreed faster acceleration and turn rate");
        var light = DraftCatalog.Mech("jenner"); var heavy = light with { Tonnage = 100 };
        var source = Friendly(MovementBattle("jenner"));
        var sameStats = new CombatUnit("same-stats", heavy, source.Warrior, source.Side,
            source.Locations.Values.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)),
            source.Equipment.Select(item => new EquipmentCondition(item.Key, item.Value)), source.Ammo, Vector2.Zero);
        check(source.Acceleration == sameStats.Acceleration && source.TurnRate == sameStats.TurnRate,
            "weight cannot change handling independently of movement stats");

        sim = MovementBattle(); f = Friendly(sim); sim.Move([f.Id], new(0, -2)); sim.Advance(20);
        check(f.Order == CombatOrder.Hold && f.CurrentSpeed == 0 && f.Position.DistanceTo(new(0, -2)) < .2f,
            "arrival brakes to a stable stop within destination tolerance");

        sim = MovementBattle(count: 2); f = Friendly(sim); var wreck = Enemy(sim);
        wreck.Position = new(0, -4); wreck.ApplyDamage(MechLocation.Head, 1000);
        sim.Move([f.Id], new(0, -8)); var crossed = false; var aligned = true;
        for (var i = 0; i < 1200; i++)
        {
            position = f.Position; sim.Advance(.05);
            crossed |= f.Position.DistanceTo(wreck.Position) < .2f;
            if (f.Moving) aligned &= Math.Abs((f.Position - position).Normalized().Cross(f.Facing)) < .001f;
        }
        check(crossed && aligned && Math.Abs(f.Position.X) < .001f && f.Position.Y < -7.8f,
            "mechs travel straight through a destroyed mech without steering around it or stopping");

        sim = MovementBattle(count: 2); f = Friendly(sim); var disabled = Enemy(sim);
        disabled.Position = new(0, -4);
        foreach (var location in new[] { MechLocation.LeftLeg, MechLocation.RightLeg })
            disabled.ApplyDamage(location, disabled.Locations[location].Armor + disabled.Locations[location].Structure);
        sim.Move([f.Id], new(0, -8)); var separated = true;
        for (var i = 0; i < 1200; i++)
        {
            sim.Advance(.05);
            separated &= f.Position.DistanceTo(disabled.Position) >= CombatRules.UnitRadius * 2 - .0001f;
        }
        check(disabled.Status == CombatStatus.Disabled && separated && f.Position.Y < -5,
            "disabled mechs remain solid obstacles that moving mechs steer around");

        sim = MovementBattle(count: 2); f = Friendly(sim); wreck = Enemy(sim);
        sim.Move([f.Id], new(0, -8)); sim.Advance(3);
        wreck.Position = f.Position + f.Facing * (CombatRules.UnitRadius * 2 + .05f);
        wreck.ApplyDamage(MechLocation.CenterTorso, 1000);
        var wreckPosition = wreck.Position; sim.Advance(20);
        check(f.Position.Y < wreckPosition.Y - 1 && wreck.Position == wreckPosition,
            "a newly destroyed mech immediately becomes passable while its wreck stays in place");

        sim = MovementBattle(); f = Friendly(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -30)); sim.Advance(3);
        var leg = f.Locations[MechLocation.LeftLeg]; f.ApplyDamage(MechLocation.LeftLeg, leg.Armor + leg.Structure);
        sim.Advance(4);
        check(f.Active && Math.Abs(f.CurrentSpeed - .45f) < .001f, "losing one leg reduces actual running speed to half");
        leg = f.Locations[MechLocation.RightLeg]; f.ApplyDamage(MechLocation.RightLeg, leg.Armor + leg.Structure);
        position = f.Position; sim.Advance(.05);
        check(!f.Active && f.CurrentSpeed == 0 && f.AngularVelocity == 0 && f.Position == position,
            "a newly disabled mech has no residual travel or turning");

        var a = MovementBattle(); var b = MovementBattle();
        a.Move([Friendly(a).Id], new(8, 12)); b.Move([Friendly(b).Id], new(8, 12));
        a.Advance(12); for (var i = 0; i < 120; i++) b.Advance(.1);
        check(Friendly(a).Position.IsEqualApprox(Friendly(b).Position) && Friendly(a).Facing.IsEqualApprox(Friendly(b).Facing)
            && Friendly(a).CurrentSpeed == Friendly(b).CurrentSpeed, "acceleration and turning are deterministic across frame partitions");
    }
    private static void CheckWolverineMovement(Action<bool, string> check)
    {
        foreach (var mode in new[] { MovementMode.Walk, MovementMode.Run })
        {
            var sim = MovementBattle("wolverine"); var unit = Friendly(sim);
            check(sim.Units.All(item => Math.Abs(item.WalkingSpeed - .9f) < .0001f
                    && Math.Abs(item.RunningSpeed - 1.44f) < .0001f),
                $"Wolverine {mode} uses 50% faster walk/run limits on both sides");
            unit.Movement = mode; sim.Move([unit.Id], new(0, -30)); sim.Advance(4);
            var before = unit.Position; sim.Advance(1);
            var expected = mode == MovementMode.Walk ? .9f : 1.44f;
            check(Math.Abs(unit.CurrentSpeed - expected) < .001f
                && Math.Abs(unit.Position.DistanceTo(before) - expected) < .001f,
                $"Wolverine {mode} actually travels 50% farther each second at full speed");
        }
        var retreat = MovementBattle("wolverine"); var friendly = Friendly(retreat); var enemy = Enemy(retreat);
        enemy.Position = new(0, -4); friendly.PreferredDistance = 10; friendly.Movement = MovementMode.Run;
        retreat.RefreshDetection(); retreat.Attack([friendly.Id], enemy.Id); retreat.Advance(3);
        check(Math.Abs(friendly.CurrentSpeed + .9f) < .001f,
            "Wolverine reverse travel uses its increased walking limit");
        var leg = friendly.Locations[MechLocation.LeftLeg];
        friendly.ApplyDamage(MechLocation.LeftLeg, leg.Armor + leg.Structure);
        check(Math.Abs(friendly.WalkingSpeed - .45f) < .0001f && Math.Abs(friendly.RunningSpeed - .72f) < .0001f,
            "Wolverine leg damage still halves both increased speed limits");
        check(DraftCatalog.Mech("wolverine") is { Walk: 5, Run: 8 },
            "Wolverine retains its stock catalog movement points");
    }

    private static void CheckSharedMovement(Action<bool, string> check)
    {
        foreach (var spec in DraftCatalog.AllMechs)
        {
            foreach (var mode in new[] { MovementMode.Walk, MovementMode.Run })
            {
                var sim = MovementBattle(spec.Id); var unit = Friendly(sim);
                var expected = (mode == MovementMode.Walk ? spec.Walk : spec.Run) * .12f * 1.5f;
                check(sim.Units.All(item => Math.Abs((mode == MovementMode.Walk ? item.WalkingSpeed : item.RunningSpeed) - expected) < .0001f),
                    $"{spec.Id} {mode} has 50% faster movement limits on both sides");
                unit.Movement = mode; sim.Move([unit.Id], new(0, -30)); sim.Advance(9d / spec.Walk + 1);
                var before = unit.Position; sim.Advance(1);
                check(Math.Abs(unit.CurrentSpeed - expected) < .001f
                    && Math.Abs(unit.Position.DistanceTo(before) - expected) < .001f,
                    $"{spec.Id} {mode} actually covers 50% more ground at full speed");
            }
        }
        // An unfamiliar chassis ID must receive the same default without an allowlist update.
        var source = Friendly(MovementBattle("jenner"));
        var future = new CombatUnit("future", source.Spec with { Id = "future-import" }, source.Warrior, source.Side,
            source.Locations.Values.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)),
            source.Equipment.Select(item => new EquipmentCondition(item.Key, item.Value)), source.Ammo, Vector2.Zero);
        check(Math.Abs(future.WalkingSpeed - 1.26f) < .0001f && Math.Abs(future.RunningSpeed - 1.98f) < .0001f,
            "future imported chassis inherit faster travel without a chassis-specific override");
    }

}
