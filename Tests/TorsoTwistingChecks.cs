using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static void CheckTorsoTwisting(Action<bool, string> check)
    {
        foreach (var (chassis, degrees) in new[] { ("jenner", 90), ("wolverine", 75), ("catapult", 60), ("awesome", 45) })
        {
            var sim = MovementBattle(chassis); var unit = Friendly(sim); var target = Enemy(sim);
            target.Position = new(8, 0); target.Firing = FiringMode.HoldFire;
            sim.RefreshDetection(); sim.SetPriority([unit.Id], target.Id);
            sim.Move([unit.Id], new(0, -30)); sim.Advance(.5);
            check(Math.Abs(unit.TorsoTwist - Mathf.DegToRad(degrees * .5f)) < .0001f,
                $"{unit.Spec.WeightClass} torso uses {degrees} degrees/sec relative to legs");
            check(unit.Position.Y < 0 && Math.Abs(unit.Position.X) < .0001f && unit.Facing.Dot(Vector2.Up) > .9999f,
                $"{chassis} tracks sideways without diverting its moving legs");
            sim.Advance(2);
            check(Math.Abs(unit.TorsoTwist - CombatRules.MaxTorsoTwist) < .0001f && unit.CurrentTarget == target.Id,
                $"{chassis} clamps at 90 degrees and retains its out-of-arc target");
            var twist = unit.TorsoTwist; var facing = unit.TorsoFacing;
            sim.Paused = true; sim.Advance(10);
            check(unit.TorsoTwist == twist && unit.TorsoFacing == facing, "pause freezes simulated torso facing exactly");
            target.Position = new(45, 45); sim.RefreshDetection(); sim.Paused = false; sim.Advance(.5);
            check(unit.CurrentTarget is null && Math.Abs(unit.TorsoTwist - (twist - Mathf.DegToRad(degrees * .5f))) < .0001f,
                "lost detection stops live tracking and recenters at the class rate");
        }

        var aimSim = Battle(); Arrange(aimSim, 15); var f = Friendly(aimSim); var e = Enemy(aimSim);
        f.Facing = Vector2.Up; f.Firing = FiringMode.HoldFire;
        foreach (var weapon in f.Weapons) weapon.Enabled = weapon.Spec.Name == "Small Laser";
        aimSim.SetPriority([f.Id], e.Id);
        check(aimSim.SelectAimTarget(f) == e && aimSim.SelectTarget(f) is null,
            "detected priority aims outside usable range and during Hold Fire");
        aimSim.Paused = false; aimSim.Advance(.5);
        check(f.TorsoTwist > 0 && f.ShotsFired == 0 && f.Position == Vector2.Zero,
            "Hold Fire priority tracks with torso and stationary legs without pursuing or shooting");
        aimSim.Attack([f.Id], e.Id);
        check(aimSim.SelectAimTarget(f) == e, "explicit out-of-range Attack remains a tracking target");

        var firing = MovementBattle(); f = Friendly(firing); e = Enemy(firing);
        e.Position = new(2, 0); e.Firing = FiringMode.HoldFire;
        var laser = f.Weapons.Single(weapon => weapon.Spec.Name == "Small Laser"); laser.Enabled = true;
        firing.RefreshDetection(); firing.SetPriority([f.Id], e.Id); firing.Move([f.Id], new(0, -30));
        firing.Advance(.05);
        check(f.ShotsFired == 0 && laser.Remaining == 0 && firing.WeaponStatus(f, laser, e) == "Turning to target",
            "misaligned weapons wait without spending cooldown and explain turning");
        firing.Advance(1.95);
        check(f.ShotsFired > 0 && f.Facing.Dot(Vector2.Up) > .9999f, "a moving mech fires sideways after its torso aligns");
        e.Position = f.Position + Vector2.Down * 2; firing.RefreshDetection();
        check(!firing.CanFire(f, e, laser) && firing.WeaponStatus(f, laser, e) == "Outside firing arc",
            "a directly rearward main target cannot be reached with forward guns while legs retain course");

        f.Facing = Vector2.Up; f.TorsoTwist = Mathf.Pi / 2;
        foreach (var angle in new[] { -30f, 30f, -30.1f, 30.1f })
        {
            e.Position = f.Position + f.TorsoFacing.Rotated(Mathf.DegToRad(angle)) * 2;
            check(CombatRules.InFiringArc(f, e, laser) == (Math.Abs(angle) <= 30), "forward firing arc includes both 30 degree boundaries only");
        }
        var legSim = Battle(definition: "crusader"); var legMech = Friendly(legSim); var legTarget = Enemy(legSim);
        legMech.Facing = Vector2.Up; legMech.TorsoTwist = Mathf.Pi / 2;
        var legWeapon = legMech.Weapons.First(weapon => CombatRules.IsLeg(weapon.Spec.Location));
        var torsoWeapon = legMech.Weapons.First(weapon => !CombatRules.IsLeg(weapon.Spec.Location) && !weapon.Spec.Rear);
        legTarget.Position = legMech.Position + Vector2.Up * 2;
        check(CombatRules.InFiringArc(legMech, legTarget, legWeapon) && !CombatRules.InFiringArc(legMech, legTarget, torsoWeapon),
            "leg-mounted launchers keep leg facing while torso weapons twist");

        CheckRearWeapons(check);
        CheckRearDamage(check);
        var a = MovementBattle(); var b = MovementBattle();
        foreach (var sim in new[] { a, b })
        {
            Enemy(sim).Position = new(8, 0); sim.RefreshDetection();
            sim.SetPriority([Friendly(sim).Id], Enemy(sim).Id); sim.Move([Friendly(sim).Id], new(0, -30));
        }
        a.Advance(2); for (var i = 0; i < 40; i++) b.Advance(.05);
        check(Friendly(a).TorsoTwist == Friendly(b).TorsoTwist && Friendly(a).TorsoFacing == Friendly(b).TorsoFacing,
            "torso integration is independent of render-frame partitioning");
    }

    private static void CheckRearWeapons(Action<bool, string> check)
    {
        var sim = Battle(2, "atlas"); sim.EnemyAiEnabled = false;
        var f = Friendly(sim); var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        foreach (var unit in sim.Units)
        {
            unit.Position = new(30, 30); unit.Firing = FiringMode.HoldFire;
            if (unit != f) foreach (var weapon in unit.Weapons) weapon.Enabled = false;
        }
        f.Position = Vector2.Zero; f.Facing = Vector2.Up; f.Firing = FiringMode.FireAtWill;
        enemies[0].Position = new(0, -6); enemies[1].Position = new(0, 8);
        sim.RefreshDetection(); sim.SetPriority([f.Id], enemies[0].Id);
        var rear = f.Weapons.First(weapon => weapon.Spec.Rear);
        sim.Paused = false; sim.Advance(.05);
        check(f.CurrentTarget == enemies[0].Id && f.CurrentRearTarget == enemies[1].Id && rear.Remaining > 0 && f.TorsoTwist == 0,
            "rear mounts actually fire at independent rear targets without steering away from the main target");
        foreach (var angle in new[] { -30f, 30f, -30.1f, 30.1f })
        {
            enemies[1].Position = -f.TorsoFacing.Rotated(Mathf.DegToRad(angle)) * 8;
            check(CombatRules.InFiringArc(f, enemies[1], rear) == (Math.Abs(angle) <= 30), "rear weapon arcs are centered directly behind the torso");
        }
        enemies[0].Position = new(1, 6); enemies[1].Position = new(0, 8); sim.RefreshDetection();
        check(sim.SelectRearTarget(f) == enemies[0], "eligible rear priority beats retained and nearest rear targets");
        sim.Attack([f.Id], enemies[1].Id); f.Firing = FiringMode.HoldFire;
        check(sim.SelectRearTarget(f) == enemies[1], "explicit rear Attack outranks priority and permits fire under Hold Fire");
        sim.Hold([f.Id]);
        check(sim.SelectRearTarget(f) is null, "Hold Fire suppresses opportunistic rear fire even with a priority");
        f.Firing = FiringMode.FireAtWill; f.PriorityTarget = null;
        check(sim.SelectRearTarget(f) == enemies[1], "valid rear target is retained over a nearer target");
        f.CurrentRearTarget = null;
        check(sim.SelectRearTarget(f) == enemies[0], "rear mounts choose nearest eligible target without higher preferences");
        foreach (var weapon in f.Weapons.Where(weapon => weapon.Spec.Rear)) weapon.Enabled = false;
        check(sim.SelectRearTarget(f) is null, "disabled rear mounts do not acquire targets");
    }

    private static void CheckRearDamage(Action<bool, string> check)
    {
        foreach (var angle in new[] { -60f, 60f, -60.1f, 60.1f })
        {
            var unit = Friendly(Battle()); unit.Position = Vector2.Zero; unit.Facing = Vector2.Up; unit.TorsoTwist = Mathf.Pi / 2;
            var part = unit.Locations[MechLocation.CenterTorso];
            var from = -unit.TorsoFacing.Rotated(Mathf.DegToRad(angle)) * 8;
            unit.ApplyDamage(MechLocation.CenterTorso, 2, from);
            var rear = Math.Abs(angle) <= 60;
            check(part.RearArmor == part.InitialRearArmor - (rear ? 2 : 0) && part.Armor == part.InitialArmor - (rear ? 0 : 2),
                "rear armor uses the torso's rear 120 degrees including both boundaries");
            check(unit.ArmorLost == 2, "damage totals count armor loss from either face");
        }
        var target = Friendly(Battle()); target.Position = Vector2.Zero; target.Facing = Vector2.Up; target.TorsoTwist = Mathf.Pi / 2;
        var fromRear = Vector2.Left * 8;
        var arm = target.Locations[MechLocation.LeftArm]; var side = target.Locations[MechLocation.LeftTorso];
        target.ApplyDamage(MechLocation.LeftArm, arm.Armor + arm.Structure + 3, fromRear);
        check(arm.Structure == 0 && side.RearArmor == side.InitialRearArmor - 3 && side.Armor == side.InitialArmor,
            "rear arm hit consumes ordinary arm armor and transfers into torso rear armor using the original bearing");
        var center = target.Locations[MechLocation.CenterTorso];
        target.ApplyDamage(MechLocation.LeftTorso, side.RearArmor + side.Structure + 2, fromRear);
        check(side.Structure == 0 && center.RearArmor == center.InitialRearArmor - 2 && center.Armor == center.InitialArmor,
            "rear overflow continues through side torso into center rear armor");
        var head = target.Locations[MechLocation.Head];
        target.ApplyDamage(MechLocation.Head, 1, fromRear);
        check(head.Armor == head.InitialArmor - 1 && head.Structure == head.InitialStructure, "rear head hits use ordinary armor");
        target = Friendly(Battle()); target.Position = Vector2.Zero; target.Facing = Vector2.Up; target.TorsoTwist = Mathf.Pi / 2;
        var leg = target.Locations[MechLocation.LeftLeg]; center = target.Locations[MechLocation.CenterTorso];
        target.ApplyDamage(MechLocation.LeftLeg, leg.Armor + leg.Structure + 2, fromRear);
        check(center.RearArmor == center.InitialRearArmor - 2 && center.Armor == center.InitialArmor,
            "leg overflow chooses torso armor using torso facing rather than leg facing");
        target = Friendly(Battle()); target.Position = Vector2.Zero; target.Facing = Vector2.Up;
        center = target.Locations[MechLocation.CenterTorso];
        target.ApplyDamage(MechLocation.CenterTorso, center.RearArmor + 1, Vector2.Down * 8);
        check(center.Armor == center.InitialArmor && center.Structure == center.InitialStructure - 1,
            "depleted rear armor exposes structure while front armor remains intact");
    }
}
