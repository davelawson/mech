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
    private readonly TextureRect[,] _portraits = new TextureRect[8, 3];
    private readonly Label[,] _candidateNames = new Label[8, 3];
    private readonly Label[,] _candidateStats = new Label[8, 3];
    private Label _title = null!;
    private Label _progress = null!;
    private Label _instructions = null!;
    private Control _preview = null!;
    private PanelContainer _previewPanel = null!;
    private VBoxContainer _previewBody = null!;
    private Control? _previewReturn;

    public override void _Ready()
    {
        var background = new ColorRect { Color = new Color("101713") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); background.MouseFilter = MouseFilterEnum.Ignore; AddChild(background);
        _title = Ui.Label(this, "ESTABLISH YOUR COMPANY", 24, Ui.Gold);
        _instructions = Ui.Label(this, "Review one selection per pool · Inspect with … · Selections remain editable until confirmation", 12, Ui.Muted);
        NameInput = new LineEdit { PlaceholderText = "Company name", MaxLength = 40, SelectAllOnFocus = true };
        AddChild(NameInput); NameInput.TextChanged += value => { Draft.ForceName = value; Refresh(); };
        for (var pool = 0; pool < 8; pool++)
        {
            var panel = new Panel(); panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line)); AddChild(panel); _pools[pool] = panel;
            _poolTitles[pool] = Ui.Label(panel, DraftCatalog.PoolNames[pool], 13, Ui.Gold);
            for (var choice = 0; choice < 3; choice++)
            {
                var p = pool; var c = choice; var index = (pool % 4) * 3 + choice;
                var button = Ui.Button(panel, ""); button.ToggleMode = true;
                button.Pressed += () => { Draft.Select(p, c); Refresh(); };
                Choices[pool, choice] = button;
                var isMech = pool < 4;
                var path = isMech ? DraftCatalog.Mechs[index].Artwork : DraftCatalog.Warriors[index].Artwork;
                _portraits[pool, choice] = RosterUi.Picture(button, path, Vector2.Zero);
                _candidateNames[pool, choice] = Ui.Label(button, isMech ? DraftCatalog.Mechs[index].Chassis : $"“{DraftCatalog.Warriors[index].Callsign}”", 15);
                _candidateNames[pool, choice].ClipText = true;
                var spec = isMech ? DraftCatalog.Mechs[index] : null;
                _candidateStats[pool, choice] = Ui.Label(button, spec is not null ? $"{spec.Variant} · {spec.Walk}/{spec.Run}/{spec.Jump}" : $"G {DraftCatalog.Warriors[index].Gunnery}  /  P {DraftCatalog.Warriors[index].Piloting}", 11, Ui.Muted);
                _candidateStats[pool, choice].ClipText = true;
                button.TooltipText = spec is not null ? $"{spec.DisplayName}\n{spec.Role} · Armor {spec.ArmorTotal} · {spec.HeatSinks} heat sinks\nSelect this candidate; … opens the complete record." : $"{DraftCatalog.Warriors[index].DisplayName}\n{DraftCatalog.Warriors[index].Experience}\n{RosterUi.SkillText(DraftCatalog.Warriors[index])}\nLower skill numbers are better.";
                var inspect = Ui.Button(panel, "…"); inspect.TooltipText = "Inspect candidate without changing your selection";
                _inspect[pool, choice] = inspect; inspect.Pressed += () => Preview(p, c, inspect);
            }
        }
        _progress = Ui.Label(this, "", 13, Ui.Muted);
        EstablishButton = Ui.Button(this, "Establish Company"); EstablishButton.Pressed += () => { if (Draft.Complete) Completed?.Invoke(); };
        CreatePreview(); Resized += Layout; Layout(); Refresh(); Hide();
    }
    private void CreatePreview()
    {
        _preview = new Control(); _preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_preview);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .82f) }; shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _preview.AddChild(shade);
        _previewPanel = new PanelContainer(); _previewPanel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold)); _preview.AddChild(_previewPanel);
        _previewBody = new VBoxContainer(); _previewPanel.AddChild(_previewBody); _preview.Hide();
    }
    public void Begin()
    {
        Draft = InitialDraft.CreateRandom(); NameInput.Text = Draft.ForceName; _preview.Hide(); Refresh(); Show();
        RosterUi.TrapFocus(this); NameInput.CallDeferred(Control.MethodName.GrabFocus);
    }
    public void RefreshArtwork()
    {
        for (var pool = 0; pool < 8; pool++) for (var choice = 0; choice < 3; choice++)
        { var index = (pool % 4) * 3 + choice; _portraits[pool, choice].Texture = Ui.LoadTexture(pool < 4 ? DraftCatalog.Mechs[index].Artwork : DraftCatalog.Warriors[index].Artwork); }
    }
    private void Refresh()
    {
        if (EstablishButton is null) return;
        for (var p = 0; p < 8; p++) for (var c = 0; c < 3; c++)
        {
            var chosen = Draft.Choice(p) == c; Choices[p, c].SetPressedNoSignal(chosen);
            Choices[p, c].AddThemeStyleboxOverride("normal", Ui.Box(chosen ? new Color("344431") : Ui.Background, chosen ? Ui.Gold : Ui.Line));
        }
        EstablishButton.Disabled = !Draft.Complete;
        _progress.Text = $"{Draft.SelectedCount}/8 selected · {(InitialDraft.ValidName(Draft.ForceName) ? "Company named" : "Enter a company name")} · Lower G/P is better";
    }
    private void Preview(int pool, int choice, Control source)
    {
        RosterUi.Clear(_previewBody); _previewReturn = source;
        var index = (pool % 4) * 3 + choice;
        if (pool < 4)
        {
            var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _previewBody.AddChild(row);
            var image = RosterUi.Picture(row, DraftCatalog.Mechs[index].Artwork, new Vector2(200, 220)); image.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var details = new MechDetails { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(details); details.Display(DraftCatalog.Mechs[index]);
        }
        else
        {
            var spec = DraftCatalog.Warriors[index];
            var image = RosterUi.Picture(_previewBody, spec.Artwork, new Vector2(180, 180)); image.SizeFlagsVertical = SizeFlags.ExpandFill;
            RosterUi.Text(_previewBody, spec.DisplayName, 22, Ui.Gold);
            RosterUi.Text(_previewBody, $"{spec.Experience}\n{RosterUi.SkillText(spec)}\nHealthy · Ready for deployment\nLower skill numbers are better.", 16);
        }
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; _previewBody.AddChild(actions);
        var back = Ui.Button(actions, "Back to draft"); back.Pressed += ClosePreview;
        var select = Ui.Button(actions, "Select candidate"); select.Pressed += () => { Draft.Select(pool, choice); Refresh(); ClosePreview(); };
        back.CustomMinimumSize = select.CustomMinimumSize = new Vector2(140, 40);
        _preview.Show(); Layout(); RosterUi.TrapFocus(_preview); back.CallDeferred(Control.MethodName.GrabFocus);
    }
    private void ClosePreview() { _preview.Hide(); _previewReturn?.CallDeferred(Control.MethodName.GrabFocus); RosterUi.TrapFocus(this); }
    private void Layout()
    {
        if (_title is null) return;
        Ui.Place(_title, 24, 10, Size.X - 390, 34); Ui.Place(NameInput, Size.X - 354, 14, 330, 36);
        Ui.Place(_instructions, 24, 48, Size.X - 48, 24);
        var width = (Size.X - 72) / 4; var height = (Size.Y - 144) / 2;
        for (var pool = 0; pool < 8; pool++)
        {
            Ui.Place(_pools[pool], 24 + pool % 4 * (width + 8), 82 + pool / 4 * (height + 8), width, height);
            Ui.Place(_poolTitles[pool], 10, 2, width - 20, 24);
            var rowHeight = (height - 34) / 3;
            for (var choice = 0; choice < 3; choice++)
            {
                var buttonWidth = width - 48; var y = 29 + choice * rowHeight;
                Ui.Place(Choices[pool, choice], 6, y, buttonWidth, rowHeight - 4);
                Ui.Place(_inspect[pool, choice], width - 38, y, 32, rowHeight - 4);
                var imageWidth = Math.Clamp(rowHeight - 8, 30, 100);
                Ui.Place(_portraits[pool, choice], 3, 2, imageWidth, rowHeight - 8);
                Ui.Place(_candidateNames[pool, choice], imageWidth + 8, Math.Max(1, (rowHeight - 44) / 2), buttonWidth - imageWidth - 12, 24);
                Ui.Place(_candidateStats[pool, choice], imageWidth + 8, Math.Max(22, (rowHeight - 44) / 2 + 24), buttonWidth - imageWidth - 12, 18);
            }
        }
        Ui.Place(_progress, 24, Size.Y - 45, Size.X - 250, 34); Ui.Place(EstablishButton, Size.X - 214, Size.Y - 46, 190, 36);
        Ui.Place(_previewPanel, 36, 24, Size.X - 72, Size.Y - 48);
    }
    public void HandleEscape()
    {
        if (_preview.Visible) ClosePreview(); else AbandonRequested?.Invoke();
    }
}
