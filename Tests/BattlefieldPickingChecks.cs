using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestBattlefieldPicking()
    {
        var originalSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1440, 900);
        var roster = ForceRoster.Create("Picking checks", [DraftCatalog.Mech("jenner"), DraftCatalog.Mech("atlas")], DraftCatalog.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var field = new CombatViewport { Position = new Vector2(40, 40), Size = new Vector2(900, 650) };
        AddChild(field); await Frames();
        if (_screenshots) { GetWindow().GrabFocus(); await Frames(); }
        void MovePointer(Vector2 at)
        {
            if (DisplayServer.GetName() != "headless") GetViewport().WarpMouse(at);
            MouseMotion(at, Vector2.Zero);
        }
        try
        {
            var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), BattlefieldTerrain.Flat);
            var friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
            var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
            friendly.Position = new Vector2(0, 12);
            sim.Units.Last(unit => unit.Side == CombatSide.Friendly).Position = new Vector2(8, 12);
            field.Display(sim); field.Selection = [friendly.Id]; await Frames();
            field.Camera.Position = new(0, 160, 152); field.Camera.LookAt(new(0, 0, 12));
            var model = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().Single(item => item.Position.X == 0 && item.Position.Z == 12);
            foreach (var zoom in new[] { 12f, 50, 220, 280 })
            {
                field.Camera.Size = zoom; field._Process(0);
                var upperBody = field.ScreenPoint(friendly.Position, model.Height * .8f);
                Check(field.PickMech(upperBody) == friendly, $"mech-sized picking reaches the upper body at zoom {zoom}");
                var gap = field.ScreenPoint(friendly.Position, model.Height * .18f);
                Check(field.PickMech(gap) == friendly, $"gaps inside the mech envelope are clickable at zoom {zoom}");
                var beside = field.ScreenPoint(friendly.Position + Vector2.Left * (model.RestBounds.Size.X * model.Scale.X / 2 + .6f), model.Height * .5f);
                Check(field.PickMech(beside) is null, $"mech hit area excludes nearby empty space at zoom {zoom}");
            }
            field.Camera.Size = 12; field._Process(0);
            var top = field.ScreenPoint(friendly.Position, model.Height * .8f);
            Check(top.DistanceTo(field.ScreenPoint(friendly.Position, 1)) > 20, "upper-body check exercises geometry outside the old fixed hit circle");
            var second = sim.Units.Last(unit => unit.Side == CombatSide.Friendly);
            second.Position = new Vector2(4, 12); field.Camera.Size = 35; field._Process(0);
            var secondModel = field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().Single(item => item.Position.X == 4 && item.Position.Z == 12);
            Check(field.PickMech(field.ScreenPoint(second.Position, secondModel.Height * .8f)) == second,
                "a larger chassis uses its own model-sized hit area");
            friendly.Facing = Vector2.Right; field._Process(0);
            Check(field.PickMech(field.Camera.UnprojectPosition(model.GlobalTransform * model.PickBounds()!.Value.GetCenter())) == friendly,
                "picking follows mech facing");
            var intact = model.PickBounds()!.Value;
            foreach (var part in new[] { MechLocation.LeftArm, MechLocation.RightArm })
                friendly.ApplyDamage(part, friendly.Locations[part].Armor + friendly.Locations[part].Structure);
            field._Process(0);
            Check(model.PickBounds()!.Value.Size.X < intact.Size.X, "mech hit bounds shrink when outlying limbs are destroyed");

            // A shallow camera makes the two grounded mechs overlap on screen without overlapping in world space.
            field.Camera.Position = new Vector3(0, 10, 50); field.Camera.LookAt(new Vector3(0, 1.5f, 12));
            enemy.Position = new Vector2(0, 15); sim.RefreshDetection(); field._Process(0);
            var overlap = field.ScreenPoint(friendly.Position, 1.5f);
            Check(field.PickMech(overlap) == enemy, "frontmost enemy wins an overlap instead of unconditional friendly priority");
            field.InspectedEnemy = enemy.Id;
            MovePointer(field.GlobalPosition + overlap); await Frames();
            Check(field.HoveredMech == enemy.Id && field.Selection.SequenceEqual([friendly.Id]) && field.InspectedEnemy == enemy.Id,
                $"hover previews the frontmost mech without changing selection or inspection: hovered={field.HoveredMech}, expected={enemy.Id}, gui={GetViewport().GuiGetHoveredControl()}, mouse={field.GetLocalMousePosition()}, expectedPoint={overlap}, focused={GetWindow().HasFocus()}");
            Check(field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().Count(item => item.Hovered) == 1,
                "only the hovered model receives its stronger outline");
            await Capture("combat-mech-hover");
            string? selected = null; string? commanded = null;
            field.SelectionRequested += (ids, _) => selected = ids.SingleOrDefault();
            field.CommandRequested += (_, id) => commanded = id;
            MouseButton(field.GlobalPosition + overlap, Godot.MouseButton.Left, true);
            MouseButton(field.GlobalPosition + overlap, Godot.MouseButton.Left, false); await Frames();
            Check(selected == friendly.Id && field.InspectedEnemy == enemy.Id, "enemy click inspects the same frontmost mech previewed by hover");
            await RightClick(field.GlobalPosition + overlap);
            Check(commanded == enemy.Id, "right-click targets the same frontmost mech previewed by hover");

            friendly.Position = new Vector2(0, 15); enemy.Position = new Vector2(0, 12); sim.RefreshDetection(); field._Process(0);
            overlap = field.ScreenPoint(enemy.Position, 1.5f);
            Check(field.PickMech(overlap) == friendly, "frontmost friendly blocks picking an enemy behind it");
            MovePointer(field.GlobalPosition + overlap); await Frames();
            await RightClick(field.GlobalPosition + overlap);
            Check(commanded == friendly.Id && field.HoveredMech == friendly.Id, "right-click resolves the hovered friendly for Follow");
            var blocker = new Control { Position = overlap - Vector2.One * 15, Size = Vector2.One * 30, MouseFilter = Control.MouseFilterEnum.Stop };
            field.AddChild(blocker); await Frames();
            MovePointer(field.GlobalPosition + overlap); await Frames();
            Check(field.HoveredMech is null, "UI covering a mech suppresses its battlefield hover");
            blocker.QueueFree(); await Frames();
            MovePointer(field.GlobalPosition + overlap); await Frames();
            field.CommandsEnabled = false;
            Check(field.HoveredMech is null && field.FindChildren("*", "Node3D", true, false).OfType<MechModel>().All(item => !item.Hovered),
                "disabling battlefield commands clears model hover immediately");
            field.CommandsEnabled = true;
            enemy.Position = new Vector2(0, -45); friendly.Position = new Vector2(0, 12); sim.RefreshDetection(); field._Process(0);
            Check(field.PickMech(field.ScreenPoint(enemy.Position, 1.5f)) is null, "lost enemy contacts are not pickable");

            foreach (var terrain in new[]
            {
                new BattlefieldTerrain(1, [new(TerrainKind.Rock, new Vector2(0, 16), 2, 4)], []),
                new BattlefieldTerrain(2, [], [new(new Vector2(0, 17), 5, 6)])
            })
            {
                sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), terrain);
                friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly); friendly.Position = new Vector2(0, 10);
                second = sim.Units.Last(unit => unit.Side == CombatSide.Friendly); second.Position = new Vector2(8, 10);
                field.Display(sim);
                field.Camera.Size = 25; field.Camera.Position = new Vector3(0, 10, 50); field.Camera.LookAt(new Vector3(0, 1, 10));
                field._Process(0);
                var hidden = field.ScreenPoint(friendly.Position, 1.5f);
                Check(field.PickMech(hidden) is null, "solid terrain blocks camera picking of a hidden friendly mech");
                MovePointer(field.GlobalPosition + hidden); await Frames();
                Check(field.HoveredMech is null, "hidden mechs do not acquire a hover outline through terrain");
            }
            var lowRock = new BattlefieldTerrain(3, [new(TerrainKind.Rock, new Vector2(0, 16), 2, 1.8f)], []);
            sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), lowRock);
            friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly); friendly.Position = new Vector2(0, 10);
            sim.Units.Last(unit => unit.Side == CombatSide.Friendly).Position = new Vector2(8, 10);
            field.Display(sim); field.Camera.Size = 25;
            field.Camera.Position = new Vector3(0, 5, 50); field.Camera.LookAt(new Vector3(0, 1, 10)); field._Process(0);
            Check(field.PickMech(field.ScreenPoint(friendly.Position, .3f)) is null
                && field.PickMech(field.ScreenPoint(friendly.Position, 2)) == friendly,
                "partial cover blocks the hidden feet while the exposed upper body remains clickable");
            MovePointer(field.GlobalPosition + field.ScreenPoint(friendly.Position, 2)); await Frames();
            await Capture("combat-mech-partial-cover");
        }
        finally
        {
            field.CancelDrag(); RemoveChild(field); field.QueueFree();
            GetWindow().Size = originalSize; await Frames();
        }
    }
}
