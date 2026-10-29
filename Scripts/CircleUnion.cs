using Godot;

namespace Mech;

/// <summary>Exact exposed arcs of a union of circles, before terrain projection and tessellation.</summary>
internal static class CircleUnion
{
    internal readonly record struct Circle(Vector2 Center, float Radius);
    internal readonly record struct Arc(Vector2 Center, float Radius, float Start, float End);

    internal static IEnumerable<Arc> Boundary(IReadOnlyList<Circle> circles)
    {
        for (var i = 0; i < circles.Count; i++)
        {
            var circle = circles[i];
            var covered = new List<(float Start, float End)>();
            var contained = false;
            for (var j = 0; j < circles.Count; j++)
            {
                if (i == j) continue;
                var other = circles[j];
                var offset = other.Center - circle.Center;
                var distance = offset.Length();
                // Coincident circles contribute one boundary, independent of selection order.
                if (distance + circle.Radius <= other.Radius
                    && (distance > 0 || circle.Radius < other.Radius || j < i))
                { contained = true; break; }
                if (distance >= circle.Radius + other.Radius || distance <= Math.Abs(circle.Radius - other.Radius)) continue;
                var halfAngle = Mathf.Acos(Math.Clamp((distance * distance + circle.Radius * circle.Radius - other.Radius * other.Radius)
                    / (2 * distance * circle.Radius), -1, 1));
                var direction = Mathf.PosMod(offset.Angle(), Mathf.Tau);
                var start = direction - halfAngle; var end = direction + halfAngle;
                if (start < 0) { covered.Add((start + Mathf.Tau, Mathf.Tau)); start = 0; }
                if (end > Mathf.Tau) { covered.Add((0, end - Mathf.Tau)); end = Mathf.Tau; }
                covered.Add((start, end));
            }
            if (contained) continue;
            var cursor = 0f;
            foreach (var interval in covered.OrderBy(interval => interval.Start))
            {
                if (interval.Start > cursor) yield return new(circle.Center, circle.Radius, cursor, interval.Start);
                cursor = Math.Max(cursor, interval.End);
            }
            if (cursor < Mathf.Tau) yield return new(circle.Center, circle.Radius, cursor, Mathf.Tau);
        }
    }
}
