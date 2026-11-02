using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static void CheckJumping(Action<bool, string> check)
    {
        void Fly(CombatSimulation sim, CombatUnit unit) { for (var i = 0; i < 400 && unit.Airborne; i++) sim.Advance(.05); }
        CombatSimulation Quiet(CombatSimulation sim)
        {
            sim.EnemyAiEnabled = false; sim.Paused = false;
            foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
            return sim;
        }

        // Range, clamping, heat and recharge.
        var sim = Battle(1, "jenner"); Arrange(sim, 1500); Quiet(sim);
        var jenner = Friendly(sim);
        check(jenner.EffectiveJumpMp == 5 && Math.Abs(jenner.JumpRange - 225) < .01f && jenner.JumpUnavailableReason is null,
            $"a Jenner's five jump MP give a 225 m jump: {jenner.JumpRange}");
        sim.Jump([jenner.Id], new Vector2(0, -1000));
        check(jenner.Airborne && jenner.JumpTo.DistanceTo(new Vector2(0, -225)) < .01f && Math.Abs(jenner.Heat - 5) < .01f
            && jenner.Facing.IsEqualApprox(Vector2.Up),
            $"an out-of-range jump lands at maximum range, faces its landing and adds one heat per MP used: {jenner.JumpTo}, {jenner.Heat}");
        sim.Advance(CombatRules.JumpFlightSeconds(225) / 2);
        check(jenner.Altitude > 40 && jenner.Moving && jenner.Order == CombatOrder.Hold, $"the arc rises above the ground mid-flight: {jenner.Altitude}");
        Fly(sim, jenner);
        check(!jenner.Airborne && jenner.Position.DistanceTo(new Vector2(0, -225)) < .01f && jenner.Altitude == 0
            && jenner.Destination == jenner.Position && jenner.JumpRecharge > 9.9,
            "landing holds at the landing point and starts the recharge");
        sim.Jump([jenner.Id], Vector2.Zero);
        check(!jenner.Airborne && jenner.JumpUnavailableReason == "Recharging", "recharging jets refuse another jump");
        sim.Advance(CombatRules.JumpRechargeSeconds);
        check(jenner.JumpUnavailableReason is null, "jets are ready again after the recharge");
        jenner.AddHeat(16 - jenner.Heat);
        sim.Jump([jenner.Id], Vector2.Zero);
        check(!jenner.Airborne && Math.Abs(jenner.Heat - 16) < .1, "a jump whose heat would reach the Normal policy limit is refused");
        jenner.HeatPolicy = HeatPolicy.Aggressive; sim.Jump([jenner.Id], Vector2.Zero);
        check(jenner.Airborne, "Aggressive heat policy permits the same jump");

        var awesome = Battle(); Arrange(awesome); Quiet(awesome);
        Friendly(awesome).Position = Vector2.Zero; awesome.Jump([Friendly(awesome).Id], new Vector2(0, -100));
        check(!Friendly(awesome).Airborne && Friendly(awesome).JumpUnavailableReason == "No jump jets", "mechs without jump jets ignore jump orders");

        // Lost jets and legs shorten the jump.
        var spec = DraftCatalog.Mech("jenner");
        var jet = spec.Components.First(component => component.Kind == ComponentKind.JumpJet);
        var damaged = new CombatUnit("jets", spec, DraftCatalog.Warriors[0], CombatSide.Friendly,
            spec.Locations.Select(l => new LocationCondition(l.Location, l.Armor, l.RearArmor, l.Structure)),
            spec.Weapons.Select(w => w.Id).Concat(spec.Components.Select(c => c.Id)).Concat(spec.Ammo.Select(a => a.Id))
                .Select(id => new EquipmentCondition(id, id == jet.Id ? ComponentCondition.Destroyed : ComponentCondition.Operational)),
            spec.Ammo.Select(a => new AmmoCondition(a.Id, a.Shots)), Vector2.Zero);
        check(damaged.EffectiveJumpMp == 4 && Math.Abs(damaged.JumpRange - 180) < .01f, "each destroyed jump jet removes one jump MP");
        damaged.ApplyDamage(MechLocation.LeftLeg, damaged.Locations[MechLocation.LeftLeg].Armor + damaged.Locations[MechLocation.LeftLeg].Structure);
        check(Math.Abs(damaged.JumpRange - 90) < .01f, "a lost leg halves jump range");

        // Landing on free ground, over rocks and other mechs.
        sim = Battle(1, "jenner"); Arrange(sim, 150); Quiet(sim); jenner = Friendly(sim);
        sim.Jump([jenner.Id], Enemy(sim).Position);
        check(jenner.Airborne && jenner.JumpTo.DistanceTo(Enemy(sim).Position) >= CombatRules.UnitRadius * 2.1f - .01f
            && jenner.JumpTo.DistanceTo(Enemy(sim).Position) < 60 && jenner.JumpTo.Length() <= 225.01f,
            $"a jump onto another mech lands on nearby free ground: {jenner.JumpTo}");
        var rock = new BattlefieldTerrain(1, [new(TerrainKind.Rock, new Vector2(110, 0), 40, 60)], []);
        var roster = Roster(1, "jenner");
        sim = Quiet(CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), rock));
        jenner = Friendly(sim); jenner.Position = Vector2.Zero; Enemy(sim).Position = new Vector2(0, -2000);
        sim.Jump([jenner.Id], new Vector2(225, 0));
        var cleared = true;
        for (var i = 0; i < 400 && jenner.Airborne; i++)
        {
            sim.Advance(.05);
            if (Math.Abs(jenner.Position.X - 110) < 40) cleared &= rock.HeightAt(jenner.Position) + jenner.Altitude > 60;
        }
        check(jenner.Position.DistanceTo(new Vector2(225, 0)) < .01f && cleared, "a jump arcs over a rock that blocks walking");

        // Orders resume on landing; ballistic flight survives destruction. A wingman keeps the battle going.
        sim = Battle(2, "jenner"); Arrange(sim, 600); Quiet(sim); jenner = Friendly(sim);
        sim.Attack([jenner.Id], Enemy(sim).Id); sim.Jump([jenner.Id], new Vector2(0, 200));
        Fly(sim, jenner);
        check(jenner.Order == CombatOrder.Attack && jenner.AttackTarget == Enemy(sim).Id, "an Attack order resumes after the jump");
        sim.Move([jenner.Id], new Vector2(-600, 0)); sim.Advance(CombatRules.JumpRechargeSeconds);
        sim.Jump([jenner.Id], new Vector2(0, 0));
        check(jenner.Order == CombatOrder.Hold, "a jump completes a Move order");
        sim.Advance(1); jenner.ApplyDamage(MechLocation.Head, 10000); Fly(sim, jenner);
        check(jenner.Status == CombatStatus.Destroyed && !jenner.Airborne && jenner.Position == jenner.JumpTo,
            "a mech destroyed in flight still lands");

        // Jumping makes both firing and being fired on harder.
        static double Chance(int threshold)
        {
            var successes = 0;
            for (var a = 1; a <= 6; a++) for (var b = 1; b <= 6; b++) if (a + b >= threshold) successes++;
            return successes / 36.0;
        }
        foreach (var attackerJumps in new[] { true, false })
        {
            sim = Battle(1, "jenner"); Arrange(sim, 200); Quiet(sim);
            var shooter = Friendly(sim); var target = Enemy(sim);
            var laser = shooter.Weapons.First(weapon => weapon.Spec.Kind == WeaponKind.MediumLaser);
            var grounded = CombatSimulation.HitChance(shooter, target, laser);
            var threshold = Enumerable.Range(3, 10).First(item => Math.Abs(Chance(item) - grounded) < 1e-9);
            var jumper = attackerJumps ? shooter : target;
            sim.Jump([jumper.Id], jumper.Position + new Vector2(45, 0));
            sim.Advance(1);
            check(jumper.Airborne && Math.Abs(CombatSimulation.HitChance(shooter, target, laser) - Chance(threshold + CombatRules.JumpToHitModifier)) < 1e-9,
                $"a jumping {(attackerJumps ? "attacker" : "target")} adds +{CombatRules.JumpToHitModifier} hit difficulty");
        }

        // Height in flight sees over terrain.
        var screen = new BattlefieldTerrain(2, [new(TerrainKind.Rock, new Vector2(300, 0), 30, 35)], []);
        roster = Roster(1, "jenner");
        sim = Quiet(CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), screen));
        jenner = Friendly(sim); jenner.Position = Vector2.Zero; Enemy(sim).Position = new Vector2(600, 0);
        check(!sim.Sight(jenner, Enemy(sim)), "a rock taller than eye height blocks sight from the ground");
        sim.Jump([jenner.Id], new Vector2(-225, 0)); sim.Advance(CombatRules.JumpFlightSeconds(225) / 2);
        check(sim.Sight(jenner, Enemy(sim)) && sim.Detected(CombatSide.Friendly, Enemy(sim).Id), "a jumping mech sees and detects over it");

        // Enemy AI jumps when a rock hides its target.
        var wall = new BattlefieldTerrain(3, [new(TerrainKind.Rock, new Vector2(300, 0), 60, 120)], []);
        roster = Roster(2, "jenner");
        sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), wall); sim.Paused = false;
        foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        friends[0].Position = Vector2.Zero; friends[1].Position = new Vector2(-2500, 2500);
        enemies[0].Position = new Vector2(600, 0); enemies[0].Facing = Vector2.Left; enemies[1].Position = new Vector2(300, 400);
        sim.RefreshDetection();
        var jumped = false;
        for (var i = 0; i < 200 && !jumped; i++) { sim.Advance(.05); jumped = enemies[0].Airborne; }
        check(jumped, "enemy AI jumps when its attack target is hidden behind terrain");
    }
}
