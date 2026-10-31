using Godot;

namespace Mech;

/// <summary>Fills its parent and renders the 3D at the window's pixel density, not the scaled interface's logical size, so it stays sharp in large windows.</summary>
public partial class NativeViewportContainer : SubViewportContainer
{
    /// <summary>Window pixels per interface unit; multiply local coordinates by this to reach sub-viewport pixels.</summary>
    public float Density { get; private set; } = 1;

    public override void _Ready()
    {
        Stretch = true;
        Fit();
    }

    public override void _Process(double delta) => Fit();

    private void Fit()
    {
        if (GetParent() is not Control host) return;
        var density = GetViewport().GetFinalTransform().X.X;
        if (density <= 0) density = 1;
        var size = host.Size * density;
        if (Mathf.IsEqualApprox(density, Density) && Size.IsEqualApprox(size) && Position == Vector2.Zero) return;
        Density = density; Position = Vector2.Zero; Scale = Vector2.One / density; Size = size;
    }
}
