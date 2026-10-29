using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestAdditionalBatch()
    {
        var draftIds = DraftCatalog.Mechs.Select(mech => mech.Id).ToHashSet();
        var mechs = DraftCatalog.AllMechs.Where(mech => !draftIds.Contains(mech.Id) && mech.Id != "atlas").ToArray();
        Check(mechs.Length == 36, "all 36 additional stock chassis are registered outside the draft");
        var mauler = DraftCatalog.Mech("mauler");
        Check(mauler.HeatSinkType == HeatSinkKind.Double && mauler.HeatSinks == 11 && mauler.Engine.Contains("XL")
            && mauler.Weapons.Count(w => w.Name == "AC/2") == 4 && mauler.Weapons.Count(w => w.Name == "ER Large Laser") == 2,
            "Mauler retains its later-era stock engine, heat sinks, and weapons");
        var stage = new Control(); AddChild(stage); stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var hangar = new HangarScreen(); stage.AddChild(hangar); hangar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); hangar.OffsetTop = 72;
        hangar.Preview.SetProcess(false); var failures = new List<string>();
        foreach (var spec in mechs)
        {
            var roster = ForceRoster.Create("Additional model review", [spec], DraftCatalog.Warriors.Take(1));
            roster.Assign(roster.Mechs[0].Id, roster.Warriors[0].Id);
            hangar.Open(roster); await Frames(); var model = hangar.Preview.Model!; model.Rotation = Vector3.Zero;
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
            if (!model.HasLocomotion || !hangar.GaitPicker.Visible || model.Locations.Count != 8 || meshes.Length < 12
                || meshes.Any(mesh => model.Locations.Values.Count(root => root.IsAncestorOf(mesh)) != 1)) failures.Add($"{spec.Id}: model ownership/rig");
            if (model.Locations[MechLocation.Head].FindChild("AmberCanopy", true, false) is null) failures.Add($"{spec.Id}: cockpit");
            for (var weapon = 0; weapon < spec.Weapons.Length; weapon++)
            {
                if (model.FindChildren($"Weapon{weapon}", "MeshInstance3D", true, false).Count != 1
                    || model.FindChildren($"WeaponMuzzle_Weapon{weapon}_*", "Node3D", true, false).Count == 0)
                    failures.Add($"{spec.Id}: weapon {weapon} mesh or firing position");
            }
            var ankles = new[] { MechLocation.LeftLeg, MechLocation.RightLeg }.Select(loc => (Node3D)model.Locations[loc].FindChild("AnklePivot", true, false)).ToArray();
            var feet = model.FindChildren("Foot", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
            var walk = CombatRules.MovementSpeed(spec, MovementMode.Walk); var run = CombatRules.MovementSpeed(spec, MovementMode.Run);
            foreach (var gait in new[] { PreviewGait.Walk, PreviewGait.Run })
            {
                var minimum = new[] { float.PositiveInfinity, float.PositiveInfinity }; var maximum = new[] { float.NegativeInfinity, float.NegativeInfinity }; var grounded = true;
                var speed = gait == PreviewGait.Walk ? walk : run;
                for (var sample = 0; sample < 1800; sample++)
                {
                    model.AnimateLocomotion(1f / 600, speed, walk, run);
                    if (sample < 300) continue;
                    grounded &= feet.Any(foot => Math.Abs(Enumerable.Range(0, 8).Min(i => (foot.GlobalTransform * foot.Mesh.GetAabb().GetEndpoint(i)).Y)) < .002f);
                    for (var leg = 0; leg < 2; leg++)
                    {
                        var z = (model.GlobalTransform.AffineInverse() * ankles[leg].GlobalPosition).Z;
                        minimum[leg] = Math.Min(minimum[leg], z); maximum[leg] = Math.Max(maximum[leg], z);
                    }
                }
                var expected = gait == PreviewGait.Walk ? .33f : .45f;
                if (!grounded || Enumerable.Range(0, 2).Any(leg => Math.Abs(maximum[leg] - minimum[leg] - expected) >= .003f))
                    failures.Add($"{spec.Id}: {gait} travel={maximum[0]-minimum[0]:F3}/{maximum[1]-minimum[1]:F3} support={grounded}");
            }
            var backwardsGrounded = true;
            for (var frame = 0; frame < 180; frame++)
            {
                model.AnimateLocomotion(1f / 60, -walk, walk, run);
                var bottoms = feet.Select(foot => Enumerable.Range(0, 8).Min(i => (foot.GlobalTransform * foot.Mesh.GetAabb().GetEndpoint(i)).Y)).ToArray();
                backwardsGrounded &= bottoms.All(y => y >= -.002f) && bottoms.Any(y => y < .02f);
            }
            if (!backwardsGrounded) failures.Add($"{spec.Id}: reverse grounding");
            foreach (var turn in new[] { -1, 1 })
            {
                var steps = false;
                for (var frame = 0; frame < 150; frame++)
                {
                    model.AnimateLocomotion(1f / 60, 0, walk, run, turn * Mathf.DegToRad(45));
                    steps |= Math.Abs(ankles[0].GlobalPosition.Y - ankles[1].GlobalPosition.Y) > .025f;
                }
                if (!steps) failures.Add($"{spec.Id}: pivot {turn}");
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
                    {
                        var point = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetEndpoint(corner);
                        fits &= !camera.IsPositionBehind(point) && bounds.HasPoint(camera.UnprojectPosition(point));
                    }
                }
                if (!fits) failures.Add($"{spec.Id}: preview framing {size}");
            }
            GetWindow().Size = new(1440, 900); await Frames();
            foreach (var (location, root) in model.Locations)
            {
                model.SetCondition(part => part != location);
                if (root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Any(mesh => mesh.IsVisibleInTree())) failures.Add($"{spec.Id}: {location} damage");
            }
            model.SetCondition(_ => true); model.Rotation = new(0, .32f, 0);
            await Capture($"additional-{spec.Id}");
        }
        hangar.Hide();
        foreach (var group in mechs.Chunk(4))
        {
            var roster = ForceRoster.Create("Additional combat review", group, DraftCatalog.Warriors.Take(group.Length));
            for (var i = 0; i < group.Length; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
            var field = new CombatViewport(); stage.AddChild(field); field.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); field.OffsetTop = 72;
            var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(mech => mech.Id), BattlefieldTerrain.Flat, 183); sim.EnemyAiEnabled = false;
            field.Display(sim); field.SetProcess(false); await Frames();
            var models = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().ToArray();
            if (models.Length != group.Length * 2 || group.Any(spec => models.Count(model => model.ChassisId == spec.Id && model.HasLocomotion) != 2))
                failures.Add($"combat model selection: {string.Join(", ", group.Select(spec => spec.Id))}");
            field.QueueFree(); await Frames();
        }
        GD.Print("Additional model audit: " + (failures.Count == 0 ? "all pass" : string.Join("; ", failures)));
        Check(failures.Count == 0, "all additional models retain damage ownership and grounded longer strides: " + string.Join("; ", failures));
        stage.QueueFree(); await Frames();
    }
}
