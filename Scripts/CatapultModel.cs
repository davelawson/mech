using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _catapultScene;

    private void BuildCatapult()
    {
        _catapultScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Catapult.glb")
            ?? throw new InvalidOperationException("The prepared Catapult model could not be loaded.");
        BuildPreparedModel(_catapultScene, "Catapult");
    }
}
