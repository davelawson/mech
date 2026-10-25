using Godot;

namespace Mech;

public partial class CommandApp : Control
{
    public OfficeScreen Office { get; private set; } = null!;
    public MapScreen StarMap { get; private set; } = null!;
    public MainMenuScreen MainMenu { get; private set; } = null!;
    public CampaignDialog Dialog { get; private set; } = null!;
    public DraftScreen Drafting { get; private set; } = null!;
    public HangarScreen Hangar { get; private set; } = null!;
    public CombatScreen Tactical { get; private set; } = null!;
    public RolodexPanel Rolodex { get; private set; } = null!;
    public MissionsPanel Missions { get; private set; } = null!;
    public Campaign State { get; private set; } = new();
    public bool IsAdvancing { get; private set; }
    [Export] public string SavePath { get; set; } = "user://campaign.json";
    public event Action<CampaignEvent>? EventReached;
    private ColorRect _header = null!;
    private Label _date = null!;
    private Button _menuButton = null!;
    private CampaignStore _store = null!;
    private double _dayElapsed;
    private bool _untilEvent;
    private Control? _dialogReturnFocus;
    private Control? _officeReturnFocus;
    private bool _warriorsFromHangar;
    private Control? _warriorsReturnFocus;
    private bool _swallowRightRelease;
    private readonly List<PopupMenu> _backPopups = [];

    public override void _Ready()
    {
        Theme = Ui.CreateTheme();
        GetWindow().MinSize = new Vector2I(900, 500);
        Office = GetNode<OfficeScreen>("Office");
        StarMap = GetNode<MapScreen>("StarMap");
        StarMap.TextureChanged += Office.SetWallMap;
        Office.SetWallMap(StarMap.Viewport.Texture);
        _store = new CampaignStore(ProjectSettings.GlobalizePath(SavePath));
        _header = new ColorRect { Color = Ui.Panel, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_header);
        var brand = Ui.Label(_header, "◇  M E C H    |    MERCENARY COMMAND", 16, Ui.Gold);
        Ui.Place(brand, 36, 0, 480, 72);
        _date = Ui.Label(_header, "", 13, Ui.Gold);
        _date.HorizontalAlignment = HorizontalAlignment.Right;
        _menuButton = Ui.Button(_header, "Main menu");
        _menuButton.Pressed += () => Confirm("Return to main menu?", "Unsaved progress will be discarded when you start or load another company.", ShowMainMenu, CloseDialog);
        Hangar = new HangarScreen { Name = "Hangar" }; AddChild(Hangar);
        Tactical = new CombatScreen { Name = "Tactical" }; AddChild(Tactical);
        Drafting = new DraftScreen { Name = "InitialDraft" }; AddChild(Drafting);
        MainMenu = new MainMenuScreen { Name = "MainMenu" };
        AddChild(MainMenu);
        Rolodex = new RolodexPanel { Name = "Rolodex" }; AddChild(Rolodex);
        Missions = new MissionsPanel { Name = "Missions" }; AddChild(Missions);
        Dialog = new CampaignDialog { Name = "CampaignDialog" };
        AddChild(Dialog);
        MainMenu.NewRequested += () =>
        {
            if (!State.RequiresDraft && !State.Bankrupt) Confirm("Start a new company?", "Current unsaved progress will be discarded. Your manual save will be preserved.", StartNewCompany, CloseDialog, "New Company");
            else StartNewCompany();
        };
        MainMenu.LoadRequested += () =>
        {
            if (!State.RequiresDraft && !State.Bankrupt) Confirm("Load saved company?", "Current unsaved progress will be discarded.", LoadGame, CloseDialog, "Load Game");
            else LoadGame();
        };
        MainMenu.ResumeRequested += ShowOffice;
        Office.WarriorsRequested += ShowWarriors;
        Office.HangarRequested += ShowHangar;
        Rolodex.Closed += CloseWarriors;
        Office.MissionsRequested += ShowMissions;
        Missions.Closed += ShowOffice;
        Missions.AcceptRequested += id => { State.Missions.Accept(id); ShowOffice(); };
        Missions.AbandonRequested += () => Confirm("Abandon mission?",
            $"Abandon {State.Missions.Current!.Name}?\n\nThere is no penalty. The mission will remain available.",
            () =>
            {
                State.Missions.Abandon(); Dialog.Hide(); Missions.Refresh(); RefreshCampaign();
                Missions.MissionButtons.First().CallDeferred(Control.MethodName.GrabFocus);
            }, CloseDialog, "Abandon Mission");
        Hangar.BackRequested += ShowOffice;
        Hangar.WarriorDetailsRequested += ShowWarriors;
        Hangar.AssignmentRequested += AssignWarrior;
        Hangar.DeploymentRequested += ShowDeployment;
        Tactical.Closed += () =>
        {
            _header.Show(); Hangar.Show();
            Hangar.DeploymentButton.CallDeferred(Control.MethodName.GrabFocus);
            DisplayServer.WindowSetTitle("Mech · Mech Hangar");
        };
        Drafting.Completed += () =>
        {
            State.EstablishCompany(Drafting.Draft); Drafting.Hide(); ShowOffice(); RefreshCampaign();
        };
        Drafting.AbandonRequested += () => Confirm("Abandon initial draft?", "Your company name and draft selections will be discarded. Your manual save will be preserved.", ShowMainMenu, CloseDialog, "Abandon draft");
        Office.MapRequested += ShowMap;
        Office.DayRequested += () => StartAdvancing(false);
        Office.EventRequested += () => StartAdvancing(true);
        Office.FinancesRequested += ShowFinances;
        Office.FilingRequested += ShowFiling;
        StarMap.BackRequested += ShowOffice;
        StarMap.TravelChanged += RefreshCampaign;
        foreach (var popup in new[] { Hangar.PilotPicker.GetPopup(), Rolodex.Section.GetPopup(),
            StarMap.Destinations.GetPopup(), StarMap.Search.GetMenu(), Drafting.NameInput.GetMenu() })
            RegisterBackPopup(popup);
        Resized += Layout;
        Layout();
        RefreshCampaign();
        ShowMainMenu();
    }

    private void Layout()
    {
        if (_header is null) return;
        Ui.Place(_header, 0, 0, Size.X, 72);
        Ui.Place(_date, Size.X - 210, 0, 174, 72);
        Ui.Place(_menuButton, Size.X - 350, 16, 124, 40);
        Ui.Place(Office, 0, 72, Size.X, Size.Y - 72);
        Ui.Place(StarMap, 0, 72, Size.X, Size.Y - 72);
        Ui.Place(Hangar, 0, 72, Size.X, Size.Y - 72);
        Ui.Place(Tactical, 0, 0, Size.X, Size.Y);
        Ui.Place(Drafting, 0, 0, Size.X, Size.Y);
        Ui.Place(MainMenu, 0, 0, Size.X, Size.Y);
    }

    public void ShowMap()
    {
        if (State.RequiresDraft || Drafting.Visible || Rolodex.Visible || Missions.Visible || Dialog.Visible) return;
        StopAdvancing();
        _officeReturnFocus = Office.Hotspot;
        Office.Hide();
        Hangar.Hide();
        StarMap.SetCampaign(State);
        StarMap.Show();
        StarMap.Viewport.CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Inner Sphere");
    }

    public void ShowOffice()
    {
        StopAdvancing();
        Tactical.Hide();
        _warriorsFromHangar = false; _warriorsReturnFocus = null;
        MainMenu.Hide();
        Rolodex.Hide(); Missions.Hide(); Hangar.Hide();
        _header.Show();
        StarMap.Viewport.EndDrag();
        StarMap.Hide();
        Office.Show();
        RefreshCampaign();
        if (State.RequiresDraft) { ShowDraft(); return; }
        (_officeReturnFocus ?? Office.Hotspot).CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Command");
    }

    public void ShowMainMenu()
    {
        StopAdvancing();
        Tactical.Hide();
        Dialog.Hide();
        Drafting.Hide(); Rolodex.Hide(); Missions.Hide(); Hangar.Hide();
        StarMap.Viewport.EndDrag();
        Office.Hide();
        StarMap.Hide();
        _header.Hide();
        MainMenu.LoadButton.Disabled = !_store.Exists;
        MainMenu.ResumeButton.Visible = !State.RequiresDraft && !State.Bankrupt;
        MainMenu.Show();
        (MainMenu.ResumeButton.Visible ? MainMenu.ResumeButton : MainMenu.NewButton).CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Mercenary Command");
    }

    public void StartNewCompany()
    {
        State = new Campaign();
        _officeReturnFocus = Office.Hotspot;
        Drafting.Hide();
        Dialog.Hide();
        ShowOffice();
        RefreshCampaign();
    }

    public void StartAdvancing(bool untilEvent)
    {
        if (IsAdvancing) { StopAdvancing(); return; }
        if (!State.CanAdvance || Dialog.Visible || Rolodex.Visible || Missions.Visible || Drafting.Visible || !Office.Visible) return;
        IsAdvancing = true;
        _untilEvent = untilEvent;
        _dayElapsed = 0;
    }

    public void StopAdvancing()
    {
        IsAdvancing = false;
        _dayElapsed = 0;
    }

    private void RegisterBackPopup(PopupMenu popup)
    {
        _backPopups.Add(popup);
        popup.WindowInput += input =>
        {
            if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Right } mouse) return;
            if (mouse.Pressed)
            {
                StopAdvancing(); StarMap.Viewport.EndDrag(); _swallowRightRelease = true;
                popup.Hide();
            }
            else _swallowRightRelease = false;
            popup.SetInputAsHandled();
        };
    }

    private void BackOneLevel()
    {
        var popup = _backPopups.LastOrDefault(item => item.Visible);
        if (popup is not null) popup.Hide();
        else if (Dialog.Visible) Dialog.RequestBack();
        else if (Drafting.Visible) Drafting.HandleEscape();
        else if (Rolodex.Visible) Rolodex.RequestBack();
        else if (Missions.Visible || StarMap.Visible || Hangar.Visible) ShowOffice();
    }

    public override void _Input(InputEvent @event)
    {
        // Tactical right-click is an order. Never send it through campaign back navigation.
        if (Tactical.Visible)
        {
            if (Tactical.HandleKey(@event)) GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right } back)
        {
            if (back.Pressed)
            {
                StopAdvancing(); StarMap.Viewport.EndDrag(); _swallowRightRelease = true;
                BackOneLevel();
                GetViewport().SetInputAsHandled(); return;
            }
            if (_swallowRightRelease)
            {
                _swallowRightRelease = false;
                GetViewport().SetInputAsHandled(); return;
            }
        }
        if (Missions.Visible && !Dialog.Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            ShowOffice(); GetViewport().SetInputAsHandled(); return;
        }
        // LineEdit consumes Escape during GUI input, so route the draft's exit before that phase.
        if (Drafting.Visible && !Dialog.Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Drafting.HandleEscape(); GetViewport().SetInputAsHandled(); return;
        }
        if (!IsAdvancing || @event is not InputEventMouseButton { Pressed: true } mouse
            || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right or MouseButton.Middle)) return;
        StopAdvancing();
        if (Office.Visible && !Dialog.Visible && (Office.CalendarButton.GetGlobalRect().HasPoint(mouse.Position)
            || Office.ReminderButton.GetGlobalRect().HasPoint(mouse.Position)))
            GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!IsAdvancing) return;
        _dayElapsed += delta;
        if (_dayElapsed < .1) return;
        _dayElapsed -= .1;
        var result = State.AdvanceOneDay();
        RefreshCampaign();
        if (!_untilEvent || result.QuarterReached || result.Events.Count > 0 || !result.Advanced) StopAdvancing();
        if (State.Bankrupt) ShowBankruptcy();
        else PresentEvents(result.Events);
    }

    private void RefreshCampaign()
    {
        _date.Text = $"●   {State.DateText}";
        Office.RefreshCampaign(State);
        StarMap.SetCampaign(State);
    }

    private static string Money(long amount) => $"{amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} C-bills";

    public void ShowFinances()
    {
        if (State.RequiresDraft || Drafting.Visible) return;
        StopAdvancing();
        _dialogReturnFocus = Office.SafeButton;
        var body = $"Company cash: {Money(State.Cash)}\n\nQuarterly company charge: {Money(Campaign.QuarterlyCharge)}\nTransit ({(State.PaymentPending ? State.BilledTransitDays : State.AccruedTransitDays)} days × {Money(Campaign.TransitDailyCharge)}): {Money(State.PaymentPending ? State.BilledTransitCost : State.AccruedTransitCost)}\nDeferred debt: {Money(State.DeferredDebt)}\nPayment due now: {Money(State.PaymentDue)}\nNext quarter: {State.NextQuarter:dd MMM yyyy}";
        if (!State.PaymentPending) body += $"\nNext bill: {Money(State.ProjectedBill)}";
        else if (!State.CanPay) body += "\n\nInsufficient cash to pay the bill in full.";
        else if (State.DeferredDebt > 0) body += "\n\nBoth quarters must be paid in full. No further deferral is available.";
        var actions = new List<DialogAction> { new("Close", CloseDialog) };
        if (State.PaymentPending)
        {
            if (State.CanDefer) actions.Add(new("Pay Later", ConfirmDeferral));
            actions.Add(new("Pay Now", PayNow, !State.CanPay));
        }
        Dialog.Open("Quarterly finances", body, CloseDialog, actions.ToArray());
    }

    private void PayNow()
    {
        var events = State.PayNow();
        RefreshCampaign();
        CloseDialog();
        PresentEvents(events);
    }

    private void ConfirmDeferral() => Confirm("Defer quarterly payment?",
        $"The full {Money(State.PaymentDue)} bill will remain unpaid.\n\nAt the start of the next quarter, you must pay this debt and the new quarterly bill in full. Failure to do so will result in bankruptcy.\n\nNo cash will be collected now.",
        () =>
        {
            var events = State.DeferPayment();
            RefreshCampaign();
            CloseDialog();
            PresentEvents(events);
        }, ShowFinances, "Pay Later");

    private void PresentEvents(IReadOnlyList<CampaignEvent> events)
    {
        if (events.Count == 0) return;
        StopAdvancing();
        if (!Dialog.Visible) _dialogReturnFocus = GetViewport().GuiGetFocusOwner();
        foreach (var item in events) EventReached?.Invoke(item);
        Dialog.Open("Scheduled event", string.Join("\n", events.Select(item => item.Title)), CloseDialog,
            new DialogAction("Continue", CloseDialog));
    }

    private void ShowBankruptcy()
    {
        StopAdvancing();
        Dialog.Open("Company bankrupt",
            $"The company cannot cover its deferred debt and the new quarterly bill.\n\nCash: {Money(State.Cash)}\nRequired: {Money(State.PaymentDue)}\n\nThe company has been dissolved. Your manual save has been preserved.",
            ShowMainMenu, new DialogAction("Main Menu", ShowMainMenu));
    }

    public void ShowFiling()
    {
        if (State.RequiresDraft || Drafting.Visible) return;
        StopAdvancing();
        _dialogReturnFocus = Office.FilingButton;
        Dialog.Open("Campaign files", _store.Exists
            ? "One manual campaign save is available. Saving replaces it; loading restores it."
            : "No campaign has been saved yet. Save your company to resume it later.", CloseDialog,
            new DialogAction("Close", CloseDialog),
            new DialogAction("Load Game", () => Confirm("Load saved company?", "Current unsaved progress will be discarded.", LoadGame, ShowFiling, "Load Game"), !_store.Exists),
            new DialogAction("Save Game", () =>
            {
                if (_store.Exists) Confirm("Overwrite campaign save?", "This will replace your previous manual save with the current company.", SaveGame, ShowFiling, "Save Game");
                else SaveGame();
            }));
    }

    private void SaveGame()
    {
        try
        {
            _store.Save(State);
            Dialog.Open("Company saved", $"{State.DateText}\n{Money(State.Cash)}\n\nYour campaign can now be loaded from the filing cabinet or main menu.", CloseDialog,
                new DialogAction("Close", CloseDialog));
        }
        catch (Exception error) when (IsStorageError(error))
        {
            Dialog.Open("Unable to save", "The campaign could not be written. Check available disk space and storage access, then try again.", ShowFiling,
                new DialogAction("Back", ShowFiling));
        }
    }

    private void LoadGame()
    {
        try
        {
            var restored = _store.Load();
            StopAdvancing();
            State = restored;
            Dialog.Hide();
            ShowOffice();
            RefreshCampaign();
            if (State.Bankrupt) ShowBankruptcy();
        }
        catch (Exception error) when (IsStorageError(error))
        {
            Dialog.Open("Unable to load", "The campaign save is missing, unreadable, or invalid. Your current company has not been changed.", CloseDialog,
                new DialogAction("Close", CloseDialog));
        }
    }

    private static bool IsStorageError(Exception error) => error is System.IO.IOException or UnauthorizedAccessException
        or System.Text.Json.JsonException or ArgumentException or NotSupportedException;

    private void Confirm(string title, string message, Action accept, Action cancel, string acceptText = "Confirm")
    {
        StopAdvancing();
        if (!Dialog.Visible) _dialogReturnFocus = GetViewport().GuiGetFocusOwner();
        Dialog.Open(title, message, cancel, new DialogAction("Cancel", cancel), new DialogAction(acceptText, accept));
    }

    private void CloseDialog()
    {
        Dialog.Hide();
        if (_dialogReturnFocus is not null && _dialogReturnFocus.IsVisibleInTree()) _dialogReturnFocus.CallDeferred(Control.MethodName.GrabFocus);
        else if (MainMenu.Visible) MainMenu.NewButton.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void ShowDraft()
    {
        StopAdvancing(); _header.Hide(); MainMenu.Hide(); StarMap.Hide(); Hangar.Hide(); Rolodex.Hide(); Missions.Hide();
        if (!Drafting.Visible) Drafting.Begin();
        DisplayServer.WindowSetTitle("Mech · Establish Company");
    }

    public void ShowMissions()
    {
        if (State.RequiresDraft || Dialog.Visible || Rolodex.Visible || !Office.Visible) return;
        StopAdvancing(); _officeReturnFocus = Office.PhoneButton;
        Missions.Open(State.Missions); RefreshCampaign();
    }

    public void ShowWarriors()
    {
        if (State.RequiresDraft || Dialog.Visible || Missions.Visible || Rolodex.Visible) return;
        StopAdvancing();
        if (Hangar.Visible)
        {
            _warriorsFromHangar = true; _warriorsReturnFocus = Hangar.WarriorsButton;
            Rolodex.Open(State.Roster!, "Back to hangar");
        }
        else
        {
            ShowOffice(); _officeReturnFocus = Office.RolodexButton; Rolodex.Open(State.Roster!);
        }
    }

    private void CloseWarriors()
    {
        Rolodex.Hide();
        if (_warriorsFromHangar && Hangar.Visible)
        {
            _warriorsFromHangar = false;
            _warriorsReturnFocus?.CallDeferred(Control.MethodName.GrabFocus);
            _warriorsReturnFocus = null;
        }
        else ShowOffice();
    }

    public void ShowHangar()
    {
        if (State.RequiresDraft || Dialog.Visible || Missions.Visible) return;
        StopAdvancing(); _officeReturnFocus = Office.HangarButton; Office.Hide(); StarMap.Hide(); Rolodex.Hide(); Missions.Hide(); MainMenu.Hide();
        _header.Show(); Hangar.Open(State.Roster!);
        DisplayServer.WindowSetTitle("Mech · Mech Hangar");
    }

    public void ShowDeployment()
    {
        if (!Hangar.Visible || Dialog.Visible || Rolodex.Visible || State.RequiresDraft || Tactical.Visible) return;
        StopAdvancing(); Hangar.Hide(); _header.Hide();
        Tactical.Open(State.Roster!, Hangar.SelectedMech.Id);
        DisplayServer.WindowSetTitle("Mech · Simulated Deployment");
    }

    private void AssignWarrior(string mechId, string? warriorId)
    {
        var roster = State.Roster!;
        var target = roster.Mechs.Single(item => item.Id == mechId);
        var previous = warriorId is null ? null : roster.Assignment(warriorId);
        void Apply(bool confirmed)
        {
            roster.Assign(mechId, warriorId, confirmed); Dialog.Hide(); Hangar.Refresh(); RefreshCampaign();
            Hangar.PilotPicker.CallDeferred(Control.MethodName.GrabFocus);
        }
        if (previous is not null && previous != target)
        {
            var warrior = roster.Warriors.Single(item => item.Id == warriorId);
            var current = roster.Pilot(target);
            var body = current is null
                ? $"Move {warrior.Spec.DisplayName} from {previous.Spec.DisplayName} to {target.Spec.DisplayName}?\n\n{previous.Spec.DisplayName} will be unassigned."
                : $"Assign {warrior.Spec.DisplayName} to {target.Spec.DisplayName}, and {current.Spec.DisplayName} to {previous.Spec.DisplayName}?";
            Confirm(current is null ? "Transfer MechWarrior?" : "Swap MechWarriors?", body, () => Apply(true), CloseDialog, current is null ? "Transfer" : "Swap");
        }
        else Apply(false);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Tactical.Visible || Dialog.Visible || Drafting.Visible || Rolodex.Visible || Missions.Visible || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (StarMap.Visible || Hangar.Visible) ShowOffice();
        else if (Office.Visible) ShowMainMenu();
        else return;
        GetViewport().SetInputAsHandled();
    }
}
