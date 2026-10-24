using Godot;

namespace Mech;

public partial class CommandApp : Control
{
    public OfficeScreen Office { get; private set; } = null!;
    public MapScreen StarMap { get; private set; } = null!;
    public MainMenuScreen MainMenu { get; private set; } = null!;
    public CampaignDialog Dialog { get; private set; } = null!;
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
        MainMenu = new MainMenuScreen { Name = "MainMenu" };
        AddChild(MainMenu);
        Dialog = new CampaignDialog { Name = "CampaignDialog" };
        AddChild(Dialog);
        MainMenu.NewRequested += StartNewCompany;
        MainMenu.LoadRequested += LoadGame;
        Office.MapRequested += ShowMap;
        Office.DayRequested += () => StartAdvancing(false);
        Office.EventRequested += () => StartAdvancing(true);
        Office.FinancesRequested += ShowFinances;
        Office.FilingRequested += ShowFiling;
        StarMap.BackRequested += ShowOffice;
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
        Ui.Place(MainMenu, 0, 0, Size.X, Size.Y);
    }

    public void ShowMap()
    {
        StopAdvancing();
        Office.Hide();
        StarMap.Show();
        StarMap.Viewport.CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Inner Sphere");
    }

    public void ShowOffice()
    {
        StopAdvancing();
        MainMenu.Hide();
        _header.Show();
        StarMap.Viewport.EndDrag();
        StarMap.Hide();
        Office.Show();
        Office.Hotspot.CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Command");
    }

    public void ShowMainMenu()
    {
        StopAdvancing();
        Dialog.Hide();
        StarMap.Viewport.EndDrag();
        Office.Hide();
        StarMap.Hide();
        _header.Hide();
        MainMenu.LoadButton.Disabled = !_store.Exists;
        MainMenu.Show();
        MainMenu.NewButton.CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Mercenary Command");
    }

    public void StartNewCompany()
    {
        State = new Campaign();
        Dialog.Hide();
        ShowOffice();
        RefreshCampaign();
    }

    public void StartAdvancing(bool untilEvent)
    {
        if (IsAdvancing) { StopAdvancing(); return; }
        if (!State.CanAdvance || Dialog.Visible || !Office.Visible) return;
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
    }

    private static string Money(long amount) => $"{amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} C-bills";

    public void ShowFinances()
    {
        StopAdvancing();
        _dialogReturnFocus = Office.SafeButton;
        var body = $"Company cash: {Money(State.Cash)}\n\nQuarterly company charge: {Money(Campaign.QuarterlyCharge)}\nDeferred debt: {Money(State.DeferredDebt)}\nPayment due now: {Money(State.PaymentDue)}\nNext quarter: {State.NextQuarter:dd MMM yyyy}";
        if (!State.PaymentPending) body += $"\nNext bill: {Money(Campaign.QuarterlyCharge + State.DeferredDebt)}";
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

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Dialog.Visible && StarMap.Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            ShowOffice();
            GetViewport().SetInputAsHandled();
        }
    }
}
