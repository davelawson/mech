using Godot;

namespace Mech;

/// <summary>A control zone's bar on the combat HUD: the zone's name and the company's share of control over a bar that fills from the
/// left in the company's colour against the opposition's, marked CONTESTED while control is off its owner's end.</summary>
public sealed partial class ControlZoneBar : Control
{
    public const float BarWidth = 240, BarHeight = 12, CaptionHeight = 17;
    public static readonly Color CompanyColor = CombatScreen.FriendlyHighlight, OppositionColor = CombatScreen.EnemyHighlight;
    public ControlZone? Zone { get; set; }

    public ControlZoneBar() { MouseFilter = MouseFilterEnum.Ignore; }
    public override Vector2 _GetMinimumSize() => new(BarWidth, CaptionHeight + BarHeight + 2);

    /// <summary>"OBJECTIVE · 45% · CONTESTED".</summary>
    public string Caption => Zone is not { } zone ? ""
        : $"{zone.Name} · {Math.Floor(zone.Control * 100):0}%{(zone.Contested ? " · CONTESTED" : "")}";

    public override void _Draw()
    {
        if (Zone is not { } zone) return;
        var font = ThemeDB.FallbackFont;
        var at = new Vector2(0, 13);
        DrawStringOutline(font, at, Caption, HorizontalAlignment.Left, -1, 13, 4, new Color(0, 0, 0, .85f));
        DrawString(font, at, Caption, HorizontalAlignment.Left, -1, 13, zone.Contested ? Ui.Gold : zone.Owner == CombatSide.Friendly ? CompanyColor : OppositionColor);
        var bar = new Rect2(0, CaptionHeight, BarWidth, BarHeight);
        DrawRect(bar, new Color(OppositionColor, .55f));
        DrawRect(new Rect2(bar.Position, new Vector2(BarWidth * (float)zone.Control, BarHeight)), CompanyColor);
        DrawRect(bar, new Color(0, 0, 0, .85f), false, 1.5f);
    }
}
