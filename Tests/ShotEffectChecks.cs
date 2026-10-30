using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestShotEffects()
    {
        var stage = new Node3D(); AddChild(stage);
        var unmarked = new List<string>();
        foreach (var spec in DraftCatalog.AllMechs)
        {
            var model = MechModel.Create(spec, CombatSide.Friendly); stage.AddChild(model);
            if (model.HasLocomotion) foreach (var weapon in spec.Weapons)
                if (model.NextFiringMarker(weapon.Id) is null) unmarked.Add($"{spec.Id} {weapon.Id}");
            model.Free();
        }
        // The Wolverine's head laser source mesh has no firing point; it fires from the head's center.
        Check(unmarked.SequenceEqual(["wolverine weapon-2"]),
            $"prepared chassis weapons fire from source markers, including off-location visuals ({string.Join(", ", unmarked)})");

        var awesome = MechModel.Create(DraftCatalog.Mech("awesome"), CombatSide.Friendly); stage.AddChild(awesome);
        var mounts = DraftCatalog.Mech("awesome").Weapons.Select(weapon => (weapon, marker: awesome.NextFiringMarker(weapon.Id)!)).ToArray();
        Check(mounts.All(item => awesome.Locations[item.weapon.Location].IsAncestorOf(item.marker)
                && item.marker.Name == (item.weapon.Kind == WeaponKind.PPC ? "WeaponMuzzle_PpcEmitter" : "WeaponMuzzle_LaserEmitter")),
            "Awesome PPCs and head laser fire from their own source emitters");
        var atlasSpec = DraftCatalog.Mech("atlas");
        var atlas = MechModel.Create(atlasSpec, CombatSide.Friendly); stage.AddChild(atlas);
        string Marker(WeaponKind kind) => atlas.NextFiringMarker(atlasSpec.Weapons.First(w => w.Kind == kind).Id)!.Name;
        Check(Marker(WeaponKind.AC20) == "CannonMuzzle" && Marker(WeaponKind.LRM20).StartsWith("LrmTube", StringComparison.Ordinal)
                && Marker(WeaponKind.SRM6).StartsWith("MissileTube", StringComparison.Ordinal),
            "Atlas cannon and launchers fire from markers of their own kind");
        var jennerSpec = DraftCatalog.Mech("jenner");
        var jenner = MechModel.Create(jennerSpec, CombatSide.Friendly); stage.AddChild(jenner);
        var srm = jennerSpec.Weapons.First(w => w.Kind == WeaponKind.SRM4).Id;
        Check(Enumerable.Range(0, 4).Select(_ => jenner.NextFiringMarker(srm)!).Distinct().Count() == 4,
            "successive SRM shots cycle through the launcher's tubes");
        stage.QueueFree();

        var roster = ForceRoster.Create("Shot effect checks", [DraftCatalog.Mech("awesome"), DraftCatalog.Mech("atlas")], DraftCatalog.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var field = new CombatViewport { Position = new Vector2(40, 40), Size = new Vector2(900, 650) };
        AddChild(field); await Frames();
        try
        {
            var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), BattlefieldTerrain.Flat);
            var friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
            friendly.Position = Vector2.Zero; friendly.Facing = Vector2.Up;
            var spread = 0;
            foreach (var other in sim.Units.Where(unit => unit != friendly)) other.Position = new Vector2(-60 + 15 * spread++, 60);
            var target = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
            target.Position = new Vector2(0, -12); target.Facing = Vector2.Down;
            field.Display(sim); sim.RefreshDetection(); sim.Paused = false;
            var shots = new List<CombatShot>(); sim.ShotFired += shots.Add;
            for (var step = 0; step < 200 && (!shots.Any(shot => shot.Hit) || !shots.Any(shot => !shot.Hit)); step++)
            { sim.Advance(CombatRules.StepSeconds); field._Process(0); }
            var paths = field.ShotPaths().ToArray();
            Check(paths.Any(path => path.Shot.Hit) && paths.Any(path => !path.Shot.Hit), "the exchange produced visible hits and misses");
            var models = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().ToArray();
            MechModel ModelAt(CombatUnit unit) => models.Single(model => model.Position.X == unit.Position.X && model.Position.Z == unit.Position.Y);
            var markers = models.SelectMany(model => model.FindChildren("*", "Node3D", true, false).OfType<Node3D>())
                .Where(node => node is not MeshInstance3D && node.Name.ToString() is var name
                    && (name.StartsWith("WeaponMuzzle_") || name.StartsWith("MissileTube") || name.StartsWith("LrmTube") || name == "CannonMuzzle"))
                .Select(node => node.GlobalPosition).ToArray();
            Check(paths.Length > 0 && paths.All(path => markers.Any(marker => marker.IsEqualApprox(path.From))),
                "every firing effect starts at a weapon's source firing marker");
            Check(paths.Where(path => path.Shot.Hit).All(path =>
                    path.To.IsEqualApprox(ModelAt(sim.Unit(path.Shot.TargetId)).LocationCenter(path.Shot.Location))),
                "hits end at the struck location on the target model");
            Check(paths.Where(path => !path.Shot.Hit).All(path =>
                    path.To.DistanceTo(ModelAt(sim.Unit(path.Shot.TargetId)).LocationCenter(path.Shot.Location)) > 1),
                "misses pass beside the rolled location");
        }
        finally { field.QueueFree(); await Frames(); }
    }
}
