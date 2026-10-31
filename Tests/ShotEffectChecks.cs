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
            CombatSimulation sim = null!; var shots = new List<CombatShot>(); MechModel[] models = [];
            // Later weapon checks start from a fresh deployment rather than wherever an earlier fight left off.
            void Deploy(bool atlasFires = false)
            {
                sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), BattlefieldTerrain.Flat);
                var friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
                friendly.Position = Vector2.Zero; friendly.Facing = Vector2.Up;
                var spread = 0;
                foreach (var other in sim.Units.Where(unit => unit != friendly)) other.Position = new Vector2(-60 + 15 * spread++, 60);
                var target = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
                target.Position = new Vector2(0, -12); target.Facing = Vector2.Down;
                if (atlasFires)
                {
                    var gunner = sim.Units.Single(unit => unit.Side == CombatSide.Friendly && unit.Spec.Id == "atlas");
                    gunner.Position = new Vector2(8, 0); gunner.Facing = Vector2.Up;
                }
                field.Display(sim); sim.RefreshDetection(); sim.Paused = false;
                shots.Clear(); sim.ShotFired += shots.Add;
                models = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().ToArray();
            }
            Deploy();
            for (var step = 0; step < 200 && (!shots.Any(shot => shot.Hit) || !shots.Any(shot => !shot.Hit)); step++)
            { sim.Advance(CombatRules.StepSeconds); field._Process(0); }
            var paths = field.ShotPaths().ToArray();
            Check(paths.Any(path => path.Shot.Hit) && paths.Any(path => !path.Shot.Hit), "the exchange produced visible hits and misses");
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
            var burning = Fresh(shot => shot.SecondLocation is { } second && second != shot.Location);
            Check(burning is not null, "the exchange produced a visible laser hit");
            var kind = sim.Unit(burning!.AttackerId).Spec.Weapons.Single(weapon => weapon.Id == burning.WeaponId).Kind;
            var (_, halo, core) = field.Beams().Single(beam => beam.Shot == burning);
            Check(new Color(((StandardMaterial3D)halo.MaterialOverride).AlbedoColor, 1).IsEqualApprox(CombatViewport.LaserColor(kind))
                    && new Color(((StandardMaterial3D)core.MaterialOverride).AlbedoColor, 1)
                        .IsEqualApprox(CombatViewport.LaserColor(kind).Lerp(Colors.White, .3f)),
                "laser hits draw a 3D beam in their class color with a mostly colored core");
            Check(CombatViewport.LaserColor(WeaponKind.SmallLaser) is { R: 1, G: <= .2f, B: <= .2f }
                    && CombatViewport.LaserColor(WeaponKind.MediumLaser) is { G: 1, R: <= .2f, B: <= .2f }
                    && CombatViewport.LaserColor(WeaponKind.LargeLaser) is { B: 1, R: <= .2f, G: <= .2f }
                    && CombatViewport.LaserColor(WeaponKind.ERLargeLaser) == CombatViewport.LaserColor(WeaponKind.LargeLaser),
                "small lasers are red, medium green, large and ER large blue");
            Check(!halo.Visible, "a beam has no length at the instant it fires");
            sim.Advance(.05); field._Process(0);
            var growing = field.ShotPaths().Single(path => path.Shot == burning);
            Check(halo.Visible && Math.Abs(halo.GlobalTransform.Basis.Y.Length() - growing.From.DistanceTo(growing.To) / 2) < 1e-3f,
                "a beam reaches half its length after half its growth time");
            sim.Advance(.6); field._Process(0);
            var swept = field.ShotPaths().Where(path => path.Shot.Sequence == burning.Sequence).ToArray();
            var struckModel = ModelAt(sim.Unit(burning.TargetId));
            Check(swept.Length == 1 && swept[0].Location == burning.SecondLocation
                    && swept[0].To.DistanceTo(struckModel.LocationCenter(burning.Location)
                        .Lerp(struckModel.LocationCenter(burning.SecondLocation!.Value), .65f)) < 1e-3f
                    && Math.Abs(halo.GlobalTransform.Basis.Y.Length() - swept[0].From.DistanceTo(swept[0].To)) < 1e-3f,
                "a full-length beam sweeps linearly from the first location toward the adjacent one over the burn");
            Check(CombatViewport.LaserRadius(WeaponKind.SmallLaser) == .02f && CombatViewport.LaserRadius(WeaponKind.MediumLaser) == .04f
                    && CombatViewport.LaserRadius(WeaponKind.LargeLaser) == .06f && CombatViewport.LaserRadius(WeaponKind.ERLargeLaser) == .06f,
                "beam widths are one, two and three thirds of the large laser's");

            var miss = Fresh(shot => !shot.Hit);
            Check(miss is not null, "the exchange produced a visible laser miss");
            Vector3 MissOffset() => field.ShotPaths().Single(path => path.Shot == miss).To
                - ModelAt(sim.Unit(miss!.TargetId)).LocationCenter(miss.Location);
            var early = MissOffset(); sim.Advance(.7); field._Process(0);
            // Both models may move; the miss stays the same distance beside its location rather than sweeping.
            Check(Math.Abs(MissOffset().Length() - early.Length()) < 1e-3f, "a laser miss keeps its aim for the whole burn");

            burning = Fresh(shot => shot.Hit);
            var beams = field.Beams().Count();
            sim.Unit(burning!.AttackerId).ApplyDamage(MechLocation.Head, 100000); field._Process(0);
            Check(!field.ShotPaths().Any(path => path.Shot.Sequence == burning.Sequence) && field.Beams().Count() < beams,
                "a destroyed shooter's beam vanishes immediately");

            Check(new[] { WeaponKind.AC2, WeaponKind.AC5, WeaponKind.AC10, WeaponKind.AC20 }
                    .Select(CombatViewport.ExplosionRadius).Zip(new[] { .3f, .5f, .8f, 1.2f }).All(pair => pair.First == pair.Second),
                "autocannon explosions scale with caliber");
            Check(new[] { WeaponKind.AC2, WeaponKind.AC5, WeaponKind.AC10, WeaponKind.AC20 }
                    .Select(CombatViewport.ShellRadius).Zip(new[] { .06f, .08f, .1f, .13f }).All(pair => pair.First == pair.Second),
                "autocannon bullets scale with caliber");
            CombatShot? shell = null;
            Deploy(atlasFires: true);
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
            var bullet = field.Bullets().Single(item => item.Shot == shell).Transform;
            var caliber = CombatViewport.ShellRadius(sim.Unit(shell.AttackerId).Spec.Weapons.Single(weapon => weapon.Id == shell.WeaponId).Kind);
            Check(bullet.Origin.IsEqualApprox(Shell().Position) && Math.Abs(bullet.Basis.Y.Length() - CombatViewport.BulletLength * caliber) < 1e-4f
                    && Math.Abs(bullet.Basis.X.Length() - 2 * caliber) < 1e-4f && bullet.Basis.Y.Normalized().Dot((struck - launched).Normalized()) > .99f,
                "the shell is a caliber-sized bullet with no trail, centered on the shell and pointing along its flight");
            sim.Advance(shell.FlightSeconds / 2 + .05); field._Process(0);
            struck = ModelAt(sim.Unit(shell.TargetId)).LocationCenter(shell.Location);
            Check(!Shell().Flying && Shell().Exploding && Shell().Position.IsEqualApprox(struck),
                "the shell explodes at the struck location when it lands");

            CombatShot? bolt = null;
            Deploy();
            for (var step = 0; step < 4000 && bolt is null; step++)
            {
                var before = shots.Count; sim.Advance(CombatRules.StepSeconds); field._Process(0);
                bolt = shots.Skip(before).FirstOrDefault(shot => shot.Hit && shot.InFlight && field.Ppcs().Any(item => item.Shot == shot));
            }
            Check(bolt is not null, "the exchange produced a visible PPC hit");
            (CombatShot Shot, Vector3 Bolt, bool BeamVisible, bool Flying, bool Bursting, int Arcs) Ppc() => field.Ppcs().Single(item => item.Shot == bolt);
            Check(Ppc().BeamVisible && Ppc().Flying && !Ppc().Bursting && Ppc().Arcs == 0
                    && Ppc().Bolt.DistanceTo(ModelAt(sim.Unit(bolt!.TargetId)).LocationCenter(bolt.Location)) > 1,
                "a PPC flashes its beam at once and launches a bolt from the muzzle");
            sim.Advance(bolt!.FlightSeconds + .03); field._Process(0);
            var splashes = bolt.Splashes.Select(splash => splash.Location).Distinct().Count();
            Check(!Ppc().Flying && Ppc().Bursting && Ppc().BeamVisible && Ppc().Arcs == CombatViewport.PpcRadialArcs + splashes
                    && Ppc().Bolt.IsEqualApprox(ModelAt(sim.Unit(bolt.TargetId)).LocationCenter(bolt.Location)),
                "the bolt bursts at the struck location with radial arcs and one arc per location its splash reached, while the beam still shows");
            sim.Paused = true; var frozen = Ppc(); sim.Advance(1); field._Process(0);
            Check(Ppc() == frozen, "pausing freezes the PPC burst");
            sim.Paused = false;
            sim.Advance(CombatViewport.PpcBeamSeconds); field._Process(0);
            Check(!Ppc().BeamVisible && Ppc().Bursting, "the PPC beam ends after half a second while the burst still crackles");
            sim.Advance(CombatViewport.PpcBurstSeconds); field._Process(0);
            Check(!field.Ppcs().Any(item => item.Shot == bolt), "the PPC effect ends after its burst");

            foreach (var lrm in new[] { true, false })
            {
                var rack = lrm ? "LRM" : "SRM";
                CombatShot? salvo = null;
                Deploy(atlasFires: true);
                for (var step = 0; step < 4000 && salvo is null; step++)
                {
                    var before = shots.Count; sim.Advance(CombatRules.StepSeconds); field._Process(0);
                    salvo = shots.Skip(before).FirstOrDefault(shot => shot.Hit && shot.Missiles.Count > 0 && field.Missiles().Any(item => item.Shot == shot)
                        && CombatRules.IsLrm(sim.Unit(shot.AttackerId).Spec.Weapons.Single(weapon => weapon.Id == shot.WeaponId).Kind) == lrm);
                }
                Check(salvo is not null, $"the exchange produced a visible {rack} salvo");
                (CombatShot Shot, CombatMissile Missile, Vector3 Position, Vector3 Course, Vector3 End, float Progress, bool Flying, bool Exploding, float Loft)[] Swarm() =>
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
                Check(swarm.Where(item => item.Missile.Hit).All(item => item.Progress < .5f),
                    $"{rack} missiles are still accelerating: less than half the path is covered at half the flight");
                Check(swarm.Where(item => item.Missile.Hit).All(item => item.Position.DistanceTo(item.End) > 1)
                        && swarm.All(item => lrm ? item.Loft > 0 : item.Loft == 0),
                    lrm ? "the LRM swarm is lofted and still short of its impact points" : "the SRM swarm flies flat and is still short of its impact points");
                sim.Advance(salvo.FlightSeconds / 2 + .05); field._Process(0);
                swarm = Swarm();
                Check(swarm.Where(item => item.Missile.Hit).All(item => !item.Flying && item.Exploding && item.Position.IsEqualApprox(item.End)
                        && item.End.IsEqualApprox(ModelAt(sim.Unit(salvo.TargetId)).LocationCenter(item.Missile.Location))),
                    "hit missiles gather onto their struck locations and burst");
                Check(swarm.Where(item => !item.Missile.Hit).All(item => Math.Abs(item.End.Y - sim.Terrain.HeightAt(new Vector2(item.End.X, item.End.Z))) < 1e-3f),
                    "missed missiles come down on the ground");
            }
            Check(CombatViewport.TracerRadius * 3 <= CombatViewport.ShellRadius(WeaponKind.AC2) && CombatViewport.TracerRounds == 15,
                "machine gun tracers are a third of an AC/2 shell's radius, fifteen to a burst");
            var gunners = ForceRoster.Create("Tracer checks", [DraftCatalog.Mech("firestarter")], DraftCatalog.Warriors.Take(1));
            gunners.Assign(gunners.Mechs[0].Id, gunners.Warriors[0].Id);
            void DeployGunners()
            {
                sim = CombatSimulation.DeployOnTerrain(gunners, [gunners.Mechs[0].Id], BattlefieldTerrain.Flat);
                var gunner = sim.Units.Single(unit => unit.Side == CombatSide.Friendly); var mark = sim.Units.Single(unit => unit.Side == CombatSide.Enemy);
                gunner.Position = Vector2.Zero; gunner.Facing = Vector2.Up; mark.Position = new Vector2(0, -5); mark.Facing = Vector2.Down;
                foreach (var weapon in gunner.Weapons) weapon.Enabled = weapon.Spec.Kind == WeaponKind.MachineGun;
                mark.Firing = FiringMode.HoldFire; sim.EnemyAiEnabled = false;
                field.Display(sim); sim.RefreshDetection(); sim.Paused = false;
                shots.Clear(); sim.ShotFired += shots.Add;
                models = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().ToArray();
            }
            CombatShot? Burst(bool hit)
            {
                for (var step = 0; step < 4000; step++)
                {
                    var before = shots.Count; sim.Advance(CombatRules.StepSeconds); field._Process(0);
                    if (shots.Skip(before).FirstOrDefault(shot => shot.Hit == hit && !shot.Impact && field.Tracers().Any(item => item.Shot == shot)) is { } found)
                        return found;
                }
                return null;
            }
            DeployGunners();
            var burst = Burst(hit: true);
            Check(burst is not null, "the exchange produced a visible machine gun hit");
            (CombatShot Shot, float Fired, Color Color, bool Launched, Vector3 Head, Vector3 End, MeshInstance3D Streak, bool Sparking)[] Rounds(CombatShot shot) =>
                field.Tracers().Where(item => item.Shot == shot).ToArray();
            var rounds = Rounds(burst!);
            Check(rounds.Length == CombatViewport.TracerRounds && rounds.Count(round => round.Launched) == 1
                    && rounds.All(round => round.Color == CombatViewport.TracerYellow || round.Color == CombatViewport.TracerOrange)
                    && rounds.Select(round => round.Color).Distinct().Count() == 2 && field.MuzzleFlashing(burst!),
                "a burst opens with one round and a muzzle flash, its tracers mixing yellow and orange");
            sim.Advance(.5); field._Process(0);
            rounds = Rounds(burst!);
            var gunModel = ModelAt(sim.Unit(burst!.TargetId));
            struck = gunModel.LocationCenter(burst.Location);
            Check(rounds.Count(round => round.Launched) is >= 7 and <= 9
                    && rounds.Where(round => round.Launched).All(round => round.End.DistanceTo(struck) <= CombatViewport.TracerScatter * Mathf.Sqrt(3) + 1e-4f),
                "the burst keeps firing for its second, every hit round aimed within its scatter of the struck location");
            var streaks = rounds.Where(round => round.Streak.Visible).ToArray();
            Check(streaks.All(round => Math.Abs(round.Streak.GlobalTransform.Basis.X.Length() - CombatViewport.TracerRadius) < 1e-4f
                    && round.Streak.GlobalTransform.Basis.Y.Length() <= CombatViewport.TracerLength + 1e-4f),
                "each tracer is a thin glowing streak no longer than its set length");
            Check(rounds.Any(round => round.Sparking), "hit rounds spark where they strike");
            sim.Advance(.7); field._Process(0);
            Check(!field.Tracers().Any(item => item.Shot == burst), "the burst's effect ends shortly after its last round lands");

            DeployGunners();
            burst = Burst(hit: true);
            sim.Advance(.3); field._Process(0);
            var fired = Rounds(burst!).Count(round => round.Launched);
            sim.Unit(burst!.AttackerId).ApplyDamage(MechLocation.Head, 100000); field._Process(0);
            // An unbroken burst would have fired another round by now; the effect lingers only for the rounds already in the air.
            sim.Advance(.08); field._Process(0);
            Check(Rounds(burst).Count(round => round.Launched) == fired && fired > 1, "a destroyed gunner stops firing its burst at once");

            DeployGunners();
            var spray = Burst(hit: false);
            Check(spray is not null, "the exchange produced a visible machine gun miss");
            var sparked = false; var wide = true;
            for (var step = 0; step < 30 && Rounds(spray!).Length > 0; step++)
            {
                var aim = ModelAt(sim.Unit(spray!.TargetId)).LocationCenter(spray.Location);
                sparked |= Rounds(spray).Any(round => round.Sparking);
                wide &= Rounds(spray).Where(round => round.Launched).All(round => round.End.DistanceTo(aim) > 1);
                sim.Advance(CombatRules.StepSeconds); field._Process(0);
            }
            Check(!sparked && wide && Rounds(spray!).Length == 0, "missed rounds spray beside and past the rolled location and vanish without sparks");

            // Make every enemy location fragile so the next hit destroys one.
            Deploy(atlasFires: true);
            foreach (var part in sim.Units.Where(unit => unit.Side == CombatSide.Enemy).SelectMany(unit => unit.Locations.Values))
            { part.Armor = 0; part.RearArmor = 0; part.Structure = Math.Min(part.Structure, 1); }
            CombatShot? wrecking = null;
            for (var step = 0; step < 400 && wrecking is null; step++)
            {
                var before = shots.Count; sim.Advance(CombatRules.StepSeconds); field._Process(0);
                wrecking = shots.Skip(before).FirstOrDefault(shot => shot.DamageApplied.Any(part => part.Destroyed));
            }
            Check(wrecking is not null, "an attack destroyed a location");
            var lost = wrecking!.DamageApplied.Where(part => part.Destroyed).Select(part => part.Location).ToArray();
            var blasts = field.Destructions().Where(item => item.TargetId == wrecking.TargetId && lost.Contains(item.Location)).ToArray();
            Check(blasts.Length == lost.Length && blasts.All(item => item.Visible
                    && item.Position.IsEqualApprox(ModelAt(sim.Unit(wrecking.TargetId)).LocationCenter(item.Location))),
                "each destroyed location explodes at its center on the target model");
            sim.Advance(CombatViewport.DestructionSeconds + .05); field._Process(0);
            Check(!field.Destructions().Any(item => item.TargetId == wrecking.TargetId && lost.Contains(item.Location)),
                "a location's destruction explosion ends after its duration");

            Check(CombatViewport.MissileProgress(0, CombatViewport.LrmLaunchShare) == 0 && CombatViewport.MissileProgress(1, CombatViewport.LrmLaunchShare) == 1
                    && Math.Abs(CombatViewport.MissileProgress(.638f, CombatViewport.LrmLaunchShare) - .5f) < .01f
                    && Math.Abs(CombatViewport.MissileProgress(.01f, CombatViewport.LrmLaunchShare) / .01f - .4f) < .01f
                    && Math.Abs((1 - CombatViewport.MissileProgress(.99f, CombatViewport.LrmLaunchShare)) / .01f - 1.6f) < .01f,
                "LRMs climb at 40% of their average speed and dive in at 160%, reaching the apex after 64% of the flight");
            var flameColors = (Color[])CombatViewport.FlameMesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Color];
            var flameVertices = (Vector3[])CombatViewport.FlameMesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
            Check(CombatViewport.FlameColor(1).IsEqualApprox(Colors.White) && CombatViewport.FlameColor(.5f) is { R: 1, G: > .8f, B: < .3f, A: .5f }
                    && CombatViewport.FlameColor(0) is { R: 1, G: < .5f, B: < .1f, A: 0 }
                    && flameVertices.Zip(flameColors).All(pair =>
                    {
                        var expected = CombatViewport.FlameColor(pair.First.Y + .5f);
                        return new Vector4(pair.Second.R - expected.R, pair.Second.G - expected.G, pair.Second.B - expected.B, pair.Second.A - expected.A).Length() < .01f;
                    }),
                "missile trails run from white at the missile through yellow to a fading orange tail");
            Check(CombatViewport.MissileTrailSeconds <= .04f && CombatViewport.MissileWobble == .75f, "missile trails are short and the skitter is gentle");
            Check(CombatViewport.WobbleEnvelope(0) == 0 && CombatViewport.WobbleEnvelope(1) == 0 && CombatViewport.WobbleEnvelope(.5f) == 1,
                "the skitter builds after launch and vanishes on arrival");
        }
        finally { field.QueueFree(); await Frames(); }
    }
}
