using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _victorScene;
    private void BuildVictor()
    {
        _victorScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Victor.glb")
            ?? throw new InvalidOperationException("The prepared Victor model could not be loaded.");
        BuildPreparedModel(_victorScene, "Victor");
    }
}
