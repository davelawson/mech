using Godot;

namespace Mech;

/// <summary>A cassette-player glyph drawn on a time panel button: pause bars, a play triangle or fast-forward triangles.</summary>
public partial class TimeControlIcon : Control
{
    public enum Glyph { Pause, Play, FastForward }

    private const float Side = 14;
    private static readonly float TriangleWidth = Side * Mathf.Sqrt(3) / 2;

    public Glyph Shape { get; }
    private Color _color = Ui.Muted;
    public Color Color
    {
        get => _color;
        set { if (_color == value) return; _color = value; QueueRedraw(); }
    }

    public TimeControlIcon() : this(Glyph.Play) { }
    public TimeControlIcon(Glyph shape)
    {
        Shape = shape;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Draw()
    {
        var center = Size / 2;
        switch (Shape)
        {
            case Glyph.Pause:
                const float bar = 4, gap = 4;
                DrawRect(new Rect2(center.X - gap / 2 - bar, center.Y - Side / 2, bar, Side), Color);
                DrawRect(new Rect2(center.X + gap / 2, center.Y - Side / 2, bar, Side), Color);
                break;
            case Glyph.Play:
                Triangle(center.X - TriangleWidth / 2, center.Y);
                break;
            case Glyph.FastForward:
                Triangle(center.X - TriangleWidth, center.Y);
                Triangle(center.X, center.Y);
                break;
        }
    }

    /// <summary>An equilateral triangle pointing right, its flat side at <paramref name="left"/>.</summary>
    private void Triangle(float left, float middle) => DrawColoredPolygon(
        [new Vector2(left, middle - Side / 2), new Vector2(left + TriangleWidth, middle), new Vector2(left, middle + Side / 2)], Color);
}
