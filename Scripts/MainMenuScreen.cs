using Godot;

namespace Mech;

public partial class MainMenuScreen : Control
{
    [Signal] public delegate void NewRequestedEventHandler();
    [Signal] public delegate void ResumeRequestedEventHandler();
    public Button ResumeButton { get; private set; } = null!;
    [Signal] public delegate void LoadRequestedEventHandler();
    public Button NewButton { get; private set; } = null!;
    public Button LoadButton { get; private set; } = null!;
    private VBoxContainer _menu = null!;

    public override void _Ready()
    {
        var background = new ColorRect { Color = Ui.Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        _menu = new VBoxContainer();
        _menu.AddThemeConstantOverride("separation", 20);
        AddChild(_menu);
        var title = Ui.Label(_menu, "M E C H", 48, Ui.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        var subtitle = Ui.Label(_menu, "MERCENARY COMMAND  ·  3025", 13, Ui.Muted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        ResumeButton = Ui.Button(_menu, "Resume"); ResumeButton.Pressed += () => EmitSignal(SignalName.ResumeRequested); ResumeButton.Hide();
        NewButton = Ui.Button(_menu, "New Company");
        NewButton.Pressed += () => EmitSignal(SignalName.NewRequested);
        LoadButton = Ui.Button(_menu, "Load Game");
        LoadButton.Pressed += () => EmitSignal(SignalName.LoadRequested);
        var quit = Ui.Button(_menu, "Quit");
        quit.Pressed += () => GetTree().Quit();
        foreach (var button in new[] { ResumeButton, NewButton, LoadButton, quit }) button.CustomMinimumSize = new Vector2(0, 48);
        Resized += Layout;
        Layout();
    }

    private void Layout()
    {
        if (_menu is not null) Ui.Place(_menu, (Size.X - 360) / 2, Math.Max(24, (Size.Y - 410) / 2), 360, 410);
    }
}
