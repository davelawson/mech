using Godot;

namespace Mech;

public partial class MapViewport : Control
{
    public event Action? ViewChanged;
    public event Action<string>? SystemSelected;
    private Campaign? _campaign;
    private Vector2 _pressPosition;
    private bool _moved;
    private string? _hoveredSystem;
    public string? HoveredSystemId => _hoveredSystem;

    public void SetCampaign(Campaign campaign) { _campaign = campaign; QueueRedraw(); }
    public void RefreshTravel() => QueueRedraw();

    public Vector2 ShipImagePosition
    {
        get
        {
            if (_campaign is null) return Transform.ImageSize / 2;
            var travel = _campaign.Travel;
            var system = StarChart.Instance[travel.OrbitSystemId];
            var position = new Vector2((float)system.X, (float)system.Y);
            if (!travel.InOrbit)
            {
                var next = StarChart.Instance[travel.NextSystemId!];
                var total = StarChart.Instance.JumpDays(travel.OrbitSystemId, travel.NextSystemId!);
                position = position.Lerp(new Vector2((float)next.X, (float)next.Y), 1 - (float)travel.DaysRemaining / total);
            }
            return position;
        }
    }
    public Vector2 ShipViewportPosition => Transform.Offset + ShipImagePosition * Transform.Scale;
    private Rect2 ChartControlsRect => new(Size.X / 2 - 202, Size.Y - 66, 404, 48);
    public bool ShipIsOffscreen => _campaign is not null
        && (!new Rect2(Vector2.Zero, Size).Grow(-30).HasPoint(ShipViewportPosition)
            || ChartControlsRect.Grow(24).HasPoint(ShipViewportPosition));
    public Vector2 ShipIndicatorPosition
    {
        get
        {
            // Reserve the lower edge for zoom controls so the locator stays clickable.
            var safe = new Rect2(32, 32, Math.Max(2, Size.X - 64), Math.Max(2, Size.Y - 132));
            var center = safe.GetCenter();
            var direction = ShipViewportPosition - center;
            var inset = safe.Size / 2;
            var factor = Math.Min(Math.Abs(direction.X) < .001f ? float.PositiveInfinity : inset.X / Math.Abs(direction.X),
                Math.Abs(direction.Y) < .001f ? float.PositiveInfinity : inset.Y / Math.Abs(direction.Y));
            return direction.IsZeroApprox() ? center : center + direction * factor;
        }
    }
    public Rect2 ShipIndicatorRect => new(ShipIndicatorPosition - Vector2.One * 23, Vector2.One * 46);

    public void FindDropShip()
    {
        if (!MapLoaded || _campaign is null) return;
        EndDrag(); Transform.CenterOn(ShipImagePosition); Changed();
    }

    public StarSystem? HitSystem(Vector2 position)
    {
        var point = Transform.ImagePoint(position);
        var radius = Math.Max(7 / Transform.Scale, 8);
        // Prefer the closest symbol when zoomed out; at readable zoom the complete name is clickable too.
        var systems = StarChart.Instance.Systems;
        var symbol = systems.Select(system => (System: system,
            Distance: new Vector2((float)system.X, (float)system.Y).DistanceTo(point)))
            .Where(item => item.Distance <= radius).OrderBy(item => item.Distance).FirstOrDefault();
        if (symbol.System is not null && symbol.Distance * Transform.Scale <= 5) return symbol.System;
        var label = systems.Where(system => system.LabelBounds.Length == 4 && new Rect2(
            (float)system.LabelBounds[0], (float)system.LabelBounds[1],
            (float)system.LabelBounds[2], (float)system.LabelBounds[3]).Grow(2 / Transform.Scale).HasPoint(point))
            .OrderBy(system => new Vector2((float)system.X, (float)system.Y).DistanceTo(point)).FirstOrDefault();
        return label ?? symbol.System;
    }
    public Texture2D? Texture { get; private set; }
    public MapTransform Transform { get; private set; } = new(new Vector2(7000, 6000));
    public bool IsDragging { get; private set; }
    public bool MapLoaded => Texture is not null;

    public override void _Ready()
    {
        ClipContents = true;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.Drag;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Resized += () => { Transform.Resize(Size); Changed(); };
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        VisibilityChanged += () => { if (!IsVisibleInTree()) EndDrag(); };
        GetWindow().FocusExited += EndDrag;
    }

    public void SetMap(Texture2D? texture)
    {
        Texture = texture;
        Transform = new MapTransform(texture?.GetSize() ?? new Vector2(7000, 6000));
        Transform.Resize(Size);
        Changed();
    }

    public void ZoomBy(float factor, Vector2? anchor = null)
    {
        if (!MapLoaded) return;
        Transform.ZoomAt(Transform.Zoom * factor, anchor ?? Size / 2);
        Changed();
    }

    public void Fit()
    {
        if (!MapLoaded) return;
        Transform.Fit();
        Changed();
    }

    public void EndDrag()
    {
        IsDragging = false;
        MouseDefaultCursorShape = CursorShape.Drag;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!MapLoaded) return;
        if (@event is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed)
                {
                    IsDragging = true; GrabFocus(); _pressPosition = mouse.Position; _moved = false;
                    MouseDefaultCursorShape = CursorShape.Move;
                }
                else FinishPointer(mouse.Position);
                AcceptEvent();
            }
            else if (mouse.Pressed && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var direction = mouse.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
                ZoomBy(MathF.Pow(1.15f, direction * (mouse.Factor > 0 ? mouse.Factor : 1)), mouse.Position);
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            if (IsDragging)
            {
                if (motion.Position.DistanceTo(_pressPosition) > 5) _moved = true;
                if (_moved) { Transform.Pan(motion.Relative); Changed(); }
                AcceptEvent();
            }
            else
            {
                if (ShipIsOffscreen && ShipIndicatorRect.HasPoint(motion.Position))
                {
                    _hoveredSystem = null; TooltipText = "Find DropShip";
                    MouseDefaultCursorShape = CursorShape.PointingHand; QueueRedraw(); return;
                }
                var system = HitSystem(motion.Position);
                _hoveredSystem = system?.Id;
                TooltipText = system is null ? "" : system.Name + (_campaign?.Travel.InOrbit == true
                    ? " · Click to plot a journey" : " · Plotting is available in orbit");
                MouseDefaultCursorShape = system is not null && _campaign?.Travel.InOrbit == true
                    ? CursorShape.PointingHand : CursorShape.Drag;
                QueueRedraw();
            }
        }
        else if (@event is InputEventKey { Pressed: true } key)
        {
            switch (key.Keycode)
            {
                case Key.Plus: case Key.Equal: case Key.KpAdd: ZoomBy(1.35f); break;
                case Key.Minus: case Key.KpSubtract: ZoomBy(1 / 1.35f); break;
                case Key.Home: Fit(); break;
                case Key.Left: Transform.Pan(new Vector2(70, 0)); break;
                case Key.Right: Transform.Pan(new Vector2(-70, 0)); break;
                case Key.Up: Transform.Pan(new Vector2(0, 70)); break;
                case Key.Down: Transform.Pan(new Vector2(0, -70)); break;
                default: return;
            }
            Changed();
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mouse && IsDragging)
        {
            FinishPointer(GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    private void FinishPointer(Vector2 position)
    {
        var clicked = IsDragging && !_moved && position.DistanceTo(_pressPosition) <= 5
            && new Rect2(Vector2.Zero, Size).HasPoint(position);
        EndDrag();
        if (clicked && ShipIsOffscreen && ShipIndicatorRect.HasPoint(position)) FindDropShip();
        else if (clicked && _campaign?.Travel.InOrbit == true)
        {
            var selected = HitSystem(position);
            if (selected is not null) SystemSelected?.Invoke(selected.Id);
        }
    }

    private void Changed()
    {
        QueueRedraw();
        ViewChanged?.Invoke();
    }

    private Vector2 SystemPoint(string id)
    {
        var system = StarChart.Instance[id];
        return Transform.Offset + new Vector2((float)system.X, (float)system.Y) * Transform.Scale;
    }

    private void DrawRoute(IReadOnlyList<string>? route, Color color, float width, bool candidate)
    {
        if (route is null) return;
        for (var i = 1; i < route.Count; i++)
        {
            var from = SystemPoint(route[i - 1]); var to = SystemPoint(route[i]);
            DrawLine(from, to, new Color("050b15"), width + 2, true);
            if (candidate) DrawDashedLine(from, to, color, width, 8, true);
            else DrawLine(from, to, color, width, true);
            DrawCircle(to, 3, color);
        }
    }

    private void DrawTravel(Travel travel)
    {
        DrawRoute(travel.Journey, new Color("61de95"), 5, false);
        DrawRoute(travel.Candidate, new Color("62b9ff"), 2, true);
        if (_hoveredSystem is not null) DrawArc(SystemPoint(_hoveredSystem), 7, 0, MathF.Tau, 24,
            travel.InOrbit ? new Color("62b9ff") : Ui.Muted, 1, true);
    }

    private void DrawShip()
    {
        if (_campaign is null) return;
        var position = ShipIsOffscreen ? ShipIndicatorPosition : ShipViewportPosition;
        DrawCircle(position, 24, new Color("050b15"));
        DrawArc(position, 24, 0, MathF.Tau, 48, Ui.Gold, 2, true);
        if (ShipIsOffscreen)
        {
            var direction = (ShipViewportPosition - Size / 2).Normalized();
            var side = direction.Orthogonal();
            DrawColoredPolygon([position + direction * 15, position - direction * 10 + side * 10,
                position - direction * 6, position - direction * 10 - side * 10], Ui.Gold);
        }
        else
        {
            // A ship silhouette and halo stay readable independently of map zoom.
            Vector2[] hull = [new(0, -16), new(7, -4), new(13, 13), new(0, 7), new(-13, 13), new(-7, -4)];
            DrawColoredPolygon(hull.Select(point => point + position).ToArray(), Ui.Gold);
            DrawLine(position + new Vector2(0, -8), position + new Vector2(0, 4), new Color("050b15"), 3, true);
        }
        var travel = _campaign.Travel;
        var label = travel.InOrbit ? $"DropShip · {StarChart.Instance[travel.OrbitSystemId].Name}"
            : $"DropShip · {StarChart.Instance[travel.OrbitSystemId].Name} → {StarChart.Instance[travel.NextSystemId!].Name}";
        if (ShipIsOffscreen) label += " · Find";
        DrawShipLabel(position, label);
    }

    private void DrawShipLabel(Vector2 position, string text)
    {
        var font = GetThemeDefaultFont();
        const int fontSize = 14;
        var maxWidth = Math.Max(20, Size.X - 24);
        while (text.Length > 2 && font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X + 16 > maxWidth)
            text = text[..^2].TrimEnd('…') + "…";
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X + 16;
        const float height = 28;
        var x = position.X + 32;
        if (x + width > Size.X - 8) x = position.X - width - 32;
        x = Math.Clamp(x, 8, Math.Max(8, Size.X - width - 8));
        var y = Math.Clamp(position.Y - height / 2, 8, Math.Max(8, Size.Y - height - 8));
        var bounds = new Rect2(x, y, width, height);
        if (bounds.Intersects(ChartControlsRect.Grow(4))) bounds.Position = new Vector2(x, Size.Y - 66 - height - 8);
        DrawRect(bounds, new Color("050b15"));
        DrawRect(bounds, Ui.Gold, false, 1);
        DrawString(font, bounds.Position + new Vector2(8, 19), text, HorizontalAlignment.Left, width - 16, fontSize, Ui.Gold);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("050b15"));
        if (Texture is not null)
        {
            DrawTextureRect(Texture, Transform.ImageRect, false);
            if (_campaign is not null) { DrawTravel(_campaign.Travel); DrawShip(); }
        }
        DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), HasFocus() ? Ui.Gold : Ui.Line, false, 1);
    }
}
