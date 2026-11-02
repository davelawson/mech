using Godot;

namespace Mech;

/// <summary>A hangar summary bar with its value written over it. For armor and structure, the full width is the most the mech's
/// tonnage allows; what remains is one solid colour taken from a red → orange → yellow → green scale at the fraction remaining,
/// and what is still to repair, up to stock, is grey. A ratio bar (cooling) fills to the ratio, capped at full, and shows ⚠ above it.
/// Colours are dimmed so the overlaid text stays legible.</summary>
public partial class HangarConditionBar : Control
{
    public const float Height = 30;
    private static readonly Gradient Condition = new()
    {
        Offsets = [0, .33f, .66f, 1],
        Colors = [new Color("d64541"), new Color("e8892f"), new Color("e8d23a"), new Color("5fbf4a")]
    };
    private static readonly Color Track = new(.1f, .13f, .12f);
    private static readonly Color Repair = new(.45f, .47f, .45f);
    private const float Dim = .45f;
    public int Current { get; private set; }
    public int Stock { get; private set; }
    public int Maximum { get; private set; }
    public string Text { get; private set; } = "";
    /// <summary>The filled share of the bar, 0 to 1.</summary>
    public float Fraction { get; private set; }
    /// <summary>The filled share plus what is still to repair up to stock.</summary>
    public float StockFraction { get; private set; }
    /// <summary>A ratio bar whose ratio exceeds 1, marked ⚠.</summary>
    public bool Warning { get; private set; }

    public HangarConditionBar()
    {
        CustomMinimumSize = new Vector2(0, Height); SizeFlagsHorizontal = SizeFlags.ExpandFill; MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Set(int current, int stock, int maximum, string caption)
    {
        Current = current; Stock = stock; Maximum = maximum; Warning = false;
        Fraction = maximum > 0 ? Mathf.Clamp((float)current / maximum, 0, 1) : 0;
        StockFraction = maximum > 0 ? Mathf.Clamp((float)stock / maximum, Fraction, 1) : 0;
        Text = $"{caption} {current:N0} / {stock:N0}"; QueueRedraw();
    }

    public void SetRatio(double ratio, string text, string tooltip)
    {
        Fraction = StockFraction = (float)Math.Clamp(ratio, 0, 1); Warning = ratio > 1;
        Text = text; TooltipText = tooltip; MouseFilter = MouseFilterEnum.Stop; QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, Track);
        DrawRect(new Rect2(Size.X * Fraction, 0, Size.X * (StockFraction - Fraction), Size.Y), Repair.Darkened(Dim));
        DrawRect(new Rect2(0, 0, Size.X * Fraction, Size.Y), Condition.Sample(Fraction).Darkened(Dim));
        DrawRect(bounds, Ui.Line, false, 1);
        var icon = Warning ? Size.Y * .6f : 0;
        HangarLocationPanel.DrawOverlay(this, new Rect2(6, 0, Size.X - 12 - (icon > 0 ? icon + 6 : 0), Size.Y), Text, "", 13);
        if (Warning) HangarLocationPanel.DrawWarning(this, new Vector2(Size.X - 6 - icon, (Size.Y - icon) / 2), icon, Track);
    }
}
