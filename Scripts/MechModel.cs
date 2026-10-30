using Godot;

namespace Mech;

/// <summary>One chassis-specific shape, with every detail owned by a damage location.</summary>
public partial class MechModel : Node3D
{
    public IReadOnlyDictionary<MechLocation, Node3D> Locations => _locations;
    public string ChassisId { get; private set; } = "";
    public bool IsAwesome => ChassisId == "awesome";
    public Aabb RestBounds { get; private set; }
    public float Height { get; private set; }
    private readonly Dictionary<MechLocation, Node3D> _locations = [];
    private readonly Dictionary<Color, StandardMaterial3D> _materials = [];
    private readonly Dictionary<string, Node3D[]> _firingMarkers = [];
    private readonly Dictionary<string, int> _firingCounts = [];
    private ShaderMaterial _outline = null!;
    private Color _teamColor;
    private float _outlineWidth = 1.15f;
    public bool Hovered { get; private set; }
    private static readonly Color Olive = new("777749"), LightOlive = new("909063"), DarkOlive = new("555b3e");
    private static readonly Color Joint = new("242d2c"), Steel = new("56605a"), Trim = new("b1a477"), Amber = new("eda82d");

    public static Color TeamColor(CombatSide side) => new(side == CombatSide.Friendly ? "85d0df" : "ee9c85");

    public static MechModel Create(MechSpec spec, CombatSide side)
    {
        var model = new MechModel { Name = "MechModel", ChassisId = spec.Id };
        model.Scale = Vector3.One * (.9f + spec.Tonnage / 200f);
        // A pixel-sized inverted hull remains legible at combat zoom without tinting the armor.
        model._outline = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            render_mode unshaded, cull_front, shadows_disabled;
            uniform vec4 team_color : source_color;
            uniform float outline_width = 1.15;
            void vertex() {
                vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
                vec3 view_normal = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
                vec2 direction = (PROJECTION_MATRIX * vec4(view_normal, 0.0)).xy;
                direction /= max(length(direction), 0.0001);
                clip.xy += direction * (2.0 * outline_width / VIEWPORT_SIZE) * clip.w;
                POSITION = clip;
            }
            void fragment() { ALBEDO = team_color.rgb; }
            """ } };
        model._teamColor = TeamColor(side);
        model._outline.SetShaderParameter("team_color", model._teamColor);
        PreparedModels.TryGetValue(spec.Id, out var prepared);
        if (prepared is not null) model._outlineWidth = .65f;
        model._outline.SetShaderParameter("outline_width", model._outlineWidth);
        foreach (var location in Enum.GetValues<MechLocation>())
        {
            var root = new Node3D { Name = location.ToString() };
            model.AddChild(root); model._locations[location] = root;
        }
        if (prepared is not null)
        {
            model.BuildPreparedModel(prepared.Chassis);
            model.BuildLocomotionRig(prepared.Rig);
        }
        else model.BuildGeneric(side);
        model.BindFiringMarkers(spec);
        model.RestBounds = model.MeasureBounds() ?? new Aabb(Vector3.Zero, Vector3.One);
        model.Height = model.RestBounds.End.Y * model.Scale.Y;
        return model;
    }

    // Re-measure visible animated geometry so lost limbs and fallen wrecks keep fitting their hit area.
    internal Aabb? PickBounds() => MeasureBounds(visibleOnly: true);

    private Aabb? MeasureBounds(bool visibleOnly = false)
    {
        Aabb? bounds = null;
        void Visit(Node3D node, Transform3D transform)
        {
            if (visibleOnly && !node.Visible) return;
            if (node is MeshInstance3D { Mesh: { } mesh })
            {
                var box = transform * mesh.GetAabb();
                bounds = bounds is { } previous ? previous.Merge(box) : box;
            }
            foreach (var child in node.GetChildren().OfType<Node3D>()) Visit(child, transform * child.Transform);
        }
        foreach (var child in GetChildren().OfType<Node3D>()) Visit(child, child.Transform);
        return bounds;
    }

    public void SetHovered(bool hovered)
    {
        if (Hovered == hovered) return;
        Hovered = hovered;
        _outline.SetShaderParameter("team_color", hovered ? _teamColor.Lerp(Colors.White, .5f) : _teamColor);
        _outline.SetShaderParameter("outline_width", hovered ? _outlineWidth + 1.3f : _outlineWidth);
    }

    public void SetCondition(Func<MechLocation, bool> intact)
    {
        foreach (var (location, node) in _locations) node.Visible = intact(location);
    }

    /// <summary>The source firing marker for this weapon's next shot, cycling through launcher tubes.
    /// Null when the model has no marker for it (generic geometry, or a source visual mounted elsewhere).</summary>
    public Node3D? NextFiringMarker(string weaponId)
    {
        if (!_firingMarkers.TryGetValue(weaponId, out var markers)) return null;
        _firingCounts.TryGetValue(weaponId, out var count); _firingCounts[weaponId] = count + 1;
        return markers[count % markers.Length];
    }

    /// <summary>World-space center of a damage location's current pose, including hidden destroyed parts.</summary>
    public Vector3 LocationCenter(MechLocation location)
    {
        Aabb? bounds = null;
        foreach (var mesh in _locations[location].FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
            bounds = bounds is { } previous ? previous.Merge(box) : box;
        }
        return bounds?.GetCenter() ?? _locations[location].GlobalPosition;
    }

    private static bool IsFiringMarker(string name) => name.StartsWith("WeaponMuzzle_", StringComparison.Ordinal)
        || name.StartsWith("MissileTube", StringComparison.Ordinal) || name.StartsWith("LrmTube", StringComparison.Ordinal)
        || name == "CannonMuzzle";

    private static string MarkerKind(string name) =>
        name.StartsWith("MissileTube", StringComparison.Ordinal) ? "SRM"
        : name.StartsWith("LrmTube", StringComparison.Ordinal) ? "LRM"
        : name == "CannonMuzzle" ? "AC"
        : name.Contains("Ppc", StringComparison.Ordinal) ? "PPC"
        : name.Contains("MachineGun", StringComparison.Ordinal) ? "MG"
        : name.Contains("Flamer", StringComparison.Ordinal) ? "Flamer"
        : name.Contains("Laser", StringComparison.Ordinal) ? "Laser" : "";

    private static string MarkerKind(WeaponKind kind) => kind switch
    {
        WeaponKind.SRM2 or WeaponKind.SRM4 or WeaponKind.SRM6 => "SRM",
        WeaponKind.LRM5 or WeaponKind.LRM10 or WeaponKind.LRM15 or WeaponKind.LRM20 => "LRM",
        WeaponKind.AC2 or WeaponKind.AC5 or WeaponKind.AC10 or WeaponKind.AC20 => "AC",
        WeaponKind.PPC => "PPC", WeaponKind.MachineGun => "MG", WeaponKind.Flamer => "Flamer",
        _ => "Laser"
    };

    // Indexed markers (`WeaponMuzzle_Weapon<i>_*`) name their catalog weapon directly. Otherwise each location's
    // weapons share its markers of the matching kind in catalog order: one beam each, launcher tubes split evenly.
    private void BindFiringMarkers(MechSpec spec)
    {
        static (string Stem, int Number) Order(Node3D node)
        {
            var name = node.Name.ToString(); var stem = name.TrimEnd("0123456789".ToCharArray());
            return (stem, int.TryParse(name[stem.Length..], out var number) ? number : -1);
        }
        var all = _locations.Values.SelectMany(root => root.FindChildren("*", "Node3D", true, false).OfType<Node3D>()
            .Where(node => node is not MeshInstance3D && IsFiringMarker(node.Name))
            .OrderBy(node => Order(node).Stem, StringComparer.Ordinal).ThenBy(node => Order(node).Number)).ToList();
        var claimed = new HashSet<Node3D>();
        var pending = new List<WeaponSpec>();
        for (var index = 0; index < spec.Weapons.Length; index++)
        {
            var own = all.Where(node => node.Name.ToString().StartsWith($"WeaponMuzzle_Weapon{index}_", StringComparison.Ordinal)).ToArray();
            if (own.Length > 0) { _firingMarkers[spec.Weapons[index].Id] = own; claimed.UnionWith(own); }
            else pending.Add(spec.Weapons[index]);
        }
        void Share(IReadOnlyList<WeaponSpec> weapons, IReadOnlyList<Node3D> markers)
        {
            if (weapons.Count == 0 || markers.Count == 0) return;
            var each = Math.Max(1, markers.Count / weapons.Count);
            for (var i = 0; i < weapons.Count; i++)
                _firingMarkers[weapons[i].Id] = markers.Count >= weapons.Count
                    ? markers.Skip(i * each).Take(i == weapons.Count - 1 ? markers.Count - i * each : each).ToArray()
                    : [markers[i % markers.Count]];
            claimed.UnionWith(markers);
        }
        foreach (var location in pending.GroupBy(weapon => weapon.Location))
        {
            var markers = all.Where(node => _locations[location.Key].IsAncestorOf(node)).ToArray();
            foreach (var kind in location.GroupBy(weapon => MarkerKind(weapon.Kind)))
                Share(kind.ToArray(), markers.Where(node => !claimed.Contains(node) && MarkerKind(node.Name) == kind.Key).ToArray());
            var unmatched = location.Where(weapon => !_firingMarkers.ContainsKey(weapon.Id)).ToArray();
            var spare = markers.Where(node => !claimed.Contains(node)).ToArray();
            Share(unmatched, spare.Length > 0 ? spare : markers);
        }
        // A catalog mount with no source visual in its location (Crusader leg SRMs) uses unclaimed markers of its kind.
        foreach (var kind in pending.Where(weapon => !_firingMarkers.ContainsKey(weapon.Id)).GroupBy(weapon => MarkerKind(weapon.Kind)))
            Share(kind.ToArray(), all.Where(node => !claimed.Contains(node) && MarkerKind(node.Name) == kind.Key).ToArray());
    }

    private StandardMaterial3D Material(Color color)
    {
        if (_materials.TryGetValue(color, out var material)) return material;
        material = new StandardMaterial3D { AlbedoColor = color, Roughness = .78f,
            Metallic = .28f, NextPass = _outline };
        _materials[color] = material; return material;
    }

    private MeshInstance3D Plate(MechLocation location, string name, Vector3 at, Vector3 size,
        Color color, float bevel = .035f)
    {
        var mesh = new MeshInstance3D { Name = name, Position = at,
            Mesh = BeveledBox(size, bevel), MaterialOverride = Material(color) };
        _locations[location].AddChild(mesh); return mesh;
    }

    // Flat face normals preserve the deliberate, chunky armor facets.
    private static ArrayMesh BeveledBox(Vector3 size, float bevel)
    {
        var h = size / 2; var b = Math.Min(bevel, Math.Min(h.X, Math.Min(h.Y, h.Z)) * .7f);
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        void Face(params Vector3[] points)
        {
            var normal = (points[1] - points[0]).Cross(points[2] - points[0]).Normalized();
            if (normal.Dot(points.Aggregate(Vector3.Zero, (sum, p) => sum + p)) < 0) normal = -normal;
            for (var i = 1; i < points.Length - 1; i++)
            {
                var a = points[0]; var c = points[i]; var d = points[i + 1];
                if ((c - a).Cross(d - a).Dot(normal) > 0) (c, d) = (d, c);
                foreach (var vertex in new[] { a, c, d }) { surface.SetNormal(normal); surface.AddVertex(vertex); }
            }
        }
        for (var axis = 0; axis < 3; axis++)
        {
            var u = (axis + 1) % 3; var v = (axis + 2) % 3;
            Vector3 Point(float a, float c, float d)
            { var p = Vector3.Zero; p[axis] = a; p[u] = c; p[v] = d; return p; }
            foreach (var sign in new[] { -1, 1 })
                Face(Point(sign * h[axis], -h[u] + b, -h[v] + b), Point(sign * h[axis], h[u] - b, -h[v] + b),
                    Point(sign * h[axis], h[u] - b, h[v] - b), Point(sign * h[axis], -h[u] + b, h[v] - b));
            foreach (var s in new[] { -1, 1 }) foreach (var t in new[] { -1, 1 })
                Face(Point(-h[axis] + b, s * h[u], t * (h[v] - b)), Point(h[axis] - b, s * h[u], t * (h[v] - b)),
                    Point(h[axis] - b, s * (h[u] - b), t * h[v]), Point(-h[axis] + b, s * (h[u] - b), t * h[v]));
        }
        foreach (var x in new[] { -1, 1 }) foreach (var y in new[] { -1, 1 }) foreach (var z in new[] { -1, 1 })
            Face(new(x * h.X, y * (h.Y - b), z * (h.Z - b)),
                new(x * (h.X - b), y * h.Y, z * (h.Z - b)), new(x * (h.X - b), y * (h.Y - b), z * h.Z));
        return surface.Commit();
    }

    private MeshInstance3D Cylinder(MechLocation location, string name, Vector3 at,
        float radius, float depth, Color color, Vector3? rotation = null)
    {
        var mesh = new MeshInstance3D { Name = name, Position = at, RotationDegrees = rotation ?? new Vector3(90, 0, 0),
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = depth, RadialSegments = 16 },
            MaterialOverride = Material(color) };
        _locations[location].AddChild(mesh); return mesh;
    }
    private void Vent(MechLocation location, Vector3 at, Vector2 size, int slats = 4)
    {
        Plate(location, "VentRecess", at, new(size.X, size.Y, .035f), Joint, .012f);
        for (var i = 0; i < slats; i++)
            Plate(location, "VentSlat", at + new Vector3(0, (i - (slats - 1) / 2f) * size.Y / (slats + 1), -.024f),
                new(size.X * .82f, .013f, .018f), Steel, .003f);
    }
    private void BuildGeneric(CombatSide side)
    {
        var color = side == CombatSide.Friendly ? new Color("6bafbd") : new Color("d17c66");
        Plate(MechLocation.CenterTorso, "Torso", new(0, 1.45f, 0), new(.5f, .75f, .5f), color);
        Plate(MechLocation.Head, "Head", new(0, 2, -.08f), new(.3f, .28f, .35f), Ui.Gold);
        Plate(MechLocation.Head, "Cockpit", new(0, 1.98f, -.265f), new(.22f, .09f, .025f), Joint, .006f);
        foreach (var sign in new[] { -1, 1 })
        {
            Plate(sign < 0 ? MechLocation.LeftTorso : MechLocation.RightTorso, "SideTorso",
                new(sign * .4f, 1.5f, 0), new(.3f, .65f, .5f), color.Darkened(.12f));
            Plate(sign < 0 ? MechLocation.LeftArm : MechLocation.RightArm, "Arm",
                new(sign * .7f, 1.25f, -.1f), new(.22f, .8f, .35f), color);
            Plate(sign < 0 ? MechLocation.LeftLeg : MechLocation.RightLeg, "Leg",
                new(sign * .23f, .55f, 0), new(.26f, 1.05f, .4f), color.Darkened(.25f));
        }
    }
}
