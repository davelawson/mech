using Godot;

namespace Mech;

public enum PreviewGait { Idle, Walk, Run }

/// <summary>A view of the shared combat geometry with chassis gait previews: a turntable, or a front-on hangar blueprint.</summary>
public partial class MechPreview : Control
{
    public const float SpinDegreesPerPixel = .5f, SpringBackSeconds = .3f;
    public MechModel? Model { get; private set; }
    public PreviewGait Gait { get; set; }
    /// <summary>Front-on, tinted and gridded; turns only while dragged and springs back to the front when released.</summary>
    public bool Blueprint { get; init; }
    public float Yaw { get; private set; }
    private Node3D _world = null!;
    private Camera3D _camera = null!;
    private OwnedMech? _owned;
    private MeshInstance3D _floor = null!, _ring = null!;
    private float _springFrom, _springTime = -1;
    private static readonly Color BlueprintIntact = new("5fc8e8"), BlueprintDamaged = new("e8a33c");
    private static readonly Dictionary<Color, ShaderMaterial> Overlays = [];
    private static Shader? _overlayShader;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ClipContents = true;
        var surface = new NativeViewportContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(surface);
        var viewport = new SubViewport { OwnWorld3D = true, TransparentBg = true, GuiDisableInput = true,
            Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
            HandleInputLocally = false };
        surface.AddChild(viewport); _world = new Node3D(); viewport.AddChild(_world);
        _camera = new Camera3D { Current = true, Fov = 32, Near = 1.5f, Far = 1200 };
        _world.AddChild(_camera);
        _world.AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("c5cec0"), AmbientLightEnergy = .7f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        } });
        _world.AddChild(new DirectionalLight3D { RotationDegrees = new(-42, -32, 0), LightEnergy = 1.5f });
        _world.AddChild(new DirectionalLight3D { RotationDegrees = new(-20, 145, 0), LightColor = new("a4bec9"), LightEnergy = .7f });
        var floor = new MeshInstance3D { Position = new(0, -.9f, 0),
            Mesh = new CylinderMesh { TopRadius = 26.25f, BottomRadius = 26.25f, Height = 1.5f, RadialSegments = 64 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new("26302b"), Roughness = .9f } };
        _world.AddChild(floor); _floor = floor;
        var ring = new ImmediateMesh();
        ring.SurfaceBegin(Mesh.PrimitiveType.Lines, new StandardMaterial3D
        { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = MechModel.TeamColor(CombatSide.Friendly) });
        for (var i = 0; i < 64; i++)
        {
            foreach (var angle in new[] { i * Mathf.Tau / 64, (i + 1) * Mathf.Tau / 64 })
                ring.SurfaceAddVertex(new(Mathf.Cos(angle) * 24.75f, .075f, Mathf.Sin(angle) * 24.75f));
        }
        ring.SurfaceEnd(); _ring = new MeshInstance3D { Mesh = ring }; _world.AddChild(_ring);
        _floor.Visible = _ring.Visible = !Blueprint;
        Resized += FrameModel; Resized += QueueRedraw; FrameModel();
    }
    public override void _Draw()
    {
        if (!Blueprint) return;
        var minor = new Color(BlueprintIntact, .06f); var major = new Color(BlueprintIntact, .13f);
        for (var i = 0; i * 24 <= Size.X; i++) DrawLine(new(i * 24, 0), new(i * 24, Size.Y), i % 5 == 0 ? major : minor);
        for (var i = 0; i * 24 <= Size.Y; i++) DrawLine(new(0, i * 24), new(Size.X, i * 24), i % 5 == 0 ? major : minor);
    }
    /// <summary>Turns the blueprint by a horizontal drag distance in pixels.</summary>
    public void Spin(float pixels)
    {
        if (Model is null) return;
        _springTime = -1; Yaw += Mathf.DegToRad(pixels * SpinDegreesPerPixel); Model.Rotation = new(0, Yaw, 0);
    }
    /// <summary>Eases the blueprint back to front-on by the shorter direction.</summary>
    public void ReleaseSpin()
    {
        Yaw = Mathf.Wrap(Yaw, -Mathf.Pi, Mathf.Pi); _springFrom = Yaw; _springTime = 0;
        if (Yaw == 0) _springTime = -1;
    }
    /// <summary>A preview-only mount map (the debug hardpoint cycle); null shows the mech's resolved loadout.</summary>
    public MountMap? MountOverride { get; private set; }
    public void Display(OwnedMech mech)
    {
        if (!ReferenceEquals(_owned, mech)) { _owned = mech; MountOverride = null; Rebuild(); }
        Model!.SetCondition(mech.LocationIntact);
        if (Blueprint) { Yaw = 0; _springTime = -1; Model.Rotation = Vector3.Zero; Tint(mech); }
        FrameModel();
    }
    /// <summary>Rebuilds the current mech with different hardpoint visuals, without touching its owned state.</summary>
    public void ShowMounts(MountMap? mounts)
    {
        if (_owned is null) return;
        MountOverride = mounts; Rebuild(); Display(_owned);
    }
    private void Rebuild()
    {
        if (Model is not null) { _world.RemoveChild(Model); Model.QueueFree(); }
        Model = MechModel.Create(_owned!.Spec, CombatSide.Friendly, MountOverride); _world.AddChild(Model);
    }
    private void Tint(OwnedMech mech)
    {
        foreach (var (location, root) in Model!.Locations)
        {
            var spec = mech.Spec.Locations.Single(item => item.Location == location);
            var state = mech.Locations.Single(item => item.Location == location);
            var damaged = state.Armor < spec.Armor || state.RearArmor < spec.RearArmor || state.Structure < spec.Structure;
            var overlay = Overlay(damaged ? BlueprintDamaged : BlueprintIntact);
            foreach (var mesh in root.FindChildren("*", "GeometryInstance3D", true, false).OfType<GeometryInstance3D>())
                mesh.MaterialOverlay = overlay;
        }
    }
    /// <summary>An overlay pass, so the model's own materials and outline stay untouched underneath.</summary>
    private static ShaderMaterial Overlay(Color color)
    {
        if (Overlays.TryGetValue(color, out var material)) return material;
        _overlayShader ??= new Shader { Code = """
            shader_type spatial;
            render_mode unshaded, blend_mix, depth_draw_never, shadows_disabled;
            uniform vec4 tint : source_color;
            varying float height;
            void vertex() { height = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y; }
            void fragment() {
                float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
                float contour = step(0.93, fract(height / 3.0));
                ALBEDO = mix(tint.rgb * 0.45, tint.rgb * 1.5, max(rim, contour * 0.6));
                ALPHA = clamp(0.72 + rim * 0.28 + contour * 0.15, 0.0, 1.0);
            }
            """ };
        material = new ShaderMaterial { Shader = _overlayShader };
        material.SetShaderParameter("tint", color);
        Overlays[color] = material; return material;
    }
    private void FrameModel()
    {
        if (_camera is null) return;
        var aspect = Math.Max(.1f, Size.X / Math.Max(1, Size.Y));
        var bounds = Model is null ? new Aabb(Vector3.Zero, new(30, 45, 30))
            : new Aabb(Model.RestBounds.Position * Model.Scale, Model.RestBounds.Size * Model.Scale);
        var target = Blueprint ? bounds.GetCenter() with { X = 0, Z = 0 } : new Vector3(0, (Model?.Height ?? 45) * .48f, 0);
        var direction = (Blueprint ? new Vector3(0, .08f, -1) : new Vector3(.40f, .18f, -.90f)).Normalized();
        var right = Vector3.Up.Cross(direction).Normalized(); var up = direction.Cross(right);
        var tangent = Mathf.Tan(Mathf.DegToRad(_camera.Fov / 2));
        var distance = 0f;
        void Fit(Vector3 point)
        {
            var relative = point - target;
            distance = Math.Max(distance, relative.Dot(direction) + Math.Max(
                Math.Abs(relative.Dot(right)) / (tangent * aspect), Math.Abs(relative.Dot(up)) / tangent));
        }
        // The blueprint fits its front view; the turntable fits every orientation, including launcher width, cannon depth and the floor.
        if (Blueprint) for (var corner = 0; corner < 8; corner++) Fit(bounds.GetEndpoint(corner));
        else for (var angle = 0; angle < 32; angle++)
        {
            for (var corner = 0; corner < 8; corner++)
                Fit(bounds.GetEndpoint(corner).Rotated(Vector3.Up, angle * Mathf.Tau / 32));
            Fit(new Vector3(Mathf.Cos(angle * Mathf.Tau / 32) * 26.25f, -1.65f, Mathf.Sin(angle * Mathf.Tau / 32) * 26.25f));
        }
        _camera.Position = target + direction * (distance * (Blueprint ? 1.04f : 1.10f) + 1.5f);
        _camera.LookAt(target);
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || Model is null || _owned is null) return;
        if (!Blueprint) Model.RotateY((float)delta * .28f);
        else if (_springTime >= 0)
        {
            _springTime = Math.Min(SpringBackSeconds, _springTime + (float)delta);
            var t = _springTime / SpringBackSeconds;
            Yaw = _springFrom * (1 - t * t * (3 - 2 * t)); Model.Rotation = new(0, Yaw, 0);
            if (t >= 1) { Yaw = 0; _springTime = -1; }
        }
        var walk = CombatRules.MovementSpeed(_owned.Spec, MovementMode.Walk);
        var run = CombatRules.MovementSpeed(_owned.Spec, MovementMode.Run);
        Model.AnimateLocomotion((float)delta, Gait == PreviewGait.Walk ? walk : Gait == PreviewGait.Run ? run : 0, walk, run);
    }
}
