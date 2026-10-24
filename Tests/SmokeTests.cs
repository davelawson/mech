using Godot;

namespace Mech.Tests;

public partial class SmokeTests : Node
{
    private CommandApp _app = null!;
    private int _checks;
    private bool _screenshots;

    public override async void _Ready()
    {
        try
        {
            _screenshots = OS.GetCmdlineUserArgs().Contains("--screenshots");
            TestGeometry();
            _app = ResourceLoader.Load<PackedScene>("res://Scenes/Command.tscn").Instantiate<CommandApp>();
            AddChild(_app);
            await Frames();
            Check(_app.Office.Visible && !_app.StarMap.Visible, "starts in the office");
            Check(_app.Office.ArtworkLoaded && _app.StarMap.Viewport.MapLoaded, "both local images load");
            Check(_app.Office.Hotspot.HasFocus(), "initial keyboard focus is on the wall map");
            await Capture("office");
            await PressKey(Key.Enter);
            Check(_app.StarMap.Visible && !_app.Office.Visible, "keyboard activation opens the chart");
            Check(_app.StarMap.Viewport.HasFocus(), "map receives keyboard focus");
            await Capture("map-fit");
            await PressKey(Key.Plus);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1.35f, "keyboard zoom");
            var offset = _app.StarMap.Viewport.Transform.Offset;
            await PressKey(Key.Down);
            Check(_app.StarMap.Viewport.Transform.Offset != offset, "arrow keys pan the map");
            await Capture("map-zoom");
            await PressKey(Key.Home);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1, "Home fits the map");
            Check(_app.StarMap.ZoomOut.Disabled, "minimum zoom disables zoom out");
            _app.StarMap.ZoomIn.EmitSignal(BaseButton.SignalName.Pressed);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1.35f, "zoom button changes the viewport");
            _app.StarMap.FitButton.EmitSignal(BaseButton.SignalName.Pressed);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1, "Fit button restores the overview");
            await TestMouse();
            var zoom = _app.StarMap.Viewport.Transform.Zoom;
            var mapPosition = _app.StarMap.Viewport.Transform.Offset;
            await PressKey(Key.Escape);
            Check(_app.Office.Visible && _app.Office.Hotspot.HasFocus(), "Escape returns to the office and restores focus");
            _app.Office.Hotspot.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Near(_app.StarMap.Viewport.Transform.Zoom, zoom, "zoom survives navigation");
            Check(_app.StarMap.Viewport.Transform.Offset.IsEqualApprox(mapPosition), "pan survives navigation");
            _app.StarMap.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Check(_app.Office.Hotspot.HasFocus(), "Back button restores wall-map focus");
            await TestResizing();
            TestMissingArtwork();
            await Frames();
            _app.QueueFree();
            await Frames();
            GD.Print($"PASS: {_checks} checks; office/map navigation, input, geometry, resize and asset failures.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"FAIL after {_checks} checks: {error}");
            GetTree().Quit(1);
        }
    }

    private void TestGeometry()
    {
        var model = new MapTransform(new Vector2(7000, 6000));
        model.Resize(new Vector2(1368, 692));
        Near(model.Zoom, 1, "initial fit zoom");
        Check(model.ImageRect.Position.X >= 0 && model.ImageRect.End.X <= model.ViewSize.X + .1f
            && model.ImageRect.Position.Y >= 0 && model.ImageRect.End.Y <= model.ViewSize.Y + .1f, "fit includes the complete chart");
        model.ZoomAt(4, model.ViewSize / 2);
        var anchor = new Vector2(600, 320);
        var point = model.ImagePoint(anchor);
        model.ZoomAt(5, anchor);
        Check(model.ImagePoint(anchor).DistanceTo(point) < .01f, "zoom preserves the image point under the cursor");
        var center = model.ImagePoint(model.ViewSize / 2);
        model.Resize(new Vector2(900, 500));
        Check(model.ImagePoint(model.ViewSize / 2).DistanceTo(center) < .01f, "resize preserves the viewed center");
        model.ZoomAt(100, model.ViewSize / 2);
        Near(model.Zoom, 12, "upper zoom bound");
        model.Pan(new Vector2(100000, 100000));
        Check(model.Offset.IsEqualApprox(Vector2.Zero), "pan clamps to the top/left edges");
        model.Pan(new Vector2(-100000, -100000));
        Check(model.ImageRect.End.IsEqualApprox(model.ViewSize), "pan clamps to the bottom/right edges");
        model.ZoomAt(.001f, model.ViewSize / 2);
        Near(model.Zoom, 1, "lower zoom bound");
        model.Fit();
        Check(model.ImageRect.GetCenter().DistanceTo(model.ViewSize / 2) < .01f, "fit recenters both axes");
    }

    private async Task TestMouse()
    {
        var viewport = _app.StarMap.Viewport;
        var at = viewport.GlobalPosition + viewport.Size / 2;
        MouseMotion(at, Vector2.Zero);
        MouseButton(at, Godot.MouseButton.WheelUp, true, 30);
        await Frames();
        Near(viewport.Transform.Zoom, 12, "mouse wheel reaches the zoom cap");
        Check(_app.StarMap.ZoomIn.Disabled, "maximum zoom disables zoom in");
        var before = viewport.Transform.Offset;
        MouseButton(at, Godot.MouseButton.Left, true);
        MouseMotion(at + new Vector2(80, 60), new Vector2(80, 60));
        MouseButton(at + new Vector2(80, 60), Godot.MouseButton.Left, false);
        await Frames();
        Check(viewport.Transform.Offset != before, "mouse drag pans the image");
        Check(!viewport.IsDragging, "mouse release ends dragging");
        MouseButton(at, Godot.MouseButton.Left, true);
        await Frames();
        _app.ShowOffice();
        Check(!viewport.IsDragging, "navigation cancels an active drag");
        MouseButton(at, Godot.MouseButton.Left, false);
        _app.ShowMap();
        await Frames();
    }

    private async Task TestResizing()
    {
        _app.ShowOffice();
        foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(1024, 768), new Vector2I(900, 500), new Vector2I(1200, 1100) })
        {
            GetWindow().Size = size;
            await Frames();
            var office = _app.Office;
            var art = office.ArtworkRect;
            var hotspot = office.Hotspot;
            Check(art.Size.X > 0 && art.Size.Y > 0, $"office remains visible at {size}");
            Near(art.Size.X / art.Size.Y, 1672f / 940, $"office aspect ratio at {size}");
            Near((hotspot.Position.X - art.Position.X) / art.Size.X, .149f, $"hotspot horizontal alignment at {size}");
            Near((hotspot.Position.Y - art.Position.Y) / art.Size.Y, .083f, $"hotspot vertical alignment at {size}");
            var at = hotspot.GlobalPosition + hotspot.Size / 2;
            MouseMotion(at, Vector2.Zero);
            MouseButton(at, Godot.MouseButton.Left, true);
            MouseButton(at, Godot.MouseButton.Left, false);
            await Frames();
            Check(_app.StarMap.Visible, $"actual hotspot click opens the chart at {size}");
            var viewport = _app.StarMap.Viewport;
            var rect = viewport.Transform.ImageRect;
            Check(rect.Intersects(new Rect2(Vector2.Zero, viewport.Size)), $"map remains in view at {size}");
            _app.ShowOffice();
            await Frames();
        }
        GetWindow().Size = new Vector2I(1440, 900);
    }

    private void TestMissingArtwork()
    {
        _app.Office.SetArtwork("res://Assets/missing-office.png");
        Check(!_app.Office.ArtworkLoaded, "missing office artwork is handled");
        Check(_app.Office.Hotspot.Size.X > 0, "wall-map navigation survives missing office artwork");
        _app.Office.SetArtwork(_app.Office.ArtworkPath);
        _app.ShowMap();
        _app.StarMap.SetMap("res://Assets/missing-map.jpg");
        Check(_app.StarMap.Status.Visible && _app.StarMap.Status.Text.Contains("could not load"), "missing map is explained");
        Check(_app.StarMap.ZoomIn.Disabled && _app.StarMap.ZoomOut.Disabled && _app.StarMap.FitButton.Disabled, "map controls are disabled on load failure");
        _app.StarMap.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(_app.Office.Visible, "office remains reachable after a map load failure");
        _app.StarMap.SetMap(_app.StarMap.MapPath);
        Check(_app.StarMap.Viewport.MapLoaded, "map can be restored after a load failure");
    }

    private async Task PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
        await Frames();
    }

    private static void MouseMotion(Vector2 at, Vector2 delta) => Input.ParseInputEvent(new InputEventMouseMotion
    {
        Position = at, GlobalPosition = at, Relative = delta,
        ButtonMask = Input.IsMouseButtonPressed(Godot.MouseButton.Left) ? MouseButtonMask.Left : 0
    });

    private static void MouseButton(Vector2 at, Godot.MouseButton button, bool pressed, float factor = 1) => Input.ParseInputEvent(new InputEventMouseButton
    {
        Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = pressed, Factor = factor
    });

    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    private void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .01f, $"{message}: expected {expected}, got {actual}");

    private async Task Capture(string name)
    {
        if (!_screenshots || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://TestResults"));
        var result = GetViewport().GetTexture().GetImage().SavePng($"res://TestResults/{name}.png");
        Check(result == Error.Ok, $"saved {name} screenshot");
    }
}
