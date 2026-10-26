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
    private ShaderMaterial _outline = null!;
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
            void vertex() {
                vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
                vec3 view_normal = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
                vec2 direction = (PROJECTION_MATRIX * vec4(view_normal, 0.0)).xy;
                direction /= max(length(direction), 0.0001);
                clip.xy += direction * (2.0 * 1.15 / VIEWPORT_SIZE) * clip.w;
                POSITION = clip;
            }
            void fragment() { ALBEDO = team_color.rgb; }
            """ } };
        model._outline.SetShaderParameter("team_color", TeamColor(side));
        foreach (var location in Enum.GetValues<MechLocation>())
        {
            var root = new Node3D { Name = location.ToString() };
            model.AddChild(root); model._locations[location] = root;
        }
        switch (spec.Id)
        {
            case "awesome": model.BuildAwesome(); model.BuildLocomotionRig(AwesomeRig); break;
            case "jenner": model.BuildJenner(); model.BuildLocomotionRig(JennerRig); break;
            case "wolverine": model.BuildWolverine(); model.BuildLocomotionRig(WolverineRig); break;
            case "catapult": model.BuildCatapult(); model.BuildLocomotionRig(CatapultRig); break;
            default: model.BuildGeneric(side); break;
        }
        model.RestBounds = model.MeasureBounds();
        model.Height = model.RestBounds.End.Y * model.Scale.Y;
        return model;
    }

    private Aabb MeasureBounds()
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
        foreach (var child in GetChildren().OfType<Node3D>()) Visit(child, child.Transform);
        return bounds ?? new Aabb(Vector3.Zero, Vector3.One);
    }

    public void SetCondition(Func<MechLocation, bool> intact)
    {
        foreach (var (location, node) in _locations) node.Visible = intact(location);
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
    private void Ppc(MechLocation location, Vector3 at, bool longBarrel)
    {
        Cylinder(location, "PpcSocket", at, .157f, .065f, Joint);
        Cylinder(location, "PpcCollar", at + new Vector3(0, 0, -.035f), .131f, .055f, Trim);
        var length = longBarrel ? .23f : .07f;
        Cylinder(location, "PpcBarrel", at + new Vector3(0, 0, -.07f - length / 2), .113f, length, Steel);
        Cylinder(location, "PpcMuzzle", at + new Vector3(0, 0, -.08f - length), .093f, .022f, Joint);
        Cylinder(location, "PpcEmitter", at + new Vector3(0, 0, -.094f - length), .048f, .012f, Amber);
    }
    private void BuildAwesome()
    {
        const MechLocation ct = MechLocation.CenterTorso, head = MechLocation.Head;
        Plate(ct, "WaistJoint", new(0, 1.30f, .03f), new(.43f, .22f, .39f), Joint);
        Plate(ct, "Pelvis", new(0, 1.36f, -.06f), new(.43f, .30f, .45f), DarkOlive);
        Plate(ct, "ReactorHousing", new(0, 1.91f, .14f), new(.47f, .79f, .64f), DarkOlive, .065f);
        Plate(ct, "ChestArmor", new(0, 1.96f, -.16f), new(.45f, .57f, .44f), Olive, .06f);
        Plate(ct, "ChestBrow", new(0, 2.23f, -.19f), new(.47f, .10f, .42f), LightOlive);
        Plate(ct, "Abdomen", new(0, 1.58f, -.21f), new(.36f, .22f, .32f), Olive);
        Vent(ct, new(0, 1.68f, -.398f), new(.29f, .13f));
        // Rear reactor and three separate torso seams are designed extensions of the front reference.
        Plate(ct, "RearReactor", new(0, 1.99f, .485f), new(.38f, .53f, .08f), Olive);
        for (var i = 0; i < 5; i++)
            Plate(ct, "RearCoolingSlat", new(0, 1.82f + i * .075f, .536f), new(.28f, .025f, .025f), Joint, .004f);
        Plate(head, "Neck", new(0, 2.26f, -.025f), new(.26f, .16f, .25f), Joint);
        Plate(head, "CockpitHousing", new(0, 2.37f, -.055f), new(.34f, .29f, .36f), Olive, .05f);
        Plate(head, "VisorRecess", new(0, 2.37f, -.241f), new(.265f, .115f, .025f), Joint, .013f);
        Plate(head, "AmberVisor", new(0, 2.37f, -.258f), new(.21f, .067f, .012f), Amber, .008f);
        Plate(head, "VisorDivider", new(.025f, 2.37f, -.270f), new(.018f, .082f, .014f), Trim, .003f);
        Cylinder(head, "SmallLaser", new(.11f, 2.27f, -.252f), .022f, .04f, Joint);
        foreach (var sign in new[] { -1, 1 })
        {
            var torso = sign < 0 ? MechLocation.LeftTorso : MechLocation.RightTorso;
            var arm = sign < 0 ? MechLocation.LeftArm : MechLocation.RightArm;
            var leg = sign < 0 ? MechLocation.LeftLeg : MechLocation.RightLeg;
            var x = sign * .405f;
            Plate(torso, "SideTorsoArmor", new(x, 1.94f, .015f), new(.33f, .69f, .65f), Olive, .055f);
            Plate(torso, "TorsoTopPlate", new(x, 2.28f, -.015f), new(.32f, .09f, .53f), LightOlive);
            Plate(torso, "TorsoRearPlate", new(x, 1.96f, .37f), new(.30f, .57f, .08f), DarkOlive);
            Ppc(torso, new(x, 1.95f, -.329f), false);
            Plate(torso, "LowerTorsoPlate", new(x, 1.65f, -.12f), new(.29f, .13f, .41f), DarkOlive);
            Cylinder(arm, "ShoulderJoint", new(sign * .66f, 2.12f, .015f), .20f, .23f, Joint, new(0, 0, 90));
            var shoulder = Plate(arm, "ShoulderSlab", new(sign * .91f, 2.17f, .025f), new(.65f, .64f, .63f), Olive, .065f);
            shoulder.RotationDegrees = new(0, 0, -sign * 6);
            Plate(arm, "ShoulderCrown", new(sign * .91f, 2.48f, .025f), new(.51f, .05f, .48f), LightOlive, .014f);
            Plate(arm, "ShoulderPanel", new(sign * 1.00f, 2.16f, -.302f), new(.29f, .40f, .025f), DarkOlive, .018f);
            Vent(arm, new(sign * .75f, 2.31f, -.305f), new(.13f, .20f), 5);
            Plate(arm, "UpperArm", new(sign * .96f, 1.72f, .01f), new(.30f, .37f, .34f), DarkOlive);
            Cylinder(arm, "Elbow", new(sign * .96f, 1.50f, -.02f), .145f, .34f, Steel, new(0, 0, 90));
            if (sign > 0)
            {
                Plate(arm, "PpcForearm", new(.97f, 1.39f, -.27f), new(.39f, .34f, .57f), Olive, .055f);
                Plate(arm, "ForearmTopPlate", new(.97f, 1.58f, -.27f), new(.32f, .05f, .44f), LightOlive);
                Ppc(arm, new(.97f, 1.39f, -.57f), true);
            }
            else
            {
                Plate(arm, "LeftForearm", new(-.98f, 1.30f, -.08f), new(.35f, .40f, .38f), Olive, .05f);
                Plate(arm, "Fist", new(-.98f, 1.03f, -.10f), new(.29f, .18f, .28f), Joint);
                for (var finger = 0; finger < 3; finger++)
                    Plate(arm, "Knuckle", new(-1.075f + finger * .09f, 1.04f, -.255f), new(.065f, .12f, .07f), Steel, .012f);
            }
            Cylinder(leg, "Hip", new(sign * .27f, 1.20f, .025f), .16f, .28f, Joint, new(0, 0, 90));
            var thigh = Plate(leg, "ThighArmor", new(sign * .30f, 1.08f, .025f), new(.39f, .39f, .43f), Olive, .05f);
            thigh.RotationDegrees = new(0, 0, sign * 10);
            Plate(leg, "ThighFrontPlate", new(sign * .30f, 1.11f, -.215f), new(.29f, .25f, .05f), LightOlive);
            Cylinder(leg, "Knee", new(sign * .35f, .85f, -.015f), .14f, .43f, Joint, new(0, 0, 90));
            Plate(leg, "ShinArmor", new(sign * .36f, .57f, -.02f), new(.43f, .55f, .47f), Olive, .06f);
            Plate(leg, "KneeGuard", new(sign * .36f, .82f, -.25f), new(.32f, .18f, .10f), LightOlive);
            Vent(leg, new(sign * .36f, .43f, -.265f), new(.24f, .095f), 3);
            Plate(leg, "Ankle", new(sign * .36f, .25f, 0), new(.27f, .15f, .31f), Joint);
            Plate(leg, "Foot", new(sign * .37f, .12f, -.16f), new(.49f, .23f, .70f), DarkOlive, .05f);
            Plate(leg, "ToeCap", new(sign * .37f, .11f, -.49f), new(.43f, .17f, .10f), LightOlive);
        }
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
