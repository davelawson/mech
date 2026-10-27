using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestChassisVisuals()
    {
        var definitions = new[] { "jenner", "wolverine", "catapult", "atlas" };
        var roster = ForceRoster.Create("Chassis model review", definitions.Select(DraftCatalog.Mech), DraftCatalog.Warriors.Take(4));
        for (var i = 0; i < definitions.Length; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var stage = new Control(); AddChild(stage); stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var hangar = new HangarScreen(); stage.AddChild(hangar); hangar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); hangar.OffsetTop = 72;
        hangar.Open(roster); hangar.Preview.SetProcess(false); await Frames();
        var heights = new Dictionary<string, float>(); var widths = new Dictionary<string, float>();
        static MeshInstance3D[] Meshes(Node node) => node.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        static int Count(Node node, string prefix) => Meshes(node).Count(mesh => mesh.Name.ToString().StartsWith(prefix, StringComparison.Ordinal));
        static float Bottom(MeshInstance3D mesh) => Enumerable.Range(0, 8)
            .Min(i => (mesh.GlobalTransform * mesh.Mesh.GetAabb().GetEndpoint(i)).Y);
        for (var chassis = 0; chassis < definitions.Length; chassis++)
        {
            GetWindow().Size = new(1440, 900); hangar.Select(chassis); await Frames();
            var model = hangar.Preview.Model!; var spec = roster.Mechs[chassis].Spec;
            model.Rotation = Vector3.Zero;
            heights[spec.Id] = model.Height; widths[spec.Id] = model.RestBounds.Size.X * model.Scale.X;
            var meshes = Meshes(model);
            Check(model.ChassisId == spec.Id && model.HasLocomotion && model.Locations.Count == 8,
                $"{spec.Chassis} receives its own articulated eight-location model");
            Check(meshes.All(mesh => model.Locations.Values.Count(root => root.IsAncestorOf(mesh)) == 1),
                $"{spec.Chassis} owns every detail beneath exactly one damage location");
            foreach (var (location, root) in model.Locations)
            {
                var weapons = spec.Weapons.Where(weapon => weapon.Location == location).ToArray();
                var tubes = weapons.Where(weapon => weapon.Name.StartsWith("SRM ", StringComparison.Ordinal)
                    || weapon.Name.StartsWith("LRM ", StringComparison.Ordinal)).Sum(weapon => int.Parse(weapon.Name.Split(' ')[^1]));
                // Imported weapons retain non-rendering source Fire socket markers.
                var displayedTubes = root.FindChildren("MissileTube*", "Node3D", true, false).Count;
                if (spec.Id == "atlas") tubes -= weapons.Where(weapon => weapon.Name == "LRM 20").Sum(_ => 20);
                var displayedCannons = root.FindChildren("CannonMuzzle", "Node3D", true, false).Count;
                Check(displayedTubes == tubes && Count(root, "LaserEmitter") == weapons.Count(weapon => weapon.Name == "Medium Laser")
                    && displayedCannons == weapons.Count(weapon => weapon.Name is "AC/5" or "AC/20"),
                    $"{spec.Chassis} {location} displays stock weapon counts rather than illustration discrepancies");
            }
            if (spec.Id == "atlas")
            {
                Check(Math.Abs(model.Height - 4f) < .02f && meshes.Length == 24, "Atlas retains its complete articulated source silhouette");
                Check(model.Locations[MechLocation.Head].FindChild("AmberCanopy", true, false) is not null
                    && model.Locations[MechLocation.Head].FindChild("CockpitFrame", true, false) is not null, "Atlas keeps its skull and separate glowing eyes");
                Check(model.Locations[MechLocation.LeftTorso].FindChildren("LrmTube*", "Node3D", true, false).Count == 10,
                    "Atlas LRM 20 preserves the ten source launcher positions");
            }
            if (spec.Id == "jenner") TestImportedJenner(model);
            if (spec.Id == "catapult") TestImportedCatapult(model);
            if (spec.Id == "wolverine") TestImportedWolverine(model);
            Check(hangar.GaitPicker.Visible, $"{spec.Chassis} exposes its gait preview selector");
            var legs = new[] { model.Locations[MechLocation.LeftLeg], model.Locations[MechLocation.RightLeg] };
            var ankles = legs.Select(root => (Node3D)root.FindChild("AnklePivot", true, false)).ToArray();
            var feet = legs.SelectMany(Meshes).Where(mesh => mesh.Name == "Foot" || mesh.Name.ToString().StartsWith("ToeCap", StringComparison.Ordinal)).ToArray();
            var shoulders = new[] { MechLocation.LeftArm, MechLocation.RightArm }
                .Select(location => (Node3D)model.Locations[location].FindChild("ShoulderPivot", true, false)).ToArray();
            var maximumArmSwing = new float[2];
            foreach (var gait in new[] { PreviewGait.Walk, PreviewGait.Run })
            {
                hangar.GaitPicker.Select((int)gait); hangar.GaitPicker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)gait);
                var grounded = true; var articulated = false;
                for (var frame = 0; frame < 180; frame++)
                {
                    hangar.Preview._Process(1d / 60);
                    grounded &= feet.All(foot => Bottom(foot) >= -.002f) && feet.Any(foot => Bottom(foot) < .02f);
                    articulated |= Math.Abs(ankles[0].GlobalPosition.Y - ankles[1].GlobalPosition.Y) > .025f;
                    for (var arm = 0; arm < 2; arm++) maximumArmSwing[arm] = Math.Max(maximumArmSwing[arm], Math.Abs(shoulders[arm].Rotation.X));
                    if (frame is 15 or 40 or 70)
                    {
                        model.Rotation = new(0, .32f, 0);
                        await Capture($"{spec.Id}-{gait.ToString().ToLowerInvariant()}-{frame}");
                    }
                }
                Check(grounded && articulated, $"{spec.Chassis} {gait} articulates both legs, clears the floor and maintains foot support");
                var excursion = (spec.Id, gait) switch
                {
                    ("jenner", PreviewGait.Walk) => .45f, ("jenner", _) => .57f,
                    ("atlas", PreviewGait.Walk) => .33f, ("atlas", _) => .45f,
                    ("wolverine", PreviewGait.Walk) => .48f, ("wolverine", _) => .63f,
                    (_, PreviewGait.Walk) => .42f, _ => .51f
                };
                TestLongStride(model, gait, excursion);
                Check(gait == PreviewGait.Walk ? model.RunBlend < .01f : model.RunBlend > .99f,
                    $"{spec.Chassis} actual speed selects the {gait} gait");
                Check(hangar.Preview.Gait == gait, $"{spec.Chassis} preview control selects {gait}");
            }
            if (spec.Id == "wolverine")
                Check(maximumArmSwing[0] > maximumArmSwing[1] * 5 && maximumArmSwing[1] < .025f,
                    "Wolverine braces its right-arm cannon while the left arm swings");
            else if (spec.Id == "atlas") Check(maximumArmSwing.All(value => value > .04f), "Atlas swings both arms while moving");
            else Check(maximumArmSwing.All(value => value < .02f), $"{spec.Chassis} weapon pods have restrained motion");
            var reverseGrounded = true;
            for (var frame = 0; frame < 180; frame++)
            {
                model.AnimateLocomotion(1f / 60, -CombatRules.MovementSpeed(spec, MovementMode.Walk),
                    CombatRules.MovementSpeed(spec, MovementMode.Walk), CombatRules.MovementSpeed(spec, MovementMode.Run));
                reverseGrounded &= feet.All(foot => Bottom(foot) >= -.002f) && feet.Any(foot => Bottom(foot) < .02f);
            }
            Check(reverseGrounded && model.RunBlend < .01f, $"{spec.Chassis} reverse travel uses grounded backward walking");
            foreach (var turn in new[] { -1, 1 })
            {
                var steps = false;
                for (var frame = 0; frame < 150; frame++)
                {
                    model.AnimateLocomotion(1f / 60, 0, CombatRules.MovementSpeed(spec, MovementMode.Walk),
                        CombatRules.MovementSpeed(spec, MovementMode.Run), turn * Mathf.DegToRad(45));
                    steps |= Math.Abs(ankles[0].GlobalPosition.Y - ankles[1].GlobalPosition.Y) > .025f;
                }
                Check(steps, $"{spec.Chassis} steps during a pivot in direction {turn}");
            }
            for (var frame = 0; frame < 180; frame++) model.AnimateLocomotion(1f / 60, 0,
                CombatRules.MovementSpeed(spec, MovementMode.Walk), CombatRules.MovementSpeed(spec, MovementMode.Run));
            Check(legs.All(root => ((Node3D)root.FindChild("HipPivot", true, false)).Rotation.Length() < .001f),
                $"{spec.Chassis} settles back to its authored neutral joint positions");
            var frozen = ankles[0].GlobalTransform;
            model.AnimateLocomotion(0, 1, 1, 2);
            Check(ankles[0].GlobalTransform.IsEqualApprox(frozen), $"{spec.Chassis} freezes its pose without animation time");
            foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
            {
                GetWindow().Size = size; await Frames();
                var camera = hangar.Preview.FindChildren("*", "Camera3D", true, false).OfType<Camera3D>().Single();
                var viewport = camera.GetViewport(); var fits = true;
                for (var angle = 0; angle < 12; angle++)
                {
                    model.Rotation = new(0, angle * Mathf.Tau / 12, 0);
                    foreach (var mesh in meshes) for (var corner = 0; corner < 8; corner++)
                    {
                        var point = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetEndpoint(corner);
                        var screen = camera.UnprojectPosition(point);
                        fits &= !camera.IsPositionBehind(point) && new Rect2(Vector2.One, viewport.GetVisibleRect().Size - Vector2.One * 2).HasPoint(screen);
                    }
                }
                Check(fits, $"{spec.Chassis} complete rotating silhouette fits its preview at {size}");
                Check(hangar.GetGlobalRect().Encloses(hangar.GaitPicker.GetGlobalRect()), $"{spec.Chassis} gait controls fit at {size}");
                model.Rotation = new(0, .32f, 0);
                hangar.GaitPicker.Select(0); hangar.GaitPicker.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
                await Capture($"{spec.Id}-hangar-{size.X}x{size.Y}");
            }
            foreach (var location in Enum.GetValues<MechLocation>())
            {
                model.SetCondition(part => part != location);
                Check(Meshes(model.Locations[location]).All(mesh => !mesh.IsVisibleInTree()),
                    $"{spec.Chassis} hides all {location} details when the location is destroyed");
            }
            model.SetCondition(_ => true); hangar.Refresh();
            Check(ReferenceEquals(model, hangar.Preview.Model), $"{spec.Chassis} assignment refresh preserves its model");
        }
        Check(heights["jenner"] < heights["wolverine"] && heights["wolverine"] < heights["catapult"]
            && heights["catapult"] > 3 && heights["catapult"] < 3.276f, "new chassis have agreed relative heights beneath the Awesome");
        Check(widths["catapult"] > heights["catapult"] * .65f && widths["jenner"] is > 1.4f and < 1.5f,
            "MW5 Catapult keeps its broad launcher silhouette and Jenner retains its source width");

        GetWindow().Size = new(1440, 900); hangar.Hide();
        var field = new CombatViewport(); stage.AddChild(field); field.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); field.OffsetTop = 72;
        var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(mech => mech.Id), BattlefieldTerrain.Flat, 183); sim.EnemyAiEnabled = false;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        for (var i = 0; i < friends.Length; i++)
        {
            friends[i].Position = new((i - 1) * 4.5f, 13); enemies[i].Position = new((i - 1) * 4.5f, 7);
            friends[i].Facing = enemies[i].Facing = Vector2.Down;
        }
        foreach (var unit in sim.Units) foreach (var weapon in unit.Weapons) weapon.Enabled = false;
        sim.RefreshDetection(); field.Display(sim); field.Camera.Size = 13; field.SetProcess(false); await Frames();
        var combatModels = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().ToArray();
        Check(combatModels.Length == definitions.Length * 2 && definitions.All(id => combatModels.Count(model => model.ChassisId == id && model.HasLocomotion) == 2),
            "both combat sides use the same custom chassis and animation rigs as the hangar");
        await Capture("chassis-combat-idle");
        foreach (var unit in friends) { unit.Movement = MovementMode.Run; sim.Move([unit.Id], unit.Position + Vector2.Down * 7); }
        sim.Paused = false;
        for (var frame = 0; frame < 180; frame++) { sim.Advance(1d / 60); field._Process(1d / 60); }
        var movingModels = combatModels.Where(model => model.Position.Z > 13).ToArray();
        Check(movingModels.Length == definitions.Length && movingModels.All(model => model.RunBlend > .9f),
            "combat supplies actual speed to all chassis gait controllers");
        await Capture("chassis-combat-running");
        sim.Paused = true;
        var snapshots = combatModels.Select(model => ((Node3D)model.FindChild("AnklePivot", true, false)).GlobalTransform).ToArray();
        field._Process(1);
        Check(combatModels.Select((model, i) => ((Node3D)model.FindChild("AnklePivot", true, false)).GlobalTransform.IsEqualApprox(snapshots[i])).All(value => value),
            "combat pause preserves all new chassis poses");
        foreach (var enemy in enemies)
        {
            var torso = enemy.Locations[MechLocation.LeftTorso]; enemy.ApplyDamage(MechLocation.LeftTorso, torso.Armor + torso.Structure);
        }
        field._Process(0);
        Check(combatModels.Where(model => model.Position.Z < 10).All(model => !model.Locations[MechLocation.LeftTorso].Visible && !model.Locations[MechLocation.LeftArm].Visible),
            "combat side-torso loss removes attached new-chassis weapons and arms");
        await Capture("chassis-combat-damaged");
        stage.QueueFree(); await Frames();
    }
    private void TestLongStride(MechModel model, PreviewGait gait, float excursion)
    {
        var ankles = new[] { MechLocation.LeftLeg, MechLocation.RightLeg }
            .Select(location => (Node3D)model.Locations[location].FindChild("AnklePivot", true, false)).ToArray();
        var feet = model.FindChildren("Foot", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        var minimumZ = new[] { float.PositiveInfinity, float.PositiveInfinity };
        var maximumZ = new[] { float.NegativeInfinity, float.NegativeInfinity };
        var supportContact = true;
        var spec = DraftCatalog.Mech(model.ChassisId);
        var walk = CombatRules.MovementSpeed(spec, MovementMode.Walk);
        var run = CombatRules.MovementSpeed(spec, MovementMode.Run);
        // Fine sampling distinguishes leg-reach clipping from 60 Hz endpoint sampling.
        for (var sample = 0; sample < 1800; sample++)
        {
            model.AnimateLocomotion(1f / 600, gait == PreviewGait.Walk ? walk : run, walk, run);
            supportContact &= feet.Any(foot => Math.Abs(Enumerable.Range(0, 8)
                .Min(i => (foot.GlobalTransform * foot.Mesh.GetAabb().GetEndpoint(i)).Y)) < .002f);
            for (var leg = 0; leg < 2; leg++)
            {
                var z = (model.GlobalTransform.AffineInverse() * ankles[leg].GlobalPosition).Z;
                minimumZ[leg] = Math.Min(minimumZ[leg], z);
                maximumZ[leg] = Math.Max(maximumZ[leg], z);
            }
        }
        GD.Print($"{model.ChassisId} {gait} foot travel: {maximumZ[0] - minimumZ[0]:F4}, {maximumZ[1] - minimumZ[1]:F4}; support contact: {supportContact}");
        Check(Enumerable.Range(0, 2).All(leg => Math.Abs(maximumZ[leg] - minimumZ[leg] - excursion) < .002f),
            $"{model.ChassisId} {gait} feet cover the requested 50% longer stride");
        Check(supportContact, $"{model.ChassisId} {gait} keeps a support foot planted throughout its longer stride");
    }

}
