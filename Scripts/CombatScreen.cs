using Godot;

namespace Mech;

public enum CombatModal { None, OpponentSelection, ForceSelection, Menu, Results }

public partial class CombatScreen : Control
{
    public event Action? Closed;
    public CombatSimulation? Simulation { get; private set; }
    public CombatViewport Battlefield { get; private set; } = null!;
    public CombatModal Modal { get; private set; }
    public CombatPaperdoll Paperdoll { get; private set; } = null!;
    public CombatLogPanel BattleLog { get; private set; } = null!;
    public List<CheckBox> ForceChoices { get; } = [];
    public Button RandomizedEnemyButton { get; private set; } = null!;
    public Button MirrorMatchButton { get; private set; } = null!;
    public SimulationOpponent Opponent { get; private set; }
    public LineEdit TerrainSeedInput { get; private set; } = null!;
    public Button NewTerrainButton { get; private set; } = null!;
    /// <summary>Random, then each biome in <see cref="Biomes.All"/> order.</summary>
    public OptionButton BiomePicker { get; private set; } = null!;
    private Label _biomeClimate = null!;
    public Button LaunchButton { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Button PauseButton { get; private set; } = null!;
    public Button MenuButton { get; private set; } = null!;
    public Button ResumeButton { get; private set; } = null!;
    public Button EndButton { get; private set; } = null!;
    public Button ReturnButton { get; private set; } = null!;
    public Button NormalHeatButton { get; private set; } = null!;
    public Button AggressiveHeatButton { get; private set; } = null!;
    public List<Button> UnitButtons { get; } = [];
    /// <summary>The compact paperdoll on each unit card, in card order.</summary>
    public List<CombatPaperdoll> UnitDolls { get; } = [];
    private float _unitBarHeight;
    public IReadOnlyList<string> Selected => _selected;
    /// <summary>The local player's slot: force selection offers its lance, and its orders go to that lance's mechs.</summary>
    public int Slot { get; set; } = Campaign.LeaderSlot;
    /// <summary>The players' names by slot in a co-op campaign; null in single-player.</summary>
    public IReadOnlyList<string>? PlayerNames { get; set; }
    /// <summary>Where the local player's orders go when the leader's machine steps the battle; null carries them out here.</summary>
    public Action<CombatCommand>? Orders { get; set; }
    /// <summary>A co-op mission: every player deploys from their own lance, and only the leader launches, withdraws and leaves the results.</summary>
    public bool Coop { get; private set; }
    /// <summary>The local player leads the battle: always so outside a co-op mission.</summary>
    public bool Leading { get; private set; } = true;
    /// <summary>A co-op player other than the leader has marked their force ready.</summary>
    public bool ForceReady { get; private set; }
    /// <summary>Every player's force selection, in a co-op mission.</summary>
    public Label DeploymentStatus { get; private set; } = null!;
    /// <summary>A co-op player other than the leader chose their force and readiness.</summary>
    public event Action<string[], bool>? ForceChosen;
    /// <summary>The co-op leader launches with their own force on this terrain seed.</summary>
    public event Action<string[], int>? LaunchRequested;
    /// <summary>The local player marked a point of the battlefield for everyone.</summary>
    public event Action<Vector2>? PingRequested;
    /// <summary>The co-op leader stops waiting for a disconnected player, ending the game.</summary>
    public event Action? StopWaitingRequested;
    /// <summary>Who the co-op game is waiting for, over the battlefield and force selection.</summary>
    public PanelContainer WaitingBanner { get; private set; } = null!;
    public Label WaitingText { get; private set; } = null!;
    public Button StopWaitingButton { get; private set; } = null!;
    private bool _othersReady;
    public bool SettingPriority { get; private set; }
    /// <summary>Jump targeting is armed: the next ground right-click sends the selection on a jump.</summary>
    public bool SettingJump { get; private set; }
    public Button JumpButton { get; private set; } = null!;
    private ForceRoster _roster = null!;
    private MissionOffer? _mission;
    private Func<CombatSimulation, string>? _settle;
    private string? _settlement;
    private Label _title = null!;
    /// <summary>Deployed to the current mission, whose result is settled into the campaign, rather than a simulation.</summary>
    public bool MissionMode => _mission is not null;
    /// <summary>The roster this deployment was opened with: pending hangar loadouts included.</summary>
    public ForceRoster Roster => _roster;
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
    private Label _systems = null!;
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
        BattleLog = new CombatLogPanel(); AddChild(BattleLog);
        BattleLog.CloseRequested += ToggleBattleLog;
        _top = new Control(); AddChild(_top);
        _title = Ui.Label(_top, "SIMULATED DEPLOYMENT", 20, Ui.Gold); _title.Position = new Vector2(20, 9);
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
        _walk.Pressed += () => Send(new MovementCommand([.. _selected], MovementMode.Walk));
        _run.Pressed += () => Send(new MovementCommand([.. _selected], MovementMode.Run));
        JumpButton = Ui.Button(movement, "Jump [J]"); JumpButton.Pressed += ToggleJump;
        var fire = new HBoxContainer(); _settings.AddChild(fire);
        _fire = Ui.Button(fire, "Fire at will"); _hold = Ui.Button(fire, "Hold fire");
        _fire.Pressed += () => Send(new FiringCommand([.. _selected], FiringMode.FireAtWill));
        _hold.Pressed += () => Send(new FiringCommand([.. _selected], FiringMode.HoldFire));
        var heat = new HBoxContainer(); _settings.AddChild(heat);
        NormalHeatButton = Ui.Button(heat, "Normal heat"); AggressiveHeatButton = Ui.Button(heat, "Aggressive heat");
        NormalHeatButton.TooltipText = "Fire the highest expected-damage volley that keeps heat below 20.";
        AggressiveHeatButton.TooltipText = "Allow heat below 30 for a stronger burst, accepting movement and accuracy penalties.";
        NormalHeatButton.Pressed += () => Send(new HeatPolicyCommand([.. _selected], HeatPolicy.Normal));
        AggressiveHeatButton.Pressed += () => Send(new HeatPolicyCommand([.. _selected], HeatPolicy.Aggressive));
        Ui.Button(_settings, "Stop movement / clear order [Z]").Pressed += StopUnits;
        RosterUi.Text(_settings, "Preferred engagement distance (m)", 12, Ui.Muted);
        _distance = new SpinBox { MinValue = CombatRules.MinimumPreferredDistance, MaxValue = CombatRules.MaximumPreferredDistance,
            Step = 15, AllowGreater = false, AllowLesser = false };
        _settings.AddChild(_distance);
        _distance.ValueChanged += value => { if (!_refreshing) Send(new DistanceCommand([.. _selected], (float)value)); };
        _priority = Ui.Button(_settings, "Set priority target…");
        _priority.Pressed += () => { SettingPriority = !SettingPriority; SettingJump = false; Battlefield.GrabFocus(); RefreshDetails(); };
        Ui.Button(_settings, "Clear priority target").Pressed += () => { SettingPriority = false; Send(new PriorityCommand([.. _selected], null)); };
        _weaponsTitle = RosterUi.Text(body, "WEAPONS · PRIMARY SELECTION", 12, Ui.Gold);
        _weapons = new VBoxContainer(); body.AddChild(_weapons);
        _locations = RosterUi.Text(body, "", 12);
        _systems = RosterUi.Text(body, "", 12);
        _unitBar = new HBoxContainer(); _unitBar.AddThemeConstantOverride("separation", 6); AddChild(_unitBar);
        _help = Ui.Label(this, "", 12, Ui.Muted); _help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        WaitingBanner = new PanelContainer { Visible = false }; WaitingBanner.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2));
        var waiting = new HBoxContainer(); waiting.AddThemeConstantOverride("separation", 14); WaitingBanner.AddChild(waiting);
        WaitingText = Ui.Label(waiting, "", 15, Ui.Gold); WaitingText.VerticalAlignment = VerticalAlignment.Center;
        StopWaitingButton = Ui.Button(waiting, "Stop Waiting"); StopWaitingButton.Pressed += () => StopWaitingRequested?.Invoke();
        StopWaitingButton.TooltipText = "End the co-op game. The campaign resumes from the save made when this mission launched.";
        _shade = new Control { MouseFilter = MouseFilterEnum.Stop }; _shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_shade);
        var tint = new ColorRect { Color = new Color(0, 0, 0, .83f), MouseFilter = MouseFilterEnum.Stop };
        tint.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _shade.AddChild(tint);
        _modalPanel = new PanelContainer(); _modalPanel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2)); _shade.AddChild(_modalPanel);
        _modalContent = new VBoxContainer(); _modalContent.AddThemeConstantOverride("separation", 12); _modalPanel.AddChild(_modalContent);
        // The banner stays above force selection and the combat menu.
        AddChild(WaitingBanner);
        Resized += Layout; Layout(); Hide();
    }
    /// <summary>Shows who the game is waiting for, or hides the banner when nobody is missing. Only the leader may stop waiting,
    /// and only during the battle.</summary>
    public void ShowWaiting(IReadOnlyList<string> absent, bool canStop)
    {
        WaitingBanner.Visible = absent.Count > 0;
        WaitingText.Text = absent.Count == 0 ? "" : $"Waiting for {string.Join(" and ", absent)} to reconnect…";
        StopWaitingButton.Visible = canStop;
        PlaceBanner();
    }
    private void PlaceBanner()
    {
        WaitingBanner.Size = Vector2.Zero;
        var banner = WaitingBanner.GetCombinedMinimumSize();
        Ui.Place(WaitingBanner, (Size.X - banner.X) / 2, 72, banner.X, banner.Y);
    }
    private void Layout()
    {
        if (_top is null) return;
        var sidebar = Size.X < 1100 ? 280 : 330;
        Ui.Place(_top, 0, 0, Size.X, 66);
        Ui.Place(PauseButton, Size.X - 360, 14, 160, 38); Ui.Place(MenuButton, Size.X - 190, 14, 174, 38);
        var logWidth = BattleLog.Visible ? Math.Clamp(Size.X * .29f, 290, 420) : 0;
        // Unit cards shrink on short windows so the battlefield keeps its height.
        var unitBar = _unitBarHeight = Math.Clamp(Size.Y * .13f, 78, 124);
        var field = Size.Y - 120 - unitBar;
        Ui.Place(BattleLog, 0, 66, logWidth, Math.Min(field, 12 * 56 + 56));
        Ui.Place(Battlefield, logWidth, 66, Size.X - sidebar - 8 - logWidth, field);
        Ui.Place(_sidebar, Size.X - sidebar, 66, sidebar - 12, field);
        // Shrink the cards first: a container never sizes below its children's current minimum.
        LayoutUnitCards();
        Ui.Place(_unitBar, 16, Size.Y - 46 - unitBar, Size.X - 32, unitBar);
        Ui.Place(_help, 16, Size.Y - 43, Size.X - 32, 40);
        if (WaitingBanner is not null) PlaceBanner();
        var width = Math.Min(760, Size.X - 48); var height = Math.Min(650, Size.Y - 48);
        Ui.Place(_modalPanel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
    public void Open(ForceRoster roster, string selectedMechId)
    {
        Reset(roster, null, null);
        ShowOpponentSelection(selectedMechId);
    }
    /// <summary>Deploys to a mission: force selection straight away, on the mission system's biome. The battle's result is
    /// passed to <paramref name="settle"/> once, which returns the settlement to show.</summary>
    /// <summary>In a co-op mission the leader passes <paramref name="settle"/>; every other player passes null and is shown the
    /// leader's settlement.</summary>
    public void OpenMission(ForceRoster roster, MissionOffer offer, string selectedMechId, Func<CombatSimulation, string>? settle, bool coop = false)
    {
        Reset(roster, offer, settle);
        Coop = coop; Leading = !coop || settle is not null;
        ShowForceSelection(selectedMechId);
    }
    private void Reset(ForceRoster roster, MissionOffer? mission, Func<CombatSimulation, string>? settle)
    {
        _roster = roster; _mission = mission; _settle = settle; _settlement = null;
        WaitingBanner.Hide();
        Coop = false; Leading = true; ForceReady = false; _othersReady = false; Orders = null;
        Simulation = null; _primary = null; _selected.Clear(); SettingPriority = SettingJump = false;
        _title.Text = mission is null ? "SIMULATED DEPLOYMENT" : $"MISSION · {mission.Name.ToUpperInvariant()}";
        BattleLog.Hide(); Layout();
        Show();
    }
    private Biome MissionBiome => StarChart.Instance[_mission!.SystemId].Biome ?? Biome.Valley;
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
    private void ShowOpponentSelection(string selectedMechId)
    {
        BeginModal(CombatModal.OpponentSelection, "Choose your opponent");
        RosterUi.Text(_modalContent, "Choose an opponent, then select your force and terrain. Your campaign will be preserved.", 14);
        RandomizedEnemyButton = Ui.Button(_modalContent, "Randomized enemy");
        RosterUi.Text(_modalContent, "A fresh enemy draft matches your deployed count and weight categories. Your units keep their current condition.", 14, Ui.Muted);
        RandomizedEnemyButton.Pressed += () => { Opponent = SimulationOpponent.Randomized; ShowForceSelection(selectedMechId); };
        MirrorMatchButton = Ui.Button(_modalContent, "Mirror match");
        RosterUi.Text(_modalContent, "Face the same mechs and pilot skills. Both sides start fully repaired and supplied.", 14, Ui.Muted);
        MirrorMatchButton.Pressed += () => { Opponent = SimulationOpponent.Mirror; ShowForceSelection(selectedMechId); };
        BackButton = Ui.Button(ModalActions(), "Back to hangar"); BackButton.Pressed += Exit;
        RandomizedEnemyButton.GrabFocus(); RosterUi.TrapFocus(_shade);
    }
    private void ShowForceSelection(string selectedMechId)
    {
        BeginModal(CombatModal.ForceSelection, _mission?.Name ?? (Opponent == SimulationOpponent.Mirror ? "Mirror match" : "Randomized enemy"));
        if (_mission is not null)
            RosterUi.Text(_modalContent, $"Choose 1–4 BattleMechs. They deploy as they stand, and the battle's damage, losses and result are settled into your campaign.\nOpposition: "
                + string.Join(", ", _mission.Opposition.Select(item => $"{DraftCatalog.Mech(item.MechId).DisplayName} ({item.Rank})")), 14);
        else RosterUi.Text(_modalContent, Opponent == SimulationOpponent.Mirror
            ? "Choose 1–4 BattleMechs. Opposition mirrors your mechs and pilot skills. Both sides start fully repaired and supplied. Your campaign will be preserved."
            : "Choose 1–4 BattleMechs. A fresh enemy draft matches your count and weight categories. Your units keep their current condition. Your campaign will be preserved.", 14);
        RosterUi.Text(_modalContent, "Natural terrain · the same seed and biome recreate a battlefield. Combat rolls vary independently.", 12, Ui.Muted);
        var biomeRow = new HBoxContainer(); _modalContent.AddChild(biomeRow);
        Ui.Label(biomeRow, "Biome", 13, Ui.Gold);
        BiomePicker = new OptionButton { FitToLongestItem = false };
        BiomePicker.AddItem("Random");
        foreach (var biome in Biomes.All) BiomePicker.AddItem(biome.Name);
        biomeRow.AddChild(BiomePicker);
        // A mission is fought on its system's biome.
        if (_mission is not null) { BiomePicker.Select(1 + Biomes.All.ToList().FindIndex(spec => spec.Biome == MissionBiome)); BiomePicker.Disabled = true; }
        _biomeClimate = Ui.Label(biomeRow, "", 12, Ui.Muted);
        _biomeClimate.SizeFlagsHorizontal = SizeFlags.ExpandFill; _biomeClimate.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        BiomePicker.ItemSelected += _ => RefreshForceSelection();
        var seedRow = new HBoxContainer(); _modalContent.AddChild(seedRow);
        Ui.Label(seedRow, "Terrain seed", 13, Ui.Gold);
        TerrainSeedInput = new LineEdit { Text = Random.Shared.Next().ToString(System.Globalization.CultureInfo.InvariantCulture),
            MaxLength = 11, SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "Whole number" };
        seedRow.AddChild(TerrainSeedInput);
        NewTerrainButton = Ui.Button(seedRow, "New terrain");
        NewTerrainButton.Pressed += () => { TerrainSeedInput.Text = Random.Shared.Next().ToString(System.Globalization.CultureInfo.InvariantCulture); RefreshForceSelection(); };
        TerrainSeedInput.TextChanged += _ => RefreshForceSelection();
        // The leader's terrain seed recreates the battlefield on every player's machine.
        seedRow.Visible = Leading;
        var body = RosterUi.Scroll(_modalContent); ForceChoices.Clear(); _forceIds.Clear();
        // The player's own lance deploys; lance pool mechs are listed so their absence is explained.
        var offered = _roster.Mechs.Where(item => item.Lance == Slot || item.Lance is null).ToArray();
        var first = offered.FirstOrDefault(item => item.Id == selectedMechId && CombatSimulation.UnavailableReason(_roster, item) is null)
            ?? offered.FirstOrDefault(item => CombatSimulation.UnavailableReason(_roster, item) is null);
        foreach (var mech in offered)
        {
            var reason = CombatSimulation.UnavailableReason(_roster, mech); var warrior = _roster.Pilot(mech);
            var choice = new CheckBox { Text = $"{mech.Spec.DisplayName} · {mech.Spec.Tonnage}t", Disabled = reason is not null, ButtonPressed = mech == first };
            body.AddChild(choice); ForceChoices.Add(choice); _forceIds.Add(mech.Id);
            RosterUi.Text(body, reason ?? $"{warrior!.Spec.DisplayName} · {RosterUi.SkillText(warrior.Spec)}\n{mech.Status}", 12, reason is null ? Ui.Muted : new Color("e7a185"));
            choice.Toggled += _ => RefreshForceSelection();
        }
        _forceCount = RosterUi.Text(_modalContent, "", 13, Ui.Gold);
        DeploymentStatus = RosterUi.Text(_modalContent, "", 13, Ui.Muted); DeploymentStatus.Visible = Coop;
        var actions = ModalActions(); BackButton = Ui.Button(actions, "Back to hangar"); BackButton.Pressed += Exit;
        // Only the leader closes force selection; it returns every player to the hangar.
        BackButton.Visible = Leading;
        LaunchButton = Ui.Button(actions, _mission is null ? "Launch simulation" : Leading ? "Launch mission" : "Ready"); LaunchButton.Pressed += Launch;
        RefreshForceSelection(); BackButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    private void RefreshForceSelection()
    {
        var count = ForceChoices.Count(item => item.ButtonPressed); _forceCount.Text = $"{count} / 4 selected · Starts paused";
        if (Coop && !Leading) LaunchButton.Text = ForceReady ? "Not Ready" : "Ready";
        _biomeClimate.Text = SelectedBiome is { } chosen ? $"{Biomes.Spec(chosen).ClimateText} · {Biomes.Spec(chosen).Description}"
            : string.Join(" · ", Biomes.All.Select(biome => FormattableString.Invariant($"{biome.Name} {biome.Ambient:0}/{biome.EfficiencyText}")));
        var validSeed = int.TryParse(TerrainSeedInput.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _);
        if (!validSeed) _forceCount.Text += " · Enter a seed from −2147483648 to 2147483647";
        LaunchButton.Disabled = !ForceReady && (count is < 1 or > 4 || !validSeed) || Coop && Leading && !_othersReady;
        LaunchButton.TooltipText = Coop && Leading && !_othersReady ? "Every other player must be ready." : "";
        for (var i = 0; i < ForceChoices.Count; i++)
            ForceChoices[i].Disabled = ForceReady || CombatSimulation.UnavailableReason(_roster, _roster.Mechs.Single(item => item.Id == _forceIds[i])) is not null
                || (count >= ForceRoster.LanceSize && !ForceChoices[i].ButtonPressed);
    }
    private Biome? SelectedBiome => BiomePicker.Selected > 0 ? Biomes.All[BiomePicker.Selected - 1].Biome : null;
    /// <summary>Picks a biome for the next launch; null picks Random.</summary>
    public void ChooseBiome(Biome? biome)
    {
        BiomePicker.Select(biome is { } chosen ? 1 + Biomes.All.ToList().FindIndex(spec => spec.Biome == chosen) : 0);
        RefreshForceSelection();
    }
    private void Launch()
    {
        if (LaunchButton.Disabled) return;
        var force = _forceIds.Where((_, i) => ForceChoices[i].ButtonPressed).ToArray();
        if (Coop && !Leading) { ForceReady = !ForceReady; RefreshForceSelection(); ForceChosen?.Invoke(force, ForceReady); return; }
        var terrainSeed = int.Parse(TerrainSeedInput.Text, System.Globalization.CultureInfo.InvariantCulture);
        if (Coop) { LaunchRequested?.Invoke(force, terrainSeed); return; }
        Begin(_mission is not null ? CombatSimulation.DeployMission(_roster, force, _mission, terrainSeed: terrainSeed)
            : CombatSimulation.Deploy(_roster, force, terrainSeed: terrainSeed, opponent: Opponent,
                biome: SelectedBiome ?? Biomes.All[Random.Shared.Next(Biomes.All.Count)].Biome));
    }
    /// <summary>Shows every player's force selection. The leader may launch once every other player is ready.</summary>
    public void ShowDeployment(IReadOnlyList<DeploymentSeat> seats, bool othersReady)
    {
        if (Modal != CombatModal.ForceSelection || !Coop) return;
        _othersReady = othersReady;
        if (!Leading && seats.FirstOrDefault(seat => seat.Slot == Slot) is { } own) ForceReady = own.Ready;
        DeploymentStatus.Text = string.Join("\n", seats.Select(seat => $"{ForceRoster.LanceName(seat.Slot)} · {PlayerName(seat.Slot)} · "
            + (seat.Slot == Campaign.LeaderSlot ? "launches when everyone is ready" : seat.Ready ? $"Ready · {seat.Mechs.Length} {(seat.Mechs.Length == 1 ? "mech" : "mechs")}" : "Choosing")));
        RefreshForceSelection();
    }
    private string PlayerName(int slot) => PlayerNames?.ElementAtOrDefault(slot) ?? ForceRoster.LanceName(slot);
    /// <summary>The local player's own lance in this battle, which they order.</summary>
    private IEnumerable<CombatUnit> OwnUnits => Simulation!.Units.Where(unit => Simulation.Commands(Slot, unit));
    /// <summary>Shows a battle: one launched here, or a replica of the leader's.</summary>
    public void Begin(CombatSimulation simulation)
    {
        Simulation = simulation;
        Battlefield.Display(Simulation); _shade.Hide(); Modal = CombatModal.None; Battlefield.CommandsEnabled = true;
        BattleLog.Hide(); BattleLog.Display(Simulation); Layout();
        Battlefield.MouseFilter = MouseFilterEnum.Stop;
        RosterUi.Clear(_unitBar); UnitButtons.Clear(); UnitDolls.Clear();
        var number = 0;
        foreach (var unit in OwnUnits)
        {
            number++;
            var button = Ui.Button(_unitBar, $"{number} · {unit.Warrior.Callsign}\n{unit.Spec.DisplayName}");
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            // The doll sits below the names; clicks pass through it to the card.
            var doll = new CombatPaperdoll { Compact = true };
            button.AddChild(doll);
            doll.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            UnitButtons.Add(button); UnitDolls.Add(doll);
            button.Pressed += () => { Battlefield.InspectedEnemy = null; SelectUnits([unit.Id], Input.IsKeyPressed(Key.Shift)); Battlefield.GrabFocus(); };
        }
        LayoutUnitCards();
        SelectUnits([OwnUnits.First().Id], false);
        Battlefield.GrabFocus(); RefreshDetails();
    }
    public void SelectUnits(IReadOnlyList<string> ids, bool additive)
    {
        if (!additive) _selected.Clear();
        foreach (var id in ids)
        {
            // Every player sees the whole company, but selects and orders only their own lance.
            if (Simulation is null || !Simulation.Commands(Slot, Simulation.Unit(id))) continue;
            if (additive && _selected.Contains(id)) _selected.Remove(id); else if (!_selected.Contains(id)) _selected.Add(id);
        }
        Battlefield.Selection = _selected; SettingPriority = SettingJump = false; RefreshDetails();
    }
    public void IssueCommand(Vector2 ground, string? target, MovementMode? mode = null)
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing) return;
        if (SettingPriority)
        {
            if (target is not null && Simulation.Unit(target) is { Side: CombatSide.Enemy, Defeated: false })
            { Order(new PriorityCommand([.. _selected], target)); SettingPriority = false; }
            RefreshDetails(); return;
        }
        if (SettingJump)
        {
            // Mechs are not landing points: a click on one keeps jump targeting armed.
            if (target is null) { Order(new JumpCommand([.. _selected], ground)); SettingJump = false; }
            RefreshDetails(); return;
        }
        // Alt/Ctrl+right-click keeps the chosen mode, exactly as the Walk/Run buttons do.
        if (mode is { } movement) Order(new MovementCommand([.. _selected], movement));
        Order(target is null ? new MoveCommand([.. _selected], ground)
            : Simulation.Unit(target).Side == CombatSide.Friendly ? new FollowCommand([.. _selected], target) : new AttackCommand([.. _selected], target));
        RefreshDetails();
    }
    /// <summary>Sends a command as the local player, then shows its effect.</summary>
    private void Send(CombatCommand command)
    {
        if (Simulation is null) return;
        Order(command);
        RefreshDetails();
    }
    /// <summary>Carries out a command as the local player, or sends it to the leader's battle.</summary>
    private void Order(CombatCommand command)
    {
        if (Orders is { } send) send(command); else Simulation!.Execute(command, Slot);
    }
    /// <summary>Two text lines at the card font, with the margins above and below them.</summary>
    private float UnitCardTextHeight => UnitButtons.Count == 0 ? 0
        : UnitButtons[0].GetThemeFont("font").GetHeight(UnitButtons[0].GetThemeFontSize("font_size")) * 2 + 10;
    /// <summary>The compact paperdoll's height: whatever the card leaves beneath its names.</summary>
    private float UnitDollHeight => Math.Max(16, _unitBarHeight - UnitCardTextHeight - 4);
    private void LayoutUnitCards()
    {
        foreach (var doll in UnitDolls) { doll.OffsetTop = -UnitDollHeight - 4; doll.OffsetBottom = -4; doll.OffsetLeft = 6; doll.OffsetRight = -6; }
        foreach (var button in UnitButtons)
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                button.AddThemeStyleboxOverride(state, CardBox(button.GetThemeStylebox(state)));
    }
    /// <summary>A unit card style with its names clear of the compact paperdoll beneath them.</summary>
    private StyleBox CardBox(StyleBox style)
    {
        var box = (StyleBox)style.Duplicate();
        box.ContentMarginTop = 6; box.ContentMarginBottom = UnitDollHeight + 4;
        return box;
    }
    /// <summary>Why no selected unit can jump now, or null when at least one can.</summary>
    private string? JumpUnavailable()
    {
        var units = _selected.Select(Simulation!.Unit).ToArray();
        return units.Length == 0 ? "No selection" : units.Any(unit => unit.JumpUnavailableReason is null) ? null
            : string.Join(" · ", units.Select(unit => $"{unit.Warrior.Callsign}: {unit.JumpUnavailableReason}").Distinct());
    }
    public void ToggleJump()
    {
        if (Simulation is null) return;
        SettingJump = !SettingJump && JumpUnavailable() is null; SettingPriority = false;
        Battlefield.GrabFocus(); RefreshDetails();
    }
    private void StopUnits() => Send(new HoldCommand([.. _selected]));
    public void TogglePause()
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing) return;
        Send(new PauseCommand(!Simulation.PauseHolds.Contains(Slot)));
    }
    public void OpenMenu()
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing) return;
        _resumeRunning = !Simulation.PauseHolds.Contains(Slot); Order(new PauseCommand(true));
        BeginModal(CombatModal.Menu, "Combat paused");
        RosterUi.Text(_modalContent, $"Terrain seed: {Simulation.Terrain.Seed} · {BiomeText(Simulation)}", 14, Ui.Gold);
        var seedCopy = new LineEdit { Text = Simulation.Terrain.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), Editable = false };
        _modalContent.AddChild(seedCopy);
        RosterUi.Text(_modalContent, _mission is null ? "Resume the simulation or end it and review damage. This practice battle has no campaign consequences."
            : "Resume the battle, or withdraw and fail the mission. Damage and losses so far are kept.", 16);
        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill }; _modalContent.AddChild(spacer);
        var actions = ModalActions(); ResumeButton = Ui.Button(actions, "Resume"); ResumeButton.Pressed += Resume;
        EndButton = Ui.Button(actions, _mission is null ? "End Simulation" : "Withdraw"); EndButton.Pressed += EndSimulation;
        EndButton.Disabled = !Leading; EndButton.TooltipText = Leading ? "" : "Only the leader withdraws from the battle.";
        ResumeButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    public void Resume()
    {
        if (Simulation is null || Modal != CombatModal.Menu) return;
        Order(new PauseCommand(!_resumeRunning)); Modal = CombatModal.None; _shade.Hide(); Battlefield.CommandsEnabled = true;
        Battlefield.MouseFilter = MouseFilterEnum.Stop;
        Battlefield.GrabFocus(); RefreshDetails();
    }
    public void EndSimulation()
    {
        if (!Leading || Simulation is null || Simulation.Replica) return;
        Simulation.End(); ShowResults();
    }
    /// <summary>The leader's settlement of a co-op mission, shown on the results.</summary>
    public void ShowSettlement(string settlement)
    {
        _settlement = settlement;
        if (Modal == CombatModal.Results) ShowResults();
    }
    private void ShowResults()
    {
        if (Simulation is null) return;
        if (_mission is not null && _settle is not null) _settlement ??= _settle(Simulation);
        var title = Simulation.Outcome == CombatOutcome.Ended ? (_mission is null ? "Simulation ended" : "Withdrew · Mission failed")
            : _mission is null ? Simulation.Outcome.ToString() : $"{Simulation.Outcome} · Mission {(Simulation.Outcome == CombatOutcome.Victory ? "succeeded" : "failed")}";
        // A finished battle has nothing left to wait for.
        StopWaitingButton.Hide();
        BeginModal(CombatModal.Results, title);
        RosterUi.Text(_modalContent, $"Combat time {TimeSpan.FromSeconds(Simulation.Elapsed):mm\\:ss}{(_mission is null ? " · Campaign preserved" : "")}", 15, Ui.Gold);
        if (_settlement is not null) RosterUi.Text(_modalContent, _settlement, 14);
        else if (_mission is not null) RosterUi.Text(_modalContent, "Waiting for the leader to settle the mission…", 14, Ui.Muted);
        RosterUi.Text(_modalContent, $"Terrain seed: {Simulation.Terrain.Seed} · {BiomeText(Simulation)}", 14, Ui.Muted);
        var body = RosterUi.Scroll(_modalContent);
        foreach (var side in Enum.GetValues<CombatSide>())
        {
            RosterUi.Text(body, side == CombatSide.Enemy ? "OPPOSITION" : Coop ? "THE COMPANY" : "YOUR FORCE", 16, Ui.Gold);
            foreach (var unit in Simulation.Units.Where(item => item.Side == side))
            {
                RosterUi.Text(body, $"{unit.Spec.DisplayName} · {unit.Warrior.Callsign} · {unit.Status}{(unit.WarriorKilled ? " · MechWarrior killed" : "")}", 14);
                RosterUi.Text(body, $"Armor lost (front + rear): {unit.ArmorLost} · Structure lost: {unit.StructureLost}\nShots: {unit.ShotsFired} · Hits: {unit.Hits} · Damage delivered: {unit.DamageDealt}\n"
                    + string.Join("  ", unit.Locations.Values.Select(loc => $"{DraftCatalog.Abbreviation(loc.Location)} {loc.Armor}/{loc.Structure}")), 12, Ui.Muted);
            }
        }
        ReturnButton = Ui.Button(ModalActions(), Leading ? "Return to hangar" : "Waiting for leader"); ReturnButton.Pressed += Exit;
        ReturnButton.Disabled = !Leading;
        ReturnButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    private void Exit()
    {
        if (!Leading) return;
        Close();
    }
    /// <summary>Leaves the screen; a co-op player other than the leader leaves when the leader does.</summary>
    public void Close() { Battlefield.CancelDrag(); Hide(); Closed?.Invoke(); }
    /// <summary>Shows a player's ping on the battlefield.</summary>
    public void ShowPing(int slot, Vector2 point) => Battlefield.Ping(point, PlayerName(slot), slot == Slot ? Ui.Gold : new Color("7fd4ff"));
    private void ToggleBattleLog()
    {
        if (Simulation is null || Modal != CombatModal.None) return;
        Battlefield.CancelDrag(); BattleLog.Visible = !BattleLog.Visible; Layout(); Battlefield.GrabFocus();
    }
    public bool HandleKey(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return false;
        if (key.Keycode == Key.Escape)
        {
            if (Modal is CombatModal.OpponentSelection or CombatModal.ForceSelection or CombatModal.Results) Exit();
            else if (Modal == CombatModal.Menu) Resume();
            else if (SettingPriority || SettingJump) { SettingPriority = SettingJump = false; RefreshDetails(); }
            else OpenMenu();
            return true;
        }
        if (key.Keycode == Key.Space && Modal == CombatModal.None && Battlefield.HasFocus()) { TogglePause(); return true; }
        if (Simulation is null || Modal != CombatModal.None || key.CtrlPressed || key.AltPressed || key.MetaPressed) return false;
        // Preserve typing in the preferred-distance input; other combat controls share these shortcuts.
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return false;
        var code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (code is >= Key.Key1 and <= Key.Key4)
        {
            var slot = (int)code - (int)Key.Key1;
            var unit = OwnUnits.ElementAtOrDefault(slot);
            if (unit is not null)
            {
                Battlefield.CancelDrag(); Battlefield.InspectedEnemy = null;
                SelectUnits([unit.Id], key.ShiftPressed); Battlefield.GrabFocus();
            }
            return true;
        }
        if (Battlefield.HasFocus())
        {
            Action? order = code switch
            {
                Key.Z => StopUnits,
                Key.X => () => Send(new MovementCommand([.. _selected], MovementMode.Walk)),
                Key.C => () => Send(new MovementCommand([.. _selected], MovementMode.Run)),
                Key.G => () => Send(new FiringCommand([.. _selected], FiringMode.HoldFire)),
                Key.F => () => Send(new FiringCommand([.. _selected], FiringMode.FireAtWill)),
                Key.J => ToggleJump,
                Key.V => () => PingRequested?.Invoke(Battlefield.GroundPoint(Battlefield.GetLocalMousePosition())),
                _ => null
            };
            if (order is not null) { order(); return true; }
        }
        if (code == Key.Quoteleft || key.Keycode == Key.Asciitilde)
        {
            if (key.ShiftPressed || key.Keycode == Key.Asciitilde) ToggleBattleLog();
            else { Battlefield.ResetToSelection(); Battlefield.GrabFocus(); }
            return true;
        }
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
        _status.Text = $"{(Simulation.Paused ? $"PAUSED{PauseHolders} · orders available" : "LIVE")}  |  {TimeSpan.FromSeconds(Simulation.Elapsed):mm\\:ss}  |  Contacts {Simulation.Contacts(CombatSide.Friendly).Values.Count(item => item.Detected)}";
        // The button shows the local player's own toggle; the battle stays paused while any player holds one.
        PauseButton.Text = Simulation.PauseHolds.Contains(Slot) ? "Resume  [Space]" : "Pause  [Space]";
        // Jump targeting lapses once no selected unit can still jump.
        if (SettingJump && JumpUnavailable() is not null) SettingJump = false;
        Battlefield.JumpArmed = SettingJump;
        _help.Text = SettingPriority ? "PRIORITY TARGET: right-click a detected enemy. This changes target preference without ordering pursuit. Escape cancels."
            : SettingJump ? "JUMP: right-click the ground to jump there, within each mech's range circle. Attack and Follow orders resume on landing. Escape cancels."
            : $"Click / drag / 1–4: select · Shift: toggle · Right-click: Move / Attack / Follow (Alt: walk · Ctrl: run) · Z: stop · X/C: walk/run · J: jump · G/F: hold/fire at will · Space: pause{(Coop ? " · V: ping" : "")} · WASD / arrows / middle-drag: pan (Shift: fast) · Wheel: zoom · Right-drag / Q/E: orbit · `: frame selection · ~: battle log · Home: arena";
        var friends = OwnUnits.ToArray();
        for (var i = 0; i < UnitButtons.Count; i++)
        {
            var friend = friends[i]; var button = UnitButtons[i];
            button.Text = $"{i + 1} · {friend.Warrior.Callsign} · {friend.Status}\n{friend.Spec.DisplayName} · H {friend.Heat:0.0}";
            button.TooltipText = $"{friend.Spec.DisplayName} · {friend.Status}\n{HeatReadout(friend)}";
            button.AddThemeStyleboxOverride("normal", CardBox(Ui.Box(_selected.Contains(friend.Id) ? new Color("344431") : Ui.Panel, _selected.Contains(friend.Id) ? Ui.Gold : Ui.Line)));
            UnitDolls[i].Display(friend);
        }
        var primary = _selected.FirstOrDefault(); var unit = primary is null ? null : Simulation.Unit(primary);
        var active = _selected.Select(Simulation.Unit).Where(item => !item.Defeated).ToArray();
        var inspected = Battlefield.InspectedEnemy;
        if (inspected is not null && !Simulation.Detected(CombatSide.Friendly, inspected))
            Battlefield.InspectedEnemy = inspected = null;
        _settings.Visible = active.Length > 0 && inspected is null;
        _weapons.Visible = inspected is null;
        _weaponsTitle.Visible = unit is not null && inspected is null;
        Paperdoll.Display(unit);
        if (_primary != primary)
        {
            _primary = primary; RosterUi.Clear(_weapons); _weaponControls.Clear();
            if (unit is not null)
                foreach (var weapon in unit.Weapons)
                {
                    var toggle = new CheckButton { ButtonPressed = weapon.Enabled };
                    _weapons.AddChild(toggle); _weaponControls.Add((weapon, toggle));
                    var index = _weaponControls.Count - 1;
                    toggle.Toggled += value => Simulation!.Execute(new WeaponCommand(primary!, index, value), Slot);
                }
        }
        if (inspected is not null)
        {
            var enemy = Simulation.Unit(inspected);
            var detailed = Simulation.DetailsVisible(CombatSide.Friendly, inspected);
            _selectionTitle.Text = detailed ? enemy.Spec.DisplayName : enemy.Spec.Chassis;
            _unitInfo.Text = detailed ? $"OPPOSITION · {enemy.Spec.Tonnage}t · {enemy.Status}\n{HeatReadout(enemy)}"
                : "OPPOSITION · Model identified\nClose to half detection range for armour, internals and systems.";
            Paperdoll.Display(detailed ? enemy : null);
            _locations.Text = detailed ? CombatPaperdoll.Readout(enemy) : "";
            _systems.Text = detailed ? SystemsReadout(enemy, enemy: true) : "";
            _targetInfo.Text = "";
            return;
        }
        if (unit is null)
        {
            _selectionTitle.Text = "Select a BattleMech";
            _unitInfo.Text = "Click a friendly unit or its card below. Drag across the field to select a group.";
            _locations.Text = _targetInfo.Text = _systems.Text = "";
            return;
        }
        _selectionTitle.Text = _selected.Count > 1 ? $"{_selected.Count} selected · {unit.Warrior.Callsign}" : $"{unit.Warrior.Callsign} · {unit.Spec.Chassis}";
        var orderTarget = unit.FollowTarget ?? unit.AttackTarget;
        _unitInfo.Text = $"{unit.Spec.DisplayName} · {unit.Status}\n{RosterUi.SkillText(unit.Warrior)}\nOrder: {unit.Order}{(orderTarget is { } targetId ? $" → {Simulation.Unit(targetId).Warrior.Callsign} · {Simulation.Unit(targetId).Spec.Chassis}" : "")}";
        _unitInfo.Text += $"\n{HeatReadout(unit)}";
        if (unit.InterferenceRemaining > 0)
            _unitInfo.Text += FormattableString.Invariant($"\nElectro-interference: +{CombatRules.InterferenceModifier} hit difficulty ({Math.Ceiling(unit.InterferenceRemaining):0}s)");
        if (active.Length > 0)
        {
            _groupInfo.Visible = active.Length > 1;
            _groupInfo.Text = $"{active.Length} units · edits apply to group\n"
                + $"Movement: {(active.Select(item => item.Movement).Distinct().Count() > 1 ? "Mixed" : active[0].Movement)} · Fire: {(active.Select(item => item.Firing).Distinct().Count() > 1 ? "Mixed" : active[0].Firing == FiringMode.FireAtWill ? "At will" : "Hold")}\n"
                + $"Distance: {(active.Select(item => item.PreferredDistance).Distinct().Count() > 1 ? "Mixed" : $"{active[0].PreferredDistance:0} m")}\n"
                + $"Heat policy: {(active.Select(item => item.HeatPolicy).Distinct().Count() > 1 ? "Mixed" : active[0].HeatPolicy.ToString())}";
            _walk.Text = (active.All(item => item.Movement == MovementMode.Walk) ? "● Walk" : "Walk") + " [X]";
            _run.Text = (active.All(item => item.Movement == MovementMode.Run) ? "● Run" : "Run") + " [C]";
            _fire.Text = (active.All(item => item.Firing == FiringMode.FireAtWill) ? "● Fire at will" : "Fire at will") + " [F]";
            _hold.Text = (active.All(item => item.Firing == FiringMode.HoldFire) ? "● Hold fire" : "Hold fire") + " [G]";
            NormalHeatButton.Text = active.All(item => item.HeatPolicy == HeatPolicy.Normal) ? "● Normal heat" : "Normal heat";
            AggressiveHeatButton.Text = active.All(item => item.HeatPolicy == HeatPolicy.Aggressive) ? "● Aggressive heat" : "Aggressive heat";
            _refreshing = true;
            // Leave the user's edit intact while the spin box owns focus.
            if (!_distance.GetLineEdit().HasFocus()) _distance.SetValueNoSignal(active[0].PreferredDistance);
            _distance.TooltipText = active.Select(item => item.PreferredDistance).Distinct().Count() > 1 ? "Mixed distances; editing applies to all selected units that are not defeated." : "Applies to all selected units that are not defeated.";
            _refreshing = false;
        }
        _priority.Text = SettingPriority ? "Choose enemy on battlefield…" : "Set priority target…";
        var jumpBlocked = JumpUnavailable();
        JumpButton.Disabled = jumpBlocked is not null;
        JumpButton.Text = SettingJump ? "● Jump [J]" : "Jump [J]";
        JumpButton.TooltipText = jumpBlocked ?? "Choose a landing point, then right-click the ground.";
        _unitInfo.Text += $"\n{JumpReadout(unit)}";
        var firingTarget = Simulation.SelectAimTarget(unit);
        var target = firingTarget ?? Simulation.Units.Where(item => item.Side != unit.Side && !item.Defeated && Simulation.Detected(unit.Side, item.Id))
            .OrderBy(item => item.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault();
        _targetInfo.Text = $"Priority: {(unit.PriorityTarget is { } priority ? Simulation.Unit(priority).Spec.Chassis : "none")}\n"
            + (target is null ? "No contact · move toward arena center" : $"{(firingTarget is null ? "Aim preview" : "Target")}: {target.Spec.Chassis} · {unit.Position.DistanceTo(target.Position):0} m");
        if (target is not null)
        {
            var clear = Simulation.Sight(unit, target);
            var cover = Simulation.Terrain.VegetationModifier(unit.Position, target.Position);
            var spotter = firingTarget is null ? null : unit.Weapons.Where(weapon => weapon.Enabled && unit.WeaponUsable(weapon))
                .Select(weapon => Simulation.IndirectSpotter(unit, firingTarget, weapon)).FirstOrDefault(item => item is not null);
            _targetInfo.Text += spotter is not null ? $"\nIndirect fire · spotted by {Simulation.SlotLabel(spotter)} · +{CombatRules.IndirectFireModifier}"
                : !clear ? "\nLine of fire blocked by terrain" : cover > 0 ? $"\nVegetation: +{cover} hit difficulty" : "\nClear line of fire";
        }
        _unitInfo.Text += Simulation.Terrain.Contains(TerrainKind.Water, unit.Position) ? "\nWater slows movement" :
            Simulation.Terrain.Contains(TerrainKind.Vegetation, unit.Position) ? "\nVegetation slows movement" : "";
        var rearTarget = Simulation.SelectRearTarget(unit);
        var weaponStatuses = Simulation.WeaponStatuses(unit, firingTarget, rearTarget, target);
        for (var i = 0; i < _weaponControls.Count; i++)
        {
            var (weapon, control) = _weaponControls[i];
            control.Disabled = unit.Defeated || !unit.WeaponUsable(weapon);
            var weaponTarget = weapon.Spec.Rear ? rearTarget : firingTarget;
            var status = weaponStatuses[i];
            control.Text = $"{weapon.Spec.Name}{(weapon.Spec.Rear ? " (rear)" : "")} · {DraftCatalog.Abbreviation(weapon.Spec.Location)}\n"
                + status + (weapon.Spec.AmmoKind is { } ammo ? $" · {unit.AmmoRemaining(ammo)} shots" : "") + (weaponTarget is null ? "" : $" · {Simulation.TerrainHitChance(unit, weaponTarget, weapon):P0} hit")
                + (weapon.Spec.Rear && weaponTarget is not null ? $" · {weaponTarget.Spec.Chassis}" : "");
        }
        _locations.Text = CombatPaperdoll.Readout(unit)
            + "\nGround markers: team color = legs; gold dot = torso.\nPrimary unit's weapons and locations shown; group orders and settings include shut-down selections.";
        _systems.Text = SystemsReadout(unit);

    }
    /// <summary>In co-op, who is holding the battle paused.</summary>
    private string PauseHolders => !Coop || Simulation is null ? "" : $" by {string.Join(", ", Simulation.PauseHolds.Select(PlayerName))}";
    internal static string JumpReadout(CombatUnit unit) => unit.Spec.Jump == 0 ? "Jump: no jump jets"
        : FormattableString.Invariant($"Jump {unit.JumpRange:0} m · ") + (unit.Airborne ? "Airborne"
            : unit.JumpRecharge > 0 ? FormattableString.Invariant($"Recharging {Math.Ceiling(unit.JumpRecharge):0}s")
            : unit.JumpUnavailableReason ?? "Ready");
    internal static string BiomeText(CombatSimulation simulation) => simulation.Terrain.Biome is { } biome ? Biomes.Spec(biome).Name : "Test terrain";
    internal static string HeatReadout(CombatUnit unit) =>
        $"Heat {unit.Heat:0.0} · {unit.HeatBand} · {unit.HeatPolicy}\n"
        + $"Cooling {unit.CoolingRate:0.00} heat/s · {unit.EffectiveHeatSinks}/{unit.Spec.HeatSinks} {DefinitionText.HeatSinks(unit.Spec.HeatSinkType)} sinks"
        + (unit.Climate is { } climate ? $" × {climate.EfficiencyText}"
            + $"\nAmbient {climate.Ambient:0} · {(unit.AmbientHeatRate >= 0 ? "+" : "−")}{Math.Abs(unit.AmbientHeatRate):0.00} heat/s" : "")
        + (unit.Shutdown && !unit.Defeated ? "\nAutomatic restart below 10 heat" : "");

    // Enemy ammunition depletion is never known: inspection shows only physical equipment loss.
    internal static string SystemsReadout(CombatUnit unit, bool enemy = false)
    {
        string State(string id, MechLocation location, bool usable = true) => !unit.Intact(location) ? "Lost"
            : unit.Equipment[id] == ComponentCondition.Destroyed ? "Destroyed"
            : !usable ? $"{unit.Equipment[id]} · Unavailable" : unit.Equipment[id].ToString();
        string Bin(AmmoSpec bin)
        {
            var state = State(bin.Id, bin.Location);
            if (enemy || state is "Lost" or "Destroyed") return state;
            return unit.Shots(bin.Id) > 0 ? $"{state} · {unit.Shots(bin.Id)} shots" : $"{state} · Empty";
        }
        return "SYSTEMS · CONDITION / AVAILABILITY\n" + string.Join("\n",
            unit.Weapons.Select(weapon => $"{DraftCatalog.Abbreviation(weapon.Spec.Location)} · {weapon.Spec.Name}{(weapon.Spec.Rear ? " (rear)" : "")}: {State(weapon.Spec.Id, weapon.Spec.Location, enemy ? unit.WeaponIntact(weapon) : unit.WeaponUsable(weapon))}")
            .Concat(unit.Spec.Components.Select(item => $"{DraftCatalog.Abbreviation(item.Location)} · {item.Name}: {State(item.Id, item.Location)}"))
            .Concat(unit.Spec.Ammo.Select(item => $"{DraftCatalog.Abbreviation(item.Location)} · {item.Name}: {Bin(item)}")));
    }

}
