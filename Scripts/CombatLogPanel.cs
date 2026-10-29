using Godot;

namespace Mech;

/// <summary>Append-only battle history with independent scroll positions for each information view.</summary>
public partial class CombatLogPanel : PanelContainer
{
    public event Action? CloseRequested;
    public TabContainer Tabs { get; private set; } = null!;
    public RichTextLabel Combat { get; private set; } = null!;
    public RichTextLabel Debug { get; private set; } = null!;
    private CombatSimulation? _simulation;
    private int _read;
    private LogPage[] _pages = [];

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line));
        var body = new VBoxContainer(); AddChild(body);
        var header = new HBoxContainer(); body.AddChild(header);
        var title = Ui.Label(header, "BATTLE LOG  [~]", 14, Ui.Gold); title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var close = Ui.Button(header, "×"); close.TooltipText = "Hide battle log [Shift+`]";
        close.Pressed += () => CloseRequested?.Invoke();
        Tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddChild(Tabs);
        Combat = CreatePage("Combat"); Debug = CreatePage("Debug");
        _pages = [new(Combat), new(Debug)];
        Hide();
    }

    private RichTextLabel CreatePage(string name)
    {
        var text = new RichTextLabel { Name = name, BbcodeEnabled = false, FitContent = false,
            ScrollActive = true, ScrollFollowing = false, SelectionEnabled = true, ContextMenuEnabled = false,
            FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Stop };
        text.AddThemeFontSizeOverride("normal_font_size", 12);
        text.AddThemeColorOverride("default_color", Ui.Text);
        Tabs.AddChild(text); return text;
    }

    public void Display(CombatSimulation simulation)
    {
        _simulation = simulation; _read = 0; Tabs.CurrentTab = 0;
        foreach (var page in _pages) page.Clear();
    }

    public override void _Process(double delta)
    {
        foreach (var page in _pages) page.FinishLayout();
        if (_simulation is null) return;
        var history = _simulation.History;
        while (_read < history.Count)
        {
            var entry = history[_read++];
            if (entry.CombatText is { } combat) _pages[0].Append(combat);
            _pages[1].Append(entry.DebugText);
        }
    }

    private sealed class LogPage
    {
        private readonly RichTextLabel _text;
        private bool _pendingFollow;
        public LogPage(RichTextLabel text)
        {
            _text = text;
            // Explicit user scrolling wins over a queued tail-follow after text layout.
            void Scrolling(InputEvent input)
            {
                if (input is InputEventMouseButton { Pressed: true }) _pendingFollow = false;
            }
            text.GuiInput += Scrolling; text.GetVScrollBar().GuiInput += Scrolling;
        }
        public void Clear() { _text.Clear(); _pendingFollow = true; }
        public void Append(string text)
        {
            var bar = _text.GetVScrollBar();
            var follow = _pendingFollow || bar.Value >= bar.MaxValue - bar.Page - 2;
            if (_text.GetTotalCharacterCount() > 0) _text.AddText("\n\n");
            _text.AddText(text);
            _pendingFollow = follow;
        }
        public void FinishLayout()
        {
            if (!_pendingFollow || !_text.IsVisibleInTree()) return;
            var bar = _text.GetVScrollBar(); bar.Value = bar.MaxValue;
            _pendingFollow = false;
        }
    }
}
