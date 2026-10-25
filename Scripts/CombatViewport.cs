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
    public bool CommandsEnabled
    {
        get => _commandsEnabled;
        set { _commandsEnabled = value; if (!value) CancelDrag(); }
    }
    public Camera3D Camera { get; private set; } = null!;
    private SubViewport _viewport = null!;
    private Node3D _world = null!;
    private readonly Dictionary<string, MechModel> _models = [];
    private readonly List<(CombatShot Shot, float Life)> _effects = [];
    private Vector3 _focus = new(0, 0, 12);
    private static readonly float DefaultPitch = Mathf.Atan2(80, 70);
    private static readonly float OrbitRadius = new Vector2(70, 80).Length();
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
        Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 50, Far = 400, Current = true };
        _world.AddChild(Camera);
        var light = new DirectionalLight3D { RotationDegrees = new Vector3(-60, -30, 0), LightEnergy = 1.1f };
        _world.AddChild(light);
        _world.AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("111b21"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("b1c1cc"), AmbientLightEnergy = .65f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        } });
        Box(_world, new Vector3(0, -.15f, 0), new Vector3(96, .2f, 96), new Color("26363b"));
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, new StandardMaterial3D
        { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color("3b5055") });
        for (var n = -48; n <= 48; n += 8)
        {
            mesh.SurfaceAddVertex(new Vector3(n, 0, -48)); mesh.SurfaceAddVertex(new Vector3(n, 0, 48));
            mesh.SurfaceAddVertex(new Vector3(-48, 0, n)); mesh.SurfaceAddVertex(new Vector3(48, 0, n));
        }
        mesh.SurfaceEnd(); _world.AddChild(new MeshInstance3D { Mesh = mesh });
        Resized += QueueRedraw;
        FocusExited += CancelDrag;
        VisibilityChanged += () => { if (!IsVisibleInTree()) CancelDrag(); };
        GetWindow().FocusExited += WindowFocusExited;
        GetWindow().FocusEntered += WindowFocusEntered;
        UpdateCamera();
    }
    public void Display(CombatSimulation simulation)
    {
        if (Simulation is not null) Simulation.ShotFired -= OnShot;
        foreach (var model in _models.Values) { _world.RemoveChild(model); model.QueueFree(); }
        _models.Clear(); _effects.Clear();
        Simulation = simulation; Simulation.ShotFired += OnShot;
        _focus = new Vector3(0, 0, 12); _yaw = 0; _pitch = DefaultPitch; Camera.Size = 50; UpdateCamera();
        foreach (var unit in simulation.Units) CreateMech(unit);
        CancelDrag(); SyncModels(); QueueRedraw();
    }
    private void OnShot(CombatShot shot)
    {
        if (Simulation is null) return;
        if (Simulation.Unit(shot.AttackerId).Side == CombatSide.Friendly || Simulation.Detected(CombatSide.Friendly, shot.AttackerId))
            _effects.Add((shot, .45f));
    }
    private static MeshInstance3D Box(Node3D parent, Vector3 position, Vector3 size, Color color)
    {
        var part = new MeshInstance3D { Position = position, Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = .85f } };
        parent.AddChild(part); return part;
    }
    private void CreateMech(CombatUnit unit)
    {
        var model = MechModel.Create(unit.Spec, unit.Side);
        _world.AddChild(model); _models[unit.Id] = model;
    }
    private void SyncModels()
    {
        if (Simulation is null) return;
        foreach (var unit in Simulation.Units)
        {
            var model = _models[unit.Id];
            model.Visible = unit.Side == CombatSide.Friendly || Simulation.Detected(CombatSide.Friendly, unit.Id);
            model.Position = World(unit.Position);
            model.Rotation = new Vector3(unit.Status == CombatStatus.Destroyed ? Mathf.Pi / 2 : 0,
                Mathf.Atan2(-unit.Facing.X, -unit.Facing.Y), 0);
            model.SetCondition(unit.Intact);
        }
    }
    public static Vector3 World(Vector2 point, float height = 0) => new(point.X, height, point.Y);
    public Vector2 ScreenPoint(Vector2 point, float height = 0) => Camera.UnprojectPosition(World(point, height));
    public Vector2 GroundPoint(Vector2 screen)
    {
        var from = Camera.ProjectRayOrigin(screen); var direction = Camera.ProjectRayNormal(screen);
        var point = from + direction * (-from.Y / direction.Y);
        return CombatRules.Clamp(new Vector2(point.X, point.Z));
    }
    public void CancelDrag()
    {
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
                && GetGlobalRect().HasPoint(mouse.Position);
            CancelDrag();
            if (click)
            {
                var at = GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
                var target = Pick(at, CombatSide.Enemy);
                CommandRequested?.Invoke(GroundPoint(at), target?.Id);
            }
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton) GetViewport().SetInputAsHandled();
    }
    private void UpdateCamera()
    {
        var horizontal = OrbitRadius * Mathf.Cos(_pitch);
        Camera.Position = _focus + new Vector3(Mathf.Sin(_yaw) * horizontal,
            OrbitRadius * Mathf.Sin(_pitch), Mathf.Cos(_yaw) * horizontal);
        Camera.LookAt(_focus); QueueRedraw();
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
        for (var i = _effects.Count - 1; i >= 0; i--)
        { var effect = _effects[i]; _effects[i] = (effect.Shot, effect.Life - (float)delta); if (_effects[i].Life <= 0) _effects.RemoveAt(i); }
        SyncModels(); QueueRedraw();
    }
    private void Pan(Vector2 amount)
    {
        var rotated = amount.Rotated(-_yaw);
        _focus.X = Math.Clamp(_focus.X + rotated.X, -48, 48);
        _focus.Z = Math.Clamp(_focus.Z + rotated.Y, -48, 48); UpdateCamera();
    }
    private CombatUnit? Pick(Vector2 at, CombatSide side) => Simulation?.Units
        .Where(unit => unit.Side == side && (side == CombatSide.Friendly || Simulation.Detected(CombatSide.Friendly, unit.Id)))
        .Where(unit => ScreenPoint(unit.Position, 1).DistanceTo(at) < 20)
        .OrderBy(unit => ScreenPoint(unit.Position, 1).DistanceSquaredTo(at)).FirstOrDefault();
    public override void _GuiInput(InputEvent input)
    {
        if (!CommandsEnabled || Simulation is null || !_windowFocused) return;
        if (input is InputEventMouseButton mouse)
        {
            GrabFocus();
            if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && mouse.Pressed)
            { Camera.Size = Math.Clamp(Camera.Size * (mouse.ButtonIndex == MouseButton.WheelUp ? .85f : 1.18f), 12, 140); AcceptEvent(); }
            if (mouse.ButtonIndex == MouseButton.Middle)
            { _panning = mouse.Pressed && !mouse.ShiftPressed; _rotating = mouse.Pressed && mouse.ShiftPressed; AcceptEvent(); }
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed) { _selecting = true; _dragStart = _dragEnd = mouse.Position; }
                else if (_selecting)
                {
                    _selecting = false;
                    if (_dragStart.DistanceTo(mouse.Position) < 6)
                    {
                        var friendly = Pick(mouse.Position, CombatSide.Friendly);
                        InspectedEnemy = friendly is null ? Pick(mouse.Position, CombatSide.Enemy)?.Id : null;
                        SelectionRequested?.Invoke(friendly is null ? Array.Empty<string>() : [friendly.Id], mouse.ShiftPressed);
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
        { CancelDrag(); _focus = Vector3.Zero; _yaw = 0; _pitch = DefaultPitch; Camera.Size = 110; UpdateCamera(); AcceptEvent(); }
    }
    public override void _Draw()
    {
        if (Simulation is null || Camera is null) return;
        var font = ThemeDB.FallbackFont;
        var center = ScreenPoint(Vector2.Zero);
        DrawString(font, center + new Vector2(-42, -8), "ARENA CENTER", HorizontalAlignment.Left, -1, 12, new Color(Ui.Muted, .7f));
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
                if (Selection.Count == 1 && unit.Active)
                {
                    var radius = CombatRules.DetectionRadius;
                    for (var segment = 0; segment < 64; segment++)
                        DrawLine(ScreenPoint(unit.Position + Vector2.FromAngle(segment * Mathf.Tau / 64) * radius),
                            ScreenPoint(unit.Position + Vector2.FromAngle((segment + 1) * Mathf.Tau / 64) * radius), new Color(color, .25f), 1, true);
                }
                DrawArc(at, Math.Max(10, 1.15f * Size.Y / Camera.Size), 0, Mathf.Tau, 32, Ui.Gold, 2, true);
                DrawCircle(at, 3, Ui.Gold);
                var to = unit.Order == CombatOrder.Move ? unit.Destination : unit.AttackTarget is { } id
                    && Simulation.Contacts(unit.Side).TryGetValue(id, out var contact) ? contact.LastKnownPosition : unit.Position;
                if (to.DistanceTo(unit.Position) > .4f) { var end = ScreenPoint(to); DrawLine(at, end, new Color(Ui.Gold, .65f), 1, true); DrawArc(end, 5, 0, Mathf.Tau, 16, Ui.Gold); }
                if (unit.PriorityTarget is { } priority && Simulation.Contacts(unit.Side).TryGetValue(priority, out var preferred))
                    DrawArc(ScreenPoint(preferred.LastKnownPosition), 16, 0, Mathf.Tau, 24, Ui.Gold, 1, true);
            }
            var forward = ScreenPoint(unit.Position + unit.Facing * 1.5f);
            DrawLine(at, forward, color, 2, true);
            var label = ScreenPoint(unit.Position, _models[unit.Id].Height + .3f);
            var text = friendly ? Selection.Count == 1 && Selection.Contains(unit.Id) ? $"{friendlyIndex} · {unit.Warrior.Callsign}" : friendlyIndex.ToString()
                : InspectedEnemy == unit.Id ? unit.Spec.DisplayName : unit.Id.Replace("enemy-", "E", StringComparison.Ordinal);
            var labelOffset = new Vector2(-font.GetStringSize(text, fontSize: 12).X / 2, -4);
            DrawStringOutline(font, label + labelOffset, text, HorizontalAlignment.Left, -1, 12, 4, new Color("101820"));
            DrawString(font, label + labelOffset, text, HorizontalAlignment.Left, -1, 12, color);
        }
        foreach (var contact in Simulation.Contacts(CombatSide.Friendly).Values.Where(item => !item.Detected))
        {
            var at = ScreenPoint(contact.LastKnownPosition);
            DrawArc(at, 10, 0, Mathf.Tau, 16, Ui.Muted, 1, true);
            DrawString(font, at + new Vector2(13, 4), $"? Last seen {Math.Max(0, Simulation.Elapsed - contact.LastSeenAt):0}s", HorizontalAlignment.Left, -1, 12, Ui.Muted);
        }
        foreach (var (shot, life) in _effects)
        {
            var color = shot.Hit ? Ui.Gold : Ui.Muted;
            var to = shot.Hit ? shot.To : shot.To + new Vector2(1, 1);
            DrawLine(ScreenPoint(shot.From, 1.5f), ScreenPoint(to, 1), new Color(color, life / .45f), shot.Weapon.Contains("Laser") || shot.Weapon == "PPC" ? 3 : 1, true);
            if (shot.Hit) DrawArc(ScreenPoint(to, 1), (1 - life / .45f) * 14 + 3, 0, Mathf.Tau, 16, new Color(color, life / .45f), 2, true);
        }
        if (_selecting && _dragStart.DistanceTo(_dragEnd) > 5)
        { var rect = new Rect2(_dragStart, _dragEnd - _dragStart).Abs(); DrawRect(rect, new Color(Ui.Gold, .12f)); DrawRect(rect, Ui.Gold, false); }
    }
}
