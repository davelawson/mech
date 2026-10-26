using Godot;

namespace Mech;

public partial class MechModel
{
    // New hulls share the Awesome's flat armor facets, materials and location ownership.
    private MeshInstance3D FacetedHull(MechLocation location, string name, Vector3 at, Vector3 size, Color color)
    {
        const int facets = 10;
        var rings = new[] { (-.5f, .45f), (-.30f, .90f), (.20f, 1f), (.5f, .65f) };
        var faces = new List<Vector3[]>();
        Vector3 Point(int ring, int i)
        {
            var angle = (i + .5f) * Mathf.Tau / facets;
            return new(Mathf.Cos(angle) * size.X * .5f * rings[ring].Item2,
                Mathf.Sin(angle) * size.Y * .5f * rings[ring].Item2, rings[ring].Item1 * size.Z);
        }
        for (var ring = 0; ring < rings.Length - 1; ring++)
            for (var i = 0; i < facets; i++) faces.Add([Point(ring, i), Point(ring, i + 1), Point(ring + 1, i + 1), Point(ring + 1, i)]);
        faces.Add(Enumerable.Range(0, facets).Select(i => Point(0, i)).ToArray());
        faces.Add(Enumerable.Range(0, facets).Select(i => Point(rings.Length - 1, i)).ToArray());
        return Polyhedron(location, name, at, color, faces);
    }
    private MeshInstance3D Wedge(MechLocation location, string name, Vector3 at, Vector3 size, Color color)
    {
        var x = size.X / 2; var y = size.Y / 2; var z = size.Z / 2;
        Vector3 a = new(-x * .8f, -y, -z), b = new(x * .8f, -y, -z), c = new(x * .8f, y * .12f, -z), d = new(-x * .8f, y * .12f, -z);
        Vector3 e = new(-x, -y, z), f = new(x, -y, z), g = new(x, y, z), h = new(-x, y, z);
        return Polyhedron(location, name, at, color, [[a, b, c, d], [e, f, g, h], [a, e, f, b], [d, c, g, h], [a, d, h, e], [b, f, g, c]]);
    }
    private MeshInstance3D Polyhedron(MechLocation location, string name, Vector3 at, Color color, IEnumerable<Vector3[]> faces)
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var points in faces)
        {
            var normal = (points[1] - points[0]).Cross(points[2] - points[0]).Normalized();
            if (normal.Dot(points.Aggregate(Vector3.Zero, (sum, point) => sum + point)) < 0) normal = -normal;
            for (var i = 1; i < points.Length - 1; i++)
            {
                var b = points[i]; var c = points[i + 1];
                if ((b - points[0]).Cross(c - points[0]).Dot(normal) > 0) (b, c) = (c, b);
                foreach (var vertex in new[] { points[0], b, c }) { surface.SetNormal(normal); surface.AddVertex(vertex); }
            }
        }
        var mesh = new MeshInstance3D { Name = name, Position = at, Mesh = surface.Commit(), MaterialOverride = Material(color) };
        _locations[location].AddChild(mesh); return mesh;
    }
    private void Laser(MechLocation location, Vector3 at, int index, float radius = .04f)
    {
        Cylinder(location, $"LaserSocket{index}", at, radius * 1.6f, .04f, Joint);
        Cylinder(location, $"LaserCollar{index}", at + new Vector3(0, 0, -.025f), radius * 1.3f, .025f, Steel);
        Cylinder(location, $"LaserEmitter{index}", at + new Vector3(0, 0, -.042f), radius, .012f, Amber);
    }
    private void MissileRack(MechLocation location, Vector3 at, Vector3 size, int columns, int rows, string name)
    {
        Plate(location, name, at, size, Olive, .045f);
        var front = at + new Vector3(0, 0, -size.Z / 2 - .006f);
        Plate(location, "LauncherFace", front, new(size.X * .88f, size.Y * .88f, .025f), Steel, .015f);
        var pitchX = size.X * .78f / columns; var pitchY = size.Y * .78f / rows;
        var radius = Math.Min(pitchX, pitchY) * .36f;
        for (var row = 0; row < rows; row++) for (var column = 0; column < columns; column++)
        {
            var center = front + new Vector3((column - (columns - 1) / 2f) * pitchX, (row - (rows - 1) / 2f) * pitchY, -.025f);
            Cylinder(location, $"MissileCollar{row * columns + column}", center, radius * 1.2f, .025f, Trim);
            Cylinder(location, $"MissileTube{row * columns + column}", center + new Vector3(0, 0, -.014f), radius, .012f, Joint);
        }
        Plate(location, "LauncherCrown", at + new Vector3(0, size.Y / 2 + .008f, 0), new(size.X * .84f, .03f, size.Z * .8f), LightOlive, .01f);
        for (var i = 0; i < 3; i++)
            Plate(location, $"LauncherRearVent{i}", at + new Vector3(0, (i - 1) * size.Y * .18f, size.Z / 2 + .012f),
                new(size.X * .64f, .025f, .025f), Joint, .004f);
    }
    private MeshInstance3D ArmorLink(MechLocation location, string name, Vector3 from, Vector3 to, float width, float depth, Color color)
    {
        var link = Plate(location, name, (from + to) / 2, new(width, from.DistanceTo(to) * .72f, depth), color, .035f);
        link.Quaternion = new Quaternion(Vector3.Up, (from - to).Normalized()); return link;
    }
    private void Piston(MechLocation location, string name, Vector3 from, Vector3 to, float radius)
    {
        var piston = Cylinder(location, name, (from + to) / 2, radius, from.DistanceTo(to), Steel);
        piston.Quaternion = new Quaternion(Vector3.Up, (from - to).Normalized());
    }
    private void BuildLeg(RigProfile rig, int sign, float width, float footWidth)
    {
        var location = sign < 0 ? MechLocation.LeftLeg : MechLocation.RightLeg;
        Vector3 Mirror(Vector3 point) => new(point.X * sign, point.Y, point.Z);
        var hip = Mirror(rig.Hip); var knee = Mirror(rig.Knee); var ankle = Mirror(rig.Ankle);
        Cylinder(location, "Hip", hip, width * .58f, width * 1.3f, Joint, new(0, 0, 90));
        Cylinder(location, "ThighHipCap", hip + new Vector3(sign * width * .66f, 0, 0), width * .45f, .025f, Steel, new(0, 0, 90));
        ArmorLink(location, "ThighArmor", hip, knee, width, width * 1.1f, Olive);
        ArmorLink(location, "ThighFrontPlate", hip + new Vector3(0, 0, -width * .58f), knee + new Vector3(0, .06f, -width * .58f), width * .7f, .035f, LightOlive);
        Piston(location, "ThighPiston", hip + new Vector3(-sign * width * .45f, -.07f, .08f), knee + new Vector3(-sign * width * .45f, .06f, .08f), .025f);
        Cylinder(location, "Knee", knee, width * .44f, width * 1.3f, Joint, new(0, 0, 90));
        ArmorLink(location, "ShinArmor", knee, ankle, width * .88f, width, Olive);
        Plate(location, "KneeGuard", knee + new Vector3(0, 0, -width * .55f), new(width * .78f, .15f, .08f), LightOlive);
        Piston(location, "ShinPiston", knee + new Vector3(sign * width * .47f, -.06f, .06f), ankle + new Vector3(sign * width * .47f, .04f, .06f), .025f);
        Cylinder(location, "Ankle", ankle, .075f, width * 1.05f, Joint, new(0, 0, 90));
        Plate(location, "Foot", new(ankle.X, .09f, ankle.Z - .04f), new(footWidth, .17f, .36f), DarkOlive, .025f);
        for (var toe = 0; toe < 2; toe++)
            Plate(location, $"ToeCap{toe}", new(ankle.X + (toe == 0 ? -1 : 1) * footWidth * .26f, .075f, ankle.Z - .28f),
                new(footWidth * .42f, .14f, .28f), LightOlive, .025f);
    }
    private void BuildJenner()
    {
        const MechLocation ct = MechLocation.CenterTorso, head = MechLocation.Head;
        Plate(ct, "Pelvis", new(0, 1.27f, .08f), new(.62f, .22f, .43f), Joint);
        Wedge(ct, "CenterHull", new(0, 1.70f, -.02f), new(.46f, .55f, 1.05f), Olive);
        Plate(ct, "RearReactor", new(0, 1.76f, .49f), new(.4f, .32f, .18f), DarkOlive);
        MissileRack(ct, new(0, 2.08f, .14f), new(.38f, .25f, .34f), 2, 2, "Srm4Housing");
        Wedge(head, "CockpitNose", new(0, 1.84f, -.59f), new(.45f, .49f, .66f), DarkOlive);
        var canopy = Plate(head, "AmberCanopy", new(0, 1.970f, -.65f), new(.31f, .035f, .48f), Amber, .012f);
        canopy.RotationDegrees = new(-18, 0, 0);
        var canopyFrame = Plate(head, "CanopyFrame", new(0, 1.987f, -.65f), new(.022f, .045f, .50f), Trim, .004f);
        canopyFrame.RotationDegrees = canopy.RotationDegrees;
        Plate(head, "CockpitChin", new(0, 1.63f, -.93f), new(.26f, .15f, .035f), Olive);
        foreach (var sign in new[] { -1, 1 })
        {
            var torso = sign < 0 ? MechLocation.LeftTorso : MechLocation.RightTorso;
            var arm = sign < 0 ? MechLocation.LeftArm : MechLocation.RightArm;
            Wedge(torso, "WingHull", new(sign * .48f, 1.72f, .02f), new(.51f, .55f, .96f), Olive);
            var top = Plate(torso, "WingTopPlate", new(sign * .47f, 1.875f, -.04f), new(.43f, .045f, .73f), LightOlive);
            top.RotationDegrees = new(-14, 0, 0);
            Vent(torso, new(sign * .49f, 1.69f, -.48f), new(.32f, .16f));
            Plate(torso, "RearCoolingBank", new(sign * .47f, 1.72f, .52f), new(.35f, .27f, .05f), Joint);
            Cylinder(arm, "ArmJoint", new(sign * .78f, 1.79f, .02f), .13f, .20f, Joint, new(0, 0, 90));
            Plate(arm, "LaserPod", new(sign * 1.08f, 1.79f, -.04f), new(.57f, .40f, .73f), Olive, .06f);
            Plate(arm, "PodTopPlate", new(sign * 1.08f, 1.995f, -.02f), new(.46f, .035f, .57f), LightOlive, .012f);
            Plate(arm, "PodFace", new(sign * 1.08f, 1.79f, -.415f), new(.46f, .27f, .035f), Joint);
            for (var laser = 0; laser < 2; laser++) Laser(arm, new(sign * 1.08f + (laser == 0 ? -.115f : .115f), 1.79f, -.445f), laser, .054f);
            BuildLeg(JennerRig, sign, .25f, .34f);
        }
    }
    private void BuildWolverine()
    {
        const MechLocation ct = MechLocation.CenterTorso, head = MechLocation.Head;
        Plate(ct, "Waist", new(0, 1.27f, .02f), new(.38f, .24f, .35f), Joint);
        Plate(ct, "Pelvis", new(0, 1.18f, -.02f), new(.49f, .23f, .39f), DarkOlive);
        Plate(ct, "ChestArmor", new(0, 1.74f, -.01f), new(.47f, .68f, .60f), Olive, .055f);
        Plate(ct, "ChestCrown", new(0, 2.06f, -.06f), new(.43f, .10f, .55f), LightOlive);
        Plate(ct, "Abdomen", new(0, 1.43f, -.16f), new(.32f, .21f, .28f), DarkOlive);
        Plate(ct, "RearReactor", new(0, 1.77f, .37f), new(.42f, .57f, .16f), DarkOlive);
        Plate(head, "Neck", new(0, 2.08f, 0), new(.22f, .13f, .23f), Joint);
        FacetedHull(head, "Helmet", new(0, 2.23f, -.025f), new(.39f, .34f, .41f), Olive);
        Plate(head, "VisorRecess", new(0, 2.24f, -.22f), new(.30f, .10f, .03f), Joint, .012f);
        Plate(head, "AmberVisor", new(0, 2.24f, -.241f), new(.25f, .05f, .012f), Amber, .006f);
        Laser(head, new(.105f, 2.12f, -.215f), 0, .025f);
        foreach (var sign in new[] { -1, 1 })
        {
            var torso = sign < 0 ? MechLocation.LeftTorso : MechLocation.RightTorso;
            var arm = sign < 0 ? MechLocation.LeftArm : MechLocation.RightArm;
            Plate(torso, "SideTorsoArmor", new(sign * .37f, 1.77f, .015f), new(.26f, .61f, .55f), Olive, .045f);
            Vent(torso, new(sign * .36f, 1.75f, -.275f), new(.19f, .18f));
            Plate(torso, "SideTorsoCrown", new(sign * .37f, 2.065f, -.02f), new(.25f, .06f, .46f), LightOlive);
            Cylinder(arm, "ShoulderJoint", new(sign * .59f, 1.99f, .015f), .17f, .20f, Joint, new(0, 0, 90));
            Plate(arm, "ShoulderArmor", new(sign * .76f, 2.01f, .015f), new(.44f, .43f, .49f), Olive, .055f);
            Plate(arm, "ShoulderCrown", new(sign * .76f, 2.23f, .015f), new(.35f, .035f, .39f), LightOlive, .012f);
            Plate(arm, "UpperArm", new(sign * .77f, 1.67f, 0), new(.25f, .31f, .29f), DarkOlive);
            Cylinder(arm, "Elbow", new(sign * .78f, 1.46f, -.04f), .115f, .29f, Steel, new(0, 0, 90));
            if (sign > 0)
            {
                Plate(arm, "BracedForearm", new(.79f, 1.43f, -.28f), new(.29f, .22f, .52f), Olive);
                Plate(arm, "CannonGrip", new(.79f, 1.34f, -.36f), new(.20f, .16f, .21f), Joint);
                Plate(arm, "CannonReceiver", new(.79f, 1.59f, -.47f), new(.25f, .24f, .54f), DarkOlive);
                Cylinder(arm, "CannonBarrel", new(.79f, 1.59f, -.97f), .073f, .52f, Steel);
                Cylinder(arm, "CannonMuzzle", new(.79f, 1.59f, -1.25f), .088f, .07f, Joint);
                Cylinder(arm, "CannonBore", new(.79f, 1.59f, -1.292f), .054f, .012f, new Color("101817"));
            }
            else
            {
                Plate(arm, "FreeForearm", new(-.79f, 1.24f, -.06f), new(.31f, .34f, .34f), Olive);
                Plate(arm, "Fist", new(-.79f, .99f, -.10f), new(.26f, .18f, .27f), Joint);
                for (var finger = 0; finger < 3; finger++)
                    Plate(arm, $"Knuckle{finger}", new(-.88f + finger * .09f, 1, -.245f), new(.065f, .10f, .04f), Steel, .009f);
            }
            BuildLeg(WolverineRig, sign, .34f, .43f);
        }
        MissileRack(MechLocation.LeftTorso, new(-.39f, 2.20f, .06f), new(.32f, .55f, .46f), 2, 3, "Srm6Housing");
    }
}
