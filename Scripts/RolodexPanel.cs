using Godot;

namespace Mech;

public partial class RolodexPanel : Control
{
    public event Action? Closed;
    public Label CardName { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Button PreviousButton { get; private set; } = null!;
    public Button NextButton { get; private set; } = null!;
    public OptionButton Section { get; private set; } = null!;
    private ForceRoster _roster = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _index = null!;
    private VBoxContainer _card = null!;
    private Label _summary = null!;
    private TextureRect _portrait = null!;
    private Label _record = null!;
    private Label _position = null!;
    private int _selected;
    private RosterWarrior[] _visible = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f) }; shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _panel = new PanelContainer(); _panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color("25291f"), Ui.Gold, 2)); AddChild(_panel);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); _panel.AddChild(body);
        var top = new HBoxContainer(); body.AddChild(top);
        var heading = Ui.Label(top, "PERSONNEL / ROLODEX", 22, Ui.Gold); heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BackButton = Ui.Button(top, "Back to office"); BackButton.Pressed += RequestBack; BackButton.CustomMinimumSize = new Vector2(135, 38);
        _summary = Ui.Label(body, "", 13, Ui.Muted);
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 24); body.AddChild(content);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(180, 0) }; content.AddChild(left);
        Section = new OptionButton(); Section.AddItem("Active personnel"); Section.AddItem("In memoriam"); left.AddChild(Section);
        Section.ItemSelected += _ => { _selected = 0; RebuildIndex(); ShowCard(); };
        _index = RosterUi.Scroll(left);
        _card = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _card.AddThemeConstantOverride("separation", 12); content.AddChild(_card);
        var record = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; record.AddThemeConstantOverride("separation", 20); _card.AddChild(record);
        _portrait = RosterUi.Picture(record, "", new Vector2(185, 160)); _portrait.SizeFlagsVertical = SizeFlags.ExpandFill;
        var details = RosterUi.Scroll(record);
        CardName = RosterUi.Text(details, "", 23, Ui.Gold);
        _record = RosterUi.Text(details, "", 16);
        var navigation = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; navigation.AddThemeConstantOverride("separation", 12); _card.AddChild(navigation);
        PreviousButton = Ui.Button(navigation, "← Previous"); PreviousButton.Pressed += () => Move(-1);
        _position = Ui.Label(navigation, "", 13, Ui.Muted);
        NextButton = Ui.Button(navigation, "Next →"); NextButton.Pressed += () => Move(1);
        PreviousButton.CustomMinimumSize = NextButton.CustomMinimumSize = new Vector2(115, 38);
        Resized += Layout; Layout(); Hide();
    }
    public void Open(ForceRoster roster, string backText = "Back to office")
    {
        BackButton.Text = backText;
        _roster = roster; _selected = 0; Section.Select(0); Show(); RebuildIndex(); ShowCard(); Layout();
        RosterUi.TrapFocus(this); Section.CallDeferred(Control.MethodName.GrabFocus);
    }
    public void RequestBack() => Closed?.Invoke();

    private void RebuildIndex()
    {
        _visible = _roster.Warriors.Where(item => (item.Health == WarriorHealth.Dead) == (Section.Selected == 1)).ToArray();
        RosterUi.Clear(_index);
        for (var i = 0; i < _visible.Length; i++)
        {
            var index = i; var warrior = _visible[i];
            var button = Ui.Button(_index, $"“{warrior.Spec.Callsign}”\n{warrior.Spec.Name}"); button.CustomMinimumSize = new Vector2(0, 58);
            button.Pressed += () => { _selected = index; ShowCard(); }; button.TooltipText = warrior.Status;
        }
        RosterUi.TrapFocus(this);
    }
    private void Move(int direction)
    {
        if (_visible.Length == 0) return;
        _selected = (_selected + direction + _visible.Length) % _visible.Length; ShowCard();
    }
    private void ShowCard()
    {
        _summary.Text = $"{_roster.ForceName}  ·  {_roster.ReadyWarriors} ready  ·  {_roster.Warriors.Count(item => item.Health == WarriorHealth.Injured)} injured  ·  {_roster.Warriors.Count(item => item.Health == WarriorHealth.Dead)} in memoriam";
        PreviousButton.Disabled = NextButton.Disabled = _visible.Length < 2;
        if (_visible.Length == 0)
        { CardName.Text = Section.Selected == 1 ? "No memorial records" : "No active personnel"; _portrait.Texture = null; _record.Text = Section.Selected == 1 ? "No MechWarriors have been lost." : "All personnel records are in the memorial section."; _position.Text = "0 / 0"; return; }
        _selected = Math.Clamp(_selected, 0, _visible.Length - 1);
        var warrior = _visible[_selected]; var assignment = _roster.Assignment(warrior.Id);
        _portrait.Texture = Ui.LoadTexture(warrior.Spec.Artwork); CardName.Text = $"“{warrior.Spec.Callsign}”\n{warrior.Spec.Name}";
        _record.Text = $"{warrior.Spec.Experience}\n\nGunnery  {warrior.Spec.Gunnery}\nPiloting   {warrior.Spec.Piloting}\n\n{warrior.Status}\n\nAssigned BattleMech\n{assignment?.Spec.DisplayName ?? "Unassigned"}\n\nLower skill numbers are better.";
        _position.Text = $"{_selected + 1} / {_visible.Length}";
    }
    private void Layout()
    {
        if (_panel is null) return;
        var width = Math.Min(960, Size.X - 48); var height = Math.Min(590, Size.Y - 48);
        Ui.Place(_panel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { RequestBack(); GetViewport().SetInputAsHandled(); }
    }
}
