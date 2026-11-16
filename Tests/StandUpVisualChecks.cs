using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Every rigged chassis lies face down, pushes up onto its knees, crouches with its feet planted and stands,
    /// resting on the ground throughout.</summary>
    private async Task TestStandUpVisuals()
    {
        await CaptureStandUp();
        foreach (var spec in DraftCatalog.AllMechs.DistinctBy(mech => mech.Chassis))
        {
            var model = MechModel.Create(spec, CombatSide.Friendly); AddChild(model); await Frames();
            if (!model.HasLocomotion) { model.QueueFree(); continue; }
            Node3D[] Pivots(string name) => model.FindChildren(name, "Node3D", true, false).OfType<Node3D>().ToArray();
            var (hips, knees, ankles, all) = (Pivots("HipPivot"), Pivots("KneePivot"), Pivots("AnklePivot"), Pivots("*Pivot"));
            var height = model.Height;
            float Height(IEnumerable<Node3D> nodes) => nodes.Average(node => node.GlobalPosition.Y);
            var problems = new List<string>();
            float Place(MechModel.Posture posture)
            {
                model.Rotation = new Vector3(posture.Pitch, 0, 0);
                model.Position = posture.Offset * model.Scale.X;
                if (!all.All(pivot => float.IsFinite(pivot.Rotation.Length()))) problems.Add("non-finite joint");
                return hips.Average(hip => hip.GlobalPosition.Y);
            }
            var jets = model.FindChildren("JumpJet_*", "Node3D", true, false);
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Where(mesh => mesh.Mesh is not null && !jets.Any(jet => jet.IsAncestorOf(mesh))).Select(mesh => (mesh, Faces: mesh.Mesh.GetFaces())).ToArray();
            // Every vertex, so the model's sampled contact is checked against the exact geometry.
            float Lowest() => meshes.Where(item => item.mesh.IsVisibleInTree())
                .Min(item => { var transform = item.mesh.GlobalTransform; return item.Faces.Min(vertex => (transform * vertex).Y); });

            string LowestMesh() => meshes.Where(item => item.mesh.IsVisibleInTree())
                .MinBy(item => { var transform = item.mesh.GlobalTransform; return item.Faces.Min(vertex => (transform * vertex).Y); })
                .mesh?.GetPath().ToString().Split('/')[^3..].Join("/") ?? "";
            Place(model.AnimateStandUp(1)); var restLowest = Lowest(); var restFeet = ankles.Average(ankle => ankle.GlobalPosition.Z);
            var restAnkles = Height(ankles);
            Place(model.AnimateStandUp(0));
            var head = model.LocationCenter(MechLocation.Head);
            if (!(model.Rotation.X < -1.5f && head.Z < -.5f * height && head.Y < .45f * height)) problems.Add($"not lying face down (head {head})");
            for (var progress = 0f; progress <= 1.001f; progress += .06f)
            {
                var hip = Place(model.AnimateStandUp(progress));
                if (Math.Abs(Lowest() - restLowest) > .01f * height) problems.Add($"off the ground at {progress:0.00} ({Lowest() - restLowest:0.00})");
                var feet = ankles.Average(ankle => ankle.GlobalPosition.Z);
                if (Math.Abs(feet - restFeet) > .01f * height) problems.Add($"feet slide at {progress:0.00} ({feet - restFeet:0.00})");
            }
            var kneeling = Place(model.AnimateStandUp(.38f));
            if (!(Height(knees) < .25f * height && kneeling > Height(knees))) problems.Add($"not kneeling (knees {Height(knees):0.0}, hips {Height(hips):0.0}, height {height:0.0})");
            var rest = Place(model.AnimateStandUp(1));
            if (Math.Abs(model.Rotation.X) > 1e-4f || model.Position.Length() > .001f * height
                || all.Any(pivot => pivot.Rotation.Length() > 1e-3f)) problems.Add($"does not finish at rest ({model.Rotation.X:0.000}, {model.Position}, {string.Join(",", all.Where(pivot => pivot.Rotation.Length() > 1e-3f).Select(pivot => pivot.Name + pivot.Rotation))})");
            var crouching = Place(model.AnimateStandUp(.68f));
            if (!(crouching < rest - .05f * height && ankles.All(ankle => ankle.GlobalPosition.Y < restAnkles + .04f * height))) problems.Add($"not crouching (hips {Height(hips):0.0} vs {rest:0.0}, ankles {ankles.Max(ankle => ankle.GlobalPosition.Y):0.0} vs {restAnkles:0.0}, resting on {LowestMesh()})");
            Check(problems.Count == 0, $"{spec.Chassis} gets up from face down through a kneel and a crouch{(problems.Count > 0 ? ": " + string.Join("; ", problems.Distinct()) : "")}");
            model.QueueFree();
        }
        await Frames();
    }
    /// <summary>Side views of the stand-up keys, lying to standing, for forward-kneed and rearward-kneed chassis.</summary>
    private async Task CaptureStandUp()
    {
        if (!_screenshots || DisplayServer.GetName() == "headless") return;
        var layer = new CanvasLayer { Layer = 100 }; AddChild(layer);
        var container = new SubViewportContainer { Stretch = true }; container.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(container);
        var view = new SubViewport { OwnWorld3D = true }; container.AddChild(view);
        view.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new("1d2226"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new("8a9098") } });
        view.AddChild(new DirectionalLight3D { RotationDegrees = new(-50, 60, 0) });
        var progress = new[] { 0, .14f, .38f, .53f, .68f, .84f, 1 };
        foreach (var id in new[] { "atlas", "catapult", "firestarter", "wolverine", "crusader" })
        {
            var spec = DraftCatalog.Mech(id);
            var height = 0f; var models = new List<Node3D>();
            for (var i = 0; i < progress.Length; i++)
            {
                var model = MechModel.Create(spec, CombatSide.Friendly); view.AddChild(model); models.Add(model); height = model.Height;
                var posture = model.AnimateStandUp(progress[i]);
                model.Rotation = new Vector3(posture.Pitch, 0, 0);
                // Lying first on the left: the camera looks along −X, so screen left is +Z.
                model.Position = new Vector3(0, 0, (progress.Length - 1 - i) * height * 1.5f) + posture.Offset * model.Scale.X;
            }
            var span = (progress.Length - 1) * height * 1.5f;
            var ground = new MeshInstance3D { Mesh = new BoxMesh { Size = new(height, height * .02f, span + height * 3) }, Position = new(0, -height * .01f, span / 2) };
            view.AddChild(ground); models.Add(ground);
            var center = new Vector3(0, height * .5f, (span - height) / 2);
            var camera = new Camera3D { Name = "StandUpCamera", Projection = Camera3D.ProjectionType.Orthogonal, Size = (span + height * 2.4f) / 1.6f,
                Current = true, Position = center + new Vector3(height * 3, height * .25f, 0) };
            view.AddChild(camera); camera.LookAt(center);
            await Frames(); await Capture($"stand-up-{id}");
            foreach (var model in models) model.QueueFree();
            camera.Name = "Done"; camera.QueueFree(); await Frames();
        }
        layer.QueueFree();
    }
}
