using Godot;

namespace Mech;

public partial class MechModel
{
    private static PackedScene? _jennerScene;

    private void BuildJenner()
    {
        _jennerScene ??= ResourceLoader.Load<PackedScene>("res://Assets/Models/Jenner.glb")
            ?? throw new InvalidOperationException("The prepared Jenner model could not be loaded.");
        BuildPreparedModel(_jennerScene, "Jenner");
    }
}
