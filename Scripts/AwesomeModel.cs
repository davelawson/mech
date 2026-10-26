using Godot;

namespace Mech;

/// <summary>The prepared GLB shares geometry/textures across instances; outlines belong to each team.</summary>
public partial class MechModel
{
    private static PackedScene? _awesomeScene;

    private void BuildAwesome()
    {
        _awesomeScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Awesome.glb")
            ?? throw new InvalidOperationException("The prepared Awesome model could not be loaded.");
        BuildPreparedModel(_awesomeScene, "Awesome");
    }

    private void BuildPreparedModel(PackedScene scene, string chassis)
    {
        var imported = scene.Instantiate<Node3D>();
        var materials = new Dictionary<Material, Material>();
        try
        {
            void Visit(Node3D node, Transform3D transform, MechLocation? owner)
            {
                if (Enum.TryParse<MechLocation>(node.Name.ToString(), out var location)) owner = location;
                if (node is MeshInstance3D { Mesh: { } mesh } source)
                {
                    if (owner is not { } damageLocation)
                        throw new InvalidOperationException($"{chassis} mesh {node.Name} has no damage location.");
                    var prefix = damageLocation + "_";
                    var name = node.Name.ToString();
                    var part = new MeshInstance3D
                    {
                        Name = name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name,
                        Mesh = mesh, Transform = transform
                    };
                    for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
                    {
                        var original = source.GetActiveMaterial(surface)
                            ?? throw new InvalidOperationException($"{chassis} mesh {name} has no material.");
                        if (!materials.TryGetValue(original, out var outlined))
                        {
                            outlined = (Material)original.Duplicate();
                            outlined.NextPass = _outline;
                            materials[original] = outlined;
                        }
                        part.SetSurfaceOverrideMaterial(surface, outlined);
                    }
                    _locations[damageLocation].AddChild(part);
                }
                else if (owner is { } markerLocation
                    && (node.Name.ToString().StartsWith(markerLocation + "_MissileTube", StringComparison.Ordinal)
                        || node.Name.ToString().StartsWith(markerLocation + "_WeaponMuzzle_", StringComparison.Ordinal)
                        || node.Name == markerLocation + "_CannonMuzzle"))
                {
                    _locations[markerLocation].AddChild(new Node3D
                    {
                        Name = node.Name.ToString()[(markerLocation.ToString().Length + 1)..],
                        Transform = transform
                    });
                }
                foreach (var child in node.GetChildren().OfType<Node3D>())
                    Visit(child, transform * child.Transform, owner);
            }
            Visit(imported, imported.Transform, null);
            if (_locations.Any(pair => pair.Value.GetChildCount() == 0))
                throw new InvalidOperationException($"The {chassis} model must contain all eight damage locations.");
        }
        finally { imported.Free(); }
    }
}
