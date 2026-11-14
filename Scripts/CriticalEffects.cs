using Godot;

namespace Mech;

public enum CriticalBlastKind { Damaged, Destroyed, Explosion }

/// <summary>Battlefield effects for critical hits: a small flash and sparks when a system is damaged or destroyed, and a large
/// fireball with flying fragments and a smoke plume when ammunition explodes.</summary>
public partial class CombatViewport
{
    internal const float AmmoBlastSeconds = 1.4f;
    internal const float AmmoSecondaryAt = .15f;
    internal const int AmmoFragments = 48;
    internal const float AmmoFragmentSeconds = 2;
    internal const float AmmoSmokeEmitSeconds = 4;
    internal const float AmmoSmokeSeconds = 3;
    internal const int AmmoSmokeParticles = 40;
    internal const float AmmoEmberEmitSeconds = 2;
    internal const float AmmoEmberSeconds = .8f;
    internal const float DamagedFlashRadius = 4;
    internal const float DamagedFlashSeconds = .3f;
    internal const int DamagedSparks = 10;
    internal const float DamagedSparkSeconds = .45f;
    internal const float DestroyedFlashRadius = 7;
    internal const float DestroyedFlashSeconds = .5f;
    internal const int DestroyedSparks = 20;
    internal const float DestroyedSmokeEmitSeconds = .5f;
    internal const float DestroyedSmokeSeconds = 1.5f;
    /// <summary>Fireball radius in metres for an ammunition explosion's damage: a 600-damage bin reaches 44 metres.</summary>
    internal static float AmmoBlastRadius(int damage) => Math.Clamp(20 + damage / 25f, 25, 60);
    private static readonly Color SmokeColor = new(.16f, .15f, .14f);
    private static readonly Color PuffColor = new(.55f, .53f, .5f);
    private static readonly QuadMesh ParticleQuad = new() { Size = Vector2.One };
    private static readonly GradientTexture2D ParticleTexture = new()
    {
        Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1, .5f), Width = 64, Height = 64,
        Gradient = new Gradient { Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, .7f), new Color(1, 1, 1, 0)], Offsets = [0, .4f, 1] }
    };
    private static readonly StandardMaterial3D FireParticleMaterial = ParticleMaterial(additive: true);
    private static readonly StandardMaterial3D SmokeParticleMaterial = ParticleMaterial(additive: false);
    private static StandardMaterial3D ParticleMaterial(bool additive) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, BillboardKeepScale = true, VertexColorUseAsAlbedo = true, AlbedoTexture = ParticleTexture,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private sealed class CriticalBlast(string targetId, MechLocation location, double start, CriticalBlastKind kind, float radius)
    {
        public string TargetId { get; } = targetId;
        public MechLocation Location { get; } = location;
        public double Start { get; } = start;
        public CriticalBlastKind Kind { get; } = kind;
        /// <summary>The flash radius, or the fireball radius of an ammunition explosion.</summary>
        public float Radius { get; } = radius;
        public MeshInstance3D Core { get; set; } = null!;
        public MeshInstance3D? Fireball { get; set; }
        public MeshInstance3D? Bloom { get; set; }
        public MeshInstance3D? Secondary { get; set; }
        /// <summary>Each emitter with the effect age at which it stops emitting.</summary>
        public List<(CpuParticles3D Emitter, float StopAt)> Emitters { get; } = [];
        public float Duration => Kind switch
        {
            CriticalBlastKind.Explosion => Math.Max(AmmoBlastSeconds, AmmoSmokeEmitSeconds + AmmoSmokeSeconds),
            CriticalBlastKind.Destroyed => Math.Max(DestroyedFlashSeconds, DestroyedSmokeEmitSeconds + DestroyedSmokeSeconds),
            _ => Math.Max(DamagedFlashSeconds, DamagedSparkSeconds)
        };
        public IEnumerable<Node3D> Nodes => new Node3D?[] { Core, Fireball, Bloom, Secondary }.OfType<Node3D>()
            .Concat(Emitters.Select(item => item.Emitter));
    }
    private readonly List<CriticalBlast> _criticalBlasts = [];

    private void ShowCriticals(CombatShot shot)
    {
        if (shot.Criticals.Count == 0) return;
        if (Simulation!.Unit(shot.TargetId).Side != CombatSide.Friendly && !Simulation.Detected(CombatSide.Friendly, shot.TargetId)) return;
        foreach (var critical in shot.Criticals)
        {
            var kind = critical.Explosion > 0 ? CriticalBlastKind.Explosion
                : critical.Destroyed ? CriticalBlastKind.Destroyed : CriticalBlastKind.Damaged;
            var radius = kind switch
            {
                CriticalBlastKind.Explosion => AmmoBlastRadius(critical.Explosion),
                CriticalBlastKind.Destroyed => DestroyedFlashRadius,
                _ => DamagedFlashRadius
            };
            var blast = new CriticalBlast(shot.TargetId, critical.Location, Simulation.Clock, kind, radius);
            var at = _models[shot.TargetId].LocationCenter(critical.Location);
            switch (kind)
            {
                case CriticalBlastKind.Explosion:
                    blast.Core = Blob(Colors.White); blast.Fireball = Blob(BlastColor); blast.Bloom = Blob(BloomColor);
                    blast.Secondary = Blob(FlameYellow.Lerp(Colors.White, .5f));
                    blast.Emitters.Add((Emitter(at, fire: true, AmmoFragments, AmmoFragmentSeconds, oneShot: true, size: radius * .15f,
                        speed: (radius * 1.3f, radius * 2.1f), spread: 60, gravity: -radius * 1.2f, damping: radius * .3f,
                        colors: [Colors.White, FlameYellow, FlameOrange, new Color(FlameOrange, 0)], grow: (1, .4f)), 0));
                    blast.Emitters.Add((Emitter(at, fire: false, AmmoSmokeParticles, AmmoSmokeSeconds, oneShot: false, size: 30,
                        speed: (12, 22), spread: 20, gravity: 6, damping: 2, colors: [new Color(SmokeColor, 0), new Color(SmokeColor, .75f),
                            new Color(SmokeColor.Lightened(.2f), 0)], grow: (8 / 30f, 1), emitRadius: radius * .2f), AmmoSmokeEmitSeconds));
                    blast.Emitters.Add((Emitter(at, fire: true, 24, AmmoEmberSeconds, oneShot: false, size: 2.5f, speed: (25, 45), spread: 35,
                        gravity: -20, damping: 5, colors: [FlameYellow, FlameOrange, new Color(FlameOrange, 0)], emitRadius: radius * .15f),
                        AmmoEmberEmitSeconds));
                    break;
                case CriticalBlastKind.Destroyed:
                    blast.Core = Blob(FlameYellow.Lerp(Colors.White, .6f)); blast.Fireball = Blob(BlastColor);
                    blast.Emitters.Add((Emitter(at, fire: true, DestroyedSparks, DamagedSparkSeconds * 1.3f, oneShot: true, size: 2,
                        speed: (30, 55), spread: 80, gravity: -40, damping: 10, colors: [Colors.White, FlameYellow, new Color(FlameOrange, 0)]), 0));
                    blast.Emitters.Add((Emitter(at, fire: false, 10, DestroyedSmokeSeconds, oneShot: false, size: 12, speed: (8, 14), spread: 30,
                        gravity: 4, damping: 2, colors: [new Color(PuffColor, .6f), new Color(PuffColor, 0)], grow: (.4f, 1), emitRadius: 2),
                        DestroyedSmokeEmitSeconds));
                    break;
                default:
                    blast.Core = Blob(FlameYellow.Lerp(Colors.White, .6f));
                    blast.Emitters.Add((Emitter(at, fire: true, DamagedSparks, DamagedSparkSeconds, oneShot: true, size: 1.5f, speed: (25, 45),
                        spread: 70, gravity: -40, damping: 10, colors: [Colors.White, FlameYellow, new Color(FlameYellow, 0)]), 0));
                    break;
            }
            _criticalBlasts.Add(blast);
            UpdateCriticalBlast(blast);
        }
    }

    private CpuParticles3D Emitter(Vector3 at, bool fire, int amount, float lifetime, bool oneShot, float size, (float Min, float Max) speed,
        float spread, float gravity, float damping, Color[] colors, (float From, float To)? grow = null, float emitRadius = 0)
    {
        var scale = new Curve();
        var (from, to) = grow ?? (1, 0);
        scale.AddPoint(new Vector2(0, from)); scale.AddPoint(new Vector2(1, to));
        var emitter = new CpuParticles3D
        {
            Amount = amount, Lifetime = lifetime, OneShot = oneShot, Explosiveness = oneShot ? 1 : 0, Randomness = .5f,
            LocalCoords = false, Mesh = ParticleQuad, MaterialOverride = fire ? FireParticleMaterial : SmokeParticleMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Direction = Vector3.Up, Spread = spread,
            Gravity = new Vector3(0, gravity, 0), InitialVelocityMin = speed.Min, InitialVelocityMax = speed.Max,
            DampingMin = damping * .5f, DampingMax = damping, ScaleAmountMin = size * .7f, ScaleAmountMax = size, ScaleAmountCurve = scale,
            ColorRamp = new Gradient { Colors = colors, Offsets = colors.Select((_, i) => i / (float)(colors.Length - 1)).ToArray() },
            EmissionShape = emitRadius > 0 ? CpuParticles3D.EmissionShapeEnum.Sphere : CpuParticles3D.EmissionShapeEnum.Point,
            EmissionSphereRadius = Math.Max(emitRadius, .01f)
        };
        _world.AddChild(emitter);
        emitter.GlobalPosition = at;
        // A one-shot burst only fires reliably from a restart.
        if (oneShot) emitter.Restart(); else emitter.Emitting = true;
        return emitter;
    }

    private float Age(CriticalBlast blast) => (float)((Simulation?.Clock ?? blast.Start) - blast.Start);

    private void UpdateCriticalBlast(CriticalBlast blast)
    {
        var age = Age(blast);
        var at = _models[blast.TargetId].LocationCenter(blast.Location);
        static void Set(MeshInstance3D? mesh, float alpha, float radius, Vector3 at)
        {
            if (mesh is null) return;
            mesh.Visible = alpha > 0 && radius > 0;
            var material = (StandardMaterial3D)mesh.MaterialOverride;
            material.AlbedoColor = new Color(material.AlbedoColor, alpha);
            if (mesh.Visible) mesh.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * radius), at);
        }
        var radius = blast.Radius;
        switch (blast.Kind)
        {
            case CriticalBlastKind.Explosion:
            {
                // A white flash, a second yellow-white cook-off bang, then a swelling fireball inside a slower dark-red bloom.
                var t = age / AmmoBlastSeconds; var live = t is >= 0 and < 1;
                var core = age / (AmmoBlastSeconds * .2f);
                Set(blast.Core, live && core < 1 ? 1 - core : 0, radius * .55f * Mathf.Sqrt(Math.Clamp(core, 0, 1)), at);
                var second = (age - AmmoSecondaryAt) / (AmmoBlastSeconds * .25f);
                Set(blast.Secondary, second is >= 0 and < 1 ? 1 - second : 0, radius * .75f * Mathf.Sqrt(Math.Clamp(second, 0, 1)), at);
                Set(blast.Fireball, live ? 1 - t * t : 0, radius * (.3f + .7f * Mathf.Sqrt(Math.Max(t, 0))), at);
                Set(blast.Bloom, live ? .6f * (1 - t) : 0, radius * (.5f + .75f * Mathf.Sqrt(Math.Max(t, 0))), at);
                break;
            }
            case CriticalBlastKind.Destroyed:
            {
                var t = age / DestroyedFlashSeconds; var live = t is >= 0 and < 1;
                Set(blast.Core, live ? 1 - t : 0, radius * (.5f + .5f * Mathf.Sqrt(Math.Max(t, 0))), at);
                Set(blast.Fireball, live ? .8f * (1 - t * t) : 0, radius * .8f * (.4f + .6f * Mathf.Sqrt(Math.Max(t, 0))), at);
                break;
            }
            default:
            {
                var t = age / DamagedFlashSeconds;
                Set(blast.Core, t is >= 0 and < 1 ? 1 - t : 0, radius * (.4f + .6f * Mathf.Sqrt(Math.Max(t, 0))), at);
                break;
            }
        }
        // Emitters follow the location; particles already emitted stay where they are, so smoke trails behind a moving mech.
        var running = Simulation is { Paused: false, Outcome: CombatOutcome.Ongoing };
        foreach (var (emitter, stopAt) in blast.Emitters)
        {
            emitter.GlobalPosition = at;
            emitter.SpeedScale = running ? 1 : 0;
            if (!emitter.OneShot && age >= stopAt) emitter.Emitting = false;
        }
    }

    private void UpdateCriticalBlasts()
    {
        for (var i = _criticalBlasts.Count - 1; i >= 0; i--)
            if (Simulation is null || Age(_criticalBlasts[i]) >= _criticalBlasts[i].Duration) RemoveCriticalBlast(i);
        foreach (var blast in _criticalBlasts) UpdateCriticalBlast(blast);
    }

    private void RemoveCriticalBlast(int index)
    {
        foreach (var node in _criticalBlasts[index].Nodes) node.QueueFree();
        _criticalBlasts.RemoveAt(index);
    }

    internal IEnumerable<(string TargetId, MechLocation Location, CriticalBlastKind Kind, float Radius, Vector3 Position, bool Visible,
        IReadOnlyList<CpuParticles3D> Emitters)> CriticalBlasts() =>
        _criticalBlasts.Select(item => (item.TargetId, item.Location, item.Kind, item.Radius, _models[item.TargetId].LocationCenter(item.Location),
            item.Core.Visible || item.Fireball is { Visible: true }, (IReadOnlyList<CpuParticles3D>)item.Emitters.Select(e => e.Emitter).ToArray()));
}
