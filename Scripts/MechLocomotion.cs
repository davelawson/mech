using Godot;

namespace Mech;

public partial class MechModel
{
    // Stride amplitude in metres per m/s of gait speed, so every chassis shares the Marauder's step rate.
    private const float StrideSeconds = .4383f;
    internal sealed record GaitProfile(float WalkLift = .075f, float RunLift = .11f, float BodyDrop = .035f, float RunDrop = .012f,
        float Bob = .010f, float Sway = .016f, float Lean = .035f, float Roll = .008f,
        float LeftArmSwing = .045f, float RightArmSwing = .045f, float RunStance = .60f, float RunBounce = 0,
        float WalkStance = .65f, float WalkCadence = 0, float RunCadence = 0, float WalkDip = 0, float RunDip = 0, float FootRoll = 0,
        float MaxRunCadence = 0, float FlightBounce = 0)
    {
        /// <summary>A flight run leaves the ground between steps rather than step faster than <see cref="MaxRunCadence"/>.</summary>
        public bool Flight => MaxRunCadence > 0;
        /// <summary>Weighted gaits size strides from a target cadence, dip the hips through double support and roll each planted
        /// foot from heel to toe, which lets legs that stand nearly straight take longer steps.</summary>
        public bool Weighted => WalkCadence > 0 || RunCadence > 0 || WalkDip > 0 || RunDip > 0 || FootRoll > 0 || Flight;
    }
    // RunStance is each foot's planted share of a running stride (never below half, so a foot is always down).
    // RunBounce, when set, replaces the running bob: the body dips as each foot lands and rises through mid-stance.
    // WalkStance is each foot's planted share of a walking stride. WalkCadence and RunCadence are target footfalls per second,
    // reached when leg reach allows. WalkDip and RunDip lower the hips through double support, in model units; FootRoll is the
    // pitch in radians of a foot rocking on its heel as it lands and on its toes as it pushes off.
    // MaxRunCadence caps running footfalls per second: a run that would step faster shortens its stance below half, so both feet
    // leave the ground between steps. FlightBounce is how far, in model units, the body arcs up while airborne; it sinks half as far
    // at mid-stance.
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
    // A flight run's reach, for its own hip profile.
    private float _flightReach;
    // Each foot's bottom-rear (heel) and bottom-front (toe) corners, as (Y, Z) in its ankle pivot's frame, in leg order. Source
    // feet are not always mirror images, so each foot rocks about its own corners.
    private readonly record struct Sole(float HeelY, float HeelZ, float ToeY, float ToeZ);
    private readonly List<Sole> _soles = [];
    // Stance progress by which a landing foot has rocked flat off its heel, and from which it rocks onto its toes.
    private const float HeelRock = .2f, ToeRock = .55f;
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
        foreach (var leg in _legs)
        {
            var foot = leg.Ankle.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Select(mesh => Relative(mesh, leg.Ankle) * mesh.Mesh.GetAabb()).Aggregate((Aabb?)null, (sum, box) => sum?.Merge(box) ?? box);
            _soles.Add(foot is { } box ? new(box.Position.Y, box.End.Z, box.Position.Y, box.Position.Z) : default);
        }
        var gait = rig.Gait;
        if (gait.Weighted)
        {
            // Hip height for stance progress 0–1 in a walk or grounded run, and in a flight run.
            Func<float, float> Grounded(float stance, float drop, float dip) => progress =>
                -drop + gait.Bob * Mathf.Cos(progress * stance * Mathf.Tau * 2) - dip * DipCurve(progress * stance, stance);
            Func<float, float> Flying(float drop, float dip) => progress => -drop + FlightBob(progress * .5f, .5f, dip, gait.FlightBounce);
            float WeightedReach(Func<float, float> hip)
            {
                // Every planted pose of the stance, with the hip dip and foot roll at full strength, must stay within leg reach.
                bool Fits(float amplitude)
                {
                    for (var sample = 0; sample <= 32; sample++)
                    {
                        var progress = sample / 32f; var bob = hip(progress);
                        foreach (var sole in _soles)
                        {
                            var foot = PlantedFoot(sole, progress, amplitude * (2 * progress - 1), gait.FootRoll);
                            if (new Vector2(thigh.Y + shin.Y + foot.Y - bob, thigh.Z + shin.Z + foot.Z).Length() > length) return false;
                        }
                    }
                    return true;
                }
                float low = 0, high = length;
                for (var i = 0; i < 24; i++) { var mid = (low + high) / 2; if (Fits(mid)) low = mid; else high = mid; }
                return low;
            }
            _walkReach = WeightedReach(Grounded(Math.Max(.5f, gait.WalkStance), gait.BodyDrop, gait.WalkDip));
            _runReach = WeightedReach(Grounded(Math.Max(.5f, gait.RunStance), gait.BodyDrop + gait.RunDrop, gait.RunDip));
            if (gait.Flight) _flightReach = WeightedReach(Flying(gait.BodyDrop + gait.RunDrop, gait.RunDip));
            return;
        }
        _walkReach = Reach(gait.BodyDrop);
        // A running bounce is at its lowest when the feet are at the stride's extremes, so no bob shortens the running reach.
        _runReach = Reach(gait.BodyDrop + gait.RunDrop + (gait.RunBounce > 0 ? gait.Bob : 0));
    }
    /// <summary>Stride amplitude in local units, capped at leg reach: sized for the gait's target cadence when it has one, otherwise
    /// proportional to gait speed.</summary>
    internal float StrideAmplitude(float speed, bool running)
    {
        if (_rig is not { Gait: var gait }) return StrideSeconds * speed / Scale.X;
        if (!running) return Amplitude(speed, gait.WalkCadence, Math.Max(.5f, gait.WalkStance), _walkReach);
        return Flies(gait, speed) ? Amplitude(speed, gait.RunCadence, .5f, _flightReach)
            : Amplitude(speed, gait.RunCadence, Math.Max(.5f, gait.RunStance), _runReach);
    }
    // Footfalls per second are speed × stance / amplitude (in metres), so a target cadence sets the amplitude.
    private float Amplitude(float speed, float cadence, float stance, float reach) =>
        Math.Min(cadence > 0 ? speed * stance / (cadence * Scale.X) : StrideSeconds * speed / Scale.X, reach);
    /// <summary>Whether a run at this speed leaves the ground between steps: only a flight gait whose grounded run would land
    /// footfalls faster than its cap.</summary>
    private bool Flies(GaitProfile gait, float speed)
    {
        var stance = Math.Max(.5f, gait.RunStance);
        return gait.Flight && speed * stance / (Amplitude(speed, gait.RunCadence, stance, _runReach) * Scale.X) > gait.MaxRunCadence;
    }
    /// <summary>Hip dip weight for a leg's step phase: 1 in the middle of double support, when the feet are furthest apart, and 0 in
    /// mid single support, when the body vaults over the planted leg.</summary>
    private static float DipCurve(float step, float stance) => (1 + Mathf.Cos((step - (stance - .5f) / 2) * Mathf.Tau * 2)) / 2;
    /// <summary>Hip offset in a flight run, for the time since the latest footfall as a share of the stride cycle (0–0.5) and a stance
    /// of at most half: held down by the dip as each foot lands and pushes off, sinking by half the bounce at mid-stance, then arcing
    /// up by the bounce while both feet are off the ground.</summary>
    private static float FlightBob(float sinceLanding, float stance, float dip, float bounce) => sinceLanding < stance
        ? -dip - bounce / 2 * Mathf.Sin(Mathf.Pi * sinceLanding / stance)
        : -dip + bounce * Mathf.Sin(Mathf.Pi * (sinceLanding - stance) / Math.Max(.0001f, .5f - stance));
    /// <summary>How far a sole corner, as (Y, Z) from the ankle pivot, rises when the foot pitches by <paramref name="pitch"/>.</summary>
    private static float CornerRise(float y, float z, float pitch) => y * Mathf.Cos(pitch) - z * Mathf.Sin(pitch) - y;
    /// <summary>A planted foot at stance progress 0–1, its flat ankle at <paramref name="ground"/> along Z: the ankle's offset from
    /// that flat pose and the foot's pitch (positive raises the toes). It lands rocked back on its heel, settles flat, then rocks
    /// onto its toes to push off, pivoting about the heel or toe so the touching corner stays put.</summary>
    private static (float Y, float Z, float Pitch) PlantedFoot(Sole sole, float progress, float ground, float roll)
    {
        float pitch, pivotY, pivotZ;
        if (progress < HeelRock) (pitch, pivotY, pivotZ) = (roll * (1 - Mathf.SmoothStep(0, 1, progress / HeelRock)), sole.HeelY, sole.HeelZ);
        else if (progress > ToeRock) (pitch, pivotY, pivotZ) = (-roll * Mathf.SmoothStep(0, 1, (progress - ToeRock) / (1 - ToeRock)), sole.ToeY, sole.ToeZ);
        else return (0, ground, 0);
        // The ankle sits at minus the pivot from it; rotating that about X moves the ankle while the pivot stays put.
        var (sin, cos) = (Mathf.Sin(pitch), Mathf.Cos(pitch));
        var (y, z) = (-pivotY, -pivotZ);
        return (y * cos - z * sin - y, ground + y * sin + z * cos - z, pitch);
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
        var run = Math.Max(walk, runningSpeed);
        var walkStride = StrideAmplitude(walk, false); var runStride = StrideAmplitude(run, true);
        var stride = Mathf.Lerp(walkStride, runStride, RunBlend);
        // A flight run shortens its stance below half until full-speed footfalls come no faster than its cap; otherwise a foot is
        // always planted.
        var flying = Flies(gait, run);
        var runStance = flying ? Math.Clamp(gait.MaxRunCadence * runStride * Scale.X / run, .25f, .5f) : Math.Max(.5f, gait.RunStance);
        var stance = Mathf.Lerp(Math.Max(.5f, gait.WalkStance), runStance, RunBlend);
        // A weighted gait dips and rolls in proportion to how much of its leg reach the stride uses.
        float Use(float amplitude, float reach) => gait.Weighted && reach > 0 ? Math.Min(1, amplitude / reach) : 0;
        var (walkUse, runUse) = (Use(walkStride, _walkReach), Use(runStride, flying ? _flightReach : _runReach));
        var dip = Mathf.Lerp(gait.WalkDip * walkUse, gait.RunDip * runUse, RunBlend);
        var roll = moving ? gait.FootRoll * Mathf.Lerp(walkUse, runUse, RunBlend) * _motionBlend : 0;
        var cycles = speed / (2 * stride * Scale.X / stance);
        if (!moving) cycles = pivoting * .5f;
        _stridePhase = (_stridePhase + cycles * delta) % 1;
        if (_stridePhase < 0) _stridePhase += 1;
        var phase = (float)_stridePhase;
        var wave = Mathf.Sin(phase * Mathf.Tau);
        var bounce = gait.Bob * Mathf.Cos(phase * Mathf.Tau * 2);
        if (gait.RunBounce > 0) bounce = Mathf.Lerp(bounce, gait.RunBounce * Math.Abs(Mathf.Sin(phase * Mathf.Tau)), RunBlend);
        var lowered = -dip * DipCurve(phase, stance);
        if (flying) lowered = Mathf.Lerp(-gait.WalkDip * walkUse * DipCurve(phase, stance),
            FlightBob(phase % .5f, Math.Min(.5f, stance), gait.RunDip * runUse, gait.FlightBounce), RunBlend);
        var bob = (-gait.BodyDrop - RunBlend * gait.RunDrop + bounce + lowered) * _motionBlend;
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
            var lift = Mathf.Pow(Mathf.Sin(swing * Mathf.Pi), 2) * Mathf.Lerp(gait.WalkLift, gait.RunLift, RunBlend) * _motionBlend;
            var stepLength = (moving ? stride : .065f * Math.Sign(turning) * leg.Side) * _motionBlend;
            // Planted, the foot's flat position travels back at ground speed; swinging, it carries forward from push-off to landing.
            var sole = _soles[index];
            var foot = PlantedFoot(sole, step / stance, stepLength * (2 * step / stance - 1), roll);
            if (step >= stance)
            {
                var (pushOff, landing) = (PlantedFoot(sole, 1, stepLength, roll), PlantedFoot(sole, 0, -stepLength, roll));
                var carry = Mathf.SmoothStep(0, 1, swing);
                foot = (Mathf.Lerp(pushOff.Y, landing.Y, carry), Mathf.Lerp(pushOff.Z, landing.Z, carry), Mathf.Lerp(pushOff.Pitch, landing.Pitch, carry));
                // A rolled foot sinks from its raised push-off as it swings; lift it clear wherever its heel or toe would dip below ground.
                lift = Math.Max(lift, -foot.Y - Math.Min(CornerRise(sole.HeelY, sole.HeelZ, foot.Pitch), CornerRise(sole.ToeY, sole.ToeZ, foot.Pitch)));
            }
            leg.Hip.Position = leg.Rest + new Vector3(sway, bob, 0);
            PoseLeg(leg, leg.KneeRest.Y + leg.AnkleRest.Y + lift + foot.Y - bob, leg.KneeRest.Z + leg.AnkleRest.Z + foot.Z);
            leg.Ankle.Rotation = new(leg.Ankle.Rotation.X + foot.Pitch,
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
