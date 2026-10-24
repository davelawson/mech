using Godot;

namespace Mech;

public partial class MapScreen : Control
{
    public event Action<Texture2D?>? TextureChanged;
    [Signal] public delegate void BackRequestedEventHandler();
    [Export(PropertyHint.File, "*.jpg")] public string MapPath { get; set; } = "res://Assets/inner-sphere-3025.jpg";
    public MapViewport Viewport { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Button ZoomIn { get; private set; } = null!;
    public Button ZoomOut { get; private set; } = null!;
    public Button FitButton { get; private set; } = null!;
    public Label Status { get; private set; } = null!;
    private Label _zoom = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _hint = null!;
    private LinkButton _source = null!;
    private Panel _controls = null!;

    public override void _Ready()
    {
        BackButton = Ui.Button(this, "←  Back to office");
        BackButton.Pressed += () => EmitSignal(SignalName.BackRequested);
        _subtitle = Ui.Label(this, "STELLAR CARTOGRAPHY", 11, Ui.Muted);
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _title = Ui.Label(this, "The Inner Sphere  ·  3025", 26);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        Viewport = new MapViewport { Name = "MapViewport" };
        AddChild(Viewport);
        Status = Ui.Label(Viewport, "Unfolding star chart…", 16, Ui.Gold);
        Status.HorizontalAlignment = HorizontalAlignment.Center;
        _controls = new Panel();
        _controls.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold));
        AddChild(_controls);
        ZoomOut = Ui.Button(_controls, "−");
        ZoomOut.TooltipText = "Zoom out";
        _zoom = Ui.Label(_controls, "100%", 13);
        _zoom.HorizontalAlignment = HorizontalAlignment.Center;
        ZoomIn = Ui.Button(_controls, "+");
        ZoomIn.TooltipText = "Zoom in";
        FitButton = Ui.Button(_controls, "Fit map");
        ZoomIn.Pressed += () => Viewport.ZoomBy(1.35f);
        ZoomOut.Pressed += () => Viewport.ZoomBy(1 / 1.35f);
        FitButton.Pressed += Viewport.Fit;
        Ui.Place(ZoomOut, 6, 6, 38, 36);
        Ui.Place(_zoom, 44, 6, 70, 36);
        Ui.Place(ZoomIn, 114, 6, 38, 36);
        Ui.Place(FitButton, 166, 6, 90, 36);
        _hint = Ui.Label(this, "Drag to pan · Scroll or + / − to zoom · Arrows to pan · Home to fit", 12, Ui.Muted);
        _source = new LinkButton { Text = "Official BattleTech map ↗" };
        _source.AddThemeFontSizeOverride("font_size", 12);
        _source.AddThemeColorOverride("font_color", Ui.Muted);
        _source.Pressed += () => OS.ShellOpen("https://battletech.com/wp-content/uploads/sites/6/2025/03/BattleTech-Inner-Spher-at-War-3025.pdf");
        AddChild(_source);
        Viewport.ViewChanged += RefreshControls;
        Resized += Layout;
        Layout();
        SetMap(MapPath);
    }

    public void SetMap(string path)
    {
        Viewport.SetMap(Ui.LoadTexture(path));
        TextureChanged?.Invoke(Viewport.Texture);
        Status.Text = Viewport.MapLoaded ? "" : "The star chart could not load. Return to the office and check the game assets.";
        Status.Visible = !Viewport.MapLoaded;
        RefreshControls();
    }

    private void RefreshControls()
    {
        var zoom = Viewport.Transform.Zoom;
        _zoom.Text = $"{MathF.Round(zoom * 100)}%";
        ZoomOut.Disabled = !Viewport.MapLoaded || zoom <= MapTransform.MinimumZoom;
        ZoomIn.Disabled = !Viewport.MapLoaded || zoom >= MapTransform.MaximumZoom;
        FitButton.Disabled = !Viewport.MapLoaded;
    }

    private void Layout()
    {
        if (Viewport is null) return;
        Ui.Place(BackButton, 36, 22, 170, 40);
        Ui.Place(_subtitle, Size.X / 2 - 220, 10, 440, 22);
        Ui.Place(_title, Size.X / 2 - 220, 32, 440, 42);
        Ui.Place(Viewport, 36, 86, Size.X - 72, Size.Y - 136);
        Ui.Place(Status, 12, Viewport.Size.Y / 2 - 25, Viewport.Size.X - 24, 50);
        Ui.Place(_controls, Size.X / 2 - 131, Size.Y - 116, 262, 48);
        Ui.Place(_hint, 36, Size.Y - 50, Size.X - 330, 50);
        Ui.Place(_source, Size.X - 260, Size.Y - 42, 224, 34);
    }
}
