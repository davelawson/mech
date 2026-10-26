using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _griffinScene;
    private void BuildGriffin()
    {
        _griffinScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Griffin.glb")
            ?? throw new InvalidOperationException("The prepared Griffin model could not be loaded.");
        BuildPreparedModel(_griffinScene, "Griffin");
    }
}
