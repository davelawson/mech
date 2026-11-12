using Godot;

namespace Mech;

public partial class DraftScreen : Control
{
    public event Action? Completed;
    public event Action? AbandonRequested;
    public InitialDraft Draft { get; private set; } = new();
    public LineEdit NameInput { get; private set; } = null!;
    public Button EstablishButton { get; private set; } = null!;
    public Button[,] Choices { get; } = new Button[8, 3];
    private readonly Panel[] _pools = new Panel[8];
    private readonly Label[] _poolTitles = new Label[8];
    private readonly Button[,] _inspect = new Button[8, 3];
    private readonly MechPreview[,] _mechPreviews = new MechPreview[4, 3];
    private readonly TextureRect[,] _portraits = new TextureRect[8, 3];
    private readonly Label[,] _candidateNames = new Label[8, 3];
    private readonly Label[,] _candidateStats = new Label[8, 3];
    private Label _title = null!;
    private Label _progress = null!;
    private Control _preview = null!;
    private PanelContainer _previewPanel = null!;
    private VBoxContainer _previewBody = null!;
    private Control? _previewReturn;

    public override void _Ready()
    {
        var background = new ColorRect { Color = new Color("101713") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); background.MouseFilter = MouseFilterEnum.Ignore; AddChild(background);
        _title = Ui.Label(this, "SELECT INITIAL MECHS AND PILOTS", 24, Ui.Gold);
        NameInput = new LineEdit { PlaceholderText = "Company name", MaxLength = 40, SelectAllOnFocus = true };
        AddChild(NameInput); NameInput.TextChanged += value => { Draft.ForceName = value; Refresh(); };
        for (var pool = 0; pool < 8; pool++)
        {
            var panel = new Panel(); panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line)); AddChild(panel); _pools[pool] = panel;
            _poolTitles[pool] = Ui.Label(panel, DraftCatalog.PoolNames[pool], 13, Ui.Gold);
            _poolTitles[pool].HorizontalAlignment = HorizontalAlignment.Center;
            for (var choice = 0; choice < 3; choice++)
            {
                var p = pool; var c = choice; var index = (pool % 4) * 3 + choice;
                var button = Ui.Button(panel, ""); button.ToggleMode = true;
                button.Pressed += () => { Draft.Select(p, c); Refresh(); };
                Choices[pool, choice] = button;
                var isMech = pool < 4;
                var path = isMech ? DraftCatalog.Mechs[index].Artwork : DraftCatalog.Warriors[index].Artwork;
                _portraits[pool, choice] = RosterUi.Picture(button, path, Vector2.Zero);
                if (isMech)
                {
                    _portraits[pool, choice].Hide();
                    var preview = new MechPreview(); button.AddChild(preview); _mechPreviews[pool, choice] = preview;
                }
                _candidateNames[pool, choice] = Ui.Label(button, isMech ? DraftCatalog.Mechs[index].Chassis : $"“{DraftCatalog.Warriors[index].Callsign}”", 15);
                _candidateNames[pool, choice].ClipText = true;
                _candidateStats[pool, choice] = Ui.Label(button, "", 10, Ui.Muted);
                _candidateStats[pool, choice].VerticalAlignment = VerticalAlignment.Top;
                _candidateStats[pool, choice].ClipText = true;
                _candidateStats[pool, choice].AddThemeConstantOverride("line_spacing", -2);
                var inspect = Ui.Button(panel, "…"); inspect.TooltipText = "Inspect candidate without changing your selection";
                _inspect[pool, choice] = inspect; inspect.Pressed += () => Preview(p, c, inspect);
            }
        }
        _progress = Ui.Label(this, "", 13, Ui.Muted); _progress.HorizontalAlignment = HorizontalAlignment.Right; _progress.ClipText = true;
        EstablishButton = Ui.Button(this, "Establish Company"); EstablishButton.Pressed += () => { if (Draft.Complete && !Waiting) Completed?.Invoke(); };
        CreatePreview(); Resized += Layout; Layout(); Refresh(); Hide();
    }
    private void CreatePreview()
    {
        _preview = new Control(); _preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_preview);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .82f) }; shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _preview.AddChild(shade);
        _previewPanel = new PanelContainer(); _previewPanel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold)); _preview.AddChild(_previewPanel);
        _previewBody = new VBoxContainer(); _previewPanel.AddChild(_previewBody); _preview.Hide();
    }
    /// <summary>The draft's random source; unset, drafts use the shared one. Tests set it so every run drafts the same company.</summary>
    public Random? DraftRandom { get; set; }
    /// <summary>Waiting for the other players to confirm their drafts; the selections are locked.</summary>
    public bool Waiting { get; private set; }
    /// <summary>Begins a draft: a random single-player one, or a co-op player's own (whose draft only names the company if they lead).</summary>
    public void Begin(InitialDraft? draft = null, bool coop = false)
    {
        Draft = draft ?? InitialDraft.CreateRandom(DraftRandom); Waiting = false;
        NameInput.Text = Draft.ForceName; NameInput.Visible = Draft.NamesCompany;
        _title.Text = Draft.NamesCompany ? "SELECT INITIAL MECHS AND PILOTS" : "DRAFT YOUR LANCE";
        EstablishButton.Text = coop ? "Confirm Lance" : "Establish Company";
        _preview.Hide(); RefreshCandidates(); Refresh(); Layout(); Show();
        RosterUi.TrapFocus(this); (Draft.NamesCompany ? (Control)NameInput : EstablishButton).CallDeferred(Control.MethodName.GrabFocus);
    }
    /// <summary>Locks the confirmed draft while the other players finish theirs.</summary>
    public void Wait(string status)
    {
        Waiting = true; _waitingText = status; Refresh();
    }
    private string _waitingText = "";
    public void RefreshArtwork() => RefreshCandidates();
    private void RefreshCandidates()
    {
        for (var pool = 0; pool < 8; pool++) for (var choice = 0; choice < 3; choice++)
        {
            if (pool < 4)
            {
                var spec = Draft.MechCandidate(pool, choice);
                var owned = ForceRoster.Create("Preview", [spec], []).Mechs[0];
                _mechPreviews[pool, choice].Display(owned);
                _candidateNames[pool, choice].Text = spec.Chassis;
                _candidateStats[pool, choice].Text = $"{spec.Variant}\n{spec.Tonnage} tons\n{CombatRules.CompactMovement(spec)}";
                Choices[pool, choice].TooltipText = $"{spec.DisplayName}\n{spec.Role} · Armor {spec.ArmorTotal} · {spec.HeatSinks} heat sinks\n{CombatRules.MovementSummary(spec)}\nSelect this candidate; … opens the complete record.";
            }
            else
            {
                var spec = Draft.WarriorCandidate(pool, choice);
                _portraits[pool, choice].Texture = Ui.LoadTexture(spec.Artwork);
                _candidateNames[pool, choice].Text = $"“{spec.Callsign}”";
                _candidateStats[pool, choice].Text = RosterUi.SkillLines(spec);
                Choices[pool, choice].TooltipText = $"{spec.DisplayName}\n{spec.Experience}\n{RosterUi.SkillLines(spec)}\n{RosterUi.SkillBudgetText(spec)}";
            }
        }
    }
    private void Refresh()
    {
        if (EstablishButton is null) return;
        NameInput.Editable = !Waiting;
        for (var p = 0; p < 8; p++) for (var c = 0; c < 3; c++)
        {
            var chosen = Draft.Choice(p) == c; Choices[p, c].SetPressedNoSignal(chosen); Choices[p, c].Disabled = Waiting;
            Choices[p, c].AddThemeStyleboxOverride("normal", Ui.Box(chosen ? new Color("344431") : Ui.Background, chosen ? Ui.Gold : Ui.Line));
        }
        EstablishButton.Disabled = !Draft.Complete || Waiting;
        _progress.Text = _waitingText; _progress.Visible = Waiting; NameInput.Visible = Draft.NamesCompany && !Waiting;
    }
    private void Preview(int pool, int choice, Control source)
    {
        RosterUi.Clear(_previewBody); _previewReturn = source;
        var index = (pool % 4) * 3 + choice;
        if (pool < 4)
        {
            var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _previewBody.AddChild(row);
            var spec = Draft.MechCandidate(pool, choice);
            var image = new MechPreview { CustomMinimumSize = new Vector2(200, 220), SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(image);
            image.Display(ForceRoster.Create("Preview", [spec], []).Mechs[0]);
            var details = new MechDetails { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(details); details.Display(spec);
        }
        else
        {
            var spec = Draft.WarriorCandidate(pool, choice);
            var image = RosterUi.Picture(_previewBody, spec.Artwork, new Vector2(180, 180)); image.SizeFlagsVertical = SizeFlags.ExpandFill;
            RosterUi.Text(_previewBody, spec.DisplayName, 22, Ui.Gold);
            RosterUi.Text(_previewBody, $"{spec.Experience}\n{RosterUi.SkillLines(spec)}\n{RosterUi.SkillBudgetText(spec)}\nHealthy · Ready for deployment", 16);
        }
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; _previewBody.AddChild(actions);
        var back = Ui.Button(actions, "Back to draft"); back.Pressed += ClosePreview;
        var select = Ui.Button(actions, "Select candidate"); select.Disabled = Waiting;
        select.Pressed += () => { Draft.Select(pool, choice); Refresh(); ClosePreview(); };
        back.CustomMinimumSize = select.CustomMinimumSize = new Vector2(140, 40);
        _preview.Show(); Layout(); RosterUi.TrapFocus(_preview); back.CallDeferred(Control.MethodName.GrabFocus);
    }
    private void ClosePreview() { _preview.Hide(); _previewReturn?.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(this); }
    private void Layout()
    {
        if (_title is null) return;
        // Header: title, the company name and the confirm button; while waiting, the co-op status replaces the locked name.
        var buttonLeft = Size.X - 24 - 170; var nameLeft = buttonLeft - 10 - Math.Clamp(Size.X * .22f, 180, 300);
        Ui.Place(EstablishButton, buttonLeft, 12, 170, 36); Ui.Place(NameInput, nameLeft, 12, buttonLeft - 10 - nameLeft, 36);
        var titleWidth = Math.Min(_title.GetMinimumSize().X + 8, nameLeft - 34);
        Ui.Place(_title, 24, 8, titleWidth, 40);
        Ui.Place(_progress, 24 + titleWidth + 10, 8, buttonLeft - 10 - (24 + titleWidth + 10), 40);
        const float top = 58, bottom = 12;
        var width = (Size.X - 72) / 4; var height = (Size.Y - top - bottom - 8) / 2;
        for (var pool = 0; pool < 8; pool++)
        {
            Ui.Place(_pools[pool], 24 + pool % 4 * (width + 8), top + pool / 4 * (height + 8), width, height);
            Ui.Place(_poolTitles[pool], 10, 1, width - 20, 22);
            var rowHeight = (height - 28) / 3;
            for (var choice = 0; choice < 3; choice++)
            {
                var inspectWidth = Math.Max(28, _inspect[pool, choice].GetCombinedMinimumSize().X);
                var buttonWidth = width - inspectWidth - 16; var y = 24 + choice * rowHeight;
                Ui.Place(Choices[pool, choice], 6, y, buttonWidth, rowHeight - 4);
                Ui.Place(_inspect[pool, choice], width - 6 - inspectWidth, y, inspectWidth, rowHeight - 4);
                // The picture gives way so the longest stat line ("Piloting: Legendary") keeps its room.
                var imageWidth = Math.Clamp(Math.Min(rowHeight - 8, buttonWidth - 20 - 100), 30, 100);
                Ui.Place(_portraits[pool, choice], 3, 2, imageWidth, rowHeight - 8);
                if (pool < 4) Ui.Place(_mechPreviews[pool, choice], 3, 2, imageWidth, rowHeight - 8);
                // Name, then three stat lines, centred in the card when it has room.
                var textTop = Math.Max(1, (rowHeight - 4 - 62) / 2);
                Ui.Place(_candidateNames[pool, choice], imageWidth + 8, textTop, buttonWidth - imageWidth - 12, 20);
                Ui.Place(_candidateStats[pool, choice], imageWidth + 8, textTop + 19, buttonWidth - imageWidth - 12, Math.Max(0, rowHeight - 4 - textTop - 19));
            }
        }
        Ui.Place(_previewPanel, 36, 24, Size.X - 72, Size.Y - 48);
    }
    public void HandleEscape()
    {
        if (_preview.Visible) ClosePreview(); else AbandonRequested?.Invoke();
    }
}
