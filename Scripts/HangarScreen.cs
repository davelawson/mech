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
    public event Action? ApplyRequested;
    public Button BackButton { get; private set; } = null!;
    public Button WarriorsButton { get; private set; } = null!;
    public Button DeploymentButton { get; private set; } = null!;
    public Button ApplyButton { get; private set; } = null!;
    public Button ResetButton { get; private set; } = null!;
    public OptionButton PilotPicker { get; private set; } = null!;
    public Button AssignButton { get; private set; } = null!;
    public Button UnassignButton { get; private set; } = null!;
    public List<Button> RosterButtons { get; } = [];
    public OwnedMech SelectedMech => _roster.Mechs[_selected];
    /// <summary>The selected mech as shown: its pending loadout, when it has one.</summary>
    public OwnedMech DisplayedMech => Displayed(SelectedMech);
    public bool HasPendingChanges => _pending.Count > 0;
    public int PendingMechCount => _pending.Count;
    public bool SelectedHasPendingChanges => _pending.ContainsKey(SelectedMech.Id);
    /// <summary>The selected mech's whole loadout, applied and pending.</summary>
    public LoadoutPlan SelectedPlan => _pending.GetValueOrDefault(SelectedMech.Id) ?? SelectedMech.Plan;
    public bool SelectedOverTonnage => DisplayedMech.OverTonnage;
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
    public Label TonnageText { get; private set; } = null!;
    /// <summary>"⚠ Over Tonnage" above the blueprint while the selected mech is overweight.</summary>
    public Label OverTonnageText { get; private set; } = null!;
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
    // Pending loadouts by mech id, with the preview mech each one displays.
    private readonly Dictionary<string, LoadoutPlan> _pending = [];
    private readonly Dictionary<string, OwnedMech> _previews = [];
    private int _debugHardpoint;
    private Label? _debugMount;

    public override void _Ready()
    {
        _background = RosterUi.Picture(this, "res://Assets/Roster/hangar-background.png", Vector2.Zero);
        _background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        var tint = new ColorRect { Color = new Color(0, .02f, .04f, .62f), MouseFilter = MouseFilterEnum.Ignore };
        tint.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(tint);
        Preview = new MechPreview { Blueprint = true }; AddChild(Preview);
        if (OS.IsDebugBuild())
        {
            _debugMount = new Label { Position = new(12, 12), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            _debugMount.AddThemeColorOverride("font_color", Ui.Gold); Preview.AddChild(_debugMount);
        }
        Board = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, MouseFilter = MouseFilterEnum.Pass };
        AddChild(Board);
        _boardContent = new Control { MouseFilter = MouseFilterEnum.Pass }; Board.AddChild(_boardContent);
        foreach (var location in Enum.GetValues<MechLocation>())
        {
            var panel = new HangarLocationPanel(location); _boardContent.AddChild(panel); _locations[location] = panel;
            panel.Dropped += (id, mount) => Install(id, location, mount);
        }
        OverTonnageText = new Label { Text = "⚠ Over Tonnage", HorizontalAlignment = HorizontalAlignment.Center, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        OverTonnageText.AddThemeColorOverride("font_color", HangarLocationPanel.Warning); OverTonnageText.AddThemeFontSizeOverride("font_size", 16);
        AddChild(OverTonnageText);
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
        TonnageText = RosterUi.Text(summary, "", 20);
        _mechClass = RosterUi.Text(summary, "", 11, Ui.Muted);
        _mechStatus = RosterUi.Text(summary, "", 13, new Color("a5c4a5"));
        RosterUi.Text(summary, "MOVEMENT", 11, Ui.Gold); SpeedText = RosterUi.Text(summary, "", 13);
        RosterUi.Text(summary, "PROTECTION", 11, Ui.Gold); ProtectionText = RosterUi.Text(summary, "", 13);
        RosterUi.Text(summary, "HEAT", 11, Ui.Gold); HeatText = RosterUi.Text(summary, "", 13);

        DeploymentButton = Ui.Button(this, "Simulate Deployment");
        DeploymentButton.Pressed += () => DeploymentRequested?.Invoke();
        ResetButton = Ui.Button(this, "Reset"); ResetButton.Pressed += ResetSelected;
        ApplyButton = Ui.Button(this, "Apply"); ApplyButton.Pressed += () => { if (SelectedHasPendingChanges && !SelectedOverTonnage) ApplyRequested?.Invoke(); };
        _rosterScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_rosterScroll); _rosterButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _rosterButtons.AddThemeConstantOverride("separation", 8); _rosterScroll.AddChild(_rosterButtons);
        Resized += Layout; Layout(); Hide();
    }
    private PanelContainer Panel()
    { var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color(.045f, .07f, .065f, .9f), Ui.Line)); AddChild(panel); return panel; }
    public void Open(ForceRoster roster)
    {
        if (!ReferenceEquals(_roster, roster)) { _selected = 0; AbandonPending(); }
        _roster = roster; _selected = Math.Clamp(_selected, 0, roster.Mechs.Count - 1); Board.Visible = true; Show();
        RosterUi.Clear(_rosterButtons); RosterButtons.Clear();
        for (var i = 0; i < roster.Mechs.Count; i++)
        {
            var index = i; var mech = roster.Mechs[i];
            var button = Ui.Button(_rosterButtons, "");
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(150, 54);
            button.Pressed += () => Select(index); RosterButtons.Add(button);
        }
        ShowStorage();
        Select(_selected); Layout(); BackButton.CallDeferred(Control.MethodName.GrabFocus);
    }
    private OwnedMech Displayed(OwnedMech mech)
    {
        if (!_pending.TryGetValue(mech.Id, out var plan)) return mech;
        if (!_previews.TryGetValue(mech.Id, out var preview)) _previews[mech.Id] = preview = mech.WithLoadout(plan);
        return preview;
    }
    /// <summary>The roster as the hangar shows it, pending loadouts included, detached from the company.</summary>
    public ForceRoster PendingRoster() => _roster.WithLoadouts(_pending);
    /// <summary>Takes a removable item out of the selected mech's pending loadout; the items below it in its location move up.</summary>
    public bool Remove(string itemId)
    {
        var plan = SelectedPlan;
        if (plan.Installed.FirstOrDefault(item => item.Id == itemId) is { } installed) Change(plan with { Installed = plan.Installed.Remove(installed) });
        else if (Loadout.Removable(SelectedMech.Definition, itemId) && !plan.Removed.Contains(itemId)) Change(plan with { Removed = plan.Removed.Add(itemId) });
        else return false;
        return true;
    }
    /// <summary>Installs a spare from storage in the first empty slots of a location of the selected mech, as a pending change. A
    /// non-weapon dropped on a weapon mount covers it; weapons always take a mount of their own class.</summary>
    public bool Install(string storageId, MechLocation location, HardpointClass? mount = null)
    {
        if (InstallProblem(storageId, location) is not null) return false;
        var plan = SelectedPlan;
        if (storageId.StartsWith("weapon:", StringComparison.Ordinal)) mount = null;
        Change(plan with { Installed = plan.Installed.Add(new(Loadout.NextId(SelectedMech.Installed.Concat(plan.Installed)), storageId, location, mount)) });
        return true;
    }
    /// <summary>Why a spare cannot go in a location of the selected mech as shown, or null when it can.</summary>
    public string? InstallProblem(string storageId, MechLocation location) =>
        StorageCounts().GetValueOrDefault(storageId) <= 0 ? "None in storage" : Loadout.InstallProblem(DisplayedMech, storageId, location);
    private void Change(LoadoutPlan plan)
    {
        var mech = SelectedMech;
        if (plan.SameAs(mech.Plan)) _pending.Remove(mech.Id); else _pending[mech.Id] = plan;
        _previews.Remove(mech.Id); Refresh();
    }
    /// <summary>Storage as the hangar shows it: items taken out in pending loadouts are in it, and items pending installation,
    /// on any mech, are already out of it.</summary>
    public Dictionary<string, int> StorageCounts()
    {
        var counts = _roster.Storage.ToDictionary(entry => entry.Id, entry => entry.Count, StringComparer.Ordinal);
        foreach (var (id, plan) in _pending)
            foreach (var (stored, count) in _roster.Mechs.Single(mech => mech.Id == id).StorageChange(plan)) counts[stored] = counts.GetValueOrDefault(stored) + count;
        return counts;
    }
    /// <summary>A right click over a location panel's slots removes the item there when it is removable. Returns
    /// whether the click landed on a slot, so it does not also go back.</summary>
    public bool RightClick(Vector2 viewportPosition)
    {
        if (_roster is null || !Board.IsVisibleInTree()) return false;
        foreach (var panel in _locations.Values)
        {
            var local = panel.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
            if (!new Rect2(Vector2.Zero, panel.Size).HasPoint(local) || panel.BlockAt(local) is not { } block) continue;
            if (block.ItemId is { } id && Loadout.Removable(block.Kind)) Remove(id);
            return true;
        }
        return false;
    }
    public void ResetSelected()
    {
        if (!_pending.Remove(SelectedMech.Id)) return;
        _previews.Remove(SelectedMech.Id); Refresh();
    }
    /// <summary>Called once the selected mech's pending loadout has been applied to the company.</summary>
    public void CommitSelected()
    {
        _pending.Remove(SelectedMech.Id); _previews.Remove(SelectedMech.Id); Refresh();
    }
    public void AbandonPending() { _pending.Clear(); _previews.Clear(); }
    public void Refresh()
    {
        if (_roster is not null) { ShowStorage(); Select(_selected); }
    }
    /// <summary>Space shows or hides the location panels over the mech preview; Tab cycles the preview
    /// gait still → walking → running.</summary>
    public bool HandleKey(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space or Key.Tab } key) return false;
        if (key.Keycode == Key.Tab) Preview.Gait = (PreviewGait)(((int)Preview.Gait + 1) % 3);
        else Board.Visible = !Board.Visible;
        return true;
    }
    /// <summary>Escape or a right click while dragging a spare cancels the drag and nothing else.</summary>
    public bool CancelDrag(InputEvent input)
    {
        if (!IsVisibleInTree() || !GetViewport().GuiIsDragging() || input is not (InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            or InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })) return false;
        GetViewport().GuiCancelDrag();
        return true;
    }
    /// <summary>Dragging a spare shows the location panels, each marked with whether it can take the spare and why not.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin && IsVisibleInTree() && _roster is not null
            && GetViewport().GuiGetDragData().Obj is string id && StorageCatalog.Describe(id) is not null)
        {
            Board.Visible = true;
            foreach (var (location, panel) in _locations) panel.ShowDrop(id, InstallProblem(id, location));
        }
        else if (what == NotificationDragEnd)
            foreach (var panel in _locations.Values) panel.ShowDrop(null, null);
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild() || !IsVisibleInTree() || _roster is null
            || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F6 or Key.F7 } key) return;
        CycleDebugMount(key.Keycode == Key.F7);
        GetViewport().SetInputAsHandled();
    }
    /// <summary>Debug builds: F6 selects the previewed mech's next hardpoint, F7 cycles that hardpoint's visual
    /// (including empty). The preview alone changes; the owned mech and save are untouched.</summary>
    public void CycleDebugMount(bool visual)
    {
        var hardpoints = MechModel.Hardpoints(SelectedMech.Spec.Id);
        if (hardpoints.Count == 0 || Preview.Model is not { } model) return;
        var showing = _debugMount is { Visible: true };
        if (!visual) _debugHardpoint = showing ? (_debugHardpoint + 1) % hardpoints.Count : 0;
        var hardpoint = hardpoints[_debugHardpoint % hardpoints.Count];
        var current = model.Mounts.Visuals.GetValueOrDefault(hardpoint.Id);
        if (visual)
        {
            var options = hardpoint.Visuals.Append(null).ToList();
            var next = options[(options.IndexOf(current) + 1) % options.Count];
            var visuals = next is null ? model.Mounts.Visuals.Remove(hardpoint.Id) : model.Mounts.Visuals.SetItem(hardpoint.Id, next);
            Preview.ShowMounts(model.Mounts with { Visuals = visuals, Stock = false });
            current = next;
        }
        if (_debugMount is not null)
        {
            _debugMount.Text = $"{hardpoint.Id} ({hardpoint.Location}): {current ?? "empty"}   F6 next mount · F7 next visual";
            _debugMount.Visible = true;
        }
    }
    public void Select(int index)
    {
        if (index != _selected) { _debugHardpoint = 0; if (_debugMount is not null) _debugMount.Visible = false; }
        _selected = index; var mech = DisplayedMech; var spec = mech.Spec; var warrior = _roster.Pilot(mech);
        Preview.Display(mech);
        var layout = CriticalLayout.For(spec); var mounts = MountLayout.For(spec, mech.Installed, layout);
        foreach (var (location, panel) in _locations) panel.Display(mech, layout[location], mounts[location]);
        _mechName.Text = spec.DisplayName;
        TonnageText.Text = TonnageSummary(mech);
        TonnageText.AddThemeColorOverride("font_color", mech.OverTonnage ? HangarLocationPanel.Destroyed : mech.Tons < mech.Definition.Tonnage ? HangarLocationPanel.Damaged : Ui.Text);
        OverTonnageText.Visible = mech.OverTonnage;
        _mechClass.Text = $"{DefinitionText.WeightClass(spec.WeightClass).ToUpperInvariant()}  ·  {spec.Role}\n{spec.Engine}";
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
        var pending = SelectedHasPendingChanges; var applicable = pending && !mech.OverTonnage;
        ResetButton.Disabled = !pending; ApplyButton.Disabled = !applicable;
        ResetButton.FocusMode = pending ? FocusModeEnum.All : FocusModeEnum.None;
        ApplyButton.FocusMode = applicable ? FocusModeEnum.All : FocusModeEnum.None;
        ApplyButton.TooltipText = mech.OverTonnage ? $"Over tonnage by {(mech.Tons - mech.Definition.Tonnage).ToString("0.#", CultureInfo.InvariantCulture)} t" : "";
        if (applicable) ApplyButton.AddThemeStyleboxOverride("normal", Ui.Box(new Color("344431"), Ui.Gold));
        else ApplyButton.RemoveThemeStyleboxOverride("normal");
        for (var i = 0; i < RosterButtons.Count; i++)
        {
            var design = _roster.Mechs[i].Definition;
            RosterButtons[i].Text = $"{design.Chassis}\n{design.Variant} · {design.Tonnage}t{(_pending.ContainsKey(_roster.Mechs[i].Id) ? " •" : "")}";
        }
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
    public static string TonnageSummary(OwnedMech mech) =>
        $"{mech.Tons.ToString("0.#", CultureInfo.InvariantCulture)} / {mech.Definition.Tonnage} t";
    public static string HeatSummary(MechSpec spec) =>
        $"{spec.HeatSinks} {DefinitionText.HeatSinks(spec.HeatSinkType)} sinks  ·  −{spec.HeatSinks * spec.CoolingPerSink}/turn  ·  alpha {spec.Weapons.Sum(item => item.Heat)}";

    private void ShowStorage()
    {
        RosterUi.Clear(StorageList);
        var counts = StorageCounts();
        var items = counts.Select(pair => (Entry: new StorageEntry(pair.Key, pair.Value), Item: StorageCatalog.Describe(pair.Key)!)).ToArray();
        if (items.Length == 0) RosterUi.Text(StorageList, "No spare equipment", 12, Ui.Muted);
        foreach (var group in items.GroupBy(item => item.Item.Group).OrderBy(group => group.Key))
        {
            var heading = RosterUi.Text(StorageList, group.Key.ToString().ToUpperInvariant(), 11, Ui.Gold);
            heading.CustomMinimumSize = new Vector2(0, 22); heading.VerticalAlignment = VerticalAlignment.Bottom;
            foreach (var (entry, item) in group.OrderBy(item => item.Item.Name, StringComparer.Ordinal))
            {
                StorageList.AddChild(new HangarStorageLine(item, entry.Count) { Name = $"Storage {item.Id}" });
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
        Ui.Place(OverTonnageText, 24 + leftWidth + gap, 0, Size.X - 48 - leftWidth - rightWidth - gap * 2, top);
        Ui.Place(StoragePanel, 24, top, leftWidth, bottom - top - 8);
        Ui.Place(SummaryPanel, Size.X - rightWidth - 24, top, rightWidth, bottom - top - 8);
        var centreX = 24 + leftWidth + gap; var centreWidth = Size.X - 48 - leftWidth - rightWidth - gap * 2;
        Ui.Place(Preview, centreX, top - 8, centreWidth, bottom - top + 8);
        Ui.Place(Board, centreX, top, centreWidth, bottom - top - 8);
        LayoutBoard(centreWidth, bottom - top - 8, compact);
        Ui.Place(_rosterScroll, 24, Size.Y - 70, Size.X - 48 - 230 - 158 - 236, 62);
        Ui.Place(ResetButton, Size.X - 392 - 236, Size.Y - 66, 110, 54);
        Ui.Place(ApplyButton, Size.X - 392 - 118, Size.Y - 66, 110, 54);
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
