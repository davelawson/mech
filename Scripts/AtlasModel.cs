using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _atlasScene;
    private void BuildAtlas()
    {
        _atlasScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Atlas.glb")
            ?? throw new InvalidOperationException("The prepared Atlas model could not be loaded.");
        BuildPreparedModel(_atlasScene, "Atlas");
    }
}
