using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Running gaits: fast runs leave the ground between steps rather than step faster than their cap, slower runs and
    /// every walk keep overlapping planted steps, and no foot sinks into the ground.</summary>
    private async Task TestRunningGaits()
    {
        foreach (var id in new[] { "locust", "jenner", "spider", "cicada" })
        {
            var run = Gait(id, MovementMode.Run);
            GD.Print($"{id} flight run: {run.FootfallRate:F2} footfalls/s, footprints {run.Spacing:F1} m apart, single support {run.Single:P1}, "
                + $"double {run.Double:P1}, airborne {run.Airborne:P1}, bounce {run.Bounce:F3}, lowest foot {run.Sink:F3} m");
            Check(Math.Abs(run.FootfallRate - 4.5f) <= .05f * 4.5f, $"a running {id} steps no faster than its 4.5 footfalls/s cap ({run.FootfallRate:F2})");
            Check(run.Airborne >= .05f && run.Double == 0, $"a running {id} leaves the ground between steps ({run.Airborne:P1} airborne)");
            Check(run.Sink >= -.01f, $"a running {id}'s feet never sink into the ground ({run.Sink:F3} m)");
        }
        foreach (var id in new[] { "atlas", "marauder", "griffin" })
        {
            var run = Gait(id, MovementMode.Run);
            Check(run.Airborne == 0 && run.Double >= .15f && run.FootfallRate < 4.5f,
                $"a running {id}, below the cap, keeps its overlapping planted steps ({run.FootfallRate:F2} footfalls/s, {run.Double:P1} double support)");
        }
        var walk = Gait("locust", MovementMode.Walk);
        Check(walk.Airborne == 0 && walk.Double >= .15f && walk.Sink >= -.01f,
            $"a walking Locust keeps its overlapping planted steps ({walk.Double:P1} double support)");
        await Frames();
    }

    private sealed record GaitMeasure(float Single, float Double, float Airborne, float FootfallRate, float ExpectedRate, float Spacing,
        float Bounce, float FootfallDip, float Height, float Sink);

    /// <summary>Runs a chassis at a steady walk or run and measures foot support, cadence and body bounce at 1/600 s.</summary>
    private GaitMeasure Gait(string id, MovementMode mode)
    {
        var spec = DraftCatalog.Mech(id);
        var model = MechModel.Create(spec, CombatSide.Friendly); AddChild(model);
        var feet = model.FindChildren("Foot", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        var body = (Node3D)model.FindChild("BodyMotion", true, false);
        var walk = CombatRules.MovementSpeed(spec, MovementMode.Walk); var run = CombatRules.MovementSpeed(spec, MovementMode.Run);
        var speed = mode == MovementMode.Run ? run : walk;
        const float delta = 1f / 600; const int samples = 6000;
        for (var sample = 0; sample < 1200; sample++) model.AnimateLocomotion(delta, speed, walk, run);
        int single = 0, both = 0, none = 0; var footfalls = model.Footfalls;
        var heights = new List<float>(); var landings = new List<float>(); var sink = 0f;
        for (var sample = 0; sample < samples; sample++)
        {
            var before = model.Footfalls;
            model.AnimateLocomotion(delta, speed, walk, run);
            var bottoms = feet.Select(foot => Enumerable.Range(0, 8).Min(i => (foot.GlobalTransform * foot.Mesh.GetAabb().GetEndpoint(i)).Y)).ToArray();
            var grounded = bottoms.Count(bottom => Math.Abs(bottom) < .002f * CombatRules.BodyScale);
            sink = Math.Min(sink, bottoms.Min());
            if (grounded == 1) single++; else if (grounded == 0) none++; else both++;
            heights.Add(body.Position.Y);
            if (model.Footfalls != before) landings.Add(body.Position.Y);
        }
        var rate = (model.Footfalls - footfalls) / (samples * delta);
        var stance = mode == MovementMode.Run ? .5f : .65f;
        var expected = speed * stance / (model.StrideAmplitude(speed, mode == MovementMode.Run) * model.Scale.X);
        var (low, high) = (heights.Min(), heights.Max());
        var dip = landings.Count == 0 || high <= low ? 1 : landings.Max(height => (height - low) / (high - low));
        var height = model.Height;
        model.QueueFree();
        return new(single / (float)samples, both / (float)samples, none / (float)samples, rate, expected, speed / rate, high - low, dip, height, sink);
    }
}
