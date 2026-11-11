using Godot;
using System.Globalization;

namespace Mech;

/// <summary>A paged grid of save slots over the current screen, for saving into or loading from one library.</summary>
public partial class SaveBrowser : Control
{
    public const int Columns = 3;
    public const int Rows = 3;
    public const int PageSize = Columns * Rows;

    /// <summary>A slot was clicked: null is the empty slot that starts a save page.</summary>
    public event Action<SaveSlot?>? SlotChosen;
    public event Action? Closed;

    public Button CloseButton { get; private set; } = null!;
    public Button SaveTab { get; private set; } = null!;
    public Button LoadTab { get; private set; } = null!;
    public Button PreviousButton { get; private set; } = null!;
    public Button NextButton { get; private set; } = null!;
    public Label PageLabel { get; private set; } = null!;
    public Label Heading { get; private set; } = null!;
    public Label Status { get; private set; } = null!;
    public IReadOnlyList<Button> SlotButtons => _slotButtons;
    public bool Saving { get; private set; }
    public int Page { get; private set; }
    public int PageCount => Math.Max(1, (Entries.Count + PageSize - 1) / PageSize);
    public SaveLibrary? Library { get; private set; }
    /// <summary>The cards in order: in save mode the empty slot (null) comes first, then every save, newest first.</summary>
    public IReadOnlyList<SaveSlot?> Entries { get; private set; } = [];
    /// <summary>The entry each visible button stands for.</summary>
    public IReadOnlyList<SaveSlot?> PageEntries => [.. Entries.Skip(Page * PageSize).Take(PageSize)];

    private readonly List<Button> _slotButtons = [];
    private PanelContainer _panel = null!;
    private GridContainer _grid = null!;
    private HBoxContainer _tabs = null!;
    private HBoxContainer _paging = null!;
    private Label _empty = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _panel = new PanelContainer(); _panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color("25291f"), Ui.Gold, 2)); AddChild(_panel);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 14); _panel.AddChild(body);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 10); body.AddChild(top);
        Heading = Ui.Label(top, "SAVE GAME", 22, Ui.Gold); Heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tabs = new HBoxContainer(); _tabs.AddThemeConstantOverride("separation", 6); top.AddChild(_tabs);
        SaveTab = Tab("Save", true); LoadTab = Tab("Load", false);
        CloseButton = Ui.Button(top, "Close"); CloseButton.CustomMinimumSize = new Vector2(110, 38);
        CloseButton.Pressed += () => Closed?.Invoke();
        Status = Ui.Label(body, "", 13, Ui.Muted);
        _grid = new GridContainer { Columns = Columns, SizeFlagsVertical = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 12); _grid.AddThemeConstantOverride("v_separation", 12);
        body.AddChild(_grid);
        for (var index = 0; index < PageSize; index++)
        {
            var button = new Button { FocusMode = FocusModeEnum.All, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 92), ClipContents = true };
            var lines = new VBoxContainer { Name = "Lines", MouseFilter = MouseFilterEnum.Ignore };
            lines.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            lines.OffsetLeft = 14; lines.OffsetRight = -14; lines.OffsetTop = 10; lines.OffsetBottom = -10;
            lines.AddThemeConstantOverride("separation", 4);
            button.AddChild(lines);
            var name = Ui.Label(lines, "", 17, Ui.Gold); name.Name = "SlotName"; name.ClipText = true; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            Ui.Label(lines, "", 14).Name = "SlotDate";
            Ui.Label(lines, "", 14).Name = "SlotCash";
            var slot = index;
            button.Pressed += () => Choose(slot);
            _grid.AddChild(button); _slotButtons.Add(button);
        }
        _empty = Ui.Label(body, "No saved games.", 16, Ui.Muted); _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _paging = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; _paging.AddThemeConstantOverride("separation", 16); body.AddChild(_paging);
        PreviousButton = Ui.Button(_paging, "◀ Previous"); PreviousButton.CustomMinimumSize = new Vector2(130, 38);
        PreviousButton.Pressed += () => TurnPage(-1);
        PageLabel = Ui.Label(_paging, "", 14, Ui.Muted); PageLabel.CustomMinimumSize = new Vector2(110, 0); PageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        NextButton = Ui.Button(_paging, "Next ▶"); NextButton.CustomMinimumSize = new Vector2(130, 38);
        NextButton.Pressed += () => TurnPage(1);
        Resized += Layout; Layout(); Hide();
    }

    private Button Tab(string text, bool saving)
    {
        var tab = Ui.Button(_tabs, text);
        tab.ToggleMode = true; tab.CustomMinimumSize = new Vector2(90, 38);
        tab.Pressed += () => SetMode(saving);
        return tab;
    }

    /// <summary>Shows <paramref name="library"/> for saving or loading. With <paramref name="tabs"/>, the other mode is one click away.</summary>
    public void Open(SaveLibrary library, bool saving, bool tabs)
    {
        Library = library; _tabs.Visible = tabs; Status.Text = "";
        Show();
        SetMode(saving);
    }

    public void SetMode(bool saving)
    {
        Saving = saving; Page = 0;
        Refresh();
        FocusFirst();
    }

    /// <summary>Reads the library again, keeping the page where possible.</summary>
    public void Refresh()
    {
        if (Library is null) return;
        var slots = Library.List();
        Entries = Saving ? [null, .. slots] : [.. slots];
        Page = Math.Clamp(Page, 0, PageCount - 1);
        Heading.Text = (Saving ? "SAVE GAME" : "LOAD GAME") + (Library.Coop ? " · CO-OP" : "");
        SaveTab.SetPressedNoSignal(Saving); LoadTab.SetPressedNoSignal(!Saving);
        var entries = PageEntries;
        for (var index = 0; index < PageSize; index++)
        {
            var button = _slotButtons[index];
            var shown = index < entries.Count;
            // Hidden cards keep their place so the grid does not reflow on the last page.
            button.Modulate = shown ? Colors.White : Colors.Transparent;
            button.Disabled = !shown; button.MouseFilter = shown ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            button.FocusMode = shown ? FocusModeEnum.All : FocusModeEnum.None;
            var (name, date, cash, tooltip) = shown ? Describe(entries[index]) : ("", "", "", "");
            button.GetNode<Label>("Lines/SlotName").Text = name;
            button.GetNode<Label>("Lines/SlotDate").Text = date;
            button.GetNode<Label>("Lines/SlotCash").Text = cash;
            button.TooltipText = tooltip;
        }
        _empty.Visible = Entries.Count == 0;
        _grid.Visible = Entries.Count > 0;
        _paging.Visible = PageCount > 1;
        PageLabel.Text = $"Page {Page + 1} of {PageCount}";
        PreviousButton.Disabled = Page == 0;
        NextButton.Disabled = Page >= PageCount - 1;
    }

    private (string Name, string Date, string Cash, string Tooltip) Describe(SaveSlot? slot) => slot switch
    {
        null => ("Empty slot", "New save", "", "Save the company in a new slot."),
        { Info: null } => ("Unreadable save", "", "", "This save cannot be read."),
        { Info: { } info } => (info.Name, DateText(info.Date), HangarScreen.Money(info.Cash),
            $"{info.Company}\nSaved {info.SavedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)}"),
    };

    public static string DateText(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

    public void TurnPage(int delta)
    {
        var page = Math.Clamp(Page + delta, 0, PageCount - 1);
        if (page == Page) return;
        Page = page; Refresh();
        // Keep focus on the control that turned the page, or move into the grid once it is disabled.
        var turner = delta < 0 ? PreviousButton : NextButton;
        (turner.Disabled ? (Control)_slotButtons[0] : turner).CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Choose(int index)
    {
        var entries = PageEntries;
        if (index < entries.Count) SlotChosen?.Invoke(entries[index]);
    }

    /// <summary>The control that takes focus when the browser opens or returns from a prompt.</summary>
    public Control FocusTarget => Entries.Count > 0 ? _slotButtons[0] : CloseButton;

    public void FocusFirst() => FocusTarget.CallDeferred(Control.MethodName.GrabFocus);

    private void Layout()
    {
        if (_panel is null) return;
        var width = Math.Min(960, Size.X - 48);
        var height = Math.Min(620, Size.Y - 48);
        Ui.Place(_panel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
}
