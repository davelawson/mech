using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Every jump-capable chassis tucks its legs through a jump, absorbs the landing and fires its jets from the locations holding them.</summary>
    private async Task TestJumpVisuals()
    {
        foreach (var spec in DraftCatalog.AllMechs.Where(mech => mech.Jump > 0))
        {
            var model = MechModel.Create(spec, CombatSide.Friendly); AddChild(model); await Frames();
            var ankles = model.FindChildren("AnklePivot", "Node3D", true, false).OfType<Node3D>().ToArray();
            var pivots = model.FindChildren("*Pivot", "Node3D", true, false).OfType<Node3D>().ToArray();
            float Lift() => ankles.Average(ankle => (ankle.GlobalPosition - model.GlobalPosition).Y);
            model.AnimateJump(0); var crouch = Lift();
            var finite = pivots.All(pivot => float.IsFinite(pivot.Rotation.Length()));
            model.AnimateJump(.5f); var tucked = Lift();
            finite &= pivots.All(pivot => float.IsFinite(pivot.Rotation.Length()));
            model.AnimateLanding(MechModel.LandingSeconds / 2);
            finite &= pivots.All(pivot => float.IsFinite(pivot.Rotation.Length()));
            Check(ankles.Length == 2 && finite && tucked > crouch + .01f * model.Scale.Y,
                $"{spec.Chassis} tucks its legs at the top of a jump: {crouch:0.00} → {tucked:0.00}");
            model.SetJetting(MechModel.JetStrength(.1f), 1);
            var jetLocations = spec.Components.Where(item => item.Kind == ComponentKind.JumpJet).Select(item => item.Location).Distinct().ToArray();
            var nozzles = model.FindChildren("JumpJet_*", "Node3D", true, false).OfType<Node3D>().ToArray();
            string Where(Node3D nozzle) => nozzle.Name.ToString()["JumpJet_".Length..];
            var placed = nozzles.Length == jetLocations.Length && jetLocations.All(location => nozzles.Count(nozzle => Where(nozzle) == location.ToString()
                && model.Locations[location].IsAncestorOf(nozzle) && nozzle.Visible) == 1);
            var problems = new List<string>();
            foreach (var nozzle in nozzles)
            {
                var location = Enum.Parse<MechLocation>(Where(nozzle));
                var plume = nozzle.FindChild("Plume", true, false) as MeshInstance3D;
                var plumeCenter = plume is null ? nozzle.GlobalPosition : plume.GlobalTransform * plume.Mesh.GetAabb().GetCenter();
                if (plumeCenter.Y >= nozzle.GlobalPosition.Y - .05f * model.Scale.Y) problems.Add($"{location} plume not below nozzle");
                if (location is MechLocation.LeftLeg or MechLocation.RightLeg)
                { if (nozzle.GlobalPosition.Y > ankles.Max(ankle => ankle.GlobalPosition.Y)) problems.Add($"{location} nozzle above ankles"); }
                // Local forward is -Z: the back of the torso faces +Z in model space.
                else if (model.ToLocal(nozzle.GlobalPosition).Z <= model.ToLocal(model.LocationCenter(location)).Z) problems.Add($"{location} nozzle not behind its torso");
            }
            Check(placed && problems.Count == 0,
                $"{spec.Chassis} fires jump jets from {string.Join(",", jetLocations)}: {string.Join(",", nozzles.Select(nozzle => nozzle.Name))} {string.Join("; ", problems)}");
            var cut = jetLocations[0];
            model.SetJetting(MechModel.JetStrength(.1f), 1, location => location == cut ? 0 : 99);
            Check(nozzles.All(nozzle => nozzle.Visible == (Where(nozzle) != cut.ToString())), $"{spec.Chassis} {cut} goes dark with its jets lost");
            model.SetJetting(0, 1);
            Check(nozzles.All(nozzle => !nozzle.Visible) && MechModel.JetStrength(1) == 0, $"{spec.Chassis} jets cut out on landing");
            model.QueueFree();
        }
        await CaptureJumpJets();
    }

    /// <summary>Rendered review (`scripts/mech capture mech-visuals`): torso jets (Jenner) and leg jets (Panther) at takeoff, from behind.</summary>
    private async Task CaptureJumpJets()
    {
        if (!_screenshots || DisplayServer.GetName() == "headless") return;
        var layer = new CanvasLayer { Layer = 100 }; AddChild(layer);
        var container = new SubViewportContainer { Stretch = true }; container.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(container);
        var view = new SubViewport { OwnWorld3D = true }; container.AddChild(view);
        view.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new("1d2226"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new("8a9098") } });
        view.AddChild(new DirectionalLight3D { RotationDegrees = new(-50, 30, 0) });
        foreach (var id in new[] { "jenner", "panther" })
        {
            var model = MechModel.Create(DraftCatalog.Mech(id), CombatSide.Friendly); view.AddChild(model);
            model.AnimateJump(.15f); model.SetJetting(MechModel.JetStrength(.15f), 1.3);
            var height = model.Height;
            view.AddChild(new Camera3D { Name = "JumpCamera", Current = true, Position = new(height * .55f, height * .45f, height * 1.35f) });
            var camera = view.GetNode<Camera3D>("JumpCamera"); camera.LookAt(new(0, height * .3f, 0));
            await Frames(); await Capture($"jump-jets-{id}");
            model.QueueFree(); camera.Name = "Done"; camera.QueueFree(); await Frames();
        }
        layer.QueueFree();
    }
}
