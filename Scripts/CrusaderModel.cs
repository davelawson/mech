using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _crusaderScene;
    private void BuildCrusader()
    {
        _crusaderScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Crusader.glb")
            ?? throw new InvalidOperationException("The prepared Crusader model could not be loaded.");
        BuildPreparedModel(_crusaderScene, "Crusader");
    }
}
