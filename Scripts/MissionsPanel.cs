using Godot;
using System.Globalization;

namespace Mech;

public partial class MissionsPanel : Control
{
    public event Action? Closed;
    public event Action<string>? AcceptRequested;
    public event Action? AbandonRequested;
    public Button CloseButton { get; private set; } = null!;
    public Button AcceptButton { get; private set; } = null!;
    public Button AbandonButton { get; private set; } = null!;
    public Label MissionName { get; private set; } = null!;
    public IReadOnlyList<Button> MissionButtons => _missionButtons;
    private readonly List<Button> _missionButtons = [];
    private MissionBoard _board = null!;
    private string? _viewedId;
    private PanelContainer _panel = null!;
    private VBoxContainer _index = null!;
    private Label _status = null!;
    private Label _briefing = null!;
    private Label _terms = null!;
    private Label _acceptHint = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _panel = new PanelContainer(); _panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color("25291f"), Ui.Gold, 2)); AddChild(_panel);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 12); _panel.AddChild(body);
        var top = new HBoxContainer(); body.AddChild(top);
        var heading = Ui.Label(top, "MISSION OFFERS", 22, Ui.Gold); heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CloseButton = Ui.Button(top, "Back to office"); CloseButton.CustomMinimumSize = new Vector2(135, 38);
        CloseButton.Pressed += () => Closed?.Invoke();
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 24); body.AddChild(content);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(205, 0) }; content.AddChild(left);
        _index = RosterUi.Scroll(left);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; right.AddThemeConstantOverride("separation", 10); content.AddChild(right);
        var details = RosterUi.Scroll(right);
        _status = RosterUi.Text(details, "", 12, Ui.Muted);
        MissionName = RosterUi.Text(details, "", 26, Ui.Gold);
        _briefing = RosterUi.Text(details, "", 16);
        _terms = RosterUi.Text(details, "", 15);
        _acceptHint = RosterUi.Text(right, "", 12, Ui.Muted);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; right.AddChild(actions);
        AcceptButton = Ui.Button(actions, "Accept Mission"); AcceptButton.CustomMinimumSize = new Vector2(165, 42);
        AcceptButton.Pressed += () => { if (_viewedId is not null) AcceptRequested?.Invoke(_viewedId); };
        AbandonButton = Ui.Button(actions, "Abandon Mission"); AbandonButton.CustomMinimumSize = new Vector2(165, 42);
        AbandonButton.Pressed += () => AbandonRequested?.Invoke();
        _panel.MinimumSizeChanged += () => Callable.From(Layout).CallDeferred();
        Resized += Layout; Layout(); Hide();
    }

    public void Open(MissionBoard board)
    {
        _board = board;
        _board.MarkOffersSeen();
        _viewedId = board.SelectedMissionId ?? board.Available.FirstOrDefault()?.Id;
        Show(); Refresh();
        (_missionButtons.FirstOrDefault(button => button.ButtonPressed) ?? CloseButton).CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Refresh()
    {
        RosterUi.Clear(_index); _missionButtons.Clear();
        if (_board.Current is { } current)
        {
            RosterUi.Text(_index, "CURRENT MISSION", 12, Ui.Gold);
            AddMission(current);
        }
        RosterUi.Text(_index, "AVAILABLE OFFERS", 12, Ui.Muted);
        foreach (var offer in _board.Available) AddMission(offer);
        if (!_board.Available.Any()) RosterUi.Text(_index, "No other offers available.", 13, Ui.Muted);
        ShowDetails(); Layout();
    }

    private void AddMission(MissionOffer offer)
    {
        var button = Ui.Button(_index, offer.Name); button.CustomMinimumSize = new Vector2(0, 48);
        button.ToggleMode = true; button.ButtonPressed = offer.Id == _viewedId;
        button.Pressed += () =>
        {
            _viewedId = offer.Id;
            foreach (var item in _missionButtons) item.SetPressedNoSignal(item == button);
            ShowDetails();
        };
        _missionButtons.Add(button);
    }

    private void ShowDetails()
    {
        var offer = _board.Offers.SingleOrDefault(item => item.Id == _viewedId);
        var isCurrent = offer is not null && offer.Id == _board.SelectedMissionId;
        _status.Text = isCurrent ? "CURRENT MISSION · ACCEPTED" : "AVAILABLE OFFER";
        MissionName.Text = offer?.Name ?? "No missions available";
        _briefing.Text = offer is null ? "Check the phone again when new offers arrive." : $"{offer.Briefing}\n\nOBJECTIVE\n{offer.Objective}";
        _terms.Text = offer is null ? "" : $"PAYMENT  {offer.Payment.ToString("N0", CultureInfo.InvariantCulture)} C-bills\nDESTINATION  {offer.Destination}\nTRAVEL ESTIMATE  {offer.TravelEstimate}\nDEADLINE  {offer.Deadline}\nEXPECTED OPPOSITION  {offer.Opposition}\nABANDONMENT PENALTY  None";
        AcceptButton.Visible = offer is not null && !isCurrent;
        AcceptButton.Disabled = _board.Current is not null;
        AcceptButton.TooltipText = AcceptButton.Disabled ? "Abandon the current mission before accepting another offer." : "Accept this mission and return to the office.";
        AbandonButton.Visible = isCurrent;
        _acceptHint.Text = isCurrent ? "Mission accepted. You can continue browsing other offers."
            : _board.Current is not null ? "Abandon your current mission before accepting another offer." : "Accept this mission to undertake it.";
        RosterUi.TrapFocus(this);
    }

    private void Layout()
    {
        if (_panel is null) return;
        var width = Math.Min(960, Size.X - 48); var height = Math.Min(600, Size.Y - 48);
        Ui.Place(_panel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
}
