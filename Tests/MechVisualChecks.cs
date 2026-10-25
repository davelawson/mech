using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestMechVisuals()
    {
        var roster = ForceRoster.Create("Awesome model review", [DraftCatalog.Mech("awesome"), DraftCatalog.Mech("jenner")], DraftCatalog.Warriors.Take(2));
        roster.Assign(roster.Mechs[0].Id, roster.Warriors[0].Id);
        var stage = new Control(); AddChild(stage); stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var hangar = new HangarScreen(); stage.AddChild(hangar); hangar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); hangar.OffsetTop = 72;
        hangar.Open(roster); await Frames();
        var model = hangar.Preview.Model!;
        Check(model.IsAwesome && model.Locations.Count == 8, "AWESOME hangar preview uses its own eight-location chassis model");
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        Check(meshes.All(mesh => model.Locations.Values.Any(root => root.IsAncestorOf(mesh))), "every visual detail belongs to a damage location, including cockpit and weapons");
        Check(meshes.Count(mesh => mesh.Name.ToString().StartsWith("PpcEmitter", StringComparison.Ordinal)) == 3,
            "AWESOME visibly carries three PPCs");
        Check(model.Locations[MechLocation.RightArm].FindChild("PpcEmitter", true, false) is MeshInstance3D { Position.X: > 0 }
            && model.Locations[MechLocation.LeftArm].FindChild("Fist", true, false) is not null,
            "right-arm PPC and left fist follow the hangar illustration's handedness");
        var yaw = model.Rotation.Y; hangar.Preview._Process(.5);
        Check(model.Rotation.Y != yaw, "hangar preview rotates automatically");
        hangar.Refresh();
        Check(ReferenceEquals(model, hangar.Preview.Model), "assignment refresh preserves model instance and rotation");
        hangar.Preview.SetProcess(false); model.Rotation = Vector3.Zero;
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames();
            Check(hangar.GetGlobalRect().Encloses(hangar.Preview.GetGlobalRect()) && hangar.Preview.Size.X > 200,
                $"AWESOME preview remains inside hangar at {size}");
            await Capture($"awesome-hangar-{size.X}x{size.Y}");
        }
        GetWindow().Size = new(1440, 900); await Frames();
        hangar.Select(1); Check(!hangar.Preview.Model!.IsAwesome, "other chassis retain generic geometry");
        // Owned condition is reflected before any battle starts.
        var snapshot = roster.Snapshot(); var saved = snapshot.Mechs[0];
        var damaged = saved with { Locations = saved.Locations.Select(loc => loc.Location == MechLocation.LeftArm
            ? loc with { Armor = 0, Structure = 0 } : loc.Location == MechLocation.RightTorso
            ? loc with { Armor = loc.Armor / 2 } : loc).ToArray() };
        var damagedRoster = ForceRoster.Restore(snapshot with { Mechs = [damaged] }, Campaign.StartDate);
        hangar.Open(damagedRoster); await Frames();
        Check(!hangar.Preview.Model!.Locations[MechLocation.LeftArm].Visible, "hangar model reflects an already-destroyed owned location");
        await Capture("awesome-hangar-damaged");
        hangar.Hide();
        var combat = new CombatScreen(); stage.AddChild(combat); combat.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        combat.Open(damagedRoster, damaged.Id); combat.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        var sim = combat.Simulation!; var friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        friendly.Position = new(-2.3f, 12); enemy.Position = new(2.3f, 12); friendly.Facing = enemy.Facing = Vector2.Down;
        sim.RefreshDetection(); combat.Battlefield.Camera.Size = 12; await Frames();
        Check(combat.Paperdoll.Unit == friendly && friendly.Locations[MechLocation.RightTorso].Armor == friendly.Locations[MechLocation.RightTorso].InitialArmor
            && CombatPaperdoll.ConditionColor(friendly.Locations[MechLocation.RightTorso].Armor, friendly.Spec.Locations.Single(loc => loc.Location == MechLocation.RightTorso).Armor) == CombatPaperdoll.ModerateDamage,
            "pre-deployment damage remains colored against stock maxima rather than deployment values");
        Check(CombatPaperdoll.ConditionColor(100, 100) == CombatPaperdoll.Full
            && CombatPaperdoll.ConditionColor(99, 100) == CombatPaperdoll.LightDamage
            && CombatPaperdoll.ConditionColor(2, 3) == CombatPaperdoll.ModerateDamage
            && CombatPaperdoll.ConditionColor(1, 3) == CombatPaperdoll.CriticalDamage,
            "paperdoll distinguishes full, light, moderate and critical condition at exact third boundaries");
        var combatModels = combat.Battlefield.FindChildren("*", "Node3D", true, false).OfType<MechModel>().ToArray();
        Check(combatModels.Length == 2 && combatModels.All(item => item.IsAwesome), "combat and hangar share the AWESOME model builder");
        await Capture("awesome-combat-friendly");
        var leftArm = enemy.Locations[MechLocation.LeftArm];
        enemy.ApplyDamage(MechLocation.LeftArm, leftArm.Armor + leftArm.Structure);
        enemy.Locations[MechLocation.LeftTorso].Armor = 0;
        enemy.Locations[MechLocation.LeftTorso].Structure /= 2;
        enemy.Locations[MechLocation.CenterTorso].Armor = 1;
        enemy.Locations[MechLocation.RightTorso].Armor = enemy.Locations[MechLocation.RightTorso].InitialArmor * 4 / 5;
        combat.Battlefield.InspectedEnemy = enemy.Id; combat.SelectUnits([], false); await Frames();
        Check(combat.Paperdoll.Unit == enemy && combat.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == CombatPaperdoll.Readout(enemy)),
            "enemy selection presents exact armor and structure through the shared paperdoll/readout");
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames(); await Capture($"awesome-combat-enemy-{size.X}x{size.Y}");
            Check(combat.Paperdoll.Size.X > 200, $"paperdoll fits sidebar width at {size}");
        }
        enemy.ApplyDamage(MechLocation.RightTorso, 100); await Frames();
        var enemyModel = combatModels.Single(item => item.Position.X == enemy.Position.X);
        Check(!enemyModel.Locations[MechLocation.RightTorso].Visible && !enemyModel.Locations[MechLocation.RightArm].Visible,
            "side-torso destruction removes its entire visual region and attached arm");
        enemy.ApplyDamage(MechLocation.Head, 100); await Frames();
        Check(!enemyModel.Locations[MechLocation.Head].Visible && !enemyModel.Locations[MechLocation.Head].IsVisibleInTree(),
            "head loss removes visor and laser along with cockpit geometry");
        GetWindow().Size = new(1440, 900);
        stage.QueueFree(); await Frames();
    }
}
