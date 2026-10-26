using Godot;

namespace Mech;

public enum PreviewGait { Idle, Walk, Run }

/// <summary>A rotating view of the shared combat geometry with chassis gait previews.</summary>
public partial class MechPreview : Control
{
    public MechModel? Model { get; private set; }
    public PreviewGait Gait { get; set; }
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
        var aspect = Math.Max(.1f, Size.X / Math.Max(1, Size.Y));
        var bounds = Model is null ? new Aabb(Vector3.Zero, new(2, 3, 2))
            : new Aabb(Model.RestBounds.Position * Model.Scale, Model.RestBounds.Size * Model.Scale);
        var target = new Vector3(0, (Model?.Height ?? 3) * .48f, 0);
        var direction = new Vector3(.40f, .18f, -.90f).Normalized();
        var right = Vector3.Up.Cross(direction).Normalized(); var up = direction.Cross(right);
        var tangent = Mathf.Tan(Mathf.DegToRad(_camera.Fov / 2));
        var distance = 0f;
        void Fit(Vector3 point)
        {
            var relative = point - target;
            distance = Math.Max(distance, relative.Dot(direction) + Math.Max(
                Math.Abs(relative.Dot(right)) / (tangent * aspect), Math.Abs(relative.Dot(up)) / tangent));
        }
        // Fit every turntable orientation, including launcher width, cannon depth and the floor.
        for (var angle = 0; angle < 32; angle++)
        {
            for (var corner = 0; corner < 8; corner++)
                Fit(bounds.GetEndpoint(corner).Rotated(Vector3.Up, angle * Mathf.Tau / 32));
            Fit(new Vector3(Mathf.Cos(angle * Mathf.Tau / 32) * 1.75f, -.11f, Mathf.Sin(angle * Mathf.Tau / 32) * 1.75f));
        }
        _camera.Position = target + direction * (distance * 1.10f + .1f);
        _camera.LookAt(target);
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || Model is null || _owned is null) return;
        Model.RotateY((float)delta * .28f);
        var walk = CombatRules.MovementSpeed(_owned.Spec, MovementMode.Walk);
        var run = CombatRules.MovementSpeed(_owned.Spec, MovementMode.Run);
        Model.AnimateLocomotion((float)delta, Gait == PreviewGait.Walk ? walk : Gait == PreviewGait.Run ? run : 0, walk, run);
    }
}
