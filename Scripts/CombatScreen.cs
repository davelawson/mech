using Godot;

namespace Mech;

public enum CombatModal { None, ForceSelection, Menu, Results }

public partial class CombatScreen : Control
{
    public event Action? Closed;
    public CombatSimulation? Simulation { get; private set; }
    public CombatViewport Battlefield { get; private set; } = null!;
    public CombatModal Modal { get; private set; }
    public CombatPaperdoll Paperdoll { get; private set; } = null!;
    public List<CheckBox> ForceChoices { get; } = [];
    public LineEdit TerrainSeedInput { get; private set; } = null!;
    public Button NewTerrainButton { get; private set; } = null!;
    public Button LaunchButton { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Button PauseButton { get; private set; } = null!;
    public Button MenuButton { get; private set; } = null!;
    public Button ResumeButton { get; private set; } = null!;
    public Button EndButton { get; private set; } = null!;
    public Button ReturnButton { get; private set; } = null!;
    public List<Button> UnitButtons { get; } = [];
    public IReadOnlyList<string> Selected => _selected;
    public bool SettingPriority { get; private set; }
    private ForceRoster _roster = null!;
    private readonly List<string> _selected = [];
    private readonly List<string> _forceIds = [];
    private readonly List<(CombatWeapon Weapon, CheckButton Control)> _weaponControls = [];
    private Control _top = null!;
    private PanelContainer _sidebar = null!;
    private HBoxContainer _unitBar = null!;
    private Control _shade = null!;
    private PanelContainer _modalPanel = null!;
    private VBoxContainer _modalContent = null!;
    private Label _status = null!;
    private Label _help = null!;
    private Label _selectionTitle = null!;
    private Label _unitInfo = null!;
    private Label _targetInfo = null!;
    private Label _locations = null!;
    private Label _forceCount = null!;
    private Label _groupInfo = null!;
    private VBoxContainer _settings = null!;
    private VBoxContainer _weapons = null!;
    private Label _weaponsTitle = null!;
    private SpinBox _distance = null!;
    private Button _walk = null!;
    private Button _run = null!;
    private Button _fire = null!;
    private Button _hold = null!;
    private Button _priority = null!;
    private bool _refreshing;
    private bool _resumeRunning;
    private string? _primary;
    private double _uiElapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var background = new ColorRect { Color = new Color("111b21"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        Battlefield = new CombatViewport(); AddChild(Battlefield);
        Battlefield.SelectionRequested += SelectUnits;
        Battlefield.CommandRequested += IssueCommand;
        _top = new Control(); AddChild(_top);
        Ui.Label(_top, "SIMULATED DEPLOYMENT", 20, Ui.Gold).Position = new Vector2(20, 9);
        _status = Ui.Label(_top, "Select your force", 13, Ui.Muted); _status.Position = new Vector2(20, 36);
        PauseButton = Ui.Button(_top, "Resume  [Space]"); PauseButton.Pressed += TogglePause;
        MenuButton = Ui.Button(_top, "Combat menu  [Esc]"); MenuButton.Pressed += OpenMenu;
        _sidebar = new PanelContainer(); _sidebar.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line)); AddChild(_sidebar);
        var body = RosterUi.Scroll(_sidebar);
        _selectionTitle = RosterUi.Text(body, "Select a BattleMech", 19, Ui.Gold);
        Paperdoll = new CombatPaperdoll { Visible = false }; body.AddChild(Paperdoll);
        _unitInfo = RosterUi.Text(body, "", 13);
        _targetInfo = RosterUi.Text(body, "", 12, Ui.Gold);
        _settings = new VBoxContainer(); body.AddChild(_settings);
        _groupInfo = RosterUi.Text(_settings, "", 12, Ui.Muted);
        var movement = new HBoxContainer(); _settings.AddChild(movement);
        _walk = Ui.Button(movement, "Walk"); _run = Ui.Button(movement, "Run");
        _walk.Pressed += () => ChangeUnits(unit => unit.Movement = MovementMode.Walk);
        _run.Pressed += () => ChangeUnits(unit => unit.Movement = MovementMode.Run);
        var fire = new HBoxContainer(); _settings.AddChild(fire);
        _fire = Ui.Button(fire, "Fire at will"); _hold = Ui.Button(fire, "Hold fire");
        _fire.Pressed += () => ChangeUnits(unit => unit.Firing = FiringMode.FireAtWill);
        _hold.Pressed += () => ChangeUnits(unit => unit.Firing = FiringMode.HoldFire);
        Ui.Button(_settings, "Stop movement / clear order").Pressed += () => { Simulation?.Hold(_selected); RefreshDetails(); };
        RosterUi.Text(_settings, "Preferred engagement distance (hexes)", 12, Ui.Muted);
        _distance = new SpinBox { MinValue = 1.5, MaxValue = 21, Step = .5, AllowGreater = false, AllowLesser = false };
        _settings.AddChild(_distance);
        _distance.ValueChanged += value => { if (!_refreshing) ChangeUnits(unit => unit.PreferredDistance = (float)value); };
        _priority = Ui.Button(_settings, "Set priority target…");
        _priority.Pressed += () => { SettingPriority = !SettingPriority; Battlefield.GrabFocus(); RefreshDetails(); };
        Ui.Button(_settings, "Clear priority target").Pressed += () => { Simulation?.SetPriority(_selected, null); SettingPriority = false; RefreshDetails(); };
        _weaponsTitle = RosterUi.Text(body, "WEAPONS · PRIMARY SELECTION", 12, Ui.Gold);
        _weapons = new VBoxContainer(); body.AddChild(_weapons);
        _locations = RosterUi.Text(body, "", 12);
        _unitBar = new HBoxContainer(); _unitBar.AddThemeConstantOverride("separation", 6); AddChild(_unitBar);
        _help = Ui.Label(this, "", 12, Ui.Muted); _help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _shade = new Control { MouseFilter = MouseFilterEnum.Stop }; _shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_shade);
        var tint = new ColorRect { Color = new Color(0, 0, 0, .83f), MouseFilter = MouseFilterEnum.Stop };
        tint.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _shade.AddChild(tint);
        _modalPanel = new PanelContainer(); _modalPanel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2)); _shade.AddChild(_modalPanel);
        _modalContent = new VBoxContainer(); _modalContent.AddThemeConstantOverride("separation", 12); _modalPanel.AddChild(_modalContent);
        Resized += Layout; Layout(); Hide();
    }
    private void Layout()
    {
        if (_top is null) return;
        var sidebar = Size.X < 1100 ? 280 : 330;
        Ui.Place(_top, 0, 0, Size.X, 66);
        Ui.Place(PauseButton, Size.X - 360, 14, 160, 38); Ui.Place(MenuButton, Size.X - 190, 14, 174, 38);
        Ui.Place(Battlefield, 0, 66, Size.X - sidebar - 8, Size.Y - 172);
        Ui.Place(_sidebar, Size.X - sidebar, 66, sidebar - 12, Size.Y - 172);
        Ui.Place(_unitBar, 16, Size.Y - 98, Size.X - 32, 52);
        Ui.Place(_help, 16, Size.Y - 43, Size.X - 32, 40);
        var width = Math.Min(760, Size.X - 48); var height = Math.Min(650, Size.Y - 48);
        Ui.Place(_modalPanel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
    public void Open(ForceRoster roster, string selectedMechId)
    {
        _roster = roster; Simulation = null; _primary = null; _selected.Clear(); SettingPriority = false;
        Show(); ShowForceSelection(selectedMechId);
    }
    private void BeginModal(CombatModal modal, string title)
    {
        Modal = modal; Battlefield.CommandsEnabled = false; Battlefield.CancelDrag();
        Battlefield.MouseFilter = MouseFilterEnum.Ignore;
        RosterUi.Clear(_modalContent); Ui.Label(_modalContent, title, 24, Ui.Gold); _shade.Show();
    }
    private HBoxContainer ModalActions()
    {
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10); _modalContent.AddChild(buttons); return buttons;
    }
    private void ShowForceSelection(string selectedMechId)
    {
        BeginModal(CombatModal.ForceSelection, "Simulate Deployment");
        RosterUi.Text(_modalContent, "Choose 1–4 BattleMechs. Opposition mirrors your force at full condition. Your campaign will be preserved.", 14);
        RosterUi.Text(_modalContent, "Natural terrain · enter a seed to recreate a battlefield. Combat rolls vary independently.", 12, Ui.Muted);
        var seedRow = new HBoxContainer(); _modalContent.AddChild(seedRow);
        Ui.Label(seedRow, "Terrain seed", 13, Ui.Gold);
        TerrainSeedInput = new LineEdit { Text = Random.Shared.Next().ToString(System.Globalization.CultureInfo.InvariantCulture),
            MaxLength = 11, SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "Whole number" };
        seedRow.AddChild(TerrainSeedInput);
        NewTerrainButton = Ui.Button(seedRow, "New terrain");
        NewTerrainButton.Pressed += () => { TerrainSeedInput.Text = Random.Shared.Next().ToString(System.Globalization.CultureInfo.InvariantCulture); RefreshForceSelection(); };
        TerrainSeedInput.TextChanged += _ => RefreshForceSelection();
        var body = RosterUi.Scroll(_modalContent); ForceChoices.Clear(); _forceIds.Clear();
        var first = _roster.Mechs.FirstOrDefault(item => item.Id == selectedMechId && CombatSimulation.UnavailableReason(_roster, item) is null)
            ?? _roster.Mechs.FirstOrDefault(item => CombatSimulation.UnavailableReason(_roster, item) is null);
        foreach (var mech in _roster.Mechs)
        {
            var reason = CombatSimulation.UnavailableReason(_roster, mech); var warrior = _roster.Pilot(mech);
            var choice = new CheckBox { Text = $"{mech.Spec.DisplayName} · {mech.Spec.Tonnage}t", Disabled = reason is not null, ButtonPressed = mech == first };
            body.AddChild(choice); ForceChoices.Add(choice); _forceIds.Add(mech.Id);
            RosterUi.Text(body, reason ?? $"{warrior!.Spec.DisplayName} · {RosterUi.SkillText(warrior.Spec)}\n{mech.Status}", 12, reason is null ? Ui.Muted : new Color("e7a185"));
            choice.Toggled += _ => RefreshForceSelection();
        }
        _forceCount = RosterUi.Text(_modalContent, "", 13, Ui.Gold);
        var actions = ModalActions(); BackButton = Ui.Button(actions, "Back to hangar"); BackButton.Pressed += Exit;
        LaunchButton = Ui.Button(actions, "Launch simulation"); LaunchButton.Pressed += Launch;
        RefreshForceSelection(); BackButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    private void RefreshForceSelection()
    {
        var count = ForceChoices.Count(item => item.ButtonPressed); _forceCount.Text = $"{count} / 4 selected · Starts paused";
        var validSeed = int.TryParse(TerrainSeedInput.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _);
        if (!validSeed) _forceCount.Text += " · Enter a seed from −2147483648 to 2147483647";
        LaunchButton.Disabled = count is < 1 or > 4 || !validSeed;
        for (var i = 0; i < ForceChoices.Count; i++)
            ForceChoices[i].Disabled = CombatSimulation.UnavailableReason(_roster, _roster.Mechs[i]) is not null || (count >= 4 && !ForceChoices[i].ButtonPressed);
    }
    private void Launch()
    {
        if (LaunchButton.Disabled) return;
        Simulation = CombatSimulation.Deploy(_roster, _forceIds.Where((_, i) => ForceChoices[i].ButtonPressed),
            terrainSeed: int.Parse(TerrainSeedInput.Text, System.Globalization.CultureInfo.InvariantCulture));
        Battlefield.Display(Simulation); _shade.Hide(); Modal = CombatModal.None; Battlefield.CommandsEnabled = true;
        Battlefield.MouseFilter = MouseFilterEnum.Stop;
        RosterUi.Clear(_unitBar); UnitButtons.Clear();
        var number = 0;
        foreach (var unit in Simulation.Units.Where(item => item.Side == CombatSide.Friendly))
        {
            number++;
            var button = Ui.Button(_unitBar, $"{number} · {unit.Warrior.Callsign}\n{unit.Spec.DisplayName}");
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; UnitButtons.Add(button);
            button.Pressed += () => { Battlefield.InspectedEnemy = null; SelectUnits([unit.Id], Input.IsKeyPressed(Key.Shift)); Battlefield.GrabFocus(); };
        }
        SelectUnits([Simulation.Units.First(item => item.Side == CombatSide.Friendly).Id], false);
        Battlefield.GrabFocus(); RefreshDetails();
    }
    public void SelectUnits(IReadOnlyList<string> ids, bool additive)
    {
        if (!additive) _selected.Clear();
        foreach (var id in ids)
        {
            if (Simulation?.Unit(id).Side != CombatSide.Friendly) continue;
            if (additive && _selected.Contains(id)) _selected.Remove(id); else if (!_selected.Contains(id)) _selected.Add(id);
        }
        Battlefield.Selection = _selected; SettingPriority = false; RefreshDetails();
    }
    private void IssueCommand(Vector2 ground, string? target)
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing) return;
        if (SettingPriority)
        { if (target is not null) { Simulation.SetPriority(_selected, target); SettingPriority = false; } }
        else if (target is not null) Simulation.Attack(_selected, target);
        else Simulation.Move(_selected, ground);
        RefreshDetails();
    }
    private void ChangeUnits(Action<CombatUnit> change)
    {
        if (Simulation is null) return;
        foreach (var unit in _selected.Select(Simulation.Unit).Where(item => item.Active)) change(unit);
        RefreshDetails();
    }
    public void TogglePause()
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing) return;
        Simulation.Paused = !Simulation.Paused; RefreshDetails();
    }
    public void OpenMenu()
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing) return;
        _resumeRunning = !Simulation.Paused; Simulation.Paused = true;
        BeginModal(CombatModal.Menu, "Combat paused");
        RosterUi.Text(_modalContent, $"Terrain seed: {Simulation.Terrain.Seed}", 14, Ui.Gold);
        var seedCopy = new LineEdit { Text = Simulation.Terrain.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), Editable = false };
        _modalContent.AddChild(seedCopy);
        RosterUi.Text(_modalContent, "Resume the simulation or end it and review damage. This practice battle has no campaign consequences.", 16);
        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill }; _modalContent.AddChild(spacer);
        var actions = ModalActions(); ResumeButton = Ui.Button(actions, "Resume"); ResumeButton.Pressed += Resume;
        EndButton = Ui.Button(actions, "End Simulation"); EndButton.Pressed += EndSimulation;
        ResumeButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    public void Resume()
    {
        if (Simulation is null || Modal != CombatModal.Menu) return;
        Simulation.Paused = !_resumeRunning; Modal = CombatModal.None; _shade.Hide(); Battlefield.CommandsEnabled = true;
        Battlefield.MouseFilter = MouseFilterEnum.Stop;
        Battlefield.GrabFocus(); RefreshDetails();
    }
    public void EndSimulation() { Simulation?.End(); ShowResults(); }
    private void ShowResults()
    {
        if (Simulation is null) return;
        BeginModal(CombatModal.Results, Simulation.Outcome == CombatOutcome.Ended ? "Simulation ended" : Simulation.Outcome.ToString());
        RosterUi.Text(_modalContent, $"Combat time {TimeSpan.FromSeconds(Simulation.Elapsed):mm\\:ss} · Campaign preserved", 15, Ui.Gold);
        RosterUi.Text(_modalContent, $"Terrain seed: {Simulation.Terrain.Seed}", 14, Ui.Muted);
        var body = RosterUi.Scroll(_modalContent);
        foreach (var side in Enum.GetValues<CombatSide>())
        {
            RosterUi.Text(body, side == CombatSide.Friendly ? "YOUR FORCE" : "OPPOSITION", 16, Ui.Gold);
            foreach (var unit in Simulation.Units.Where(item => item.Side == side))
            {
                RosterUi.Text(body, $"{unit.Spec.DisplayName} · {unit.Warrior.Callsign} · {unit.Status}", 14);
                RosterUi.Text(body, $"Front armor lost: {unit.ArmorLost} · Structure lost: {unit.StructureLost}\nShots: {unit.ShotsFired} · Hits: {unit.Hits} · Damage delivered: {unit.DamageDealt}\n"
                    + string.Join("  ", unit.Locations.Values.Select(loc => $"{DraftCatalog.Abbreviation(loc.Location)} {loc.Armor}/{loc.Structure}")), 12, Ui.Muted);
            }
        }
        ReturnButton = Ui.Button(ModalActions(), "Return to hangar"); ReturnButton.Pressed += Exit;
        ReturnButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    private void Exit() { Battlefield.CancelDrag(); Hide(); Closed?.Invoke(); }
    public bool HandleKey(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return false;
        if (key.Keycode == Key.Escape)
        {
            if (Modal == CombatModal.ForceSelection || Modal == CombatModal.Results) Exit();
            else if (Modal == CombatModal.Menu) Resume();
            else if (SettingPriority) { SettingPriority = false; RefreshDetails(); }
            else OpenMenu();
            return true;
        }
        if (key.Keycode == Key.Space && Modal == CombatModal.None && Battlefield.HasFocus()) { TogglePause(); return true; }
        return false;
    }
    public override void _Process(double delta)
    {
        if (!Visible || Simulation is null || Modal is CombatModal.ForceSelection or CombatModal.Results) return;
        if (Modal == CombatModal.None) Simulation.Advance(delta);
        if (Simulation.Outcome != CombatOutcome.Ongoing) { ShowResults(); return; }
        _uiElapsed += delta;
        if (_uiElapsed >= .1) { _uiElapsed = 0; RefreshDetails(); }
    }
    private void RefreshDetails()
    {
        if (Simulation is null) return;
        _status.Text = $"{(Simulation.Paused ? "PAUSED · orders available" : "LIVE")}  |  {TimeSpan.FromSeconds(Simulation.Elapsed):mm\\:ss}  |  Contacts {Simulation.Contacts(CombatSide.Friendly).Values.Count(item => item.Detected)}";
        PauseButton.Text = Simulation.Paused ? "Resume  [Space]" : "Pause  [Space]";
        _help.Text = SettingPriority ? "PRIORITY TARGET: right-click a detected enemy. This changes target preference without ordering pursuit. Escape cancels."
            : "Click / drag: select · Shift: add · Right-click: Move / Attack · Space: pause · WASD / arrows / middle-drag: pan · Wheel: zoom · Right-drag: orbit · Q/E / Shift-middle: rotate · Home: arena";
        var primary = _selected.FirstOrDefault(); var unit = primary is null ? null : Simulation.Unit(primary);
        var active = _selected.Select(Simulation.Unit).Where(item => item.Active).ToArray();
        _settings.Visible = active.Length > 0;
        _weaponsTitle.Visible = unit is not null;
        Paperdoll.Display(unit);
        if (_primary != primary)
        {
            _primary = primary; RosterUi.Clear(_weapons); _weaponControls.Clear();
            if (unit is not null)
                foreach (var weapon in unit.Weapons)
                {
                    var toggle = new CheckButton { ButtonPressed = weapon.Enabled };
                    _weapons.AddChild(toggle); _weaponControls.Add((weapon, toggle));
                    toggle.Toggled += value => weapon.Enabled = value;
                }
        }
        if (unit is null)
        {
            var inspected = Battlefield.InspectedEnemy;
            if (inspected is not null && Simulation.Detected(CombatSide.Friendly, inspected))
            {
                var enemy = Simulation.Unit(inspected);
                _selectionTitle.Text = enemy.Spec.DisplayName;
                _unitInfo.Text = $"OPPOSITION · {enemy.Spec.Tonnage}t · {enemy.Status}";
                Paperdoll.Display(enemy);
                _locations.Text = CombatPaperdoll.Readout(enemy);
            }
            else { _selectionTitle.Text = "Select a BattleMech"; _unitInfo.Text = "Click a friendly unit or its card below. Drag across the field to select a group."; _locations.Text = ""; }
            _targetInfo.Text = ""; return;
        }
        _selectionTitle.Text = _selected.Count > 1 ? $"{_selected.Count} selected · {unit.Warrior.Callsign}" : $"{unit.Warrior.Callsign} · {unit.Spec.Chassis}";
        _unitInfo.Text = $"{unit.Spec.DisplayName} · {unit.Status}\nGunnery {unit.Warrior.Gunnery} / Piloting {unit.Warrior.Piloting}\nOrder: {unit.Order}{(unit.AttackTarget is { } attack ? $" → {Simulation.Unit(attack).Spec.Chassis}" : "")}";
        if (active.Length > 0)
        {
            _groupInfo.Visible = active.Length > 1;
            _groupInfo.Text = $"{active.Length} active units · edits apply to group\n"
                + $"Movement: {(active.Select(item => item.Movement).Distinct().Count() > 1 ? "Mixed" : active[0].Movement)} · Fire: {(active.Select(item => item.Firing).Distinct().Count() > 1 ? "Mixed" : active[0].Firing == FiringMode.FireAtWill ? "At will" : "Hold")}\n"
                + $"Distance: {(active.Select(item => item.PreferredDistance).Distinct().Count() > 1 ? "Mixed" : $"{active[0].PreferredDistance:0.0} hexes")}";
            _walk.Text = active.All(item => item.Movement == MovementMode.Walk) ? "● Walk" : "Walk";
            _run.Text = active.All(item => item.Movement == MovementMode.Run) ? "● Run" : "Run";
            _fire.Text = active.All(item => item.Firing == FiringMode.FireAtWill) ? "● Fire at will" : "Fire at will";
            _hold.Text = active.All(item => item.Firing == FiringMode.HoldFire) ? "● Hold fire" : "Hold fire";
            _refreshing = true;
            // Leave the user's edit intact while the spin box owns focus.
            if (!_distance.GetLineEdit().HasFocus()) _distance.SetValueNoSignal(active[0].PreferredDistance);
            _distance.TooltipText = active.Select(item => item.PreferredDistance).Distinct().Count() > 1 ? "Mixed distances; editing applies to all selected active units." : "Applies to all selected active units.";
            _refreshing = false;
        }
        _priority.Text = SettingPriority ? "Choose enemy on battlefield…" : "Set priority target…";
        var firingTarget = Simulation.SelectTarget(unit);
        var target = firingTarget ?? Simulation.Units.Where(item => item.Side != unit.Side && item.Active && Simulation.Detected(unit.Side, item.Id))
            .OrderBy(item => item.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault();
        _targetInfo.Text = $"Priority: {(unit.PriorityTarget is { } priority ? Simulation.Unit(priority).Spec.Chassis : "none")}\n"
            + (target is null ? "No contact · move toward arena center" : $"{(firingTarget is null ? "Aim preview" : "Target")}: {target.Spec.Chassis} · {unit.Position.DistanceTo(target.Position):0.0} hexes");
        if (target is not null)
        {
            var clear = Simulation.Terrain.ClearSight(unit.Position, target.Position);
            var cover = Simulation.Terrain.VegetationModifier(unit.Position, target.Position);
            _targetInfo.Text += !clear ? "\nLine of fire blocked by terrain" : cover > 0 ? $"\nVegetation: +{cover} hit difficulty" : "\nClear line of fire";
        }
        _unitInfo.Text += Simulation.Terrain.Contains(TerrainKind.Water, unit.Position) ? "\nWater slows movement" :
            Simulation.Terrain.Contains(TerrainKind.Vegetation, unit.Position) ? "\nVegetation slows movement" : "";
        foreach (var (weapon, control) in _weaponControls)
        {
            control.Disabled = !unit.Active || !unit.WeaponUsable(weapon);
            control.Text = $"{weapon.Spec.Name} · {DraftCatalog.Abbreviation(weapon.Spec.Location)}\n"
                + (!unit.WeaponUsable(weapon) ? "Unavailable" : weapon.Remaining > .05 ? $"Ready in {weapon.Remaining:0.0}s" : "Ready")
                + (target is null ? "" : $" · {Simulation.TerrainHitChance(unit, target, weapon):P0} hit");
        }
        _locations.Text = CombatPaperdoll.Readout(unit)
            + "\nRear armor preserved.\nPrimary unit's weapons and locations shown; group orders and settings apply to all active selections.";
        var friends = Simulation.Units.Where(item => item.Side == CombatSide.Friendly).ToArray();
        for (var i = 0; i < UnitButtons.Count; i++)
        {
            var friend = friends[i]; var button = UnitButtons[i];
            button.Text = $"{i + 1} · {friend.Warrior.Callsign} · {friend.Status}\n{friend.Spec.DisplayName}";
            button.AddThemeStyleboxOverride("normal", Ui.Box(_selected.Contains(friend.Id) ? new Color("344431") : Ui.Panel, _selected.Contains(friend.Id) ? Ui.Gold : Ui.Line));
        }
    }
}
