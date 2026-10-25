using Godot;

namespace Mech;

public partial class MapScreen : Control
{
    public event Action<Texture2D?>? TextureChanged;
    public event Action? TravelChanged;
    public Button EmbarkButton { get; private set; } = null!;
    public Button CancelButton { get; private set; } = null!;
    public Label LocationLabel { get; private set; } = null!;
    public Label CandidateLabel { get; private set; } = null!;
    public Label JourneyLabel { get; private set; } = null!;
    public LineEdit Search { get; private set; } = null!;
    public OptionButton Destinations { get; private set; } = null!;
    private readonly List<string> _destinationIds = [];
    private Campaign? _campaign;
    private Panel _travelPanel = null!;
    private ScrollContainer _travelScroll = null!;
    private string? _notice;
    private string? _noticeOrigin;
    [Signal] public delegate void BackRequestedEventHandler();
    [Export(PropertyHint.File, "*.jpg")] public string MapPath { get; set; } = "res://Assets/inner-sphere-3025-starscape.jpg";
    public MapViewport Viewport { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Button ZoomIn { get; private set; } = null!;
    public Button ZoomOut { get; private set; } = null!;
    public Button FitButton { get; private set; } = null!;
    public Button FindShipButton { get; private set; } = null!;
    public Label Status { get; private set; } = null!;
    private Label _zoom = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _hint = null!;
    private LinkButton _source = null!;
    private Panel _controls = null!;

    public override void _Ready()
    {
        BackButton = Ui.Button(this, "←  Back to office");
        BackButton.Pressed += () => EmitSignal(SignalName.BackRequested);
        _subtitle = Ui.Label(this, "STELLAR CARTOGRAPHY", 11, Ui.Muted);
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _title = Ui.Label(this, "The Inner Sphere  ·  3025", 26);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        Viewport = new MapViewport { Name = "MapViewport" };
        AddChild(Viewport);
        Status = Ui.Label(Viewport, "Unfolding star chart…", 16, Ui.Gold);
        Status.HorizontalAlignment = HorizontalAlignment.Center;
        CreateTravelControls();
        Viewport.SystemSelected += SelectSystem;
        _controls = new Panel();
        _controls.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold));
        AddChild(_controls);
        ZoomOut = Ui.Button(_controls, "−");
        ZoomOut.TooltipText = "Zoom out";
        _zoom = Ui.Label(_controls, "100%", 13);
        _zoom.HorizontalAlignment = HorizontalAlignment.Center;
        ZoomIn = Ui.Button(_controls, "+");
        ZoomIn.TooltipText = "Zoom in";
        FitButton = Ui.Button(_controls, "Fit map");
        FindShipButton = Ui.Button(_controls, "Find DropShip");
        FindShipButton.TooltipText = "Center the DropShip without changing zoom";
        FindShipButton.Pressed += Viewport.FindDropShip;
        ZoomIn.Pressed += () => Viewport.ZoomBy(1.35f);
        ZoomOut.Pressed += () => Viewport.ZoomBy(1 / 1.35f);
        FitButton.Pressed += Viewport.Fit;
        Ui.Place(ZoomOut, 6, 6, 38, 36);
        Ui.Place(_zoom, 44, 6, 70, 36);
        Ui.Place(ZoomIn, 114, 6, 38, 36);
        Ui.Place(FitButton, 166, 6, 90, 36);
        Ui.Place(FindShipButton, 268, 6, 130, 36);
        _hint = Ui.Label(this, "Click a system to plot · Drag to pan · Scroll to zoom · Home to fit", 12, Ui.Muted);
        _source = new LinkButton { Text = "Official BattleTech map ↗" };
        _source.AddThemeFontSizeOverride("font_size", 12);
        _source.AddThemeColorOverride("font_color", Ui.Muted);
        _source.Pressed += () => OS.ShellOpen("https://battletech.com/wp-content/uploads/sites/6/2025/03/BattleTech-Inner-Spher-at-War-3025.pdf");
        AddChild(_source);
        Viewport.ViewChanged += RefreshControls;
        Resized += Layout;
        Layout();
        SetMap(MapPath);
    }

    public void SetMap(string path)
    {
        Viewport.SetMap(Ui.LoadTexture(path));
        TextureChanged?.Invoke(Viewport.Texture);
        Status.Text = Viewport.MapLoaded ? "" : "The star chart could not load. Return to the office and check the game assets.";
        Status.Visible = !Viewport.MapLoaded;
        RefreshControls();
    }

    public void SetCampaign(Campaign campaign)
    {
        if (_campaign != campaign || _noticeOrigin != campaign.Travel.OrbitSystemId || !campaign.Travel.InOrbit) _notice = null;
        _noticeOrigin = campaign.Travel.OrbitSystemId;
        _campaign = campaign; Viewport.SetCampaign(campaign); RefreshTravel();
    }

    private Label TravelLabel(VBoxContainer box, string text, Color color, int size = 13)
    {
        var label = Ui.Label(box, text, size, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private void CreateTravelControls()
    {
        _travelPanel = new Panel();
        _travelPanel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line)); AddChild(_travelPanel);
        _travelScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        _travelPanel.AddChild(_travelScroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 10); _travelScroll.AddChild(box);
        TravelLabel(box, "NAVIGATION", Ui.Gold, 15);
        LocationLabel = TravelLabel(box, "", Ui.Text);
        Search = new LineEdit { PlaceholderText = "Find a system…", ClearButtonEnabled = true };
        box.AddChild(Search);
        Destinations = new OptionButton { FitToLongestItem = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(Destinations);
        Search.TextChanged += _ => PopulateDestinations();
        Search.TextSubmitted += _ => { if (_destinationIds.Count == 1) SelectSystem(_destinationIds[0]); };
        Destinations.ItemSelected += index => { if (index > 0) SelectSystem(_destinationIds[(int)index - 1]); };
        CandidateLabel = TravelLabel(box, "", new Color("62b9ff"));
        EmbarkButton = Ui.Button(box, "Embark");
        EmbarkButton.Pressed += () => { _campaign!.Embark(); _notice = null; RefreshTravel(); TravelChanged?.Invoke(); };
        JourneyLabel = TravelLabel(box, "", new Color("61de95"));
        CancelButton = Ui.Button(box, "Cancel Journey");
        CancelButton.Pressed += () => { _campaign!.CancelJourney(); _notice = null; RefreshTravel(); TravelChanged?.Invoke(); };
        TravelLabel(box, "Blue: candidate · Green: embarked\nAdvance time from the office.\nTransit: 2,000 C-bills/day, billed quarterly.", Ui.Muted, 12);
        PopulateDestinations();
    }

    private void PopulateDestinations()
    {
        _destinationIds.Clear(); Destinations.Clear(); Destinations.AddItem("Choose destination");
        foreach (var system in StarChart.Instance.Systems.Where(system => system.Name.Contains(Search.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(system => system.Name, StringComparer.Ordinal).ThenBy(system => system.Id, StringComparer.Ordinal))
        {
            _destinationIds.Add(system.Id); Destinations.AddItem(system.Name);
        }
        Destinations.Select(0);
    }

    public void SelectSystem(string id)
    {
        if (_campaign is null || !_campaign.Travel.InOrbit || _campaign.RequiresDraft || _campaign.Bankrupt) return;
        var found = _campaign.PlotJourney(id);
        _notice = found ? null : id == _campaign.Travel.OrbitSystemId ? "You are already in orbit here."
            : $"No route to {StarChart.Instance[id].Name} using jumps of 30 light-years or less.";
        RefreshTravel(); TravelChanged?.Invoke();
    }

    public void RefreshTravel()
    {
        if (_campaign is null) { EmbarkButton.Hide(); CancelButton.Hide(); return; }
        var travel = _campaign.Travel;
        LocationLabel.Text = travel.LocationText;
        Search.Editable = travel.InOrbit; Destinations.Disabled = !travel.InOrbit;
        CandidateLabel.Text = _notice ?? (travel.Candidate is null ? (travel.InOrbit ? "Select a destination to preview a route." : "Plotting becomes available on arrival.")
            : RouteText("CANDIDATE", travel.Candidate, StarChart.Instance.RouteDays(travel.Candidate)));
        JourneyLabel.Text = travel.Journey is null ? "No embarked journey."
            : RouteText("EMBARKED", travel.Journey.Skip(travel.LegIndex).ToArray(), travel.JourneyDaysRemaining, true)
                + (travel.InOrbit ? "\nDeparture on the next day." : $"\nNext arrival: {_campaign.Date.AddDays(travel.DaysToArrival):dd MMM yyyy}");
        CandidateLabel.TooltipText = travel.Candidate is null ? "" : string.Join(" → ", travel.Candidate.Select(id => StarChart.Instance[id].Name));
        CandidateLabel.MouseFilter = MouseFilterEnum.Pass;
        JourneyLabel.TooltipText = travel.Journey is null ? "" : string.Join(" → ", travel.Journey.Select(id => StarChart.Instance[id].Name));
        JourneyLabel.MouseFilter = MouseFilterEnum.Pass;
        EmbarkButton.Visible = travel.InOrbit && travel.Candidate is not null;
        CancelButton.Visible = travel.InOrbit && travel.HasJourney;
        Viewport.RefreshTravel();
    }

    private static string RouteText(string title, IReadOnlyList<string> route, int transitDays, bool remaining = false)
    {
        var names = route.Select(id => StarChart.Instance[id].Name).ToArray();
        // The complete sequence remains available in the tooltip for long routes.
        var summary = names.Length <= 6 ? string.Join(" → ", names)
            : $"{names[0]} → … → {names[^1]}";
        return $"{title}\n{summary}\n{route.Count - 1} {(route.Count == 2 ? "jump" : "jumps")} · {transitDays} transit {(transitDays == 1 ? "day" : "days")}{(remaining ? " remaining" : "")}\n{transitDays * Campaign.TransitDailyCharge:N0} C-bills{(remaining ? " remaining" : "")} transit cost";
    }

    private void RefreshControls()
    {
        var zoom = Viewport.Transform.Zoom;
        _zoom.Text = $"{MathF.Round(zoom * 100)}%";
        ZoomOut.Disabled = !Viewport.MapLoaded || zoom <= MapTransform.MinimumZoom;
        ZoomIn.Disabled = !Viewport.MapLoaded || zoom >= MapTransform.MaximumZoom;
        FitButton.Disabled = FindShipButton.Disabled = !Viewport.MapLoaded;
    }

    private void Layout()
    {
        if (Viewport is null) return;
        Ui.Place(BackButton, 36, 22, 170, 40);
        Ui.Place(_subtitle, Size.X / 2 - 220, 10, 440, 22);
        Ui.Place(_title, Size.X / 2 - 220, 32, 440, 42);
        Ui.Place(Viewport, 36, 86, Size.X - 356, Size.Y - 136);
        Ui.Place(_travelPanel, Size.X - 300, 86, 264, Size.Y - 136);
        Ui.Place(_travelScroll, 12, 12, 240, _travelPanel.Size.Y - 24);
        Ui.Place(Status, 12, Viewport.Size.Y / 2 - 25, Viewport.Size.X - 24, 50);
        Ui.Place(_controls, 36 + Viewport.Size.X / 2 - 202, Size.Y - 116, 404, 48);
        Ui.Place(_hint, 36, Size.Y - 50, Size.X - 330, 50);
        Ui.Place(_source, Size.X - 260, Size.Y - 42, 224, 34);
    }
}
