using Godot;

namespace Mech;

public sealed record DialogAction(string Text, Action Execute, bool Disabled = false);

/// <summary>One modal surface for finances, filing, confirmation and campaign notices.</summary>
public partial class CampaignDialog : Control
{
    private PanelContainer _panel = null!;
    private VBoxContainer _content = null!;
    private Action? _cancel;
    public string Heading { get; private set; } = "";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .78f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _panel = new PanelContainer();
        _panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Gold, 2));
        AddChild(_panel);
        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 16);
        _panel.AddChild(_content);
        Resized += Layout;
        Hide();
    }

    public void Open(string heading, string body, Action? cancel, params DialogAction[] actions)
    {
        Heading = heading;
        _cancel = cancel;
        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
        Ui.Label(_content, heading, 24, Ui.Gold);
        var text = Ui.Label(_content, body, 16);
        text.VerticalAlignment = VerticalAlignment.Top;
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsVertical = SizeFlags.ExpandFill;
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 12);
        _content.AddChild(buttons);
        Button? first = null;
        var enabled = new List<Button>();
        foreach (var action in actions)
        {
            var button = Ui.Button(buttons, action.Text);
            button.CustomMinimumSize = new Vector2(120, 44);
            button.Disabled = action.Disabled;
            button.Pressed += action.Execute;
            if (!button.Disabled)
            {
                first ??= button;
                enabled.Add(button);
            }
        }
        for (var index = 0; index < enabled.Count; index++)
        {
            enabled[index].FocusNext = enabled[(index + 1) % enabled.Count].GetPath();
            enabled[index].FocusPrevious = enabled[(index + enabled.Count - 1) % enabled.Count].GetPath();
            enabled[index].FocusNeighborRight = enabled[index].FocusNext;
            enabled[index].FocusNeighborLeft = enabled[index].FocusPrevious;
            enabled[index].FocusNeighborTop = enabled[index].GetPath();
            enabled[index].FocusNeighborBottom = enabled[index].GetPath();
        }
        Show();
        Layout();
        first?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void RequestBack() => _cancel?.Invoke();

    private void Layout()
    {
        if (_panel is null) return;
        var width = Math.Min(660, Size.X - 48);
        var height = Math.Min(390, Size.Y - 48);
        Ui.Place(_panel, (Size.X - width) / 2, (Size.Y - height) / 2, width, height);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            RequestBack();
            GetViewport().SetInputAsHandled();
        }
    }
}
