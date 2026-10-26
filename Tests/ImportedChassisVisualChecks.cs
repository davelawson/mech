using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private void TestSourceBeamMount(MechModel model, MechLocation location, string name)
    {
        var root = model.Locations[location];
        var weapon = root.FindChild(name, true, false) as MeshInstance3D;
        var muzzle = root.FindChild("WeaponMuzzle_" + name, true, false) as Node3D;
        Check(weapon is not null && muzzle is not null && weapon.Mesh.GetAabb().Grow(.08f)
                .HasPoint(weapon.GlobalTransform.AffineInverse() * muzzle.GlobalPosition),
            $"{model.ChassisId} {location} {name} retains its source firing point inside the weapon");
    }

    private void TestImportedAwesome(MechModel model)
    {
        Check(Math.Abs(model.Height - 3.64f) < .02f, "MW5 Awesome preserves its battlefield height");
        var head = model.Locations[MechLocation.Head];
        Check(head.FindChild("AmberCanopy", true, false) is MeshInstance3D canopy && canopy.Mesh.GetFaces().Length >= 42
            && head.FindChild("LaserEmitter", true, false) is MeshInstance3D,
            "Awesome exterior canopy and stock small laser share head damage ownership");
        foreach (var location in new[] { MechLocation.RightArm, MechLocation.LeftTorso, MechLocation.RightTorso })
            TestSourceBeamMount(model, location, "PpcEmitter");
        TestSourceBeamMount(model, MechLocation.Head, "LaserEmitter");
        var material = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
            .SelectMany(mesh => Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).Select(mesh.GetActiveMaterial))
            .OfType<StandardMaterial3D>().First(paint => paint.AlbedoTexture is not null);
        Check(material.NormalEnabled && material.NormalTexture is not null,
            "Awesome retains source normal detail alongside its olive paint");
    }

    private void TestImportedJenner(MechModel model)
    {
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        var triangles = meshes.Sum(mesh => mesh.Mesh.GetFaces().Length / 3);
        Check(triangles is >= 20000 and <= 30000 && meshes.Length < 35,
            "MW5 Jenner retains source body and exterior cockpit detail within its geometry budget");
        Check(Math.Abs(model.Height - 2.40f) < .02f, "MW5 Jenner preserves its battlefield height");
        var head = model.Locations[MechLocation.Head];
        Check(head.FindChild("AmberCanopy", true, false) is MeshInstance3D canopy && canopy.Mesh.GetFaces().Length >= 138
            && head.FindChild("CockpitFrame", true, false) is MeshInstance3D frame && frame.Mesh.GetFaces().Length > 15000,
            "Jenner includes its extracted exterior cockpit nose and amber glazing beneath Head");
        foreach (var side in new[] { MechLocation.LeftArm, MechLocation.RightArm })
            for (var laser = 0; laser < 2; laser++) TestSourceBeamMount(model, side, "LaserEmitter" + laser);
        var torso = model.Locations[MechLocation.CenterTorso];
        var rack = (MeshInstance3D)torso.FindChild("Srm4Housing", true, false);
        var tubes = torso.FindChildren("MissileTube*", "Node3D", true, false).OfType<Node3D>().ToArray();
        Check(tubes.Length == 4 && tubes.All(tube => rack.Mesh.GetAabb().Grow(.02f)
                .HasPoint(rack.GlobalTransform.AffineInverse() * tube.GlobalPosition)),
            "Jenner four source SRM firing positions stay inside the stock center-torso MH3 rack");
        var paint = meshes.SelectMany(mesh => Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).Select(mesh.GetActiveMaterial))
            .OfType<StandardMaterial3D>().First(material => material.AlbedoTexture is not null);
        Check(paint.AlbedoTexture.GetSize() == new Vector2(2048, 2048) && paint.NormalEnabled && paint.NormalTexture is not null,
            "Jenner olive paint preserves source mask resolution and normal detail");
        var other = MechModel.Create(DraftCatalog.Mech("jenner"), CombatSide.Enemy);
        try
        {
            var source = (MeshInstance3D)model.Locations[MechLocation.LeftArm].FindChild("LaserEmitter0", true, false);
            var enemy = (MeshInstance3D)other.Locations[MechLocation.LeftArm].FindChild("LaserEmitter0", true, false);
            var friendlyPaint = source.GetActiveMaterial(0); var enemyPaint = enemy.GetActiveMaterial(0);
            Check(ReferenceEquals(source.Mesh, enemy.Mesh) && !ReferenceEquals(friendlyPaint, enemyPaint)
                && friendlyPaint.NextPass is ShaderMaterial friendlyOutline && enemyPaint.NextPass is ShaderMaterial enemyOutline
                && friendlyOutline.GetShaderParameter("team_color").AsColor() == MechModel.TeamColor(CombatSide.Friendly)
                && enemyOutline.GetShaderParameter("team_color").AsColor() == MechModel.TeamColor(CombatSide.Enemy),
                "Jenner instances share geometry with independent team outlines");
        }
        finally { other.Free(); }
        GD.Print($"Imported Jenner: {triangles} triangles, {meshes.Length} render meshes, height {model.Height:F3}");
    }
}
