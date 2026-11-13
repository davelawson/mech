using Godot;

namespace Mech;

public partial class CommandApp : Control
{
    public OfficeScreen Office { get; private set; } = null!;
    public MapScreen StarMap { get; private set; } = null!;
    public MainMenuScreen MainMenu { get; private set; } = null!;
    public CampaignDialog Dialog { get; private set; } = null!;
    public CampaignDialog UnsavedDialog { get; private set; } = null!;
    /// <summary>The grid of save slots, for the filing cabinet, main menu Load Game and resuming a co-op save.</summary>
    public SaveBrowser Saves { get; private set; } = null!;
    /// <summary>Name prompts, load confirmations and errors over the save grid.</summary>
    public CampaignDialog SaveDialog { get; private set; } = null!;
    public DraftScreen Drafting { get; private set; } = null!;
    public HangarScreen Hangar { get; private set; } = null!;
    public CombatScreen Tactical { get; private set; } = null!;
    public RolodexPanel Rolodex { get; private set; } = null!;
    public MissionsPanel Missions { get; private set; } = null!;
    public LobbyScreen Lobby { get; private set; } = null!;
    /// <summary>The co-op session this machine leads, or null.</summary>
    public CoopHost? Host { get; private set; }
    /// <summary>The co-op session this machine has joined, or null.</summary>
    public CoopClient? Client { get; private set; }
    public bool InSession => Host is not null || Client is not null;
    /// <summary>Single-player and the hosting machine lead; a joined player does not.</summary>
    public bool IsLeader => Client is null;
    /// <summary>Opens the leader's connection: a port, and how many other players may join. Tests substitute loopback transports.</summary>
    public Func<int, int, INetTransport> HostTransport { get; set; } = ENetTransport.Host;
    public Func<string, int, INetTransport> JoinTransport { get; set; } = ENetTransport.Connect;
    /// <summary>Asks the router for the internet address other players join at. Tests substitute a fixed answer.</summary>
    public Func<Task<string?>> PublicAddressLookup { get; set; } = HostAddress.PublicAsync;
    private Task<string?>? _addressLookup;
    private string? _publicAddress;
    /// <summary>Asks the router to forward the co-op port while the leader hosts; null when it does not.</summary>
    public Func<int, Task<IDisposable?>> RouterPortOpener { get; set; } = RouterPort.OpenAsync;
    private Task<IDisposable?>? _portOpening;
    private IDisposable? _routerPort;
    private RouterPortState _routerState;
    public Campaign State { get; private set; } = new();
    public bool IsAdvancing { get; private set; }
    /// <summary>The local player's slot. Single-player is a one-slot campaign whose only player is the leader.</summary>
    public int LocalSlot => Client?.Slot ?? Campaign.LeaderSlot;
    public CommandNavigator Navigation { get; private set; } = null!;
    public bool HasUnsavedChanges => IsLeader && !State.RequiresDraft && (_savedSnapshot is null || CampaignData() != _savedSnapshot);
    /// <summary>Holds the <c>solo</c> and <c>co-op</c> save folders.</summary>
    [Export] public string SaveRoot { get; set; } = "user://saves";
    /// <summary>The single save from before save slots; it is moved into the folder of its kind at startup.</summary>
    [Export] public string LegacySavePath { get; set; } = "user://campaign.json";
    public SaveLibrary SoloSaves { get; private set; } = null!;
    public SaveLibrary CoopSaves { get; private set; } = null!;
    /// <summary>Single-player games see only solo saves; a co-op game sees only co-op saves.</summary>
    private SaveLibrary Library => InSession ? CoopSaves : SoloSaves;
    /// <summary>The slot this company was last saved to, loaded or resumed from. Co-op autosaves overwrite it.</summary>
    public string? SlotId { get; private set; }
    public event Action<CampaignEvent>? EventReached;
    private enum SavePurpose { Filing, MenuLoad, HostResume }
    private SavePurpose _savePurpose;
    /// <summary>The leader's name and port while hosting, so loading a co-op save can reopen the lobby.</summary>
    private (string Name, int Port)? _hosting;
    /// <summary>A co-op save to host once the ended session has said goodbye and freed its port.</summary>
    private (string Name, int Port, Campaign Campaign, string Slot)? _pendingHost;
    private double _dayElapsed;
    private bool _untilEvent;
    private string? _savedSnapshot;
    private MissionSettlement? _settlement;

    /// <summary>The stretch mode scales the 1440 × 900 base layout to fit the window; cancel any shrink so smaller windows keep full-size text and their compact layouts.</summary>
    private void ScaleInterface()
    {
        var window = GetWindow(); var size = (Vector2)window.Size; var baseSize = (Vector2)window.ContentScaleSize;
        var fit = Math.Min(size.X / baseSize.X, size.Y / baseSize.Y);
        window.ContentScaleFactor = fit < 1 ? 1 / fit : 1;
    }

    public override void _Ready()
    {
        Theme = Ui.CreateTheme();
        GetWindow().MinSize = new Vector2I(900, 500);
        GetWindow().SizeChanged += ScaleInterface; ScaleInterface();
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestQuit;
        GetWindow().FocusExited += CancelRightDrag;
        Office = GetNode<OfficeScreen>("Office");
        StarMap = GetNode<MapScreen>("StarMap");
        StarMap.TextureChanged += Office.SetWallMap;
        Office.SetWallMap(StarMap.Viewport.Texture);
        var root = ProjectSettings.GlobalizePath(SaveRoot);
        SoloSaves = new SaveLibrary(System.IO.Path.Combine(root, "solo"), coop: false);
        CoopSaves = new SaveLibrary(System.IO.Path.Combine(root, "coop"), coop: true);
        SaveLibrary.ImportLegacy(ProjectSettings.GlobalizePath(LegacySavePath), SoloSaves, CoopSaves);
        Hangar = new HangarScreen { Name = "Hangar" }; AddChild(Hangar);
        Tactical = new CombatScreen { Name = "Tactical" }; AddChild(Tactical);
        Drafting = new DraftScreen { Name = "InitialDraft" }; AddChild(Drafting);
        MainMenu = new MainMenuScreen { Name = "MainMenu" };
        AddChild(MainMenu);
        Rolodex = new RolodexPanel { Name = "Rolodex" }; AddChild(Rolodex);
        Missions = new MissionsPanel { Name = "Missions" }; AddChild(Missions);
        Lobby = new LobbyScreen { Name = "Lobby" }; AddChild(Lobby);
        Dialog = new CampaignDialog { Name = "CampaignDialog" };
        AddChild(Dialog);
        Saves = new SaveBrowser { Name = "SaveSlots" }; AddChild(Saves);
        SaveDialog = new CampaignDialog { Name = "SaveDialog" }; AddChild(SaveDialog);
        UnsavedDialog = new CampaignDialog { Name = "UnsavedChanges" };
        AddChild(UnsavedDialog);
        CreateNavigator();
        SyncSlots();
        MainMenu.NewRequested += () => ConfirmUnsaved("Start a new company?", "New Company", StartNewCompany);
        MainMenu.LoadRequested += () => OpenSaves(SoloSaves, saving: false, tabs: false, SavePurpose.MenuLoad, MainMenu.LoadButton);
        Saves.SlotChosen += ChooseSlot;
        Saves.Closed += CloseSaves;
        MainMenu.QuitRequested += RequestQuit;
        MainMenu.ResumeRequested += ResumeCampaign;
        MainMenu.HostRequested += () => ConfirmUnsaved("Host a co-op campaign?", "Continue", ShowHostLobby);
        MainMenu.JoinRequested += () => ConfirmUnsaved("Join a co-op campaign?", "Continue", ShowJoinLobby);
        Lobby.HostRequested += (name, port, players, resume) =>
        {
            if (!resume) { HostSession(name, port, players, null, null); return; }
            _hosting = (name, port);
            OpenSaves(CoopSaves, saving: false, tabs: false, SavePurpose.HostResume, Lobby.ConfirmButton);
        };
        Lobby.JoinRequested += JoinSession;
        Lobby.ClaimRequested += slot => Client?.Claim(slot);
        Lobby.StartRequested += StartSession;
        Lobby.BackRequested += LeaveLobby;
        Office.MenuRequested += ShowMainMenu;
        Office.WarriorsRequested += ShowWarriors;
        Office.HangarRequested += ShowHangar;
        Rolodex.Closed += CloseWarriors;
        Office.MissionsRequested += ShowMissions;
        Missions.Closed += ShowOffice;
        Missions.AcceptRequested += id => { Execute(new AcceptMissionCommand(id)); ShowOffice(); };
        Missions.AbandonRequested += () => Confirm("Abandon mission?",
            $"Abandon {State.Missions.Current!.Name}?\n\nThere is no penalty, but the mission will be gone for good.",
            () =>
            {
                Execute(new AbandonMissionCommand()); Missions.Refresh(); RefreshCampaign();
                Navigation.Close(CommandScreen.Dialog, Missions.MissionButtons.First());
            }, CloseDialog, "Abandon Mission");
        Hangar.BackRequested += LeaveHangar;
        Hangar.ApplyRequested += ApplyLoadout;
        Hangar.WarriorDetailsRequested += ShowWarriors;
        Hangar.AssignmentRequested += AssignWarrior;
        Hangar.LanceRequested += (mechId, lance) => { Execute(new SetLanceCommand(mechId, lance)); Hangar.Refresh(); RefreshCampaign(); };
        Hangar.RepairRequested += OrderRepair;
        Hangar.CancelRepairRequested += CancelRepair;
        Hangar.DeploymentRequested += ShowDeployment;
        Hangar.QuickLaunchRequested += ShowQuickLaunch;
        Tactical.Closed += CloseTactical;
        Tactical.LaunchRequested += LaunchCoopMission;
        Tactical.ForceChosen += (mechs, ready) => Client?.ChooseDeployment(mechs, ready);
        Tactical.PingRequested += point => { Host?.Ping(point); Client?.Ping(point); };
        Tactical.StopWaitingRequested += StopWaiting;
        Drafting.Completed += CompleteDraft;
        Drafting.AbandonRequested += () =>
        {
            if (InSession) Confirm("Leave co-op game?", IsLeader ? "The co-op game ends for every player." : "You leave the co-op game.", () => EndSession(null), CloseDialog, "Leave");
            else Confirm("Abandon initial draft?", "Your company name and draft selections will be discarded. Your saves are kept.", ShowMainMenu, CloseDialog, "Abandon draft");
        };
        Office.MapRequested += ShowMap;
        Office.DayRequested += () => StartAdvancing(false);
        Office.EventRequested += () => StartAdvancing(true);
        Office.FinancesRequested += ShowFinances;
        Office.FilingRequested += ShowFiling;
        StarMap.BackRequested += ShowOffice;
        StarMap.TravelChanged += () => { Host?.MarkDirty(); RefreshCampaign(); };
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
        if (MainMenu is null) return;
        foreach (var screen in new Control[] { Office, StarMap, Hangar, Tactical, Drafting, MainMenu, Lobby })
            Ui.Place(screen, 0, 0, Size.X, Size.Y);
    }

    private void CreateNavigator()
    {
        Navigation = new CommandNavigator(this, () => { StopAdvancing(); StarMap.Viewport.EndDrag(); });
        Navigation.Register(CommandScreen.MainMenu, MainMenu, "Mech · Mercenary Command", false,
            () => MainMenu.ResumeButton.Visible ? MainMenu.ResumeButton : MainMenu.NewButton, ResumeCampaign);
        Navigation.Register(CommandScreen.Office, Office, "Mech · Command", false, () => Office.Hotspot, ShowMainMenu);
        Navigation.Register(CommandScreen.Map, StarMap, "Mech · Inner Sphere", false, () => StarMap.Viewport, ShowOffice);
        Navigation.Register(CommandScreen.Hangar, Hangar, "Mech · Mech Hangar", false, () => Hangar.BackButton, LeaveHangar,
            Hangar.Preview.Spin, Hangar.Preview.ReleaseSpin, Hangar.RightClick);
        Navigation.Register(CommandScreen.Lobby, Lobby, "Mech · Co-op", false, () => Lobby.SeatsShown ? Lobby.BackButton : Lobby.NameInput, LeaveLobby);
        Navigation.Register(CommandScreen.Draft, Drafting, "Mech · Establish Company", false, () => Drafting.NameInput, Drafting.HandleEscape);
        Navigation.Register(CommandScreen.Tactical, Tactical, "Mech · Simulated Deployment", false, () => Tactical.Battlefield);
        Navigation.Register(CommandScreen.Rolodex, Rolodex, null, true, () => Rolodex.Section, CloseWarriors);
        Navigation.Register(CommandScreen.Missions, Missions, null, true, () => Missions.CloseButton, ShowOffice);
        Control? DialogFocus(CampaignDialog dialog) => dialog.FindChildren("*", "Button", true, false)
            .OfType<Button>().FirstOrDefault(button => !button.Disabled);
        Navigation.Register(CommandScreen.Dialog, Dialog, null, true, () => DialogFocus(Dialog), Dialog.RequestBack);
        Navigation.Register(CommandScreen.Saves, Saves, null, true, () => Saves.FocusTarget, CloseSaves);
        Navigation.Register(CommandScreen.SaveDialog, SaveDialog, null, true, () => SaveDialog.NameInput ?? DialogFocus(SaveDialog), SaveDialog.RequestBack);
        Navigation.Register(CommandScreen.UnsavedDialog, UnsavedDialog, null, true, () => DialogFocus(UnsavedDialog), UnsavedDialog.RequestBack);
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
        MainMenu.LoadButton.Disabled = !SoloSaves.Any();
        MainMenu.HostButton.Visible = MainMenu.JoinButton.Visible = !InSession;
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
        EndSession(null, menu: false);
        State = new Campaign();
        _savedSnapshot = null; SlotId = null;
        Navigation.ClearResume();
        ShowDraft(restart: true);
        RefreshCampaign();
    }

    public void StartAdvancing(bool untilEvent)
    {
        if (IsAdvancing) { StopAdvancing(); return; }
        if (!State.CanAdvance || Navigation.CurrentScreen != CommandScreen.Office) return;
        if (!IsLeader || Host is { AllPresent: false })
        {
            Navigation.Push(CommandScreen.Dialog, Office.CalendarButton);
            OpenDialog("Time cannot advance", !IsLeader ? "Only the leader advances time."
                : $"Time cannot advance until {string.Join(" and ", Host!.Seats.Where(seat => !seat.Present).Select(seat => seat.Name))} reconnects.", CloseDialog,
                new DialogAction("Close", CloseDialog));
            return;
        }
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
        if (Navigation.CurrentScreen == CommandScreen.Hangar && Hangar.CancelDrag(@event) || Navigation.HandleBackInput(@event))
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
        // Before GUI input, so Space toggles the panels instead of pressing the focused button.
        if (Navigation.CurrentScreen == CommandScreen.Hangar && Hangar.HandleKey(@event))
        {
            GetViewport().SetInputAsHandled();
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
        Host?.Update(delta); Client?.Update();
        if (_addressLookup is { IsCompleted: true } lookup)
        {
            _publicAddress = lookup.IsCompletedSuccessfully ? lookup.Result : null; _addressLookup = null;
            ShowAddress();
        }
        if (_portOpening is { IsCompleted: true } opening)
        {
            var opened = opening.IsCompletedSuccessfully ? opening.Result : null; _portOpening = null;
            // A session that ended while the router was asked leaves nothing forwarded.
            if (Host is null) opened?.Dispose();
            else { _routerPort = opened; _routerState = opened is null ? RouterPortState.Failed : RouterPortState.Opened; }
            ShowAddress();
        }
        foreach (var closing in _closing) closing.Update(delta);
        _closing.RemoveAll(closing => closing.Finished);
        if (_pendingHost is { } pending && _closing.Count == 0)
        {
            _pendingHost = null;
            if (Navigation.Contains(CommandScreen.Lobby) && !InSession) HostSession(pending.Name, pending.Port, pending.Campaign.Players, pending.Campaign, pending.Slot);
        }
        if (!IsAdvancing) return;
        _dayElapsed += delta;
        if (_dayElapsed < .1) return;
        _dayElapsed -= .1;
        var result = Execute(new AdvanceDayCommand());
        RefreshCampaign();
        if (!_untilEvent || result.QuarterReached || result.Events.Count > 0 || !result.Advanced) StopAdvancing();
        if (State.Bankrupt) ShowBankruptcy();
        else PresentEvents(result.Events);
    }

    private void RefreshCampaign()
    {
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
        if (State.PaymentPending && !IsLeader) actions.Add(new("Waiting for leader", () => { }, true));
        else if (State.PaymentPending)
        {
            if (State.CanDefer) actions.Add(new("Pay Later", ConfirmDeferral));
            actions.Add(new("Pay Now", PayNow, !State.CanPay));
        }
        Navigation.Push(CommandScreen.Dialog, Office.SafeButton);
        OpenDialog("Quarterly finances", body, CloseDialog, actions.ToArray());
    }

    private void PayNow()
    {
        var events = Execute(new PayNowCommand());
        RefreshCampaign();
        CloseDialog();
        PresentEvents(events);
    }

    private void ConfirmDeferral() => Confirm("Defer quarterly payment?",
        $"The full {Money(State.PaymentDue)} bill will remain unpaid.\n\nAt the start of the next quarter, you must pay this debt and the new quarterly bill in full. Failure to do so will result in bankruptcy.\n\nNo cash will be collected now.",
        () =>
        {
            var events = Execute(new DeferPaymentCommand());
            RefreshCampaign();
            CloseDialog();
            PresentEvents(events);
        }, ShowFinances, "Pay Later");

    private void PresentEvents(IReadOnlyList<CampaignEvent> events)
    {
        if (events.Count == 0) return;
        StopAdvancing();
        Host?.Broadcast("Scheduled event", string.Join("\n", events.Select(item => item.Title)));
        foreach (var item in events) EventReached?.Invoke(item);
        OpenDialog("Scheduled event", string.Join("\n", events.Select(item => item.Title)), CloseDialog,
            new DialogAction("Continue", CloseDialog));
    }

    private void ShowBankruptcy()
    {
        StopAdvancing();
        OpenDialog("Company bankrupt",
            $"The company cannot cover its deferred debt and the new quarterly bill.\n\nCash: {Money(State.Cash)}\nRequired: {Money(State.PaymentDue)}\n\nThe company has been dissolved. Your saves are kept.",
            LeaveBankruptCompany, new DialogAction("Main Menu", LeaveBankruptCompany));
        Host?.Flush();
    }
    private void LeaveBankruptCompany() { EndSession(null, menu: false); ShowMainMenu(); }

    public void ShowFiling()
    {
        if (State.RequiresDraft || Navigation.Contains(CommandScreen.Draft)) return;
        StopAdvancing();
        if (!IsLeader)
        {
            Navigation.Push(CommandScreen.Dialog, Office.FilingButton);
            OpenDialog("Campaign files", "Only the leader saves and loads the campaign.", CloseDialog, new DialogAction("Close", CloseDialog));
            return;
        }
        OpenSaves(Library, saving: true, tabs: true, SavePurpose.Filing, Office.FilingButton);
    }

    private void OpenSaves(SaveLibrary library, bool saving, bool tabs, SavePurpose purpose, Control? returnFocus)
    {
        if (Navigation.Contains(CommandScreen.Saves)) return;
        StopAdvancing();
        _savePurpose = purpose;
        Navigation.Push(CommandScreen.Saves, returnFocus);
        Saves.Open(library, saving, tabs);
    }

    private void CloseSaves()
    {
        Navigation.Close(CommandScreen.Saves);
        if (Navigation.CurrentScreen == CommandScreen.MainMenu) MainMenu.LoadButton.Disabled = !SoloSaves.Any();
    }

    private void ChooseSlot(SaveSlot? slot)
    {
        if (Navigation.CurrentScreen != CommandScreen.Saves) return;
        if (Saves.Saving) PromptSave(slot);
        else if (slot is not null) RequestLoad(slot);
    }

    private void OpenSaveDialog(string heading, string body, params DialogAction[] actions)
    {
        Navigation.Push(CommandScreen.SaveDialog);
        SaveDialog.Open(heading, body, CloseSaveDialog, actions);
    }

    private void CloseSaveDialog() => Navigation.Close(CommandScreen.SaveDialog);

    /// <summary>Names the save before writing it: the empty slot starts with the company name, a saved slot with its own.</summary>
    private void PromptSave(SaveSlot? slot)
    {
        var name = slot?.Info?.Name ?? State.Roster?.ForceName ?? "";
        var body = slot is null ? "Name the new save."
            : slot.Info is { } info ? $"This replaces {info.Name} · {SaveBrowser.DateText(info.Date)}." : "This replaces an unreadable save.";
        Navigation.Push(CommandScreen.SaveDialog);
        SaveDialog.OpenNamed(slot is null ? "Save game" : "Overwrite save?", body, name, CloseSaveDialog,
            new DialogAction("Cancel", CloseSaveDialog), new DialogAction("Save", () => SaveGame(slot?.Id)));
    }

    private void SaveGame(string? id)
    {
        var library = Saves.Library!;
        try
        {
            var name = CampaignStore.CleanName(SaveDialog.NameInput?.Text, State);
            SlotId = library.Save(State, name, id);
            _savedSnapshot = CampaignData();
            CloseSaveDialog();
            Saves.SetMode(saving: true);
            Saves.Status.Text = $"Saved {name}.";
        }
        catch (Exception error) when (IsStorageError(error))
        {
            SaveDialog.Open("Unable to save", "The campaign could not be written. Check available disk space and storage access, then try again.", CloseSaveDialog,
                new DialogAction("Back", CloseSaveDialog));
        }
    }

    /// <summary>Loading asks first only when it would discard unsaved changes, or end a co-op game.</summary>
    private void RequestLoad(SaveSlot slot)
    {
        var name = slot.Info?.Name ?? "this save";
        if (_savePurpose == SavePurpose.HostResume) { ResumeHosting(slot); return; }
        if (InSession)
        {
            OpenSaveDialog("Load co-op save?", $"Loading {name} ends the co-op game for every player and reopens the lobby with its seats."
                + (HasUnsavedChanges ? " Unsaved changes will be discarded." : ""),
                new DialogAction("Cancel", CloseSaveDialog), new DialogAction("Load Game", () => LoadCoop(slot)));
            return;
        }
        if (HasUnsavedChanges)
            OpenSaveDialog($"Load {name}?", "Your company has unsaved changes. Loading will discard them.",
                new DialogAction("Cancel", CloseSaveDialog), new DialogAction("Load Game", () => LoadGame(slot)));
        else LoadGame(slot);
    }

    private void ShowLoadError(Exception error)
    {
        var body = error.Message == SaveLibrary.CoopRefusal ? "The save is a co-op campaign. Resume it from Host Co-op."
            : error.Message == SaveLibrary.SoloRefusal ? "The save is not a co-op campaign."
            : "The save is missing, unreadable, or invalid. Your current company has not been changed.";
        if (Navigation.Contains(CommandScreen.SaveDialog)) SaveDialog.Open("Unable to load", body, CloseSaveDialog, new DialogAction("Close", CloseSaveDialog));
        else OpenSaveDialog("Unable to load", body, new DialogAction("Close", CloseSaveDialog));
    }

    private void LoadGame(SaveSlot slot)
    {
        Campaign restored;
        try { restored = SoloSaves.Load(slot.Id); }
        catch (Exception error) when (IsStorageError(error)) { ShowLoadError(error); return; }
        EndSession(null, menu: false);
        StopAdvancing();
        State = restored;
        SlotId = slot.Id;
        _savedSnapshot = CampaignData();
        Navigation.ClearResume();
        ShowOffice();
        RefreshCampaign();
        if (State.Bankrupt) ShowBankruptcy();
    }

    /// <summary>The leader loads a co-op save mid-game: the game ends for everyone and the lobby reopens with the save's seats.</summary>
    private void LoadCoop(SaveSlot slot)
    {
        Campaign restored;
        try { restored = CoopSaves.Load(slot.Id); }
        catch (Exception error) when (IsStorageError(error)) { ShowLoadError(error); return; }
        if (_hosting is not { } hosting) return;
        EndSession(null, farewell: "The leader loaded another save. Join again to take your seat.");
        ShowHostLobby();
        Lobby.NameInput.Text = hosting.Name; Lobby.PortInput.Value = hosting.Port;
        HostWhenFree(hosting.Name, hosting.Port, restored, slot.Id);
    }

    /// <summary>Host Co-op's resume: the chosen co-op save is hosted with the name and port from the form.</summary>
    private void ResumeHosting(SaveSlot slot)
    {
        if (_hosting is not { } hosting) return;
        Campaign restored;
        try { restored = CoopSaves.Load(slot.Id); }
        catch (Exception error) when (IsStorageError(error)) { ShowLoadError(error); return; }
        CloseSaves();
        HostWhenFree(hosting.Name, hosting.Port, restored, slot.Id);
    }

    /// <summary>A session that is still saying goodbye holds the port; the new one opens once it is closed.</summary>
    private void HostWhenFree(string name, int port, Campaign campaign, string slot)
    {
        if (_closing.Count == 0) { HostSession(name, port, campaign.Players, campaign, slot); return; }
        _pendingHost = (name, port, campaign, slot);
        Lobby.ShowStatus("Ending the previous game…");
    }

    private string CampaignData() => System.Text.Json.JsonSerializer.Serialize(State.Snapshot());

    public void RequestQuit() => ConfirmUnsaved("Quit game?", "Quit Without Saving", () => GetTree().Quit());

    private void ConfirmUnsaved(string title, string actionText, Action accept)
    {
        if (Navigation.Contains(CommandScreen.UnsavedDialog)) return;
        if (!HasUnsavedChanges && !InSession) { accept(); return; }
        StopAdvancing();
        StarMap.Viewport.EndDrag();
        Navigation.Push(CommandScreen.UnsavedDialog);
        void Cancel() => Navigation.Close(CommandScreen.UnsavedDialog);
        var body = !InSession ? "Your company has unsaved changes. Continuing will discard them. Your saves are kept."
            : !IsLeader ? "Continuing leaves the co-op game." : HasUnsavedChanges ? "Continuing ends the co-op game for every player and discards unsaved changes. Your saves are kept."
            : "Continuing ends the co-op game for every player.";
        UnsavedDialog.Open(title, body, Cancel,
            new DialogAction("Cancel", Cancel), new DialogAction(actionText, () => { Navigation.Close(CommandScreen.UnsavedDialog); accept(); }));
    }

    public override void _ExitTree()
    {
        GetWindow().CloseRequested -= RequestQuit; GetWindow().FocusExited -= CancelRightDrag;
        Host?.Shutdown(LeaderLeft); Host?.Dispose(); Client?.Dispose(); CloseRouterPort();
        foreach (var closing in _closing) closing.Dispose();
    }
    private void CancelRightDrag() => Navigation?.CancelRightDrag();

    private static bool IsStorageError(Exception error) => error is System.IO.IOException or UnauthorizedAccessException
        or System.Text.Json.JsonException or ArgumentException or NotSupportedException;

    /// <summary>Sends a command as the local player: a joined player's go to the leader, which may refuse them.</summary>
    private void Execute(CampaignCommand command)
    {
        if (Client is not null) { Client.Send(command); return; }
        State.Execute(command, LocalSlot); Host?.MarkDirty();
    }
    /// <summary>Commands whose result is shown are the leader's alone.</summary>
    private TResult Execute<TResult>(CampaignCommand<TResult> command)
    {
        if (Client is not null) throw new InvalidOperationException("Only the leader can do that.");
        var result = State.Execute(command, LocalSlot); Host?.MarkDirty();
        return result;
    }

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
        Missions.Open(State); RefreshCampaign();
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
        Hangar.Campaign = State;
        Hangar.Open(State.Roster!, State.Travel.InOrbit ? State.Travel.OrbitSystemId : null, State.Cash);
        UpdateQuickLaunch();
    }

    private void UpdateQuickLaunch() => Hangar.QuickLaunchBlock = Client is not null ? "Only the leader launches missions."
        : Host is not null ? Host.DeploymentProblem
        : State.Missions.Current is null ? "Accept a mission from the office phone first."
        : !State.CanAdvance ? "Settle the quarterly payment first." : null;

    /// <summary>Deploys to the current mission wherever the DropShip is. Its result is settled into the campaign when the battle ends.</summary>
    public void ShowQuickLaunch()
    {
        if (State.RequiresDraft || Navigation.CurrentScreen != CommandScreen.Hangar || State.Missions.Current is not { } mission
            || Hangar.HasPendingChanges || !State.CanAdvance) return;
        _settlement = null;
        if (Host is not null)
        {
            // Every player is pulled into force selection; the leader launches once they are all ready.
            if (Host.DeploymentProblem is not null) return;
            Host.OpenDeployment();
            Navigation.Push(CommandScreen.Tactical, Hangar.QuickLaunchButton);
            Tactical.OpenMission(State.Roster!, mission, Hangar.SelectedMech.Id, SettleMission, coop: true);
            Tactical.ShowDeployment(Host.Deployment!, Host.OthersReady); RefreshWaiting();
            return;
        }
        Navigation.Push(CommandScreen.Tactical, Hangar.QuickLaunchButton);
        Tactical.OpenMission(State.Roster!, mission, Hangar.SelectedMech.Id, SettleMission);
    }

    /// <summary>The leader launches a co-op mission with every player's force, saving the campaign as the battle begins.</summary>
    private void LaunchCoopMission(string[] force, int terrainSeed)
    {
        if (Host is null) return;
        CombatSimulation battle;
        try { battle = Host.Launch(force, terrainSeed); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { Tactical.DeploymentStatus.Text = error.Message; return; }
        Autosave();
        Tactical.Begin(battle); RefreshWaiting();
    }

    private string? SafeName(string id)
    {
        try { return CoopSaves.Info(id).Name; }
        catch (Exception error) when (IsStorageError(error)) { return null; }
    }

    /// <summary>A co-op campaign is saved on the leader's machine when a mission starts and when it ends, over its own slot.</summary>
    private void Autosave()
    {
        if (Host is null) return;
        try { SlotId = CoopSaves.Save(State, SlotId is null ? null : SafeName(SlotId), SlotId); _savedSnapshot = CampaignData(); }
        catch (Exception error) when (IsStorageError(error)) { }
    }

    /// <summary>A joined player is pulled from wherever they are into the mission's force selection, by way of the hangar.</summary>
    private void JoinDeployment()
    {
        if (Client is null || State.Roster is null || State.Missions.Current is not { } mission || Navigation.Contains(CommandScreen.Tactical) && Tactical.Coop) return;
        if (Navigation.Contains(CommandScreen.Tactical)) Tactical.Close();
        StopAdvancing(); _waitingDialog = false;
        if (Navigation.CurrentScreen != CommandScreen.Hangar)
        {
            Navigation.ClearResume();
            Navigation.NavigateFromOffice(CommandScreen.Hangar, Office.HangarButton);
            Hangar.Campaign = State;
            Hangar.Open(State.Roster, State.Travel.InOrbit ? State.Travel.OrbitSystemId : null, State.Cash);
            UpdateQuickLaunch();
        }
        Navigation.Push(CommandScreen.Tactical, Hangar.BackButton);
        Tactical.OpenMission(State.Roster, mission, Hangar.SelectedMech.Id, null, coop: true);
        Tactical.Orders = Client.Send; RefreshWaiting();
    }

    private string SettleMission(CombatSimulation simulation)
    {
        var text = Settlement(_settlement = Execute(new SettleMissionCommand(simulation.MissionReport())));
        Host?.FinishBattle(text); Autosave();
        return text;
    }

    private string Settlement(MissionSettlement result)
    {
        RefreshCampaign();
        var lines = new List<string> { result.Payment > 0 ? $"Payment received: {Money(result.Payment)}" : "No payment" };
        lines.AddRange(result.Reputation.OrderBy(pair => pair.Key).Select(pair =>
            $"{Factions.Name(pair.Key)} reputation {(pair.Value > 0 ? "+" : "−")}{(Math.Abs(pair.Value) / (double)ReputationTuning.Scale).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}"));
        lines.AddRange(result.Killed.Select(id => $"{State.Roster!.Warriors.Single(item => item.Id == id).Spec.DisplayName} was killed"));
        lines.Add($"Date: {State.DateText}");
        return string.Join("\n", lines);
    }

    private void CloseTactical()
    {
        // The leader leaving force selection or the results takes every player back to the hangar.
        if (Host?.Battle is not null) Host.CloseBattle(); else Host?.CancelDeployment();
        if (Client is not null && Tactical.Coop) { Navigation.Close(CommandScreen.Tactical, Hangar.BackButton); Hangar.Refresh(); UpdateQuickLaunch(); RefreshCompanyDialogs(); return; }
        var settled = _settlement; _settlement = null;
        RefreshCompanyDialogs();
        if (settled is null) { Navigation.Close(CommandScreen.Tactical, Tactical.MissionMode ? Hangar.QuickLaunchButton : Hangar.DeploymentButton); return; }
        // The settled mission is gone, so Quick Launch is disabled and cannot take focus.
        Hangar.Cash = State.Cash; Hangar.Refresh(); UpdateQuickLaunch();
        Navigation.Close(CommandScreen.Tactical, Hangar.BackButton);
        if (State.Bankrupt) ShowBankruptcy();
        else PresentEvents(settled.Day.Events);
    }

    public void ShowDeployment()
    {
        if (State.RequiresDraft || Navigation.CurrentScreen != CommandScreen.Hangar) return;
        Navigation.Push(CommandScreen.Tactical, Hangar.DeploymentButton);
        // Simulations deploy pending loadouts; the hangar keeps them pending for the return.
        Tactical.Reputation = State.Reputation;
        Tactical.Open(Hangar.HasPendingChanges ? Hangar.PendingRoster() : State.Roster!, Hangar.SelectedMech.Id);
    }

    /// <summary>Leaving with unapplied loadout changes asks first; staying keeps them.</summary>
    private void LeaveHangar()
    {
        if (Navigation.CurrentScreen != CommandScreen.Hangar) return;
        if (!Hangar.HasPendingChanges) { ShowOffice(); return; }
        var count = Hangar.PendingMechCount;
        void Stay() { CloseDialog(); Navigation.RestoreFocus(Hangar.BackButton); }
        OpenDialog("Unapplied changes", $"{count} {(count == 1 ? "mech has" : "mechs have")} unapplied loadout changes. Leaving the hangar abandons them, and cancels any pending purchases.", Stay,
            new DialogAction("Stay in hangar", Stay),
            new DialogAction("Abandon changes and leave", () => { Hangar.AbandonPending(); CloseDialog(); ShowOffice(); }));
    }

    /// <summary>Applies the selected mech's pending loadout to the company, paying for its pending purchases, confirming first when it has items marked ⚠.</summary>
    private void ApplyLoadout()
    {
        if (Navigation.CurrentScreen != CommandScreen.Hangar || !Hangar.SelectedCanApply) return;
        void Commit()
        {
            Execute(new ApplyLoadoutCommand(Hangar.SelectedMech.Id, Hangar.SelectedPlan, [.. Hangar.SelectedPurchases]));
            Hangar.Cash = State.Cash; Hangar.CommitSelected();
            if (Navigation.CurrentScreen == CommandScreen.Dialog) CloseDialog();
            Navigation.RestoreFocus(Hangar.BackButton);
        }
        if (Loadout.Suspect(Hangar.DisplayedMech.Spec).Count == 0) { Commit(); return; }
        void Cancel() { CloseDialog(); Navigation.RestoreFocus(Hangar.ApplyButton); }
        OpenDialog("Apply loadout?", "This loadout has items marked ⚠. Apply anyway?", Cancel,
            new DialogAction("Cancel", Cancel), new DialogAction("Apply anyway", Commit));
    }

    private void OrderRepair(string mechId, RepairScope scope)
    {
        if (Navigation.CurrentScreen != CommandScreen.Hangar) return;
        Execute(new OrderRepairCommand(mechId, scope)); AfterRepairChange();
    }

    /// <summary>Cancelling work already started forfeits its payment, so it asks first; a queued order cancels at once.</summary>
    private void CancelRepair(string mechId)
    {
        if (Navigation.CurrentScreen != CommandScreen.Hangar || State.Roster!.Mechs.Single(item => item.Id == mechId).Repair is not { } order) return;
        void Cancel() { Execute(new CancelRepairCommand(mechId)); if (Navigation.CurrentScreen == CommandScreen.Dialog) CloseDialog(); AfterRepairChange(); }
        if (order.Started is null) { Cancel(); return; }
        void Keep() { CloseDialog(); Navigation.RestoreFocus(Hangar.CancelRepairButton); }
        OpenDialog("Cancel repair?", $"Work has started. Cancelling forfeits the {HangarScreen.Money(order.Cost)} paid, and the damage stays.", Keep,
            new DialogAction("Keep repairing", Keep), new DialogAction("Cancel repair", Cancel));
    }

    private void AfterRepairChange()
    {
        Hangar.Cash = State.Cash; Hangar.Refresh(); RefreshCampaign(); UpdateQuickLaunch();
        Navigation.RestoreFocus(Hangar.BackButton);
    }

    private void AssignWarrior(string mechId, string? warriorId)
    {
        var roster = State.Roster!;
        var target = roster.Mechs.Single(item => item.Id == mechId);
        var previous = warriorId is null ? null : roster.Assignment(warriorId);
        void Apply(bool confirmed)
        {
            Execute(new AssignWarriorCommand(mechId, warriorId, confirmed)); Hangar.Refresh(); RefreshCampaign();
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

    private void ShowHostLobby()
    {
        _pendingHost = null;
        Lobby.ShowHost(CoopSaves.Any());
        // The router is asked once per run; a failed answer is asked again on the next visit.
        if (_publicAddress is null && _addressLookup is null) _addressLookup = PublicAddressLookup();
        Navigation.Push(CommandScreen.Lobby, MainMenu.HostButton); Navigation.RestoreFocus(Lobby.NameInput);
        ShowAddress();
    }

    /// <summary>Shows the leader's addresses on the hosting screens.</summary>
    private void ShowAddress()
    {
        if (!Navigation.Contains(CommandScreen.Lobby) || !Lobby.Hosting) return;
        Lobby.ShowAddress(_publicAddress, HostAddress.Local(), looking: _addressLookup is not null, _routerState);
    }

    private void ShowJoinLobby()
    {
        Lobby.ShowJoin();
        Navigation.Push(CommandScreen.Lobby, MainMenu.JoinButton); Navigation.RestoreFocus(Lobby.NameInput);
    }

    /// <summary>Opens the leader's lobby for a new campaign of this many players, or for the saved co-op campaign.</summary>
    private void HostSession(string name, int port, int players, Campaign? resumed, string? slot)
    {
        INetTransport transport;
        try { transport = HostTransport(port, (resumed?.Players ?? players) - 1); }
        catch (IOException error) { Lobby.ShowStatus(error.Message); return; }
        try { Host = new CoopHost(transport, name, players, resumed); }
        catch (ArgumentException error) { transport.Dispose(); Lobby.ShowStatus(error.Message); return; }
        _hosting = (name, port); SlotId = slot;
        Host.Changed += () => { RefreshLobby(); UpdateQuickLaunch(); RefreshWaiting(); if (Drafting.Waiting) Drafting.Wait(DraftWaitText()); };
        Host.CampaignChanged += RefreshAfterSync;
        Host.CompanyEstablished += () => { State = Host.Campaign!; _savedSnapshot = null; SlotId = null; Navigation.ClearResume(); ShowOffice(); SyncSlots(); RefreshCampaign(); };
        Host.PlayerLeft += left =>
        {
            StopAdvancing();
            var body = Host.Phase == LobbyPhase.Playing ? $"{left} disconnected. Time cannot advance until they reconnect." : $"{left} disconnected. Their seat waits for them.";
            Host.Broadcast("Player disconnected", body); Notify("Player disconnected", body);
        };
        Host.DeploymentChanged += () => { if (Host.Deployment is { } seats) Tactical.ShowDeployment(seats, Host.OthersReady); };
        Host.Pinged += Tactical.ShowPing;
        Host.PlayerJoined += joined => { Host.Broadcast("Player reconnected", $"{joined} has joined."); Notify("Player reconnected", $"{joined} has joined."); };
        _routerState = RouterPortState.Opening; _portOpening = RouterPortOpener(port);
        RefreshLobby(); ShowAddress();
    }

    /// <summary>Removes the co-op port's forwarding on the router. One still being asked for is removed once the router answers.</summary>
    private void CloseRouterPort()
    {
        _routerPort?.Dispose(); _routerPort = null; _routerState = RouterPortState.NotHosting;
    }

    private void JoinSession(string name, string address, int port) => JoinSession(name, address, port, null);

    /// <summary>Joins the leader's game, taking <paramref name="claim"/> as soon as it is free when rejoining.</summary>
    private void JoinSession(string name, string address, int port, int? claim)
    {
        try { Client = new CoopClient(JoinTransport(address, port), name); }
        catch (IOException error) { Lobby.ShowStatus(error.Message); return; }
        _joined = (name, address, port);
        var client = Client;
        client.Changed += () =>
        {
            if (claim is { } slot && client.Slot is null && client.Seats.Any(seat => seat.Slot == slot && !seat.Present)) { client.Claim(slot); claim = null; }
            RefreshLobby(); RefreshWaiting();
        };
        Client.DraftOffered += () => { Navigation.Reset(CommandScreen.Draft); Drafting.Begin(Client.Draft, coop: true); };
        Client.CampaignReceived += ReceiveCampaign;
        Client.CommandRefused += reason => Notify("Not possible", reason);
        Client.Notice += Notify;
        // A dropped connection may be rejoined straight to the same seat.
        Client.Ended += reason => EndSession(reason, rejoin: client.LostConnection ? client.Slot : null);
        Client.DeploymentChanged += () =>
        {
            if (Client.Deployment is not { } seats) return;
            JoinDeployment(); Tactical.ShowDeployment(seats, false);
        };
        Client.BattleStarted += () => { JoinDeployment(); Tactical.Begin(Client.Battle!); Tactical.Orders = Client.Send; RefreshWaiting(); };
        Client.BattleSettled += () => Tactical.ShowSettlement(Client.Settlement!);
        Client.BattleClosed += () => { if (Navigation.Contains(CommandScreen.Tactical) && Tactical.Coop) Tactical.Close(); };
        Client.Pinged += Tactical.ShowPing;
        RefreshLobby();
    }

    private void StartSession()
    {
        if (Host is not { CanStart: true }) return;
        Host.Start(Drafting.DraftRandom);
        if (Host.Phase == LobbyPhase.Drafting) { Navigation.Reset(CommandScreen.Draft); Drafting.Begin(Host.LeaderDraft, coop: true); return; }
        State = Host.Campaign!; _savedSnapshot = CampaignData(); Navigation.ClearResume(); SyncSlots(); ShowOffice(); RefreshCampaign();
    }

    private void LeaveLobby()
    {
        if (!Lobby.SeatsShown) { Navigation.Close(CommandScreen.Lobby, Lobby.Hosting ? MainMenu.HostButton : MainMenu.JoinButton); return; }
        EndSession(null);
    }

    private void RefreshLobby()
    {
        if (!Navigation.Contains(CommandScreen.Lobby)) return;
        if (Host is not null)
            Lobby.ShowSeats(Host.Seats, Host.Phase, Campaign.LeaderSlot, Host.CanStart, Host.CanStart ? "Every seat is filled. Start when ready."
                : $"Waiting for players to join on port {Lobby.PortInput.Value:0}…");
        else if (Client is not null)
            Lobby.ShowSeats(Client.Seats, Client.Phase, Client.Slot, false, !Client.Connected ? "Connecting…"
                : Client.Slot is null ? "Take a free seat." : "Waiting for the leader to start…");
    }

    private void CompleteDraft()
    {
        if (Host is not null)
        {
            Host.SubmitLeaderDraft();
            if (Host.Campaign is null) Drafting.Wait(DraftWaitText());
        }
        else if (Client is not null) { Client.SubmitDraft(); Drafting.Wait("Lance confirmed · waiting for the other players to confirm theirs…"); }
        else { State.EstablishCompany(Drafting.Draft); ShowOffice(); RefreshCampaign(); }
    }

    private string DraftWaitText() => Host is null ? "" : $"Lance confirmed · waiting for {string.Join(" and ", Host.Seats.Where(seat => !seat.Drafted).Select(seat => seat.Name ?? ForceRoster.LanceName(seat.Slot)))}…";

    /// <summary>A joined player's newer copy of the campaign: the first one leaves the lobby or draft for the office.</summary>
    private void ReceiveCampaign()
    {
        State = Client!.Campaign!; SyncSlots();
        if (Navigation.Contains(CommandScreen.Lobby) || Navigation.Contains(CommandScreen.Draft)) { Navigation.ClearResume(); ShowOffice(); }
        RefreshAfterSync();
    }

    /// <summary>Shows a campaign changed by another player on every screen that reads it.</summary>
    private void RefreshAfterSync()
    {
        RefreshCampaign();
        Hangar.Campaign = State;
        if (State.Roster is { } roster) Hangar.Rebind(roster, State.Travel.InOrbit ? State.Travel.OrbitSystemId : null, State.Cash);
        UpdateQuickLaunch();
        if (Navigation.CurrentScreen == CommandScreen.Missions) Missions.Refresh();
        RefreshCompanyDialogs();
    }

    private bool _waitingDialog;
    /// <summary>A joined player sees the company's quarterly bill while the leader settles it, and the company's bankruptcy.</summary>
    private void RefreshCompanyDialogs()
    {
        if (IsLeader || State.RequiresDraft || Navigation.Contains(CommandScreen.Tactical) || Navigation.Contains(CommandScreen.MainMenu)) return;
        if (State.Bankrupt) { ShowBankruptcy(); return; }
        if (State.PaymentPending && !_waitingDialog && !Navigation.Contains(CommandScreen.Dialog) && Navigation.CurrentScreen is CommandScreen.Office or CommandScreen.Map or CommandScreen.Hangar)
        { _waitingDialog = true; ShowFinances(); }
        else if (!State.PaymentPending && _waitingDialog) { _waitingDialog = false; if (Navigation.Contains(CommandScreen.Dialog)) CloseDialog(); }
    }

    /// <summary>Tells the local player something, unless they are in a battle.</summary>
    private void Notify(string title, string body)
    {
        if (Navigation.Contains(CommandScreen.Tactical) || Navigation.Contains(CommandScreen.MainMenu) && !Navigation.Contains(CommandScreen.Lobby)) return;
        OpenDialog(title, body, CloseDialog, new DialogAction("Continue", CloseDialog));
    }

    private const string LeaderLeft = "The leader left the game.";
    private (string Name, string Address, int Port)? _joined;
    private readonly List<CoopHost> _closing = [];

    /// <summary>Ends the co-op session. A joined player's copy of the campaign, and a co-op campaign on the leader's machine, go with it.
    /// The leader tells every other player why (<paramref name="farewell"/>). A player whose connection dropped may rejoin their seat.</summary>
    private void EndSession(string? reason, bool menu = true, int? rejoin = null, string farewell = LeaderLeft)
    {
        if (!InSession) return;
        // The ended session lingers until the other players have its farewell.
        if (Host is not null) { Host.Shutdown(farewell); if (!Host.Finished) _closing.Add(Host); }
        Host = null; Client?.Dispose(); Client = null; _waitingDialog = false; CloseRouterPort(); _hosting = null;
        StopAdvancing(); State = new Campaign(); _savedSnapshot = null; SlotId = null; Navigation.ClearResume(); SyncSlots();
        if (!menu) return;
        ShowMainMenu();
        if (reason is null) return;
        void Close() => Navigation.Close(CommandScreen.UnsavedDialog, MainMenu.JoinButton);
        Navigation.Push(CommandScreen.UnsavedDialog);
        if (rejoin is { } slot && _joined is { } joined)
            UnsavedDialog.Open("Co-op game ended", reason + "\n\nRejoin to return to your seat.", Close, new DialogAction("Close", Close),
                new DialogAction("Rejoin", () => { Close(); Rejoin(joined, slot); }));
        else UnsavedDialog.Open("Co-op game ended", reason, Close, new DialogAction("Close", Close));
    }

    /// <summary>Reconnects to the leader as before and retakes the same seat.</summary>
    private void Rejoin((string Name, string Address, int Port) joined, int slot)
    {
        Lobby.ShowJoin();
        Lobby.NameInput.Text = joined.Name; Lobby.AddressInput.Text = joined.Address; Lobby.PortInput.Value = joined.Port;
        Navigation.Push(CommandScreen.Lobby, MainMenu.JoinButton);
        JoinSession(joined.Name, joined.Address, joined.Port, slot);
    }

    /// <summary>Shows, over a co-op mission, who has disconnected. The leader may stop waiting while the battle is fought.</summary>
    private void RefreshWaiting()
    {
        if (!Navigation.Contains(CommandScreen.Tactical) || !Tactical.Coop) return;
        var absent = Host?.Absent ?? Client?.Seats.Where(seat => !seat.Present && seat.Name is not null).Select(seat => seat.Name!).ToArray() ?? [];
        Tactical.ShowWaiting(absent, Host?.Battle is { Outcome: CombatOutcome.Ongoing });
    }

    /// <summary>The leader gives up on a disconnected player: the game ends for everyone, and the campaign resumes from the save
    /// made when the mission launched.</summary>
    private void StopWaiting()
    {
        if (Host is not { Battle: { Outcome: CombatOutcome.Ongoing } } || Host.Absent.Count == 0) return;
        var names = string.Join(" and ", Host.Absent);
        Confirm("Stop waiting?", $"End the co-op game for every player? The battle is lost, and the campaign resumes from the save made when this mission launched: Host Co-op and resume it to fight the mission again.",
            () => EndSession($"You stopped waiting for {names}. Host Co-op and resume the saved campaign to fight the mission again.",
                farewell: $"The leader stopped waiting for {names}. The campaign resumes from the save made when the mission launched."),
            CloseDialog, "Stop Waiting");
    }

    private void SyncSlots()
    {
        StarMap.Slot = Hangar.Slot = Tactical.Slot = LocalSlot;
        Tactical.PlayerNames = State.PlayerNames;
        Missions.LeaderControls = IsLeader;
        Hangar.LanceOwners = State.PlayerNames;
    }
}
