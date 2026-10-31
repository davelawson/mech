using Godot;

namespace Mech;

/// <summary>Location-owned chassis models with a screen-space command/visibility overlay.</summary>
public partial class CombatViewport : Control
{
    public event Action<IReadOnlyList<string>, bool>? SelectionRequested;
    public event Action<Vector2, string?>? CommandRequested;
    public CombatSimulation? Simulation { get; private set; }
    public IReadOnlyCollection<string> Selection { get; set; } = Array.Empty<string>();
    public string? InspectedEnemy { get; set; }
    public string? HoveredMech { get; private set; }
    public bool CommandsEnabled
    {
        get => _commandsEnabled;
        set { _commandsEnabled = value; if (!value) CancelDrag(); }
    }
    public Camera3D Camera { get; private set; } = null!;
    private SubViewport _viewport = null!;
    private Node3D _world = null!;
    private Node3D? _terrainView;
    private readonly Dictionary<string, MechModel> _models = [];
    private readonly Dictionary<string, Aabb> _pickBounds = [];
    private readonly List<ShotEffect> _effects = [];
    private const float ShotLife = .45f;
    private const float BeamReach = .1f;
    private const float BeamFade = .15f;
    private static readonly CylinderMesh BeamMesh = new() { TopRadius = 1, BottomRadius = 1, Height = 1, RadialSegments = 10, Rings = 1 };
    private static readonly SphereMesh BlastMesh = new() { Radius = 1, Height = 2, RadialSegments = 16, Rings = 8 };
    private const float MuzzleFlash = .1f;
    private const float ShellFade = .1f;
    private static readonly Color ShellColor = new(1, .72f, .25f);
    private static readonly Color BulletColor = new(1, .85f, .3f);
    private static readonly Color BlastColor = new(1, .5f, .12f);
    private static readonly Color BloomColor = new(.6f, .1f, .03f);
    // A unit-length capsule along +Y, stretched to the caliber's bullet.
    private static readonly CapsuleMesh BulletMesh = new() { Radius = .5f, Height = 1, RadialSegments = 12, Rings = 4 };
    /// <summary>Bullet length in shell radii.</summary>
    internal const float BulletLength = 5;
    // A shell's white-hot core reaches this share of the fireball radius over the first part of the explosion.
    internal const float BurstShare = .45f;
    internal const float BurstPortion = .4f;
    // An attack that destroys a location sets off a large explosion there.
    internal const float DestructionRadius = 2.5f;
    internal const float DestructionSeconds = 1.1f;
    private const int DestructionDebris = 5;
    // A PPC flashes a brief beam, then a glowing bolt races down it and bursts in crackling arcs.
    private static readonly Color PpcColor = new(.6f, .4f, 1);
    private static readonly Color PpcWhite = Colors.White.Lerp(PpcColor, .3f);
    internal const float PpcBeamRadius = .02f;
    /// <summary>The beam holds until the bolt lands, then fades out over the rest of its half second.</summary>
    internal const float PpcBeamHold = (float)CombatRules.PpcFlightSeconds;
    internal const float PpcBeamSeconds = .5f;
    internal const float PpcBoltRadius = .18f;
    internal const float PpcBurstRadius = .9f;
    internal const float PpcBurstSeconds = .8f;
    internal const int PpcRadialArcs = 6;
    private const int PpcArcSegments = 3;
    private const float PpcArcRadius = .025f;
    /// <summary>Arcs jump to new random paths this often, in effect age.</summary>
    private const float PpcArcFlicker = .06f;
    // A machine gun's burst streams tiny yellow and orange tracers; hits spark on the target, misses vanish.
    internal const int TracerRounds = 15;
    internal const float TracerRadius = .02f;
    internal const float TracerLength = .4f;
    internal const float TracerScatter = .15f;
    internal const float SparkRadius = .05f;
    internal const float SparkSeconds = .08f;
    internal const float TracerFlashSeconds = .04f;
    private const float TracerFlashRadius = .08f;
    internal static readonly Color TracerYellow = new(1, .9f, .3f);
    internal static readonly Color TracerOrange = new(1, .55f, .1f);
    private static readonly Color SparkColor = new(1, 1, .75f);
    // Missile salvos ripple out of the rack and skitter in flight, gathering only as they reach their impact points.
    internal const float MissileRipple = .25f;
    internal const float MissileWobble = .75f;
    internal const float MissileBlastRadius = .25f;
    internal const float MissileBlastSeconds = .3f;
    internal const float MissileTrailSeconds = .04f;
    /// <summary>LRMs leave the rack at 40% of their average speed and dive in at 160%.</summary>
    internal const float LrmLaunchShare = 2f / 3;
    private static readonly Color MissileGlow = new(1, .85f, .55f);
    private static readonly Color FlameYellow = new(1, .9f, .2f);
    private static readonly Color FlameOrange = new(1, .45f, .05f);
    /// <summary>Flame trail color from the tail (0) to the missile (1): orange, yellow, white, fading out toward the tail.</summary>
    internal static Color FlameColor(float along) => new(along < .5f ? FlameOrange.Lerp(FlameYellow, along * 2)
        : FlameYellow.Lerp(Colors.White, along * 2 - 1), along);
    // The beam cylinder with one middle ring, recolored by height so the trail's +Y end is the missile.
    internal static readonly ArrayMesh FlameMesh = BuildFlameMesh();
    private static ArrayMesh BuildFlameMesh()
    {
        var arrays = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 1, RadialSegments = 8, Rings = 1 }.GetMeshArrays();
        arrays[(int)Mesh.ArrayType.Color] = ((Vector3[])arrays[(int)Mesh.ArrayType.Vertex]).Select(vertex => FlameColor(vertex.Y + .5f)).ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
    // The muzzle is chosen when the weapon fires; both ends follow the animated models while the effect fades.
    // Effects age with simulation time, so pause freezes a laser burn in step with its delayed damage.
    private sealed class ShotEffect(CombatShot shot, MechLocation mount, Node3D? muzzle, double start, WeaponKind? laser, WeaponKind? shell = null)
    {
        public CombatShot Shot { get; } = shot;
        public MechLocation Mount { get; } = mount;
        public Node3D? Muzzle { get; } = muzzle;
        public double Start { get; } = start;
        public WeaponKind? Laser { get; } = laser;
        public WeaponKind? Shell { get; } = shell;
        public float Duration => Salvo is { } salvo ? salvo.Max(missile => missile.Arrival) + MissileBlastSeconds
            : Rounds is { } rounds ? rounds.Max(round => round.Launched ? round.Fired + round.Flight + Math.Max(SparkSeconds, ShellFade)
                : round.Fired < (CutOff ?? float.MaxValue) ? float.MaxValue : 0)
            : Ppc ? Flight + (Shot.Hit || Grounded ? PpcBurstSeconds : ShellFade)
            : Shell is { } kind ? Flight + (Shot.Hit || Grounded ? ExplosionSeconds(kind) : ShellFade)
            : Laser is null ? ShotLife : (float)CombatRules.LaserBurnSeconds + BeamFade;
        public double? BurnLanded { get; set; }
        public MeshInstance3D? Halo { get; set; }
        public MeshInstance3D? Core { get; set; }
        // A shell leaves from where it was fired. A miss flies on to the ground or fades beyond long range.
        public Vector3 Launch { get; set; }
        public Vector3 MissEnd { get; set; }
        public bool Grounded { get; set; }
        public float Flight { get; set; }
        public MeshInstance3D? Slug { get; set; }
        public MeshInstance3D? Flash { get; set; }
        public MeshInstance3D? Blast { get; set; }
        public MeshInstance3D? Burst { get; set; }
        public IReadOnlyList<MissileFlight>? Salvo { get; set; }
        public bool Ppc { get; init; }
        /// <summary>A PPC's beam, bolt and burst; each arc is PpcArcSegments jagged segments, then one arc per splashed location.</summary>
        public MeshInstance3D[] PpcMeshes { get; set; } = [];
        public MechLocation[] SplashLocations { get; set; } = [];
        /// <summary>Peak height of the salvo's arc above the straight line to its target.</summary>
        public float Loft { get; set; }
        public WeaponSpec? Weapon { get; init; }
        /// <summary>A machine gun burst's tracers, and the effect age at which its shooter stopped firing.</summary>
        public IReadOnlyList<TracerRound>? Rounds { get; set; }
        public float? CutOff { get; set; }
    }
    private sealed class TracerRound(float fired, Color color, Vector3 scatter)
    {
        public float Fired { get; } = fired;
        public Color Color { get; } = color;
        /// <summary>Offset from the struck location for a hit; a miss spreads three times wider around its miss point.</summary>
        public Vector3 Scatter { get; } = scatter;
        public bool Launched { get; set; }
        public Vector3 Launch { get; set; }
        public Vector3 MissEnd { get; set; }
        public bool Grounded { get; set; }
        public float Flight { get; set; }
        public Vector3 Head { get; set; }
        public Vector3 End { get; set; }
        public MeshInstance3D Streak { get; set; } = null!;
        public MeshInstance3D Spark { get; set; } = null!;
    }
    private sealed class MissileFlight(CombatMissile missile, Node3D? tube, float delay, float arrival, float amplitude, float[] waves)
    {
        /// <summary>Launch speed relative to the steady acceleration over the flight; see MissileProgress.</summary>
        public float LaunchShare { get; set; }
        public CombatMissile Missile { get; } = missile;
        public Node3D? Tube { get; } = tube;
        public float Delay { get; } = delay;
        public float Arrival { get; set; } = arrival;
        public float Amplitude { get; } = amplitude;
        /// <summary>Two lateral and two vertical waves: cycles per flight, then phases.</summary>
        public float[] Waves { get; } = waves;
        public Vector3 Launch { get; set; }
        public Vector3 MissEnd { get; set; }
        public MeshInstance3D Glow { get; set; } = null!;
        public MeshInstance3D Trail { get; set; } = null!;
        public MeshInstance3D Flash { get; set; } = null!;
        public MeshInstance3D Blast { get; set; } = null!;
    }
    private sealed class Destruction(string targetId, MechLocation location, double start, Vector3[] scatter)
    {
        public string TargetId { get; } = targetId;
        public MechLocation Location { get; } = location;
        public double Start { get; } = start;
        /// <summary>Each debris blob's outward direction and relative reach.</summary>
        public Vector3[] Scatter { get; } = scatter;
        public MeshInstance3D Core { get; set; } = null!;
        public MeshInstance3D Fireball { get; set; } = null!;
        public MeshInstance3D Bloom { get; set; } = null!;
        public MeshInstance3D[] Debris { get; set; } = [];
        public IEnumerable<MeshInstance3D> Meshes => Debris.Prepend(Bloom).Prepend(Fireball).Prepend(Core);
    }
    private readonly List<Destruction> _destructions = [];
    internal static float ExplosionRadius(WeaponKind kind) => kind switch
    {
        WeaponKind.AC2 => .3f, WeaponKind.AC5 => .5f, WeaponKind.AC10 => .8f, _ => 1.2f
    };
    internal static float ExplosionSeconds(WeaponKind kind) => kind switch
    {
        WeaponKind.AC2 => .3f, WeaponKind.AC5 => .4f, WeaponKind.AC10 => .5f, _ => .6f
    };
    internal static float ShellRadius(WeaponKind kind) => kind switch
    {
        WeaponKind.AC2 => .06f, WeaponKind.AC5 => .08f, WeaponKind.AC10 => .1f, _ => .13f
    };
    // A mostly colored core keeps additive beams from washing out to white.
    internal const float LaserCoreWhite = .3f;
    internal static Color LaserColor(WeaponKind kind) => kind switch
    {
        WeaponKind.SmallLaser => new Color(1, 0, 0),
        WeaponKind.MediumLaser => new Color(0, 1, 0),
        _ => new Color(.1f, .2f, 1)
    };
    internal static float LaserRadius(WeaponKind kind) => kind switch
    {
        WeaponKind.SmallLaser => .02f, WeaponKind.MediumLaser => .04f, _ => .06f
    };
    private Vector3 _focus = new(0, 0, CombatRules.DeploymentDepth - 20);
    private static readonly float DefaultPitch = Mathf.Atan2(80, 70);
    private static readonly float OrbitRadius = new Vector2(140, 160).Length();
    private const float RightDragThreshold = 5;
    private static readonly float OrbitSensitivity = Mathf.DegToRad(.25f);
    private float _yaw;
    private float _pitch = DefaultPitch;
    private bool _commandsEnabled = true;
    private bool _windowFocused = true;
    private bool _rightPressed;
    private bool _rightDragging;
    private Vector2 _rightCursorStart;
    private Vector2 _rightDisplacement;
    private Input.MouseModeEnum _previousMouseMode;
    private bool _selecting;
    private bool _panning;
    private bool _rotating;
    private Vector2 _dragStart;
    private Vector2 _dragEnd;

    public override void _Ready()
    {
        ClipContents = true; FocusMode = FocusModeEnum.All; MouseDefaultCursorShape = CursorShape.Cross;
        var surface = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
        surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(surface);
        _viewport = new SubViewport { OwnWorld3D = true, GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible, HandleInputLocally = false };
        surface.AddChild(_viewport);
        _world = new Node3D(); _viewport.AddChild(_world);
        Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 50, Far = 800, Current = true };
        _world.AddChild(Camera);
        var light = new DirectionalLight3D { RotationDegrees = new Vector3(-60, -30, 0), LightEnergy = 1.1f };
        _world.AddChild(light);
        _world.AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("111b21"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("b1c1cc"), AmbientLightEnergy = .65f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        } });
        Resized += QueueRedraw;
        FocusExited += CancelDrag;
        MouseExited += () => SetHovered(null);
        VisibilityChanged += () => { if (!IsVisibleInTree()) CancelDrag(); };
        GetWindow().FocusExited += WindowFocusExited;
        GetWindow().FocusEntered += WindowFocusEntered;
        UpdateCamera();
    }
    public void Display(CombatSimulation simulation)
    {
        CancelDrag();
        if (Simulation is not null) Simulation.ShotFired -= OnShot;
        foreach (var model in _models.Values) { _world.RemoveChild(model); model.QueueFree(); }
        for (var i = _effects.Count - 1; i >= 0; i--) RemoveEffect(i);
        for (var i = _destructions.Count - 1; i >= 0; i--) RemoveDestruction(i);
        _models.Clear(); _pickBounds.Clear();
        Simulation = simulation; Simulation.ShotFired += OnShot; InspectedEnemy = null; Selection = Array.Empty<string>();
        if (_terrainView is not null) { _world.RemoveChild(_terrainView); _terrainView.QueueFree(); }
        _terrainView = BattlefieldTerrainView.Create(simulation.Terrain); _world.AddChild(_terrainView);
        _focus = new Vector3(0, 0, CombatRules.DeploymentDepth - 20); _yaw = 0; _pitch = DefaultPitch; Camera.Size = 50; UpdateCamera();
        foreach (var unit in simulation.Units) CreateMech(unit);
        CancelDrag(); SyncModels(); QueueRedraw();
    }
    private void OnShot(CombatShot shot)
    {
        if (Simulation is null) return;
        Destroy(shot);
        if (shot.Burn)
        {
            foreach (var effect in _effects.Where(item => item.Shot.Sequence == shot.Sequence)) effect.BurnLanded = Simulation.Elapsed;
            return;
        }
        // The shell effect times its own flight from the discharge.
        if (shot.Impact) return;
        var attacker = Simulation.Unit(shot.AttackerId);
        if (attacker.Side != CombatSide.Friendly && !Simulation.Detected(CombatSide.Friendly, shot.AttackerId)) return;
        var weapon = attacker.Spec.Weapons.FirstOrDefault(item => item.Id == shot.WeaponId);
        var laser = weapon is not null && CombatRules.IsLaser(weapon.Kind) ? weapon.Kind : (WeaponKind?)null;
        var shell = weapon is not null && CombatRules.IsAutocannon(weapon.Kind) ? weapon.Kind : (WeaponKind?)null;
        var created = new ShotEffect(shot, weapon?.Location ?? MechLocation.CenterTorso,
            weapon is null ? null : _models[shot.AttackerId].NextFiringMarker(weapon.Id), Simulation.Elapsed, laser, shell)
        { Ppc = weapon is not null && CombatRules.IsPpc(weapon.Kind), Weapon = weapon };
        if (laser is { } kind)
        {
            var color = LaserColor(kind);
            created.Halo = Beam(color); created.Core = Beam(color.Lerp(Colors.White, LaserCoreWhite));
        }
        if (shell is not null) LaunchShell(created, weapon!);
        if (created.Ppc) LaunchPpc(created, weapon!);
        if (shot.Missiles.Count > 0) LaunchSalvo(created, weapon!);
        if (weapon is not null && CombatRules.IsMachineGun(weapon.Kind)) LoadBurst(created);
        _effects.Add(created);
    }
    // Unshaded additive cylinders read as a glowing beam from any camera angle without post-processing.
    private MeshInstance3D Beam(Color color, Mesh? mesh = null)
    {
        var beam = new MeshInstance3D
        {
            Mesh = mesh ?? BeamMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = color
            }
        };
        _world.AddChild(beam);
        return beam;
    }
    // A straight projectile leaves from where it was fired. A miss flies on to the ground or fades beyond long range plus 10 hexes.
    private void AimProjectile(ShotEffect effect, WeaponSpec weapon, float speed)
    {
        var (from, to, _) = ShotPath(effect);
        effect.Launch = from;
        if (effect.Shot.Hit) effect.Flight = (float)effect.Shot.FlightSeconds;
        else
        {
            var direction = (to - from).Normalized();
            var reach = weapon.Long * CombatRules.WeaponRangeScale + 10;
            var beyond = Math.Max(0, reach - from.DistanceTo(to));
            var ground = Simulation!.Terrain.RayDistance(to, direction, beyond);
            effect.Grounded = ground is not null;
            effect.MissEnd = ground is { } distance ? to + direction * distance : from + direction * reach;
            effect.Flight = from.DistanceTo(effect.MissEnd) / speed;
        }
    }
    private void LaunchShell(ShotEffect effect, WeaponSpec weapon)
    {
        AimProjectile(effect, weapon, CombatRules.ShellSpeed);
        effect.Slug = Beam(BulletColor, BulletMesh); effect.Slug.Visible = false;
        effect.Flash = Blob(ShellColor.Lerp(Colors.White, .5f)); effect.Blast = Blob(BlastColor); effect.Burst = Blob(Colors.White);
    }
    private MeshInstance3D Blob(Color color)
    {
        var blob = Beam(color); blob.Mesh = BlastMesh; blob.Visible = false;
        return blob;
    }
    // Where the shell is now, or where it struck: hits track the struck location on the moving target.
    private Vector3 ShellPoint(ShotEffect effect, float age)
    {
        var end = effect.Shot.Hit ? ShotPath(effect).To : effect.MissEnd;
        return effect.Flight <= 0 ? end : effect.Launch.Lerp(end, Math.Clamp(age / effect.Flight, 0, 1));
    }
    internal IEnumerable<(CombatShot Shot, Vector3 Position, bool Flying, bool Exploding)> Shells() =>
        _effects.Where(effect => effect.Shell is not null)
            .Select(effect => (effect.Shot, ShellPoint(effect, Age(effect)), effect.Slug!.Visible, effect.Blast!.Visible));
    private void UpdateShell(ShotEffect effect)
    {
        if (effect.Shell is not { } kind) return;
        var age = Age(effect);
        static void Set(MeshInstance3D mesh, float alpha)
        {
            mesh.Visible = alpha > 0;
            var material = (StandardMaterial3D)mesh.MaterialOverride;
            material.AlbedoColor = new Color(material.AlbedoColor, alpha);
        }
        var flash = Math.Clamp(1 - age / MuzzleFlash, 0, 1);
        Set(effect.Flash!, flash);
        if (flash > 0) effect.Flash!.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * ExplosionRadius(kind) * (.6f - .2f * flash)), effect.Launch);
        // The bullet flies nose-first along its path, with no trail.
        var head = ShellPoint(effect, age);
        var axis = (effect.Shot.Hit ? ShotPath(effect).To : effect.MissEnd) - effect.Launch;
        var flying = age < effect.Flight;
        var fade = flying ? 1 : effect.Shot.Hit || effect.Grounded ? 0 : Math.Clamp(1 - (age - effect.Flight) / ShellFade, 0, 1);
        Set(effect.Slug!, axis.LengthSquared() > 1e-6f ? fade : 0);
        if (effect.Slug!.Visible) effect.Slug.GlobalTransform = Bullet(head, axis.Normalized(), ShellRadius(kind));
        // The fireball swells to its caliber's radius and fades over its caliber's duration, around a brief white-hot core.
        var blast = flying || !(effect.Shot.Hit || effect.Grounded) ? -1 : (age - effect.Flight) / ExplosionSeconds(kind);
        Set(effect.Blast!, blast is >= 0 and < 1 ? 1 - blast * blast : 0);
        if (effect.Blast!.Visible)
            effect.Blast.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * ExplosionRadius(kind) * (.35f + .65f * Mathf.Sqrt(blast))), head);
        var burst = blast < 0 ? -1 : blast / BurstPortion;
        Set(effect.Burst!, burst is >= 0 and < 1 ? 1 - burst : 0);
        if (effect.Burst!.Visible)
            effect.Burst.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * ExplosionRadius(kind) * BurstShare * Mathf.Sqrt(burst)), head);
    }
    private void LaunchPpc(ShotEffect effect, WeaponSpec weapon)
    {
        // A miss keeps the speed a hit would have had, flying on past the target.
        var (from, to, _) = ShotPath(effect);
        AimProjectile(effect, weapon, Math.Max(from.DistanceTo(to), 1e-3f) / (float)CombatRules.PpcFlightSeconds);
        effect.SplashLocations = effect.Shot.Splashes.Select(splash => splash.Location).Distinct().ToArray();
        var arcs = (PpcRadialArcs + effect.SplashLocations.Length) * PpcArcSegments;
        // Beam halo and core, bolt halo and core, burst glow and flash, then the arc segments.
        effect.PpcMeshes = new[] { Beam(PpcColor), Beam(PpcColor.Lerp(Colors.White, .5f)), Blob(PpcColor), Blob(PpcWhite),
            Blob(PpcColor), Blob(PpcWhite) }.Concat(Enumerable.Range(0, arcs).Select(_ => Beam(PpcColor.Lerp(Colors.White, .6f)))).ToArray();
        foreach (var mesh in effect.PpcMeshes) mesh.Visible = false;
    }
    // A cylinder of BeamMesh spanning two points.
    private static Transform3D Span(Vector3 from, Vector3 to, float radius)
    {
        var axis = to - from;
        var across = axis.Cross(Math.Abs(axis.Normalized().Y) > .9f ? Vector3.Right : Vector3.Up).Normalized();
        return new Transform3D(new Basis(across * radius, axis, axis.Cross(across).Normalized() * radius), (from + to) / 2);
    }
    private void UpdatePpc(ShotEffect effect)
    {
        if (!effect.Ppc) return;
        var age = Age(effect);
        var meshes = effect.PpcMeshes;
        static void Set(MeshInstance3D mesh, float alpha)
        {
            mesh.Visible = alpha > 0;
            var material = (StandardMaterial3D)mesh.MaterialOverride;
            material.AlbedoColor = new Color(material.AlbedoColor, alpha);
        }
        static void Sphere(MeshInstance3D mesh, float alpha, float radius, Vector3 at)
        {
            Set(mesh, radius > 0 ? alpha : 0);
            if (mesh.Visible) mesh.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * radius), at);
        }
        // The beam appears at full length at once, holds briefly and fades.
        var (from, to, _) = ShotPath(effect);
        var beam = age < PpcBeamHold ? 1 : Math.Clamp(1 - (age - PpcBeamHold) / (PpcBeamSeconds - PpcBeamHold), 0, 1);
        for (var i = 0; i < 2; i++)
        {
            Set(meshes[i], from.DistanceSquaredTo(to) > 1e-6f ? beam : 0);
            if (meshes[i].Visible) meshes[i].GlobalTransform = Span(from, to, PpcBeamRadius * (i == 0 ? 1 : .4f));
        }
        // The bolt races down the beam's path to the struck location on the moving target.
        var head = ShellPoint(effect, age);
        var flying = age < effect.Flight;
        var bolt = flying ? 1 : effect.Shot.Hit || effect.Grounded ? 0 : Math.Clamp(1 - (age - effect.Flight) / ShellFade, 0, 1);
        Sphere(meshes[2], bolt, PpcBoltRadius, head);
        Sphere(meshes[3], bolt, PpcBoltRadius * .5f, head);
        // The burst: a blue glow around a brief white flash, with flickering arcs radiating out and jumping to each splashed location.
        var t = flying || !(effect.Shot.Hit || effect.Grounded) ? -1 : (age - effect.Flight) / PpcBurstSeconds;
        var live = t is >= 0 and < 1;
        Sphere(meshes[4], live ? .8f * (1 - t) : 0, PpcBurstRadius * (.4f + .6f * Mathf.Sqrt(Math.Max(t, 0))), head);
        var flash = t / .3f;
        Sphere(meshes[5], live && flash < 1 ? 1 - flash : 0, PpcBurstRadius * .5f * Mathf.Sqrt(Math.Clamp(flash, 0, 1)), head);
        var arcCount = PpcRadialArcs + effect.SplashLocations.Length;
        var bucket = (int)(Math.Max(age - effect.Flight, 0) / PpcArcFlicker);
        for (var arc = 0; arc < arcCount; arc++)
        {
            var random = new Random(unchecked((int)effect.Shot.Sequence * 7919 + bucket * 104729 + arc * 31));
            float Next() => (float)random.NextDouble() * 2 - 1;
            var splash = arc >= PpcRadialArcs;
            var end = splash ? _models[effect.Shot.TargetId].LocationCenter(effect.SplashLocations[arc - PpcRadialArcs])
                : head + new Vector3(Next(), Next(), Next()).Normalized() * PpcBurstRadius * (1.1f + .6f * (float)random.NextDouble());
            var alpha = live ? (1 - t) * (.5f + .5f * (float)random.NextDouble()) : 0;
            var jitter = head.DistanceTo(end) * .18f;
            var previous = head;
            for (var segment = 0; segment < PpcArcSegments; segment++)
            {
                var mesh = meshes[6 + arc * PpcArcSegments + segment];
                var next = segment == PpcArcSegments - 1 ? end
                    : head.Lerp(end, (segment + 1f) / PpcArcSegments) + new Vector3(Next(), Next(), Next()) * jitter;
                Set(mesh, previous.DistanceSquaredTo(next) > 1e-6f ? alpha : 0);
                if (mesh.Visible) mesh.GlobalTransform = Span(previous, next, PpcArcRadius);
                previous = next;
            }
        }
    }
    internal IEnumerable<(CombatShot Shot, Vector3 Bolt, bool BeamVisible, bool Flying, bool Bursting, int Arcs)> Ppcs() =>
        _effects.Where(effect => effect.Ppc).Select(effect => (effect.Shot, ShellPoint(effect, Age(effect)), effect.PpcMeshes[0].Visible,
            effect.PpcMeshes[2].Visible, effect.PpcMeshes[4].Visible,
            Enumerable.Range(0, (effect.PpcMeshes.Length - 6) / PpcArcSegments)
                .Count(arc => effect.PpcMeshes.Skip(6 + arc * PpcArcSegments).Take(PpcArcSegments).Any(mesh => mesh.Visible))));
    internal static Transform3D Bullet(Vector3 center, Vector3 direction, float radius)
    {
        var across = direction.Cross(Math.Abs(direction.Y) > .9f ? Vector3.Right : Vector3.Up).Normalized();
        return new Transform3D(new Basis(across * radius * 2, direction * radius * BulletLength, direction.Cross(across).Normalized() * radius * 2), center);
    }
    internal IEnumerable<(CombatShot Shot, Transform3D Transform)> Bullets() =>
        _effects.Where(effect => effect.Slug is { Visible: true }).Select(effect => (effect.Shot, effect.Slug!.GlobalTransform));
    // Every location an attack destroys on a visible mech explodes, whatever the weapon.
    private void Destroy(CombatShot shot)
    {
        if (Simulation!.Unit(shot.TargetId).Side != CombatSide.Friendly && !Simulation.Detected(CombatSide.Friendly, shot.TargetId)) return;
        foreach (var part in shot.DamageApplied.Where(item => item.Destroyed))
        {
            var random = new Random(unchecked((int)shot.Sequence * 7919 + (int)part.Location * 104729 + 31));
            var scatter = Enumerable.Range(0, DestructionDebris).Select(_ =>
            {
                var direction = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * .8f + .2f, (float)random.NextDouble() * 2 - 1).Normalized();
                return direction * (1.2f + .6f * (float)random.NextDouble());
            }).ToArray();
            _destructions.Add(new Destruction(shot.TargetId, part.Location, Simulation.Clock, scatter)
            {
                Core = Blob(Colors.White), Fireball = Blob(BlastColor), Bloom = Blob(BloomColor),
                Debris = scatter.Select(_ => Blob(FlameYellow)).ToArray()
            });
        }
    }
    private float Age(Destruction destruction) => (float)((Simulation?.Clock ?? destruction.Start) - destruction.Start);
    internal IEnumerable<(string TargetId, MechLocation Location, Vector3 Position, bool Visible)> Destructions() =>
        _destructions.Select(item => (item.TargetId, item.Location, _models[item.TargetId].LocationCenter(item.Location), item.Fireball.Visible));
    private void UpdateDestruction(Destruction destruction)
    {
        var t = Age(destruction) / DestructionSeconds;
        var at = _models[destruction.TargetId].LocationCenter(destruction.Location);
        static void Set(MeshInstance3D mesh, float alpha, float radius, Vector3 at)
        {
            mesh.Visible = alpha > 0 && radius > 0;
            var material = (StandardMaterial3D)mesh.MaterialOverride;
            material.AlbedoColor = new Color(material.AlbedoColor, alpha);
            if (mesh.Visible) mesh.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * radius), at);
        }
        var live = t is >= 0 and < 1;
        // A white flash, a swelling orange fireball, a slower dark-red bloom and yellow debris flung outward.
        var core = t / .25f;
        Set(destruction.Core, core < 1 && live ? 1 - core : 0, DestructionRadius * .5f * Mathf.Sqrt(Math.Clamp(core, 0, 1)), at);
        Set(destruction.Fireball, live ? 1 - t * t : 0, DestructionRadius * (.3f + .7f * Mathf.Sqrt(Math.Max(t, 0))), at);
        Set(destruction.Bloom, live ? .6f * (1 - t) : 0, DestructionRadius * (.5f + .7f * Mathf.Sqrt(Math.Max(t, 0))), at);
        for (var i = 0; i < destruction.Debris.Length; i++)
        {
            var reach = DestructionRadius * (1 - (1 - t) * (1 - t));
            Set(destruction.Debris[i], live ? 1 - t : 0, DestructionRadius * .12f * (1 - .5f * t), at + destruction.Scatter[i] * reach);
        }
    }
    private void RemoveDestruction(int index)
    {
        foreach (var mesh in _destructions[index].Meshes) mesh.QueueFree();
        _destructions.RemoveAt(index);
    }
    private void LaunchSalvo(ShotEffect effect, WeaponSpec weapon)
    {
        var shot = effect.Shot; var flight = (float)shot.FlightSeconds;
        var ripple = Math.Min(MissileRipple, flight * .4f);
        // Visual randomness is seeded by the discharge and never touches the simulation's draws.
        var random = new Random(unchecked((int)shot.Sequence * 7919 + 17));
        float Between(float low, float high) => low + (float)random.NextDouble() * (high - low);
        var attacker = _models[shot.AttackerId]; var target = _models[shot.TargetId];
        var from = effect.Muzzle?.GlobalPosition ?? attacker.LocationCenter(effect.Mount);
        var aim = target.LocationCenter(shot.Location);
        var reach = new Vector2(aim.X - from.X, aim.Z - from.Z).Length();
        // SRMs fly flat; LRMs loft, and an indirect salvo arcs over whatever blocks the shooter's sight.
        effect.Loft = CombatRules.IsSrm(weapon.Kind) ? 0
            : shot.Indirect ? Math.Max(.35f * reach, Math.Min(reach, Clearance(from, aim))) : .15f * reach;
        var salvo = new List<MissileFlight>();
        for (var i = 0; i < shot.Missiles.Count; i++)
        {
            var missile = shot.Missiles[i];
            var tube = i == 0 ? effect.Muzzle : attacker.NextFiringMarker(weapon.Id);
            var delay = shot.Missiles.Count == 1 ? 0 : ripple * i / (shot.Missiles.Count - 1);
            var waves = new[] { Between(1.5f, 4), Between(1.5f, 4), Between(1.5f, 4), Between(1.5f, 4),
                Between(0, Mathf.Tau), Between(0, Mathf.Tau), Between(0, Mathf.Tau), Between(0, Mathf.Tau) };
            var created = new MissileFlight(missile, tube, delay, flight, MissileWobble * Between(.5f, 1), waves)
            { Launch = tube?.GlobalPosition ?? from };
            if (!missile.Hit)
            {
                // A miss comes down beside and beyond its rolled location and bursts on the ground.
                var struck = target.LocationCenter(missile.Location);
                var direction = new Vector3(struck.X - created.Launch.X, 0, struck.Z - created.Launch.Z).Normalized();
                var side = new Vector3(-direction.Z, 0, direction.X) * (random.Next(2) == 0 ? 1 : -1);
                var ground = struck + side * Between(1, 2.5f) + direction * Between(1, 3);
                created.MissEnd = new Vector3(ground.X, Simulation!.Terrain.HeightAt(new Vector2(ground.X, ground.Z)), ground.Z);
                var span = new Vector2(struck.X - created.Launch.X, struck.Z - created.Launch.Z).Length();
                var missReach = new Vector2(created.MissEnd.X - created.Launch.X, created.MissEnd.Z - created.Launch.Z).Length();
                // A miss keeps the rack's motion out to its ground point.
                created.Arrival = CombatRules.IsSrm(weapon.Kind) ? flight + (float)(CombatRules.SrmFlightSeconds(missReach) - CombatRules.SrmFlightSeconds(span))
                    : span < 1e-3f ? flight : flight * missReach / span;
                created.Arrival = Math.Max(created.Arrival, delay + .05f);
            }
            var window = created.Arrival - delay;
            created.LaunchShare = CombatRules.IsSrm(weapon.Kind) ? (float)(2 * CombatRules.SrmLaunchSpeed / (CombatRules.SrmAcceleration * window)) : LrmLaunchShare;
            created.Glow = Blob(MissileGlow); created.Flash = Blob(ShellColor.Lerp(Colors.White, .5f)); created.Blast = Blob(BlastColor);
            created.Trail = Beam(Colors.White, FlameMesh); created.Trail.Visible = false;
            ((StandardMaterial3D)created.Trail.MaterialOverride).VertexColorUseAsAlbedo = true;
            salvo.Add(created);
        }
        effect.Salvo = salvo;
    }
    // An indirect arc peaks high enough to pass blocking hills and rocks with room to spare.
    private float Clearance(Vector3 from, Vector3 to)
    {
        var terrain = Simulation!.Terrain; var peak = 0f;
        for (var i = 1; i < 20; i++)
        {
            var t = i / 20f; var point = from.Lerp(to, t); var ground = new Vector2(point.X, point.Z);
            var height = terrain.HeightAt(ground);
            foreach (var rock in terrain.Patches.Where(patch => patch.Kind == TerrainKind.Rock && ground.DistanceTo(patch.Center) <= patch.Radius))
                height = Math.Max(height, terrain.HeightAt(rock.Center) + rock.Height);
            peak = Math.Max(peak, (height + 3 - point.Y) / (4 * t * (1 - t)));
        }
        return peak;
    }
    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
    /// <summary>Skitter strength over a flight: building after launch, strongest mid-flight, gone over the last fifth.</summary>
    internal static float WobbleEnvelope(float progress) => Smooth(progress / .25f) * (1 - Smooth((progress - .8f) / .2f));
    /// <summary>
    /// Fraction of the path covered after a fraction of the flight under steady acceleration.
    /// launchShare is twice the launch speed over the speed gained in flight; the missile always speeds up.
    /// </summary>
    internal static float MissileProgress(float time, float launchShare)
    { time = Math.Clamp(time, 0, 1); return (launchShare * time + time * time) / (launchShare + 1); }
    private static float Progress(MissileFlight missile, float age) => MissileProgress(
        (age - missile.Delay) / Math.Max(1e-3f, missile.Arrival - missile.Delay), missile.LaunchShare);
    // The lofted course to the live struck location (or the miss point), and the skittering point on it.
    private (Vector3 Course, Vector3 Position) MissilePoint(ShotEffect effect, MissileFlight missile, float age)
    {
        var u = Progress(missile, age);
        var end = missile.Missile.Hit ? _models[effect.Shot.TargetId].LocationCenter(missile.Missile.Location) : missile.MissEnd;
        var course = missile.Launch.Lerp(end, u) + Vector3.Up * effect.Loft * 4 * u * (1 - u);
        var heading = new Vector3(end.X - missile.Launch.X, 0, end.Z - missile.Launch.Z);
        var side = heading.LengthSquared() < 1e-6f ? Vector3.Right : new Vector3(-heading.Z, 0, heading.X).Normalized();
        var w = missile.Waves;
        float Wave(int a, int b) => (Mathf.Sin(Mathf.Tau * w[a] * u + w[a + 4]) + Mathf.Sin(Mathf.Tau * w[b] * u + w[b + 4])) / 2;
        // Each axis stays within amplitude / √2, so the combined offset stays inside the envelope.
        var scale = missile.Amplitude * WobbleEnvelope(u) / Mathf.Sqrt2;
        return (course, course + (side * Wave(0, 1) + Vector3.Up * Wave(2, 3)) * scale);
    }
    internal IEnumerable<(CombatShot Shot, CombatMissile Missile, Vector3 Position, Vector3 Course, Vector3 End, float Progress, bool Flying, bool Exploding, float Loft)> Missiles() =>
        _effects.Where(effect => effect.Salvo is not null).SelectMany(effect => effect.Salvo!.Select(missile =>
        {
            var age = Age(effect); var (course, position) = MissilePoint(effect, missile, age);
            var end = missile.Missile.Hit ? _models[effect.Shot.TargetId].LocationCenter(missile.Missile.Location) : missile.MissEnd;
            return (effect.Shot, missile.Missile, position, course, end, Progress(missile, age), missile.Glow.Visible, missile.Blast.Visible, effect.Loft);
        }));
    private void UpdateSalvo(ShotEffect effect)
    {
        if (effect.Salvo is not { } salvo) return;
        var age = Age(effect);
        static void Set(MeshInstance3D mesh, float alpha, float maximum = 1)
        {
            mesh.Visible = alpha > 0;
            var material = (StandardMaterial3D)mesh.MaterialOverride;
            material.AlbedoColor = new Color(material.AlbedoColor, alpha * maximum);
        }
        foreach (var missile in salvo)
        {
            // Until it leaves, a missile waits in its tube on the moving shooter.
            if (age <= missile.Delay && missile.Tube is { } tube) missile.Launch = tube.GlobalPosition;
            var launched = age >= missile.Delay; var flying = launched && age < missile.Arrival;
            var flash = launched ? Math.Clamp(1 - (age - missile.Delay) / MuzzleFlash, 0, 1) : 0;
            Set(missile.Flash, flash);
            if (flash > 0) missile.Flash.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * .15f), missile.Launch);
            var head = MissilePoint(effect, missile, age).Position;
            Set(missile.Glow, flying ? 1 : 0);
            if (flying) missile.Glow.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * .07f), head);
            // A short flame trail follows the skittering path back toward the launcher.
            var tail = MissilePoint(effect, missile, Math.Max(missile.Delay, age - MissileTrailSeconds)).Position;
            var axis = head - tail;
            Set(missile.Trail, flying && axis.LengthSquared() > 1e-6f ? 1 : 0);
            if (missile.Trail.Visible)
            {
                var across = axis.Cross(Math.Abs(axis.Normalized().Y) > .9f ? Vector3.Right : Vector3.Up).Normalized();
                missile.Trail.GlobalTransform = new Transform3D(new Basis(across * .05f, axis, axis.Cross(across).Normalized() * .05f), (head + tail) / 2);
            }
            var blast = flying || !launched ? -1 : (age - missile.Arrival) / MissileBlastSeconds;
            Set(missile.Blast, blast is >= 0 and < 1 ? 1 - blast * blast : 0);
            if (missile.Blast.Visible)
                missile.Blast.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * MissileBlastRadius * (.35f + .65f * Mathf.Sqrt(blast))), head);
        }
    }
    private void LoadBurst(ShotEffect effect)
    {
        // Visual randomness is seeded by the discharge and never touches the simulation's draws.
        var random = new Random(unchecked((int)effect.Shot.Sequence * 7919 + 29));
        float Spread() => ((float)random.NextDouble() * 2 - 1) * TracerScatter;
        effect.Rounds = Enumerable.Range(0, TracerRounds).Select(i =>
        {
            var round = new TracerRound(i * (float)CombatRules.MachineGunBurstSeconds / TracerRounds,
                random.Next(2) == 0 ? TracerYellow : TracerOrange, new Vector3(Spread(), Spread(), Spread()));
            round.Streak = Beam(round.Color); round.Streak.Visible = false; round.Spark = Blob(SparkColor);
            return round;
        }).ToArray();
        effect.Flash = Blob(TracerYellow.Lerp(Colors.White, .5f));
    }
    // Each round leaves the muzzle where it is when fired. A hit flies to the live struck location with the discharge's flight time;
    // a miss sprays past its miss point to the ground, or fades beyond long range plus 10 hexes.
    private void LaunchRound(ShotEffect effect, TracerRound round)
    {
        var (from, to, _) = ShotPath(effect);
        round.Launched = true; round.Launch = from;
        if (effect.Shot.Hit) { round.Flight = (float)effect.Shot.FlightSeconds; return; }
        to += round.Scatter * 3;
        var direction = (to - from).Normalized();
        var reach = effect.Weapon!.Long * CombatRules.WeaponRangeScale + 10;
        var ground = Simulation!.Terrain.RayDistance(to, direction, Math.Max(0, reach - from.DistanceTo(to)));
        round.Grounded = ground is not null;
        round.MissEnd = ground is { } distance ? to + direction * distance : from + direction * reach;
        round.Flight = from.DistanceTo(round.MissEnd) / CombatRules.BulletSpeed;
    }
    // A shooter destroyed, shut down or stripped of the gun stops firing; rounds already in the air fly on.
    private void FireRounds(ShotEffect effect)
    {
        if (effect.Rounds is not { } rounds) return;
        var age = Age(effect);
        var attacker = Simulation!.Unit(effect.Shot.AttackerId);
        if (effect.CutOff is null && (attacker.Status is CombatStatus.Destroyed or CombatStatus.Shutdown
            || !attacker.WeaponIntact(attacker.Weapons.Single(weapon => weapon.Spec.Id == effect.Shot.WeaponId))))
            effect.CutOff = age;
        foreach (var round in rounds)
            if (!round.Launched && age >= round.Fired && round.Fired < (effect.CutOff ?? float.MaxValue)) LaunchRound(effect, round);
    }
    private void UpdateBurst(ShotEffect effect)
    {
        if (effect.Rounds is not { } rounds) return;
        var age = Age(effect);
        static void Set(MeshInstance3D mesh, float alpha)
        {
            mesh.Visible = alpha > 0;
            var material = (StandardMaterial3D)mesh.MaterialOverride;
            material.AlbedoColor = new Color(material.AlbedoColor, alpha);
        }
        var lastFired = -1f;
        foreach (var round in rounds)
        {
            if (!round.Launched) { Set(round.Streak, 0); Set(round.Spark, 0); continue; }
            lastFired = round.Fired;
            round.End = effect.Shot.Hit ? _models[effect.Shot.TargetId].LocationCenter(effect.Shot.Location) + round.Scatter : round.MissEnd;
            var flown = age - round.Fired;
            round.Head = round.Flight <= 0 ? round.End : round.Launch.Lerp(round.End, Math.Clamp(flown / round.Flight, 0, 1));
            // The streak trails its head back toward the muzzle, never past it.
            var direction = (round.End - round.Launch).Normalized();
            var tail = round.Head - direction * Math.Min(TracerLength, round.Head.DistanceTo(round.Launch));
            var flying = flown < round.Flight;
            var fade = flying ? 1 : effect.Shot.Hit || round.Grounded ? 0 : Math.Clamp(1 - (flown - round.Flight) / ShellFade, 0, 1);
            Set(round.Streak, tail.DistanceSquaredTo(round.Head) > 1e-6f ? fade : 0);
            if (round.Streak.Visible) round.Streak.GlobalTransform = Span(tail, round.Head, TracerRadius);
            var spark = effect.Shot.Hit && !flying ? (flown - round.Flight) / SparkSeconds : -1;
            Set(round.Spark, spark is >= 0 and < 1 ? 1 - spark : 0);
            if (round.Spark.Visible) round.Spark.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * SparkRadius), round.End);
        }
        // The muzzle re-flashes with every round.
        var flash = lastFired < 0 ? 0 : Math.Clamp(1 - (age - lastFired) / TracerFlashSeconds, 0, 1);
        Set(effect.Flash!, flash);
        if (flash > 0) effect.Flash!.GlobalTransform = new Transform3D(Basis.Identity.Scaled(Vector3.One * TracerFlashRadius),
            effect.Muzzle?.GlobalPosition ?? _models[effect.Shot.AttackerId].LocationCenter(effect.Mount));
    }
    internal IEnumerable<(CombatShot Shot, float Fired, Color Color, bool Launched, Vector3 Head, Vector3 End, MeshInstance3D Streak, bool Sparking)> Tracers() =>
        _effects.Where(effect => effect.Rounds is not null).SelectMany(effect => effect.Rounds!.Select(round =>
            (effect.Shot, round.Fired, round.Color, round.Launched, round.Head, round.End, round.Streak, round.Spark.Visible)));
    internal bool MuzzleFlashing(CombatShot shot) => _effects.Any(effect => effect.Shot == shot && effect.Rounds is not null && effect.Flash!.Visible);
    private void RemoveEffect(int index)
    {
        var effect = _effects[index];
        effect.Halo?.QueueFree(); effect.Core?.QueueFree();
        effect.Slug?.QueueFree(); effect.Flash?.QueueFree(); effect.Blast?.QueueFree(); effect.Burst?.QueueFree();
        foreach (var mesh in effect.PpcMeshes) mesh.QueueFree();
        foreach (var missile in effect.Salvo ?? [])
        { missile.Glow.QueueFree(); missile.Trail.QueueFree(); missile.Flash.QueueFree(); missile.Blast.QueueFree(); }
        foreach (var round in effect.Rounds ?? []) { round.Streak.QueueFree(); round.Spark.QueueFree(); }
        _effects.RemoveAt(index);
    }
    private float Age(ShotEffect effect) => (float)((Simulation?.Clock ?? effect.Start) - effect.Start);
    private void CreateMech(CombatUnit unit)
    {
        var model = MechModel.Create(unit.Spec, unit.Side);
        _world.AddChild(model); _models[unit.Id] = model;
    }
    private void SyncModels(float animationDelta = 0)
    {
        if (Simulation is null) return;
        _pickBounds.Clear();
        foreach (var unit in Simulation.Units)
        {
            var model = _models[unit.Id];
            model.Visible = unit.Side == CombatSide.Friendly || Simulation.Detected(CombatSide.Friendly, unit.Id);
            model.Position = World(unit.Position, Simulation.Terrain.HeightAt(unit.Position));
            model.Rotation = new Vector3(unit.Status == CombatStatus.Destroyed ? Mathf.Pi / 2 : 0,
                Mathf.Atan2(-unit.Facing.X, -unit.Facing.Y), 0);
            model.SetCondition(unit.Intact);
            model.SetTorsoTwist(unit.TorsoTwist);
            model.AnimateLocomotion(animationDelta, unit.Active ? unit.CurrentSpeed : 0,
                unit.WalkingSpeed, unit.RunningSpeed, unit.Active ? unit.AngularVelocity : 0);
            if (model.Visible && model.PickBounds() is { } bounds) _pickBounds[unit.Id] = bounds.Grow(.08f);
        }
    }
    public static Vector3 World(Vector2 point, float height = 0) => new(point.X, height, point.Y);
    public Vector2 ScreenPoint(Vector2 point, float height = 0) => Camera.UnprojectPosition(World(point, (Simulation?.Terrain.HeightAt(point) ?? 0) + height));
    public Vector2 GroundPoint(Vector2 screen)
    {
        var from = Camera.ProjectRayOrigin(screen); var direction = Camera.ProjectRayNormal(screen);
        if (Simulation?.Terrain.RayDistance(from, direction, Camera.Far) is { } distance)
        {
            var hit = from + direction * distance;
            return CombatRules.Clamp(new Vector2(hit.X, hit.Z));
        }
        var point = from + direction * (-from.Y / direction.Y);
        return CombatRules.Clamp(new Vector2(point.X, point.Z));
    }
    public void CancelDrag()
    {
        SetHovered(null);
        _selecting = _panning = _rotating = _rightPressed = false;
        if (_rightDragging)
        {
            _rightDragging = false;
            Input.MouseMode = _previousMouseMode;
            GetViewport().WarpMouse(_rightCursorStart);
        }
        QueueRedraw();
    }
    private void WindowFocusExited() { _windowFocused = false; CancelDrag(); }
    private void WindowFocusEntered() => _windowFocused = true;
    public override void _ExitTree()
    {
        CancelDrag();
        GetWindow().FocusExited -= WindowFocusExited;
        GetWindow().FocusEntered -= WindowFocusEntered;
    }
    // A captured mouse is routed through _Input rather than relying on GUI hit testing.
    // Consume the whole gesture, including releases outside the battlefield.
    public override void _Input(InputEvent input)
    {
        if (!_rightPressed) return;
        if (!CommandsEnabled || !IsVisibleInTree() || !_windowFocused) { CancelDrag(); return; }
        if (input is InputEventMouseMotion motion)
        {
            if (_rightDragging)
            {
                _yaw -= motion.ScreenRelative.X * OrbitSensitivity;
                _pitch = Math.Clamp(_pitch - motion.ScreenRelative.Y * OrbitSensitivity,
                    Mathf.DegToRad(15), Mathf.DegToRad(85));
                UpdateCamera();
            }
            else
            {
                _rightDisplacement += motion.ScreenRelative;
                if (_rightDisplacement.LengthSquared() > RightDragThreshold * RightDragThreshold)
                {
                    _rightDragging = true;
                    SetHovered(null);
                    _previousMouseMode = Input.MouseMode;
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                    // Discard the threshold movement so capture never jumps the camera.
                }
            }
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false } mouse)
        {
            var displacement = GetViewport().GetScreenTransform().BasisXform(mouse.Position - _rightCursorStart);
            var click = !_rightDragging && displacement.LengthSquared() <= RightDragThreshold * RightDragThreshold
                && GetGlobalRect().HasPoint(mouse.Position) && GetViewport().GuiGetHoveredControl() == this;
            CancelDrag();
            if (click)
            {
                var at = GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
                var target = PickMech(at);
                CommandRequested?.Invoke(GroundPoint(at), target?.Id);
            }
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton) GetViewport().SetInputAsHandled();
    }
    private void UpdateCamera()
    {
        _focus.Y = Simulation?.Terrain.HeightAt(new Vector2(_focus.X, _focus.Z)) ?? 0;
        var horizontal = OrbitRadius * Mathf.Cos(_pitch);
        Camera.Position = _focus + new Vector3(Mathf.Sin(_yaw) * horizontal,
            OrbitRadius * Mathf.Sin(_pitch), Mathf.Cos(_yaw) * horizontal);
        Camera.LookAt(_focus); QueueRedraw();
    }
    public void ResetToSelection()
    {
        if (Simulation is null) return;
        CancelDrag(); SyncModels();
        var group = Simulation.Units.Where(unit => unit.Side == CombatSide.Friendly && Selection.Contains(unit.Id)).ToArray();
        if (group.Length == 0) group = Simulation.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        if (group.Length == 0) return;
        _focus = World(new Vector2((group.Min(unit => unit.Position.X) + group.Max(unit => unit.Position.X)) / 2,
            (group.Min(unit => unit.Position.Y) + group.Max(unit => unit.Position.Y)) / 2));
        _yaw = 0; _pitch = DefaultPitch; Camera.Size = 50; UpdateCamera();
        // Fit the complete visible models, including hills and projecting weapons, in camera space.
        var extent = (Vector2)_viewport.Size / 2;
        var available = (extent - Vector2.One * 24).Max(Vector2.One);
        var scale = 1f;
        foreach (var unit in group)
        {
            var model = _models[unit.Id];
            if (model.PickBounds() is not { } bounds) continue;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = bounds.Position + new Vector3((corner & 1) == 0 ? 0 : bounds.Size.X,
                    (corner & 2) == 0 ? 0 : bounds.Size.Y, (corner & 4) == 0 ? 0 : bounds.Size.Z);
                var offset = (Camera.UnprojectPosition(model.GlobalTransform * local) - extent).Abs();
                scale = Math.Max(scale, Math.Max(offset.X / available.X, offset.Y / available.Y));
            }
        }
        Camera.Size = 50 * scale; QueueRedraw();
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || Simulation is null) return;
        if (HasFocus() && CommandsEnabled && _windowFocused)
        {
            var pan = Vector2.Zero;
            if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up)) pan.Y--;
            if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down)) pan.Y++;
            if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left)) pan.X--;
            if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) pan.X++;
            if (pan != Vector2.Zero) Pan(pan.Normalized() * (float)delta * Camera.Size * .5f);
            if (Input.IsPhysicalKeyPressed(Key.Q)) { _yaw -= (float)delta; UpdateCamera(); }
            if (Input.IsPhysicalKeyPressed(Key.E)) { _yaw += (float)delta; UpdateCamera(); }
        }
        SyncModels(!Simulation.Paused && Simulation.Outcome == CombatOutcome.Ongoing ? (float)delta : 0);
        // A destroyed shooter's beam vanishes at once. Bursts fire their due rounds first so their duration is current.
        foreach (var effect in _effects) FireRounds(effect);
        for (var i = _effects.Count - 1; i >= 0; i--)
            if (Age(_effects[i]) >= _effects[i].Duration
                || _effects[i].Laser is not null && Simulation.Unit(_effects[i].Shot.AttackerId).Status == CombatStatus.Destroyed)
                RemoveEffect(i);
        foreach (var effect in _effects) { UpdateBeam(effect); UpdateShell(effect); UpdatePpc(effect); UpdateSalvo(effect); UpdateBurst(effect); }
        for (var i = _destructions.Count - 1; i >= 0; i--)
            if (Age(_destructions[i]) >= DestructionSeconds) RemoveDestruction(i);
        foreach (var destruction in _destructions) UpdateDestruction(destruction);
        var hovering = CommandsEnabled && _windowFocused && !_selecting && !_panning && !_rotating && !_rightDragging
            && GetViewport().GuiGetHoveredControl() == this;
        SetHovered(hovering ? PickMech(GetLocalMousePosition())?.Id : null);
        QueueRedraw();
    }
    private void Pan(Vector2 amount)
    {
        var rotated = amount.Rotated(-_yaw);
        _focus.X = Math.Clamp(_focus.X + rotated.X, -CombatRules.ArenaHalfSize, CombatRules.ArenaHalfSize);
        _focus.Z = Math.Clamp(_focus.Z + rotated.Y, -CombatRules.ArenaHalfSize, CombatRules.ArenaHalfSize); UpdateCamera();
    }
    // Shots leave the weapon's source muzzle (or its location's center) and end at the rolled hit location.
    // Misses pass beside and beyond that location. A laser hit sweeps linearly from its first location to
    // the adjacent one over the whole burn; a miss holds beside its first location.
    private (Vector3 From, Vector3 To, MechLocation Location) ShotPath(ShotEffect effect)
    {
        var shot = effect.Shot;
        var from = effect.Muzzle?.GlobalPosition ?? _models[shot.AttackerId].LocationCenter(effect.Mount);
        var to = _models[shot.TargetId].LocationCenter(shot.Location);
        var sweep = effect.Laser is null ? 0 : Math.Clamp(Age(effect) / (float)CombatRules.LaserBurnSeconds, 0, 1);
        var location = sweep >= .5f && shot.SecondLocation is { } second ? second : shot.Location;
        var direction = new Vector3(to.X - from.X, 0, to.Z - from.Z).Normalized();
        var side = new Vector3(-direction.Z, 0, direction.X) * ((int)(shot.Roll * 100) % 2 == 0 ? 1 : -1);
        if (shot.Hit)
            return (from, shot.SecondLocation is { } next ? to.Lerp(_models[shot.TargetId].LocationCenter(next), sweep) : to, location);
        return (from, to + side * 1.2f + direction * 1.5f, location);
    }
    internal IEnumerable<(CombatShot Shot, Vector3 From, Vector3 To, MechLocation Location)> ShotPaths()
        => _effects.Select(effect => { var (from, to, location) = ShotPath(effect); return (effect.Shot, from, to, location); });
    internal IEnumerable<(CombatShot Shot, MeshInstance3D Halo, MeshInstance3D Core)> Beams() =>
        _effects.Where(effect => effect.Halo is not null).Select(effect => (effect.Shot, effect.Halo!, effect.Core!));
    private void UpdateBeam(ShotEffect effect)
    {
        if (effect.Laser is not { } kind) return;
        var (from, to, _) = ShotPath(effect);
        // The beam's front travels from the muzzle and reaches its full length after BeamReach.
        to = from.Lerp(to, Math.Clamp(Age(effect) / BeamReach, 0, 1));
        var alpha = Math.Clamp((effect.Duration - Age(effect)) / BeamFade, 0, 1);
        var axis = to - from;
        var visible = axis.LengthSquared() > 1e-6f && alpha > 0;
        foreach (var (beam, scale) in new[] { (effect.Halo!, 1f), (effect.Core!, .4f) })
        {
            beam.Visible = visible;
            if (!visible) continue;
            var radius = LaserRadius(kind) * scale;
            var across = axis.Cross(Math.Abs(axis.Normalized().Y) > .9f ? Vector3.Right : Vector3.Up).Normalized();
            var depth = axis.Cross(across).Normalized();
            beam.GlobalTransform = new Transform3D(new Basis(across * radius, axis, depth * radius), (from + to) / 2);
            var material = (StandardMaterial3D)beam.MaterialOverride;
            material.AlbedoColor = new Color(material.AlbedoColor, alpha);
        }
    }
    // One camera ray drives hover, selection, inspection, attack, priority targeting and Follow.
    internal CombatUnit? PickMech(Vector2 at)
    {
        if (Simulation is null || !new Rect2(Vector2.Zero, Size).HasPoint(at)) return null;
        var origin = Camera.ProjectRayOrigin(at); var direction = Camera.ProjectRayNormal(at);
        var nearest = Camera.Far;
        CombatUnit? picked = null;
        foreach (var unit in Simulation.Units)
        {
            if (unit.Side != CombatSide.Friendly && !Simulation.Detected(CombatSide.Friendly, unit.Id)) continue;
            if (!_pickBounds.TryGetValue(unit.Id, out var bounds)) continue;
            var inverse = _models[unit.Id].GlobalTransform.AffineInverse();
            // Keep the local direction unnormalized: its parameter still measures world distance.
            if (RayBoxDistance(inverse * origin, inverse.Basis * direction, bounds, nearest) is not { } hit) continue;
            nearest = hit; picked = unit;
        }
        return picked is not null && Simulation.Terrain.RayDistance(origin, direction, nearest) is null ? picked : null;
    }
    private static float? RayBoxDistance(Vector3 origin, Vector3 direction, Aabb bounds, float maximum)
    {
        var enter = 0f; var exit = maximum;
        for (var axis = 0; axis < 3; axis++)
        {
            if (Math.Abs(direction[axis]) < 1e-8f)
            {
                if (origin[axis] < bounds.Position[axis] || origin[axis] > bounds.End[axis]) return null;
                continue;
            }
            var a = (bounds.Position[axis] - origin[axis]) / direction[axis];
            var b = (bounds.End[axis] - origin[axis]) / direction[axis];
            enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
            if (enter > exit) return null;
        }
        return enter;
    }
    private void SetHovered(string? id)
    {
        if (HoveredMech == id) return;
        if (HoveredMech is { } previous && _models.TryGetValue(previous, out var old)) old.SetHovered(false);
        HoveredMech = id;
        if (id is not null && _models.TryGetValue(id, out var model)) model.SetHovered(true);
    }
    public IReadOnlyList<(CombatUnit Friendly, CombatUnit Enemy, float Distance)> HoverRanges => Simulation is not null
        && CommandsEnabled && HoveredMech is { } id && Simulation.Unit(id) is { Side: CombatSide.Enemy } enemy
        && Simulation.Detected(CombatSide.Friendly, id)
        ? Selection.Select(Simulation.Unit).Where(unit => unit.Side == CombatSide.Friendly)
            .Select(unit => (unit, enemy, unit.Position.DistanceTo(enemy.Position))).ToArray() : [];
    public override void _GuiInput(InputEvent input)
    {
        if (!CommandsEnabled || Simulation is null || !_windowFocused) return;
        if (input is InputEventMouseButton mouse)
        {
            GrabFocus();
            if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && mouse.Pressed)
            { Camera.Size = Math.Clamp(Camera.Size * (mouse.ButtonIndex == MouseButton.WheelUp ? .85f : 1.18f), 12, 280); AcceptEvent(); }
            if (mouse.ButtonIndex == MouseButton.Middle)
            { _panning = mouse.Pressed && !mouse.ShiftPressed; _rotating = mouse.Pressed && mouse.ShiftPressed; SetHovered(null); AcceptEvent(); }
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed) { _selecting = true; _dragStart = _dragEnd = mouse.Position; SetHovered(null); }
                else if (_selecting)
                {
                    _selecting = false;
                    if (_dragStart.DistanceTo(mouse.Position) < 6)
                    {
                        var picked = PickMech(mouse.Position);
                        var friendly = picked?.Side == CombatSide.Friendly ? picked : null;
                        InspectedEnemy = picked?.Side == CombatSide.Enemy ? picked.Id : null;
                        // Inspection keeps the friendly selection as the range reference and command group.
                        SelectionRequested?.Invoke(friendly is not null ? [friendly.Id]
                            : InspectedEnemy is not null ? Selection.ToArray() : Array.Empty<string>(), mouse.ShiftPressed && InspectedEnemy is null);
                    }
                    else
                    {
                        InspectedEnemy = null;
                        var rect = new Rect2(_dragStart, mouse.Position - _dragStart).Abs();
                        SelectionRequested?.Invoke(Simulation.Units.Where(unit => unit.Side == CombatSide.Friendly
                            && rect.HasPoint(ScreenPoint(unit.Position, 1))).Select(unit => unit.Id).ToArray(), mouse.ShiftPressed);
                    }
                }
                AcceptEvent();
            }
            if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed)
            {
                CancelDrag(); _rightPressed = true;
                _rightCursorStart = GetGlobalTransformWithCanvas() * mouse.Position;
                _rightDisplacement = Vector2.Zero;
                AcceptEvent();
            }
        }
        if (input is InputEventMouseMotion motion)
        {
            if (_selecting) _dragEnd = motion.Position;
            if (_panning) Pan(-motion.Relative * Camera.Size / Math.Max(1, Size.Y));
            if (_rotating) { _yaw -= motion.Relative.X * .008f; UpdateCamera(); }
        }
        // Arrow navigation belongs to the camera, not Control's directional focus traversal.
        if (input is InputEventKey { Pressed: true, Keycode: Key.Up or Key.Down or Key.Left or Key.Right }) AcceptEvent();
        if (input is InputEventKey { Pressed: true, Keycode: Key.Home })
        { CancelDrag(); _focus = Vector3.Zero; _yaw = 0; _pitch = DefaultPitch; Camera.Size = 220; UpdateCamera(); AcceptEvent(); }
    }
    private CombatUnit? RangeTarget => Simulation is not null && InspectedEnemy is { } id && Simulation.Detected(CombatSide.Friendly, id) ? Simulation.Unit(id) : null;
    public IReadOnlyList<(CombatUnit Observer, float Identification, float? Detail)> DetectionRanges
    {
        get
        {
            if (Simulation is null) return [];
            var target = RangeTarget;
            return Selection.Select(Simulation.Unit).Where(unit => unit.Side == CombatSide.Friendly && unit.Active).Select(observer =>
            {
                var radius = target is null ? CombatRules.DetectionRadius(observer.Warrior.Sensors, 100) : CombatRules.DetectionRadius(observer, target);
                return (observer, radius, target is null ? (float?)null : radius * .5f);
            }).ToArray();
        }
    }
    public string SensorRangeText => RangeText(DetectionRanges);
    private string RangeText(IReadOnlyList<(CombatUnit Observer, float Identification, float? Detail)> ranges)
    {
        if (ranges.Count == 0) return "";
        if (ranges.Count == 1)
        {
            var (observer, identification, detail) = ranges[0];
            return RangeTarget is { } target
                ? $"{observer.Warrior.Callsign} · {target.Spec.Chassis}: identify {identification:0.##} hexes (blue) · status {detail:0.##} (gold)"
                : $"{observer.Warrior.Callsign} · 20–100t: identify {CombatRules.DetectionRadius(observer.Warrior.Sensors, 20):0.##}–{identification:0.##} hexes · circle: 100t";
        }
        var identificationSpan = $"{ranges.Min(range => range.Identification):0.##}–{ranges.Max(range => range.Identification):0.##}";
        return RangeTarget is { } inspected
            ? $"Selected mechs · {inspected.Spec.Chassis}: identify {identificationSpan} hexes (blue) · status {ranges.Min(range => range.Detail):0.##}–{ranges.Max(range => range.Detail):0.##} (gold)"
            : $"Selected mechs · 100t: identify {identificationSpan} hexes (blue) · 20–100t: {ranges.Min(range => CombatRules.DetectionRadius(range.Observer.Warrior.Sensors, 20)):0.##}–{ranges.Max(range => range.Identification):0.##} hexes";
    }
    private void DrawRangeUnion(IReadOnlyList<CircleUnion.Circle> circles, Color color)
    {
        foreach (var arc in CircleUnion.Boundary(circles))
        {
            var segments = Math.Max(1, Mathf.CeilToInt((arc.End - arc.Start) * 96 / Mathf.Tau));
            DrawPolyline(Enumerable.Range(0, segments + 1).Select(segment => ScreenPoint(arc.Center
                + Vector2.FromAngle(Mathf.Lerp(arc.Start, arc.End, (float)segment / segments)) * arc.Radius)).ToArray(),
                new Color(color, .45f), 1, true);
        }
    }
    public override void _Draw()
    {
        if (Simulation is null || Camera is null) return;
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(12, 18), $"Terrain seed: {Simulation.Terrain.Seed}", HorizontalAlignment.Left, -1, 12, Ui.Gold);
        DrawString(font, new Vector2(12, 34), "Rocks: blocked · Green: soft cover · Blue: slow water · Tan: steep", HorizontalAlignment.Left, -1, 11, Ui.Text);
        var center = ScreenPoint(Vector2.Zero);
        DrawString(font, center + new Vector2(-42, -8), "ARENA CENTER", HorizontalAlignment.Left, -1, 12, new Color(Ui.Muted, .7f));
        var ranges = DetectionRanges;
        if (ranges.Count > 0)
        {
            DrawRangeUnion(ranges.Select(range => new CircleUnion.Circle(range.Observer.Position, range.Identification)).ToArray(), new Color("7fb8bf"));
            if (ranges[0].Detail is not null)
                DrawRangeUnion(ranges.Select(range => new CircleUnion.Circle(range.Observer.Position, range.Detail!.Value)).ToArray(), Ui.Gold);
            DrawString(font, new Vector2(12, 50), RangeText(ranges), HorizontalAlignment.Left, -1, 11, Ui.Text);
        }
        var friendlyIndex = 0;
        foreach (var unit in Simulation.Units)
        {
            var friendly = unit.Side == CombatSide.Friendly;
            if (friendly) friendlyIndex++;
            if (!friendly && !Simulation.Detected(CombatSide.Friendly, unit.Id)) continue;
            var at = ScreenPoint(unit.Position);
            var color = MechModel.TeamColor(unit.Side);
            var baseRadius = _models[unit.Id].Scale.X * .95f;
            DrawPolyline(Enumerable.Range(0, 49).Select(segment => ScreenPoint(unit.Position
                + Vector2.FromAngle(segment * Mathf.Tau / 48) * baseRadius, .015f)).ToArray(), new Color(color, .7f), 1, true);
            if (!unit.Active) color = Ui.Muted;
            if (Selection.Contains(unit.Id))
            {
                DrawArc(at, Math.Max(10, 1.15f * Size.Y / Camera.Size), 0, Mathf.Tau, 32, Ui.Gold, 2, true);
                DrawCircle(at, 3, Ui.Gold);
                var to = unit.Order == CombatOrder.Move ? unit.Destination : unit.Order == CombatOrder.Follow && unit.FollowTarget is { } leader
                    ? Simulation.Unit(leader).Position : unit.AttackTarget is { } id
                    && Simulation.Contacts(unit.Side).TryGetValue(id, out var contact) ? contact.LastKnownPosition : unit.Position;
                if (to.DistanceTo(unit.Position) > .4f) { var end = ScreenPoint(to); DrawLine(at, end, new Color(Ui.Gold, .65f), 1, true); DrawArc(end, 5, 0, Mathf.Tau, 16, Ui.Gold); }
                if (unit.PriorityTarget is { } priority && Simulation.Contacts(unit.Side).TryGetValue(priority, out var preferred))
                    DrawArc(ScreenPoint(preferred.LastKnownPosition), 16, 0, Mathf.Tau, 24, Ui.Gold, 1, true);
            }
            var forward = ScreenPoint(unit.Position + unit.Facing * 1.5f);
            DrawLine(at, forward, color, 2, true);
            var torso = ScreenPoint(unit.Position + unit.TorsoFacing * 2.1f);
            DrawLine(at, torso, Ui.Gold, 2, true);
            DrawCircle(torso, 2.5f, Ui.Gold);
            var label = ScreenPoint(unit.Position, _models[unit.Id].Height + .3f);
            var text = friendly ? Selection.Count == 1 && Selection.Contains(unit.Id) ? $"{friendlyIndex} · {unit.Warrior.Callsign}" : friendlyIndex.ToString()
                : InspectedEnemy == unit.Id && Simulation.DetailsVisible(CombatSide.Friendly, unit.Id) ? unit.Spec.DisplayName : unit.Spec.Chassis;
            var labelOffset = new Vector2(-font.GetStringSize(text, fontSize: 12).X / 2, -4);
            DrawStringOutline(font, label + labelOffset, text, HorizontalAlignment.Left, -1, 12, 4, new Color("101820"));
            DrawString(font, label + labelOffset, text, HorizontalAlignment.Left, -1, 12, color);
            if (friendly)
            {
                var heatColor = unit.Shutdown ? new Color("ed735e") : unit.Heat >= CombatRules.HotHeat
                    ? new Color("e7a185") : unit.Heat >= CombatRules.WarmHeat ? Ui.Gold : Ui.Muted;
                var heatLabel = $"H {unit.Heat:0.0}";
                var heatAt = label + new Vector2(-font.GetStringSize(heatLabel, fontSize: 11).X / 2, 10);
                DrawStringOutline(font, heatAt, heatLabel, HorizontalAlignment.Left, -1, 11, 4, new Color("101820"));
                DrawString(font, heatAt, heatLabel, HorizontalAlignment.Left, -1, 11, heatColor);
                DrawRect(new Rect2(label + new Vector2(-15, 13), new Vector2(30, 3)), new Color("101820"));
                DrawRect(new Rect2(label + new Vector2(-15, 13), new Vector2(30 * (float)Math.Clamp(unit.Heat / CombatRules.ShutdownHeat, 0, 1), 3)), heatColor);
            }
        }
        foreach (var contact in Simulation.Contacts(CombatSide.Friendly).Values.Where(item => !item.Detected))
        {
            var at = ScreenPoint(contact.LastKnownPosition);
            DrawArc(at, 10, 0, Mathf.Tau, 16, Ui.Muted, 1, true);
            DrawString(font, at + new Vector2(13, 4), $"? Last seen {Math.Max(0, Simulation.Elapsed - contact.LastSeenAt):0}s", HorizontalAlignment.Left, -1, 12, Ui.Muted);
        }
        var rangeIndex = 0;
        foreach (var (friendly, enemy, distance) in HoverRanges)
        {
            var from = ScreenPoint(friendly.Position, BattlefieldTerrain.EyeHeight);
            var to = ScreenPoint(enemy.Position, BattlefieldTerrain.EyeHeight);
            var color = new Color("9cdae3");
            DrawDashedLine(from, to, new Color(color, .9f), 2, 7, true);
            var label = (from + to) / 2 + new Vector2(6, -6 - rangeIndex++ * 16);
            var text = FormattableString.Invariant($"{distance:0.0} hex");
            DrawStringOutline(font, label, text, HorizontalAlignment.Left, -1, 12, 5, new Color("101820"));
            DrawString(font, label, text, HorizontalAlignment.Left, -1, 12, color);
        }
        foreach (var effect in _effects)
        {
            var shot = effect.Shot; var age = Age(effect);
            var (from, to, _) = ShotPath(effect);
            if (effect.Laser is { } kind)
            {
                // Each struck location flashes a ring in the laser's color as the burn reaches it.
                if (!shot.Hit) continue;
                ImpactRing(_models[shot.TargetId].LocationCenter(shot.Location), age - BeamReach, LaserColor(kind));
                if (effect.BurnLanded is { } landed && shot.SecondLocation is { } second)
                    ImpactRing(_models[shot.TargetId].LocationCenter(second), (float)(Simulation.Clock - landed), LaserColor(kind));
                continue;
            }
            if (effect.Shell is not null || effect.Salvo is not null || effect.Ppc || effect.Rounds is not null) continue;
            var color = new Color(shot.Hit ? Ui.Gold : Ui.Muted, 1 - age / ShotLife);
            DrawLine(Camera.UnprojectPosition(from), Camera.UnprojectPosition(to), color, 1, true);
            if (shot.Hit) ImpactRing(to, age, Ui.Gold);
        }
        void ImpactRing(Vector3 at, float age, Color color)
        {
            var fade = 1 - age / ShotLife;
            if (age >= 0 && fade > 0) DrawArc(Camera.UnprojectPosition(at), (1 - fade) * 14 + 3, 0, Mathf.Tau, 16, new Color(color, fade), 2, true);
        }
        if (_selecting && _dragStart.DistanceTo(_dragEnd) > 5)
        { var rect = new Rect2(_dragStart, _dragEnd - _dragStart).Abs(); DrawRect(rect, new Color(Ui.Gold, .12f)); DrawRect(rect, Ui.Gold, false); }
    }
}
