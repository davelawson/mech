using Godot;

namespace Mech;

/// <summary>Simple tactical geometry using exactly the simulation's heightfield and patch footprints.</summary>
public static class BattlefieldTerrainView
{
    public static Node3D Create(BattlefieldTerrain terrain)
    {
        var root = new Node3D { Name = "Terrain" };
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        Vector3 Ground(float x, float z) => new(x, terrain.HeightAt(new Vector2(x, z)), z);
        Color ColorAt(Vector3 point)
        {
            var at = new Vector2(point.X, point.Z);
            return terrain.Contains(TerrainKind.Water, at) ? new Color("356e86")
                : terrain.Contains(TerrainKind.Vegetation, at) ? new Color("467343")
                : terrain.SlopeAt(at) > BattlefieldTerrain.MaxSlope ? new Color("887e65")
                : new Color("586348").Lerp(new Color("8c8d68"), Math.Clamp(point.Y / 9, 0, 1));
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = (b - a).Cross(c - a).Normalized();
            foreach (var point in new[] { a, b, c })
            { mesh.SurfaceSetNormal(normal); mesh.SurfaceSetColor(ColorAt(point)); mesh.SurfaceAddVertex(point); }
        }
        for (var z = -48; z < 48; z++) for (var x = -48; x < 48; x++)
        {
            var a = Ground(x, z); var b = Ground(x + 1, z); var c = Ground(x, z + 1); var d = Ground(x + 1, z + 1);
            Triangle(a, c, b); Triangle(d, b, c);
        }
        mesh.SurfaceEnd(); root.AddChild(new MeshInstance3D { Mesh = mesh });
        var grid = new ImmediateMesh();
        grid.SurfaceBegin(Mesh.PrimitiveType.Lines, new StandardMaterial3D
        { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color("727e65") });
        for (var n = -48; n <= 48; n += 8) for (var step = -48; step < 48; step++)
        {
            foreach (var point in new[] { Ground(n, step), Ground(n, step + 1), Ground(step, n), Ground(step + 1, n) })
                grid.SurfaceAddVertex(point + Vector3.Up * .025f);
        }
        grid.SurfaceEnd(); root.AddChild(new MeshInstance3D { Mesh = grid });
        foreach (var patch in terrain.Patches)
        {
            if (patch.Kind == TerrainKind.Rock)
            {
                var top = terrain.HeightAt(patch.Center) + patch.Height;
                root.AddChild(new MeshInstance3D
                {
                    Position = new Vector3(patch.Center.X, (top - .5f) / 2, patch.Center.Y),
                    Mesh = new CylinderMesh { TopRadius = patch.Radius, BottomRadius = patch.Radius, Height = top + .5f, RadialSegments = 24 },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("92918a"), Roughness = 1 }
                });
            }
            else if (patch.Kind == TerrainKind.Vegetation)
            {
                // Low shrubs signal soft cover without suggesting solid tree-height obstructions.
                for (var i = 0; i < 9; i++)
                {
                    var at = patch.Center + Vector2.FromAngle(i * 2.4f) * (patch.Radius * MathF.Sqrt(i / 10f) * .8f);
                    root.AddChild(new MeshInstance3D
                    {
                        Position = new Vector3(at.X, terrain.HeightAt(at) + .3f, at.Y),
                        Mesh = new SphereMesh { Radius = .55f, Height = .6f, RadialSegments = 8, Rings = 4 },
                        MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("305d32"), Roughness = 1 }
                    });
                }
            }
        }
        return root;
    }
}
