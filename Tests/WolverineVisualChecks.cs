using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private void TestImportedWolverine(MechModel model)
    {
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        var triangles = meshes.Sum(mesh => mesh.Mesh.GetFaces().Length / 3);
        Check(triangles is >= 20000 and <= 35000 && meshes.Length < 40,
            "Wolverine retains source detail within a bounded number of rigid render meshes");
        Check(Math.Abs(model.Height - 2.94f) < .02f, "imported Wolverine preserves its agreed battlefield height");
        var head = model.Locations[MechLocation.Head];
        Check(head.FindChild("AmberCanopy", true, false) is MeshInstance3D canopy && canopy.Mesh.GetFaces().Length >= 48
            && head.FindChild("LaserEmitter", true, false) is MeshInstance3D,
            "Wolverine exterior canopy and source head laser share head damage ownership");
        var rightArm = model.Locations[MechLocation.RightArm];
        var cannon = (MeshInstance3D)rightArm.FindChild("CannonHousing", true, false);
        var muzzle = (Node3D)rightArm.FindChild("CannonMuzzle", true, false);
        Check(cannon.Mesh.GetSurfaceCount() == 2 && muzzle.GlobalPosition.X > model.GlobalPosition.X
            && (model.GlobalTransform.AffineInverse() * muzzle.GlobalPosition).Z < -.35f
            && cannon.Mesh.GetAabb().Grow(.08f).HasPoint(cannon.GlobalTransform.AffineInverse() * muzzle.GlobalPosition),
            "Wolverine source AC/5 and forward firing position are attached to the right arm");
        var leftArm = model.Locations[MechLocation.LeftArm];
        Check(leftArm.FindChild("ForearmAndFist", true, false) is MeshInstance3D
            && leftArm.FindChild("CannonHousing", true, false) is null,
            "Wolverine keeps its unarmed left forearm and fist");
        var torso = model.Locations[MechLocation.LeftTorso];
        var rack = (MeshInstance3D)torso.FindChild("Srm6Housing", true, false);
        var tubes = torso.FindChildren("MissileTube*", "Node3D", true, false).OfType<Node3D>().ToArray();
        Check(tubes.Length == 6 && tubes.All(tube => rack.Mesh.GetAabb().Grow(.02f)
                .HasPoint(rack.GlobalTransform.AffineInverse() * tube.GlobalPosition)),
            "Wolverine six source SRM firing positions stay within the left-torso launcher");
        var paint = meshes.SelectMany(mesh => Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).Select(mesh.GetActiveMaterial))
            .OfType<StandardMaterial3D>().First(material => material.AlbedoTexture is not null);
        Check(paint.AlbedoTexture.GetSize() == new Vector2(2048, 2048) && paint.NormalEnabled && paint.NormalTexture is not null,
            "Wolverine olive paint preserves source mask resolution and normal-map detail");
        var other = MechModel.Create(DraftCatalog.Mech("wolverine"), CombatSide.Enemy);
        try
        {
            var enemyCannon = (MeshInstance3D)other.Locations[MechLocation.RightArm].FindChild("CannonHousing", true, false);
            var friendlyMaterial = cannon.GetActiveMaterial(0); var enemyMaterial = enemyCannon.GetActiveMaterial(0);
            Check(ReferenceEquals(cannon.Mesh, enemyCannon.Mesh) && !ReferenceEquals(friendlyMaterial, enemyMaterial)
                && friendlyMaterial.NextPass is ShaderMaterial friendlyOutline && enemyMaterial.NextPass is ShaderMaterial enemyOutline
                && friendlyOutline.GetShaderParameter("team_color").AsColor() == MechModel.TeamColor(CombatSide.Friendly)
                && enemyOutline.GetShaderParameter("team_color").AsColor() == MechModel.TeamColor(CombatSide.Enemy),
                "Wolverine instances share geometry with independent team outlines");
        }
        finally { other.Free(); }
        GD.Print($"Imported Wolverine: {triangles} triangles, {meshes.Length} render meshes, height {model.Height:F3}");
    }
}
