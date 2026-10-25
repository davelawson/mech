using Godot;

namespace Mech;

/// <summary>A passive, rotating view of the same location-owned geometry used in combat.</summary>
public partial class MechPreview : Control
{
    public MechModel? Model { get; private set; }
    private Node3D _world = null!;
    private Camera3D _camera = null!;
    private OwnedMech? _owned;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ClipContents = true;
        var surface = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(surface);
        var viewport = new SubViewport { OwnWorld3D = true, TransparentBg = true, GuiDisableInput = true,
            Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
            HandleInputLocally = false };
        surface.AddChild(viewport); _world = new Node3D(); viewport.AddChild(_world);
        _camera = new Camera3D { Current = true, Fov = 32, Near = .1f, Far = 80 };
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
        var floor = new MeshInstance3D { Position = new(0, -.06f, 0),
            Mesh = new CylinderMesh { TopRadius = 1.75f, BottomRadius = 1.75f, Height = .1f, RadialSegments = 64 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new("26302b"), Roughness = .9f } };
        _world.AddChild(floor);
        var ring = new ImmediateMesh();
        ring.SurfaceBegin(Mesh.PrimitiveType.Lines, new StandardMaterial3D
        { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = MechModel.TeamColor(CombatSide.Friendly) });
        for (var i = 0; i < 64; i++)
        {
            foreach (var angle in new[] { i * Mathf.Tau / 64, (i + 1) * Mathf.Tau / 64 })
                ring.SurfaceAddVertex(new(Mathf.Cos(angle) * 1.65f, .005f, Mathf.Sin(angle) * 1.65f));
        }
        ring.SurfaceEnd(); _world.AddChild(new MeshInstance3D { Mesh = ring });
        Resized += FrameModel; FrameModel();
    }
    public void Display(OwnedMech mech)
    {
        if (!ReferenceEquals(_owned, mech))
        {
            if (Model is not null) { _world.RemoveChild(Model); Model.QueueFree(); }
            Model = MechModel.Create(mech.Spec, CombatSide.Friendly); _world.AddChild(Model); _owned = mech;
        }
        Model!.SetCondition(mech.LocationIntact); FrameModel();
    }
    private void FrameModel()
    {
        if (_camera is null) return;
        var aspect = Math.Max(.35f, Size.X / Math.Max(1, Size.Y));
        var height = Math.Max(3.4f, (Model?.Height ?? 3) + .45f);
        var distance = height / (2 * Mathf.Tan(Mathf.DegToRad(_camera.Fov / 2)) * Math.Min(1, aspect));
        var target = new Vector3(0, height * .44f, 0);
        _camera.Position = target + new Vector3(.40f, .18f, -.90f).Normalized() * distance;
        _camera.LookAt(target);
    }
    public override void _Process(double delta)
    {
        if (IsVisibleInTree() && Model is not null) Model.RotateY((float)delta * .28f);
    }
}
