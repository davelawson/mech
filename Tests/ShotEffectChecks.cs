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
            // A laser briefly slides between its two struck locations at half the burn.
            bool OnStruck((CombatShot Shot, Vector3 From, Vector3 To, MechLocation Location) path)
            {
                var model = ModelAt(sim.Unit(path.Shot.TargetId));
                var first = model.LocationCenter(path.Shot.Location);
                if (path.Shot.SecondLocation is not { } second) return path.To.IsEqualApprox(first);
                var span = model.LocationCenter(second) - first;
                var t = span.LengthSquared() < 1e-8f ? 0 : Math.Clamp((path.To - first).Dot(span) / span.LengthSquared(), 0, 1);
                return path.To.DistanceTo(first + span * t) < 1e-3f;
            }
            Check(paths.Where(path => path.Shot.Hit).All(OnStruck), "hits end at the struck location on the target model");
            Check(paths.Where(path => !path.Shot.Hit).All(path =>
                    path.To.DistanceTo(ModelAt(sim.Unit(path.Shot.TargetId)).LocationCenter(path.Shot.Location)) > 1),
                "misses pass beside the rolled location");

            // Follow a laser discharge from the step it fires.
            CombatShot? Fresh(Func<CombatShot, bool> match)
            {
                for (var step = 0; step < 400; step++)
                {
                    var before = shots.Count; sim.Advance(CombatRules.StepSeconds); field._Process(0);
                    if (shots.Skip(before).FirstOrDefault(shot => match(shot) && field.Beams().Any(beam => beam.Shot == shot)) is { } found)
                        return found;
                }
                return null;
            }
            var burning = Fresh(shot => shot.SecondLocation is not null);
            Check(burning is not null, "the exchange produced a visible laser hit");
            var kind = sim.Unit(burning!.AttackerId).Spec.Weapons.Single(weapon => weapon.Id == burning.WeaponId).Kind;
            var halo = field.Beams().Single(beam => beam.Shot == burning).Halo;
            Check(new Color(((StandardMaterial3D)halo.MaterialOverride).AlbedoColor, 1).IsEqualApprox(CombatViewport.LaserColor(kind)),
                "laser hits draw a 3D beam in their class color");
            Check(CombatViewport.LaserColor(WeaponKind.SmallLaser).R > .9f && CombatViewport.LaserColor(WeaponKind.MediumLaser).G > .9f
                    && CombatViewport.LaserColor(WeaponKind.LargeLaser).B > .9f
                    && CombatViewport.LaserColor(WeaponKind.ERLargeLaser) == CombatViewport.LaserColor(WeaponKind.LargeLaser),
                "small lasers are red, medium green, large and ER large blue");
            Check(!halo.Visible, "a beam has no length at the instant it fires");
            sim.Advance(.05); field._Process(0);
            var growing = field.ShotPaths().Single(path => path.Shot == burning);
            Check(halo.Visible && Math.Abs(halo.GlobalTransform.Basis.Y.Length() - growing.From.DistanceTo(growing.To) / 2) < 1e-3f,
                "a beam reaches half its length after half its growth time");
            sim.Advance(.6); field._Process(0);
            var swept = field.ShotPaths().Where(path => path.Shot.Sequence == burning.Sequence).ToArray();
            Check(swept.Length == 1 && swept[0].Location == burning.SecondLocation
                    && swept[0].To.IsEqualApprox(ModelAt(sim.Unit(burning.TargetId)).LocationCenter(burning.SecondLocation!.Value))
                    && Math.Abs(halo.GlobalTransform.Basis.Y.Length() - swept[0].From.DistanceTo(swept[0].To)) < 1e-3f,
                "after half the burn the full-length beam holds on the adjacent location");

            var miss = Fresh(shot => !shot.Hit);
            Check(miss is not null, "the exchange produced a visible laser miss");
            Vector3 MissOffset() => field.ShotPaths().Single(path => path.Shot == miss).To
                - ModelAt(sim.Unit(miss!.TargetId)).LocationCenter(miss.Location);
            var early = MissOffset(); sim.Advance(.7); field._Process(0);
            // Both models may move; the miss stays the same distance beside its location rather than sweeping.
            Check(Math.Abs(MissOffset().Length() - early.Length()) < 1e-3f, "a laser miss keeps its aim for the whole burn");

            burning = Fresh(shot => shot.Hit);
            var beams = field.Beams().Count();
            sim.Unit(burning!.AttackerId).ApplyDamage(MechLocation.Head, 10000); field._Process(0);
            Check(!field.ShotPaths().Any(path => path.Shot.Sequence == burning.Sequence) && field.Beams().Count() < beams,
                "a destroyed shooter's beam vanishes immediately");

            Check(new[] { WeaponKind.AC2, WeaponKind.AC5, WeaponKind.AC10, WeaponKind.AC20 }
                    .Select(CombatViewport.ExplosionRadius).Zip(new[] { .3f, .5f, .8f, 1.2f }).All(pair => pair.First == pair.Second),
                "autocannon explosions scale with caliber");
            CombatShot? shell = null;
            var gunner = sim.Units.Single(unit => unit.Side == CombatSide.Friendly && unit.Spec.Id == "atlas");
            gunner.Position = new Vector2(8, 0); gunner.Facing = Vector2.Up;
            for (var step = 0; step < 4000 && shell is null; step++)
            {
                var before = shots.Count; sim.Advance(CombatRules.StepSeconds); field._Process(0);
                shell = shots.Skip(before).FirstOrDefault(shot => shot.Hit && shot.InFlight && field.Shells().Any(item => item.Shot == shot));
            }
            Check(shell is not null, "the exchange produced a visible autocannon hit");
            (CombatShot Shot, Vector3 Position, bool Flying, bool Exploding) Shell() => field.Shells().Single(item => item.Shot == shell);
            var launched = Shell().Position;
            sim.Advance(shell!.FlightSeconds / 2); field._Process(0);
            var struck = ModelAt(sim.Unit(shell.TargetId)).LocationCenter(shell.Location);
            Check(Shell().Flying && !Shell().Exploding && Shell().Position.DistanceTo(launched) > 1
                    && Shell().Position.DistanceTo(struck) > 1, "an autocannon shell travels toward its target");
            sim.Advance(shell.FlightSeconds / 2 + .05); field._Process(0);
            struck = ModelAt(sim.Unit(shell.TargetId)).LocationCenter(shell.Location);
            Check(!Shell().Flying && Shell().Exploding && Shell().Position.IsEqualApprox(struck),
                "the shell explodes at the struck location when it lands");

            CombatShot? salvo = null;
            for (var step = 0; step < 4000 && salvo is null; step++)
            {
                var before = shots.Count; sim.Advance(CombatRules.StepSeconds); field._Process(0);
                salvo = shots.Skip(before).FirstOrDefault(shot => shot.Hit && shot.Missiles.Count > 0 && field.Missiles().Any(item => item.Shot == shot));
            }
            Check(salvo is not null, "the exchange produced a visible LRM salvo");
            (CombatShot Shot, CombatMissile Missile, Vector3 Position, Vector3 Course, Vector3 End, float Progress, bool Flying, bool Exploding)[] Swarm() =>
                field.Missiles().Where(item => item.Shot == salvo).ToArray();
            Check(Swarm().Length == salvo!.Missiles.Count && Swarm().All(item => item.Progress < .05f),
                "each missile of a salvo has its own visual flight, starting at the rack");
            sim.Advance(salvo.FlightSeconds / 2); field._Process(0);
            var swarm = Swarm();
            Check(swarm.All(item => item.Flying && !item.Exploding && item.Position.DistanceTo(item.Course)
                    <= CombatViewport.MissileWobble * CombatViewport.WobbleEnvelope(item.Progress) + 1e-3f),
                "missiles skitter within their wobble envelope mid-flight");
            Check(swarm.Count(item => item.Position.DistanceTo(item.Course) > .1f) > swarm.Length / 2
                    && swarm.Select(item => item.Position - item.Course).Distinct().Count() == swarm.Length,
                "each missile skitters along its own randomized path");
            Check(swarm.Where(item => item.Missile.Hit).All(item => item.Position.DistanceTo(item.End) > 1)
                    && swarm.All(item => item.Course.Y > Math.Min(item.End.Y, item.Position.Y) - 1e-3f),
                "the swarm is still lofted short of its impact points");
            sim.Advance(salvo.FlightSeconds / 2 + .05); field._Process(0);
            swarm = Swarm();
            Check(swarm.Where(item => item.Missile.Hit).All(item => !item.Flying && item.Exploding && item.Position.IsEqualApprox(item.End)
                    && item.End.IsEqualApprox(ModelAt(sim.Unit(salvo.TargetId)).LocationCenter(item.Missile.Location))),
                "hit missiles gather onto their struck locations and burst");
            Check(swarm.Where(item => !item.Missile.Hit).All(item => Math.Abs(item.End.Y - sim.Terrain.HeightAt(new Vector2(item.End.X, item.End.Z))) < 1e-3f),
                "missed missiles come down on the ground");
            Check(CombatViewport.WobbleEnvelope(0) == 0 && CombatViewport.WobbleEnvelope(1) == 0 && CombatViewport.WobbleEnvelope(.5f) == 1,
                "the skitter builds after launch and vanishes on arrival");
        }
        finally { field.QueueFree(); await Frames(); }
    }
}
