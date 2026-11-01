using System.Globalization;
using Godot;

namespace Mech;

/// <summary>Storage on the left, the selected mech as a blueprint with one panel per body part, and the pilot and summary on the right.</summary>
public partial class HangarScreen : Control
{
    public const float CompactWidth = 1100;
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
    public bool ArtworkLoaded => _background.Texture is not null;
    public IReadOnlyDictionary<MechLocation, HangarLocationPanel> LocationPanels => _locations;
    public PanelContainer StoragePanel { get; private set; } = null!;
    public PanelContainer SummaryPanel { get; private set; } = null!;
    public ScrollContainer Board { get; private set; } = null!;
    public VBoxContainer StorageList { get; private set; } = null!;
    public Label SpeedText { get; private set; } = null!;
    public Label ProtectionText { get; private set; } = null!;
    public Label HeatText { get; private set; } = null!;
    public bool Compact => Size.X < CompactWidth;
    private ForceRoster _roster = null!;
    private int _selected;
    private TextureRect _background = null!;
    private Label _mechName = null!;
    private Label _mechClass = null!;
    private Label _mechStatus = null!;
    private Label _pilot = null!;
    private Label _readiness = null!;
    private TextureRect _portrait = null!;
    private Control _boardContent = null!;
    private HBoxContainer _rosterButtons = null!;
    private ScrollContainer _rosterScroll = null!;
    private readonly Dictionary<MechLocation, HangarLocationPanel> _locations = [];
    private readonly List<string> _pilotIds = [];

    public override void _Ready()
    {
        _background = RosterUi.Picture(this, "res://Assets/Roster/hangar-background.png", Vector2.Zero);
        _background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        var tint = new ColorRect { Color = new Color(0, .02f, .04f, .62f), MouseFilter = MouseFilterEnum.Ignore };
        tint.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(tint);
        Preview = new MechPreview { Blueprint = true }; AddChild(Preview);
        Board = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, MouseFilter = MouseFilterEnum.Pass };
        AddChild(Board);
        _boardContent = new Control { MouseFilter = MouseFilterEnum.Pass }; Board.AddChild(_boardContent);
        foreach (var location in Enum.GetValues<MechLocation>())
        { var panel = new HangarLocationPanel(location); _boardContent.AddChild(panel); _locations[location] = panel; }
        BackButton = Ui.Button(this, "Back to office"); BackButton.Pressed += () => BackRequested?.Invoke();

        StoragePanel = Panel();
        var storage = RosterUi.Scroll(StoragePanel);
        storage.AddThemeConstantOverride("separation", 4);
        RosterUi.Text(storage, "STORAGE", 13, Ui.Gold);
        RosterUi.Text(storage, "Spare equipment, not installed", 11, Ui.Muted);
        StorageList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StorageList.AddThemeConstantOverride("separation", 2); storage.AddChild(StorageList);

        SummaryPanel = Panel();
        var summary = RosterUi.Scroll(SummaryPanel);
        summary.AddThemeConstantOverride("separation", 6);
        RosterUi.Text(summary, "MECHWARRIOR", 13, Ui.Gold);
        var pilotRow = new HBoxContainer(); pilotRow.AddThemeConstantOverride("separation", 8); summary.AddChild(pilotRow);
        _portrait = RosterUi.Picture(pilotRow, "", new Vector2(72, 72));
        _pilot = RosterUi.Text(pilotRow, "", 13); _pilot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _readiness = RosterUi.Text(summary, "", 11, Ui.Muted);
        PilotPicker = new OptionButton { ClipText = true, FitToLongestItem = false }; summary.AddChild(PilotPicker);
        PilotPicker.ItemSelected += _ => UpdateAssignmentAction();
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 6); summary.AddChild(actions);
        AssignButton = Ui.Button(actions, "Assign warrior"); AssignButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AssignButton.Pressed += () => { if (PilotPicker.Selected >= 0 && PilotPicker.Selected < _pilotIds.Count) AssignmentRequested?.Invoke(SelectedMech.Id, _pilotIds[PilotPicker.Selected]); };
        UnassignButton = Ui.Button(actions, "Unassign"); UnassignButton.Pressed += () => AssignmentRequested?.Invoke(SelectedMech.Id, null);
        WarriorsButton = Ui.Button(summary, "View MechWarriors"); WarriorsButton.Pressed += () => WarriorDetailsRequested?.Invoke();
        summary.AddChild(new HSeparator());
        _mechName = RosterUi.Text(summary, "", 20, Ui.Gold);
        _mechClass = RosterUi.Text(summary, "", 11, Ui.Muted);
        _mechStatus = RosterUi.Text(summary, "", 13, new Color("a5c4a5"));
        RosterUi.Text(summary, "MOVEMENT", 11, Ui.Gold); SpeedText = RosterUi.Text(summary, "", 13);
        RosterUi.Text(summary, "PROTECTION", 11, Ui.Gold); ProtectionText = RosterUi.Text(summary, "", 13);
        RosterUi.Text(summary, "HEAT", 11, Ui.Gold); HeatText = RosterUi.Text(summary, "", 13);

        DeploymentButton = Ui.Button(this, "Simulate Deployment");
        DeploymentButton.Pressed += () => DeploymentRequested?.Invoke();
        _rosterScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_rosterScroll); _rosterButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _rosterButtons.AddThemeConstantOverride("separation", 8); _rosterScroll.AddChild(_rosterButtons);
        Resized += Layout; Layout(); Hide();
    }
    private PanelContainer Panel()
    { var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color(.045f, .07f, .065f, .9f), Ui.Line)); AddChild(panel); return panel; }
    public void Open(ForceRoster roster)
    {
        if (!ReferenceEquals(_roster, roster)) _selected = 0;
        _roster = roster; _selected = Math.Clamp(_selected, 0, roster.Mechs.Count - 1); Show();
        RosterUi.Clear(_rosterButtons); RosterButtons.Clear();
        for (var i = 0; i < roster.Mechs.Count; i++)
        {
            var index = i; var mech = roster.Mechs[i];
            var button = Ui.Button(_rosterButtons, $"{mech.Spec.Chassis}\n{mech.Spec.Variant} · {mech.Spec.Tonnage}t");
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(150, 54);
            button.Pressed += () => Select(index); RosterButtons.Add(button);
        }
        ShowStorage();
        Select(_selected); Layout(); BackButton.CallDeferred(Control.MethodName.GrabFocus);
    }
    public void Refresh()
    {
        if (_roster is not null) { ShowStorage(); Select(_selected); }
    }
    public void Select(int index)
    {
        _selected = index; var mech = SelectedMech; var spec = mech.Spec; var warrior = _roster.Pilot(mech);
        Preview.Display(mech);
        var layout = CriticalLayout.For(spec);
        foreach (var (location, panel) in _locations) panel.Display(mech, layout[location]);
        _mechName.Text = spec.DisplayName;
        _mechClass.Text = $"{DefinitionText.WeightClass(spec.WeightClass).ToUpperInvariant()}  ·  {spec.Tonnage} TONS  ·  {spec.Role}\n{spec.Engine}";
        _mechStatus.Text = mech.Status;
        SpeedText.Text = SpeedSummary(spec);
        ProtectionText.Text = $"Armor {mech.Locations.Sum(item => item.Armor + item.RearArmor):N0}/{spec.ArmorTotal:N0}  ·  Structure {mech.Locations.Sum(item => item.Structure):N0}/{spec.StructureTotal:N0}";
        HeatText.Text = HeatSummary(spec);
        _portrait.Texture = warrior is null ? null : Ui.LoadTexture(warrior.Spec.Artwork);
        _pilot.Text = warrior is null ? "No pilot assigned" : $"“{warrior.Spec.Callsign}”\n{warrior.Spec.Name}\nG{warrior.Spec.Gunnery} / P{warrior.Spec.Piloting} / S{warrior.Spec.Sensors}";
        _pilot.TooltipText = warrior is null ? "" : RosterUi.SkillText(warrior.Spec);
        _readiness.Text = warrior is null ? "Assign a MechWarrior to crew this BattleMech." : !warrior.Ready ? warrior.Status : mech.Operational ? "Ready for deployment" : "Pilot healthy · BattleMech not operational";
        UnassignButton.Disabled = warrior is null;
        PilotPicker.Clear(); _pilotIds.Clear();
        foreach (var candidate in _roster.Warriors.Where(item => item.Health != WarriorHealth.Dead))
        {
            var assignment = _roster.Assignment(candidate.Id);
            _pilotIds.Add(candidate.Id); PilotPicker.AddItem($"{candidate.Spec.Callsign} · G{candidate.Spec.Gunnery}/P{candidate.Spec.Piloting}/S{candidate.Spec.Sensors}{(candidate.Ready ? "" : " · injured")}{(assignment is null ? "" : $" · {assignment.Spec.Chassis}")}");
            PilotPicker.SetItemTooltip(_pilotIds.Count - 1, $"{candidate.Spec.DisplayName}\n{candidate.Status}\n{assignment?.Spec.DisplayName ?? "Unassigned"}");
        }
        var current = warrior is null ? _pilotIds.FindIndex(id => _roster.Assignment(id) is null) : _pilotIds.IndexOf(warrior.Id);
        PilotPicker.Select(current >= 0 ? current : _pilotIds.Count > 0 ? 0 : -1);
        PilotPicker.Disabled = _pilotIds.Count == 0; UpdateAssignmentAction();
        for (var i = 0; i < RosterButtons.Count; i++) RosterButtons[i].AddThemeStyleboxOverride("normal", Ui.Box(i == index ? new Color("344431") : Ui.Panel, i == index ? Ui.Gold : Ui.Line));
    }

    /// <summary>Movement points converted with the combat movement rule, shown in km/h.</summary>
    public static int KilometresPerHour(int movementPoints) => Mathf.RoundToInt(movementPoints * CombatRules.MovementScale * 3.6f);
    public static string SpeedSummary(MechSpec spec)
    {
        string Speed(string name, int mp) => $"{name} {KilometresPerHour(mp)} km/h ({mp} MP)";
        var lines = new List<string> { Speed("Walk", spec.Walk), Speed("Run", spec.Run) };
        if (spec.Jump > 0) lines.Add(Speed("Jump", spec.Jump));
        return string.Join("\n", lines);
    }
    public static string HeatSummary(MechSpec spec) =>
        $"{spec.HeatSinks} {DefinitionText.HeatSinks(spec.HeatSinkType)} sinks  ·  −{spec.HeatSinks * spec.CoolingPerSink}/turn  ·  alpha {spec.Weapons.Sum(item => item.Heat)}";

    private void ShowStorage()
    {
        RosterUi.Clear(StorageList);
        var items = _roster.Storage.Select(entry => (Entry: entry, Item: StorageCatalog.Describe(entry.Id)!)).ToArray();
        if (items.Length == 0) RosterUi.Text(StorageList, "No spare equipment", 12, Ui.Muted);
        foreach (var group in items.GroupBy(item => item.Item.Group).OrderBy(group => group.Key))
        {
            var heading = RosterUi.Text(StorageList, group.Key.ToString().ToUpperInvariant(), 11, Ui.Gold);
            heading.CustomMinimumSize = new Vector2(0, 22); heading.VerticalAlignment = VerticalAlignment.Bottom;
            foreach (var (entry, item) in group.OrderBy(item => item.Item.Name, StringComparer.Ordinal))
            {
                var line = RosterUi.Text(StorageList, $"{item.Name} ×{entry.Count}", 13);
                line.Name = $"Storage {item.Id}";
                var heat = item.Heat > 0 ? $" · {item.Heat} heat" : "";
                RosterUi.Text(StorageList, $"{item.Tons.ToString("0.#", CultureInfo.InvariantCulture)} t · {item.Slots} {(item.Slots == 1 ? "slot" : "slots")}{heat}", 11, Ui.Muted);
            }
        }
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
        var compact = Compact;
        var leftWidth = compact ? 190 : 280; var rightWidth = compact ? 230 : 310;
        var top = 24f; var bottom = Size.Y - 78; var gap = compact ? 10 : 16;
        Ui.Place(StoragePanel, 24, top, leftWidth, bottom - top - 8);
        Ui.Place(SummaryPanel, Size.X - rightWidth - 24, top, rightWidth, bottom - top - 8);
        var centreX = 24 + leftWidth + gap; var centreWidth = Size.X - 48 - leftWidth - rightWidth - gap * 2;
        Ui.Place(Preview, centreX, top - 8, centreWidth, bottom - top + 8);
        Ui.Place(Board, centreX, top, centreWidth, bottom - top - 8);
        LayoutBoard(centreWidth, bottom - top - 8, compact);
        Ui.Place(_rosterScroll, 24, Size.Y - 70, Size.X - 48 - 230 - 158, 62);
        Ui.Place(BackButton, Size.X - 234 - 158, Size.Y - 66, 150, 54);
        Ui.Place(DeploymentButton, Size.X - 234, Size.Y - 66, 210, 54);
    }

    /// <summary>Places each body-part panel roughly over its part of a front-facing mech: its right side on the viewer's left.</summary>
    private void LayoutBoard(float width, float height, bool compact)
    {
        const float spacing = 6;
        if (!compact)
        {
            var row = Math.Clamp((height - 3 * HangarLocationPanel.HeaderHeight - 2 * spacing - 40) / 24, 11, 16);
            var column = Math.Min(170, (width - spacing * 4) / 5);
            var head = HangarLocationPanel.HeightFor(MechLocation.Head, row);
            var torso = HangarLocationPanel.HeightFor(MechLocation.CenterTorso, row);
            var left = (width - column * 5 - spacing * 4) / 2;
            float X(int index) => left + index * (column + spacing);
            Place(MechLocation.Head, X(2), 0, row, column);
            Place(MechLocation.RightArm, X(0), head + spacing + 40, row, column);
            Place(MechLocation.RightTorso, X(1), head + spacing, row, column);
            Place(MechLocation.CenterTorso, X(2), head + spacing, row, column);
            Place(MechLocation.LeftTorso, X(3), head + spacing, row, column);
            Place(MechLocation.LeftArm, X(4), head + spacing + 40, row, column);
            Place(MechLocation.RightLeg, X(1), head + torso + spacing * 2, row, column);
            Place(MechLocation.LeftLeg, X(3), head + torso + spacing * 2, row, column);
        }
        else
        {
            const float row = 13;
            var column = (width - spacing * 2 - 12) / 3;
            float X(int index) => index * (column + spacing);
            var arm = HangarLocationPanel.HeightFor(MechLocation.RightArm, row);
            var head = HangarLocationPanel.HeightFor(MechLocation.Head, row);
            Place(MechLocation.RightTorso, X(0), 0, row, column);
            Place(MechLocation.RightArm, X(0), arm + spacing, row, column);
            Place(MechLocation.RightLeg, X(0), arm * 2 + spacing * 2, row, column);
            Place(MechLocation.Head, X(1), 0, row, column);
            Place(MechLocation.CenterTorso, X(1), head + spacing, row, column);
            Place(MechLocation.LeftTorso, X(2), 0, row, column);
            Place(MechLocation.LeftArm, X(2), arm + spacing, row, column);
            Place(MechLocation.LeftLeg, X(2), arm * 2 + spacing * 2, row, column);
        }
        var extent = _locations.Values.Aggregate(Vector2.Zero, (end, panel) => end.Max(panel.Position + panel.Size));
        _boardContent.CustomMinimumSize = new Vector2(0, extent.Y);
    }
    private void Place(MechLocation location, float x, float y, float row, float width)
    {
        var panel = _locations[location]; panel.RowHeight = row;
        Ui.Place(panel, x, y, width, HangarLocationPanel.HeightFor(location, row)); panel.QueueRedraw();
    }
}
