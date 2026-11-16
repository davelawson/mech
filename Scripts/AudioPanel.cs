using Godot;

namespace Mech;

/// <summary>Main menu Audio: master, music, effects and interface volume, and mute. Changes apply and save at once.</summary>
public partial class AudioPanel : Control
{
    public event Action? Changed;
    public event Action? Closed;
    public HSlider MasterSlider { get; private set; } = null!;
    public HSlider MusicSlider { get; private set; } = null!;
    public HSlider EffectsSlider { get; private set; } = null!;
    public HSlider InterfaceSlider { get; private set; } = null!;
    public CheckBox MuteToggle { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    private PanelContainer _panel = null!;
    private AudioSettings _settings = new();
    private bool _showing;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .78f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _panel = new PanelContainer();
        _panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2));
        AddChild(_panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(content);
        Ui.Label(content, "Audio", 24, Ui.Gold);
        var grid = new GridContainer { Columns = 3, SizeFlagsVertical = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 16); grid.AddThemeConstantOverride("v_separation", 12);
        content.AddChild(grid);
        HSlider Row(string name, Action<int> set)
        {
            Ui.Label(grid, name, 16).CustomMinimumSize = new Vector2(110, 32);
            var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.All };
            grid.AddChild(slider);
            var value = Ui.Label(grid, "", 16, Ui.Muted); value.CustomMinimumSize = new Vector2(44, 0);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            slider.ValueChanged += level =>
            {
                value.Text = $"{(int)level}";
                if (_showing) return;
                set((int)level); Changed?.Invoke();
            };
            return slider;
        }
        MasterSlider = Row("Master", level => _settings.Master = level);
        MusicSlider = Row("Music", level => _settings.Music = level);
        EffectsSlider = Row("Effects", level => _settings.Effects = level);
        InterfaceSlider = Row("Interface", level => _settings.Interface = level);
        MuteToggle = new CheckBox { Text = "Mute all sound", FocusMode = FocusModeEnum.All };
        MuteToggle.Toggled += muted => { if (_showing) return; _settings.Muted = muted; Changed?.Invoke(); };
        content.AddChild(MuteToggle);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        content.AddChild(buttons);
        BackButton = Ui.Button(buttons, "Back"); BackButton.CustomMinimumSize = new Vector2(120, 44);
        Ui.Sound(BackButton, Cue.UiCancel);
        BackButton.Pressed += () => Closed?.Invoke();
        Resized += Layout;
        Hide();
    }

    /// <summary>Shows these settings; the panel edits the same instance.</summary>
    public void Display(AudioSettings settings)
    {
        _settings = settings; _showing = true;
        MasterSlider.Value = settings.Master; MusicSlider.Value = settings.Music;
        EffectsSlider.Value = settings.Effects; InterfaceSlider.Value = settings.Interface;
        MuteToggle.ButtonPressed = settings.Muted;
        _showing = false;
        Layout();
    }

    private void Layout()
    {
        if (_panel is null) return;
        var width = Math.Min(560, Size.X - 48);
        var height = Math.Min(360, Size.Y - 48);
        Ui.Place(_panel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }
}
