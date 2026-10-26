using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestFinalChassis()
    {
        var ids = new[] { "firestarter", "crusader", "thunderbolt", "victor", "zeus" };
        foreach (var id in ids)
        {
            var roster = ForceRoster.Create("Model review", [DraftCatalog.Mech(id)], DraftCatalog.Warriors.Take(1));
            roster.Assign(roster.Mechs[0].Id, roster.Warriors[0].Id);
            var stage = new Control(); AddChild(stage); stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var hangar = new HangarScreen(); stage.AddChild(hangar); hangar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); hangar.OffsetTop = 72;
            hangar.Open(roster); hangar.Preview.SetProcess(false); await Frames();
            var model = hangar.Preview.Model!; var spec = roster.Mechs[0].Spec;
            model.Rotation = Vector3.Zero;
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
            Check(model.HasLocomotion && model.Locations.Count == 8 && meshes.Length >= 20 && hangar.GaitPicker.Visible,
                $"{spec.Chassis} uses its articulated source model and preview");
            Check(meshes.All(mesh => model.Locations.Values.Count(root => root.IsAncestorOf(mesh)) == 1),
                $"{spec.Chassis} source geometry has unique damage ownership");
            Check(model.Locations[MechLocation.Head].FindChild("AmberCanopy", true, false) is not null
                && model.Locations[MechLocation.Head].FindChild("CockpitFrame", true, false) is not null,
                $"{spec.Chassis} includes its cockpit exterior and glazing");
            var srmCount = model.FindChildren("MissileTube*", "Node3D", true, false).Count;
            var lrmCount = model.FindChildren("LrmTube*", "Node3D", true, false).Count;
            var laserCount = meshes.Count(mesh => mesh.Name.ToString().StartsWith("LaserEmitter"));
            var flamerCount = meshes.Count(mesh => mesh.Name.ToString().StartsWith("Flamer"));
            var gunCount = meshes.Count(mesh => mesh.Name.ToString().StartsWith("MachineGun"));
            var cannonCount = model.FindChildren("CannonMuzzle", "Node3D", true, false).Count;
            var expected = id switch
            {
                "firestarter" => (0, 0, 2, 4, 2, 0), "crusader" => (12, 30, 2, 0, 2, 0),
                "thunderbolt" => (2, 15, 4, 0, 2, 0), "victor" => (4, 0, 2, 0, 0, 1),
                _ => (0, 15, 3, 0, 0, 1)
            };
            Check((srmCount, lrmCount, laserCount, flamerCount, gunCount, cannonCount) == expected,
                $"{spec.Chassis} retains its complete MW5 stock weapon visuals and firing sockets");
            foreach (var gait in new[] { PreviewGait.Walk, PreviewGait.Run })
            {
                hangar.GaitPicker.Select((int)gait); hangar.GaitPicker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)gait);
                for (var frame = 0; frame < 180; frame++) hangar.Preview._Process(1d / 60);
                TestLongStride(model, gait, gait == PreviewGait.Walk ? .33f : .45f);
                model.Rotation = new(0, .32f, 0); await Capture($"{id}-{gait.ToString().ToLowerInvariant()}-imported"); model.Rotation = Vector3.Zero;
            }
            var walk = CombatRules.MovementSpeed(spec, MovementMode.Walk); var run = CombatRules.MovementSpeed(spec, MovementMode.Run);
            var feet = model.FindChildren("Foot", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray(); var grounded = true;
            for (var frame = 0; frame < 180; frame++)
            {
                model.AnimateLocomotion(1f / 60, -walk, walk, run);
                var bottoms = feet.Select(foot => Enumerable.Range(0, 8).Min(corner => (foot.GlobalTransform * foot.Mesh.GetAabb().GetEndpoint(corner)).Y)).ToArray();
                grounded &= bottoms.All(bottom => bottom >= -.002f) && bottoms.Any(bottom => bottom < .02f);
            }
            Check(grounded && model.RunBlend < .01f, $"{spec.Chassis} reverses using grounded walking");
            var ankles = model.FindChildren("AnklePivot", "Node3D", true, false).OfType<Node3D>().ToArray();
            foreach (var turn in new[] { -1, 1 })
            {
                var steps = false;
                for (var frame = 0; frame < 150; frame++) { model.AnimateLocomotion(1f / 60, 0, walk, run, turn * Mathf.DegToRad(45)); steps |= Math.Abs(ankles[0].GlobalPosition.Y - ankles[1].GlobalPosition.Y) > .025f; }
                Check(steps, $"{spec.Chassis} steps during pivot {turn}");
            }
            for (var frame = 0; frame < 180; frame++) model.AnimateLocomotion(1f / 60, 0, walk, run);
            foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
            {
                GetWindow().Size = size; await Frames();
                var camera = hangar.Preview.FindChildren("*", "Camera3D", true, false).OfType<Camera3D>().Single();
                var bounds = new Rect2(Vector2.One, camera.GetViewport().GetVisibleRect().Size - Vector2.One * 2); var fits = true;
                for (var angle = 0; angle < 12; angle++)
                {
                    model.Rotation = new(0, angle * Mathf.Tau / 12, 0);
                    foreach (var mesh in meshes) for (var corner = 0; corner < 8; corner++)
                    { var point = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetEndpoint(corner); fits &= !camera.IsPositionBehind(point) && bounds.HasPoint(camera.UnprojectPosition(point)); }
                }
                Check(fits, $"{spec.Chassis} fits its rotating preview at {size}");
            }
            GetWindow().Size = new(1440, 900); await Frames();
            hangar.GaitPicker.Select(0); hangar.GaitPicker.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            model.Rotation = new(0, .32f, 0); await Capture($"{id}-hangar-imported");
            foreach (var (location, root) in model.Locations)
            {
                model.SetCondition(part => part != location);
                Check(root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().All(mesh => !mesh.IsVisibleInTree()),
                    $"{spec.Chassis} hides {location} geometry after damage");
            }
            model.SetCondition(_ => true); hangar.Hide();
            var field = new CombatViewport(); stage.AddChild(field); field.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); field.OffsetTop = 72;
            var sim = CombatSimulation.Deploy(roster, roster.Mechs.Select(mech => mech.Id), 183); sim.EnemyAiEnabled = false;
            field.Display(sim); field.SetProcess(false); await Frames();
            var combatModels = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().ToArray();
            Check(combatModels.Length == 2 && combatModels.All(mech => mech.ChassisId == id && mech.HasLocomotion),
                $"both combat sides use the imported {spec.Chassis} rig");
            stage.QueueFree(); await Frames();
        }
    }
}
