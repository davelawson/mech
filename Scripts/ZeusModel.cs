using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _zeusScene;
    private void BuildZeus()
    {
        _zeusScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Zeus.glb")
            ?? throw new InvalidOperationException("The prepared Zeus model could not be loaded.");
        BuildPreparedModel(_zeusScene, "Zeus");
    }
}
