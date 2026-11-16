using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static void CheckKnockdowns(Action<bool, string> check)
    {
        check(CombatRules.KnockdownThreshold(50) == 100 && CombatRules.KnockdownStep(50) == 50
            && new[] { 101, 149, 150, 199, 200 }.Select(damage => CombatRules.KnockdownPenalty(50, damage)).SequenceEqual([0, 0, 1, 1, 2]),
            "a 50 t mech checks above 10 stock points, with +1 for each further 5");
        check(new[] { WeaponKind.AC20, WeaponKind.PPC, WeaponKind.LRM10, WeaponKind.SRM4 }.All(CombatRules.CountsTowardKnockdown)
            && !new[] { WeaponKind.MediumLaser, WeaponKind.MachineGun, WeaponKind.Flamer }.Any(CombatRules.CountsTowardKnockdown),
            "autocannons, PPCs and missiles count toward a knockdown; lasers, machine guns and flamers do not");

        // The window: more than 20% of tonnage within one second makes a check, which clears it.
        var sim = Battle(1, "griffin"); Arrange(sim); var unit = Friendly(sim);
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        sim.PilotingRollOverride = () => 12;
        check(sim.RecordKnockdownDamage(unit, 110) is null, "damage of exactly 20% of tonnage makes no check");
        var passed = sim.RecordKnockdownDamage(unit, 1);
        check(passed is { Damage: 111, Target: 4, Roll: 12, Fell: false } && !unit.Prone && passed.Text.Contains("keeps footing"),
            "the hit that takes the second's damage past 20% makes a Piloting check against the MechWarrior's Piloting");
        check(sim.RecordKnockdownDamage(unit, 100) is null, "a check clears the window");
        sim.Paused = false; sim.Advance(1.05);
        check(sim.RecordKnockdownDamage(unit, 20) is null, "damage older than a second drops out of the window");
        check(sim.RecordKnockdownDamage(unit, 200) is { Target: 6, DamagePenalty: 2 }, "each further 10% of tonnage adds 1 to the target number");

        // Leg and gyro damage make the check harder.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim);
        Hit(unit, Id(unit, ComponentKind.Gyro));
        check(unit.PilotingModifiers.Modifier == 3, "each gyro level adds 3");
        Hit(unit, Id(unit, ComponentKind.Hip, MechLocation.LeftLeg));
        check(unit.PilotingModifiers.Modifier == 5, "a damaged hip adds 2");
        Hit(unit, Id(unit, ComponentKind.FootActuator)); Hit(unit, Id(unit, ComponentKind.UpperLegActuator));
        check(unit.PilotingModifiers.Modifier == 7, "each damaged leg actuator adds 1");
        unit.Locations[MechLocation.RightLeg].Structure = 0;
        check(unit.PilotingModifiers.Modifier == 12 && unit.PilotingModifiers.Text == "gyro +3, hip +2, leg actuators +2, lost leg +5",
            "a lost leg adds 5, and the log names every modifier");
        sim.PilotingRollOverride = () => 12;
        check(sim.RecordKnockdownDamage(unit, 111) is { Target: 16, Fell: true }, "modifiers add to the target number; a roll below it fails");

        // A failed check: prone, with no firing, moving, twisting or jumping until it stands after Piloting seconds.
        sim = Battle(1, "griffin"); Arrange(sim); unit = Friendly(sim); var enemy = Enemy(sim);
        sim.PilotingRollOverride = () => 2;
        var fall = sim.RecordKnockdownDamage(unit, 111);
        check(fall is { Fell: true } && fall.Text.EndsWith("KNOCKED DOWN") && unit.Prone && unit.StandUpRemaining == unit.Warrior.Piloting
            && unit.FellAt == sim.Elapsed, "a failed check knocks the mech prone for its MechWarrior's Piloting in seconds");
        check(unit.WalkingSpeed == 0 && unit.RunningSpeed == 0 && sim.SelectAimTarget(unit) is null
            && unit.Weapons.All(weapon => !sim.CanFire(unit, enemy, weapon) && sim.WeaponStatus(unit, weapon, enemy) == "Prone")
            && unit.JumpUnavailableReason == "Prone" && unit.StanceText() == " · Prone · stands in 4s",
            "a prone mech cannot move, aim, fire or jump");
        check(sim.RecordKnockdownDamage(unit, 500) is null, "a prone mech makes no further checks");
        var at = unit.Position; var twist = unit.TorsoTwist; var facing = unit.Facing;
        sim.Move([unit.Id], new Vector2(-300, 0)); sim.Paused = false;
        var shots = unit.ShotsFired;
        sim.Advance(2);
        check(unit.Prone && unit.Position == at && unit.Facing == facing && unit.TorsoTwist == twist && unit.ShotsFired == shots && unit.Order == CombatOrder.Move,
            "a prone mech stays where it fell, holding its orders");
        unit.AddHeat(CombatRules.ShutdownHeat); var remaining = unit.StandUpRemaining;
        sim.Advance(1);
        check(unit.Shutdown && unit.Prone && unit.StandUpRemaining == remaining, "a shut-down mech does not get up");
        unit.AddHeat(-CombatRules.ShutdownHeat); sim.Advance(1.95);
        check(unit.Prone && unit.StandUpRemaining > 0, "standing takes the full Piloting seconds");
        sim.Advance(.1);
        check(!unit.Prone && unit.StoodAt is not null && unit.WalkingSpeed > 0, "the mech stands once its time is up");
        sim.Advance(4);
        check(unit.Position != at, "a mech back on its feet carries out its order");

        // In the air, a failed check takes effect on landing.
        sim = Battle(1, "griffin"); Arrange(sim, 600); unit = Friendly(sim);
        foreach (var item in sim.Units) item.Firing = FiringMode.HoldFire;
        sim.Jump([unit.Id], new Vector2(-100, 0)); sim.PilotingRollOverride = () => 2;
        check(unit.Airborne && sim.RecordKnockdownDamage(unit, 200) is { Fell: true } && !unit.Prone, "a mech knocked off balance in the air keeps flying");
        sim.Paused = false;
        for (var step = 0; step < 200 && unit.Airborne; step++) sim.Advance(CombatRules.StepSeconds);
        check(!unit.Airborne && unit.Prone && unit.FellAt == unit.LandedAt, "and lands prone");

        // A fought battle: autocannon hits knock mechs down; laser hits never make the check.
        sim = Battle(1, "hunchback", 11); Arrange(sim); sim.PilotingRollOverride = () => 2; sim.Paused = false;
        for (var step = 0; step < 2000 && !sim.History.Any(item => item.Shot.Piloting is not null); step++) sim.Advance(CombatRules.StepSeconds);
        var knock = sim.History.FirstOrDefault(item => item.Shot.Piloting is not null);
        check(knock is not null && knock.Shot.Weapon.StartsWith("AC") && sim.Unit(knock.Shot.TargetId).Prone
            && knock.CombatText!.Contains("Piloting check") && knock.CombatText.Contains("KNOCKED DOWN"),
            "an AC/20 hit makes the Piloting check, logged on that hit");
        var other = sim.Unit(knock!.Shot.TargetId);
        var replica = Battle(1, "hunchback", 11);
        check(sim.State().Units.Single(item => item.Id == other.Id) is { Prone: true, StandUpRemaining: > 0 },
            "a battle's state carries who is prone and for how long");
        check(replica.Units.All(item => !item.Prone), "a new battle starts with every mech standing");
    }
}
