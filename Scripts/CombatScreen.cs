using Godot;

namespace Mech;

public enum CombatModal { None, OpponentSelection, ForceSelection, Menu, MissionDetails, Results }

public partial class CombatScreen : Control
{
    public event Action? Closed;
    public CombatSimulation? Simulation { get; private set; }
    public CombatViewport Battlefield { get; private set; } = null!;
    public CombatModal Modal { get; private set; }
    public CombatPaperdoll Paperdoll { get; private set; } = null!;
    /// <summary>The primary friendly's MechWarrior, laid out as in the hangar: portrait beside callsign, name and skills.</summary>
    public HBoxContainer PilotRow { get; private set; } = null!;
    public TextureRect PilotPortrait { get; private set; } = null!;
    public Label PilotInfo { get; private set; } = null!;
    /// <summary>The sidebar's scrolling area, below the pinned title and paperdoll.</summary>
    public ScrollContainer SidebarScroll { get; private set; } = null!;
    public Button MissionDetailsButton { get; private set; } = null!;
    public Button MissionDetailsBackButton { get; private set; } = null!;
    public CombatLogPanel BattleLog { get; private set; } = null!;
    /// <summary>The controls reference toggled with ?; it never pauses the battle.</summary>
    public CombatControlsPanel ControlsPanel { get; private set; } = null!;
    public List<CheckBox> ForceChoices { get; } = [];
    public Button RandomizedEnemyButton { get; private set; } = null!;
    public Button MirrorMatchButton { get; private set; } = null!;
    public Button PatrolButton { get; private set; } = null!;
    /// <summary>The company's standing, which sets a simulated patrol's opposition as it would an offer's.</summary>
    public Reputation Reputation { get; set; } = new();
    public SimulationOpponent Opponent { get; private set; }
    public LineEdit TerrainSeedInput { get; private set; } = null!;
    public Button NewTerrainButton { get; private set; } = null!;
    /// <summary>Random, then each biome in <see cref="Biomes.All"/> order.</summary>
    public OptionButton BiomePicker { get; private set; } = null!;
    private Label _biomeClimate = null!;
    public Button LaunchButton { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    /// <summary>The time panel's buttons, in cassette-player order.</summary>
    public Button PauseButton { get; private set; } = null!;
    public Button PlayButton { get; private set; } = null!;
    public Button FastForwardButton { get; private set; } = null!;
    /// <summary>The mission's elapsed time, at the top of the time panel.</summary>
    public Label TimeLabel { get; private set; } = null!;
    /// <summary>The highlighted time button, this player's own pick: "Pause" while their toggle holds the battle, otherwise "1x" or "4x".</summary>
    public string TimeMode { get; private set; } = "Pause";
    public Button ResumeButton { get; private set; } = null!;
    public Button EndButton { get; private set; } = null!;
    public Button AudioButton { get; private set; } = null!;
    public Button ReturnButton { get; private set; } = null!;
    public List<Button> UnitButtons { get; } = [];
    /// <summary>The compact paperdoll on each unit card, in card order.</summary>
    public List<CombatPaperdoll> UnitDolls { get; } = [];
    /// <summary>Each friendly card's stress face, tinted with the level's colour; hidden once the mech is defeated.</summary>
    public List<TextureRect> UnitFaces { get; } = [];
    /// <summary>Each card's status glyph in its top left corner; hidden while the mech is active and on its feet.</summary>
    public List<TextureRect> UnitStatusIcons { get; } = [];
    /// <summary>Cards stop growing at this width; the spare width of the bar goes into the gaps between them.</summary>
    public const float MaxUnitCardWidth = 260;
    private const float StressFaceSize = 18;
    private const float UnitCardGap = 6;
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
    /// <summary>The Combat menu's Audio button: open the audio settings over the paused battle.</summary>
    public event Action? AudioRequested;
    /// <summary>Who the co-op game is waiting for, over the battlefield and force selection.</summary>
    public PanelContainer WaitingBanner { get; private set; } = null!;
    public PanelContainer SurrenderBanner { get; private set; } = null!;
    /// <summary>The drop phase's banner: how to choose a drop site, every player's progress, Ready and, for the leader, Force Start.</summary>
    public PanelContainer DropBanner { get; private set; } = null!;
    public Label DropText { get; private set; } = null!;
    public Button DropReadyButton { get; private set; } = null!;
    public Button ForceDropButton { get; private set; } = null!;
    /// <summary>Why the last drop site was turned away, shown on the banner for a moment; null otherwise.</summary>
    public string? DropNotice => _dropNoticeSeconds > 0 ? _dropNotice : null;
    private string? _dropNotice;
    private double _dropNoticeSeconds;
    private int _dropBounced;
    private bool _dropping;
    public Label SurrenderText { get; private set; } = null!;
    public Button AcceptSurrenderButton { get; private set; } = null!;
    public Button RefuseSurrenderButton { get; private set; } = null!;
    public Button EjectButton { get; private set; } = null!;
    public Label WaitingText { get; private set; } = null!;
    public Button StopWaitingButton { get; private set; } = null!;
    private bool _othersReady;
    /// <summary>Jump targeting is armed: the next ground right-click sends the selection on a jump.</summary>
    public bool SettingJump { get; private set; }
    private ForceRoster _roster = null!;
    private MissionOffer? _mission;
    private Func<CombatSimulation, SalvageClaim[], string>? _settle;
    private string? _settlement;
    /// <summary>A won mission's salvage, offered on the results before the leader settles; empty otherwise.</summary>
    public IReadOnlyList<SalvageOption> SalvagePool { get; private set; } = [];
    public Dictionary<SalvageClaim, CheckBox> SalvageChoices { get; } = [];
    public Label SalvageCount { get; private set; } = null!;
    public Button ClaimSalvageButton { get; private set; } = null!;
    public Button TakeNothingButton { get; private set; } = null!;
    /// <summary>Deployed to the current mission, whose result is settled into the campaign, rather than a simulation.</summary>
    public bool MissionMode => _mission is not null;
    /// <summary>The roster this deployment was opened with: pending hangar loadouts included.</summary>
    public ForceRoster Roster => _roster;
    private readonly List<string> _selected = [];
    private readonly List<string> _forceIds = [];
    private readonly List<(CombatWeapon Weapon, CheckButton Control)> _weaponControls = [];
    private PanelContainer _timePanel = null!;
    private readonly Dictionary<Button, TimeControlIcon> _timeIcons = [];
    private PanelContainer _sidebar = null!;
    private HBoxContainer _unitBar = null!;
    private Control _shade = null!;
    private PanelContainer _modalPanel = null!;
    private VBoxContainer _modalContent = null!;
    /// <summary>The overlays' translucent fill, which darkens the battlefield beneath without hiding it.</summary>
    public static readonly Color HudFill = new(0, 0, 0, .55f);
    private static readonly StyleBoxFlat HudBox = Ui.Box(HudFill, new Color(Ui.Line, .8f));
    private static readonly StyleBoxFlat HudHoverBox = Ui.Box(new Color(.1f, .14f, .1f, .65f), Ui.Gold);
    private static readonly StyleBoxFlat HudSelectedBox = Ui.Box(new Color(.2f, .27f, .19f, .6f), Ui.Gold);
    /// <summary>A button drawn as a translucent overlay in every state but focus.</summary>
    private static void HudButton(Button button)
    {
        button.AddThemeStyleboxOverride("normal", HudBox); button.AddThemeStyleboxOverride("disabled", HudBox);
        button.AddThemeStyleboxOverride("hover", HudHoverBox); button.AddThemeStyleboxOverride("pressed", HudSelectedBox);
        button.AddThemeStyleboxOverride("hover_pressed", HudSelectedBox);
    }
    private static readonly Dictionary<string, StyleBoxFlat> HudStates = new() { ["normal"] = HudBox, ["hover"] = HudHoverBox, ["pressed"] = HudSelectedBox };
    /// <summary>One card per enemy contact, in the order they were first detected, at the top left of the battlefield.</summary>
    public List<Button> ContactButtons { get; } = [];
    /// <summary>The enemy each contact card stands for, in card order.</summary>
    public IReadOnlyList<string> ContactIds => _contactIds;
    private readonly List<string> _contactIds = [];
    private readonly List<CombatPaperdoll> _contactDolls = [];
    private readonly List<TextureRect> _contactIcons = [];
    private Control _contactBar = null!;
    public static readonly Vector2 ContactCardSize = new(64, 74);
    private const float ContactCardGap = 4;
    /// <summary>The mission's objective, floating at the top left below the contact cards.</summary>
    public Label ObjectiveLabel { get; private set; } = null!;
    /// <summary>A notice for the local player, such as a refused jump, in red below the time panel; it fades after a few seconds.</summary>
    public PanelContainer NoticeStrip { get; private set; } = null!;
    public Label NoticeLabel { get; private set; } = null!;
    /// <summary>The notice on show, or null once it has faded.</summary>
    public string? Notice => _noticeSeconds > 0 ? _notice : null;
    private string? _notice;
    private double _noticeSeconds;
    public const double NoticeSeconds = 3, NoticeFadeSeconds = .5;
    /// <summary>The details panel shows only while a friendly is selected or an enemy inspected.</summary>
    public PanelContainer Sidebar => _sidebar;
    private Label _selectionTitle = null!;
    private Label _unitInfo = null!;
    private Label _targetInfo = null!;
    private Label _forceCount = null!;
    private Label _groupInfo = null!;
    private VBoxContainer _settings = null!;
    private VBoxContainer _weapons = null!;
    private Label _weaponsTitle = null!;
    /// <summary>The Maintain Range row: the checkbox, the preferred-distance slider and its reading.</summary>
    public CheckBox MaintainRangeToggle { get; private set; } = null!;
    public HSlider DistanceSlider { get; private set; } = null!;
    public Label DistanceValue { get; private set; } = null!;
    private bool _draggingDistance;
    private Button _fire = null!;
    private Button _hold = null!;
    /// <summary>Shows the battlefield's current range overlay; pressing it, or R, moves to the next.</summary>
    public Button RangeOverlayButton { get; private set; } = null!;
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
        // The tactical view has one global focus: its panels never take keyboard focus, so hotkeys always reach the battle.
        BattleLog.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
        BattleLog.CloseRequested += ToggleBattleLog;
        ControlsPanel = new CombatControlsPanel { FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled }; AddChild(ControlsPanel);
        ControlsPanel.CloseRequested += ToggleControls;
        // The time panel, contacts, objective and notices float over the battlefield, which fills the window.
        _timePanel = new PanelContainer { FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled }; AddChild(_timePanel);
        var panelBox = (StyleBoxFlat)HudBox.Duplicate(); panelBox.ContentMarginTop = panelBox.ContentMarginBottom = 3;
        _timePanel.AddThemeStyleboxOverride("panel", panelBox);
        var timeBody = new VBoxContainer(); timeBody.AddThemeConstantOverride("separation", 0); _timePanel.AddChild(timeBody);
        TimeLabel = Ui.Label(timeBody, "00:00", 20); TimeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        var timeButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; timeButtons.AddThemeConstantOverride("separation", 4);
        timeBody.AddChild(timeButtons);
        PauseButton = TimeButton(timeButtons, TimeControlIcon.Glyph.Pause, "Pause  [Space]"); PauseButton.Pressed += TogglePause;
        PlayButton = TimeButton(timeButtons, TimeControlIcon.Glyph.Play, "1x  [,]"); PlayButton.Pressed += () => SetSpeed(1);
        FastForwardButton = TimeButton(timeButtons, TimeControlIcon.Glyph.FastForward, "4x  [.]");
        FastForwardButton.Pressed += () => SetSpeed(CombatSimulation.FastTimeScale);
        _contactBar = new Control { FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled, MouseFilter = MouseFilterEnum.Ignore }; AddChild(_contactBar);
        ObjectiveLabel = Ui.Label(this, "", 14, Ui.Gold); ObjectiveLabel.Visible = false;
        ObjectiveLabel.AddThemeConstantOverride("outline_size", 5); ObjectiveLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .85f));
        NoticeStrip = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore }; NoticeStrip.AddThemeStyleboxOverride("panel", HudBox); AddChild(NoticeStrip);
        NoticeLabel = Ui.Label(NoticeStrip, "", 14, new Color("ec5a4a"));
        _sidebar = new PanelContainer { FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled }; _sidebar.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line)); AddChild(_sidebar);
        // The title and paperdoll stay pinned at the top; only the details below them scroll.
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); _sidebar.AddChild(column);
        _selectionTitle = RosterUi.Text(column, "Select a BattleMech", 19, Ui.Gold);
        _selectionTitle.HorizontalAlignment = HorizontalAlignment.Center;
        Paperdoll = new CombatPaperdoll { Visible = false }; column.AddChild(Paperdoll);
        var body = RosterUi.Scroll(column, margin: 10);
        SidebarScroll = body.GetParent().GetParent<ScrollContainer>();
        PilotRow = new HBoxContainer(); PilotRow.AddThemeConstantOverride("separation", 8); body.AddChild(PilotRow);
        PilotPortrait = RosterUi.Picture(PilotRow, "", new Vector2(72, 72));
        PilotInfo = RosterUi.Text(PilotRow, "", 13); PilotInfo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        // Dragging the heat bar's handle sets the limit for the whole selection, shut-down mechs included.
        Paperdoll.HeatLimitChanged += limit => Send(new HeatLimitCommand([.. _selected], limit));
        // Dragging the speed gauge's pointer sets the same desired speed for the whole selection; X, C, J and Z remain hotkeys only.
        Paperdoll.DesiredSpeedChanged += speed => Send(new SpeedCommand([.. _selected], speed));
        Paperdoll.AmmoEconomyChanged += economy => Send(new AmmoEconomyCommand(
            [.. _selected.Where(id => Simulation?.Unit(id).HasAmmoWeapons == true)], economy));
        _unitInfo = RosterUi.Text(body, "", 13);
        _targetInfo = RosterUi.Text(body, "", 12, Ui.Gold);
        _settings = new VBoxContainer(); body.AddChild(_settings);
        _groupInfo = RosterUi.Text(_settings, "", 12, Ui.Muted);
        var fire = new HBoxContainer(); _settings.AddChild(fire);
        _fire = Fit(Ui.Button(fire, "Fire at will")); _hold = Fit(Ui.Button(fire, "Hold fire"));
        _fire.Pressed += () => Send(new FiringCommand([.. _selected], FiringMode.FireAtWill));
        _hold.Pressed += () => Send(new FiringCommand([.. _selected], FiringMode.HoldFire));
        RangeOverlayButton = Fit(Ui.Button(_settings, ""));
        RangeOverlayButton.Pressed += CycleRangeOverlay;
        var range = new HBoxContainer(); range.AddThemeConstantOverride("separation", 6); _settings.AddChild(range);
        MaintainRangeToggle = new CheckBox(); range.AddChild(MaintainRangeToggle);
        MaintainRangeToggle.Toggled += on => { if (!_refreshing) Send(new MaintainRangeCommand([.. _selected], on)); };
        var rangeLabel = Ui.Label(range, "Maintain Range", 13); rangeLabel.VerticalAlignment = VerticalAlignment.Center;
        // The wheel zooms the battlefield, never the slider.
        DistanceSlider = new HSlider { MinValue = CombatRules.LowestPreferredStep, MaxValue = CombatRules.HighestPreferredStep,
            Step = CombatRules.PreferredDistanceStep, Scrollable = false, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter };
        range.AddChild(DistanceSlider);
        DistanceValue = Ui.Label(range, "", 13); DistanceValue.VerticalAlignment = VerticalAlignment.Center;
        DistanceValue.CustomMinimumSize = new Vector2(48, 0); DistanceValue.HorizontalAlignment = HorizontalAlignment.Right;
        DistanceSlider.DragStarted += () => _draggingDistance = true;
        DistanceSlider.ValueChanged += value => { if (_draggingDistance) DistanceValue.Text = $"{value:0} m"; };
        // One command when the slider is let go, rather than one per step dragged through.
        DistanceSlider.DragEnded += changed =>
        {
            _draggingDistance = false;
            if (changed) Send(new DistanceCommand([.. _selected], (float)DistanceSlider.Value)); else RefreshDetails();
        };
        Fit(Ui.Button(_settings, "Clear priority target")).Pressed += () => Send(new PriorityCommand([.. _selected], null));
        // Ejecting cannot be undone, so the first press only arms the button and a second press within a few seconds ejects.
        EjectButton = Fit(Ui.Button(_settings, "Eject"));
        EjectButton.TooltipText = "The MechWarrior ejects: they survive, injured, and the mech is left disabled. Press twice to confirm.";
        EjectButton.Pressed += PressEject;
        _weaponsTitle = RosterUi.Text(body, "WEAPONS · PRIMARY SELECTION", 12, Ui.Gold);
        _weapons = new VBoxContainer(); body.AddChild(_weapons);
        _unitBar = new HBoxContainer { FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled }; _unitBar.AddThemeConstantOverride("separation", (int)UnitCardGap); AddChild(_unitBar);
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
        // Right-click goes back from Mission Details, as Escape does; the battle's own right-click orders are disabled under a modal.
        void BackOnRightClick(InputEvent input)
        {
            if (Modal == CombatModal.MissionDetails && input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            { CloseMissionDetails(); AcceptEvent(); }
        }
        _shade.GuiInput += BackOnRightClick; _modalPanel.GuiInput += BackOnRightClick;
        SurrenderBanner = new PanelContainer { Visible = false }; SurrenderBanner.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2));
        var surrender = new HBoxContainer(); surrender.AddThemeConstantOverride("separation", 14); SurrenderBanner.AddChild(surrender);
        SurrenderText = Ui.Label(surrender, "", 15, Ui.Gold); SurrenderText.VerticalAlignment = VerticalAlignment.Center;
        AcceptSurrenderButton = Ui.Sound(Ui.Button(surrender, "Accept"), Cue.UiConfirm);
        AcceptSurrenderButton.TooltipText = "Take the pilot hostage, to be ransomed if the mission succeeds. The mech cannot be salvaged.";
        AcceptSurrenderButton.Pressed += () => AnswerSurrender(true);
        RefuseSurrenderButton = Ui.Button(surrender, "Refuse");
        RefuseSurrenderButton.TooltipText = "Refuse: an armed mech fights on, an unarmed one tries to flee the battlefield.";
        RefuseSurrenderButton.Pressed += () => AnswerSurrender(false);
        AddChild(SurrenderBanner);
        DropBanner = new PanelContainer { Visible = false, FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled };
        DropBanner.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2));
        var drop = new HBoxContainer(); drop.AddThemeConstantOverride("separation", 14); DropBanner.AddChild(drop);
        DropText = Ui.Label(drop, "", 14, Ui.Gold); DropText.VerticalAlignment = VerticalAlignment.Center;
        DropReadyButton = Ui.Sound(Ui.Button(drop, "Ready"), Cue.UiConfirm); DropReadyButton.Pressed += ToggleDropReady;
        DropReadyButton.TooltipText = "Confirm your lance's drop site. The battle begins, paused, once every player is ready.";
        ForceDropButton = Ui.Button(drop, "Force Start"); ForceDropButton.Pressed += ForceDrop;
        ForceDropButton.TooltipText = "Begin now: every lance without a drop site drops at a random one in the zone.";
        AddChild(DropBanner);
        Battlefield.DropRequested += ChooseDropSite;
        Battlefield.DropSiteProblem = point => Simulation?.DropSiteProblem(Slot, point);
        Battlefield.LanceLabel = PlayerName;
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
    /// <summary>The oldest waiting surrender: the leader answers it, the others wait. The battle stays paused until it is answered.</summary>
    /// <summary>Shows the drop phase's banner while it lasts.</summary>
    private void RefreshDrop()
    {
        var drop = Simulation?.Drop;
        // Once every lance has landed, the camera moves to the player's own.
        if (_dropping && drop is null) { _dropping = false; Battlefield.ResetToSelection(); }
        DropBanner.Visible = drop is not null && Modal == CombatModal.None;
        Battlefield.DropPlacing = drop is not null && !drop.Site(Slot).Ready;
        if (drop is null) return;
        var own = drop.Site(Slot);
        if (own.Bounced > _dropBounced) { _dropNotice = "That drop site was taken by another lance."; _dropNoticeSeconds = 3; SoundPlayer.For(this)?.Play(Cue.UiError); }
        _dropBounced = own.Bounced;
        var others = drop.Sites.Where(site => site.Slot != Slot).Select(site => $"{PlayerName(site.Slot)}: "
            + (site.Ready ? "Ready" : site.Site is null ? "Choosing" : "Placed"));
        DropText.Text = "DROP PHASE · " + (own.Ready ? "Ready · waiting for the other players" : own.Site is null
                ? "Left-click inside the drop zone to choose your lance's drop site"
                : "Left-click to move your drop site, then press Ready\nYour mechs land somewhere inside it when the drop ends")
            + (drop.Sites.Count > 1 ? "\n" + string.Join("  ·  ", others) : "") + (DropNotice is { } notice ? $"\n{notice}" : "");
        DropReadyButton.Text = own.Ready ? "Not Ready" : "Ready"; DropReadyButton.Disabled = own.Site is null;
        ForceDropButton.Visible = Leading && drop.Sites.Count > 1;
        DropBanner.Size = Vector2.Zero;
        var size = DropBanner.GetCombinedMinimumSize();
        Ui.Place(DropBanner, (Size.X - size.X) / 2, BannerTop + (WaitingBanner.Visible ? WaitingBanner.Size.Y + 6 : 0),
            size.X, size.Y);
    }
    /// <summary>Chooses the local player's drop site, as a left-click on the battlefield does.</summary>
    public void ChooseDropSite(Vector2 point)
    {
        if (Simulation?.Drop is not { } drop || drop.Site(Slot).Ready || Modal != CombatModal.None) return;
        if (Simulation.DropSiteProblem(Slot, point) is { } problem)
        { _dropNotice = $"Cannot drop there: {problem}."; _dropNoticeSeconds = 3; SoundPlayer.For(this)?.Play(Cue.UiError); RefreshDetails(); return; }
        Send(new DropSiteCommand(point));
    }
    public void ToggleDropReady()
    {
        if (Simulation?.Drop is not { } drop || drop.Site(Slot).Site is null) return;
        Send(new DropReadyCommand(!drop.Site(Slot).Ready));
    }
    public void ForceDrop()
    {
        if (Simulation?.Drop is null || !Leading) return;
        Send(new ForceDropCommand());
    }
    private void RefreshSurrender()
    {
        var offer = Simulation is { Outcome: CombatOutcome.Ongoing } battle && battle.SurrenderOffers.Count > 0 ? battle.Unit(battle.SurrenderOffers[0]) : null;
        SurrenderBanner.Visible = offer is not null && Modal == CombatModal.None;
        if (offer is null) return;
        var name = Simulation!.DetailsVisible(CombatSide.Friendly, offer.Id) ? offer.Spec.DisplayName : offer.Spec.Chassis;
        var reason = offer.Unarmed ? "has lost every weapon" : "is badly damaged";
        SurrenderText.Text = $"{Simulation.SlotLabel(offer)} {name} {reason} and offers to surrender."
            + (Leading ? "" : " Waiting for the leader to answer…");
        AcceptSurrenderButton.Visible = RefuseSurrenderButton.Visible = Leading;
        SurrenderBanner.Size = Vector2.Zero;
        var size = SurrenderBanner.GetCombinedMinimumSize();
        Ui.Place(SurrenderBanner, (Size.X - size.X) / 2, BannerTop + (WaitingBanner.Visible ? WaitingBanner.Size.Y + 6 : 0),
            size.X, size.Y);
    }
    private void AnswerSurrender(bool accept)
    {
        if (Simulation is null || Simulation.SurrenderOffers.Count == 0 || !Leading) return;
        Send(new SurrenderAnswerCommand(Simulation.SurrenderOffers[0], accept));
    }
    /// <summary>Arms the Eject button on the first press, and ejects every selected mech still in the fight on a second press
    /// within <see cref="EjectConfirmSeconds"/>.</summary>
    private void PressEject()
    {
        if (Simulation is null) return;
        if (_ejectArmed > 0) { _ejectArmed = 0; Send(new EjectCommand([.. _selected])); return; }
        _ejectArmed = EjectConfirmSeconds; RefreshDetails();
    }
    private const double EjectConfirmSeconds = 4;
    private double _ejectArmed;
    /// <summary>Where banners begin: below the time panel and any notice beneath it.</summary>
    private float BannerTop => _timePanel.Position.Y + _timePanel.Size.Y + 6 + (NoticeStrip.Visible ? NoticeStrip.Size.Y + 6 : 0);
    private void PlaceBanner()
    {
        WaitingBanner.Size = Vector2.Zero;
        var banner = WaitingBanner.GetCombinedMinimumSize();
        Ui.Place(WaitingBanner, (Size.X - banner.X) / 2, BannerTop, banner.X, banner.Y);
    }
    private void Layout()
    {
        if (_timePanel is null) return;
        var sidebar = Size.X < 1100 ? 280 : 330;
        Ui.Place(Battlefield, 0, 0, Size.X, Size.Y);
        _timePanel.Size = Vector2.Zero;
        var timePanel = _timePanel.GetCombinedMinimumSize();
        Ui.Place(_timePanel, Mathf.Round((Size.X - timePanel.X) / 2), 8, timePanel.X, timePanel.Y);
        // Unit cards shrink on short windows so the battlefield keeps its height.
        var unitBar = _unitBarHeight = Math.Clamp(Size.Y * .13f, 78, 124);
        var cardsTop = Size.Y - 8 - unitBar;
        // Contact cards fill rows from the top left, stopping short of the time panel.
        var contactWidth = Math.Max(ContactCardSize.X, _timePanel.Position.X - 16);
        var perRow = Math.Max(1, (int)((contactWidth + ContactCardGap) / (ContactCardSize.X + ContactCardGap)));
        for (var i = 0; i < ContactButtons.Count; i++)
            Ui.Place(ContactButtons[i], i % perRow * (ContactCardSize.X + ContactCardGap), i / perRow * (ContactCardSize.Y + ContactCardGap),
                ContactCardSize.X, ContactCardSize.Y);
        var rows = (ContactButtons.Count + perRow - 1) / perRow;
        var contactHeight = rows == 0 ? 0 : rows * ContactCardSize.Y + (rows - 1) * ContactCardGap;
        Ui.Place(_contactBar, 8, 8, contactWidth, contactHeight);
        var top = 8 + (rows > 0 ? contactHeight + 8 : 0);
        ObjectiveLabel.Size = Vector2.Zero;
        var objective = ObjectiveLabel.GetCombinedMinimumSize();
        Ui.Place(ObjectiveLabel, 12, top, objective.X, objective.Y);
        if (ObjectiveLabel.Visible) top += objective.Y + 8;
        var logWidth = BattleLog.Visible ? Math.Clamp(Size.X * .29f, 290, 420) : 0;
        Ui.Place(BattleLog, 8, top, logWidth, Math.Max(0, Math.Min(cardsTop - 8 - top, 12 * 56 + 56)));
        // The details panel floats over the battlefield's right edge, above the cards.
        Ui.Place(_sidebar, Size.X - sidebar, 8, sidebar - 12, cardsTop - 16);
        // The controls lie beside the details panel, or at the right edge while it is hidden.
        var controls = Math.Clamp(Size.X * .3f, 300, 440);
        Ui.Place(ControlsPanel, (_sidebar.Visible ? Size.X - sidebar - 8 : Size.X - 12) - controls, 8, controls, cardsTop - 16);
        // Shrink the cards first: a container never sizes below its children's current minimum.
        LayoutUnitCards();
        Ui.Place(_unitBar, 16, cardsTop, Size.X - 32, unitBar);
        PlaceNotice();
        if (WaitingBanner is not null) PlaceBanner();
        // The results' tables need nearly the whole screen.
        var results = Modal == CombatModal.Results;
        var width = results ? Mathf.Round(Size.X * .9f) : Math.Min(760, Size.X - 48);
        var height = results ? Mathf.Round(Size.Y * .9f) : Math.Min(650, Size.Y - 48);
        Ui.Place(_modalPanel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
    private void PlaceNotice()
    {
        NoticeStrip.Size = Vector2.Zero;
        var notice = NoticeStrip.GetCombinedMinimumSize();
        Ui.Place(NoticeStrip, Mathf.Round((Size.X - notice.X) / 2), _timePanel.Position.Y + _timePanel.Size.Y + 6, notice.X, notice.Y);
    }
    /// <summary>Shows a notice to the local player in red below the time panel, fading after a few seconds.</summary>
    public void Notify(string text)
    {
        _notice = text; _noticeSeconds = NoticeSeconds; NoticeLabel.Text = text;
        NoticeStrip.Modulate = Colors.White; NoticeStrip.Show(); PlaceNotice();
    }
    private void FadeNotice(double delta)
    {
        if (_noticeSeconds <= 0) return;
        _noticeSeconds = Math.Max(0, _noticeSeconds - delta);
        NoticeStrip.Modulate = new Color(1, 1, 1, (float)Math.Min(1, _noticeSeconds / NoticeFadeSeconds));
        if (_noticeSeconds == 0) NoticeStrip.Hide();
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
    public void OpenMission(ForceRoster roster, MissionOffer offer, string selectedMechId, Func<CombatSimulation, SalvageClaim[], string>? settle, bool coop = false)
    {
        Reset(roster, offer, settle);
        Coop = coop; Leading = !coop || settle is not null;
        ShowForceSelection(selectedMechId);
    }
    private void Reset(ForceRoster roster, MissionOffer? mission, Func<CombatSimulation, SalvageClaim[], string>? settle)
    {
        _roster = roster; _mission = mission; _settle = settle; _settlement = null; SalvagePool = []; SalvageChoices.Clear(); _resultsTab = 0; ExpandedResults.Clear();
        WaitingBanner.Hide();
        Coop = false; Leading = true; ForceReady = false; _othersReady = false; Orders = null;
        Simulation = null; _primary = null; _selected.Clear(); SettingJump = false;
        _noticeSeconds = 0; NoticeStrip.Hide(); ObjectiveLabel.Hide(); ObjectiveLabel.Text = ""; _sidebar.Hide();
        BattleLog.Hide(); ControlsPanel.Hide(); Layout();
        Show();
    }
    private Biome MissionBiome => StarChart.Instance[_mission!.SystemId].Biome ?? Biome.Valley;
    private void BeginModal(CombatModal modal, string title)
    {
        Modal = modal; Battlefield.CommandsEnabled = false; Battlefield.CancelDrag(); ControlsPanel.Hide();
        Battlefield.MouseFilter = MouseFilterEnum.Ignore;
        RosterUi.Clear(_modalContent); Ui.Label(_modalContent, title, 24, Ui.Gold); _shade.Show();
        Layout();
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
        PatrolButton = Ui.Button(_modalContent, "Patrol");
        RosterUi.Text(_modalContent, "Visit each patrol point, then reach the extraction zone, past enemy groups that wake on contact. The opposition is rolled from your reputation, as an offer's would be. Your units keep their current condition.", 14, Ui.Muted);
        PatrolButton.Pressed += () => { Opponent = SimulationOpponent.Patrol; ShowForceSelection(selectedMechId); };
        BackButton = Ui.Sound(Ui.Button(ModalActions(), "Back to hangar"), Cue.UiCancel); BackButton.Pressed += Exit;
        RandomizedEnemyButton.GrabFocus(); RosterUi.TrapFocus(_shade);
    }
    private void ShowForceSelection(string selectedMechId)
    {
        BeginModal(CombatModal.ForceSelection, _mission?.Name ?? OpponentName(Opponent));
        if (_mission is not null)
            RosterUi.Text(_modalContent, $"Choose 1–4 BattleMechs. They deploy as they stand, and the battle's damage, losses and result are settled into your campaign.\n"
                + (_mission.Kind == MissionKind.Patrol ? $"Patrol: {MissionsPanel.PatrolText(_mission)}.\n" : "")
                + $"Opposition ({_mission.OppositionTonnage} t): "
                + string.Join(", ", _mission.Opposition.Select(item => $"{DraftCatalog.Mech(item.MechId).DisplayName} ({item.Rank})")), 14);
        else if (Opponent == SimulationOpponent.Patrol)
            RosterUi.Text(_modalContent, "Choose 1–4 BattleMechs. They keep their current condition. The patrol's points, groups and opposition are rolled at launch from your reputation. Your campaign will be preserved.", 14);
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
        // The player's own lance deploys; reserve mechs are listed so their absence is explained.
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
        var actions = ModalActions(); BackButton = Ui.Sound(Ui.Button(actions, "Back to hangar"), Cue.UiCancel); BackButton.Pressed += Exit;
        // Only the leader closes force selection; it returns every player to the hangar.
        BackButton.Visible = Leading;
        LaunchButton = Ui.Sound(Ui.Button(actions, _mission is null ? "Launch simulation" : Leading ? "Launch mission" : "Ready"), Cue.UiConfirm); LaunchButton.Pressed += Launch;
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
        var biome = SelectedBiome ?? Biomes.All[Random.Shared.Next(Biomes.All.Count)].Biome;
        Begin(_mission is not null ? CombatSimulation.DeployMission(_roster, force, _mission, terrainSeed: terrainSeed, dropPhase: true)
            : Opponent == SimulationOpponent.Patrol ? CombatSimulation.DeployMission(_roster, force,
                MissionGenerator.SimulatedPatrol(new MissionRandom((ulong)Random.Shared.NextInt64()), Reputation), terrainSeed: terrainSeed, biome: biome,
                dropPhase: true)
            : CombatSimulation.Deploy(_roster, force, terrainSeed: terrainSeed, opponent: Opponent, biome: biome, dropPhase: true));
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
        // A battle on screen waits for the leader to answer a surrender; a replica only shows the leader's.
        if (!simulation.Replica) simulation.PromptSurrender = true;
        _ejectArmed = 0; Battlefield.Overlay = RangeOverlay.Sensors;
        Battlefield.Slot = Slot; Battlefield.Display(Simulation); _shade.Hide(); Modal = CombatModal.None; Battlefield.CommandsEnabled = true;
        BattleLog.Hide(); BattleLog.Display(Simulation); Layout();
        Battlefield.MouseFilter = MouseFilterEnum.Stop;
        RosterUi.Clear(_unitBar); UnitButtons.Clear(); UnitDolls.Clear(); UnitFaces.Clear(); UnitStatusIcons.Clear();
        RosterUi.Clear(_contactBar); ContactButtons.Clear(); _contactIds.Clear(); _contactDolls.Clear(); _contactIcons.Clear();
        foreach (var unit in OwnUnits)
        {
            var button = Ui.Button(_unitBar, CardText(unit));
            button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; HudButton(button);
            button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            // The stress face sits in the top right corner, clear of the names; clicks pass through it.
            var face = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(face);
            face.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
            face.OffsetLeft = -6 - StressFaceSize; face.OffsetRight = -6; face.OffsetTop = 6; face.OffsetBottom = 6 + StressFaceSize;
            UnitFaces.Add(face);
            // The status glyph mirrors it in the top left corner.
            var status = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            button.AddChild(status);
            status.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            status.OffsetLeft = 6; status.OffsetRight = 6 + StressFaceSize; status.OffsetTop = 6; status.OffsetBottom = 6 + StressFaceSize;
            UnitStatusIcons.Add(status);
            // The doll sits below the names; clicks pass through it to the card.
            var doll = new CombatPaperdoll { Compact = true };
            button.AddChild(doll);
            doll.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            UnitButtons.Add(button); UnitDolls.Add(doll);
            button.Pressed += () => PressUnitCard(unit.Id, Input.IsKeyPressed(Key.Shift));
        }
        LayoutUnitCards();
        SelectUnits([OwnUnits.First().Id], false);
        _dropBounced = simulation.Drop?.Site(Slot).Bounced ?? 0; _dropNoticeSeconds = 0; _dropping = simulation.Drop is not null;
        Battlefield.GrabFocus(); RefreshDetails();
    }
    /// <summary>A unit card: selects its mech, or, when that mech alone is already selected, carries the camera to it.</summary>
    public void PressUnitCard(string id, bool additive)
    {
        if (Simulation is null) return;
        if (!additive && Battlefield.InspectedEnemy is null && _selected.Count == 1 && _selected[0] == id) { Battlefield.GlideTo(Simulation.Unit(id).Position); return; }
        Battlefield.InspectedEnemy = null; SelectUnits([id], additive);
    }
    /// <summary>A contact card: carries the camera to the enemy, or where it was last seen, and inspects it while it is detected.</summary>
    public void PressContactCard(string id)
    {
        if (Simulation is null || !Simulation.Contacts(CombatSide.Friendly).TryGetValue(id, out var contact)) return;
        Battlefield.GlideTo(contact.Detected ? Simulation.Unit(id).Position : contact.LastKnownPosition);
        if (contact.Detected) Battlefield.InspectedEnemy = id;
        RefreshDetails();
    }
    private void AddContactCard(CombatUnit enemy)
    {
        var card = new Button { FocusMode = FocusModeEnum.None, ClipText = true }; HudButton(card);
        _contactBar.AddChild(card);
        var doll = new CombatPaperdoll { Silhouette = true }; card.AddChild(doll);
        doll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        doll.OffsetLeft = 6; doll.OffsetRight = -6; doll.OffsetTop = 5; doll.OffsetBottom = -20;
        var name = Ui.Label(card, enemy.Spec.Chassis, 11); name.HorizontalAlignment = HorizontalAlignment.Center;
        name.ClipText = true; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide); name.OffsetTop = -19; name.OffsetBottom = -3; name.OffsetLeft = 2; name.OffsetRight = -2;
        var icon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        card.AddChild(icon); icon.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        icon.OffsetLeft = 3; icon.OffsetTop = 3; icon.OffsetRight = 3 + 16; icon.OffsetBottom = 3 + 16;
        var id = enemy.Id; card.Pressed += () => PressContactCard(id);
        ContactButtons.Add(card); _contactIds.Add(id); _contactDolls.Add(doll); _contactIcons.Add(icon);
    }
    /// <summary>Adds a card for each new contact and shows each card's state: dimmed while lost, with its status once defeated.</summary>
    private bool RefreshContacts()
    {
        var contacts = Simulation!.Contacts(CombatSide.Friendly);
        var added = false;
        foreach (var id in contacts.Keys.Where(id => !_contactIds.Contains(id))) { AddContactCard(Simulation.Unit(id)); added = true; }
        for (var i = 0; i < _contactIds.Count; i++)
        {
            var enemy = Simulation.Unit(_contactIds[i]);
            var detected = contacts.TryGetValue(enemy.Id, out var contact) && contact.Detected;
            var detailed = Simulation.DetailsVisible(CombatSide.Friendly, enemy.Id);
            _contactDolls[i].DisplaySilhouette(enemy, detailed);
            var status = enemy.Defeated ? CombatPaperdoll.StatusIcon(enemy) : null;
            _contactIcons[i].Visible = status is not null;
            if (status is { } shown) { _contactIcons[i].Texture = shown.Icon; _contactIcons[i].SelfModulate = shown.Tint; }
            var card = ContactButtons[i];
            card.Modulate = new Color(1, 1, 1, detected ? 1 : .45f);
            var inspected = Battlefield.InspectedEnemy == enemy.Id;
            card.AddThemeStyleboxOverride("normal", inspected ? HudSelectedBox : HudBox);
            card.TooltipText = (detailed ? enemy.Spec.DisplayName : enemy.Spec.Chassis) + (enemy.Defeated ? $" · {enemy.Status}" : "")
                + (detected ? "" : " · Contact lost: last known position");
        }
        return added;
    }
    private string ObjectiveText() => Simulation!.PatrolObjectiveText() is { } patrol ? patrol
        : _mission?.Objective ?? "Defeat the opposing force.";
    public void SelectUnits(IReadOnlyList<string> ids, bool additive)
    {
        if (!additive) _selected.Clear();
        foreach (var id in ids)
        {
            // Every player sees the whole company, but selects and orders only their own lance.
            if (Simulation is null || !Simulation.Commands(Slot, Simulation.Unit(id))) continue;
            if (additive && _selected.Contains(id)) _selected.Remove(id); else if (!_selected.Contains(id)) _selected.Add(id);
        }
        Battlefield.Selection = _selected; SettingJump = false; RefreshDetails();
    }
    public void IssueCommand(Vector2 ground, string? target, MovementMode? mode = null)
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing || Simulation.Drop is not null) return;
        if (SettingJump)
        {
            // Mechs are not landing points: a click on one keeps jump targeting armed.
            if (target is null) { Order(new JumpCommand([.. _selected], ground)); SettingJump = false; }
            RefreshDetails(); return;
        }
        // Alt/Ctrl+right-click keeps the chosen mode, exactly as the Walk/Run buttons do.
        if (mode is { } movement) Order(new MovementCommand([.. _selected], movement));
        // An enemy becomes the priority target; players direct movement only with Move and Follow.
        if (target is not null && Simulation.Unit(target) is { Side: CombatSide.Enemy } enemy)
        { if (!enemy.Defeated) Order(new PriorityCommand([.. _selected], target)); }
        else Order(target is null ? new MoveCommand([.. _selected], ground) : new FollowCommand([.. _selected], target));
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
    /// <summary>A card's single line: the pilot's callsign and the mech's chassis, without its model.</summary>
    private static string CardText(CombatUnit unit) => $"{unit.Warrior.Callsign} · {unit.Spec.Chassis}";
    /// <summary>One text line at the card font, with the margins above and below it; the glyphs beside it are no taller.</summary>
    private float UnitCardTextHeight => UnitButtons.Count == 0 ? 0
        : Math.Max(StressFaceSize, UnitButtons[0].GetThemeFont("font").GetHeight(UnitButtons[0].GetThemeFontSize("font_size"))) + 10;
    /// <summary>The compact paperdoll's height: whatever the card leaves beneath its names.</summary>
    private float UnitDollHeight => Math.Max(16, _unitBarHeight - UnitCardTextHeight - 4);
    private void LayoutUnitCards()
    {
        // Cards share the bar up to their maximum width; the rest becomes even gaps between them, the outer cards flush with its edges.
        var bar = Size.X - 32; var count = UnitButtons.Count;
        if (count > 0)
        {
            var width = Mathf.Floor(Math.Min(MaxUnitCardWidth, (bar - UnitCardGap * (count - 1)) / count));
            foreach (var button in UnitButtons) button.CustomMinimumSize = new Vector2(width, 0);
            _unitBar.AddThemeConstantOverride("separation", count > 1 ? (int)((bar - width * count) / (count - 1)) : 0);
        }
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
        box.ContentMarginLeft = StressFaceSize + 10; box.ContentMarginRight = StressFaceSize + 10;
        return box;
    }
    /// <summary>Why no selected unit can jump now, or null when at least one can.</summary>
    public string? JumpBlockedReason => Simulation is null ? "No battle" : JumpUnavailable();
    private string? JumpUnavailable()
    {
        var units = _selected.Select(Simulation!.Unit).ToArray();
        return units.Length == 0 ? "No selection" : units.Any(unit => unit.JumpUnavailableReason is null) ? null
            : string.Join(" · ", units.Select(unit => $"{unit.Warrior.Callsign}: {unit.JumpUnavailableReason}").Distinct());
    }
    /// <summary>Sensor range → Weapon range → Firing arcs → Sensor range, for the whole battlefield.</summary>
    public void CycleRangeOverlay()
    {
        Battlefield.Overlay = (RangeOverlay)(((int)Battlefield.Overlay + 1) % Enum.GetValues<RangeOverlay>().Length);
        Battlefield.QueueRedraw(); RefreshDetails();
    }
    public static string RangeOverlayLabel(RangeOverlay overlay) => overlay switch
    {
        RangeOverlay.WeaponRanges => "Weapon range", RangeOverlay.FiringArcs => "Firing arcs", _ => "Sensor range"
    } + " [R]";
    public void ToggleJump()
    {
        if (Simulation is null || Simulation.Drop is not null) return;
        var blocked = SettingJump ? null : JumpUnavailable();
        SettingJump = !SettingJump && blocked is null;
        if (blocked is not null) RefuseJump(blocked);
        RefreshDetails();
    }
    /// <summary>Why the last jump was refused or lapsed, shown as a notice for a moment; null otherwise.</summary>
    public string? JumpNotice => Notice is { } notice && notice.StartsWith("Cannot jump", StringComparison.Ordinal) ? notice : null;
    private void RefuseJump(string reason)
    {
        Notify($"Cannot jump: {reason}");
        SoundPlayer.For(this)?.Play(Cue.UiError);
    }
    private void StopUnits() => Send(new HoldCommand([.. _selected]));
    public void TogglePause()
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing || Simulation.Drop is not null) return;
        Send(new PauseCommand(!Simulation.PauseHolds.Contains(Slot)));
    }
    /// <summary>Play and Fast-forward: this player picks <paramref name="scale"/> and releases their pause. In co-op the battle
    /// runs at the slowest pick.</summary>
    public void SetSpeed(double scale)
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing || Simulation.Drop is not null) return;
        Order(new TimeScaleCommand(scale));
        if (Simulation.PauseHolds.Contains(Slot)) Order(new PauseCommand(false));
        RefreshDetails();
    }
    private Button TimeButton(Node parent, TimeControlIcon.Glyph glyph, string tooltip)
    {
        var button = Ui.Button(parent, ""); button.CustomMinimumSize = new Vector2(40, 28); button.TooltipText = tooltip; HudButton(button);
        var icon = new TimeControlIcon(glyph); button.AddChild(icon); _timeIcons[button] = icon;
        return button;
    }
    private void RefreshTimePanel()
    {
        var elapsed = TimeSpan.FromSeconds(Simulation!.Elapsed);
        TimeLabel.Text = elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"mm\:ss");
        TimeMode = Pick(Slot);
        var flashOn = _flashClock % (2 * FlashHalfSeconds) < FlashHalfSeconds;
        PaintTimeButton(PauseButton, "Pause", "Pause  [Space]", PauseFlashing && flashOn);
        PaintTimeButton(PlayButton, "1x", "1x  [,]", false);
        PaintTimeButton(FastForwardButton, "4x", "4x  [.]", FastForwardFlashing && flashOn);
    }
    /// <summary>A player slot's pick: "Pause" while their toggle is on, otherwise the speed underneath.</summary>
    private string Pick(int slot) => Simulation!.PauseHolds.Contains(slot) ? "Pause" : Simulation.SpeedPick(slot) == CombatSimulation.FastTimeScale ? "4x" : "1x";
    private void PaintTimeButton(Button button, string mode, string tooltip, bool yellow)
    {
        _timeIcons[button].Color = yellow ? Ui.Background : TimeMode == mode ? Ui.Gold : Ui.Muted;
        foreach (var state in FlashStates)
            if (yellow) button.AddThemeStyleboxOverride(state, _flashBox);
            else if (HudStates.TryGetValue(state, out var hud)) button.AddThemeStyleboxOverride(state, hud); else button.RemoveThemeStyleboxOverride(state);
        var others = Coop ? Simulation!.Players.Where(slot => slot != Slot && Pick(slot) == mode).Select(PlayerName).ToArray() : [];
        button.TooltipText = tooltip + (others.Length > 0 ? $"\nSelected by {string.Join(", ", others)}" : "");
    }
    /// <summary>Whether the pause button is flashing: the battle stands paused, outside the drop phase, the outcome and modals.</summary>
    public bool PauseFlashing => FlashesTime && Simulation!.Paused;
    /// <summary>Whether the fast-forward button is flashing: the battle runs at 1x while at least one player has picked 4x.</summary>
    public bool FastForwardFlashing => FlashesTime && !Simulation!.Paused && Simulation.TimeScale == 1 && Simulation.FastPicks.Count > 0;
    private bool FlashesTime => Simulation is { Outcome: CombatOutcome.Ongoing, Drop: null } && Modal == CombatModal.None;
    /// <summary>Seconds each half of a time button's flash lasts.</summary>
    public const double FlashHalfSeconds = .4;
    public static readonly Color FlashYellow = new("ffe000");
    private static readonly string[] FlashStates = ["normal", "hover", "pressed", "focus"];
    private readonly StyleBoxFlat _flashBox = Ui.Box(FlashYellow, FlashYellow);
    private double _flashClock;
    public void OpenMenu()
    {
        if (Simulation is null || Modal != CombatModal.None || Simulation.Outcome != CombatOutcome.Ongoing) return;
        _resumeRunning = !Simulation.PauseHolds.Contains(Slot); Order(new PauseCommand(true));
        ShowMenu();
    }
    /// <summary>The Combat menu itself; returning from Mission Details reopens it without touching the pause.</summary>
    private void ShowMenu()
    {
        BeginModal(CombatModal.Menu, "Combat paused");
        RosterUi.Text(_modalContent, _mission is null ? "Resume the simulation or end it and review damage. This practice battle has no campaign consequences."
            : "Resume the battle, or withdraw and fail the mission. Damage and losses so far are kept.", 16);
        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill }; _modalContent.AddChild(spacer);
        var actions = ModalActions(); ResumeButton = Ui.Button(actions, "Resume"); ResumeButton.Pressed += Resume;
        MissionDetailsButton = Ui.Button(actions, "Mission Details"); MissionDetailsButton.Pressed += OpenMissionDetails;
        AudioButton = Ui.Button(actions, "Audio"); AudioButton.Pressed += () => AudioRequested?.Invoke();
        EndButton = Ui.Button(actions, _mission is null ? "End Simulation" : "Withdraw"); EndButton.Pressed += EndSimulation;
        EndButton.Disabled = !Leading; EndButton.TooltipText = Leading ? "" : "Only the leader withdraws from the battle.";
        ResumeButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    /// <summary>The mission's briefing and terms, or the practice battle's opponent, and the battlefield, over the paused battle.</summary>
    public void OpenMissionDetails()
    {
        if (Simulation is null || Modal != CombatModal.Menu) return;
        BeginModal(CombatModal.MissionDetails, _mission?.Name ?? $"Practice battle · {OpponentName(Opponent)}");
        var body = RosterUi.Scroll(_modalContent);
        RosterUi.Text(body, "MISSION", 13, Ui.Gold);
        RosterUi.Text(body, MissionText(), 14);
        RosterUi.Text(body, "BATTLEFIELD", 13, Ui.Gold);
        RosterUi.Text(body, BattlefieldText(Simulation), 14);
        var seedRow = new HBoxContainer(); seedRow.AddThemeConstantOverride("separation", 8); body.AddChild(seedRow);
        var seedLabel = Ui.Label(seedRow, "Terrain seed", 14, Ui.Gold); seedLabel.VerticalAlignment = VerticalAlignment.Center;
        // Read-only, so the seed can be copied to recreate this battlefield.
        var seedCopy = new LineEdit { Text = Simulation.Terrain.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        seedRow.AddChild(seedCopy);
        var actions = ModalActions();
        MissionDetailsBackButton = Ui.Sound(Ui.Button(actions, "Back"), Cue.UiCancel); MissionDetailsBackButton.Pressed += CloseMissionDetails;
        MissionDetailsBackButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    public void CloseMissionDetails()
    {
        if (Modal != CombatModal.MissionDetails) return;
        ShowMenu();
    }
    private static string OpponentName(SimulationOpponent opponent) => opponent switch
    {
        SimulationOpponent.Mirror => "Mirror match", SimulationOpponent.Randomized => "Randomized enemy", _ => "Patrol",
    };
    private string MissionText()
    {
        if (_mission is { } offer)
            return $"{offer.Briefing}\n\nOBJECTIVE\n{offer.Objective}\n\n{MissionsPanel.ContractTerms(offer)}"
                + $"\nOPPOSITION  {offer.OppositionTonnage} t\nEXPECTED OPPOSITION\n{MissionsPanel.OppositionLines(offer)}";
        return Opponent switch
        {
            SimulationOpponent.Mirror => "A simulated battle against a mirror of your force and pilot skills.",
            SimulationOpponent.Randomized => "A simulated battle against a fresh enemy draft matching your count and weight categories.",
            _ => "A simulated patrol, rolled from your reputation.",
        } + " It has no campaign consequences.";
    }
    internal static string BattlefieldText(CombatSimulation simulation)
    {
        var terrain = simulation.Terrain;
        var lines = new List<string>();
        if (terrain.Biome is { } biome)
        {
            var spec = Biomes.Spec(biome);
            lines.Add($"BIOME  {spec.Name} · {spec.Description}");
            lines.Add($"CLIMATE  {spec.ClimateText}");
        }
        else lines.Add("BIOME  Test terrain");
        string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
        var features = new List<string> { Count(terrain.Hills.Count, "hill", "hills") };
        foreach (var (kind, one, many) in new[] { (TerrainKind.Rock, "rock outcrop", "rock outcrops"), (TerrainKind.Vegetation, "stand of vegetation", "stands of vegetation"),
            (TerrainKind.Water, "pool of water", "pools of water"), (TerrainKind.DeepWater, "deep lake", "deep lakes"), (TerrainKind.Ice, "ice sheet", "ice sheets"),
            (TerrainKind.Lava, "lava field", "lava fields") })
            if (terrain.Patches.Count(patch => patch.Kind == kind) is > 0 and var count) features.Add(Count(count, one, many));
        if (terrain.Buildings.Count > 0) features.Add(Count(terrain.Buildings.Count, "building", "buildings"));
        lines.Add($"TERRAIN  {string.Join(" · ", features)}");
        if (simulation.Patrol is { } patrol) lines.Add($"PATROL  {patrol.Visited} of {patrol.Layout.PatrolPoints} points visited");
        lines.Add($"COMBAT TIME  {TimeSpan.FromSeconds(simulation.Elapsed):mm\\:ss}");
        return string.Join("\n", lines);
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
        if (_mission is not null && _settlement is null)
        {
            // A victory with something to salvage waits for the leader's choice; anything else settles at once.
            // The other co-op players see the same pool, read-only, until the settlement arrives.
            var report = Simulation.MissionReport();
            SalvagePool = report.Outcome == MissionOutcome.Success ? Salvage.Pool(report) : [];
            if (SalvagePool.Count == 0 && _settle is not null) _settlement = _settle(Simulation, []);
        }
        else SalvagePool = [];
        var choosing = Leading && _settle is not null && SalvagePool.Count > 0;
        var viewing = !choosing && SalvagePool.Count > 0;
        var extractedEarly = Simulation.Patrol is not null && Simulation.Units.Any(unit => unit.Extracted);
        var title = Simulation.Outcome == CombatOutcome.Ended ? (extractedEarly ? "Extracted before the patrol was complete" + (_mission is null ? "" : " · Mission failed")
                : _mission is null ? "Simulation ended" : "Withdrew · Mission failed")
            : _mission is null ? Simulation.Outcome.ToString() : $"{Simulation.Outcome} · Mission {(Simulation.Outcome == CombatOutcome.Victory ? "succeeded" : "failed")}";
        // A finished battle has nothing left to wait for.
        StopWaitingButton.Hide();
        BeginModal(CombatModal.Results, title);
        ResultsTabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _modalContent.AddChild(ResultsTabs);
        var outcome = new VBoxContainer { Name = "Outcome" }; outcome.AddThemeConstantOverride("separation", 12); ResultsTabs.AddChild(outcome);
        RosterUi.Text(outcome, $"Combat time {TimeSpan.FromSeconds(Simulation.Elapsed):mm\\:ss}{(_mission is null ? " · Campaign preserved" : "")}", 15, Ui.Gold);
        if (_mission is not null && Simulation.Outcome == CombatOutcome.Victory && Simulation.EnemyHoldsField)
            RosterUi.Text(outcome, "No salvage: the opposition still holds the field.", 14, Ui.Muted);
        if (_settlement is not null) RosterUi.Text(outcome, _settlement, 14);
        else if (choosing) RosterUi.Text(outcome, $"Choose salvage from the field: the contract allows {Picks(_mission!.SalvagePicks)}. "
            + $"Up to {Picks(SalvageTuning.ExtraPicks)} more can be taken, at a cost to your reputation.", 14);
        else if (viewing) RosterUi.Text(outcome, $"The leader is choosing salvage: the contract allows {Picks(_mission!.SalvagePicks)}, "
            + $"and up to {Picks(SalvageTuning.ExtraPicks)} more at a cost to reputation.", 14, Ui.Muted);
        else if (_mission is not null) RosterUi.Text(outcome, Simulation.Outcome == CombatOutcome.Victory
            ? "Waiting for the leader to choose salvage and settle the mission…" : "Waiting for the leader to settle the mission…", 14, Ui.Muted);
        RosterUi.Text(outcome, $"Terrain seed: {Simulation.Terrain.Seed} · {BiomeText(Simulation)}", 14, Ui.Muted);
        if (choosing) SalvageCount = RosterUi.Text(outcome, "", 14, Ui.Gold);
        var body = RosterUi.Scroll(outcome);
        if (choosing || viewing) ShowSalvage(body, readOnly: viewing);
        ShowCompanyStatus(body);
        ShowResultTables();
        ShowHighlights();
        ResultsTabs.CurrentTab = Math.Clamp(_resultsTab, 0, ResultsTabs.GetTabCount() - 1);
        ResultsTabs.TabChanged += tab => _resultsTab = (int)tab;
        var actions = ModalActions();
        if (choosing)
        {
            TakeNothingButton = Ui.Button(actions, "Take Nothing"); TakeNothingButton.Pressed += () => SettleSalvage([]);
            ClaimSalvageButton = Ui.Sound(Ui.Button(actions, "Claim Salvage"), Cue.UiConfirm);
            ClaimSalvageButton.Pressed += () => SettleSalvage(ChosenSalvage());
            RefreshSalvage(); TakeNothingButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
            return;
        }
        ReturnButton = Ui.Button(actions, Leading ? "Return to hangar" : "Waiting for leader"); ReturnButton.Pressed += Exit;
        ReturnButton.Disabled = !Leading;
        ReturnButton.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(_shade);
    }
    private static string Picks(int count) => $"{count} {(count == 1 ? "pick" : "picks")}";
    /// <summary>One block per enemy mech: the whole mech, when it can be taken, then each item left working.
    /// Read-only for a co-op player other than the leader: labels to look over, not toggles.</summary>
    private void ShowSalvage(VBoxContainer body, bool readOnly = false)
    {
        SalvageChoices.Clear();
        RosterUi.Text(body, "SALVAGE", 16, Ui.Gold);
        foreach (var unit in SalvagePool.GroupBy(option => option.UnitId))
        {
            var enemy = Simulation!.Unit(unit.Key);
            RosterUi.Text(body, $"{enemy.Spec.DisplayName} · {enemy.Status}", 14);
            var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 14); body.AddChild(row);
            foreach (var option in unit)
            {
                if (readOnly)
                {
                    // Not word-wrapped: a wrapping label in a flow row shrinks to nothing and breaks after every letter.
                    var item = Ui.Label(row, option.WholeMech ? $"Whole mech · {Picks(option.Picks)}" : $"{option.Name} · {Picks(option.Picks)}", 13);
                    item.MouseFilter = MouseFilterEnum.Pass;
                    item.TooltipText = option.WholeMech ? "Joins the hangar's reserves as it stands, with no pilot" : StoredTooltip(option);
                    continue;
                }
                var choice = new CheckBox { Text = option.WholeMech ? $"Claim whole · {Picks(option.Picks)}" : $"{option.Name} · {Picks(option.Picks)}",
                    FocusMode = FocusModeEnum.All, TooltipText = option.WholeMech ? "Joins your hangar's reserves as it stands, with no pilot" : StoredTooltip(option) };
                row.AddChild(choice); SalvageChoices[option.Claim] = choice;
                choice.Toggled += _ => RefreshSalvage();
            }
        }
    }
    /// <summary>A salvaged item's tooltip: a weapon's figures, then where it goes.</summary>
    private static string StoredTooltip(SalvageOption option) =>
        DefinitionText.WeaponDetails(option.StorageId) is { } details ? $"{option.Name}\n{details}\nGoes into hangar storage" : "Goes into hangar storage";
    private SalvageClaim[] ChosenSalvage() => SalvagePool.Select(option => option.Claim)
        .Where(claim => SalvageChoices.TryGetValue(claim, out var choice) && choice.ButtonPressed && !choice.Disabled).ToArray();
    /// <summary>A mech claimed whole cannot also be stripped; the count shows what extra picks will cost.</summary>
    private void RefreshSalvage()
    {
        if (_mission is null) return;
        var whole = SalvagePool.Where(option => option.WholeMech && SalvageChoices[option.Claim].ButtonPressed).Select(option => option.UnitId).ToHashSet();
        foreach (var option in SalvagePool.Where(option => !option.WholeMech))
            SalvageChoices[option.Claim].Disabled = whole.Contains(option.UnitId);
        var picks = SalvagePool.Where(option => ChosenSalvage().Contains(option.Claim)).Sum(option => option.Picks);
        var over = Math.Max(0, picks - _mission.SalvagePicks); var limit = _mission.SalvagePicks + SalvageTuning.ExtraPicks;
        var cost = new List<string>();
        if (_mission.Employer.Faction is { } employer) cost.Add($"{Factions.Spec(employer).ShortName} −{over * -ReputationTuning.EmployerSalvagePerPick / ReputationTuning.Scale}");
        cost.Add($"ComStar −{(over * -ReputationTuning.ComStarSalvagePerPick / (double)ReputationTuning.Scale).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}");
        SalvageCount.Text = $"Picks {picks} of {_mission.SalvagePicks}" + (over > 0 ? $" · {over} over the allotment: {string.Join(", ", cost)} reputation" : "")
            + (picks > limit ? $" · at most {limit} can be taken" : "");
        SalvageCount.AddThemeColorOverride("font_color", picks > limit ? new Color("e7a185") : Ui.Gold);
        ClaimSalvageButton.Disabled = picks == 0 || picks > limit;
    }
    private void SettleSalvage(SalvageClaim[] claims)
    {
        if (Simulation is null || _settle is null || _settlement is not null) return;
        _settlement = _settle(Simulation, claims); SalvagePool = []; ShowResults();
    }
    private void Exit()
    {
        // Salvage must be claimed or declined before the results close.
        if (!Leading || Modal == CombatModal.Results && SalvagePool.Count > 0) return;
        Close();
    }
    /// <summary>Leaves the screen; a co-op player other than the leader leaves when the leader does.</summary>
    public void Close() { Battlefield.CancelDrag(); Hide(); Closed?.Invoke(); }
    /// <summary>Shows a player's ping on the battlefield.</summary>
    public void ShowPing(int slot, Vector2 point) => Battlefield.Ping(point, PlayerName(slot), slot == Slot ? Ui.Gold : new Color("7fd4ff"));
    private void ToggleBattleLog()
    {
        if (Simulation is null || Modal != CombatModal.None) return;
        Battlefield.CancelDrag(); BattleLog.Visible = !BattleLog.Visible; Layout();
    }
    private void ToggleControls()
    {
        if (ControlsPanel.Visible) { ControlsPanel.Hide(); return; }
        if (Simulation is null || Modal != CombatModal.None) return;
        ControlsPanel.Display(Coop); ControlsPanel.Show();
    }
    public bool HandleKey(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return false;
        if (key.Keycode == Key.Escape)
        {
            if (Modal is CombatModal.OpponentSelection or CombatModal.ForceSelection or CombatModal.Results) Exit();
            else if (Modal == CombatModal.Menu) Resume();
            else if (Modal == CombatModal.MissionDetails) CloseMissionDetails();
            else if (SettingJump) { SettingJump = false; RefreshDetails(); }
            else if (ControlsPanel.Visible) ControlsPanel.Hide();
            else OpenMenu();
            return true;
        }
        // The tactical view has one global focus: only a modal holds these keys back.
        if (key.Keycode == Key.Space && Modal == CombatModal.None) { TogglePause(); return true; }
        var speedKey = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (speedKey is Key.Comma or Key.Period && Modal == CombatModal.None)
        { SetSpeed(speedKey == Key.Comma ? 1 : CombatSimulation.FastTimeScale); return true; }
        if (Simulation is null || Modal != CombatModal.None || key.CtrlPressed || key.AltPressed || key.MetaPressed) return false;
        var code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        // ? is Shift+/ on most layouts; the typed character covers the others.
        if (key.Unicode == '?' || code == Key.Slash && key.ShiftPressed) { ToggleControls(); return true; }
        if (code is >= Key.Key1 and <= Key.Key4)
        {
            var slot = (int)code - (int)Key.Key1;
            var unit = OwnUnits.ElementAtOrDefault(slot);
            if (unit is not null)
            {
                Battlefield.CancelDrag(); Battlefield.InspectedEnemy = null;
                SelectUnits([unit.Id], key.ShiftPressed);
            }
            return true;
        }
        Action? order = code switch
        {
            Key.Z => StopUnits,
            Key.X => () => Send(new MovementCommand([.. _selected], MovementMode.Walk)),
            Key.C => () => Send(new MovementCommand([.. _selected], MovementMode.Run)),
            Key.G => () => Send(new FiringCommand([.. _selected], FiringMode.HoldFire)),
            Key.F => () => Send(new FiringCommand([.. _selected], FiringMode.FireAtWill)),
            Key.J => ToggleJump,
            Key.R => CycleRangeOverlay,
            Key.V => () => PingRequested?.Invoke(Battlefield.GroundPoint(Battlefield.GetLocalMousePosition())),
            _ => null
        };
        if (order is not null) { order(); return true; }
        if (code == Key.Quoteleft || key.Keycode == Key.Asciitilde)
        {
            if (key.ShiftPressed || key.Keycode == Key.Asciitilde) ToggleBattleLog();
            else Battlefield.ResetToSelection();
            return true;
        }
        if (key.Keycode == Key.Home) { Battlefield.ResetToArena(); return true; }
        return false;
    }
    public override void _Process(double delta)
    {
        if (!Visible || Simulation is null || Modal is CombatModal.ForceSelection or CombatModal.Results) return;
        if (Modal == CombatModal.None) Simulation.Advance(delta);
        if (Simulation.Outcome != CombatOutcome.Ongoing) { ShowResults(); return; }
        SoundHeatAlarm(delta);
        _uiElapsed += delta; _flashClock += delta;
        if (_ejectArmed > 0 && (_ejectArmed -= delta) <= 0) { _ejectArmed = 0; RefreshDetails(); }
        FadeNotice(delta);
        if (_dropNoticeSeconds > 0) _dropNoticeSeconds = Math.Max(0, _dropNoticeSeconds - delta);
        if (_uiElapsed >= .1) { _uiElapsed = 0; RefreshDetails(); }
    }
    /// <summary>While a friendly mech runs hot enough for heat penalties, an alarm sounds every couple of seconds.</summary>
    private void SoundHeatAlarm(double delta)
    {
        _heatAlarm = Math.Max(0, _heatAlarm - delta);
        if (Simulation!.Paused || _heatAlarm > 0) return;
        if (!Simulation.Units.Any(unit => unit.Side == CombatSide.Friendly && unit.Active && unit.Heat >= CombatRules.HotHeat)) return;
        SoundPlayer.For(this)?.Play(Cue.HeatWarning);
        _heatAlarm = HeatAlarmSeconds;
    }
    private const double HeatAlarmSeconds = 2;
    private double _heatAlarm;
    private void RefreshDetails()
    {
        if (Simulation is null) return;
        RefreshSurrender(); RefreshDrop();
        // Overlays that change size or appear move what lies below them.
        var relayout = RefreshContacts();
        var objective = ObjectiveText();
        if (ObjectiveLabel.Text != objective || ObjectiveLabel.Visible != (objective != ""))
        { ObjectiveLabel.Text = objective; ObjectiveLabel.Visible = objective != ""; relayout = true; }
        // The time panel shows the local player's own pick and flashes for the battle's state and other players' picks.
        RefreshTimePanel();
        // Jump targeting lapses once no selected unit can still jump.
        if (SettingJump && JumpUnavailable() is { } lapsed) { SettingJump = false; RefuseJump(lapsed); }
        Battlefield.JumpArmed = SettingJump;
        var friends = OwnUnits.ToArray();
        for (var i = 0; i < UnitButtons.Count; i++)
        {
            var friend = friends[i]; var button = UnitButtons[i];
            button.Text = CardText(friend);
            // The tooltip keeps the full model and state the glyphs stand for.
            button.TooltipText = $"{friend.Spec.DisplayName} · {friend.Status}{friend.StanceText()}{friend.ConductText()}{StressReadout(friend)}\n{HeatReadout(friend)}";
            var status = CombatPaperdoll.StatusIcon(friend);
            UnitStatusIcons[i].Visible = status is not null;
            if (status is { } shown) { UnitStatusIcons[i].Texture = shown.Icon; UnitStatusIcons[i].SelfModulate = shown.Tint; }
            button.AddThemeStyleboxOverride("normal", CardBox(_selected.Contains(friend.Id) ? HudSelectedBox : HudBox));
            // A friendly card shows its MechWarrior's stress as a tinted face beside the callsign.
            UnitFaces[i].Visible = !friend.Defeated; UnitFaces[i].Texture = CombatPaperdoll.StressIcon(friend.Stress);
            UnitFaces[i].SelfModulate = CombatPaperdoll.StressColor(friend.Stress);
            UnitDolls[i].Display(friend);
        }
        var primary = _selected.FirstOrDefault(); var unit = primary is null ? null : Simulation.Unit(primary);
        var active = _selected.Select(Simulation.Unit).Where(item => !item.Defeated).ToArray();
        var inspected = Battlefield.InspectedEnemy;
        if (inspected is not null && !Simulation.Detected(CombatSide.Friendly, inspected))
            Battlefield.InspectedEnemy = inspected = null;
        // With nothing selected or inspected, the details panel is hidden.
        var details = unit is not null || inspected is not null;
        if (_sidebar.Visible != details) { _sidebar.Visible = details; relayout = true; }
        if (relayout) Layout();
        _settings.Visible = active.Length > 0 && inspected is null;
        _weapons.Visible = inspected is null;
        _weaponsTitle.Visible = unit is not null && inspected is null;
        // A group's ammo gauge belongs to its first mech with ammo weapons and sets every such mech.
        Paperdoll.Display(unit, unit is { HasAmmoWeapons: true } ? unit : active.FirstOrDefault(item => item.HasAmmoWeapons));
        PilotRow.Visible = unit is not null && inspected is null;
        if (unit is not null && inspected is null && PilotInfo.Text != PilotText(unit.Warrior))
        {
            PilotPortrait.Texture = Ui.LoadTexture(unit.Warrior.Artwork);
            PilotInfo.Text = PilotText(unit.Warrior); PilotInfo.TooltipText = unit.Warrior.Experience;
        }
        if (_primary != primary)
        {
            _primary = primary; RosterUi.Clear(_weapons); _weaponControls.Clear();
            if (unit is not null)
                foreach (var weapon in unit.Weapons)
                {
                    var toggle = new CheckButton { ButtonPressed = weapon.Enabled, AutowrapMode = TextServer.AutowrapMode.WordSmart };
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
            _unitInfo.Text = detailed ? $"OPPOSITION · {enemy.Spec.Tonnage}t · {enemy.Status}{enemy.StanceText()}{enemy.ConductText()}"
                : "OPPOSITION · Model identified\nClose to half detection range for its condition.";
            Paperdoll.Display(detailed ? enemy : null);
            _targetInfo.Text = "";
            return;
        }
        if (unit is null) return;
        _selectionTitle.Text = _selected.Count > 1 ? $"{_selected.Count} selected · {unit.Warrior.Callsign}" : $"{unit.Warrior.Callsign} · {unit.Spec.Chassis}";
        // Heat, cooling, jump and order state are left to the dolls, cards, controls and battlefield.
        _unitInfo.Text = $"{unit.Spec.DisplayName} · {unit.Status}{unit.StanceText()}{StressReadout(unit)}";
        EjectButton.Text = _ejectArmed > 0 ? "Confirm Eject" : "Eject";
        EjectButton.Modulate = _ejectArmed > 0 ? new Color("ff9a7a") : Colors.White;
        EjectButton.Disabled = active.All(item => item.Ejected);
        if (active.Length > 0)
        {
            _groupInfo.Visible = active.Length > 1;
            _groupInfo.Text = $"{active.Length} units · edits apply to group\n"
                + $"Speed: {(active.Select(item => CombatPaperdoll.Kmh(item.DesiredSpeed)).Distinct().Count() > 1 ? "Mixed" : $"{CombatPaperdoll.Kmh(active[0].DesiredSpeed)} km/h")} · Fire: {(active.Select(item => item.Firing).Distinct().Count() > 1 ? "Mixed" : active[0].Firing == FiringMode.FireAtWill ? "At will" : "Hold")}\n"
                + $"Heat limit: {(active.Select(item => item.HeatLimit).Distinct().Count() > 1 ? "Mixed" : active[0].HeatLimit.ToString())}"
                + (active.Where(item => item.HasAmmoWeapons).Select(item => item.AmmoEconomy).Distinct().ToArray() is { Length: > 0 } economies
                    ? $" · Ammo economy: {(economies.Length > 1 ? "Mixed" : economies[0].ToString())}" : "");
            _fire.Text = (active.All(item => item.Firing == FiringMode.FireAtWill) ? "● Fire at will" : "Fire at will") + " [F]";
            _hold.Text = (active.All(item => item.Firing == FiringMode.HoldFire) ? "● Hold fire" : "Hold fire") + " [G]";
            RangeOverlayButton.Text = RangeOverlayLabel(Battlefield.Overlay);
            _refreshing = true;
            var maintaining = active.All(item => item.MaintainRange);
            MaintainRangeToggle.SetPressedNoSignal(maintaining);
            // Leave a drag in progress alone; the reading follows the handle until it is let go.
            if (!_draggingDistance)
            {
                DistanceSlider.SetValueNoSignal(active[0].PreferredDistance);
                DistanceValue.Text = active.Select(item => item.PreferredDistance).Distinct().Count() > 1 ? "Mixed" : $"{active[0].PreferredDistance:0} m";
            }
            DistanceSlider.Modulate = maintaining ? Colors.White : new Color(1, 1, 1, .45f);
            const string tip = "Without a Move or Follow order, keep this range from the priority target. Applies to all selected units that are not defeated.";
            MaintainRangeToggle.TooltipText = DistanceSlider.TooltipText = tip;
            _refreshing = false;
        }
        var firingTarget = Simulation.SelectAimTarget(unit);
        var target = firingTarget ?? Simulation.Units.Where(item => item.Side != unit.Side && !item.Defeated && Simulation.Detected(unit.Side, item.Id))
            .OrderBy(item => item.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault();
        _targetInfo.Text = (target is null ? (Simulation.Patrol is null ? "No contact · move toward arena center" : "No contact · continue the patrol") : $"{(firingTarget is null ? "Aim preview" : "Target")}: {target.Spec.Chassis} · {unit.Position.DistanceTo(target.Position):0} m");
        if (target is not null)
        {
            var clear = Simulation.Sight(unit, target);
            var cover = Simulation.Terrain.VegetationModifier(unit.Position, target.Position);
            var spotter = firingTarget is null ? null : unit.Weapons.Where(weapon => weapon.Enabled && unit.WeaponUsable(weapon))
                .Select(weapon => Simulation.IndirectSpotter(unit, firingTarget, weapon)).FirstOrDefault(item => item is not null);
            _targetInfo.Text += spotter is not null ? $"\nIndirect fire · spotted by {Simulation.SlotLabel(spotter)} · +{CombatRules.IndirectFireModifier}"
                : !clear ? "\nLine of fire blocked by terrain" : cover > 0 ? $"\nVegetation: +{cover} hit difficulty" : "\nClear line of fire";
        }
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
    }
    /// <summary>A sidebar button that shares its row's width and wraps its label, so the narrow sidebar never overflows.</summary>
    private static Button Fit(Button button)
    {
        button.AutowrapMode = TextServer.AutowrapMode.WordSmart; button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return button;
    }
    /// <summary>" · Stress: Excited (skills −1)" for a friendly MechWarrior still in the fight. Enemy stress is never shown.</summary>
    internal static string StressReadout(CombatUnit unit) => unit.Side != CombatSide.Friendly || unit.Defeated ? ""
        : $" · Stress: {unit.Stress}" + (unit.StressPenalty > 0 ? $" (skills −{unit.StressPenalty})" : "");
    internal static string PilotText(WarriorSpec warrior) => $"“{warrior.Callsign}”\n{warrior.Name}\n{RosterUi.SkillLines(warrior)}";
    internal static string BiomeText(CombatSimulation simulation) => simulation.Terrain.Biome is { } biome ? Biomes.Spec(biome).Name : "Test terrain";
    internal static string HeatReadout(CombatUnit unit) =>
        $"Heat {unit.Heat:0.0} · {unit.HeatBand}{(unit.Side == CombatSide.Friendly ? $" · Heat limit {unit.HeatLimit}" : "")}\n"
        + $"Cooling {unit.CoolingRate:0.00} heat/s · {unit.EffectiveHeatSinks}/{unit.Spec.HeatSinks} {DefinitionText.HeatSinks(unit.Spec.HeatSinkType)} sinks"
        + (unit.Climate is { } climate ? $" × {climate.EfficiencyText}"
            + $"\nAmbient {climate.Ambient:0} · {(unit.AmbientHeatRate >= 0 ? "+" : "−")}{Math.Abs(unit.AmbientHeatRate):0.00} heat/s" : "")
        + (unit.Shutdown && !unit.Defeated ? "\nAutomatic restart below 10 heat" : "");

}
