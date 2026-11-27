using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Tuned running gaits: the Locust lopes on one planted foot with a landing dip; untuned runs keep double support.</summary>
    private async Task TestRunningGaits()
    {
        var locust = Gait("locust", MovementMode.Run);
        GD.Print($"Locust run: single support {locust.Single:P1}, airborne {locust.Airborne:P1}, {locust.FootfallRate:F2} footfalls/s, "
            + $"footprints {locust.Spacing:F1} m apart, bounce {locust.Bounce:F3}, footfall dip {locust.FootfallDip:P0}");
        Check(locust.Airborne == 0 && locust.Single >= .95f, $"a running Locust has exactly one foot planted almost all the time ({locust.Single:P1})");
        Check(Math.Abs(locust.FootfallRate - locust.ExpectedRate) <= .05f * locust.ExpectedRate,
            $"a running Locust steps at its half-stride stance cadence ({locust.FootfallRate:F2} of {locust.ExpectedRate:F2} footfalls/s)");
        Check(locust.Spacing >= .6f * locust.Height, $"a running Locust's footprints land long strides apart ({locust.Spacing:F1} m, {locust.Spacing / locust.Height:P0} of its height)");
        Check(locust.Bounce >= .04f && locust.FootfallDip <= .1f,
            $"a running Locust's body dips as each foot lands and rises between footfalls (bounce {locust.Bounce:F3}, dip {locust.FootfallDip:P0})");
        var walk = Gait("locust", MovementMode.Walk);
        Check(walk.Double >= .25f && walk.Bounce < .03f, $"a walking Locust keeps its overlapping planted steps ({walk.Double:P1} double support)");
        var jenner = Gait("jenner", MovementMode.Run);
        Check(jenner.Double >= .15f, $"untuned runs keep their overlapping planted steps (Jenner {jenner.Double:P1} double support)");
        await Frames();
    }

    private sealed record GaitMeasure(float Single, float Double, float Airborne, float FootfallRate, float ExpectedRate, float Spacing,
        float Bounce, float FootfallDip, float Height);

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
        var heights = new List<float>(); var landings = new List<float>();
        for (var sample = 0; sample < samples; sample++)
        {
            var before = model.Footfalls;
            model.AnimateLocomotion(delta, speed, walk, run);
            var grounded = feet.Count(foot => Math.Abs(Enumerable.Range(0, 8).Min(i => (foot.GlobalTransform * foot.Mesh.GetAabb().GetEndpoint(i)).Y)) < .002f * CombatRules.BodyScale);
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
        return new(single / (float)samples, both / (float)samples, none / (float)samples, rate, expected, speed / rate, high - low, dip, height);
    }
}
