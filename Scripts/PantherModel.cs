using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _pantherScene;
    private void BuildPanther()
    {
        _pantherScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Panther.glb")
            ?? throw new InvalidOperationException("The prepared Panther model could not be loaded.");
        BuildPreparedModel(_pantherScene, "Panther");
    }
}
