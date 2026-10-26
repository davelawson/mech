using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private void TestImportedCatapult(MechModel model)
    {
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        var triangles = meshes.Sum(mesh => mesh.Mesh.GetFaces().Length / 3);
        Check(triangles is >= 30000 and <= 45000 && meshes.Length < 40,
            "Catapult retains source detail with a bounded number of rigid render meshes");
        Check(Math.Abs(model.Height - 3.16f) < .02f, "imported Catapult preserves its agreed battlefield height");
        var canopy = model.Locations[MechLocation.Head].FindChild("AmberCanopy", true, false) as MeshInstance3D;
        Check(canopy is not null && canopy.Mesh.GetFaces().Length > 30,
            "Catapult includes the extracted exterior canopy beneath its head damage location");
        var paint = meshes.SelectMany(mesh => Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).Select(mesh.GetActiveMaterial))
            .OfType<StandardMaterial3D>().First(material => material.AlbedoTexture is not null);
        Check(paint.AlbedoTexture.GetSize() == new Vector2(2048, 2048) && paint.NormalEnabled && paint.NormalTexture is not null,
            "Catapult olive paint retains source mask resolution and normal-map detail");
        foreach (var side in new[] { MechLocation.LeftArm, MechLocation.RightArm })
        {
            var arm = model.Locations[side];
            var launcher = arm.FindChild("Lrm15Housing", true, false) as MeshInstance3D;
            Check(launcher is not null && launcher.Mesh.GetSurfaceCount() >= 2,
                $"Catapult {side} has the extracted launcher body and missile faces");
            var sockets = arm.FindChildren("MissileTube*", "Node3D", true, false).OfType<Node3D>().ToArray();
            Check(sockets.Length == 15 && sockets.All(socket => launcher!.Mesh.GetAabb().Grow(.02f)
                    .HasPoint(launcher.GlobalTransform.AffineInverse() * socket.GlobalPosition)),
                $"Catapult {side} has fifteen source firing positions attached inside its launcher");
        }
        var other = MechModel.Create(DraftCatalog.Mech("catapult"), CombatSide.Enemy);
        try
        {
            var friendly = (MeshInstance3D)model.Locations[MechLocation.CenterTorso].FindChild("CoreAndPelvis", true, false);
            var enemy = (MeshInstance3D)other.Locations[MechLocation.CenterTorso].FindChild("CoreAndPelvis", true, false);
            var friendMaterial = friendly.GetActiveMaterial(0); var enemyMaterial = enemy.GetActiveMaterial(0);
            Check(ReferenceEquals(friendly.Mesh, enemy.Mesh) && !ReferenceEquals(friendMaterial, enemyMaterial)
                && friendMaterial.NextPass is ShaderMaterial friendOutline && enemyMaterial.NextPass is ShaderMaterial enemyOutline
                && friendOutline.GetShaderParameter("team_color").AsColor() == MechModel.TeamColor(CombatSide.Friendly)
                && enemyOutline.GetShaderParameter("team_color").AsColor() == MechModel.TeamColor(CombatSide.Enemy),
                "Catapult instances share source geometry with independent team outlines");
        }
        finally { other.Free(); }
        GD.Print($"Imported Catapult: {triangles} triangles, {meshes.Length} render meshes, height {model.Height:F3}");
    }
}
