using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _thunderboltScene;
    private void BuildThunderbolt()
    {
        _thunderboltScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Thunderbolt.glb")
            ?? throw new InvalidOperationException("The prepared Thunderbolt model could not be loaded.");
        BuildPreparedModel(_thunderboltScene, "Thunderbolt");
    }
}
