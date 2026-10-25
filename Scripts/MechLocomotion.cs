using Godot;

namespace Mech;

public partial class MechModel
{
    private sealed record Leg(Node3D Hip, Node3D Knee, Node3D Ankle, Vector3 Rest, int Side);
    private readonly List<Leg> _legs = [];
    private readonly List<Node3D> _shoulders = [];
    private Node3D? _body;
    private double _stridePhase;
    private float _motionBlend;
    public float RunBlend { get; private set; }

    private static Node3D Pivot(Node3D parent, string name, Vector3 at, Func<Node3D, bool>? include = null)
    {
        var children = parent.GetChildren().OfType<Node3D>().Where(child => include?.Invoke(child) ?? true).ToArray();
        var pivot = new Node3D { Name = name, Position = at }; parent.AddChild(pivot);
        foreach (var child in children) { child.Reparent(pivot, false); child.Position -= at; }
        return pivot;
    }
    private void BuildLocomotionRig()
    {
        _body = new Node3D { Name = "BodyMotion", Position = new(0, 1.3f, 0) }; AddChild(_body);
        foreach (var (location, root) in _locations)
            if (location is not (MechLocation.LeftLeg or MechLocation.RightLeg))
            { root.Reparent(_body, false); root.Position = new(0, -1.3f, 0); }
        foreach (var sign in new[] { -1, 1 })
        {
            var leg = _locations[sign < 0 ? MechLocation.LeftLeg : MechLocation.RightLeg];
            var hip = Pivot(leg, "HipPivot", new(sign * .27f, 1.2f, .025f));
            var knee = Pivot(hip, "KneePivot", new(sign * .08f, -.35f, -.04f),
                child => child.Name != "Hip" && !child.Name.ToString().StartsWith("Thigh", StringComparison.Ordinal));
            var ankle = Pivot(knee, "AnklePivot", new(sign * .01f, -.60f, .015f),
                child => child.Name == "Foot" || child.Name == "ToeCap" || child.Name == "Ankle");
            _legs.Add(new(hip, knee, ankle, hip.Position, sign));
            var arm = _locations[sign < 0 ? MechLocation.LeftArm : MechLocation.RightArm];
            _shoulders.Add(Pivot(arm, "ShoulderPivot", new(sign * .66f, 2.12f, .015f)));
        }
    }

    /// <summary>Shared hangar/combat pose. Speed is signed world units/sec; turning is radians/sec.
    /// Foot cadence follows distance, with overlapping planted phases and two-link leg articulation.</summary>
    public void AnimateLocomotion(float delta, float speed, float walkingSpeed, float runningSpeed, float turning = 0)
    {
        if (_body is null || delta <= 0) return;
        var walk = Math.Max(.001f, walkingSpeed);
        var moving = Math.Abs(speed) > .0001f;
        var pivoting = Math.Clamp(Math.Abs(turning) / Mathf.DegToRad(45), 0, 1)
            * (1 - Math.Clamp(Math.Abs(speed) / (walk * .5f), 0, 1));
        var targetBlend = Math.Max(Math.Clamp(Math.Abs(speed) / (walk * .35f), 0, 1), pivoting);
        var smoothing = 1 - Mathf.Exp(-delta * 10);
        _motionBlend = Mathf.Lerp(_motionBlend, targetBlend, smoothing);
        RunBlend = Mathf.Lerp(RunBlend, Math.Clamp((Math.Abs(speed) - walk) / Math.Max(.001f, runningSpeed - walk), 0, 1), smoothing);
        var stride = Mathf.Lerp(.11f, .15f, RunBlend);
        var stance = Mathf.Lerp(.65f, .60f, RunBlend); // Always at least one planted foot.
        var cycles = speed / (2 * stride * Scale.X / stance);
        if (!moving) cycles = pivoting * .5f;
        _stridePhase = (_stridePhase + cycles * delta) % 1;
        if (_stridePhase < 0) _stridePhase += 1;
        var phase = (float)_stridePhase;
        var wave = Mathf.Sin(phase * Mathf.Tau);
        var bob = (-.035f - RunBlend * .012f + .010f * Mathf.Cos(phase * Mathf.Tau * 2)) * _motionBlend;
        var sway = wave * .016f * _motionBlend;
        _body.Position = new(sway, 1.3f + bob, 0);
        _body.Rotation = new(-.035f * RunBlend * _motionBlend, 0, -wave * .008f * _motionBlend);
        foreach (var leg in _legs)
        {
            var step = (phase + (leg.Side > 0 ? .5f : 0)) % 1;
            var swing = Math.Clamp((step - stance) / (1 - stance), 0, 1);
            var reach = step < stance ? -1 + 2 * step / stance
                : 1 - 2 * Mathf.SmoothStep(0, 1, swing);
            var lift = Mathf.Pow(Mathf.Sin(swing * Mathf.Pi), 2) * Mathf.Lerp(.075f, .11f, RunBlend) * _motionBlend;
            var stepLength = moving ? stride : .065f * Math.Sign(turning) * leg.Side;
            var z = reach * stepLength * _motionBlend;
            leg.Hip.Position = leg.Rest + new Vector3(sway, bob, 0);
            PoseLeg(leg, -.95f + lift - bob, -.025f + z);
            leg.Ankle.Rotation = new(leg.Ankle.Rotation.X,
                pivoting * Math.Sign(turning) * Mathf.Sin(step * Mathf.Tau) * .12f * _motionBlend, 0);
        }
        for (var i = 0; i < _shoulders.Count; i++)
            _shoulders[i].Rotation = new(wave * (i == 0 ? -1 : 1) * .045f * _motionBlend, 0, 0);
    }
    private static void PoseLeg(Leg leg, float y, float z)
    {
        var thigh = new Vector2(.35f, .04f).Length();
        var shin = new Vector2(.60f, .015f).Length();
        var distance = Math.Clamp(new Vector2(y, z).Length(), Math.Abs(shin - thigh) + .001f, thigh + shin - .0001f);
        var hip = Mathf.Atan2(-z, -y) + Mathf.Acos(Math.Clamp((thigh * thigh + distance * distance - shin * shin)
            / (2 * thigh * distance), -1, 1));
        var knee = -Mathf.Acos(Math.Clamp((distance * distance - thigh * thigh - shin * shin) / (2 * thigh * shin), -1, 1));
        var restHip = Mathf.Atan2(.04f, .35f);
        var restShin = Mathf.Atan2(-.015f, .60f);
        leg.Hip.Rotation = new(hip - restHip, 0, 0);
        leg.Knee.Rotation = new(knee - (restShin - restHip), 0, 0);
        leg.Ankle.Rotation = new(-leg.Hip.Rotation.X - leg.Knee.Rotation.X, 0, 0);
    }
}
