using Godot;

namespace Mech;

/// <summary>The prepared GLB shares geometry/textures across instances; outlines belong to each team.</summary>
public partial class MechModel
{
    private static readonly Dictionary<string, PackedScene> PreparedScenes = [];
    private sealed record LibraryVisual(Mesh Mesh, Material[] Materials, Transform3D Transform, Transform3D[] Markers);
    private static readonly Dictionary<string, Dictionary<(string Hardpoint, string Visual), LibraryVisual>> WeaponLibraries = [];
    private readonly Dictionary<string, Node3D[]> _swappedMarkers = [];

    private void BuildPreparedModel(string chassis, IReadOnlyList<Hardpoint> hardpoints)
    {
        if (!PreparedScenes.TryGetValue(chassis, out var scene))
        {
            scene = ResourceLoader.Load<PackedScene>($"res://Assets/Models/{chassis}.glb")
                ?? throw new InvalidOperationException($"The prepared {chassis} model could not be loaded.");
            PreparedScenes[chassis] = scene;
        }
        // Hardpoints whose visual differs from stock drop the stock part and its markers and take a library visual.
        var swapped = Mounts.Stock ? [] : hardpoints.Where(item => Mounts.Visuals.GetValueOrDefault(item.Id) != item.Stock).ToArray();
        var hidden = swapped.Where(item => item.StockPart is not null)
            .SelectMany(item => (item.StockMarkers ?? []).Select(marker => $"{item.Location}_{marker}").Append(item.StockPart!))
            .ToHashSet();
        var materials = BuildPreparedModel(scene, chassis, hidden);
        foreach (var hardpoint in swapped)
        {
            if (Mounts.Visuals.GetValueOrDefault(hardpoint.Id) is not { } visual) continue;
            var entry = WeaponLibrary(chassis).GetValueOrDefault((hardpoint.Id, visual))
                ?? throw new InvalidOperationException($"{chassis} has no {visual} visual for hardpoint {hardpoint.Id}.");
            var part = new MeshInstance3D { Name = $"{hardpoint.Id}_{visual}", Mesh = entry.Mesh, Transform = entry.Transform };
            for (var surface = 0; surface < entry.Materials.Length; surface++)
                part.SetSurfaceOverrideMaterial(surface, materials.GetValueOrDefault(entry.Materials[surface].ResourceName)
                    ?? Outlined(entry.Materials[surface], new()));
            _locations[hardpoint.Location].AddChild(part);
            _swappedMarkers[hardpoint.Id] = [.. entry.Markers.Select((transform, i) =>
            {
                var marker = new Node3D { Name = $"{hardpoint.Id}_{visual}_Fire{i:00}", Transform = transform };
                _locations[hardpoint.Location].AddChild(marker);
                return marker;
            })];
        }
    }

    /// <summary>The (hardpoint, visual) pairs in a chassis's weapon library.</summary>
    internal static IEnumerable<(string Hardpoint, string Visual)> WeaponLibraryVisuals(string chassisId) =>
        PreparedModels.TryGetValue(chassisId, out var prepared) ? WeaponLibrary(prepared.Chassis).Keys : [];

    private Material Outlined(Material original, Dictionary<Material, Material> cache)
    {
        if (cache.TryGetValue(original, out var outlined)) return outlined;
        outlined = (Material)original.Duplicate();
        outlined.NextPass = _outline;
        return cache[original] = outlined;
    }

    /// <summary>Every visual in `&lt;Chassis&gt;Weapons.glb`, which shares the prepared model's space. Its placeholder
    /// material is replaced by the model's own same-named weapon paint.</summary>
    private static Dictionary<(string Hardpoint, string Visual), LibraryVisual> WeaponLibrary(string chassis)
    {
        if (WeaponLibraries.TryGetValue(chassis, out var library)) return library;
        var scene = ResourceLoader.Load<PackedScene>($"res://Assets/Models/{chassis}Weapons.glb")
            ?? throw new InvalidOperationException($"The {chassis} weapon library could not be loaded.");
        var imported = scene.Instantiate<Node3D>();
        library = [];
        try
        {
            foreach (var hardpoint in imported.GetChildren().OfType<Node3D>())
            {
                var markers = hardpoint.GetChildren().OfType<Node3D>().Where(node => node is not MeshInstance3D)
                    .OrderBy(node => node.Name.ToString(), StringComparer.Ordinal).ToArray();
                foreach (var visual in hardpoint.GetChildren().OfType<MeshInstance3D>())
                {
                    // Nodes are named `<Hardpoint>_<Visual>` and `<Hardpoint>_<Visual>_Fire<nn>`.
                    var prefix = visual.Name + "_Fire";
                    library[(hardpoint.Name, visual.Name.ToString()[(hardpoint.Name.ToString().Length + 1)..])] = new(visual.Mesh,
                        [.. Enumerable.Range(0, visual.Mesh.GetSurfaceCount()).Select(visual.GetActiveMaterial)],
                        hardpoint.Transform * visual.Transform,
                        [.. markers.Where(node => node.Name.ToString().StartsWith(prefix, StringComparison.Ordinal))
                            .Select(node => hardpoint.Transform * node.Transform)]);
                }
            }
        }
        finally { imported.Free(); }
        return WeaponLibraries[chassis] = library;
    }

    /// <summary>Copies the prepared meshes and markers, except hidden stock parts, and returns outlined materials by name.</summary>
    private Dictionary<string, Material> BuildPreparedModel(PackedScene scene, string chassis, IReadOnlySet<string> hidden)
    {
        var imported = scene.Instantiate<Node3D>();
        var materials = new Dictionary<Material, Material>();
        try
        {
            void Visit(Node3D node, Transform3D transform, MechLocation? owner)
            {
                if (Enum.TryParse<MechLocation>(node.Name.ToString(), out var location)) owner = location;
                if (hidden.Contains(node.Name.ToString())) { }
                else if (node is MeshInstance3D { Mesh: { } mesh } source)
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
                        part.SetSurfaceOverrideMaterial(surface, Outlined(original, materials));
                    }
                    _locations[damageLocation].AddChild(part);
                }
                else if (owner is { } markerLocation
                    && (node.Name.ToString().StartsWith(markerLocation + "_MissileTube", StringComparison.Ordinal)
                        || node.Name.ToString().StartsWith(markerLocation + "_LrmTube", StringComparison.Ordinal)
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
        return materials.GroupBy(pair => pair.Key.ResourceName).ToDictionary(group => group.Key, group => group.First().Value);
    }
}
