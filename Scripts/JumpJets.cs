using Godot;

namespace Mech;

public partial class MechModel
{
    // One exhaust per location holding jets, placed from the rest pose in the space of the node that carries it through the jump poses.
    private sealed record JetMount(MechLocation Location, int Jets, Node3D Parent, Vector3 At, float Tilt);
    private sealed record Nozzle(JetMount Mount, Node3D Root, Node3D Exhaust, MeshInstance3D Glow, MeshInstance3D[] Diamonds, (StandardMaterial3D Material, float Alpha)[] Materials);
    private readonly List<JetMount> _jetMounts = [];
    private readonly List<Nozzle> _nozzles = [];
    private readonly HashSet<Node3D> _nozzleRoots = [];
    private static readonly float[] DiamondAt = [.12f, .23f, .34f];
    private static readonly float TorsoJetTilt = Mathf.DegToRad(-20);

    /// <summary>Finds where each location's jets fire from: torso jets from low on the back, leg jets from the soles.</summary>
    private void PlaceJumpJets(MechSpec spec)
    {
        foreach (var group in spec.Components.Where(item => item.Kind == ComponentKind.JumpJet).GroupBy(item => item.Location))
        {
            var jets = group.Sum(item => item.Slots);
            var leg = group.Key is MechLocation.LeftLeg or MechLocation.RightLeg;
            if (leg && _rig is not null && _legs.FirstOrDefault(item => item.Side == (group.Key == MechLocation.LeftLeg ? -1 : 1)) is { } rigged)
            {
                _jetMounts.Add(new(group.Key, jets, rigged.Ankle, new(0, -_rig.Ankle.Y, 0), 0));
                continue;
            }
            var root = _locations[group.Key];
            if (LocalBounds(root) is not { } box) continue;
            var center = box.GetCenter();
            // Local forward is -Z, so the back of a torso is its largest Z.
            _jetMounts.Add(leg ? new(group.Key, jets, root, new(center.X, box.Position.Y, center.Z), 0)
                : new(group.Key, jets, root, new(center.X, box.Position.Y + box.Size.Y * .3f, box.End.Z - box.Size.Z * .05f), TorsoJetTilt));
        }
    }
    private static Aabb? LocalBounds(Node3D root)
    {
        Aabb? bounds = null;
        void Visit(Node3D node, Transform3D transform)
        {
            if (node is MeshInstance3D { Mesh: { } mesh })
            {
                var box = transform * mesh.GetAabb();
                bounds = bounds is { } previous ? previous.Merge(box) : box;
            }
            foreach (var child in node.GetChildren().OfType<Node3D>()) Visit(child, transform * child.Transform);
        }
        foreach (var child in root.GetChildren().OfType<Node3D>()) Visit(child, child.Transform);
        return bounds;
    }
    private bool IsJetEffect(Node node) => _nozzleRoots.Any(root => root == node || root.IsAncestorOf(node));

    /// <summary>Rocket exhaust from every location holding jump jets: strength 0 hides it. Width follows the location's working jets
    /// (all installed jets unless given), so lost and destroyed jets go dark. The clock drives the flicker, so it freezes on pause.</summary>
    public void SetJetting(float strength, double clock, Func<MechLocation, int>? activeJets = null)
    {
        if (strength <= 0 && _nozzles.Count == 0) return;
        if (_nozzles.Count == 0) foreach (var mount in _jetMounts) _nozzles.Add(BuildNozzle(mount));
        var time = (float)clock;
        for (var i = 0; i < _nozzles.Count; i++)
        {
            var nozzle = _nozzles[i];
            var working = Math.Clamp(activeJets?.Invoke(nozzle.Mount.Location) ?? nozzle.Mount.Jets, 0, nozzle.Mount.Jets);
            nozzle.Root.Visible = strength > 0 && working > 0;
            if (!nozzle.Root.Visible) continue;
            var flicker = 1 + .12f * Mathf.Sin(time * 47 + i * 1.7f) + .08f * Mathf.Sin(time * 89 + i * 2.9f);
            var radius = .045f * Mathf.Sqrt(working) * (.85f + .15f * strength) * (1 + .4f * (flicker - 1));
            var length = (.8f + .2f * Mathf.Sqrt(nozzle.Mount.Jets)) * Math.Max(.001f, strength * flicker);
            nozzle.Exhaust.Scale = new(radius, length, radius);
            nozzle.Glow.Scale = Vector3.One * radius * (.8f + .2f * (flicker - 1));
            for (var d = 0; d < nozzle.Diamonds.Length; d++)
                nozzle.Diamonds[d].Position = new(0, -(DiamondAt[d] + .015f * Mathf.Sin(time * 61 + d * 2.3f + i)), 0);
            var brightness = Math.Clamp(.55f + .45f * strength * flicker, 0, 1);
            foreach (var (material, alpha) in nozzle.Materials) material.AlbedoColor = material.AlbedoColor with { A = alpha * brightness };
        }
    }
    private Nozzle BuildNozzle(JetMount mount)
    {
        var materials = new List<(StandardMaterial3D, float)>();
        MeshInstance3D Part(string name, Mesh mesh, Node3D parent, Color? color = null)
        {
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = color is null, AlbedoColor = color ?? Colors.White
            };
            materials.Add((material, material.AlbedoColor.A));
            var part = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            parent.AddChild(part); return part;
        }
        var root = new Node3D { Name = $"JumpJet_{mount.Location}", Position = mount.At, Rotation = new(mount.Tilt, 0, 0), Visible = false };
        mount.Parent.AddChild(root); _nozzleRoots.Add(root);
        // Everything in the exhaust is authored one unit long down -Y with unit nozzle radius, then scaled to the thrust.
        var exhaust = new Node3D { Name = "Exhaust" }; root.AddChild(exhaust);
        Part("Plume", PlumeMesh, exhaust);
        Part("Core", CoreMesh, exhaust);
        var diamonds = DiamondAt.Select((_, d) =>
        {
            var diamond = Part($"ShockDiamond{d + 1}", DiamondMesh, exhaust, new Color(1, .93f, .78f, .7f - .2f * d));
            diamond.Scale = new Vector3(.55f, .045f, .55f) * (1 - .2f * d);
            return diamond;
        }).ToArray();
        var glow = Part("NozzleGlow", DiamondMesh, root, new Color(1, .95f, .85f));
        return new(mount, root, exhaust, glow, diamonds, [.. materials]);
    }
    private static readonly SphereMesh DiamondMesh = new() { Radius = 1, Height = 2, RadialSegments = 10, Rings = 5 };
    // Declared before the meshes built from them: static fields initialize in order.
    private static readonly Color PlumeWhite = new(1, .96f, .85f), PlumeYellow = new(1, .78f, .3f), PlumeOrange = new(1, .42f, .08f), PlumeRed = new(.6f, .1f, .03f);
    // The plume leaves the nozzle white-hot, flares, then narrows through yellow and orange to a dull red nothing.
    private static readonly ArrayMesh PlumeMesh = Lathe([0, .05f, .12f, .2f, .3f, .45f, .6f, .75f, .9f, 1],
        t => t < .2f ? Mathf.Lerp(1, 1.4f, Mathf.SmoothStep(0, 1, t / .2f)) : Mathf.Lerp(1.4f, .15f, Mathf.Pow((t - .2f) / .8f, .8f)),
        PlumeColor);
    private static Color PlumeColor(float t) => (t < .12f ? PlumeWhite.Lerp(PlumeYellow, t / .12f)
        : t < .4f ? PlumeYellow.Lerp(PlumeOrange, (t - .12f) / .28f) : PlumeOrange.Lerp(PlumeRed, (t - .4f) / .6f)) with { A = .75f * Mathf.Pow(1 - t, 1.4f) };
    // A short blue-white cone of unburnt jet inside the plume.
    private static readonly ArrayMesh CoreMesh = Lathe([0, .1f, .22f, .35f],
        t => .4f * (1 - t / .35f) + .03f, t => new Color(.8f, .88f, 1, .85f * (1 - t / .4f)));
    private static ArrayMesh Lathe(float[] rings, Func<float, float> radius, Func<float, Color> color)
    {
        const int sides = 14;
        var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
        for (var r = 0; r < rings.Length; r++)
            for (var s = 0; s < sides; s++)
            {
                var angle = Mathf.Tau * s / sides;
                vertices.Add(new(radius(rings[r]) * Mathf.Cos(angle), -rings[r], radius(rings[r]) * Mathf.Sin(angle)));
                colors.Add(color(rings[r]));
            }
        for (var r = 0; r < rings.Length - 1; r++)
            for (var s = 0; s < sides; s++)
            {
                int a = r * sides + s, b = r * sides + (s + 1) % sides, c = a + sides, d = b + sides;
                indices.AddRange([a, c, b, b, c, d]);
            }
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
    /// <summary>Exhaust strength over the flight: full at takeoff, idling through the apex, braking before touchdown.</summary>
    public static float JetStrength(float phase) => phase >= 1 ? 0 : phase < .5f ? 1 - 1.3f * phase : .35f + .9f * (phase - .5f);
}
