using Godot;

namespace Mech;

public partial class MechModel
{
    // Stride amplitude in metres per m/s of gait speed, so every chassis shares the Marauder's step rate.
    private const float StrideSeconds = .4383f;
    internal sealed record GaitProfile(float WalkLift = .075f, float RunLift = .11f, float BodyDrop = .035f, float RunDrop = .012f,
        float Bob = .010f, float Sway = .016f, float Lean = .035f, float Roll = .008f,
        float LeftArmSwing = .045f, float RightArmSwing = .045f);
    // Joint positions are model-local, on the anatomical right; the left side mirrors X.
    internal sealed record RigProfile(float Waist, Vector3 Hip, Vector3 Knee, Vector3 Ankle, Vector3 Shoulder, GaitProfile Gait);
    // Per-chassis profiles are generated into PreparedMechModels.cs from model manifests and Data/mech-gaits.json.
    private sealed record Leg(Node3D Hip, Node3D Knee, Node3D Ankle, Vector3 Rest,
        Vector3 KneeRest, Vector3 AnkleRest, int Side);
    private readonly List<Leg> _legs = [];
    private readonly List<Node3D> _shoulders = [];
    // Each arm's rest angle from hanging straight down (positive points forward), from its geometry around the shoulder.
    private readonly List<float> _armRest = [];
    private Node3D? _body;
    private float _torsoTwist;
    public void SetTorsoTwist(float radians)
    {
        _torsoTwist = Math.Clamp(radians, -CombatRules.MaxTorsoTwist, CombatRules.MaxTorsoTwist);
        if (_body is not null) _body.Rotation = new(_body.Rotation.X, -_torsoTwist, _body.Rotation.Z);
    }
    private RigProfile? _rig;
    public bool HasLocomotion => _rig is not null;
    private double _stridePhase;
    // Longest stride amplitude (local units) whose stance poses stay within leg reach.
    private float _walkReach, _runReach;
    private float _motionBlend;
    public float RunBlend { get; private set; }
    /// <summary>Feet planted so far: one each time a leg's swing ends and its stance begins.</summary>
    public int Footfalls { get; private set; }
    private readonly List<float> _lastStep = [];

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
            var shoulder = Pivot(arm, "ShoulderPivot", Mirror(rig.Shoulder));
            _shoulders.Add(shoulder);
            var reach = shoulder.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Select(mesh => Relative(mesh, shoulder) * mesh.Mesh.GetAabb().GetCenter())
                .Aggregate(Vector3.Zero, (sum, center) => sum + center);
            _armRest.Add(reach == Vector3.Zero ? 0 : Mathf.Atan2(-reach.Z, -reach.Y));
        }
        var thigh = rig.Knee - rig.Hip; var shin = rig.Ankle - rig.Knee;
        var length = new Vector2(thigh.Y, thigh.Z).Length() + new Vector2(shin.Y, shin.Z).Length() - .002f;
        float Reach(float drop)
        {
            var height = Math.Abs(thigh.Y + shin.Y) - drop + rig.Gait.Bob;
            return Mathf.Sqrt(Math.Max(0, length * length - height * height)) - Math.Abs(thigh.Z + shin.Z);
        }
        _walkReach = Reach(rig.Gait.BodyDrop); _runReach = Reach(rig.Gait.BodyDrop + rig.Gait.RunDrop);
    }
    /// <summary>Stride amplitude in local units: proportional to gait speed, capped at leg reach.</summary>
    internal float StrideAmplitude(float speed, bool running) =>
        Math.Min(StrideSeconds * speed / Scale.X, running ? _runReach : _walkReach);

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
        var stride = Mathf.Lerp(StrideAmplitude(walk, false), StrideAmplitude(Math.Max(walk, runningSpeed), true), RunBlend);
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
        _body.Rotation = new(-gait.Lean * RunBlend * _motionBlend, -_torsoTwist, -wave * gait.Roll * _motionBlend);
        while (_lastStep.Count < _legs.Count) _lastStep.Add(-1);
        for (var index = 0; index < _legs.Count; index++)
        {
            var leg = _legs[index];
            var step = (phase + (leg.Side > 0 ? .5f : 0)) % 1;
            // The step wraps from swing back to stance as the foot comes down, in either direction of travel.
            if (_lastStep[index] >= 0 && Math.Abs(step - _lastStep[index]) > .5f && _motionBlend > .3f) Footfalls++;
            _lastStep[index] = step;
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
    /// <summary>A surrendered mech's pose: arms hanging straight down and the body leaning slightly forward, powered down.</summary>
    public void PoseSurrender()
    {
        if (_body is not null) _body.Rotation = new(-.12f, -_torsoTwist, 0);
        for (var i = 0; i < _shoulders.Count; i++) _shoulders[i].Rotation = new(-_armRest[i], 0, 0);
    }
    /// <summary>Jump pose for flight progress 0–1: spring from a crouch, tuck the legs through the apex, then reach for the ground.</summary>
    public void AnimateJump(float phase)
    {
        _motionBlend = RunBlend = 0;
        PoseJump(phase < .12f ? 1 - phase / .12f : 0, Mathf.SmoothStep(.05f, .3f, phase) * (1 - Mathf.SmoothStep(.7f, .97f, phase)));
    }
    /// <summary>A brief absorbing crouch after touchdown, over the ground pose for this many seconds since landing.</summary>
    public void AnimateLanding(float age)
    {
        if (age is >= 0 and < LandingSeconds) PoseJump(Mathf.Sin(Mathf.Pi * age / LandingSeconds), 0);
    }
    public const float LandingSeconds = .45f;
    private void PoseJump(float crouch, float tuck)
    {
        if (_body is null || _rig is null) return;
        var gait = _rig.Gait;
        var drop = gait.BodyDrop * 2.5f * crouch;
        _body.Position = new(0, _rig.Waist - drop, 0);
        _body.Rotation = new(-gait.Lean * (crouch + tuck), -_torsoTwist, 0);
        foreach (var leg in _legs)
        {
            var length = leg.KneeRest.Length() + leg.AnkleRest.Length();
            leg.Hip.Position = leg.Rest + new Vector3(0, -drop, 0);
            // Planted feet stay on the ground through a crouch; tucked feet draw up toward the hips.
            PoseLeg(leg, leg.KneeRest.Y + leg.AnkleRest.Y + drop + tuck * length * .3f, leg.KneeRest.Z + leg.AnkleRest.Z - tuck * length * .08f);
        }
        foreach (var shoulder in _shoulders) shoulder.Rotation = new(-.25f * tuck, 0, 0);
    }
    /// <summary>Root placement for a fallen or rising mech, in model-local units: a pitch about X (negative leans forward)
    /// and an offset applied after it.</summary>
    public readonly record struct Posture(float Pitch, Vector3 Offset);
    /// <summary>A knocked-down pose: world angles in degrees for the torso (the root pitch), thighs, shins and feet, and the
    /// arms' angle from the torso. Limb angles point a limb down for 0 and forward for positive; <see cref="Rest"/> holds the rig's own
    /// leg angles and <see cref="FromRest"/> adds the thigh and shin angles to them. Shin and thigh angles are written for forward
    /// knees and mirrored for rearward ones.</summary>
    private readonly record struct ProneKey(float At, float Torso, float Thigh, float Shin, float Foot, float Arms, bool Rest = false,
        bool FromRest = false);
    private static readonly ProneKey[] StandUpKeys =
    [
        new(0, -90, 0, 0, 0, 0, Rest: true),          // lying face down
        new(.14f, -84, 0, 0, 0, 84, Rest: true),      // arms braced against the ground
        new(.38f, -48, 8, -90, -70, 48),              // pushed up onto the knees, toes dug in
        new(.68f, -18, 50, -32, 0, 46, FromRest: true), // feet planted in a deep crouch, arms out for balance
        new(1, 0, 0, 0, 0, 0, Rest: true),            // standing
    ];
    /// <summary>Lying face down, as a fall ends: the first stand-up pose.</summary>
    public Posture PoseProne(float fall = 1)
    {
        return Ground(PoseStandUp(0) * Math.Clamp(fall, 0, 1));
    }
    /// <summary>Getting up for rise progress 0–1: brace on the arms, push up onto the knees, plant the feet in a crouch and
    /// straighten. The feet stay on the unit's position and the lowest part of the mech rests on the ground.</summary>
    public Posture AnimateStandUp(float progress) => Ground(PoseStandUp(Math.Clamp(progress, 0, 1)));
    /// <summary>Poses the joints for stand-up progress and returns the torso's pitch.</summary>
    private float PoseStandUp(float progress)
    {
        _motionBlend = RunBlend = 0;
        var next = Array.FindIndex(StandUpKeys, key => key.At >= progress);
        var from = StandUpKeys[Math.Max(0, next - 1)]; var to = StandUpKeys[Math.Max(0, next)];
        var t = to.At > from.At ? Mathf.SmoothStep(0, 1, (progress - from.At) / (to.At - from.At)) : 1;
        var torso = Mathf.DegToRad(Mathf.Lerp(from.Torso, to.Torso, t));
        if (_body is null || _rig is null) return torso;
        _body.Position = new(0, _rig.Waist, 0);
        _body.Rotation = new(0, -_torsoTwist, 0);
        foreach (var leg in _legs)
        {
            var restHip = Mathf.Atan2(-leg.KneeRest.Z, -leg.KneeRest.Y);
            var restShin = Mathf.Atan2(-leg.AnkleRest.Z, -leg.AnkleRest.Y);
            // Forward knees bend the shin back (bend −1); the Catapult's rearward knees mirror every leg angle.
            var mirror = restShin >= restHip ? -1 : 1;
            // Model-frame thigh and shin angles and the foot's world tilt for one key.
            (float Thigh, float Shin, float Foot) Angles(ProneKey key)
            {
                var pitch = Mathf.DegToRad(key.Torso);
                if (key.Rest) return (restHip, restShin, pitch);
                // Rearward knees fold half as far from rest, or the mech sits back on its heels.
                var (baseThigh, baseShin, depth) = key.FromRest ? (restHip, restShin, mirror < 0 ? .5f : 1) : (0, 0, 1);
                return (baseThigh + Mathf.DegToRad(key.Thigh * mirror * depth) - pitch, baseShin + Mathf.DegToRad(key.Shin * mirror * depth) - pitch,
                    Mathf.DegToRad(key.Foot));
            }
            var (a, b) = (Angles(from), Angles(to));
            var thigh = Mathf.Lerp(a.Thigh, b.Thigh, t); var shin = Mathf.Lerp(a.Shin, b.Shin, t); var foot = Mathf.Lerp(a.Foot, b.Foot, t);
            leg.Hip.Position = leg.Rest;
            leg.Hip.Rotation = new(thigh - restHip, 0, 0);
            leg.Knee.Rotation = new(shin - thigh - (restShin - restHip), 0, 0);
            leg.Ankle.Rotation = new(foot - torso - (shin - restShin), 0, 0);
        }
        // Arms aim relative to the torso whatever their rest pose (the Wolverine's cannon arm rests pointing forward), then
        // settle back to rest as the mech stands.
        for (var i = 0; i < _shoulders.Count; i++)
        {
            float Arm(ProneKey key) => key.At >= 1 ? 0 : Mathf.DegToRad(key.Arms) - _armRest[i];
            _shoulders[i].Rotation = new(Mathf.Lerp(Arm(from), Arm(to), t), 0, 0);
        }
        return torso;
    }
    /// <summary>Places the current pose at <paramref name="pitch"/>: the lowest visible part sits as low as it does standing and
    /// the ankles stay where they stand, so the mech gets up where it fell.</summary>
    private Posture Ground(float pitch)
    {
        var basis = new Basis(Vector3.Right, pitch);
        var lowest = LowestPoint(new Transform3D(basis, Vector3.Zero)) ?? RestBounds.Position.Y;
        if (_rig is null) return new(pitch, new Vector3(0, RestBounds.Position.Y - lowest, 0));
        var ankles = _legs.Aggregate(Vector3.Zero, (sum, leg) => sum + basis * Relative(leg.Ankle).Origin) / _legs.Count;
        return new(pitch, new Vector3(0, RestBounds.Position.Y - lowest, _rig.Ankle.Z - ankles.Z));
    }
    // Up to this many vertices per mesh decide where a fallen mech touches the ground; box corners would float tilted limbs.
    private const int ContactSamples = 384;
    private readonly Dictionary<Mesh, Vector3[]> _contactPoints = [];
    /// <summary>The lowest visible vertex under <paramref name="root"/>, sampled from each mesh and leaving out jump jets.</summary>
    private float? LowestPoint(Transform3D root)
    {
        float? lowest = null;
        void Visit(Node3D node, Transform3D transform)
        {
            if (!node.Visible || _nozzleRoots.Contains(node)) return;
            if (node is MeshInstance3D { Mesh: { } mesh })
                foreach (var point in ContactPoints(mesh))
                {
                    var y = (transform * point).Y;
                    if (lowest is not { } low || y < low) lowest = y;
                }
            foreach (var child in node.GetChildren().OfType<Node3D>()) Visit(child, transform * child.Transform);
        }
        foreach (var child in GetChildren().OfType<Node3D>()) Visit(child, root * child.Transform);
        return lowest;
    }
    /// <summary>A mesh's vertices, thinned evenly to <see cref="ContactSamples"/> but always keeping the extremes on each axis.</summary>
    private Vector3[] ContactPoints(Mesh mesh)
    {
        if (_contactPoints.TryGetValue(mesh, out var cached)) return cached;
        var vertices = mesh.GetFaces().Distinct().ToArray();
        var step = Math.Max(1, vertices.Length / ContactSamples);
        var points = vertices.Where((_, index) => index % step == 0).ToList();
        if (vertices.Length > 0)
            for (var axis = 0; axis < 3; axis++)
            { points.Add(vertices.MinBy(vertex => vertex[axis])); points.Add(vertices.MaxBy(vertex => vertex[axis])); }
        return _contactPoints[mesh] = [.. points];
    }
    /// <summary>A node's transform relative to an ancestor (the model's unscaled local frame by default), without needing the
    /// scene tree.</summary>
    private Transform3D Relative(Node3D node, Node? ancestor = null)
    {
        ancestor ??= this;
        var transform = node.Transform;
        for (var parent = node.GetParentOrNull<Node3D>(); parent is not null && parent != ancestor; parent = parent.GetParentOrNull<Node3D>())
            transform = parent.Transform * transform;
        return transform;
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
