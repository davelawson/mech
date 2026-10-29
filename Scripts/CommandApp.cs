using Godot;

namespace Mech;

public partial class CommandApp : Control
{
    public OfficeScreen Office { get; private set; } = null!;
    public MapScreen StarMap { get; private set; } = null!;
    public MainMenuScreen MainMenu { get; private set; } = null!;
    public CampaignDialog Dialog { get; private set; } = null!;
    public CampaignDialog UnsavedDialog { get; private set; } = null!;
    public DraftScreen Drafting { get; private set; } = null!;
    public HangarScreen Hangar { get; private set; } = null!;
    public CombatScreen Tactical { get; private set; } = null!;
    public RolodexPanel Rolodex { get; private set; } = null!;
    public MissionsPanel Missions { get; private set; } = null!;
    public Campaign State { get; private set; } = new();
    public bool IsAdvancing { get; private set; }
    public CommandNavigator Navigation { get; private set; } = null!;
    public bool HasUnsavedChanges => !State.RequiresDraft && (_savedSnapshot is null || CampaignData() != _savedSnapshot);
    [Export] public string SavePath { get; set; } = "user://campaign.json";
    public event Action<CampaignEvent>? EventReached;
    private ColorRect _header = null!;
    private Label _date = null!;
    private Button _menuButton = null!;
    private CampaignStore _store = null!;
    private double _dayElapsed;
    private bool _untilEvent;
    private string? _savedSnapshot;

    public override void _Ready()
    {
        Theme = Ui.CreateTheme();
        GetWindow().MinSize = new Vector2I(900, 500);
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestQuit;
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
        _menuButton.Pressed += ShowMainMenu;
        Hangar = new HangarScreen { Name = "Hangar" }; AddChild(Hangar);
        Tactical = new CombatScreen { Name = "Tactical" }; AddChild(Tactical);
        Drafting = new DraftScreen { Name = "InitialDraft" }; AddChild(Drafting);
        MainMenu = new MainMenuScreen { Name = "MainMenu" };
        AddChild(MainMenu);
        Rolodex = new RolodexPanel { Name = "Rolodex" }; AddChild(Rolodex);
        Missions = new MissionsPanel { Name = "Missions" }; AddChild(Missions);
        Dialog = new CampaignDialog { Name = "CampaignDialog" };
        AddChild(Dialog);
        UnsavedDialog = new CampaignDialog { Name = "UnsavedChanges" };
        AddChild(UnsavedDialog);
        CreateNavigator();
        MainMenu.NewRequested += () => ConfirmUnsaved("Start a new company?", "New Company", StartNewCompany);
        MainMenu.LoadRequested += RequestLoad;
        MainMenu.QuitRequested += RequestQuit;
        MainMenu.ResumeRequested += ResumeCampaign;
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
                State.Missions.Abandon(); Missions.Refresh(); RefreshCampaign();
                Navigation.Close(CommandScreen.Dialog, Missions.MissionButtons.First());
            }, CloseDialog, "Abandon Mission");
        Hangar.BackRequested += ShowOffice;
        Hangar.WarriorDetailsRequested += ShowWarriors;
        Hangar.AssignmentRequested += AssignWarrior;
        Hangar.DeploymentRequested += ShowDeployment;
        Tactical.Closed += () => Navigation.Close(CommandScreen.Tactical, Hangar.DeploymentButton);
        Drafting.Completed += () =>
        {
            State.EstablishCompany(Drafting.Draft); ShowOffice(); RefreshCampaign();
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
            Navigation.RegisterPopup(popup);
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

    private void CreateNavigator()
    {
        Navigation = new CommandNavigator(this, _header, () => { StopAdvancing(); StarMap.Viewport.EndDrag(); });
        Navigation.Register(CommandScreen.MainMenu, MainMenu, "Mech · Mercenary Command", false, false,
            () => MainMenu.ResumeButton.Visible ? MainMenu.ResumeButton : MainMenu.NewButton);
        Navigation.Register(CommandScreen.Office, Office, "Mech · Command", false, true, () => Office.Hotspot, ShowMainMenu);
        Navigation.Register(CommandScreen.Map, StarMap, "Mech · Inner Sphere", false, true, () => StarMap.Viewport, ShowOffice);
        Navigation.Register(CommandScreen.Hangar, Hangar, "Mech · Mech Hangar", false, true, () => Hangar.BackButton, ShowOffice);
        Navigation.Register(CommandScreen.Draft, Drafting, "Mech · Establish Company", false, false, () => Drafting.NameInput, Drafting.HandleEscape);
        Navigation.Register(CommandScreen.Tactical, Tactical, "Mech · Simulated Deployment", false, false, () => Tactical.Battlefield);
        Navigation.Register(CommandScreen.Rolodex, Rolodex, null, true, true, () => Rolodex.Section, CloseWarriors);
        Navigation.Register(CommandScreen.Missions, Missions, null, true, true, () => Missions.CloseButton, ShowOffice);
        Control? DialogFocus(CampaignDialog dialog) => dialog.FindChildren("*", "Button", true, false)
            .OfType<Button>().FirstOrDefault(button => !button.Disabled);
        Navigation.Register(CommandScreen.Dialog, Dialog, null, true, true, () => DialogFocus(Dialog), Dialog.RequestBack);
        Navigation.Register(CommandScreen.UnsavedDialog, UnsavedDialog, null, true, false, () => DialogFocus(UnsavedDialog), UnsavedDialog.RequestBack);
    }

    public void ShowMap()
    {
        if (State.RequiresDraft || Navigation.CurrentScreen is not (CommandScreen.Office or CommandScreen.Map or CommandScreen.Hangar)) return;
        StarMap.SetCampaign(State);
        Navigation.NavigateFromOffice(CommandScreen.Map, Office.Hotspot);
        Navigation.RestoreFocus(StarMap.Viewport);
    }

    public void ShowOffice()
    {
        RefreshCampaign();
        if (State.RequiresDraft) { ShowDraft(); return; }
        Navigation.ReturnToOffice();
    }

    public void ShowMainMenu()
    {
        MainMenu.LoadButton.Disabled = !_store.Exists;
        Navigation.ShowMainMenu(!State.RequiresDraft && !State.Bankrupt && !Navigation.Contains(CommandScreen.Tactical));
        MainMenu.ResumeButton.Visible = Navigation.CanResume;
        Navigation.RestoreFocus();
    }

    private void ResumeCampaign()
    {
        if (State.RequiresDraft || State.Bankrupt) return;
        Navigation.Resume();
    }

    public void StartNewCompany()
    {
        State = new Campaign();
        _savedSnapshot = null;
        Navigation.ClearResume();
        ShowDraft(restart: true);
        RefreshCampaign();
    }

    public void StartAdvancing(bool untilEvent)
    {
        if (IsAdvancing) { StopAdvancing(); return; }
        if (!State.CanAdvance || Navigation.CurrentScreen != CommandScreen.Office) return;
        IsAdvancing = true;
        _untilEvent = untilEvent;
        _dayElapsed = 0;
    }

    public void StopAdvancing()
    {
        IsAdvancing = false;
        _dayElapsed = 0;
    }

    public override void _Input(InputEvent @event)
    {
        // Route Escape before GUI input: LineEdit otherwise consumes it in draft/search fields.
        if (Navigation.HandleBackInput(@event))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (Navigation.CurrentScreen == CommandScreen.UnsavedDialog) return;
        if (Navigation.CurrentScreen == CommandScreen.Tactical)
        {
            if (Tactical.HandleKey(@event)) GetViewport().SetInputAsHandled();
            return;
        }
        if (!IsAdvancing || @event is not InputEventMouseButton { Pressed: true } mouse
            || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right or MouseButton.Middle)) return;
        StopAdvancing();
        if (Navigation.CurrentScreen == CommandScreen.Office && (Office.CalendarButton.GetGlobalRect().HasPoint(mouse.Position)
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
        if (State.RequiresDraft || Navigation.Contains(CommandScreen.Draft)) return;
        StopAdvancing();
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
        Navigation.Push(CommandScreen.Dialog, Office.SafeButton);
        OpenDialog("Quarterly finances", body, CloseDialog, actions.ToArray());
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
        foreach (var item in events) EventReached?.Invoke(item);
        OpenDialog("Scheduled event", string.Join("\n", events.Select(item => item.Title)), CloseDialog,
            new DialogAction("Continue", CloseDialog));
    }

    private void ShowBankruptcy()
    {
        StopAdvancing();
        OpenDialog("Company bankrupt",
            $"The company cannot cover its deferred debt and the new quarterly bill.\n\nCash: {Money(State.Cash)}\nRequired: {Money(State.PaymentDue)}\n\nThe company has been dissolved. Your manual save has been preserved.",
            ShowMainMenu, new DialogAction("Main Menu", ShowMainMenu));
    }

    public void ShowFiling()
    {
        if (State.RequiresDraft || Navigation.Contains(CommandScreen.Draft)) return;
        StopAdvancing();
        Navigation.Push(CommandScreen.Dialog, Office.FilingButton);
        OpenDialog("Campaign files", _store.Exists
            ? "One manual campaign save is available. Saving replaces it; loading restores it."
            : "No campaign has been saved yet. Save your company to resume it later.", CloseDialog,
            new DialogAction("Close", CloseDialog),
            new DialogAction("Load Game", RequestLoad, !_store.Exists),
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
            _savedSnapshot = CampaignData();
            OpenDialog("Company saved", $"{State.DateText}\n{Money(State.Cash)}\n\nYour campaign can now be loaded from the filing cabinet or main menu.", CloseDialog,
                new DialogAction("Close", CloseDialog));
        }
        catch (Exception error) when (IsStorageError(error))
        {
            OpenDialog("Unable to save", "The campaign could not be written. Check available disk space and storage access, then try again.", ShowFiling,
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
            _savedSnapshot = CampaignData();
            Navigation.ClearResume();
            ShowOffice();
            RefreshCampaign();
            if (State.Bankrupt) ShowBankruptcy();
        }
        catch (Exception error) when (IsStorageError(error))
        {
            var errorDialog = Navigation.Contains(CommandScreen.MainMenu) ? UnsavedDialog : Dialog;
            void CloseError()
            {
                if (errorDialog == Dialog) CloseDialog();
                else Navigation.Close(CommandScreen.UnsavedDialog, MainMenu.LoadButton);
            }
            Navigation.Push(errorDialog == Dialog ? CommandScreen.Dialog : CommandScreen.UnsavedDialog);
            errorDialog.Open("Unable to load", "The campaign save is missing, unreadable, or invalid. Your current company has not been changed.", CloseError,
                new DialogAction("Close", CloseError));
        }
    }

    private string CampaignData() => System.Text.Json.JsonSerializer.Serialize(State.Snapshot());

    private void RequestLoad() => ConfirmUnsaved("Load saved company?", "Load Game", LoadGame);

    public void RequestQuit() => ConfirmUnsaved("Quit game?", "Quit Without Saving", () => GetTree().Quit());

    private void ConfirmUnsaved(string title, string actionText, Action accept)
    {
        if (Navigation.Contains(CommandScreen.UnsavedDialog)) return;
        if (!HasUnsavedChanges) { accept(); return; }
        StopAdvancing();
        StarMap.Viewport.EndDrag();
        Navigation.Push(CommandScreen.UnsavedDialog);
        void Cancel() => Navigation.Close(CommandScreen.UnsavedDialog);
        UnsavedDialog.Open(title, "Your company has unsaved changes. Continuing will discard them. Your manual save will be preserved.", Cancel,
            new DialogAction("Cancel", Cancel), new DialogAction(actionText, () => { Navigation.Close(CommandScreen.UnsavedDialog); accept(); }));
    }

    public override void _ExitTree() => GetWindow().CloseRequested -= RequestQuit;

    private static bool IsStorageError(Exception error) => error is System.IO.IOException or UnauthorizedAccessException
        or System.Text.Json.JsonException or ArgumentException or NotSupportedException;

    private void Confirm(string title, string message, Action accept, Action cancel, string acceptText = "Confirm")
    {
        StopAdvancing();
        OpenDialog(title, message, cancel, new DialogAction("Cancel", cancel), new DialogAction(acceptText, accept));
    }

    private void OpenDialog(string title, string body, Action cancel, params DialogAction[] actions)
    {
        Navigation.Push(CommandScreen.Dialog);
        Dialog.Open(title, body, cancel, actions);
    }

    private void CloseDialog() => Navigation.Close(CommandScreen.Dialog);

    private void ShowDraft(bool restart = false)
    {
        var begin = restart || !Navigation.Contains(CommandScreen.Draft);
        Navigation.Reset(CommandScreen.Draft);
        if (begin) Drafting.Begin();
    }

    public void ShowMissions()
    {
        if (State.RequiresDraft || Navigation.CurrentScreen != CommandScreen.Office) return;
        Navigation.Push(CommandScreen.Missions, Office.PhoneButton);
        Missions.Open(State.Missions); RefreshCampaign();
    }

    public void ShowWarriors()
    {
        if (State.RequiresDraft || Navigation.CurrentScreen is not (CommandScreen.Office or CommandScreen.Hangar)) return;
        var fromHangar = Navigation.CurrentScreen == CommandScreen.Hangar;
        if (!fromHangar) RefreshCampaign();
        Navigation.Push(CommandScreen.Rolodex, fromHangar ? Hangar.WarriorsButton : Office.RolodexButton);
        Rolodex.Open(State.Roster!, fromHangar ? "Back to hangar" : "Back to office");
    }

    private void CloseWarriors()
    {
        Navigation.Close(CommandScreen.Rolodex);
        if (Navigation.CurrentScreen == CommandScreen.Office) RefreshCampaign();
    }

    public void ShowHangar()
    {
        if (State.RequiresDraft || Navigation.CurrentScreen is not (CommandScreen.Office or CommandScreen.Map or CommandScreen.Hangar)) return;
        Navigation.NavigateFromOffice(CommandScreen.Hangar, Office.HangarButton);
        Hangar.Open(State.Roster!);
    }

    public void ShowDeployment()
    {
        if (State.RequiresDraft || Navigation.CurrentScreen != CommandScreen.Hangar) return;
        Navigation.Push(CommandScreen.Tactical, Hangar.DeploymentButton);
        Tactical.Open(State.Roster!, Hangar.SelectedMech.Id);
    }

    private void AssignWarrior(string mechId, string? warriorId)
    {
        var roster = State.Roster!;
        var target = roster.Mechs.Single(item => item.Id == mechId);
        var previous = warriorId is null ? null : roster.Assignment(warriorId);
        void Apply(bool confirmed)
        {
            roster.Assign(mechId, warriorId, confirmed); Hangar.Refresh(); RefreshCampaign();
            Navigation.Close(CommandScreen.Dialog, Hangar.PilotPicker);
            Navigation.RestoreFocus(Hangar.PilotPicker);
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
}
