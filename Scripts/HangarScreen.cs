using System.Collections.Immutable;
using System.Globalization;
using Godot;

namespace Mech;

/// <summary>Inventory and market tabs on the left, the selected mech as a blueprint with one panel per body part, and the pilot and summary on the right.</summary>
public partial class HangarScreen : Control
{
    public const float CompactWidth = 1100;
    public event Action? BackRequested;
    public event Action<string, string?>? AssignmentRequested;
    public event Action? WarriorDetailsRequested;
    public event Action? DeploymentRequested;
    public event Action? QuickLaunchRequested;
    public event Action? ApplyRequested;
    /// <summary>Moves a mech into a lance, or into the lance pool (null).</summary>
    public event Action<string, int?>? LanceRequested;
    /// <summary>Orders a repair of a mech.</summary>
    public event Action<string, RepairScope>? RepairRequested;
    /// <summary>Cancels a mech's repair order.</summary>
    public event Action<string>? CancelRepairRequested;
    /// <summary>The campaign, for the date and repair bays; null shows repair orders without dates or queue places.</summary>
    public Campaign? Campaign { get; set; }
    public Button RepairArmorButton { get; private set; } = null!;
    public Button RepairAllButton { get; private set; } = null!;
    public Button CancelRepairButton { get; private set; } = null!;
    /// <summary>The selected mech's repair order: under repair until a date, or queued.</summary>
    public Label RepairText { get; private set; } = null!;
    /// <summary>A mech with a repair order cannot change its loadout.</summary>
    public bool SelectedLocked => SelectedMech.Repair is not null;
    /// <summary>The local player's slot: its own lance is the one mechs join from the lance pool.</summary>
    public int Slot { get; set; } = Campaign.LeaderSlot;
    /// <summary>Adds the selected mech to the player's lance, or returns it to the lance pool.</summary>
    public Button LanceButton { get; private set; } = null!;
    public Label LanceText { get; private set; } = null!;
    /// <summary>The players' names by slot in a co-op campaign, naming whose lance each is; null in single-player.</summary>
    public IReadOnlyList<string>? LanceOwners { get; set; }
    /// <summary>Whether the local player may change the selected mech: one in their own lance or the lance pool.</summary>
    public bool SelectedControlled => _roster.Controls(Slot, SelectedMech);
    public Button BackButton { get; private set; } = null!;
    public Button WarriorsButton { get; private set; } = null!;
    public Button DeploymentButton { get; private set; } = null!;
    public Button QuickLaunchButton { get; private set; } = null!;
    /// <summary>Why the current mission cannot be launched apart from pending loadout changes, or null when it can.</summary>
    public string? QuickLaunchBlock { get => _quickLaunchBlock; set { _quickLaunchBlock = value; UpdateQuickLaunch(); } }
    private string? _quickLaunchBlock = "Accept a mission from the office phone first.";
    public bool CanQuickLaunch => QuickLaunchBlock is null && !HasPendingChanges;
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
    /// <summary>Company cash, against which pending purchases are checked; nothing is paid until a loadout is applied.</summary>
    public long Cash { get; set; }
    /// <summary>The system whose market is open, or null while the DropShip is in transit.</summary>
    public string? MarketSystemId { get; private set; }
    /// <summary>Storage ids of the items the selected mech's pending loadout buys from the market.</summary>
    public IReadOnlyList<string> SelectedPurchases => Purchases(SelectedMech.Id);
    public long SelectedPurchaseCost => SelectedPurchases.Sum(EquipmentMarket.Price);
    public long PendingPurchaseCost => _purchases.Keys.Sum(id => Purchases(id).Sum(EquipmentMarket.Price));
    public bool SelectedAffordable => SelectedPurchaseCost <= Cash;
    public bool SelectedCanApply => SelectedHasPendingChanges && !SelectedOverTonnage && SelectedAffordable && SelectedControlled && !SelectedLocked;
    public MechPreview Preview { get; private set; } = null!;
    public bool ArtworkLoaded => _background.Texture is not null;
    public IReadOnlyDictionary<MechLocation, HangarLocationPanel> LocationPanels => _locations;
    public PanelContainer StoragePanel { get; private set; } = null!;
    /// <summary>The left column's Inventory and Market tabs.</summary>
    public TabContainer StorageTabs { get; private set; } = null!;
    public VBoxContainer MarketList { get; private set; } = null!;
    /// <summary>Cash and pending purchases, above the market lines.</summary>
    public Label MarketText { get; private set; } = null!;
    public PanelContainer SummaryPanel { get; private set; } = null!;
    public ScrollContainer Board { get; private set; } = null!;
    public VBoxContainer StorageList { get; private set; } = null!;
    public Label SpeedText { get; private set; } = null!;
    public HangarConditionBar ArmorBar { get; private set; } = null!;
    public HangarConditionBar StructureBar { get; private set; } = null!;
    /// <summary>Heat sunk over the most heat the mech can generate, both per second.</summary>
    public HangarConditionBar CoolingBar { get; private set; } = null!;
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
    private TextureRect _portrait = null!;
    private Control _boardContent = null!;
    private HBoxContainer _rosterButtons = null!;
    private ScrollContainer _rosterScroll = null!;
    private readonly Dictionary<MechLocation, HangarLocationPanel> _locations = [];
    private readonly List<string> _pilotIds = [];
    // Pending loadouts by mech id, with the preview mech each one displays.
    private readonly Dictionary<string, LoadoutPlan> _pending = [];
    private readonly Dictionary<string, OwnedMech> _previews = [];
    // Installed item ids in each pending loadout that are bought from the market rather than taken from storage.
    private readonly Dictionary<string, ImmutableHashSet<string>> _purchases = [];
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
            panel.Dropped += (id, purchase, mount) => Install(id, location, mount, purchase);
            panel.ArmorChanged += (rear, points) => ChangeArmor(location, rear, points);
        }
        OverTonnageText = new Label { Text = "⚠ Over Tonnage", HorizontalAlignment = HorizontalAlignment.Center, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        OverTonnageText.AddThemeColorOverride("font_color", HangarLocationPanel.Warning); OverTonnageText.AddThemeFontSizeOverride("font_size", 16);
        AddChild(OverTonnageText);
        BackButton = Ui.Button(this, "Back to office"); BackButton.Pressed += () => BackRequested?.Invoke();

        StoragePanel = Panel();
        StorageTabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; StoragePanel.AddChild(StorageTabs);
        var storage = RosterUi.Scroll(StorageTabs); storage.GetParent<Control>().Name = "Inventory";
        storage.AddThemeConstantOverride("separation", 4);
        StorageList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StorageList.AddThemeConstantOverride("separation", 2); storage.AddChild(StorageList);
        var market = RosterUi.Scroll(StorageTabs); market.GetParent<Control>().Name = "Market";
        market.AddThemeConstantOverride("separation", 4);
        MarketText = RosterUi.Text(market, "", 12, Ui.Muted);
        MarketList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        MarketList.AddThemeConstantOverride("separation", 2); market.AddChild(MarketList);

        SummaryPanel = Panel();
        var summary = RosterUi.Scroll(SummaryPanel);
        summary.AddThemeConstantOverride("separation", 6);
        var pilotRow = new HBoxContainer(); pilotRow.AddThemeConstantOverride("separation", 8); summary.AddChild(pilotRow);
        _portrait = RosterUi.Picture(pilotRow, "", new Vector2(72, 72));
        _pilot = RosterUi.Text(pilotRow, "", 13); _pilot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        PilotPicker = new OptionButton { ClipText = true, FitToLongestItem = false }; summary.AddChild(PilotPicker);
        PilotPicker.ItemSelected += _ => UpdateAssignmentAction();
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 6); summary.AddChild(actions);
        AssignButton = Ui.Button(actions, "Assign warrior"); AssignButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AssignButton.Pressed += () => { if (PilotPicker.Selected >= 0 && PilotPicker.Selected < _pilotIds.Count) AssignmentRequested?.Invoke(SelectedMech.Id, _pilotIds[PilotPicker.Selected]); };
        UnassignButton = Ui.Button(actions, "Unassign"); UnassignButton.Pressed += () => AssignmentRequested?.Invoke(SelectedMech.Id, null);
        WarriorsButton = Ui.Button(summary, "View MechWarriors"); WarriorsButton.Pressed += () => WarriorDetailsRequested?.Invoke();
        var lanceRow = new HBoxContainer(); lanceRow.AddThemeConstantOverride("separation", 6); summary.AddChild(lanceRow);
        LanceText = RosterUi.Text(lanceRow, "", 13); LanceText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        LanceButton = Ui.Button(lanceRow, "");
        LanceButton.Pressed += () => LanceRequested?.Invoke(SelectedMech.Id, SelectedMech.Lance is null ? Slot : null);
        summary.AddChild(new HSeparator());
        _mechName = RosterUi.Text(summary, "", 20, Ui.Gold);
        TonnageText = RosterUi.Text(summary, "", 20);
        _mechClass = RosterUi.Text(summary, "", 11, Ui.Muted);
        _mechStatus = RosterUi.Text(summary, "", 13, new Color("a5c4a5"));
        SpeedText = RosterUi.Text(summary, "", 13);
        ArmorBar = new HangarConditionBar(); summary.AddChild(ArmorBar);
        StructureBar = new HangarConditionBar(); summary.AddChild(StructureBar);
        CoolingBar = new HangarConditionBar(); summary.AddChild(CoolingBar);
        var repairs = new HBoxContainer(); repairs.AddThemeConstantOverride("separation", 6); summary.AddChild(repairs);
        RepairArmorButton = Ui.Button(repairs, "Repair Armor"); RepairArmorButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        RepairArmorButton.Pressed += () => RequestRepair(RepairScope.Armor);
        RepairAllButton = Ui.Button(repairs, "Repair All"); RepairAllButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        RepairAllButton.Pressed += () => RequestRepair(RepairScope.All);
        RepairText = RosterUi.Text(repairs, "", 13, HangarLocationPanel.Warning); RepairText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        RepairText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        CancelRepairButton = Ui.Button(repairs, "Cancel Repair");
        CancelRepairButton.Pressed += () => { if (SelectedMech.Repair is not null && SelectedControlled) CancelRepairRequested?.Invoke(SelectedMech.Id); };

        DeploymentButton = Ui.Button(this, "Simulate Deployment");
        DeploymentButton.Pressed += () => DeploymentRequested?.Invoke();
        QuickLaunchButton = Ui.Button(this, "Quick Launch");
        QuickLaunchButton.Pressed += () => { if (CanQuickLaunch) QuickLaunchRequested?.Invoke(); };
        ResetButton = Ui.Button(this, "Reset"); ResetButton.Pressed += ResetSelected;
        ApplyButton = Ui.Button(this, "Apply"); ApplyButton.Pressed += () => { if (SelectedCanApply) ApplyRequested?.Invoke(); };
        _rosterScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_rosterScroll); _rosterButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _rosterButtons.AddThemeConstantOverride("separation", 8); _rosterScroll.AddChild(_rosterButtons);
        Resized += Layout; Layout(); Hide();
    }
    private void RequestRepair(RepairScope scope)
    {
        if (RepairProblem(scope) is null) RepairRequested?.Invoke(SelectedMech.Id, scope);
    }
    /// <summary>Why the selected mech cannot be ordered this repair, or null when it can.</summary>
    public string? RepairProblem(RepairScope scope)
    {
        var mech = SelectedMech;
        if (!SelectedControlled) return "Another player's lance";
        if (mech.Repair is not null) return "Already ordered";
        if (SelectedHasPendingChanges) return "Apply or reset loadout changes first.";
        if (Repairs.Estimate(mech, scope) is not { } estimate) return scope == RepairScope.Armor ? "No armor to repair" : "Nothing to repair";
        return estimate.Cost > Cash ? $"Not enough C-bills · {Repairs.Text(estimate)}" : null;
    }
    private void UpdateRepair()
    {
        var mech = SelectedMech; var order = mech.Repair;
        RepairArmorButton.Visible = RepairAllButton.Visible = order is null;
        RepairText.Visible = CancelRepairButton.Visible = order is not null;
        if (order is not null)
        {
            RepairText.Text = RepairStatus(mech);
            CancelRepairButton.Disabled = !SelectedControlled;
            CancelRepairButton.TooltipText = !SelectedControlled ? "Another player's lance"
                : order.Started is null ? "Nothing has been paid yet" : $"Work has started: the {Money(order.Cost)} paid is forfeited";
            return;
        }
        foreach (var (button, scope) in new[] { (RepairArmorButton, RepairScope.Armor), (RepairAllButton, RepairScope.All) })
        {
            var problem = RepairProblem(scope);
            button.Disabled = problem is not null;
            button.FocusMode = problem is null ? FocusModeEnum.All : FocusModeEnum.None;
            button.TooltipText = problem ?? Repairs.Text(Repairs.Estimate(mech, scope)!.Value)
                + (Campaign is { } campaign && campaign.ActiveRepairs >= RepairTuning.Bays ? "\nWaits for a free repair bay" : "\nPaid when work starts");
        }
    }
    /// <summary>"Under repair · completes 12 JAN 3025", or where a queued order stands.</summary>
    public string RepairStatus(OwnedMech mech)
    {
        if (mech.Repair is not { } order) return "";
        if (order.Completes is { } done) return $"Under repair · completes {done.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant()}";
        if (Campaign is not { } campaign) return "Repair queued";
        var ahead = campaign.RepairsAhead(mech);
        return ahead > 0 ? $"Repair queued · {ahead} ahead" : campaign.ActiveRepairs >= RepairTuning.Bays ? "Repair queued · next for a bay"
            : $"Repair queued · needs {Money(order.Cost)}";
    }
    private PanelContainer Panel()
    { var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color(.045f, .07f, .065f, .9f), Ui.Line)); AddChild(panel); return panel; }
    /// <summary>Opens the hangar on a roster, with the market of the system the DropShip orbits (none in transit) and the company's cash.</summary>
    public void Open(ForceRoster roster, string? marketSystemId = null, long cash = 0)
    {
        if (!ReferenceEquals(_roster, roster)) { _selected = 0; AbandonPending(); }
        _roster = roster; MarketSystemId = marketSystemId; Cash = cash; _selected = Math.Clamp(_selected, 0, roster.Mechs.Count - 1); Board.Visible = true; Show();
        BuildRosterButtons();
        ShowStorage(); ShowMarket();
        Select(_selected); Layout(); BackButton.CallDeferred(Control.MethodName.GrabFocus);
    }
    /// <summary>Takes a newer copy of the company, as a co-op player receives it from the leader, keeping the selection and the
    /// pending changes that still apply to it.</summary>
    public void Rebind(ForceRoster roster, string? marketSystemId, long cash)
    {
        if (_roster is null) return;
        var selected = SelectedMech.Id;
        _roster = roster; MarketSystemId = marketSystemId; Cash = cash; _previews.Clear();
        foreach (var id in _pending.Keys.ToArray())
        {
            var mech = roster.Mechs.SingleOrDefault(item => item.Id == id);
            var valid = mech is not null && roster.Controls(Slot, mech);
            if (valid) try { _ = mech!.WithLoadout(_pending[id]); } catch (ArgumentException) { valid = false; }
            if (!valid) { _pending.Remove(id); _purchases.Remove(id); }
        }
        _selected = Math.Max(0, roster.Mechs.ToList().FindIndex(item => item.Id == selected));
        if (RosterButtons.Count != roster.Mechs.Count) BuildRosterButtons();
        if (IsVisibleInTree()) { Refresh(); Layout(); }
    }
    private void BuildRosterButtons()
    {
        var roster = _roster;
        RosterUi.Clear(_rosterButtons); RosterButtons.Clear();
        for (var i = 0; i < roster.Mechs.Count; i++)
        {
            var index = i; var mech = roster.Mechs[i];
            var button = Ui.Button(_rosterButtons, "");
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(150, 54);
            // Small enough for a third line, the damage notice, inside the 54-pixel button.
            button.AddThemeFontSizeOverride("font_size", RosterFontSize); button.AddThemeConstantOverride("line_spacing", -3);
            button.Pressed += () => Select(index); RosterButtons.Add(button);
        }
    }
    private OwnedMech Displayed(OwnedMech mech)
    {
        if (!_pending.TryGetValue(mech.Id, out var plan)) return mech;
        if (!_previews.TryGetValue(mech.Id, out var preview)) _previews[mech.Id] = preview = mech.WithLoadout(plan);
        return preview;
    }
    /// <summary>The roster as the hangar shows it, pending loadouts included, detached from the company.</summary>
    public ForceRoster PendingRoster() => _roster.WithLoadouts(_pending);
    /// <summary>Storage ids of the items a mech's pending loadout buys from the market.</summary>
    private List<string> Purchases(string mechId) =>
        _purchases.TryGetValue(mechId, out var bought) && _pending.TryGetValue(mechId, out var plan)
            ? [.. plan.Installed.Where(item => bought.Contains(item.Id)).Select(item => item.StorageId)] : [];
    /// <summary>Takes a removable item out of the selected mech's pending loadout; the items below it in its location move up. A
    /// pending purchase taken out is simply not bought.</summary>
    public bool Remove(string itemId)
    {
        if (!SelectedControlled || SelectedLocked) return false;
        var plan = SelectedPlan;
        if (plan.Installed.FirstOrDefault(item => item.Id == itemId) is { } installed) Change(plan with { Installed = plan.Installed.Remove(installed) });
        else if (Loadout.Removable(SelectedMech.Definition, itemId) && !plan.Removed.Contains(itemId)) Change(plan with { Removed = plan.Removed.Add(itemId) });
        else return false;
        return true;
    }
    /// <summary>Installs a spare from storage, or one bought from the market, in the first empty slots of a location of the selected
    /// mech, as a pending change; a purchase is paid for when the loadout is applied. A non-weapon dropped on a weapon mount covers
    /// it; weapons always take a mount of their own class.</summary>
    public bool Install(string storageId, MechLocation location, HardpointClass? mount = null, bool purchase = false)
    {
        if (InstallProblem(storageId, location, purchase) is not null) return false;
        var plan = SelectedPlan; var id = Loadout.NextId(SelectedMech.Installed.Concat(plan.Installed));
        if (storageId.StartsWith("weapon:", StringComparison.Ordinal)) mount = null;
        if (purchase) _purchases[SelectedMech.Id] = _purchases.GetValueOrDefault(SelectedMech.Id, []).Add(id);
        Change(plan with { Installed = plan.Installed.Add(new(id, storageId, location, mount)) });
        return true;
    }
    /// <summary>Changes the installed front or rear armor of a location of the selected mech by whole stock points, as a pending
    /// change, within zero and the location's armor limit. Returns whether the armor changed.</summary>
    public bool ChangeArmor(MechLocation location, bool rear, int points)
    {
        if (!SelectedControlled || SelectedLocked) return false;
        var shown = DisplayedMech.Spec.Locations.Single(item => item.Location == location);
        if (rear && !EquipmentRules.HasRearArmor(location)) return false;
        var limit = EquipmentRules.ArmorLimit(shown);
        var front = rear ? shown.Armor : Math.Clamp(shown.Armor + points * CombatRules.DamageScale, 0, limit - shown.RearArmor);
        var back = rear ? Math.Clamp(shown.RearArmor + points * CombatRules.DamageScale, 0, limit - shown.Armor) : shown.RearArmor;
        if (front == shown.Armor && back == shown.RearArmor) return false;
        var stock = SelectedMech.Definition.Locations.Single(item => item.Location == location);
        var plan = SelectedPlan; var fits = plan.Armor.Where(item => item.Location != location);
        if (front != stock.Armor || back != stock.RearArmor) fits = fits.Append(new(location, front, back));
        Change(plan with { Armor = [.. fits] });
        return true;
    }
    /// <summary>Why a spare from storage, or an item bought from the market, cannot go in a location of the selected mech as shown,
    /// or null when it can.</summary>
    public string? InstallProblem(string storageId, MechLocation location, bool purchase = false) =>
        !SelectedControlled ? "Another player's lance" : SelectedLocked ? "Under repair" : purchase ? (MarketSystemId is not { } system || !EquipmentMarket.Offers(system).Any(item => item.Id == storageId) ? "Not for sale here"
                : Loadout.InstallProblem(DisplayedMech, storageId, location))
            : StorageCounts().GetValueOrDefault(storageId) <= 0 ? "None in storage" : Loadout.InstallProblem(DisplayedMech, storageId, location);
    private void Change(LoadoutPlan plan)
    {
        var mech = SelectedMech;
        if (!SelectedControlled || SelectedLocked) return;
        if (plan.SameAs(mech.Plan)) _pending.Remove(mech.Id); else _pending[mech.Id] = plan;
        // Purchases last only as long as their items stay in the pending loadout.
        if (_purchases.TryGetValue(mech.Id, out var bought))
        {
            bought = bought.Intersect(_pending.TryGetValue(mech.Id, out var kept) ? kept.Installed.Select(item => item.Id) : []);
            if (bought.IsEmpty) _purchases.Remove(mech.Id); else _purchases[mech.Id] = bought;
        }
        _previews.Remove(mech.Id); Refresh();
    }
    /// <summary>Storage as the hangar shows it: items taken out in pending loadouts are in it, and items pending installation from
    /// storage, on any mech, are already out of it. Pending purchases never touch it.</summary>
    public Dictionary<string, int> StorageCounts()
    {
        var counts = _roster.Storage.ToDictionary(entry => entry.Id, entry => entry.Count, StringComparer.Ordinal);
        foreach (var (id, plan) in _pending)
            foreach (var (stored, count) in _roster.Mechs.Single(mech => mech.Id == id).StorageChange(plan).Concat(Purchases(id).Select(item => (item, 1))))
                counts[stored] = counts.GetValueOrDefault(stored) + count;
        foreach (var id in counts.Where(pair => pair.Value == 0 && _roster.Storage.All(entry => entry.Id != pair.Key)).Select(pair => pair.Key).ToArray())
            counts.Remove(id);
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
        _purchases.Remove(SelectedMech.Id); _previews.Remove(SelectedMech.Id); Refresh();
    }
    /// <summary>Called once the selected mech's pending loadout has been applied to the company.</summary>
    public void CommitSelected()
    {
        _pending.Remove(SelectedMech.Id); _purchases.Remove(SelectedMech.Id); _previews.Remove(SelectedMech.Id); Refresh();
    }
    public void AbandonPending() { _pending.Clear(); _purchases.Clear(); _previews.Clear(); }
    public void Refresh()
    {
        if (_roster is not null) { ShowStorage(); ShowMarket(); Select(_selected); }
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
            && GetViewport().GuiGetDragData().Obj is HangarDragItem drag && StorageCatalog.Describe(drag.Id) is not null)
        {
            Board.Visible = true;
            foreach (var (location, panel) in _locations) panel.ShowDrop(drag.Id, InstallProblem(drag.Id, location, drag.Purchase), drag.Purchase);
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
        var hardpoints = PreparedMechModels.Hardpoints(SelectedMech.Spec.Id);
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
        var purchased = _purchases.GetValueOrDefault(SelectedMech.Id, []);
        foreach (var (location, panel) in _locations) panel.Display(mech, layout[location], mounts[location], purchased);
        _mechName.Text = spec.DisplayName;
        TonnageText.Text = TonnageSummary(mech);
        TonnageText.AddThemeColorOverride("font_color", mech.OverTonnage ? HangarLocationPanel.Destroyed : mech.Tons < mech.Definition.Tonnage ? HangarLocationPanel.Damaged : Ui.Text);
        OverTonnageText.Visible = mech.OverTonnage;
        _mechClass.Text = $"{DefinitionText.WeightClass(spec.WeightClass).ToUpperInvariant()}  ·  {spec.Role}\n{spec.Engine}";
        _mechStatus.Text = mech.Status;
        SpeedText.Text = SpeedSummary(spec);
        var armor = mech.Locations.Sum(item => item.Armor + item.RearArmor); var structure = mech.Locations.Sum(item => item.Structure);
        ArmorBar.Set(armor, spec.ArmorTotal, spec.Locations.Sum(EquipmentRules.ArmorLimit), "Armor");
        StructureBar.Set(structure, spec.StructureTotal, spec.StructureTotal, "Structure");
        var (generated, sunk) = CoolingRates(mech);
        CoolingBar.SetRatio(sunk / generated, CoolingText(sunk / generated),
            $"Heat generated {Rate(generated)}/s\nHeat sunk {Rate(sunk)}/s\nEvery weapon firing as soon as it is ready, while running");
        _portrait.Texture = warrior is null ? null : Ui.LoadTexture(warrior.Spec.Artwork);
        _pilot.Text = warrior is null ? "No pilot assigned" : $"“{warrior.Spec.Callsign}”\n{warrior.Spec.Name}\n{RosterUi.SkillLines(warrior.Spec)}";
        _pilot.TooltipText = warrior is null ? "" : RosterUi.SkillBudgetText(warrior.Spec);
        UnassignButton.Disabled = warrior is null;
        PilotPicker.Clear(); _pilotIds.Clear();
        foreach (var candidate in _roster.Warriors.Where(item => item.Health != WarriorHealth.Dead))
        {
            var assignment = _roster.Assignment(candidate.Id);
            _pilotIds.Add(candidate.Id); PilotPicker.AddItem($"{candidate.Spec.Callsign}{(candidate.Ready ? "" : " · injured")}{(assignment is null ? "" : $" · {assignment.Spec.Chassis}")}");
            PilotPicker.SetItemTooltip(_pilotIds.Count - 1, $"{candidate.Spec.DisplayName}\n{RosterUi.SkillLines(candidate.Spec)}\n{candidate.Status}\n{assignment?.Spec.DisplayName ?? "Unassigned"}");
        }
        var current = warrior is null ? _pilotIds.FindIndex(id => _roster.Assignment(id) is null) : _pilotIds.IndexOf(warrior.Id);
        PilotPicker.Select(current >= 0 ? current : _pilotIds.Count > 0 ? 0 : -1);
        PilotPicker.Disabled = _pilotIds.Count == 0 || !SelectedControlled; UpdateAssignmentAction();
        if (!SelectedControlled) UnassignButton.Disabled = true;
        var lanceFull = _roster.LanceMechs(Slot).Count() >= ForceRoster.LanceSize;
        LanceText.Text = SelectedMech.Lance is { } lance ? $"{ForceRoster.LanceName(lance)}{(LanceOwners is { } owners && lance < owners.Count ? $" · {owners[lance]}" : "")} · {_roster.LanceMechs(lance).Count()}/{ForceRoster.LanceSize}" : "Lance pool";
        LanceButton.Text = SelectedMech.Lance is null ? $"Add to {ForceRoster.LanceName(Slot)}" : "Move to lance pool";
        LanceButton.Disabled = !_roster.Controls(Slot, SelectedMech) || SelectedMech.Lance is null && lanceFull;
        LanceButton.TooltipText = !_roster.Controls(Slot, SelectedMech) ? "Another player's lance" : SelectedMech.Lance is null && lanceFull
            ? $"A lance holds at most {ForceRoster.LanceSize} BattleMechs" : SelectedMech.Lance is null ? "Only lance mechs deploy" : "Lance pool mechs do not deploy";
        var pending = SelectedHasPendingChanges; var applicable = SelectedCanApply;
        ResetButton.Disabled = !pending; ApplyButton.Disabled = !applicable; UpdateQuickLaunch(); UpdateRepair();
        ResetButton.FocusMode = pending ? FocusModeEnum.All : FocusModeEnum.None;
        ApplyButton.FocusMode = applicable ? FocusModeEnum.All : FocusModeEnum.None;
        var bought = SelectedPurchases.Count; var cost = SelectedPurchaseCost;
        ApplyButton.TooltipText = mech.OverTonnage ? $"Over tonnage by {(mech.Tons - mech.Definition.Tonnage).ToString("0.##", CultureInfo.InvariantCulture)} t"
            : bought == 0 ? "" : $"{(SelectedAffordable ? "Buys" : "Not enough C-bills to buy")} {bought} {(bought == 1 ? "item" : "items")} · {Money(cost)}";
        if (applicable) ApplyButton.AddThemeStyleboxOverride("normal", Ui.Box(new Color("344431"), Ui.Gold));
        else ApplyButton.RemoveThemeStyleboxOverride("normal");
        for (var i = 0; i < RosterButtons.Count; i++)
        {
            var owned = _roster.Mechs[i]; var design = owned.Definition; var damage = RosterNotice(owned);
            RosterButtons[i].Text = $"{design.Chassis}\n{design.Variant} · {design.Tonnage}t{(_pending.ContainsKey(owned.Id) ? " •" : "")}{(owned.Lance is null ? " · Pool" : "")}"
                + (damage is null ? "" : $"\n{damage}");
            var fill = i == index ? new Color("344431") : Ui.Panel;
            var (border, width) = DamageBorder(owned) is { } colour ? (colour, DamageBorderWidth) : (i == index ? Ui.Gold : Ui.Line, 1);
            RosterButtons[i].AddThemeStyleboxOverride("normal", RosterBox(fill, border, width));
            RosterButtons[i].AddThemeStyleboxOverride("hover", RosterBox(fill.Lightened(.08f), border, width));
            RosterButtons[i].AddThemeStyleboxOverride("pressed", RosterBox(fill.Lightened(.15f), border, width));
        }
    }
    public const int RosterFontSize = 12, DamageBorderWidth = 3;
    private static StyleBoxFlat RosterBox(Color fill, Color border, int width)
    {
        var box = Ui.Box(fill, border, width); box.ContentMarginTop = box.ContentMarginBottom = 4; return box;
    }
    /// <summary>The roster button's third line: the repair order, or else the damage.</summary>
    public static string? RosterNotice(OwnedMech mech) => mech.Repair is { } order
        ? order.Started is null ? "Repair Queued" : "Under Repair" : DamageNotice(mech);
    /// <summary>The roster button's damage line, the worst damage first: internals, then systems, then armor.</summary>
    public static string? DamageNotice(OwnedMech mech) => mech.InternalsDamaged ? "Internals Damaged"
        : mech.SystemsDamaged ? "Systems Damaged" : mech.ArmorDamaged ? "Armour Damaged" : null;
    /// <summary>Red for internal or systems damage, yellow for armor damage alone, or none for an undamaged mech.</summary>
    public static Color? DamageBorder(OwnedMech mech) => mech.InternalsDamaged || mech.SystemsDamaged ? HangarLocationPanel.Destroyed
        : mech.ArmorDamaged ? HangarLocationPanel.Warning : null;

    public static string SpeedSummary(MechSpec spec) => CombatRules.MovementSummary(spec, "\n");
    public static string TonnageSummary(OwnedMech mech) =>
        $"{mech.Tons.ToString("0.##", CultureInfo.InvariantCulture)} / {mech.Definition.Tonnage} t";
    /// <summary>The most heat per second the mech can sustain (every usable weapon firing on cooldown, while running) and the
    /// heat per second its working sinks remove.</summary>
    public static (double Generated, double Sunk) CoolingRates(OwnedMech mech) =>
        (mech.Spec.Weapons.Where(mech.WeaponUsable).Sum(weapon => weapon.Heat / (double)CombatRules.Cooldown(weapon)) + CombatRules.RunningHeatRate,
            mech.EffectiveHeatSinks * mech.Spec.CoolingPerSink / CombatRules.HeatClockSeconds);
    public static string CoolingText(double ratio) => $"Cooling {Mathf.RoundToInt(ratio * 100)}%";
    public static string Money(long amount) => $"{amount.ToString("N0", CultureInfo.InvariantCulture)} C-bill{(amount == 1 ? "" : "s")}";
    private static string Rate(double perSecond) => perSecond.ToString("0.##", CultureInfo.InvariantCulture);

    private void ShowStorage()
    {
        RosterUi.Clear(StorageList);
        var lines = StorageCounts().Select(pair => new HangarStorageLine(StorageCatalog.Describe(pair.Key)!, pair.Value) { Name = $"Storage {pair.Key}" }).ToArray();
        if (lines.Length == 0) RosterUi.Text(StorageList, "No spare equipment", 12, Ui.Muted);
        ShowGrouped(StorageList, lines);
    }
    private void ShowMarket()
    {
        RosterUi.Clear(MarketList);
        MarketText.Text = $"Cash {Money(Cash)}\nPending purchases {Money(PendingPurchaseCost)}";
        if (MarketSystemId is not { } system) { RosterUi.Text(MarketList, "No market while in transit", 12, Ui.Muted); return; }
        ShowGrouped(MarketList, EquipmentMarket.Offers(system).Select(item =>
        {
            var line = HangarStorageLine.ForSale(item, EquipmentMarket.Price(item.Id)); line.Name = $"Market {item.Id}"; return line;
        }));
    }
    /// <summary>Lines under a heading per group (Energy, Ballistic, Missile, Ammo, Equipment), by name within each.</summary>
    private static void ShowGrouped(VBoxContainer list, IEnumerable<HangarStorageLine> lines)
    {
        foreach (var group in lines.GroupBy(line => line.Item.Group).OrderBy(group => group.Key))
        {
            var heading = RosterUi.Text(list, group.Key.ToString().ToUpperInvariant(), 11, Ui.Gold);
            heading.CustomMinimumSize = new Vector2(0, 22); heading.VerticalAlignment = VerticalAlignment.Bottom;
            foreach (var line in group.OrderBy(line => line.Item.Name, StringComparer.Ordinal)) list.AddChild(line);
        }
    }
    private void UpdateQuickLaunch()
    {
        if (QuickLaunchButton is null) return;
        QuickLaunchButton.Disabled = !CanQuickLaunch;
        QuickLaunchButton.TooltipText = QuickLaunchBlock ?? (HasPendingChanges ? "Apply or reset loadout changes first." : "Deploy to the current mission.");
    }
    private void UpdateAssignmentAction()
    {
        var valid = PilotPicker.Selected >= 0 && PilotPicker.Selected < _pilotIds.Count;
        AssignButton.Disabled = !valid || SelectedMech.WarriorId == _pilotIds[PilotPicker.Selected] || !SelectedControlled;
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
        // Bottom actions, right to left; compact windows narrow them to leave the roster strip room.
        var actionX = Size.X - 24f;
        Button[] actions = [DeploymentButton, QuickLaunchButton, BackButton, ApplyButton, ResetButton];
        float[] widths = compact ? [185, 125, 125, 80, 80] : [210, 140, 150, 110, 110];
        for (var i = 0; i < actions.Length; i++) { actionX -= widths[i]; Ui.Place(actions[i], actionX, Size.Y - 66, widths[i], 54); actionX -= 8; }
        Ui.Place(_rosterScroll, 24, Size.Y - 70, actionX - 24 - 8, 62);
    }

    /// <summary>Places each body-part panel roughly over its part of a front-facing mech: its right side on the viewer's left.</summary>
    private void LayoutBoard(float width, float height, bool compact)
    {
        const float spacing = 6;
        if (!compact)
        {
            var headers = HangarLocationPanel.HeaderFor(MechLocation.Head) + HangarLocationPanel.HeaderFor(MechLocation.CenterTorso) + HangarLocationPanel.HeaderFor(MechLocation.LeftLeg);
            var row = Math.Clamp((height - headers - 2 * spacing - 40) / 24, 11, 16);
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
            var torso = HangarLocationPanel.HeightFor(MechLocation.RightTorso, row);
            var arm = HangarLocationPanel.HeightFor(MechLocation.RightArm, row);
            var head = HangarLocationPanel.HeightFor(MechLocation.Head, row);
            Place(MechLocation.RightTorso, X(0), 0, row, column);
            Place(MechLocation.RightArm, X(0), torso + spacing, row, column);
            Place(MechLocation.RightLeg, X(0), torso + arm + spacing * 2, row, column);
            Place(MechLocation.Head, X(1), 0, row, column);
            Place(MechLocation.CenterTorso, X(1), head + spacing, row, column);
            Place(MechLocation.LeftTorso, X(2), 0, row, column);
            Place(MechLocation.LeftArm, X(2), torso + spacing, row, column);
            Place(MechLocation.LeftLeg, X(2), torso + arm + spacing * 2, row, column);
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
