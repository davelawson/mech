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
    [Export(PropertyHint.File, "*.png")] public string ArtworkPath { get; set; } = "res://Assets/dropship-office-missions.png";
    public Button PhoneButton { get; private set; } = null!;
    public ColorRect PhoneLight { get; private set; } = null!;
    public bool PhoneFlashing { get; private set; }
    public Button RolodexButton { get; private set; } = null!;
    public Button HangarButton { get; private set; } = null!;
    public Label ReadyCount { get; private set; } = null!;
    public Button Hotspot { get; private set; } = null!;
    public Button CalendarButton { get; private set; } = null!;
    public Button ReminderButton { get; private set; } = null!;
    public Button SafeButton { get; private set; } = null!;
    public Button FilingButton { get; private set; } = null!;
    public Label CalendarDate { get; private set; } = null!;
    public Label QuarterText { get; private set; } = null!;
    public Label SafeReadout => _safeReadout;
    public TextureRect WallMap { get; private set; } = null!;
    public Rect2 ArtworkRect { get; private set; }
    public bool ArtworkLoaded => _image.Texture is not null;
    // Regions are measured in source-art pixels; all controls use the artwork scale.
    public static readonly Vector2 ArtworkSize = new(1672, 940);
    public static readonly Rect2 MapRegion = new(347, 112, 288, 288f * 6 / 7);
    public static readonly Rect2 ChartRegion = new(330, 84, 322, 308);
    public static readonly Rect2 CalendarRegion = new(722, 168, 190, 102);
    public static readonly Rect2 ReminderRegion = new(767, 271, 95, 40);
    public static readonly Rect2 SafeRegion = new(324, 447, 273, 234);
    public static readonly Rect2 FilingRegion = new(4, 329, 155, 477);
    public static readonly Rect2 RolodexRegion = new(695, 552, 124, 104);
    public static readonly Rect2 PhoneRegion = new(1116, 558, 129, 83);
    public static readonly Rect2 HangarRegion = new(1470, 70, 202, 390);
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
        Hotspot = Ui.Button(_frame, "");
        Hotspot.Name = "StarMapHotspot";
        Hotspot.TooltipText = "Open star map";
        StyleHotspot(Hotspot);
        Hotspot.Pressed += () => EmitSignal(SignalName.MapRequested);
        _error = Ui.Label(_frame, "Office artwork could not load. The star map is still available.", 16, Ui.Gold);
        _error.HorizontalAlignment = HorizontalAlignment.Center;
        CreateCampaignControls();
        PhoneButton = Ui.Button(_frame, ""); PhoneButton.Name = "MissionPhone"; StyleHotspot(PhoneButton);
        PhoneButton.Pressed += () => EmitSignal(SignalName.MissionsRequested);
        PhoneLight = new ColorRect { Color = new Color("ffbd54"), MouseFilter = MouseFilterEnum.Ignore };
        PhoneButton.AddChild(PhoneLight); Ui.Place(PhoneLight, 85, 49, 8, 6); PhoneLight.Hide();
        RolodexButton = Ui.Button(_frame, ""); RolodexButton.Name = "Rolodex"; StyleHotspot(RolodexButton);
        RolodexButton.Pressed += () => EmitSignal(SignalName.WarriorsRequested);
        ReadyCount = Ui.Label(RolodexButton, "0", 16, new Color("e4d5ae")); ReadyCount.HorizontalAlignment = HorizontalAlignment.Center;
        Ui.Place(ReadyCount, 52, 68, 48, 24);
        HangarButton = Ui.Button(_frame, ""); HangarButton.Name = "HangarPorthole"; HangarButton.TooltipText = "Porthole · Open mech hangar"; StyleHotspot(HangarButton);
        HangarButton.Pressed += () => EmitSignal(SignalName.HangarRequested);
        _hint = Ui.Label(this, "Phone: missions · Rolodex: MechWarriors · Porthole: hangar · Calendar: next day · Reminder: next event · Safe: finances · Cabinet: save / load", 12, Ui.Muted);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Resized += Layout;
        SetArtwork(ArtworkPath);
    }

    private static void StyleHotspot(Button button)
    {
        button.MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "disabled" })
            button.AddThemeStyleboxOverride(state, Ui.Box(Colors.Transparent, Colors.Transparent));
        button.AddThemeStyleboxOverride("hover", Ui.Box(new Color(1, .9f, .65f, .04f), Ui.Gold));
        button.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(1, .9f, .65f, .08f), Ui.Gold));
        button.AddThemeStyleboxOverride("focus", Ui.Box(Colors.Transparent, Ui.Gold));
    }

    public void SetWallMap(Texture2D? texture) => WallMap.Texture = texture;

    private void CreateCampaignControls()
    {
        CalendarButton = Ui.Button(_frame, "");
        CalendarButton.Name = "Calendar";
        CalendarButton.TooltipText = "Advance one day · Click again to stop";
        StyleHotspot(CalendarButton);
        CalendarButton.Pressed += () => EmitSignal(SignalName.DayRequested);
        CalendarDate = Ui.Label(CalendarButton, "", 16, new Color("b6d4b1"));
        CalendarDate.HorizontalAlignment = HorizontalAlignment.Center;
        CalendarDate.ClipText = true;
        Ui.Place(CalendarDate, 27, 26, 120, 31);
        QuarterText = Ui.Label(CalendarButton, "", 11, Ui.Gold);
        QuarterText.HorizontalAlignment = HorizontalAlignment.Center;
        QuarterText.ClipText = true;
        Ui.Place(QuarterText, 27, 62, 120, 29);

        ReminderButton = Ui.Button(_frame, "");
        ReminderButton.Name = "Reminder";
        StyleHotspot(ReminderButton);
        var reminderText = Ui.Label(ReminderButton, "Reminder...", 14, new Color("3d3520"));
        reminderText.HorizontalAlignment = HorizontalAlignment.Center;
        reminderText.ClipText = true;
        Ui.Place(reminderText, 2, 4, 91, 31);
        ReminderButton.TooltipText = "Advance to the next event · Click anywhere to stop";
        ReminderButton.Pressed += () => EmitSignal(SignalName.EventRequested);

        SafeButton = Ui.Button(_frame, "");
        SafeButton.Name = "CompanySafe";
        SafeButton.TooltipText = "Company safe · Quarterly finances";
        StyleHotspot(SafeButton);
        _safeReadout = Ui.Label(SafeButton, "", 20, new Color("b6d4b1"));
        _safeReadout.HorizontalAlignment = HorizontalAlignment.Center;
        _safeReadout.ClipText = true;
        Ui.Place(_safeReadout, 58, 29, 157, 34);
        SafeButton.Pressed += () => EmitSignal(SignalName.FinancesRequested);

        FilingButton = Ui.Button(_frame, "");
        FilingButton.Name = "FilingCabinet";
        FilingButton.TooltipText = "Filing cabinet · Save / load game";
        StyleHotspot(FilingButton);
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
        PlaceArtworkControl(PhoneButton, PhoneRegion, scale);
        PlaceArtworkControl(RolodexButton, RolodexRegion, scale);
        PlaceArtworkControl(HangarButton, HangarRegion, scale);
        PlaceArtworkControl(WallMap, MapRegion, scale);
        PlaceArtworkControl(Hotspot, ChartRegion, scale);
        PlaceArtworkControl(CalendarButton, CalendarRegion, scale);
        PlaceArtworkControl(ReminderButton, ReminderRegion, scale);
        PlaceArtworkControl(SafeButton, SafeRegion, scale);
        PlaceArtworkControl(FilingButton, FilingRegion, scale);
    }

    private void PlaceArtworkControl(Control control, Rect2 region, float scale)
    {
        control.Position = ArtworkRect.Position + region.Position * scale;
        control.Scale = Vector2.One * scale;
        control.Size = region.Size;
    }
}
