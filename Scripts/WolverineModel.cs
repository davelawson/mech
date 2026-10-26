using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _wolverineScene;

    private void BuildWolverine()
    {
        _wolverineScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Wolverine.glb")
            ?? throw new InvalidOperationException("The prepared Wolverine model could not be loaded.");
        BuildPreparedModel(_wolverineScene, "Wolverine");
    }
}
