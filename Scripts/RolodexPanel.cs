using Godot;

namespace Mech;

/// <summary>The personnel files: the company's MechWarriors, those lost in memoriam, and those for hire where the DropShip orbits.</summary>
public partial class RolodexPanel : Control
{
    public event Action? Closed;
    public event Action<string>? RecruitRequested;
    public event Action<string>? DismissRequested;
    public Label CardName { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Button PreviousButton { get; private set; } = null!;
    public Button NextButton { get; private set; } = null!;
    /// <summary>Recruit on a card for hire, Dismiss on an active one; hidden on memorial cards.</summary>
    public Button ActionButton { get; private set; } = null!;
    public OptionButton Section { get; private set; } = null!;
    public const int ActiveSection = 0, MemorialSection = 1, HireSection = 2;
    private Campaign _campaign = null!;
    private int _slot;
    private ForceRoster Roster => _campaign.Roster!;
    private PanelContainer _panel = null!;
    private VBoxContainer _index = null!;
    private VBoxContainer _card = null!;
    private TextureRect _portrait = null!;
    private Label _record = null!;
    /// <summary>An active MechWarrior's skills with their progress bars; hidden on other cards, whose record lists the skills.</summary>
    public SkillBars Skills { get; private set; } = null!;
    private Label _position = null!;
    private int _selected;
    private sealed record Entry(WarriorSpec Spec, RosterWarrior? Warrior);
    private Entry[] _visible = [];
    /// <summary>The MechWarrior shown on the card, if any.</summary>
    public string? SelectedId => _visible.Length == 0 ? null : _visible[Math.Clamp(_selected, 0, _visible.Length - 1)].Spec.Id;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f) }; shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _panel = new PanelContainer(); _panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color("25291f"), Ui.Gold, 2)); AddChild(_panel);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); _panel.AddChild(body);
        var top = new HBoxContainer(); body.AddChild(top);
        var heading = Ui.Label(top, "PERSONNEL", 22, Ui.Gold); heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BackButton = Ui.Sound(Ui.Button(top, "Back to office"), Cue.UiCancel); BackButton.Pressed += RequestBack; BackButton.CustomMinimumSize = new Vector2(135, 38);
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 24); body.AddChild(content);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(180, 0) }; content.AddChild(left);
        Section = new OptionButton(); Section.AddItem("Active personnel"); Section.AddItem("In memoriam"); Section.AddItem("For hire"); left.AddChild(Section);
        Section.ItemSelected += _ => { _selected = 0; RebuildIndex(); ShowCard(); };
        _index = RosterUi.Scroll(left);
        _card = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _card.AddThemeConstantOverride("separation", 12); content.AddChild(_card);
        var record = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; record.AddThemeConstantOverride("separation", 20); _card.AddChild(record);
        _portrait = RosterUi.Picture(record, "", new Vector2(185, 160)); _portrait.SizeFlagsVertical = SizeFlags.ExpandFill;
        var details = RosterUi.Scroll(record);
        CardName = RosterUi.Text(details, "", 23, Ui.Gold);
        Skills = new SkillBars { CustomMinimumSize = new Vector2(220, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin }; details.AddChild(Skills);
        _record = RosterUi.Text(details, "", 16);
        var actions = new HBoxContainer(); details.AddChild(actions);
        ActionButton = Ui.Button(actions, "Recruit"); ActionButton.CustomMinimumSize = new Vector2(190, 38);
        ActionButton.Pressed += Act;
        var navigation = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; navigation.AddThemeConstantOverride("separation", 12); _card.AddChild(navigation);
        PreviousButton = Ui.Button(navigation, "← Previous"); PreviousButton.Pressed += () => Move(-1);
        _position = Ui.Label(navigation, "", 13, Ui.Muted);
        NextButton = Ui.Button(navigation, "Next →"); NextButton.Pressed += () => Move(1);
        PreviousButton.CustomMinimumSize = NextButton.CustomMinimumSize = new Vector2(115, 38);
        Resized += Layout; Layout(); Hide();
    }
    /// <summary>Opens on the company's active personnel, as the given player slot.</summary>
    public void Open(Campaign campaign, int slot = Campaign.LeaderSlot, string backText = "Back to office")
    {
        BackButton.Text = backText;
        _campaign = campaign; _slot = slot; _selected = 0; Section.Select(ActiveSection); Show(); RebuildIndex(); ShowCard(); Layout();
        RosterUi.TrapFocus(this); Section.CallDeferred(Control.MethodName.GrabFocus);
    }
    /// <summary>Shows a changed campaign, keeping the section and, where it is still listed, the MechWarrior on the card.</summary>
    public void Refresh(Campaign campaign)
    {
        if (!Visible || campaign.Roster is null) return;
        var shown = SelectedId; _campaign = campaign;
        RebuildIndex();
        var index = Array.FindIndex(_visible, item => item.Spec.Id == shown);
        if (index >= 0) _selected = index;
        ShowCard();
    }
    public void RequestBack() => Closed?.Invoke();

    private void RebuildIndex()
    {
        _visible = Section.Selected == HireSection ? [.. _campaign.OrbitRecruits.Select(spec => new Entry(spec, null))]
            : [.. Roster.Warriors.Where(item => (item.Health == WarriorHealth.Dead) == (Section.Selected == MemorialSection)).Select(item => new Entry(item.Spec, item))];
        RosterUi.Clear(_index);
        for (var i = 0; i < _visible.Length; i++)
        {
            var index = i; var entry = _visible[i];
            var button = Ui.Button(_index, $"“{entry.Spec.Callsign}”\n{entry.Spec.Name}"); button.CustomMinimumSize = new Vector2(0, 58);
            button.Pressed += () => { _selected = index; ShowCard(); };
            button.TooltipText = entry.Warrior?.Status ?? $"For hire · {RosterUi.Money(entry.Spec.Salary)} a quarter";
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
        PreviousButton.Disabled = NextButton.Disabled = _visible.Length < 2;
        if (_visible.Length == 0)
        {
            ActionButton.Hide(); Skills.Hide(); _portrait.Texture = null; _position.Text = "0 / 0";
            (CardName.Text, _record.Text) = Section.Selected switch
            {
                MemorialSection => ("No memorial records", "No MechWarriors have been lost."),
                HireSection => !_campaign.Travel.InOrbit ? ("No recruits in transit", "MechWarriors can only be hired where the DropShip is in orbit.")
                    : ("No MechWarriors for hire here", "No one is looking for work in this system today. Listings change from day to day."),
                _ => ("No active personnel", "All personnel records are in the memorial section."),
            };
            RosterUi.TrapFocus(this); return;
        }
        _selected = Math.Clamp(_selected, 0, _visible.Length - 1);
        var entry = _visible[_selected]; var spec = entry.Spec;
        _portrait.Texture = RosterUi.Portrait(spec); CardName.Text = $"“{spec.Callsign}”\n{spec.Name}";
        var salary = $"Salary: {RosterUi.Money(spec.Salary)} a quarter";
        if (entry.Warrior is not { } warrior)
        {
            Skills.Hide();
            _record.Text = $"FOR HIRE · {spec.Experience}\n{salary}\n\n{RosterUi.SkillLines(spec)}";
            var cost = _campaign.HireCost(spec); var problem = _campaign.HireProblem(spec);
            ActionButton.Text = $"Recruit · {RosterUi.Money(cost.Total)}"; ActionButton.Disabled = problem is not null;
            ActionButton.TooltipText = (problem is null ? "" : problem + "\n\n") + $"Hiring pays {RosterUi.Money(cost.Prorated)} for the {cost.DaysLeft} days left this quarter"
                + $" and a signing bonus of {RosterUi.Money(cost.SigningBonus)}, one quarter's salary.\nSalary: {RosterUi.Money(cost.Salary)} a quarter, from the next quarterly bill.";
            ActionButton.Show();
        }
        else
        {
            var assignment = Roster.Assignment(warrior.Id);
            var living = warrior.Health != WarriorHealth.Dead;
            Skills.Visible = living; if (living) Skills.Show(spec, warrior.Progress);
            _record.Text = $"{(living ? "" : RosterUi.SkillLines(spec) + "\n\n")}{spec.Experience}\n{warrior.Status}\n\nPiloting: {assignment?.Spec.DisplayName ?? "Unassigned"}\nDeployments: {warrior.Deployments}\nKills: {warrior.Kills}"
                + (warrior.Health == WarriorHealth.Dead ? "" : $"\n{salary}");
            ActionButton.Visible = warrior.Health != WarriorHealth.Dead;
            ActionButton.Text = "Dismiss";
            var foreign = assignment is not null && !Roster.Controls(_slot, assignment);
            ActionButton.Disabled = foreign;
            ActionButton.TooltipText = foreign ? "This MechWarrior pilots a BattleMech in another player's lance."
                : "Dismiss this MechWarrior for good. There is no refund, and they will not be available for hire again.";
        }
        _position.Text = $"{_selected + 1} / {_visible.Length}";
        RosterUi.TrapFocus(this);
    }
    private void Act()
    {
        if (ActionButton.Disabled || SelectedId is not { } id) return;
        if (_visible[_selected].Warrior is null) RecruitRequested?.Invoke(id); else DismissRequested?.Invoke(id);
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
