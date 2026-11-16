using Godot;

namespace Mech;

internal static class Ui
{
    public static readonly Color Background = new("101412");
    public static readonly Color Panel = new("171d18");
    public static readonly Color Gold = new("d4b57a");
    public static readonly Color Text = new("e3e0d5");
    public static readonly Color Muted = new("9b9f93");
    public static readonly Color Line = new("343d33");

    public static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFontSize = 14 };
        theme.SetColor("font_color", "Label", Text);
        theme.SetColor("font_color", "Button", Text);
        theme.SetColor("font_hover_color", "Button", Gold);
        theme.SetColor("font_focus_color", "Button", Gold);
        theme.SetStylebox("normal", "Button", Box(Panel, Line));
        theme.SetStylebox("hover", "Button", Box(new Color("30392b"), Gold));
        theme.SetStylebox("pressed", "Button", Box(new Color("3e4634"), Gold));
        theme.SetStylebox("disabled", "Button", Box(Panel, Line));
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, Gold, 2));
        return theme;
    }

    public static StyleBoxFlat Box(Color fill, Color border, int width = 1)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = border };
        box.SetBorderWidthAll(width);
        box.ContentMarginLeft = 12;
        box.ContentMarginRight = 12;
        box.ContentMarginTop = 6;
        box.ContentMarginBottom = 6;
        return box;
    }

    public static Label Label(Node parent, string text, int fontSize = 14, Color? color = null)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? Text);
        parent.AddChild(label);
        return label;
    }

    public static Button Button(Node parent, string text)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.All };
        parent.AddChild(button);
        return button;
    }

    private const string SoundMeta = "press_sound";

    /// <summary>Overrides the click every button makes when pressed, such as a confirmation for committing actions.</summary>
    public static T Sound<T>(T button, Cue cue) where T : BaseButton
    {
        button.SetMeta(SoundMeta, (int)cue);
        return button;
    }

    /// <summary>A button whose press makes no interface sound, for actions that play their own.</summary>
    public static T Silent<T>(T button) where T : BaseButton
    {
        button.SetMeta(SoundMeta, -1);
        return button;
    }

    /// <summary>The cue a press plays: its override, or a plain click; null when silenced.</summary>
    public static Cue? PressCue(BaseButton button)
    {
        if (!button.HasMeta(SoundMeta)) return Cue.UiClick;
        var cue = button.GetMeta(SoundMeta).AsInt32();
        return cue < 0 ? null : (Cue)cue;
    }

    public static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Math.Max(0, width), Math.Max(0, height));
    }

    public static Texture2D? LoadTexture(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }
}
