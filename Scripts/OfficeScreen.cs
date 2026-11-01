using Godot;

namespace Mech;

public partial class OfficeScreen : Control
{
    [Signal] public delegate void MapRequestedEventHandler();
    [Signal] public delegate void WarriorsRequestedEventHandler();
    [Signal] public delegate void HangarRequestedEventHandler();
    [Signal] public delegate void DayRequestedEventHandler();
    [Signal] public delegate void EventRequestedEventHandler();
    [Signal] public delegate void FinancesRequestedEventHandler();
    [Signal] public delegate void FilingRequestedEventHandler();
    [Signal] public delegate void MissionsRequestedEventHandler();
    [Signal] public delegate void MenuRequestedEventHandler();
    [Export(PropertyHint.File, "*.png")] public string ArtworkPath { get; set; } = "res://Assets/dropship-office-missions.png";
    public ArtworkHotspot PhoneButton { get; private set; } = null!;
    public ColorRect PhoneLight { get; private set; } = null!;
    public bool PhoneFlashing { get; private set; }
    public ArtworkHotspot RolodexButton { get; private set; } = null!;
    public ArtworkHotspot HangarButton { get; private set; } = null!;
    public Label ReadyCount { get; private set; } = null!;
    public ArtworkHotspot Hotspot { get; private set; } = null!;
    public ArtworkHotspot CalendarButton { get; private set; } = null!;
    public ArtworkHotspot ReminderButton { get; private set; } = null!;
    public ArtworkHotspot SafeButton { get; private set; } = null!;
    public ArtworkHotspot FilingButton { get; private set; } = null!;
    public ArtworkHotspot JacketButton { get; private set; } = null!;
    public Label CalendarDate { get; private set; } = null!;
    public Label QuarterText { get; private set; } = null!;
    public Label SafeReadout => _safeReadout;
    public TextureRect WallMap { get; private set; } = null!;
    public Rect2 ArtworkRect { get; private set; }
    public bool ArtworkLoaded => _image.Texture is not null;
    // Shapes are traced in source-art pixels; regions are their bounds, and all controls use the artwork scale.
    public static readonly Vector2 ArtworkSize = new(1672, 940);
    public static readonly Rect2 MapRegion = new(347, 112, 288, 288f * 6 / 7);
    public static readonly Vector2[] ChartShape = [new(328, 84), new(652, 84), new(652, 390), new(598, 390), new(598, 410), new(576, 410), new(576, 390), new(328, 390)];
    public static readonly Vector2[] CalendarShape = [new(730, 166), new(904, 166), new(912, 174), new(912, 292), new(904, 300), new(730, 300), new(722, 292), new(722, 174)];
    public static readonly Vector2[] ReminderShape = [new(767, 271), new(862, 271), new(862, 311), new(767, 311)];
    public static readonly Vector2[] SafeShape = [new(326, 446), new(598, 446), new(598, 680), new(326, 680)];
    public static readonly Vector2[] FilingShape = [new(0, 330), new(150, 330), new(190, 345), new(190, 790), new(150, 806), new(0, 806)];
    public static readonly Vector2[] RolodexShape = [new(702, 575), new(720, 560), new(745, 553), new(795, 553), new(812, 565), new(818, 585),
        new(815, 650), new(705, 650), new(700, 600)];
    public static readonly Vector2[] PhoneShape = [new(1125, 600), new(1120, 575), new(1140, 560), new(1200, 557), new(1235, 565), new(1240, 580),
        new(1240, 630), new(1230, 640), new(1145, 640), new(1135, 620)];
    public static readonly Vector2[] HangarShape = ArtworkHotspot.Ellipse(new(1720, 265), new(180, 185), ArtworkSize.X);
    public static readonly Vector2[] JacketShape = [new(1340, 168), new(1322, 180), new(1298, 198), new(1288, 240), new(1282, 300), new(1280, 416),
        new(1396, 416), new(1392, 300), new(1386, 240), new(1380, 198), new(1358, 180)];
    public static readonly Rect2 ChartRegion = ArtworkHotspot.Bounds(ChartShape);
    public static readonly Rect2 CalendarRegion = ArtworkHotspot.Bounds(CalendarShape);
    public static readonly Rect2 ReminderRegion = ArtworkHotspot.Bounds(ReminderShape);
    public static readonly Rect2 SafeRegion = ArtworkHotspot.Bounds(SafeShape);
    public static readonly Rect2 FilingRegion = ArtworkHotspot.Bounds(FilingShape);
    public static readonly Rect2 RolodexRegion = ArtworkHotspot.Bounds(RolodexShape);
    public static readonly Rect2 PhoneRegion = ArtworkHotspot.Bounds(PhoneShape);
    public static readonly Rect2 HangarRegion = ArtworkHotspot.Bounds(HangarShape);
    public static readonly Rect2 JacketRegion = ArtworkHotspot.Bounds(JacketShape);
    private Panel _frame = null!;
    private TextureRect _image = null!;
    private Label _error = null!;
    private Label _hint = null!;
    private Label _section = null!;
    private Label _safeReadout = null!;
    private bool _paymentPending;
    private double _blinkTime;
    private double _phoneBlinkTime;

    public override void _Ready()
    {
        Ui.Place(Ui.Label(this, "ABOARD THE DROPSHIP", 11, Ui.Muted), 36, 12, 400, 22);
        Ui.Place(Ui.Label(this, "Captain’s office", 28), 36, 34, 500, 40);
        _section = Ui.Label(this, "", 12, Ui.Gold);
        _section.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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
        WallMap = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps
        };
        _frame.AddChild(WallMap);
        Hotspot = AddHotspot("StarMapHotspot", ChartShape);
        Hotspot.TooltipText = "Open star map";
        Hotspot.Pressed += () => EmitSignal(SignalName.MapRequested);
        _error = Ui.Label(_frame, "Office artwork could not load. The star map is still available.", 16, Ui.Gold);
        _error.HorizontalAlignment = HorizontalAlignment.Center;
        CreateCampaignControls();
        PhoneButton = AddHotspot("MissionPhone", PhoneShape);
        PhoneButton.Pressed += () => EmitSignal(SignalName.MissionsRequested);
        PhoneLight = new ColorRect { Color = new Color("ffbd54"), MouseFilter = MouseFilterEnum.Ignore };
        PhoneButton.AddChild(PhoneLight); Ui.Place(PhoneLight, 81, 50, 8, 6); PhoneLight.Hide();
        RolodexButton = AddHotspot("Rolodex", RolodexShape);
        RolodexButton.Pressed += () => EmitSignal(SignalName.WarriorsRequested);
        ReadyCount = Ui.Label(RolodexButton, "0", 16, new Color("e4d5ae")); ReadyCount.HorizontalAlignment = HorizontalAlignment.Center;
        Ui.Place(ReadyCount, 47, 67, 48, 24);
        HangarButton = AddHotspot("HangarPorthole", HangarShape); HangarButton.TooltipText = "Porthole · Open mech hangar";
        HangarButton.Pressed += () => EmitSignal(SignalName.HangarRequested);
        JacketButton = AddHotspot("Jacket", JacketShape); JacketButton.TooltipText = "Jacket · Main menu";
        JacketButton.Pressed += () => EmitSignal(SignalName.MenuRequested);
        _hint = Ui.Label(this, "Phone: missions · Rolodex: MechWarriors · Porthole: hangar · Calendar: next day · Reminder: next event · Safe: finances · Cabinet: save / load · Jacket: main menu", 12, Ui.Muted);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Resized += Layout;
        SetArtwork(ArtworkPath);
    }

    private ArtworkHotspot AddHotspot(string name, Vector2[] shape)
    {
        var hotspot = new ArtworkHotspot(shape) { Name = name };
        _frame.AddChild(hotspot);
        return hotspot;
    }

    public void SetWallMap(Texture2D? texture) => WallMap.Texture = texture;

    private void CreateCampaignControls()
    {
        CalendarButton = AddHotspot("Calendar", CalendarShape);
        CalendarButton.TooltipText = "Advance one day · Click again to stop";
        CalendarButton.Pressed += () => EmitSignal(SignalName.DayRequested);
        CalendarDate = Ui.Label(CalendarButton, "", 16, new Color("b6d4b1"));
        CalendarDate.HorizontalAlignment = HorizontalAlignment.Center;
        CalendarDate.ClipText = true;
        Ui.Place(CalendarDate, 27, 28, 120, 31);
        QuarterText = Ui.Label(CalendarButton, "", 11, Ui.Gold);
        QuarterText.HorizontalAlignment = HorizontalAlignment.Center;
        QuarterText.ClipText = true;
        Ui.Place(QuarterText, 27, 64, 120, 29);

        ReminderButton = AddHotspot("Reminder", ReminderShape);
        var reminderText = Ui.Label(ReminderButton, "Reminder...", 14, new Color("3d3520"));
        reminderText.HorizontalAlignment = HorizontalAlignment.Center;
        reminderText.ClipText = true;
        Ui.Place(reminderText, 2, 4, 91, 31);
        ReminderButton.TooltipText = "Advance to the next event · Click anywhere to stop";
        ReminderButton.Pressed += () => EmitSignal(SignalName.EventRequested);

        SafeButton = AddHotspot("CompanySafe", SafeShape);
        SafeButton.TooltipText = "Company safe · Quarterly finances";
        _safeReadout = Ui.Label(SafeButton, "", 20, new Color("b6d4b1"));
        _safeReadout.HorizontalAlignment = HorizontalAlignment.Center;
        _safeReadout.ClipText = true;
        Ui.Place(_safeReadout, 56, 30, 157, 34);
        SafeButton.Pressed += () => EmitSignal(SignalName.FinancesRequested);

        FilingButton = AddHotspot("FilingCabinet", FilingShape);
        FilingButton.TooltipText = "Filing cabinet · Save / load game";
        FilingButton.Pressed += () => EmitSignal(SignalName.FilingRequested);
    }

    public void RefreshCampaign(Campaign campaign)
    {
        _section.Text = campaign.Travel.LocationText;
        _section.TooltipText = campaign.Travel.LocationText;
        var count = campaign.Roster?.ReadyWarriors ?? 0;
        ReadyCount.Text = count.ToString();
        RolodexButton.TooltipText = $"Rolodex · {count} healthy MechWarriors ready for deployment";
        RolodexButton.Disabled = HangarButton.Disabled = PhoneButton.Disabled = campaign.RequiresDraft;
        var phoneFlashing = !campaign.RequiresDraft && campaign.Missions.HasUnseenOffers;
        if (PhoneFlashing != phoneFlashing)
        {
            PhoneFlashing = phoneFlashing;
            _phoneBlinkTime = 0; PhoneLight.Visible = PhoneFlashing;
        }
        PhoneButton.TooltipText = PhoneFlashing ? "Phone · New mission offers" : "Phone · Browse missions";
        CalendarDate.Text = campaign.DateText;
        var days = campaign.DaysUntilQuarter;
        QuarterText.Text = campaign.PaymentPending ? "Payment Due" : $"bills due in {days} {(days == 1 ? "day" : "days")}";
        QuarterText.AddThemeColorOverride("font_color", campaign.PaymentPending || campaign.DeferredDebt > 0
            ? new Color("ff6c63") : Ui.Gold);
        CalendarButton.Disabled = !campaign.CanAdvance;
        ReminderButton.Disabled = !campaign.CanAdvance;
        CalendarButton.TooltipText = $"{campaign.DateText} · {QuarterText.Text}\nAdvance one day · Click again to stop";
        _safeReadout.Text = $"₡ {campaign.Cash.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}";
        SafeButton.TooltipText = $"{_safeReadout.Text} · Quarterly finances";
        _paymentPending = campaign.PaymentPending;
        _blinkTime = 0;
        _safeReadout.Modulate = Colors.White;
        _safeReadout.AddThemeColorOverride("font_color", _paymentPending ? new Color("ff6c63") : new Color("b6d4b1"));
    }

    public override void _Process(double delta)
    {
        if (PhoneFlashing)
        {
            _phoneBlinkTime += delta;
            PhoneLight.Visible = (int)(_phoneBlinkTime / .5) % 2 == 0;
        }
        if (!_paymentPending) return;
        _blinkTime += delta;
        _safeReadout.Modulate = new Color(1, 1, 1, (int)(_blinkTime / .45) % 2 == 0 ? 1 : .4f);
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
        Ui.Place(_section, Size.X - 430, 16, 394, 52);
        Ui.Place(_frame, 36, 86, Size.X - 72, Size.Y - 136);
        Ui.Place(_hint, 36, Size.Y - 50, Size.X - 72, 50);
        var source = ArtworkSize;
        var scale = Math.Min(_frame.Size.X / source.X, _frame.Size.Y / source.Y);
        var imageSize = source * scale;
        ArtworkRect = new Rect2((_frame.Size - imageSize) / 2, imageSize);
        _image.Position = ArtworkRect.Position;
        _image.Size = ArtworkRect.Size;
        Ui.Place(_error, 12, _frame.Size.Y / 2 + 60, _frame.Size.X - 24, 50);
        PlaceArtworkControl(WallMap, MapRegion, scale);
        foreach (var hotspot in new[] { Hotspot, CalendarButton, ReminderButton, SafeButton, FilingButton, RolodexButton, PhoneButton, HangarButton, JacketButton })
            PlaceArtworkControl(hotspot, hotspot.Region, scale);
    }

    private void PlaceArtworkControl(Control control, Rect2 region, float scale)
    {
        control.Position = ArtworkRect.Position + region.Position * scale;
        control.Scale = Vector2.One * scale;
        control.Size = region.Size;
    }
}
