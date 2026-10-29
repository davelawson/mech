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
        _models.Clear(); _pickBounds.Clear(); _effects.Clear();
        Simulation = simulation; Simulation.ShotFired += OnShot; InspectedEnemy = null; Selection = Array.Empty<string>();
        if (_terrainView is not null) { _world.RemoveChild(_terrainView); _terrainView.QueueFree(); }
        _terrainView = BattlefieldTerrainView.Create(simulation.Terrain); _world.AddChild(_terrainView);
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
        SyncModels(!Simulation.Paused && Simulation.Outcome == CombatOutcome.Ongoing ? (float)delta : 0);
        var hovering = CommandsEnabled && _windowFocused && !_selecting && !_panning && !_rotating && !_rightDragging
            && GetViewport().GuiGetHoveredControl() == this;
        SetHovered(hovering ? PickMech(GetLocalMousePosition())?.Id : null);
        QueueRedraw();
    }
    private void Pan(Vector2 amount)
    {
        var rotated = amount.Rotated(-_yaw);
        _focus.X = Math.Clamp(_focus.X + rotated.X, -48, 48);
        _focus.Z = Math.Clamp(_focus.Z + rotated.Y, -48, 48); UpdateCamera();
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
    public override void _GuiInput(InputEvent input)
    {
        if (!CommandsEnabled || Simulation is null || !_windowFocused) return;
        if (input is InputEventMouseButton mouse)
        {
            GrabFocus();
            if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && mouse.Pressed)
            { Camera.Size = Math.Clamp(Camera.Size * (mouse.ButtonIndex == MouseButton.WheelUp ? .85f : 1.18f), 12, 140); AcceptEvent(); }
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
        { CancelDrag(); _focus = Vector3.Zero; _yaw = 0; _pitch = DefaultPitch; Camera.Size = 110; UpdateCamera(); AcceptEvent(); }
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
            var label = ScreenPoint(unit.Position, _models[unit.Id].Height + .3f);
            var text = friendly ? Selection.Count == 1 && Selection.Contains(unit.Id) ? $"{friendlyIndex} · {unit.Warrior.Callsign}" : friendlyIndex.ToString()
                : InspectedEnemy == unit.Id && Simulation.DetailsVisible(CombatSide.Friendly, unit.Id) ? unit.Spec.DisplayName : unit.Spec.Chassis;
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
            DrawLine(ScreenPoint(shot.From, BattlefieldTerrain.EyeHeight), ScreenPoint(to, BattlefieldTerrain.EyeHeight), new Color(color, life / .45f), shot.Weapon.Contains("Laser") || shot.Weapon == "PPC" ? 3 : 1, true);
            if (shot.Hit) DrawArc(ScreenPoint(to, 1), (1 - life / .45f) * 14 + 3, 0, Mathf.Tau, 16, new Color(color, life / .45f), 2, true);
        }
        if (_selecting && _dragStart.DistanceTo(_dragEnd) > 5)
        { var rect = new Rect2(_dragStart, _dragEnd - _dragStart).Abs(); DrawRect(rect, new Color(Ui.Gold, .12f)); DrawRect(rect, Ui.Gold, false); }
    }
}
