using Godot;

namespace Mech;

public partial class HangarScreen : Control
{
    public event Action? BackRequested;
    public event Action<string, string?>? AssignmentRequested;
    public event Action? WarriorDetailsRequested;
    public event Action? DeploymentRequested;
    public Button BackButton { get; private set; } = null!;
    public Button WarriorsButton { get; private set; } = null!;
    public Button DeploymentButton { get; private set; } = null!;
    public OptionButton PilotPicker { get; private set; } = null!;
    public Button AssignButton { get; private set; } = null!;
    public Button UnassignButton { get; private set; } = null!;
    public List<Button> RosterButtons { get; } = [];
    public OwnedMech SelectedMech => _roster.Mechs[_selected];
    public MechPreview Preview { get; private set; } = null!;
    public OptionButton GaitPicker { get; private set; } = null!;
    public bool ArtworkLoaded => _background.Texture is not null;
    private ForceRoster _roster = null!;
    private int _selected;
    private TextureRect _background = null!;
    private TextureRect _mech = null!;
    private PanelContainer _pilotPanel = null!;
    private PanelContainer _detailsPanel = null!;
    private MechDetails _details = null!;
    private Label _title = null!;
    private Label _pilot = null!;
    private Label _readiness = null!;
    private TextureRect _portrait = null!;
    private HBoxContainer _rosterButtons = null!;
    private ScrollContainer _rosterScroll = null!;
    private readonly List<string> _pilotIds = [];

    public override void _Ready()
    {
        _background = RosterUi.Picture(this, "res://Assets/Roster/hangar-background.png", Vector2.Zero);
        _background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        var tint = new ColorRect { Color = new Color(0, 0, 0, .2f), MouseFilter = MouseFilterEnum.Ignore };
        tint.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(tint);
        Preview = new MechPreview(); AddChild(Preview);
        GaitPicker = new OptionButton { FitToLongestItem = false, ClipText = true, TooltipText = "Preview movement" };
        foreach (var name in new[] { "Idle", "Walk", "Run" }) GaitPicker.AddItem(name);
        AddChild(GaitPicker); GaitPicker.ItemSelected += index => Preview.Gait = (PreviewGait)index;
        _mech = RosterUi.Picture(this, "", Vector2.Zero);
        _title = Ui.Label(this, "MECH HANGAR", 25, Ui.Gold);
        _title.ClipText = true;
        BackButton = Ui.Button(this, "Back to office"); BackButton.Pressed += () => BackRequested?.Invoke();
        _pilotPanel = Panel(); _detailsPanel = Panel();
        var pilotBody = RosterUi.Scroll(_pilotPanel);
        RosterUi.Text(pilotBody, "PILOT ASSIGNMENT", 13, Ui.Gold);
        _portrait = RosterUi.Picture(pilotBody, "", new Vector2(0, 92));
        _pilot = RosterUi.Text(pilotBody, "", 15);
        _readiness = RosterUi.Text(pilotBody, "", 12, Ui.Muted);
        PilotPicker = new OptionButton { ClipText = true, FitToLongestItem = false }; pilotBody.AddChild(PilotPicker);
        PilotPicker.ItemSelected += _ => UpdateAssignmentAction();
        AssignButton = Ui.Button(pilotBody, "Assign warrior"); AssignButton.CustomMinimumSize = new Vector2(0, 36);
        AssignButton.Pressed += () => { if (PilotPicker.Selected >= 0 && PilotPicker.Selected < _pilotIds.Count) AssignmentRequested?.Invoke(SelectedMech.Id, _pilotIds[PilotPicker.Selected]); };
        UnassignButton = Ui.Button(pilotBody, "Unassign"); UnassignButton.Pressed += () => AssignmentRequested?.Invoke(SelectedMech.Id, null);
        WarriorsButton = Ui.Button(pilotBody, "View MechWarriors"); WarriorsButton.Pressed += () => WarriorDetailsRequested?.Invoke();
        DeploymentButton = Ui.Button(this, "Simulate Deployment");
        DeploymentButton.Pressed += () => DeploymentRequested?.Invoke();
        _details = new MechDetails { SizeFlagsVertical = SizeFlags.ExpandFill }; _detailsPanel.AddChild(_details);
        _rosterScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_rosterScroll); _rosterButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _rosterButtons.AddThemeConstantOverride("separation", 8); _rosterScroll.AddChild(_rosterButtons);
        Resized += Layout; Layout(); Hide();
    }
    private PanelContainer Panel()
    { var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color(.065f, .09f, .07f, .94f), Ui.Line)); AddChild(panel); return panel; }
    public void Open(ForceRoster roster)
    {
        if (!ReferenceEquals(_roster, roster)) _selected = 0;
        _roster = roster; _selected = Math.Clamp(_selected, 0, roster.Mechs.Count - 1); Show();
        RosterUi.Clear(_rosterButtons); RosterButtons.Clear();
        for (var i = 0; i < roster.Mechs.Count; i++)
        {
            var index = i; var mech = roster.Mechs[i];
            var button = Ui.Button(_rosterButtons, $"{mech.Spec.Chassis}\n{mech.Spec.Variant} · {mech.Spec.Tonnage}t");
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(160, 54);
            button.Pressed += () => Select(index); RosterButtons.Add(button);
        }
        Select(_selected); Layout(); BackButton.CallDeferred(Control.MethodName.GrabFocus);
    }
    public void Refresh()
    {
        if (_roster is not null) Select(_selected);
    }
    public void Select(int index)
    {
        _selected = index; var mech = SelectedMech; var warrior = _roster.Pilot(mech);
        _title.Text = $"MECH HANGAR  /  {_roster.ForceName}";
        _mech.Texture = Ui.LoadTexture(mech.Spec.Artwork); _details.Display(mech.Spec, mech);
        Preview.Display(mech);
        GaitPicker.Visible = Preview.Model!.IsAwesome;
        _portrait.Texture = warrior is null ? null : Ui.LoadTexture(warrior.Spec.Artwork);
        _pilot.Text = warrior is null ? "No pilot assigned" : $"“{warrior.Spec.Callsign}”\n{warrior.Spec.Name}\n{RosterUi.SkillText(warrior.Spec)}";
        _readiness.Text = warrior is null ? "Assign a MechWarrior to crew this BattleMech." : !warrior.Ready ? warrior.Status : mech.Operational ? "Ready for deployment" : "Pilot healthy · BattleMech not operational";
        UnassignButton.Disabled = warrior is null;
        PilotPicker.Clear(); _pilotIds.Clear();
        foreach (var candidate in _roster.Warriors.Where(item => item.Health != WarriorHealth.Dead))
        {
            var assignment = _roster.Assignment(candidate.Id);
            _pilotIds.Add(candidate.Id); PilotPicker.AddItem($"{candidate.Spec.Callsign} · G{candidate.Spec.Gunnery}/P{candidate.Spec.Piloting}{(candidate.Ready ? "" : " · injured")}{(assignment is null ? "" : $" · {assignment.Spec.Chassis}")}");
            PilotPicker.SetItemTooltip(_pilotIds.Count - 1, $"{candidate.Spec.DisplayName}\n{candidate.Status}\n{assignment?.Spec.DisplayName ?? "Unassigned"}");
        }
        var current = warrior is null ? _pilotIds.FindIndex(id => _roster.Assignment(id) is null) : _pilotIds.IndexOf(warrior.Id);
        PilotPicker.Select(current >= 0 ? current : _pilotIds.Count > 0 ? 0 : -1);
        PilotPicker.Disabled = _pilotIds.Count == 0; UpdateAssignmentAction();
        for (var i = 0; i < RosterButtons.Count; i++) RosterButtons[i].AddThemeStyleboxOverride("normal", Ui.Box(i == index ? new Color("344431") : Ui.Panel, i == index ? Ui.Gold : Ui.Line));
    }
    private void UpdateAssignmentAction()
    {
        var valid = PilotPicker.Selected >= 0 && PilotPicker.Selected < _pilotIds.Count;
        AssignButton.Disabled = !valid || SelectedMech.WarriorId == _pilotIds[PilotPicker.Selected];
        var other = valid ? _roster.Assignment(_pilotIds[PilotPicker.Selected]) : null;
        AssignButton.Text = other is null || other == SelectedMech ? "Assign warrior" : SelectedMech.WarriorId is null ? "Transfer warrior…" : "Swap warriors…";
    }
    private void Layout()
    {
        if (_background is null) return;
        Ui.Place(_background, 0, 0, Size.X, Size.Y);
        Ui.Place(_title, 24, 8, Size.X - 438, 48);
        Ui.Place(DeploymentButton, Size.X - 394, 14, 210, 36); Ui.Place(BackButton, Size.X - 174, 14, 150, 36);
        var leftWidth = Size.X < 1050 ? 196 : 246; var rightWidth = Size.X < 1050 ? 330 : 390;
        Ui.Place(_pilotPanel, 24, 70, leftWidth, Size.Y - 164);
        Ui.Place(_detailsPanel, Size.X - rightWidth - 24, 70, rightWidth, Size.Y - 164);
        var visualX = leftWidth + 34; var visualWidth = Size.X - leftWidth - rightWidth - 68;
        var visualHeight = Size.Y - 148;
        var artSize = Math.Clamp(visualWidth * .27f, 80, 210);
        Ui.Place(Preview, visualX, 60, visualWidth - artSize - 8, visualHeight);
        Ui.Place(_mech, visualX + visualWidth - artSize, 85, artSize, artSize);
        Ui.Place(GaitPicker, visualX + 8, 75, Math.Min(150, Preview.Size.X - 16), 34);
        Ui.Place(_rosterScroll, 24, Size.Y - 78, Size.X - 48, 68);
    }
}
