using Godot;

namespace Mech;

/// <summary>The battlefield's silhouette mask: every armor mesh gets a flat twin on its own visual layer, which only the
/// battlefield's mask camera renders. Twins are children of their meshes, so they follow the gait and damage hiding.</summary>
public partial class MechModel
{
    /// <summary>The 1-based visual layer that holds silhouette twins; the main battlefield camera never renders it.</summary>
    public const int SilhouetteLayer = 20;
    public const uint SilhouetteLayerBit = 1u << (SilhouetteLayer - 1);
    /// <summary>Mask ids run 1 to this; only bytes 64–255 survive the Compatibility output exactly.</summary>
    public const int MaxSilhouetteId = 191;

    // Writes the mech's id and its camera-space depth, each channel kept within bytes 64–255.
    private static Shader? _silhouetteShader;
    private static Shader SilhouetteShader => _silhouetteShader ??= new() { Code = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;
        uniform float mask_id;
        uniform float far_plane;
        void fragment() {
            float depth = floor(clamp(-VERTEX.z / far_plane, 0.0, 1.0) * 36863.0);
            float high = floor(depth / 192.0);
            ALBEDO = vec3(mask_id + 64.0, high + 64.0, depth - high * 192.0 + 64.0) / 255.0;
        }
        """ };

    private readonly List<MeshInstance3D> _silhouetteTwins = [];
    /// <summary>This model's id in the battlefield silhouette mask, 0 without one.</summary>
    public int SilhouetteId { get; private set; }
    /// <summary>Whether the model currently draws into the silhouette mask (false once out of the fight).</summary>
    public bool SilhouetteEnabled { get; private set; }
    internal IReadOnlyList<MeshInstance3D> SilhouetteTwins => _silhouetteTwins;

    /// <summary>Adds a mask twin to every armor and weapon mesh, leaving out jump-jet exhaust.</summary>
    public void AddSilhouetteMask(int id, float farPlane)
    {
        if (SilhouetteId != 0 || id is < 1 or > MaxSilhouetteId) return;
        SilhouetteId = id; SilhouetteEnabled = true;
        var material = new ShaderMaterial { Shader = SilhouetteShader };
        material.SetShaderParameter("mask_id", (float)id);
        material.SetShaderParameter("far_plane", farPlane);
        foreach (var mesh in FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(mesh => !IsJetEffect(mesh)).ToArray())
        {
            var twin = new MeshInstance3D
            {
                Name = "Silhouette", Mesh = mesh.Mesh, MaterialOverride = material, Layers = SilhouetteLayerBit,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled
            };
            mesh.AddChild(twin); _silhouetteTwins.Add(twin);
        }
    }

    /// <summary>Includes or drops the model from the silhouette mask.</summary>
    public void SetSilhouetteEnabled(bool enabled)
    {
        if (SilhouetteId == 0 || SilhouetteEnabled == enabled) return;
        SilhouetteEnabled = enabled;
        foreach (var twin in _silhouetteTwins) twin.Layers = enabled ? SilhouetteLayerBit : 0;
    }
}
