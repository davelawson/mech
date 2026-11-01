using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static CombatSimulation MovementBattle(string definition = "awesome", int count = 1)
    {
        var sim = Battle(count, definition); sim.EnemyAiEnabled = false; sim.Paused = false;
        for (var i = 0; i < sim.Units.Count; i++)
        {
            var unit = sim.Units[i]; unit.Position = new(600 + i * 90, -900);
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
        sim.Move([f.Id], new(0, -900)); sim.Advance(.05);
        check(f.CurrentSpeed > 0 && f.CurrentSpeed < f.Speed && f.Position.Y < 0,
            "aligned movement accelerates from rest instead of snapping to maximum speed");
        sim.Advance(2.95);
        check(Math.Abs(f.CurrentSpeed - 13.5f) < .03f && Math.Abs(f.Position.Y + 20.25f) < .06f,
            "Awesome reaches full run in three seconds with integrated acceleration distance");
        var start = f.Position; sim.Hold([f.Id]); sim.Advance(.05);
        check(f.Order == CombatOrder.Hold && f.CurrentSpeed > 0 && f.CurrentSpeed < 13.5f && f.Position.Y < start.Y,
            "Stop clears the order immediately but brakes existing momentum");
        sim.Advance(1.45);
        check(Math.Abs(f.CurrentSpeed) < .03f && Math.Abs(f.Position.DistanceTo(start) - 10.125f) < .06f,
            "Awesome brakes from full run in 1.5 seconds without excessive coasting");
        start = f.Position; sim.Advance(2);
        check(f.Position.IsEqualApprox(start) && !f.Moving, "a completed stop remains stationary");

        sim = MovementBattle(); f = Friendly(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -900)); sim.Advance(3);
        f.Movement = MovementMode.Walk; sim.Advance(.05);
        check(f.CurrentSpeed > f.WalkingSpeed && f.CurrentSpeed < f.RunningSpeed,
            "Run to Walk reduces actual speed through braking");
        sim.Advance(1);
        check(Math.Abs(f.CurrentSpeed - f.WalkingSpeed) < .03f, "Walk eventually enforces its selected speed limit");

        sim = MovementBattle(); f = Friendly(sim); var enemy = Enemy(sim);
        enemy.Position = new(0, -120); f.PreferredDistance = 300; f.Movement = MovementMode.Run;
        sim.RefreshDetection(); sim.Attack([f.Id], enemy.Id); sim.Advance(3);
        check(f.CurrentSpeed < 0 && Math.Abs(f.CurrentSpeed + f.WalkingSpeed) < .03f
            && f.Facing.Dot(Vector2.Up) > .999f && f.Position.Y > 0,
            "Attack retreats in reverse at full walking speed while retaining facing, even in Run mode");

        sim = MovementBattle(); f = Friendly(sim); enemy = Enemy(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -900)); sim.Advance(3);
        enemy.Position = f.Position + Vector2.Up * 90; f.PreferredDistance = 300;
        sim.RefreshDetection(); sim.Attack([f.Id], enemy.Id); sim.Advance(.05);
        check(f.CurrentSpeed > 0 && f.CurrentSpeed < f.RunningSpeed, "forward to reverse brakes before reversing");
        sim.Advance(1.45);
        check(Math.Abs(f.CurrentSpeed) < .03f, "direction reversal spends forward momentum first");
        sim.Advance(2);
        check(f.CurrentSpeed < 0, "reverse acceleration follows braking");
        sim.Move([f.Id], f.Position + Vector2.Up * 300); sim.Advance(.05);
        check(f.CurrentSpeed < 0 && f.CurrentSpeed > -f.WalkingSpeed, "reverse to forward also brakes before accelerating");

        sim = MovementBattle(); f = Friendly(sim); sim.Move([f.Id], new(0, 600)); sim.Advance(.05);
        check(!f.Moving && f.CurrentSpeed == 0 && Math.Abs(f.Facing.AngleTo(Vector2.Up)) > .001f
            && Math.Abs(f.Facing.AngleTo(Vector2.Up)) <= f.TurnRate * .05f + .0001f,
            "sharp orders pivot gradually before beginning forward travel");
        sim.Advance(1);
        check(f.Position == Vector2.Zero, "an Awesome cannot immediately move through a 180 degree turn");
        sim.Advance(5);
        check(f.Position.Y > 0 && f.Facing.Dot(Vector2.Down) > .9f, "pivoting eventually permits forward travel to the new goal");

        sim = MovementBattle(); f = Friendly(sim); enemy = Enemy(sim); enemy.Position = new(0, 60);
        f.Weapons.Single(weapon => weapon.Spec.Name == "Small Laser").Enabled = true;
        sim.RefreshDetection(); sim.Advance(.05);
        check(f.ShotsFired == 0 && f.Position == Vector2.Zero && Math.Abs(f.Facing.AngleTo(Vector2.Up)) < .04f,
            "stationary targeting turns gradually and waits for torso firing alignment");
        sim.Advance(3.95);
        check(f.Facing.Dot(Vector2.Down) > .999f && f.ShotsFired > 0, "stationary Awesome completes its leg turn and fires after torso alignment");

        sim = MovementBattle(); f = Friendly(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -900)); sim.Advance(3);
        sim.Move([f.Id], f.Position + Vector2.Up.Rotated(.2f) * 600); var before = f.Facing; var position = f.Position;
        sim.Advance(.05);
        check(Math.Abs(before.AngleTo(f.Facing)) < f.TurnRate * .05f * .5f && f.Moving
            && Math.Abs((f.Position - position).Normalized().Cross(f.Facing)) < .001f,
            "running limits moving turn rate and displacement follows facing without sideways sliding");
        sim.Move([f.Id], f.Position - f.Facing * 600); var speed = f.CurrentSpeed; sim.Advance(.05);
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

        sim = MovementBattle(); f = Friendly(sim); sim.Move([f.Id], new(0, -60)); sim.Advance(20);
        check(f.Order == CombatOrder.Hold && f.CurrentSpeed == 0 && f.Position.DistanceTo(new(0, -60)) < 6,
            "arrival brakes to a stable stop within destination tolerance");

        sim = MovementBattle(count: 2); f = Friendly(sim); var wreck = Enemy(sim);
        wreck.Position = new(0, -120); wreck.ApplyDamage(MechLocation.Head, 10000);
        sim.Move([f.Id], new(0, -240)); var crossed = false; var aligned = true;
        for (var i = 0; i < 1200; i++)
        {
            position = f.Position; sim.Advance(.05);
            crossed |= f.Position.DistanceTo(wreck.Position) < 6;
            if (f.Moving) aligned &= Math.Abs((f.Position - position).Normalized().Cross(f.Facing)) < .001f;
        }
        check(crossed && aligned && Math.Abs(f.Position.X) < .03f && f.Position.Y < -234,
            "mechs travel straight through a destroyed mech without steering around it or stopping");

        sim = MovementBattle(count: 2); f = Friendly(sim); var disabled = Enemy(sim);
        disabled.Position = new(0, -120);
        foreach (var location in new[] { MechLocation.LeftLeg, MechLocation.RightLeg })
            disabled.ApplyDamage(location, disabled.Locations[location].Armor + disabled.Locations[location].Structure);
        sim.Move([f.Id], new(0, -240)); var separated = true;
        for (var i = 0; i < 1200; i++)
        {
            sim.Advance(.05);
            separated &= f.Position.DistanceTo(disabled.Position) >= CombatRules.UnitRadius * 2 - .003f;
        }
        check(disabled.Status == CombatStatus.Disabled && separated && f.Position.Y < -150,
            "disabled mechs remain solid obstacles that moving mechs steer around");

        sim = MovementBattle(count: 2); f = Friendly(sim); wreck = Enemy(sim);
        sim.Move([f.Id], new(0, -240)); sim.Advance(3);
        wreck.Position = f.Position + f.Facing * (CombatRules.UnitRadius * 2 + 1.5f);
        wreck.ApplyDamage(MechLocation.CenterTorso, 10000);
        var wreckPosition = wreck.Position; sim.Advance(20);
        check(f.Position.Y < wreckPosition.Y - 30 && wreck.Position == wreckPosition,
            "a newly destroyed mech immediately becomes passable while its wreck stays in place");

        sim = MovementBattle(); f = Friendly(sim); f.Movement = MovementMode.Run;
        sim.Move([f.Id], new(0, -900)); sim.Advance(3);
        var leg = f.Locations[MechLocation.LeftLeg]; f.ApplyDamage(MechLocation.LeftLeg, leg.Armor + leg.Structure);
        sim.Advance(4);
        check(f.Active && Math.Abs(f.CurrentSpeed - 6.75f) < .03f, "losing one leg reduces actual running speed to half");
        leg = f.Locations[MechLocation.RightLeg]; f.ApplyDamage(MechLocation.RightLeg, leg.Armor + leg.Structure);
        position = f.Position; sim.Advance(.05);
        check(!f.Active && f.CurrentSpeed == 0 && f.AngularVelocity == 0 && f.Position == position,
            "a newly disabled mech has no residual travel or turning");

        var a = MovementBattle(); var b = MovementBattle();
        a.Move([Friendly(a).Id], new(240, 360)); b.Move([Friendly(b).Id], new(240, 360));
        a.Advance(12); for (var i = 0; i < 120; i++) b.Advance(.1);
        check(Friendly(a).Position.IsEqualApprox(Friendly(b).Position) && Friendly(a).Facing.IsEqualApprox(Friendly(b).Facing)
            && Friendly(a).CurrentSpeed == Friendly(b).CurrentSpeed, "acceleration and turning are deterministic across frame partitions");
    }
    private static void CheckWolverineMovement(Action<bool, string> check)
    {
        foreach (var mode in new[] { MovementMode.Walk, MovementMode.Run })
        {
            var sim = MovementBattle("wolverine"); var unit = Friendly(sim);
            check(sim.Units.All(item => Math.Abs(item.WalkingSpeed - 13.5f) < .003f
                    && Math.Abs(item.RunningSpeed - 21.6f) < .003f),
                $"Wolverine {mode} uses 13.5/21.6 m/s walk/run limits on both sides");
            unit.Movement = mode; sim.Move([unit.Id], new(0, -900)); sim.Advance(4);
            var before = unit.Position; sim.Advance(1);
            var expected = mode == MovementMode.Walk ? 13.5f : 21.6f;
            check(Math.Abs(unit.CurrentSpeed - expected) < .03f
                && Math.Abs(unit.Position.DistanceTo(before) - expected) < .03f,
                $"Wolverine {mode} actually travels its limit each second at full speed");
        }
        var retreat = MovementBattle("wolverine"); var friendly = Friendly(retreat); var enemy = Enemy(retreat);
        enemy.Position = new(0, -120); friendly.PreferredDistance = 300; friendly.Movement = MovementMode.Run;
        retreat.RefreshDetection(); retreat.Attack([friendly.Id], enemy.Id); retreat.Advance(3);
        check(Math.Abs(friendly.CurrentSpeed + 13.5f) < .03f,
            "Wolverine reverse travel uses its walking limit");
        var leg = friendly.Locations[MechLocation.LeftLeg];
        friendly.ApplyDamage(MechLocation.LeftLeg, leg.Armor + leg.Structure);
        check(Math.Abs(friendly.WalkingSpeed - 6.75f) < .003f && Math.Abs(friendly.RunningSpeed - 10.8f) < .003f,
            "Wolverine leg damage still halves both speed limits");
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
                var expected = (mode == MovementMode.Walk ? spec.Walk : spec.Run) * CombatRules.MovementScale;
                check(sim.Units.All(item => Math.Abs((mode == MovementMode.Walk ? item.WalkingSpeed : item.RunningSpeed) - expected) < .003f),
                    $"{spec.Id} {mode} has MP × movement scale limits on both sides");
                unit.Movement = mode; sim.Move([unit.Id], new(0, -900)); sim.Advance(9d / spec.Walk + 1);
                var before = unit.Position; sim.Advance(1);
                check(Math.Abs(unit.CurrentSpeed - expected) < .03f
                    && Math.Abs(unit.Position.DistanceTo(before) - expected) < .03f,
                    $"{spec.Id} {mode} actually covers its limit each second at full speed");
            }
        }
        // An unfamiliar chassis ID must receive the same default without an allowlist update.
        var source = Friendly(MovementBattle("jenner"));
        var future = new CombatUnit("future", source.Spec with { Id = "future-import" }, source.Warrior, source.Side,
            source.Locations.Values.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)),
            source.Equipment.Select(item => new EquipmentCondition(item.Key, item.Value)), source.Ammo, Vector2.Zero);
        check(Math.Abs(future.WalkingSpeed - 18.9f) < .003f && Math.Abs(future.RunningSpeed - 29.7f) < .003f,
            "future imported chassis inherit the shared movement scale without a chassis-specific override");
    }

}
