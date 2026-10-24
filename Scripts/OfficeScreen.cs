using Godot;

namespace Mech;

public partial class OfficeScreen : Control
{
    [Signal] public delegate void MapRequestedEventHandler();
    [Export(PropertyHint.File, "*.png")] public string ArtworkPath { get; set; } = "res://Assets/dropship-office.png";
    public Button Hotspot { get; private set; } = null!;
    public Rect2 ArtworkRect { get; private set; }
    public bool ArtworkLoaded => _image.Texture is not null;
    public static readonly Rect2 HotspotRegion = new(.149f, .083f, .388f, .453f);
    private Panel _frame = null!;
    private TextureRect _image = null!;
    private Label _error = null!;
    private Label _caption = null!;
    private Label _marker = null!;
    private Label _hint = null!;
    private Label _section = null!;
    private bool _hovered;

    public override void _Ready()
    {
        Ui.Place(Ui.Label(this, "ABOARD THE DROPSHIP", 11, Ui.Muted), 36, 12, 400, 22);
        Ui.Place(Ui.Label(this, "Captain’s office", 28), 36, 34, 500, 40);
        _section = Ui.Label(this, "COMMAND / 01", 11, Ui.Muted);
        _section.HorizontalAlignment = HorizontalAlignment.Right;
        _frame = new Panel { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        _frame.AddThemeStyleboxOverride("panel", Ui.Box(new Color("0a0e0c"), Ui.Line));
        AddChild(_frame);
        _image = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps
        };
        _frame.AddChild(_image);
        Hotspot = Ui.Button(_frame, "");
        Hotspot.Name = "StarMapHotspot";
        Hotspot.TooltipText = "Open star map";
        Hotspot.AddThemeStyleboxOverride("normal", Ui.Box(Colors.Transparent, Colors.Transparent));
        Hotspot.AddThemeStyleboxOverride("hover", Ui.Box(new Color(1, .9f, .65f, .06f), Ui.Gold));
        Hotspot.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(1, .9f, .65f, .12f), Ui.Gold));
        Hotspot.Pressed += () => EmitSignal(SignalName.MapRequested);
        Hotspot.MouseEntered += () => { _hovered = true; RefreshCaption(); };
        Hotspot.MouseExited += () => { _hovered = false; RefreshCaption(); };
        Hotspot.FocusEntered += RefreshCaption;
        Hotspot.FocusExited += RefreshCaption;
        _marker = Ui.Label(Hotspot, "◇", 30, Ui.Gold);
        _caption = Ui.Label(_frame, "  Star map     ↗", 14, Ui.Gold);
        _caption.AddThemeStyleboxOverride("normal", Ui.Box(Ui.Panel, Ui.Gold));
        _caption.Hide();
        _error = Ui.Label(_frame, "Office artwork could not load. The star map is still available.", 16, Ui.Gold);
        _error.HorizontalAlignment = HorizontalAlignment.Center;
        _hint = Ui.Label(this, "✧   Select the wall chart to view the Inner Sphere.", 12, Ui.Muted);
        Resized += Layout;
        VisibilityChanged += () => { _hovered = false; RefreshCaption(); };
        SetArtwork(ArtworkPath);
    }

    private void RefreshCaption()
    {
        if (_caption is not null) _caption.Visible = IsVisibleInTree() && (_hovered || Hotspot.HasFocus());
    }

    public void SetArtwork(string path)
    {
        _image.Texture = Ui.LoadTexture(path);
        _error.Visible = !ArtworkLoaded;
        Layout();
    }

    private void Layout()
    {
        if (_frame is null) return;
        Ui.Place(_section, Size.X - 200, 20, 164, 40);
        Ui.Place(_frame, 36, 86, Size.X - 72, Size.Y - 136);
        Ui.Place(_hint, 36, Size.Y - 50, Size.X - 72, 50);
        var source = _image.Texture?.GetSize() ?? new Vector2(1672, 940);
        var scale = Math.Min(_frame.Size.X / source.X, _frame.Size.Y / source.Y);
        var imageSize = source * scale;
        ArtworkRect = new Rect2((_frame.Size - imageSize) / 2, imageSize);
        _image.Position = ArtworkRect.Position;
        _image.Size = ArtworkRect.Size;
        Hotspot.Position = ArtworkRect.Position + HotspotRegion.Position * ArtworkRect.Size;
        Hotspot.Size = HotspotRegion.Size * ArtworkRect.Size;
        Ui.Place(_marker, Hotspot.Size.X - 38, Hotspot.Size.Y - 40, 32, 32);
        Ui.Place(_caption, Hotspot.Position.X + Hotspot.Size.X - 160, Hotspot.Position.Y + Hotspot.Size.Y + 12, 160, 40);
        Ui.Place(_error, 12, _frame.Size.Y / 2 + 60, _frame.Size.X - 24, 50);
    }
}
