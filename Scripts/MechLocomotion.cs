using Godot;

namespace Mech;

public partial class MechModel
{
    // Author profiles at their baseline stride; every present and future rig gets this scale once.
    private const float StrideScale = 1.5f;
    private sealed record GaitProfile(float WalkStride = .11f, float RunStride = .15f,
        float WalkLift = .075f, float RunLift = .11f, float BodyDrop = .035f, float RunDrop = .012f,
        float Bob = .010f, float Sway = .016f, float Lean = .035f, float Roll = .008f,
        float LeftArmSwing = .045f, float RightArmSwing = .045f);
    // Joint positions are model-local, on the anatomical right; the left side mirrors X.
    private sealed record RigProfile(float Waist, Vector3 Hip, Vector3 Knee, Vector3 Ankle, Vector3 Shoulder, GaitProfile Gait);
    // Source joint positions from Assets/Models/Firestarter.json.
    private static readonly RigProfile FirestarterRig = new(1.3047417f, new(0.2858072f, 1.1469318f, -0.0000100f), new(0.3097583f, 0.6324679f, -0.1360211f),
        new(0.3335240f, 0.1596391f, 0.0589678f), new(0.5360900f, 1.6528519f, 0.0000736f), new(BodyDrop: .055f, RunDrop: .035f, LeftArmSwing: .035f, RightArmSwing: .035f));
    // Source joint positions from Assets/Models/Crusader.json.
    private static readonly RigProfile CrusaderRig = new(1.5367060f, new(0.2905874f, 1.3825977f, -0.0461143f), new(0.3098342f, 0.9544584f, -0.1518857f),
        new(0.3454441f, 0.1523560f, 0.0122720f), new(0.5419724f, 2.0107628f, -0.0375151f), new(BodyDrop: .055f, RunDrop: .035f, LeftArmSwing: .035f, RightArmSwing: .035f));
    // Source joint positions from Assets/Models/Thunderbolt.json.
    private static readonly RigProfile ThunderboltRig = new(1.4473573f, new(0.3542544f, 1.2355838f, 0.0000010f), new(0.3762942f, 0.7157638f, -0.1405929f),
        new(0.3968374f, 0.2279362f, -0.0842779f), new(0.6437776f, 1.8978896f, -0.0302165f), new(BodyDrop: .055f, RunDrop: .035f, LeftArmSwing: .035f, RightArmSwing: .035f));
    // Source joint positions from Assets/Models/Victor.json.
    private static readonly RigProfile VictorRig = new(1.5629110f, new(0.3659215f, 1.3598375f, 0.0000011f), new(0.3914366f, 0.7741040f, -0.0725389f),
        new(0.4200974f, 0.1253429f, -0.0041993f), new(0.7332161f, 2.1533539f, 0.0287380f), new(BodyDrop: .075f, RunDrop: .04f, LeftArmSwing: .025f, RightArmSwing: .025f));
    // Source joint positions from Assets/Models/Zeus.json.
    private static readonly RigProfile ZeusRig = new(1.4407788f, new(0.3570305f, 1.1963000f, 0.0000009f), new(0.3783944f, 0.6754574f, -0.0539775f),
        new(0.3929816f, 0.3399147f, 0.0723574f), new(0.6083183f, 2.0827619f, 0.1214395f), new(BodyDrop: .075f, RunDrop: .04f, LeftArmSwing: .025f, RightArmSwing: .025f));
    // Source joint positions from Assets/Models/Panther.json.
    private static readonly RigProfile PantherRig = new(1.2764309f, new(0.2603224f, 1.0651958f, 0.0008271f), new(0.2802623f, 0.7261253f, -0.0897736f),
        new(0.3116038f, 0.1838667f, 0.0217271f), new(0.4515714f, 1.6494597f, 0.0194916f), new(BodyDrop: .055f, RunDrop: .025f, LeftArmSwing: .10f, RightArmSwing: .02f));
    // Source joint positions from Assets/Models/Shadowhawk.json.
    private static readonly RigProfile ShadowhawkRig = new(1.6390153f, new(0.3245066f, 1.3442132f, -0.0000000f), new(0.3522708f, 0.8864410f, -0.1821765f),
        new(0.3949963f, 0.2013487f, -0.0583009f), new(0.5898179f, 2.0852223f, 0.0277833f), new(BodyDrop: .045f, RunDrop: .02f, LeftArmSwing: .08f, RightArmSwing: .04f));
    // Source joint positions from Assets/Models/Griffin.json.
    private static readonly RigProfile GriffinRig = new(1.4483533f, new(0.3248251f, 1.2878901f, -0.0112379f), new(0.3594102f, 0.7686440f, -0.0977600f),
        new(0.3948095f, 0.2365958f, -0.0388364f), new(0.5648870f, 1.9105557f, -0.0044532f), new(BodyDrop: .055f, RunDrop: .025f, LeftArmSwing: .10f, RightArmSwing: .02f));
    // Source bone pivots from Assets/Models/Atlas.json, after grounding and scaling.
    private static readonly RigProfile AtlasRig = new(1.5606572f, new(.4203329f, 1.2878044f, .000007f), new(.4796787f, .6984711f, -.0301068f),
        new(.5271229f, .1744765f, .0221241f), new(.7591423f, 2.2646871f, .1932315f),
        new(BodyDrop: .05f, RunDrop: .025f, LeftArmSwing: .06f, RightArmSwing: .06f));
    // Source bone pivots from Assets/Models/Awesome.json, after grounding and scaling.
    private static readonly RigProfile AwesomeRig = new(1.333676f, new(0.4883256f, 1.2504348f, -0.0042566f), new(0.4969903f, 0.8082481f, -0.2179863f),
        new(0.5206548f, 0.2005744f, 0.0550997f), new(0.6977796f, 2.2978666f, 0.1752429f), new());
    // Source bone pivots from Assets/Models/Jenner.json, after grounding and scaling.
    private static readonly RigProfile JennerRig = new(1.2360389f, new(0.4022371f, 1.1043084f, 1e-06f), new(0.4022835f, 0.7587754f, -0.0823296f),
        new(0.4030017f, 0.1462707f, 0.0992548f), new(0.4734214f, 1.5326102f, 0.2385821f),
        new(WalkStride: .15f, RunStride: .19f, WalkLift: .065f, RunLift: .095f, BodyDrop: .030f, RunDrop: .015f,
            Bob: .008f, Sway: .014f, Lean: .045f, Roll: .007f, LeftArmSwing: .015f, RightArmSwing: .015f));
    // Source bone pivots from Assets/Models/Wolverine.json, after grounding and scaling.
    // Slightly lower the moving body so the 1.5x stride stays within the source leg reach.
    private static readonly RigProfile WolverineRig = new(1.4685936f, new(.2714745f, 1.2177287f, .0000008f), new(.2992869f, .6762444f, -.0841622f),
        new(.3205571f, .2610593f, -.0388172f), new(.5497093f, 1.7583625f, -.0106391f),
        new(WalkStride: .16f, RunStride: .21f, WalkLift: .085f, RunLift: .13f, BodyDrop: .045f, RunDrop: .025f,
            Bob: .012f, Sway: .022f, Lean: .055f, Roll: .010f, LeftArmSwing: .14f, RightArmSwing: .018f));
    // Source bone pivots from Assets/Models/Catapult.json, after grounding and scaling.
    private static readonly RigProfile CatapultRig = new(1.2674548f, new(.484757f, 1.1799883f, -.0022531f), new(.49902f, .8512305f, .3638493f),
        new(.530147f, .1569128f, -.0220576f), new(.3731738f, 1.7346633f, .2484192f),
        new(WalkStride: .14f, RunStride: .17f, WalkLift: .065f, RunLift: .095f, BodyDrop: .022f, RunDrop: .006f,
            Bob: .005f, Sway: .009f, Lean: .015f, Roll: .004f, LeftArmSwing: .012f, RightArmSwing: .012f));
    private sealed record Leg(Node3D Hip, Node3D Knee, Node3D Ankle, Vector3 Rest,
        Vector3 KneeRest, Vector3 AnkleRest, int Side);
    private readonly List<Leg> _legs = [];
    private readonly List<Node3D> _shoulders = [];
    private Node3D? _body;
    private RigProfile? _rig;
    public bool HasLocomotion => _rig is not null;
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
    private void BuildLocomotionRig(RigProfile rig)
    {
        _rig = rig;
        _body = new Node3D { Name = "BodyMotion", Position = new(0, rig.Waist, 0) }; AddChild(_body);
        foreach (var (location, root) in _locations)
            if (location is not (MechLocation.LeftLeg or MechLocation.RightLeg))
            { root.Reparent(_body, false); root.Position = new(0, -rig.Waist, 0); }
        foreach (var sign in new[] { -1, 1 })
        {
            var leg = _locations[sign < 0 ? MechLocation.LeftLeg : MechLocation.RightLeg];
            Vector3 Mirror(Vector3 point) => new(point.X * sign, point.Y, point.Z);
            var hip = Pivot(leg, "HipPivot", Mirror(rig.Hip));
            var knee = Pivot(hip, "KneePivot", Mirror(rig.Knee - rig.Hip),
                child => child.Name != "Hip" && !child.Name.ToString().StartsWith("Thigh", StringComparison.Ordinal));
            var ankle = Pivot(knee, "AnklePivot", Mirror(rig.Ankle - rig.Knee),
                child => child.Name == "Foot" || child.Name.ToString().StartsWith("ToeCap", StringComparison.Ordinal) || child.Name == "Ankle");
            _legs.Add(new(hip, knee, ankle, hip.Position, knee.Position, ankle.Position, sign));
            var arm = _locations[sign < 0 ? MechLocation.LeftArm : MechLocation.RightArm];
            _shoulders.Add(Pivot(arm, "ShoulderPivot", Mirror(rig.Shoulder)));
        }
    }

    /// <summary>Shared hangar/combat pose. Speed is signed world units/sec; turning is radians/sec.
    /// Foot cadence follows distance, with overlapping planted phases and two-link leg articulation.</summary>
    public void AnimateLocomotion(float delta, float speed, float walkingSpeed, float runningSpeed, float turning = 0)
    {
        if (_body is null || _rig is null || delta <= 0) return;
        var gait = _rig.Gait;
        var walk = Math.Max(.001f, walkingSpeed);
        var moving = Math.Abs(speed) > .0001f;
        var pivoting = Math.Clamp(Math.Abs(turning) / Mathf.DegToRad(45), 0, 1)
            * (1 - Math.Clamp(Math.Abs(speed) / (walk * .5f), 0, 1));
        var targetBlend = Math.Max(Math.Clamp(Math.Abs(speed) / (walk * .35f), 0, 1), pivoting);
        var smoothing = 1 - Mathf.Exp(-delta * 10);
        _motionBlend = Mathf.Lerp(_motionBlend, targetBlend, smoothing);
        RunBlend = Mathf.Lerp(RunBlend, Math.Clamp((Math.Abs(speed) - walk) / Math.Max(.001f, runningSpeed - walk), 0, 1), smoothing);
        var stride = Mathf.Lerp(gait.WalkStride, gait.RunStride, RunBlend) * StrideScale;
        var stance = Mathf.Lerp(.65f, .60f, RunBlend); // Always at least one planted foot.
        var cycles = speed / (2 * stride * Scale.X / stance);
        if (!moving) cycles = pivoting * .5f;
        _stridePhase = (_stridePhase + cycles * delta) % 1;
        if (_stridePhase < 0) _stridePhase += 1;
        var phase = (float)_stridePhase;
        var wave = Mathf.Sin(phase * Mathf.Tau);
        var bob = (-gait.BodyDrop - RunBlend * gait.RunDrop + gait.Bob * Mathf.Cos(phase * Mathf.Tau * 2)) * _motionBlend;
        var sway = wave * gait.Sway * _motionBlend;
        _body.Position = new(sway, _rig.Waist + bob, 0);
        _body.Rotation = new(-gait.Lean * RunBlend * _motionBlend, 0, -wave * gait.Roll * _motionBlend);
        foreach (var leg in _legs)
        {
            var step = (phase + (leg.Side > 0 ? .5f : 0)) % 1;
            var swing = Math.Clamp((step - stance) / (1 - stance), 0, 1);
            var reach = step < stance ? -1 + 2 * step / stance
                : 1 - 2 * Mathf.SmoothStep(0, 1, swing);
            var lift = Mathf.Pow(Mathf.Sin(swing * Mathf.Pi), 2) * Mathf.Lerp(gait.WalkLift, gait.RunLift, RunBlend) * _motionBlend;
            var stepLength = moving ? stride : .065f * Math.Sign(turning) * leg.Side;
            var z = reach * stepLength * _motionBlend;
            leg.Hip.Position = leg.Rest + new Vector3(sway, bob, 0);
            PoseLeg(leg, leg.KneeRest.Y + leg.AnkleRest.Y + lift - bob, leg.KneeRest.Z + leg.AnkleRest.Z + z);
            leg.Ankle.Rotation = new(leg.Ankle.Rotation.X,
                pivoting * Math.Sign(turning) * Mathf.Sin(step * Mathf.Tau) * .12f * _motionBlend, 0);
        }
        for (var i = 0; i < _shoulders.Count; i++)
            _shoulders[i].Rotation = new(wave * (i == 0 ? -gait.LeftArmSwing : gait.RightArmSwing) * _motionBlend, 0, 0);
    }
    private static void PoseLeg(Leg leg, float y, float z)
    {
        var thigh = new Vector2(leg.KneeRest.Y, leg.KneeRest.Z).Length();
        var shin = new Vector2(leg.AnkleRest.Y, leg.AnkleRest.Z).Length();
        var distance = Math.Clamp(new Vector2(y, z).Length(), Math.Abs(shin - thigh) + .001f, thigh + shin - .0001f);
        var restHip = Mathf.Atan2(-leg.KneeRest.Z, -leg.KneeRest.Y);
        var restShin = Mathf.Atan2(-leg.AnkleRest.Z, -leg.AnkleRest.Y);
        // Preserve each chassis's authored knee direction, including the Catapult's rearward bend.
        var bend = restShin >= restHip ? 1 : -1;
        var hip = Mathf.Atan2(-z, -y) - bend * Mathf.Acos(Math.Clamp((thigh * thigh + distance * distance - shin * shin)
            / (2 * thigh * distance), -1, 1));
        var knee = bend * Mathf.Acos(Math.Clamp((distance * distance - thigh * thigh - shin * shin) / (2 * thigh * shin), -1, 1));
        leg.Hip.Rotation = new(hip - restHip, 0, 0);
        leg.Knee.Rotation = new(knee - (restShin - restHip), 0, 0);
        leg.Ankle.Rotation = new(-leg.Hip.Rotation.X - leg.Knee.Rotation.X, 0, 0);
    }
}
