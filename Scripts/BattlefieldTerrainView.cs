using Godot;

namespace Mech;

/// <summary>Procedural battlefield visuals, built exactly over the simulation's heightfield, patch footprints and buildings.</summary>
public static class BattlefieldTerrainView
{
    /// <summary>Colours and lighting for one biome.</summary>
    internal sealed record Palette(Color Low, Color High, float HighAt, Color Steep, Color Forest, Color Bed, Color Grid, Color Rock, Color RockTop,
        Color Foliage, Color Trunk, Color Water, Color Sky, Color Horizon, Color Ambient, float AmbientEnergy, Vector3 Sun, Color SunColor,
        float SunEnergy, Color Backdrop);

    internal static Palette For(Biome? biome) => biome switch
    {
        Biome.Arctic => new(new("c3ccd3"), new("f4f7f9"), 240, new("6a6e73"), new("aeb9b6"), new("c9dde6"), new("8796a3"),
            new("54585e"), new("eef2f4"), new("2f4a3c"), new("4b3a2c"), new("cfe6f0"), new("b9c6d0"), new("e3e8ec"), new("c9d6e2"), .8f,
            new(-28, -40, 0), new("f2f0ea"), .9f, new("39424a")),
        Biome.Lunar => new(new("6c6a66"), new("a6a39d"), 120, new("57554f"), new("6c6a66"), new("6c6a66"), new("4f5257"),
            new("5f5d58"), new("7d7a73"), new("6c6a66"), new("6c6a66"), new("6c6a66"), new("020306"), new("05070b"), new("4a5058"), .35f,
            new(-32, 35, 0), new("ffffff"), 1.6f, new("050608")),
        Biome.Highlands => new(new("6b7450"), new("a9a58f"), 320, new("6f6a63"), new("4f5e3a"), new("4a5560"), new("7b8270"),
            new("66625c"), new("8a857c"), new("3c5a36"), new("4b3a2c"), new("3d6e8c"), new("4f86c6"), new("c9d8e6"), new("b4c2cc"), .6f,
            new(-50, -35, 0), new("fff6e6"), 1.2f, new("1f2a28")),
        Biome.Desert => new(new("c9a46a"), new("e2c48e"), 120, new("a5754a"), new("8e8a4e"), new("6f7a5a"), new("a88a5c"),
            new("9a6a44"), new("c08a5a"), new("6f7a3e"), new("6b5233"), new("3a7f8a"), new("8fb3d6"), new("f0e2c8"), new("e0cfb0"), .7f,
            new(-62, -20, 0), new("fff0d0"), 1.35f, new("3a2c1e")),
        Biome.Tropical => new(new("3f6a2e"), new("6f8a4a"), 170, new("6e5a3e"), new("254a22"), new("3e4d3a"), new("5f7a50"),
            new("5f5f55"), new("7a7a6c"), new("2a6a2a"), new("5a4330"), new("3a6e5e"), new("7fa9c9"), new("dfe8e0"), new("b8c8b4"), .7f,
            new(-60, -40, 0), new("fff2d8"), 1.1f, new("17241a")),
        Biome.Grassland => new(new("8a9a52"), new("b0a868"), 60, new("8a7a5a"), new("4f6a35"), new("56604a"), new("8a9068"),
            new("77716a"), new("948e82"), new("4a7038"), new("5a4330"), new("3a7598"), new("76a8d8"), new("dbe6ec"), new("bcc8cc"), .65f,
            new(-50, -25, 0), new("fff4e0"), 1.15f, new("222c20")),
        // Sandy shores rise to green island interiors.
        Biome.Archipelago => new(new("d6c79a"), new("5e7a44"), 60, new("7a7060"), new("3a6430"), new("c2b688"), new("8a8a70"),
            new("6e6a62"), new("8c877c"), new("3c7a3a"), new("6b5233"), new("2a7ea0"), new("79b4e0"), new("e2eef4"), new("bcccd6"), .7f,
            new(-55, -30, 0), new("fff6e8"), 1.15f, new("0f2a3a")),
        Biome.Volcanic => new(new("3a3634"), new("57514c"), 220, new("2a2624"), new("3a3634"), new("2a1a14"), new("6a5a50"),
            new("2e2b2a"), new("4a4542"), new("3a3634"), new("3a3634"), new("ff6a1a"), new("7a5a50"), new("c09070"), new("a08878"), .55f,
            new(-40, -30, 0), new("ffc89a"), 1, new("1a1210")),
        Biome.Toxic => new(new("6a6a3e"), new("8f8a52"), 160, new("6a5a3a"), new("5a4a5e"), new("3a4a2a"), new("7a7a50"),
            new("5e5848"), new("7c7560"), new("7a5a8a"), new("4a4038"), new("9ab83a"), new("a8b070"), new("d8d8a0"), new("c0c09a"), .6f,
            new(-45, -30, 0), new("f0f0b0"), 1, new("23251a")),
        _ => new(new("566a3e"), new("8d8c66"), 240, new("7d7462"), new("3e5c30"), new("4d5a49"), new("6e7b5f"),
            new("6b665c"), new("857f72"), new("3d6b35"), new("5a4330"), new("2f6f8f"), new("6d9fd0"), new("cfdde6"), new("b1c1cc"), .65f,
            new(-55, -30, 0), new("fff4e0"), 1.1f, new("1d2a22")),
    };

    private const string GroundShader = """
        shader_type spatial;
        render_mode cull_disabled;
        uniform float detail = 0.14;
        varying vec3 world_position;
        float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float noise(vec2 p)
        {
            vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
        }
        void vertex() { world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
        void fragment()
        {
            vec2 p = world_position.xz;
            float n = noise(p / 140.0) * 0.45 + noise(p / 31.0) * 0.35 + noise(p / 7.0) * 0.2;
            ALBEDO = COLOR.rgb * (1.0 - detail + 2.0 * detail * n);
            ROUGHNESS = 0.95;
        }
        """;
    private const string StarSkyShader = """
        shader_type sky;
        float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        // The battlefield camera is orthographic, so every pixel shares one eye direction: place stars on the screen.
        void sky()
        {
            vec2 cell = floor(SCREEN_UV * vec2(480.0, 300.0));
            float star = step(0.9965, hash(cell)) * hash(cell + 3.1);
            COLOR = vec3(0.004, 0.006, 0.012) + vec3(star * 1.4);
        }
        """;

    public static Node3D Create(BattlefieldTerrain terrain)
    {
        var palette = For(terrain.Biome);
        var root = new Node3D { Name = "Terrain" };
        root.AddChild(Ground(terrain, palette));
        root.AddChild(Grid(terrain, palette));
        var random = new Random(terrain.Seed);
        // Unique names keep repeated features identifiable; Godot would otherwise rename duplicates by type.
        for (var i = 0; i < terrain.Patches.Count; i++)
        {
            var patch = terrain.Patches[i];
            Node3D? feature = patch.Kind switch
            {
                TerrainKind.Rock => Rock(terrain, patch, palette, random),
                _ when BattlefieldTerrain.IsFlat(patch.Kind) => Surface(terrain, patch, palette),
                _ => null,
            };
            if (feature is null) continue;
            feature.Name = $"{patch.Kind}{i}"; root.AddChild(feature);
        }
        foreach (var liquid in Liquids(terrain, palette)) root.AddChild(liquid);
        if (terrain.Streets.Count > 0) root.AddChild(Streets(terrain));
        if (terrain.Roads.Count > 0) root.AddChild(Roads(terrain));
        for (var i = 0; i < terrain.Bridges.Count; i++)
        {
            var bridge = Bridge(terrain, terrain.Bridges[i], palette);
            bridge.Name = $"Bridge{i}"; root.AddChild(bridge);
        }
        if (terrain.Patches.Any(patch => patch.Kind == TerrainKind.Vegetation)) root.AddChild(Trees(terrain, palette, random));
        for (var i = 0; i < terrain.Buildings.Count; i++)
        {
            var building = Building(terrain, terrain.Buildings[i], palette);
            building.Name = $"{terrain.Buildings[i].Style}{i}"; root.AddChild(building);
        }
        return root;
    }

    /// <summary>Sky, sun and ambient light for the battlefield's biome.</summary>
    public static (DirectionalLight3D Sun, Godot.Environment Environment) Lighting(Biome? biome)
    {
        var palette = For(biome);
        var sun = new DirectionalLight3D { RotationDegrees = palette.Sun, LightColor = palette.SunColor, LightEnergy = palette.SunEnergy };
        Material sky = biome == Biome.Lunar ? new ShaderMaterial { Shader = new Shader { Code = StarSkyShader } }
            : new ProceduralSkyMaterial { SkyTopColor = palette.Sky, SkyHorizonColor = palette.Horizon,
                // Below the horizon the orthographic camera sees past the arena edge: keep it a dark, quiet backdrop.
                GroundHorizonColor = palette.Backdrop, GroundBottomColor = palette.Backdrop, SunAngleMax = 20 };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky, Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = palette.Ambient, AmbientLightEnergy = palette.AmbientEnergy,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        return (sun, environment);
    }

    private static MeshInstance3D Ground(BattlefieldTerrain terrain, Palette palette)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        // The ground itself: bridge decks are drawn over it.
        Vector3 Point(float x, float z) => new(x, terrain.GroundAt(new Vector2(x, z)), z);
        Vector3 Normal(float x, float z)
        {
            var at = new Vector2(x, z);
            return new Vector3(terrain.GroundAt(at - new Vector2(15, 0)) - terrain.GroundAt(at + new Vector2(15, 0)), 30,
                terrain.GroundAt(at - new Vector2(0, 15)) - terrain.GroundAt(at + new Vector2(0, 15))).Normalized();
        }
        Color ColorAt(Vector3 point)
        {
            var at = new Vector2(point.X, point.Z);
            if (terrain.Patches.Any(patch => BattlefieldTerrain.IsFlat(patch.Kind) && at.DistanceSquaredTo(patch.Center) <= patch.Radius * patch.Radius)
                || terrain.LiquidAt(at) is not null) return palette.Bed;
            if (terrain.Contains(TerrainKind.Vegetation, at)) return palette.Forest;
            var slope = terrain.SlopeAt(at);
            var ground = palette.Low.Lerp(palette.High, Math.Clamp(point.Y / palette.HighAt, 0, 1));
            return ground.Lerp(palette.Steep, Math.Clamp((slope - BattlefieldTerrain.MaxSlope * .6f) / (BattlefieldTerrain.MaxSlope * .6f), 0, 1));
        }
        // One quad per heightfield cell, so the mesh matches the simulation's triangulation.
        const float cell = BattlefieldTerrain.CellSize;
        var halfCells = (BattlefieldTerrain.GridSize - 1) / 2;
        for (var iz = -halfCells; iz <= halfCells; iz++) for (var ix = -halfCells; ix <= halfCells; ix++)
        {
            var point = Point(ix * cell, iz * cell);
            // The ground shader reads vertex colours as linear albedo.
            tool.SetNormal(Normal(point.X, point.Z)); tool.SetColor(ColorAt(point).SrgbToLinear()); tool.AddVertex(point);
        }
        var side = BattlefieldTerrain.GridSize;
        for (var z = 0; z < side - 1; z++) for (var x = 0; x < side - 1; x++)
        {
            int a = z * side + x, b = a + 1, c = a + side, d = c + 1;
            // Wound to face upward, so the double-sided shader lights the top with the given normals.
            tool.AddIndex(a); tool.AddIndex(b); tool.AddIndex(c);
            tool.AddIndex(d); tool.AddIndex(c); tool.AddIndex(b);
        }
        return new MeshInstance3D
        {
            Name = "Ground", Mesh = tool.Commit(),
            MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = GroundShader } }
        };
    }

    private static MeshInstance3D Grid(BattlefieldTerrain terrain, Palette palette)
    {
        Vector3 Ground(float x, float z) => new(x, terrain.HeightAt(new Vector2(x, z)), z);
        const float cell = BattlefieldTerrain.CellSize;
        var halfCells = (BattlefieldTerrain.GridSize - 1) / 2;
        var grid = new ImmediateMesh();
        grid.SurfaceBegin(Mesh.PrimitiveType.Lines, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = palette.Grid with { A = .55f },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        });
        for (var n = -halfCells; n <= halfCells; n += 8) for (var step = -halfCells; step < halfCells; step++)
        {
            float line = n * cell, from = step * cell, to = from + cell;
            foreach (var point in new[] { Ground(line, from), Ground(line, to), Ground(from, line), Ground(to, line) })
                grid.SurfaceAddVertex(point + Vector3.Up * .75f);
        }
        grid.SurfaceEnd();
        return new MeshInstance3D { Name = "Grid", Mesh = grid };
    }

    /// <summary>A faceted low-poly rock that stays inside its simulation footprint and reaches its simulated top.</summary>
    private static MeshInstance3D Rock(BattlefieldTerrain terrain, TerrainPatch rock, Palette palette, Random random)
    {
        var ground = terrain.HeightAt(rock.Center); var bottom = Math.Min(-15, ground - 15); var top = ground + rock.Height;
        var sides = 9; var boulder = terrain.Biome == Biome.Lunar;
        // Ring heights as fractions from bottom to top, with radius fractions that taper upward.
        float[] levels = boulder ? [0, .35f, .7f, .92f] : [0, .55f, .85f, .97f];
        float[] widths = boulder ? [.9f, 1, .8f, .45f] : [1, .92f, .75f, .5f];
        var rings = levels.Select((level, ring) => Enumerable.Range(0, sides).Select(index =>
        {
            var angle = (index + random.NextSingle() * .4f) * Mathf.Tau / sides;
            var radius = rock.Radius * widths[ring] * (.82f + random.NextSingle() * .18f);
            var height = ring == 0 ? bottom : Mathf.Lerp(ground, top, level) + (random.NextSingle() - .5f) * rock.Height * .06f;
            return new Vector3(rock.Center.X + MathF.Cos(angle) * radius, height, rock.Center.Y + MathF.Sin(angle) * radius);
        }).ToArray()).ToArray();
        var peak = new Vector3(rock.Center.X + (random.NextSingle() - .5f) * rock.Radius * .3f, top, rock.Center.Y + (random.NextSingle() - .5f) * rock.Radius * .3f);
        var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetSmoothGroup(uint.MaxValue); // Flat facets, not a smoothed blob.
        Color Shade(Vector3 point) => palette.Rock.Lerp(palette.RockTop, Math.Clamp((point.Y - ground) / Math.Max(1, top - ground) * 1.6f - .9f, 0, 1));
        void Triangle(Vector3 a, Vector3 b, Vector3 c) { foreach (var point in new[] { a, b, c }) { tool.SetColor(Shade(point)); tool.AddVertex(point); } }
        for (var ring = 0; ring < rings.Length - 1; ring++) for (var index = 0; index < sides; index++)
        {
            var next = (index + 1) % sides;
            Triangle(rings[ring][index], rings[ring + 1][index], rings[ring][next]);
            Triangle(rings[ring][next], rings[ring + 1][index], rings[ring + 1][next]);
        }
        for (var index = 0; index < sides; index++) Triangle(rings[^1][index], peak, rings[^1][(index + 1) % sides]);
        tool.GenerateNormals();
        return new MeshInstance3D
        {
            Name = "Rock", Mesh = tool.Commit(),
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled }
        };
    }

    /// <summary>A flat surface over water, ice, lava or deep water. Lava glows; deep water is darker and opaque.</summary>
    private static MeshInstance3D Surface(BattlefieldTerrain terrain, TerrainPatch patch, Palette palette)
    {
        var opaque = patch.Kind is TerrainKind.Ice or TerrainKind.Lava or TerrainKind.DeepWater;
        var color = patch.Kind switch
        {
            TerrainKind.DeepWater => palette.Water.Darkened(.45f),
            TerrainKind.Water => palette.Water with { A = .82f },
            _ => palette.Water,
        };
        return new MeshInstance3D
        {
            Name = patch.Kind.ToString(),
            Position = new Vector3(patch.Center.X, terrain.HeightAt(patch.Center) + 1.5f, patch.Center.Y),
            Mesh = new CylinderMesh { TopRadius = patch.Radius, BottomRadius = patch.Radius, Height = .5f, RadialSegments = 48, Rings = 1 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color, Roughness = patch.Kind == TerrainKind.Ice ? .2f : patch.Kind == TerrainKind.Lava ? .7f : .08f,
                Metallic = patch.Kind is TerrainKind.Water or TerrainKind.DeepWater ? .1f : 0,
                EmissionEnabled = patch.Kind == TerrainKind.Lava, Emission = palette.Water, EmissionEnergyMultiplier = 1.4f,
                Transparency = opaque ? BaseMaterial3D.TransparencyEnum.Disabled : BaseMaterial3D.TransparencyEnum.Alpha
            }
        };
    }

    /// <summary>One surface per lake, river or sea over the grid triangles it wets, at its own level: flat for lakes and the sea, falling
    /// with a river's course. The surface runs on under the banks, so the ground cuts the shoreline along its contour.
    /// Water darkens and turns opaque with depth; ice is opaque; lava glows.</summary>
    private static IEnumerable<MeshInstance3D> Liquids(BattlefieldTerrain terrain, Palette palette)
    {
        if (terrain.Liquids.Count == 0) yield break;
        var side = BattlefieldTerrain.GridSize; var half = (side - 1) / 2;
        var triangles = terrain.Liquids.Select(_ => new List<(int X, int Z)[]>()).ToArray();
        for (var z = 0; z < side - 1; z++) for (var x = 0; x < side - 1; x++)
            foreach (var triangle in new[] { new[] { (x, z), (x + 1, z), (x, z + 1) }, new[] { (x + 1, z + 1), (x, z + 1), (x + 1, z) } })
                if (triangle.Select(node => terrain.NodeBody(node.Item1, node.Item2)).FirstOrDefault(body => body >= 0, -1) is >= 0 and var owner)
                    triangles[owner].Add(triangle);
        var deepAt = terrain.Biome is Biome.Tropical or Biome.Archipelago ? BattlefieldTerrain.DeepWaterDepth : 16;
        for (var body = 0; body < terrain.Liquids.Count; body++)
        {
            var liquid = terrain.Liquids[body];
            if (triangles[body].Count == 0) continue;
            var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var triangle in triangles[body])
            {
                // A shore node shared with another body takes this body's level.
                var level = triangle.Where(node => terrain.NodeBody(node.X, node.Z) == body).Max(node => terrain.NodeSurface(node.X, node.Z));
                foreach (var (x, z) in triangle)
                {
                    var surface = terrain.NodeBody(x, z) == body || terrain.NodeBody(x, z) < 0 && !float.IsNaN(terrain.NodeSurface(x, z))
                        ? terrain.NodeSurface(x, z) : level;
                    if (float.IsNaN(surface)) surface = level;
                    var depth = Math.Clamp((surface - terrain.NodeHeight(x, z)) / deepAt, 0, 1);
                    var color = liquid.Kind == TerrainKind.Water
                        ? palette.Water.Lerp(palette.Water.Darkened(.45f), depth) with { A = Mathf.Lerp(.7f, .97f, depth) } : palette.Water;
                    tool.SetNormal(Vector3.Up); tool.SetColor(color);
                    tool.AddVertex(new((x - half) * BattlefieldTerrain.CellSize, surface + .25f, (z - half) * BattlefieldTerrain.CellSize));
                }
            }
            yield return new MeshInstance3D
            {
                Name = $"{liquid.Shape}{body}", Mesh = tool.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    Transparency = liquid.Kind == TerrainKind.Water ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                    Roughness = liquid.Kind == TerrainKind.Ice ? .2f : liquid.Kind == TerrainKind.Lava ? .7f : .08f,
                    Metallic = liquid.Kind == TerrainKind.Water ? .1f : 0,
                    EmissionEnabled = liquid.Kind == TerrainKind.Lava, Emission = palette.Water, EmissionEnergyMultiplier = 1.4f,
                },
            };
        }
    }

    /// <summary>Paved roads in towns and on most worlds, dirt tracks on farmland, sand tracks in the desert, graded regolith on the moon.</summary>
    private static Color RoadColor(Biome? biome) => biome switch
    {
        Biome.Valley or Biome.Grassland or Biome.Tropical => new("7a6248"),
        Biome.Desert => new("a88a5e"),
        Biome.Lunar => new("8f8c86"),
        _ => new("4c4b4a"),
    };
    private static StandardMaterial3D Paving(Biome? biome) =>
        new() { AlbedoColor = RoadColor(biome), Roughness = .95f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };

    /// <summary>Clearance between a road or street and the ground beneath, so depth imprecision never draws the ground over it.</summary>
    private const float PavingLift = 1;

    /// <summary>Each road as a ribbon draped over its bed, leaving its bridges to their decks. Vertices every 10 m along and across
    /// stand clear of the highest ground around them, so the ground's triangles never rise through the ribbon between them.</summary>
    private static MeshInstance3D Roads(BattlefieldTerrain terrain)
    {
        var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        var half = BattlefieldTerrain.RoadWidth / 2;
        float[] sides = [-half, -half / 2, 0, half / 2, half];
        float Clear(Vector2 at) => new[] { Vector2.Zero, new(7, 0), new(-7, 0), new(0, 7), new(0, -7) }.Max(offset => terrain.HeightAt(at + offset)) + PavingLift;
        foreach (var road in terrain.Roads)
        {
            var course = road.Course;
            Vector3[] Row(int i)
            {
                var tangent = (course[Math.Min(i + 1, course.Count - 1)] - course[Math.Max(i - 1, 0)]).Normalized();
                return sides.Select(side => course[i] + tangent.Orthogonal() * side).Select(at => new Vector3(at.X, Clear(at), at.Y)).ToArray();
            }
            bool OnDeck(Vector2 point) => terrain.HeightAt(point) > terrain.GroundAt(point) + .01f;
            var previous = Row(0);
            for (var i = 0; i < course.Count - 1; i++)
            {
                var next = Row(i + 1);
                if (!OnDeck(course[i]) && !OnDeck(course[i + 1]))
                    for (var j = 0; j < sides.Length - 1; j++)
                        foreach (var corner in new[] { previous[j], previous[j + 1], next[j], previous[j + 1], next[j + 1], next[j] })
                        { tool.SetNormal(Vector3.Up); tool.AddVertex(corner); }
                previous = next;
            }
        }
        return new MeshInstance3D { Name = "Roads", Mesh = tool.Commit(), MaterialOverride = Paving(terrain.Biome), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    /// <summary>Settlement streets: the paved pad, cut away where a river runs through it.</summary>
    private static MeshInstance3D Streets(BattlefieldTerrain terrain)
    {
        var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        const float step = 10;
        foreach (var street in terrain.Streets)
        {
            var extent = street.Half + new Vector2(30, 30);
            for (var x = -extent.X; x < extent.X - .01f; x += step) for (var z = -extent.Y; z < extent.Y - .01f; z += step)
            {
                var corners = new[] { new Vector2(x, z), new(x + step, z), new(x, z + step), new(x + step, z + step) }.Select(street.World).ToArray();
                if (corners.Any(corner => Math.Abs(terrain.HeightAt(corner) - street.Level) > .5f)) continue;
                var points = corners.Select(corner => new Vector3(corner.X, street.Level + PavingLift, corner.Y)).ToArray();
                foreach (var i in new[] { 0, 1, 2, 1, 3, 2 }) { tool.SetNormal(Vector3.Up); tool.AddVertex(points[i]); }
            }
        }
        return new MeshInstance3D { Name = "Streets", Mesh = tool.Commit(), MaterialOverride = Paving(terrain.Biome), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    /// <summary>A deck with low rails on piers; piers over lava are dark basalt.</summary>
    private static Node3D Bridge(BattlefieldTerrain terrain, TerrainBridge bridge, Palette palette)
    {
        var root = new Node3D();
        var from = new Vector3(bridge.From.X, bridge.FromHeight, bridge.From.Y); var to = new Vector3(bridge.To.X, bridge.ToHeight, bridge.To.Y);
        var along = (to - from).Normalized(); var across = along.Cross(Vector3.Up).Normalized(); var up = across.Cross(along).Normalized();
        var basis = new Basis(along, up, across);
        var concrete = new StandardMaterial3D { AlbedoColor = new Color("8a8780"), Roughness = .9f };
        var stone = bridge.Over == TerrainKind.Lava ? new StandardMaterial3D { AlbedoColor = palette.Rock.Darkened(.35f), Roughness = 1 } : concrete;
        void Box(Vector3 center, Vector3 size, Material material) => root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size }, MaterialOverride = material, Transform = new Transform3D(basis, center),
        });
        var length = from.DistanceTo(to) + 2; var middle = (from + to) / 2;
        Box(middle - up * .8f, new(length, 1.6f, BattlefieldTerrain.DeckWidth), concrete);
        Box(middle + across * (BattlefieldTerrain.DeckWidth / 2 - .8f) + up * 1.2f, new(length, 2.4f, 1.6f), concrete);
        Box(middle - across * (BattlefieldTerrain.DeckWidth / 2 - .8f) + up * 1.2f, new(length, 2.4f, 1.6f), concrete);
        for (var at = 25f; at < bridge.Length - 15; at += 45)
        {
            var top = from.Lerp(to, at / bridge.Length) - up * 1.6f;
            var ground = terrain.GroundAt(new Vector2(top.X, top.Z)) - 2;
            if (top.Y - ground < 2) continue;
            foreach (var side in new[] { -1, 1 })
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 3, BottomRadius = 3.6f, Height = top.Y - ground, RadialSegments = 10, Rings = 1 },
                    MaterialOverride = stone, Position = new Vector3(top.X, (top.Y + ground) / 2, top.Z) + across * side * (BattlefieldTerrain.DeckWidth / 2 - 8),
                });
        }
        return root;
    }

    /// <summary>Trees fill each vegetation patch: conifers in the arctic and highlands, broadleaf elsewhere. Cover is the patch, not the trees.</summary>
    private static MultiMeshInstance3D Trees(BattlefieldTerrain terrain, Palette palette, Random random)
    {
        var conifer = terrain.Biome is Biome.Arctic or Biome.Highlands;
        var mesh = new ArrayMesh();
        void Part(PrimitiveMesh part, Transform3D transform, Color color)
        {
            var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
            tool.AppendFrom(part, 0, transform); tool.SetMaterial(new StandardMaterial3D { AlbedoColor = color, Roughness = 1, VertexColorUseAsAlbedo = true });
            tool.Commit(mesh);
        }
        Part(new CylinderMesh { TopRadius = .45f, BottomRadius = .7f, Height = 6, RadialSegments = 6, Rings = 1 }, new Transform3D(Basis.Identity, new Vector3(0, 3, 0)), palette.Trunk);
        if (conifer)
        {
            Part(new CylinderMesh { TopRadius = 0, BottomRadius = 4.2f, Height = 8, RadialSegments = 8, Rings = 1 }, new Transform3D(Basis.Identity, new Vector3(0, 7, 0)), palette.Foliage);
            Part(new CylinderMesh { TopRadius = 0, BottomRadius = 3, Height = 6, RadialSegments = 8, Rings = 1 }, new Transform3D(Basis.Identity, new Vector3(0, 11.5f, 0)), palette.Foliage);
        }
        else Part(new SphereMesh { Radius = 4.8f, Height = 8, RadialSegments = 10, Rings = 5 }, new Transform3D(Basis.Identity, new Vector3(0, 8.5f, 0)), palette.Foliage);
        var placements = new List<Transform3D>(); var tints = new List<Color>();
        foreach (var patch in terrain.Patches.Where(patch => patch.Kind == TerrainKind.Vegetation))
        {
            // Sunflower spacing fills the disc evenly; jitter breaks up the pattern.
            var count = (int)(Mathf.Pi * patch.Radius * patch.Radius / (conifer ? 520 : 420));
            for (var i = 0; i < count; i++)
            {
                var at = patch.Center + Vector2.FromAngle(i * 2.39996f) * (patch.Radius * MathF.Sqrt((i + .5f) / count) * .97f)
                    + new Vector2(random.NextSingle() - .5f, random.NextSingle() - .5f) * 8;
                var scale = .8f + random.NextSingle() * .55f;
                var basis = Basis.Identity.Rotated(Vector3.Up, random.NextSingle() * Mathf.Tau).Scaled(new Vector3(scale, scale * (.85f + random.NextSingle() * .3f), scale));
                var tint = Colors.White.Lerp(new Color(.75f, .85f, .7f), random.NextSingle());
                // Roads run clear through forests.
                if (terrain.OnRoad(at)) continue;
                placements.Add(new Transform3D(basis, new Vector3(at.X, terrain.HeightAt(at) - .5f, at.Y)));
                tints.Add(tint);
            }
        }
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = placements.Count };
        for (var i = 0; i < placements.Count; i++) { multimesh.SetInstanceTransform(i, placements[i]); multimesh.SetInstanceColor(i, tints[i]); }
        return new MultiMeshInstance3D { Name = "Trees", Multimesh = multimesh };
    }

    /// <summary>A building assembled from primitives, never wider or taller than its simulation footprint.</summary>
    private static Node3D Building(BattlefieldTerrain terrain, TerrainBuilding building, Palette palette)
    {
        var ground = terrain.HeightAt(building.Center);
        // Godot turns +X toward −Z for a positive yaw; the simulation turns X toward +Z.
        var node = new Node3D { Name = building.Style.ToString(), Position = new Vector3(building.Center.X, ground, building.Center.Y), Rotation = new Vector3(0, -building.Rotation, 0) };
        var size = building.HalfSize * 2; var height = building.Height;
        void Add(Mesh mesh, Vector3 position, Color color, float roughness = .9f, float metallic = 0, Vector3? rotation = null, Color? glow = null) =>
            node.AddChild(new MeshInstance3D
            {
                Mesh = mesh, Position = position, Rotation = rotation ?? Vector3.Zero,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness, Metallic = metallic,
                    EmissionEnabled = glow is not null, Emission = glow ?? Colors.Black }
            });
        // Walls standing on the ground run below it, so pads on slight slopes never show a gap.
        void Box(Vector3 extent, float bottom, Color color, float roughness = .9f)
        {
            var footing = bottom <= 0 ? 6 : 0;
            Add(new BoxMesh { Size = extent with { Y = extent.Y + footing } }, new Vector3(0, bottom + (extent.Y - footing) / 2, 0), color, roughness);
        }
        void Roof(float width, float depth, float bottom, float rise, Color color) =>
            Add(new PrismMesh { Size = new Vector3(width, rise, depth) }, new Vector3(0, bottom + rise / 2, 0), color);
        void Column(float radius, float bottom, float tall, Color color, float metallic = 0) =>
            Add(new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = tall + 6, RadialSegments = 20, Rings = 1 },
                new Vector3(0, bottom + tall / 2 - 3, 0), color, .6f, metallic);
        var radius = building.HalfSize.X;
        switch (building.Style)
        {
            case BuildingStyle.House:
                Box(new(size.X, height * .6f, size.Y), 0, new("c9b79a")); Roof(size.X, size.Y, height * .6f, height * .4f, new("7a3b2e")); break;
            case BuildingStyle.Barn:
                Box(new(size.X, height * .55f, size.Y), 0, new("8e3a2c")); Roof(size.X, size.Y, height * .55f, height * .45f, new("4a4a48")); break;
            case BuildingStyle.Block when terrain.Biome == Biome.Arctic:
                Box(new(size.X * .98f, height, size.Y * .98f), 0, new("d27a2c"));
                Box(new(size.X, 1.2f, size.Y), height * .5f, new("3a3f44"));
                Box(new(size.X * .3f, 3, size.Y * .3f), height - 3, new("e0e0da")); break;
            case BuildingStyle.Block when terrain.Biome == Biome.Desert:
                // Flat-roofed adobe with a low parapet.
                Box(new(size.X * .98f, height - 1.5f, size.Y * .98f), 0, new("c8a97a"));
                Box(new(size.X, 1.5f, size.Y), height - 1.5f, new("b08f60")); break;
            case BuildingStyle.Block when terrain.Biome == Biome.Volcanic:
                Box(new(size.X * .98f, height, size.Y * .98f), 0, new("5c5a58"));
                Box(new(size.X, 1.6f, size.Y), height * .6f, new("e0742a"), .4f);
                Box(new(size.X * .4f, 3, size.Y * .4f), height - 3, new("3e3c3a")); break;
            case BuildingStyle.Block:
                // Window bands sit flush on slightly inset walls.
                Box(new(size.X * .98f, height, size.Y * .98f), 0, new("9c9a93"));
                for (var floor = 6f; floor < height - 3; floor += 7) Box(new(size.X, 1.6f, size.Y), floor, new("3b4a57"), .2f);
                Box(new(size.X * .35f, 3, size.Y * .35f), height - 3, new("7d7b75")); break;
            case BuildingStyle.Silo:
                // A hemisphere mesh rises its full Height above its base.
                Column(radius, 0, height - radius * .6f, new("b8bcbf"), .3f);
                Add(new SphereMesh { Radius = radius, Height = radius * .6f, IsHemisphere = true, RadialSegments = 20, Rings = 6 }, new Vector3(0, height - radius * .6f, 0), new("9aa0a4"), .5f, .3f);
                break;
            case BuildingStyle.Tank:
                Column(radius, 0, height, new("c9ccce"), .4f);
                Column(radius * .2f, height - 1, 1, new("8b8f93")); break;
            case BuildingStyle.Mast:
                Column(radius, 0, 3, new("6c7075"));
                Add(new CylinderMesh { TopRadius = .5f, BottomRadius = 1.6f, Height = height - 3, RadialSegments = 4, Rings = 1 }, new Vector3(0, 3 + (height - 3) / 2, 0), new("b03030"), .6f);
                Add(new SphereMesh { Radius = 1.2f, Height = 2.4f }, new Vector3(0, height - 1.2f, 0), new("ff3a2a"), .5f, 0, null, new("ff3a2a"));
                break;
            case BuildingStyle.Stack:
                // A tapering geothermal cooling stack with a dark rim.
                Add(new CylinderMesh { TopRadius = radius * .7f, BottomRadius = radius, Height = height + 6, RadialSegments = 20, Rings = 1 },
                    new Vector3(0, height / 2 - 3, 0), new("a7a39c"), .8f);
                Add(new CylinderMesh { TopRadius = radius * .72f, BottomRadius = radius * .72f, Height = 1.5f, RadialSegments = 20, Rings = 1 },
                    new Vector3(0, height - .75f, 0), new("3a3836"));
                break;
            case BuildingStyle.Dome:
                Column(radius, 0, 2.5f, new("8d9298"));
                Add(new SphereMesh { Radius = radius, Height = radius, IsHemisphere = true, RadialSegments = 28, Rings = 10 }, new Vector3(0, 2.5f, 0) , new("dfe3e6"), .25f, .2f);
                node.GetChild<MeshInstance3D>(node.GetChildCount() - 1).Scale = new Vector3(1, (height - 2.5f) / radius, 1);
                Add(new CylinderMesh { TopRadius = radius * .3f, BottomRadius = radius * .3f, Height = .6f, RadialSegments = 16 }, new Vector3(0, height - .1f, 0), new("ffe7a8"), .3f, 0, null, new("ffd27a"));
                break;
            case BuildingStyle.Module:
                Box(new(size.X, 4, size.Y), 0, new("8d9298"));
                Add(new CylinderMesh { TopRadius = size.X / 2 * .95f, BottomRadius = size.X / 2 * .95f, Height = size.Y, RadialSegments = 16, Rings = 1 },
                    new Vector3(0, 4 + (height - 4) / 2, 0), new("c8ccd0"), .35f, .2f, new Vector3(Mathf.Pi / 2, 0, 0));
                node.GetChild<MeshInstance3D>(node.GetChildCount() - 1).Scale = new Vector3(1, 1, (height - 4) / (size.X * .95f));
                Box(new(size.X, 1, 1.2f), height * .55f, new("ffd27a"), .3f);
                break;
        }
        return node;
    }
}
