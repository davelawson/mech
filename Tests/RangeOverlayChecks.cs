using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    /// <summary>Arm weapons reach 60° further toward their own side; positive angles are toward the mech's right.</summary>
    private static void CheckArmArcs(Action<bool, string> check)
    {
        var sim = Battle(definition: "crusader"); var unit = Friendly(sim); var target = Enemy(sim);
        unit.Position = Vector2.Zero; unit.Facing = Vector2.Up; unit.TorsoTwist = Mathf.DegToRad(40);
        var right = unit.Weapons.First(weapon => weapon.Spec.Location == MechLocation.RightArm);
        var left = unit.Weapons.First(weapon => weapon.Spec.Location == MechLocation.LeftArm);
        bool At(CombatWeapon weapon, float degrees)
        {
            target.Position = unit.TorsoFacing.Rotated(Mathf.DegToRad(degrees)) * 300;
            return CombatRules.InFiringArc(unit, target, weapon);
        }
        check(At(right, 120) && At(right, -60) && !At(right, 120.1f) && !At(right, -60.1f),
            "a right-arm weapon fires from 60 degrees left to 120 degrees right of torso facing, boundaries included");
        check(At(left, -120) && At(left, 60) && !At(left, -120.1f) && !At(left, 60.1f), "a left-arm weapon mirrors the right arm");
        check(new Vector2(0, -1).AngleTo(new Vector2(1, 0)) > 0, "positive bearings turn toward the mech's right (+X faces right from -Y)");
        check(CombatRules.ArcDirection(right.Spec) == FiringArcDirection.RightArm
            && CombatRules.ArcDirection(unit.Weapons.First(weapon => CombatRules.IsLeg(weapon.Spec.Location)).Spec) == FiringArcDirection.Leg,
            "arm and leg mounts take their own arcs");
        // Straight behind the legs: an arm reaches it by twisting, a head or torso weapon never can.
        unit.TorsoTwist = 0; target.Position = Vector2.Down * 300;
        check(CombatRules.ReachableByTwist(unit, target, right) && CombatRules.ReachableByTwist(unit, target, left),
            "arm weapons can twist onto a target directly behind the legs");
        var awesome = Friendly(Battle()); awesome.Position = Vector2.Zero; awesome.Facing = Vector2.Up; awesome.TorsoTwist = 0;
        var head = awesome.Weapons.Single(weapon => weapon.Spec.Location == MechLocation.Head);
        var behind = Enemy(Battle()); behind.Position = Vector2.Down * 300;
        var flank = Enemy(Battle()); flank.Position = Vector2.Up.Rotated(Mathf.DegToRad(149)) * 300;
        check(!CombatRules.ReachableByTwist(awesome, behind, head) && CombatRules.ReachableByTwist(awesome, flank, head),
            "head and torso weapons reach 90 degrees of twist plus 60 degrees of arc");
    }

    /// <summary>The weapon range and firing arc overlays: grouping, colours by class, and which weapons count.</summary>
    private static void CheckRangeOverlays(Action<bool, string> check)
    {
        var unit = Friendly(Battle(definition: "crusader"));
        float Long(string name) => unit.Weapons.First(weapon => weapon.Spec.Name == name).LongRange;
        var (lrm, srm, laser, gun) = (Long("LRM 15"), Long("SRM 6"), Long("Medium Laser"), Long("Machine Gun"));
        check(srm == laser && lrm > srm && laser > gun, "the Crusader fixture shares one long range between its SRMs and lasers");
        string Circles() => string.Join(" ", CombatRules.WeaponRangeCircles(unit).Select(circle => $"{circle.Range}:{string.Join("+", circle.Classes)}"));
        string Wedges() => string.Join(" ", CombatRules.FiringArcWedges(unit)
            .Select(wedge => $"{wedge.Direction}/{wedge.Range}:{string.Join("+", wedge.Classes)}"));
        check(Circles() == $"{lrm}:Missile {laser}:Energy+Missile {gun}:Ballistic",
            "one circle per distinct long range, longest first, with every class sharing it in a fixed order");
        check(Wedges() == $"LeftArm/{lrm}:Missile RightArm/{lrm}:Missile LeftArm/{laser}:Energy RightArm/{laser}:Energy Leg/{laser}:Missile "
            + $"LeftArm/{gun}:Ballistic RightArm/{gun}:Ballistic",
            "one wedge per arc and long range; the twin leg SRMs share one");

        foreach (var weapon in unit.Weapons.Where(weapon => weapon.Spec.Name == "Medium Laser")) weapon.Enabled = false;
        check(Circles() == $"{lrm}:Missile {laser}:Missile {gun}:Ballistic", "weapons switched off leave the overlays");
        var leftGun = unit.Weapons.First(weapon => weapon.Spec.Name == "Machine Gun" && weapon.Spec.Location == MechLocation.LeftArm);
        unit.CriticalHit(MechLocation.LeftArm, leftGun.Spec.Id, out _);
        check(!unit.WeaponIntact(leftGun) && Wedges().Contains($"RightArm/{gun}") && !Wedges().Contains($"LeftArm/{gun}"),
            "destroyed weapons leave the overlays");
        var lrmAmmo = unit.Weapons.First(weapon => weapon.Spec.Name == "LRM 15").Spec.AmmoKind!.Value;
        while (unit.ConsumeAmmo(lrmAmmo)) { }
        check(!Circles().Contains($"{lrm}:") && !Wedges().Contains($"/{lrm}:"), "weapons out of ammunition leave the overlays");
    }
}
