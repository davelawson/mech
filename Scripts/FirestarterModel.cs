using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _firestarterScene;
    private void BuildFirestarter()
    {
        _firestarterScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Firestarter.glb")
            ?? throw new InvalidOperationException("The prepared Firestarter model could not be loaded.");
        BuildPreparedModel(_firestarterScene, "Firestarter");
    }
}
