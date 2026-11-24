using Godot;

namespace Mech;

/// <summary>Outlines each mech's on-screen silhouette (see docs/mech-models.md). A second camera renders the models' mask
/// twins (<see cref="MechModel.SilhouetteLayer"/>) into a mask of mech ids and depths. A full-screen pass in the
/// battlefield then colours the pixels just outside each silhouette, at half opacity where terrain hides that edge.</summary>
public partial class CombatViewport
{
    /// <summary>Outline thickness in window pixels, and while hovered.</summary>
    public const int OutlinePixels = 2, HoveredOutlinePixels = 4;
    /// <summary>An edge is hidden when the scene in front of it is nearer than the mech by more than this many metres.</summary>
    private const float OcclusionMargin = 3;

    private const string CompositeShader = """
        shader_type spatial;
        render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
        uniform sampler2D mask : filter_nearest, repeat_disable;
        uniform sampler2D palette : filter_nearest, repeat_disable;
        uniform sampler2D scene_depth : hint_depth_texture, filter_nearest;
        uniform float far_plane;
        uniform int hovered = 0;
        uniform int width = 2;
        uniform int hovered_width = 4;
        uniform float occlusion_margin = 3.0;
        void vertex() { POSITION = vec4(VERTEX.xy, 1.0, 1.0); }
        // Ids and depths are stored within bytes 64-255; uncovered pixels have zero alpha.
        ivec3 decode(vec4 texel) { return texel.a < .5 ? ivec3(0) : ivec3(round(texel.rgb * 255.0)) - 64; }
        float mask_depth(ivec3 texel) { return float(texel.g * 192 + texel.b) / 36863.0 * far_plane; }
        void fragment() {
            ivec2 size = textureSize(mask, 0);
            ivec2 pixel = ivec2(FRAGCOORD.xy);
            if (pixel.x >= size.x || pixel.y >= size.y) discard;
            ivec3 here = decode(texelFetch(mask, pixel, 0));
            float here_depth = here.r <= 0 ? 1e30 : mask_depth(here);
            // The frontmost mech within its outline width of this pixel, and the nearest of its pixels.
            int best = 0; float best_depth = 1e30; int best_distance = 1000; ivec2 best_pixel = pixel;
            // Only a hovered mech needs the wider search.
            int search = hovered > 0 ? max(width, hovered_width) : width;
            for (int y = -search; y <= search; y++) for (int x = -search; x <= search; x++) {
                ivec2 at = pixel + ivec2(x, y);
                if (at.x < 0 || at.y < 0 || at.x >= size.x || at.y >= size.y) continue;
                ivec3 sample = decode(texelFetch(mask, at, 0));
                if (sample.r <= 0 || sample.r == here.r) continue;
                int distance = x * x + y * y;
                int reach = sample.r == hovered ? hovered_width : width;
                if (distance > reach * reach + reach) continue;
                float depth = mask_depth(sample);
                // A mech in front of this pixel's own mech hides its outline.
                if (depth >= here_depth) continue;
                if (sample.r != best) {
                    if (depth >= best_depth) continue;
                    best = sample.r; best_depth = depth; best_distance = distance; best_pixel = at;
                } else {
                    best_depth = min(best_depth, depth);
                    if (distance < best_distance) { best_distance = distance; best_pixel = at; }
                }
            }
            if (best == 0) discard;
            vec3 color = texelFetch(palette, ivec2(best, 0), 0).rgb;
            if (best == hovered) color = mix(color, vec3(1.0), .5);
            // Compare the scene's depth at the nearest silhouette pixel with the mech's own depth there.
            float raw = texelFetch(scene_depth, best_pixel, 0).r;
            vec4 view = INV_PROJECTION_MATRIX * vec4((vec2(best_pixel) + .5) / vec2(size) * 2.0 - 1.0, raw * 2.0 - 1.0, 1.0);
            float scene = -view.z / view.w;
            bool hidden = scene < mask_depth(decode(texelFetch(mask, best_pixel, 0))) - occlusion_margin;
            ALBEDO = color;
            ALPHA = hidden ? .5 : 1.0;
        }
        """;

    private SubViewport _maskViewport = null!;
    private Camera3D _maskCamera = null!;
    private ShaderMaterial _outlineMaterial = null!;
    private Image _palette = null!;
    private ImageTexture _paletteTexture = null!;
    internal ShaderMaterial OutlineMaterial => _outlineMaterial;
    internal SubViewport SilhouetteMask => _maskViewport;
    internal SubViewport SceneViewport => _viewport;

    private void CreateOutline()
    {
        Camera.CullMask &= ~MechModel.SilhouetteLayerBit;
        _maskViewport = new SubViewport
        {
            Name = "SilhouetteMask", World3D = _viewport.FindWorld3D(), TransparentBg = true, Size = _viewport.Size,
            Msaa3D = Viewport.Msaa.Disabled, ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled, UseTaa = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible, GuiDisableInput = true,
            PositionalShadowAtlasSize = 0
        };
        AddChild(_maskViewport);
        // A plain environment keeps the encoded bytes exact: no sky, fog, glow or filmic curve.
        _maskCamera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal, CullMask = MechModel.SilhouetteLayerBit, Current = true,
            Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor, BackgroundColor = Colors.Transparent }
        };
        _maskViewport.AddChild(_maskCamera);
        _palette = Image.CreateEmpty(256, 1, false, Image.Format.Rgba8);
        _paletteTexture = ImageTexture.CreateFromImage(_palette);
        _outlineMaterial = new ShaderMaterial { Shader = new Shader { Code = CompositeShader }, RenderPriority = (int)Material.RenderPriorityMax };
        _outlineMaterial.SetShaderParameter("mask", _maskViewport.GetTexture());
        _outlineMaterial.SetShaderParameter("palette", _paletteTexture);
        _outlineMaterial.SetShaderParameter("width", OutlinePixels);
        _outlineMaterial.SetShaderParameter("hovered_width", HoveredOutlinePixels);
        _outlineMaterial.SetShaderParameter("occlusion_margin", OcclusionMargin);
        // Its vertex shader covers the screen; the margin keeps it from being culled with the camera's quad.
        Camera.AddChild(new MeshInstance3D
        {
            Name = "SilhouetteOutline", Mesh = new QuadMesh { Size = new Vector2(2, 2) }, MaterialOverride = _outlineMaterial,
            Position = new Vector3(0, 0, -Camera.Near - 1), ExtraCullMargin = 16384,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled
        });
        SyncOutline();
    }

    /// <summary>Gives a new battle's model its mask id and colour; the first model of a battle is 1.</summary>
    private void AddOutline(CombatUnit unit, MechModel model)
    {
        var id = _models.Count;
        if (id > MechModel.MaxSilhouetteId) return;
        model.AddSilhouetteMask(id, Camera.Far);
        _palette.SetPixel(id, 0, MechModel.TeamColor(unit.Side, unit.Lance));
        _paletteTexture.Update(_palette);
    }

    /// <summary>Keeps the mask camera and its target matched to the battlefield's camera and pixel size.</summary>
    private void SyncOutline()
    {
        if (_maskViewport.Size != _viewport.Size) _maskViewport.Size = _viewport.Size;
        _maskCamera.GlobalTransform = Camera.GlobalTransform;
        _maskCamera.Size = Camera.Size; _maskCamera.Near = Camera.Near; _maskCamera.Far = Camera.Far;
        _outlineMaterial.SetShaderParameter("far_plane", Camera.Far);
        _outlineMaterial.SetShaderParameter("hovered", HoveredMech is { } id && _models.TryGetValue(id, out var model) ? model.SilhouetteId : 0);
    }
}
