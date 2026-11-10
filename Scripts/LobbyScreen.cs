using Godot;

namespace Mech;

/// <summary>Sets up a co-op session: hosting a new or saved campaign, or joining one; then the seats until the leader starts.</summary>
public partial class LobbyScreen : Control
{
    public event Action<string, int, int, bool>? HostRequested;
    public event Action<string, string, int>? JoinRequested;
    public event Action<int>? ClaimRequested;
    public event Action? StartRequested;
    public event Action? BackRequested;
    public LineEdit NameInput { get; private set; } = null!;
    public LineEdit AddressInput { get; private set; } = null!;
    public SpinBox PortInput { get; private set; } = null!;
    public OptionButton PlayersPicker { get; private set; } = null!;
    /// <summary>Hosting: a new company, or the saved co-op campaign.</summary>
    public OptionButton CampaignPicker { get; private set; } = null!;
    public Button ConfirmButton { get; private set; } = null!;
    public Button StartButton { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Label Status { get; private set; } = null!;
    /// <summary>While hosting: the addresses other players connect to.</summary>
    public Label AddressLabel { get; private set; } = null!;
    /// <summary>Copies the internet address and port, once the router has reported the address.</summary>
    public Button CopyAddressButton { get; private set; } = null!;
    private HBoxContainer _addressInfo = null!;
    private string? _wan;
    private IReadOnlyList<string> _lan = [];
    private bool _looking;
    private RouterPortState _router;
    public List<Button> ClaimButtons { get; } = [];
    public List<Label> SeatLabels { get; } = [];
    public bool Hosting { get; private set; }
    /// <summary>Showing the seats of an open session rather than its setup form.</summary>
    public bool SeatsShown { get; private set; }
    private Label _title = null!;
    private VBoxContainer _form = null!;
    private VBoxContainer _seats = null!;
    private VBoxContainer _box = null!;
    private HBoxContainer _addressRow = null!, _playersRow = null!, _campaignRow = null!;
    private int? _savedPlayers;

    public override void _Ready()
    {
        var background = new ColorRect { Color = Ui.Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        _box = new VBoxContainer(); _box.AddThemeConstantOverride("separation", 14); AddChild(_box);
        _title = Ui.Label(_box, "", 30, Ui.Gold); _title.HorizontalAlignment = HorizontalAlignment.Center;
        _form = new VBoxContainer(); _form.AddThemeConstantOverride("separation", 10); _box.AddChild(_form);
        NameInput = Field(_form, "Your name", new LineEdit { MaxLength = 24, PlaceholderText = "Player name" }, out _);
        AddressInput = Field(_form, "Leader's address", new LineEdit { Text = "127.0.0.1", PlaceholderText = "IP address or host name" }, out _addressRow);
        PortInput = Field(_form, "Port", new SpinBox { MinValue = 1024, MaxValue = 65535, Value = NetProtocol.DefaultPort, Rounded = true }, out _);
        CampaignPicker = Field(_form, "Campaign", new OptionButton(), out _campaignRow);
        CampaignPicker.AddItem("New company"); CampaignPicker.AddItem("Resume saved campaign");
        CampaignPicker.ItemSelected += _ => RefreshForm();
        PlayersPicker = Field(_form, "Players", new OptionButton(), out _playersRow);
        for (var players = 2; players <= ForceRoster.MaxLances; players++) PlayersPicker.AddItem($"{players} players");
        NameInput.TextChanged += _ => RefreshForm();
        _seats = new VBoxContainer(); _seats.AddThemeConstantOverride("separation", 8); _box.AddChild(_seats);
        _addressInfo = new HBoxContainer(); _addressInfo.AddThemeConstantOverride("separation", 10); _box.AddChild(_addressInfo);
        AddressLabel = Ui.Label(_addressInfo, "", 13, Ui.Gold); AddressLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddressLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CopyAddressButton = Ui.Button(_addressInfo, "Copy address");
        CopyAddressButton.Pressed += () => { if (_wan is not null) DisplayServer.ClipboardSet($"{_wan}:{PortInput.Value:0}"); };
        PortInput.ValueChanged += _ => RefreshAddress();
        Status = Ui.Label(_box, "", 13, Ui.Muted); Status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; actions.AddThemeConstantOverride("separation", 10); _box.AddChild(actions);
        BackButton = Ui.Button(actions, "Back"); BackButton.Pressed += () => BackRequested?.Invoke();
        ConfirmButton = Ui.Button(actions, ""); ConfirmButton.Pressed += Confirm;
        StartButton = Ui.Button(actions, "Start Campaign"); StartButton.Pressed += () => { if (!StartButton.Disabled) StartRequested?.Invoke(); };
        foreach (var button in new[] { BackButton, ConfirmButton, StartButton }) button.CustomMinimumSize = new Vector2(150, 42);
        Resized += Layout; Layout(); Hide();
    }

    private static T Field<T>(VBoxContainer parent, string label, T input, out HBoxContainer row) where T : Control
    {
        row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); parent.AddChild(row);
        var text = Ui.Label(row, label, 14, Ui.Gold); text.CustomMinimumSize = new Vector2(150, 0);
        input.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(input);
        return input;
    }

    /// <summary>The hosting form. A saved co-op campaign with this many players may be resumed; null when there is none.</summary>
    public void ShowHost(int? savedPlayers)
    {
        Hosting = true; SeatsShown = false; _savedPlayers = savedPlayers; _title.Text = "HOST CO-OP CAMPAIGN";
        _addressRow.Hide(); _campaignRow.Show(); _playersRow.Show(); RefreshAddress();
        CampaignPicker.SetItemDisabled(1, savedPlayers is null); CampaignPicker.Select(0);
        CampaignPicker.SetItemTooltip(1, savedPlayers is null ? "The saved campaign is not a co-op campaign." : "");
        for (var i = 0; i < PlayersPicker.ItemCount; i++)
        {
            var problem = InitialDraft.CompanyProblem(i + 2);
            PlayersPicker.SetItemDisabled(i, problem is not null); PlayersPicker.SetItemTooltip(i, problem ?? "");
        }
        PlayersPicker.Select(0); ConfirmButton.Text = "Open Lobby";
        Status.Text = "Other players join at one of the addresses above.";
        ShowForm();
    }

    public void ShowJoin()
    {
        Hosting = false; SeatsShown = false; _title.Text = "JOIN CO-OP CAMPAIGN";
        _addressRow.Show(); _campaignRow.Hide(); _playersRow.Hide(); ConfirmButton.Text = "Connect";
        RefreshAddress();
        Status.Text = "Enter the leader's address and port.";
        ShowForm();
    }

    private void ShowForm()
    {
        _form.Show(); _seats.Hide(); ConfirmButton.Show(); StartButton.Hide(); BackButton.Text = "Back";
        RefreshForm(); Show(); NameInput.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void RefreshForm()
    {
        var resume = Hosting && CampaignPicker.Selected == 1;
        if (resume && _savedPlayers is { } players) PlayersPicker.Select(players - 2);
        PlayersPicker.Disabled = resume;
        ConfirmButton.Disabled = !Campaign.ValidPlayerName(NameInput.Text.Trim()) || Hosting && PlayersPicker.IsItemDisabled(PlayersPicker.Selected) && !resume;
    }

    private void Confirm()
    {
        if (ConfirmButton.Disabled) return;
        var name = NameInput.Text.Trim(); var port = (int)PortInput.Value;
        if (Hosting) HostRequested?.Invoke(name, port, PlayersPicker.Selected + 2, CampaignPicker.Selected == 1);
        else JoinRequested?.Invoke(name, AddressInput.Text.Trim(), port);
    }

    /// <summary>Shows the seats. The leader may start once all are filled; a seatless player may claim any free seat.</summary>
    public void ShowSeats(IReadOnlyList<LobbySeat> seats, LobbyPhase phase, int? slot, bool canStart, string status)
    {
        SeatsShown = true; _form.Hide(); ConfirmButton.Hide(); _seats.Show(); BackButton.Text = "Leave";
        _title.Text = Hosting ? "CO-OP LOBBY · LEADER" : "CO-OP LOBBY";
        RosterUi.Clear(_seats); ClaimButtons.Clear(); SeatLabels.Clear();
        foreach (var seat in seats)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); _seats.AddChild(row);
            var who = seat.Present ? seat.Name : seat.Name is null ? "Open" : $"Open · saved for {seat.Name}";
            var label = Ui.Label(row, $"{ForceRoster.LanceName(seat.Slot)}{(seat.Slot == Campaign.LeaderSlot ? " · Leader" : "")} · {who}"
                + (seat.Slot == slot ? " (you)" : "") + (phase == LobbyPhase.Drafting && seat.Present ? seat.Drafted ? " · drafted" : " · drafting" : ""), 16);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill; SeatLabels.Add(label);
            var claim = Ui.Button(row, "Take seat"); claim.Visible = !Hosting && slot is null && !seat.Present && seat.Slot != Campaign.LeaderSlot;
            var index = seat.Slot; claim.Pressed += () => ClaimRequested?.Invoke(index); ClaimButtons.Add(claim);
        }
        StartButton.Visible = Hosting && phase == LobbyPhase.Gathering; StartButton.Disabled = !canStart;
        Status.Text = status; Show();
    }

    public void ShowStatus(string status) => Status.Text = status;

    /// <summary>Shows the leader's addresses: the router's internet address once it answers (null if it did not), and the local
    /// ones; and whether the router forwards the co-op port.</summary>
    public void ShowAddress(string? wan, IReadOnlyList<string> lan, bool looking, RouterPortState router)
    {
        _wan = wan; _lan = lan; _looking = looking; _router = router;
        RefreshAddress();
    }

    private void RefreshAddress()
    {
        _addressInfo.Visible = Hosting;
        AddressLabel.Text = HostAddress.Text(_wan, _lan, (int)PortInput.Value, _looking, _router);
        CopyAddressButton.Disabled = _looking || _wan is null;
    }

    private void Layout()
    {
        if (_box is not null) Ui.Place(_box, (Size.X - 560) / 2, Math.Max(24, (Size.Y - 460) / 2), 560, 460);
    }
}
