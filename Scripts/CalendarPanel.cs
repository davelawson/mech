using Godot;
using System.Globalization;

namespace Mech;

/// <summary>The office calendar's upcoming events: one row per dated event, soonest first. Clicking a row runs time to its date;
/// Next Day advances a single day and leaves the list open.</summary>
public partial class CalendarPanel : Control
{
    public event Action? Closed;
    public event Action? NextDayRequested;
    public event Action<DateOnly>? JumpRequested;
    public Button CloseButton { get; private set; } = null!;
    public Button NextDayButton { get; private set; } = null!;
    /// <summary>Why time cannot advance from here, or empty.</summary>
    public Label Note { get; private set; } = null!;
    public IReadOnlyList<Button> Rows => _rows;
    /// <summary>The events listed, in row order.</summary>
    public IReadOnlyList<UpcomingEvent> Events { get; private set; } = [];
    private readonly List<Button> _rows = [];
    private PanelContainer _panel = null!;
    private VBoxContainer _list = null!;
    private ScrollContainer _scroll = null!;
    private const int DaysWidth = 70, DateWidth = 130, RowHeight = 34, VisibleRows = 12;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _panel = new PanelContainer(); _panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2)); AddChild(_panel);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 10); _panel.AddChild(body);
        Ui.Label(body, "UPCOMING EVENTS", 22, Ui.Gold);
        // Inset like the rows' cells, so each heading sits over its column.
        var inset = new MarginContainer(); inset.AddThemeConstantOverride("margin_left", 6); inset.AddThemeConstantOverride("margin_right", 8); body.AddChild(inset);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); inset.AddChild(header);
        Column(header, "DAYS", DaysWidth, 12, Ui.Muted, HorizontalAlignment.Right);
        Column(header, "DATE", DateWidth, 12, Ui.Muted);
        Column(header, "EVENT", 0, 12, Ui.Muted);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(_scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _list.AddThemeConstantOverride("separation", 2); _scroll.AddChild(_list);
        Note = Ui.Label(body, "", 13, Ui.Muted);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; actions.AddThemeConstantOverride("separation", 12); body.AddChild(actions);
        NextDayButton = Ui.Button(actions, "Next Day"); NextDayButton.CustomMinimumSize = new Vector2(120, 44);
        NextDayButton.Pressed += () => NextDayRequested?.Invoke();
        CloseButton = Ui.Sound(Ui.Button(actions, "Close"), Cue.UiCancel); CloseButton.CustomMinimumSize = new Vector2(120, 44);
        CloseButton.Pressed += () => Closed?.Invoke();
        Resized += Layout; Layout(); Hide();
    }

    private static Label Column(Node parent, string text, int width, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = Ui.Label(parent, text, size, color);
        label.HorizontalAlignment = align; label.VerticalAlignment = VerticalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore;
        if (width > 0) label.CustomMinimumSize = new Vector2(width, 0);
        else { label.SizeFlagsHorizontal = SizeFlags.ExpandFill; label.ClipText = true; }
        return label;
    }

    /// <summary>Shows the campaign's upcoming events; <paramref name="blocked"/> explains why time cannot advance, disabling rows and Next Day.</summary>
    public void Open(Campaign campaign, string? blocked)
    {
        Show(); Refresh(campaign, blocked);
        (_rows.FirstOrDefault(row => !row.Disabled) ?? CloseButton).CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Refresh(Campaign campaign, string? blocked)
    {
        RosterUi.Clear(_list); _rows.Clear();
        Events = campaign.UpcomingEvents;
        foreach (var item in Events)
        {
            var row = Ui.Button(_list, ""); row.CustomMinimumSize = new Vector2(0, RowHeight);
            row.Disabled = blocked is not null;
            var cells = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; cells.AddThemeConstantOverride("separation", 12);
            row.AddChild(cells); cells.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); cells.OffsetLeft = 6; cells.OffsetRight = -8;
            var days = item.Date.DayNumber - campaign.Date.DayNumber;
            Column(cells, days.ToString(CultureInfo.InvariantCulture), DaysWidth, 15, Ui.Gold, HorizontalAlignment.Right);
            Column(cells, item.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant(), DateWidth, 15, Ui.Text);
            Column(cells, item.Title, 0, 15, Ui.Text);
            row.TooltipText = blocked ?? $"Advance to {item.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant()} · Click anywhere to stop";
            var date = item.Date;
            row.Pressed += () => JumpRequested?.Invoke(date);
            _rows.Add(row);
        }
        NextDayButton.Disabled = blocked is not null;
        Note.Text = blocked ?? "";
        Note.Visible = blocked is not null;
        Layout();
    }

    private void Layout()
    {
        if (_panel is null) return;
        var width = Math.Min(760, Size.X - 48);
        // Up to twelve rows show at once, fewer when the window is short; the rest scroll.
        var rows = Math.Clamp(Events.Count, 1, VisibleRows) * (RowHeight + 2);
        _scroll.CustomMinimumSize = Vector2.Zero;
        var chrome = _panel.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(0, Math.Max(RowHeight, Math.Min(rows, Size.Y - 48 - chrome)));
        var height = Math.Min(_panel.GetCombinedMinimumSize().Y, Size.Y - 48);
        Ui.Place(_panel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
}
