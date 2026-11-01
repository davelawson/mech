using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private static MechSpec Refit(string id, Func<WeaponSpec, WeaponSpec?> change)
    {
        var spec = DraftCatalog.Mech(id);
        return spec with { Weapons = [.. spec.Weapons.Select(change).OfType<WeaponSpec>()] };
    }

    private static MeshInstance3D? Part(MechModel model, MechLocation location, string name) =>
        model.Locations[location].FindChild(name, true, false) as MeshInstance3D;

    private async Task TestWeaponMounts()
    {
        foreach (var spec in DraftCatalog.AllMechs.Where(mech => MechModel.Hardpoints(mech.Id).Count > 0))
        {
            var hardpoints = MechModel.Hardpoints(spec.Id);
            var expected = hardpoints.SelectMany(item => item.Visuals.Select(visual => (item.Id, visual))).ToHashSet();
            var library = MechModel.WeaponLibraryVisuals(spec.Id).ToHashSet();
            Check(library.SetEquals(expected), $"{spec.Id} weapon library holds exactly its hardpoint table's visuals; missing "
                + string.Join(", ", expected.Except(library).Take(6)) + "; extra " + string.Join(", ", library.Except(expected).Take(6)));
            var stock = WeaponMounts.Resolve(spec);
            Check(stock.Stock && stock.Visuals.Count == hardpoints.Count(item => item.Stock is not null)
                && hardpoints.Where(item => item.Stock is not null).All(item => stock.Visuals[item.Id] == item.Stock),
                $"{spec.Id} catalog loadout resolves to its stock hardpoint visuals");
        }
        var full = new[] { "atlas", "awesome", "catapult", "crusader", "firestarter", "griffin", "jenner", "panther",
            "shadow-hawk", "thunderbolt", "victor", "wolverine", "zeus" };
        Check(full.All(id => MechModel.Hardpoints(id).Any(item => item.Stock is null)),
            "the thirteen fully exported chassis offer hardpoints beyond their stock mounts");
        foreach (var id in full)
        {
            var model = MechModel.Create(DraftCatalog.Mech(id), CombatSide.Friendly);
            Check(MechModel.Hardpoints(id).Where(item => item.StockPart is not null).All(item =>
                    Part(model, item.Location, item.StockPart![(item.Location.ToString().Length + 1)..]) is not null)
                && !model.FindChildren("*", "MeshInstance3D", true, false).Any(mesh => MechModel.Hardpoints(id).Any(item =>
                    mesh.Name.ToString().StartsWith(item.Id + "_", StringComparison.Ordinal))),
                $"{id} stock model keeps its prepared weapon parts and adds no library visuals");
            model.Free();
        }

        // Wolverine: right-arm AC/5 → AC/10 swaps the cannon on the same ballistic hardpoint.
        var stage = new Node3D(); AddChild(stage);
        var cannon = DraftCatalog.Mech("wolverine").Weapons.Single(weapon => weapon.Kind == WeaponKind.AC5);
        var refit = Refit("wolverine", weapon => weapon.Id == cannon.Id ? weapon with { Kind = WeaponKind.AC10 } : weapon);
        var mounts = WeaponMounts.Resolve(refit);
        Check(!mounts.Stock && mounts.Visuals["Forearm_Right_BH1"] == "AC10" && mounts.WeaponHardpoints[cannon.Id] == "Forearm_Right_BH1"
            && mounts.Visuals["Torso_Left_MH1"] == "Missile6" && mounts.Visuals["Head_EH1"] == "Laser" && mounts.Unmounted.IsEmpty,
            "Wolverine AC/10 refit takes the right-arm ballistic hardpoint and leaves the other stock visuals");
        var wolverine = MechModel.Create(refit, CombatSide.Friendly); stage.AddChild(wolverine);
        var ac10 = Part(wolverine, MechLocation.RightArm, "Forearm_Right_BH1_AC10");
        Check(ac10 is not null && Part(wolverine, MechLocation.RightArm, "CannonHousing") is null
            && Part(wolverine, MechLocation.LeftTorso, "Srm6Housing") is not null && Part(wolverine, MechLocation.Head, "LaserEmitter") is not null,
            "Wolverine shows the AC/10 visual in place of its stock AC/5 housing");
        Check(wolverine.Locations[MechLocation.RightArm].FindChild("ShoulderPivot", false, false) is Node3D shoulder && shoulder.IsAncestorOf(ac10),
            "the swapped arm cannon rides the shoulder pivot");
        var paint = ac10!.GetActiveMaterial(0) as StandardMaterial3D;
        Check(paint?.AlbedoTexture is { } texture && texture.GetSize() == new Vector2(2048, 2048) && paint.NextPass is ShaderMaterial,
            "library visuals use the chassis's own weapon paint and team outline");
        var muzzle = wolverine.NextFiringMarker(cannon.Id);
        Check(muzzle is not null && muzzle.Name == "Forearm_Right_BH1_AC10_Fire00" && muzzle.GetParent() == ac10.GetParent(),
            "the AC/10 fires from its own library muzzle");
        var rest = muzzle!.GlobalPosition;
        var walk = CombatRules.MovementSpeed(refit, MovementMode.Walk); var run = CombatRules.MovementSpeed(refit, MovementMode.Run);
        for (var frame = 0; frame < 20; frame++) wolverine.AnimateLocomotion(1f / 60, walk, walk, run);
        Check(muzzle.GlobalPosition.DistanceTo(rest) > .01f, "the swapped cannon's muzzle moves with the walking arm");
        wolverine.SetCondition(location => location != MechLocation.RightArm);
        Check(!ac10.IsVisibleInTree(), "destroying the arm hides its swapped visual");

        // No compatible hardpoint: Wolverine's head energy slot has no PPC visual.
        var laser = DraftCatalog.Mech("wolverine").Weapons.Single(weapon => weapon.Location == MechLocation.Head);
        var headPpc = Refit("wolverine", weapon => weapon.Id == laser.Id ? weapon with { Kind = WeaponKind.PPC } : weapon);
        var unmounted = MechModel.Create(headPpc, CombatSide.Friendly); stage.AddChild(unmounted);
        Check(unmounted.Mounts.Unmounted.SequenceEqual([laser.Id]) && Part(unmounted, MechLocation.Head, "LaserEmitter") is null
            && unmounted.NextFiringMarker(laser.Id) is null,
            "a weapon without a compatible hardpoint is unmounted, with no visual or marker");

        // Energy slot: one Jenner arm laser becomes a PPC on the same hardpoint; the other laser keeps its stock part.
        var jennerLaser = DraftCatalog.Mech("jenner").Weapons.First(weapon => weapon.Location == MechLocation.LeftArm);
        var jenner = MechModel.Create(Refit("jenner", weapon => weapon.Id == jennerLaser.Id ? weapon with { Kind = WeaponKind.PPC } : weapon),
            CombatSide.Friendly); stage.AddChild(jenner);
        var ppcPoint = jenner.Mounts.WeaponHardpoints[jennerLaser.Id];
        Check(Part(jenner, MechLocation.LeftArm, $"{ppcPoint}_PPC") is not null
            && new[] { "LaserEmitter0", "LaserEmitter1" }.Count(name => Part(jenner, MechLocation.LeftArm, name) is not null) == 1,
            "a Jenner arm laser becomes a PPC on its energy hardpoint");
        Check(jenner.NextFiringMarker(jennerLaser.Id)?.Name.ToString().StartsWith($"{ppcPoint}_PPC_Fire", StringComparison.Ordinal) == true,
            "the swapped PPC fires from its library muzzle");

        // Emptied stock hardpoint: removing the Jenner's SRM 4 closes its rack with the source blank cover.
        var srm = DraftCatalog.Mech("jenner").Weapons.Single(weapon => weapon.Kind == WeaponKind.SRM4);
        var covered = MechModel.Create(Refit("jenner", weapon => weapon.Id == srm.Id ? null : weapon), CombatSide.Friendly); stage.AddChild(covered);
        Check(covered.Mounts.Visuals["Torso_Center_MH3"] == WeaponMounts.Blank && Part(covered, MechLocation.CenterTorso, "Torso_Center_MH3_Blank") is not null
            && Part(covered, MechLocation.CenterTorso, "Srm4Housing") is null && covered.Locations[MechLocation.CenterTorso]
                .FindChildren("MissileTube*", "Node3D", true, false).Count == 0,
            "an emptied Jenner SRM rack shows its blank cover and drops its tubes");

        // Family rule: the Atlas's LRM 20 keeps its stock ten-tube rack when an unrelated arm laser changes.
        var atlasLaser = DraftCatalog.Mech("atlas").Weapons.First(weapon => weapon.Location == MechLocation.LeftArm);
        var atlas = WeaponMounts.Resolve(Refit("atlas", weapon => weapon.Id == atlasLaser.Id ? weapon with { Kind = WeaponKind.PPC } : weapon));
        Check(atlas.Visuals["Torso_Left_MH1"] == "Missile10" && atlas.Visuals["Forearm_Left_EH1"] == "PPC" && atlas.Unmounted.IsEmpty,
            "Atlas keeps its stock LRM visual after an unrelated refit");
        // Catalog leg SRMs keep the Crusader's torso racks after an arm change.
        var crusaderLaser = DraftCatalog.Mech("crusader").Weapons.First(weapon => weapon.Location == MechLocation.RightArm
            && weapon.Kind is WeaponKind.MediumLaser or WeaponKind.SmallLaser);
        var crusader = WeaponMounts.Resolve(Refit("crusader", weapon => weapon.Id == crusaderLaser.Id ? weapon with { Kind = WeaponKind.PPC } : weapon));
        Check(crusader.Visuals["Torso_Left_MH1"] == "Missile6" && crusader.Visuals["Torso_Right_MH1"] == "Missile6"
            && DraftCatalog.Mech("crusader").Weapons.Where(weapon => weapon.Kind == WeaponKind.SRM6).All(weapon => crusader.WeaponHardpoints.ContainsKey(weapon.Id)),
            "Crusader leg SRMs stay on their torso racks after a refit");
        stage.QueueFree(); await Frames();

        // Rendered review (`scripts/mech capture mech-visuals`): stock and refitted blueprints side by side.
        var review = new (string Name, MechSpec Spec)[]
        {
            ("wolverine-stock", DraftCatalog.Mech("wolverine")), ("wolverine-ac10", refit),
            ("jenner-arm-ppc", Refit("jenner", weapon => weapon.Id == jennerLaser.Id ? weapon with { Kind = WeaponKind.PPC } : weapon)),
            ("jenner-srm-cover", Refit("jenner", weapon => weapon.Id == srm.Id ? null : weapon)),
        };
        // Owned mechs keep catalog loadouts until refitting exists, so each refit's mount map drives the preview.
        var roster = ForceRoster.Create("Weapon mount review", [.. review.Select(item => DraftCatalog.Mech(item.Spec.Id))], DraftCatalog.Warriors.Take(1));
        var screen = new Control(); AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var hangar = new HangarScreen(); screen.AddChild(hangar); hangar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hangar.Open(roster); hangar.Board.Visible = false; await Frames();
        for (var i = 0; i < review.Length; i++)
        {
            hangar.Select(i); hangar.Preview.ShowMounts(WeaponMounts.Resolve(review[i].Spec)); await Frames();
            Check(hangar.Preview.Model!.Mounts.Stock == (i == 0), $"hangar preview of {review[i].Name} uses its resolved mounts");
            await Capture($"weapon-mounts-{review[i].Name}");
        }
        screen.QueueFree(); await Frames();
    }
}
