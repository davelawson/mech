using Godot;

namespace Mech;

public partial class MapViewport : Control
{
    public event Action? ViewChanged;
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
                IsDragging = mouse.Pressed;
                if (mouse.Pressed) GrabFocus();
                MouseDefaultCursorShape = mouse.Pressed ? CursorShape.Move : CursorShape.Drag;
                AcceptEvent();
            }
            else if (mouse.Pressed && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var direction = mouse.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
                ZoomBy(MathF.Pow(1.15f, direction * (mouse.Factor > 0 ? mouse.Factor : 1)), mouse.Position);
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motion && IsDragging)
        {
            Transform.Pan(motion.Relative);
            Changed();
            AcceptEvent();
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
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) EndDrag();
    }

    private void Changed()
    {
        QueueRedraw();
        ViewChanged?.Invoke();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("111611"));
        if (Texture is not null) DrawTextureRect(Texture, Transform.ImageRect, false);
        DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), HasFocus() ? Ui.Gold : Ui.Line, false, 1);
    }
}
