using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _shadowhawkScene;
    private void BuildShadowhawk()
    {
        _shadowhawkScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Shadowhawk.glb")
            ?? throw new InvalidOperationException("The prepared Shadowhawk model could not be loaded.");
        BuildPreparedModel(_shadowhawkScene, "Shadowhawk");
    }
}
