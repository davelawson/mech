using Godot;

namespace Mech;

// All geometry is in map-local logical pixels, independent of display DPI.
public sealed class MapTransform
{
    public const float MinimumZoom = 1;
    public const float MaximumZoom = 12;
    public Vector2 ImageSize { get; }
    public Vector2 ViewSize { get; private set; }
    public Vector2 Offset { get; private set; }
    public float Zoom { get; private set; } = 1;
    public float Scale { get; private set; } = 1;
    public Rect2 ImageRect => new(Offset, ImageSize * Scale);

    public MapTransform(Vector2 imageSize) => ImageSize = imageSize;
    private float FitScale => Math.Min(ViewSize.X / ImageSize.X, ViewSize.Y / ImageSize.Y) * .96f;

    public void Resize(Vector2 size)
    {
        if (size.X <= 0 || size.Y <= 0) return;
        var center = ViewSize.X > 0 ? ImagePoint(ViewSize / 2) : ImageSize / 2;
        ViewSize = size;
        Scale = FitScale * Zoom;
        Offset = size / 2 - center * Scale;
        Clamp();
    }

    public Vector2 ImagePoint(Vector2 viewPoint) => (viewPoint - Offset) / Scale;

    public void ZoomAt(float zoom, Vector2 anchor)
    {
        if (ViewSize.X <= 0 || ViewSize.Y <= 0) return;
        var point = ImagePoint(anchor);
        Zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        Scale = FitScale * Zoom;
        Offset = anchor - point * Scale;
        Clamp();
    }

    public void CenterOn(Vector2 imagePoint)
    {
        Offset = ViewSize / 2 - imagePoint * Scale;
        Clamp();
    }

    public void Pan(Vector2 delta)
    {
        Offset += delta;
        Clamp();
    }

    public void Fit()
    {
        Zoom = 1;
        Scale = FitScale;
        Offset = (ViewSize - ImageSize * Scale) / 2;
        Clamp();
    }

    private void Clamp()
    {
        static float Axis(float offset, float frame, float content) => content <= frame
            ? (frame - content) / 2
            : Math.Clamp(offset, frame - content, 0);
        var scaled = ImageSize * Scale;
        Offset = new Vector2(Axis(Offset.X, ViewSize.X, scaled.X), Axis(Offset.Y, ViewSize.Y, scaled.Y));
    }
}
