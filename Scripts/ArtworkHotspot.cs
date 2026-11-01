using Godot;

namespace Mech;

/// <summary>An invisible button that only responds over a painted object, outlined in gold on hover or focus.</summary>
public partial class ArtworkHotspot : Button
{
    /// <summary>The painted outline in source-art pixels; <see cref="Region"/> is its bounding box.</summary>
    public Vector2[] Shape { get; }
    public Rect2 Region { get; }
    private readonly Vector2[] _local;

    public ArtworkHotspot() : this([Vector2.Zero, Vector2.Right, Vector2.Down]) { }

    public ArtworkHotspot(Vector2[] shape)
    {
        Shape = shape;
        Region = Bounds(shape);
        _local = shape.Select(point => point - Region.Position).ToArray();
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        foreach (var signal in new[] { SignalName.MouseEntered, SignalName.MouseExited, SignalName.FocusEntered,
            SignalName.FocusExited, BaseButton.SignalName.ButtonDown, BaseButton.SignalName.ButtonUp })
            Connect(signal, Callable.From(QueueRedraw));
    }

    public static Rect2 Bounds(Vector2[] shape)
    {
        var bounds = new Rect2(shape[0], Vector2.Zero);
        foreach (var point in shape) bounds = bounds.Expand(point);
        return bounds;
    }

    /// <summary>A polygonal ellipse, with points beyond <paramref name="maxX"/> pulled back to the artwork edge.</summary>
    public static Vector2[] Ellipse(Vector2 center, Vector2 radius, float maxX, int points = 32) =>
        Enumerable.Range(0, points).Select(index =>
        {
            var angle = Mathf.Tau * index / points;
            return new Vector2(Math.Min(maxX, center.X + radius.X * Mathf.Cos(angle)), center.Y + radius.Y * Mathf.Sin(angle));
        }).ToArray();

    public override bool _HasPoint(Vector2 point) => Geometry2D.IsPointInPolygon(point, _local);
    public bool ContainsGlobal(Vector2 point) => _HasPoint(GetGlobalTransform().AffineInverse() * point);

    public override void _Draw()
    {
        if (Disabled) return;
        var hovered = IsHovered();
        if (!hovered && !HasFocus()) return;
        var fill = ButtonPressed || GetDrawMode() == DrawMode.Pressed ? .08f : hovered ? .04f : 0;
        if (fill > 0) DrawColoredPolygon(_local, new Color(1, .9f, .65f, fill));
        DrawPolyline([.. _local, _local[0]], Ui.Gold, 1.5f / Math.Max(Scale.X, .01f), true);
    }
}
