using Godot;
using System.Text.Json;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestCombatUi()
    {
        // Rendered command checks need a real focused window: combat intentionally ignores
        // commands after desktop focus loss. Do not bypass the production focus guard.
        if (_screenshots) { GetWindow().GrabFocus(); await Frames(); }
        await Click(_app.Office.HangarButton);
        await Click(_app.Hangar.RosterButtons[1]);
        var tabs = _app.Hangar.FindChildren("*", "TabContainer", true, false).OfType<TabContainer>().Single();
        tabs.CurrentTab = 2;
        await Click(_app.Hangar.DeploymentButton);
        var tactical = _app.Tactical;
        Check(tactical.Visible && !_app.Hangar.Visible && tactical.Modal == CombatModal.ForceSelection, "hangar simulation opens force selection");
        Check(tactical.LaunchButton.Disabled && tactical.ForceChoices.All(item => item.Disabled), "unassigned roster cannot launch simulation");
        await Capture("deployment-unavailable");
        await PressKey(Key.Escape);
        Check(_app.Hangar.Visible && _app.Hangar.SelectedMech == _app.State.Roster!.Mechs[1] && tabs.CurrentTab == 2, "cancelling deployment preserves hangar mech and tab");
        var roster = _app.State.Roster!;
        for (var i = 0; i < 4; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        _app.Hangar.Refresh();
        // Refresh recreates the detail panel, so capture its new tab container.
        tabs = _app.Hangar.FindChildren("*", "TabContainer", true, false).OfType<TabContainer>().Single(); tabs.CurrentTab = 2;
        await Frames();
        var before = JsonSerializer.Serialize(_app.State.Snapshot());
        await Click(_app.Hangar.DeploymentButton);
        Check(tactical.ForceChoices[1].ButtonPressed && tactical.ForceChoices.Count(item => item.ButtonPressed) == 1, "force selection preselects eligible hangar mech");
        await Click(tactical.ForceChoices[0]);
        tactical.TerrainSeedInput.Text = "invalid";
        tactical.TerrainSeedInput.EmitSignal(LineEdit.SignalName.TextChanged, "invalid");
        Check(tactical.LaunchButton.Disabled, "invalid terrain seed prevents launch with explanation");
        await Click(tactical.NewTerrainButton);
        Check(!tactical.LaunchButton.Disabled && int.TryParse(tactical.TerrainSeedInput.Text, out _), "New terrain supplies a valid seed");
        tactical.TerrainSeedInput.Text = "183";
        tactical.TerrainSeedInput.EmitSignal(LineEdit.SignalName.TextChanged, "183");
        await Capture("deployment-selection");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(tactical.GetGlobalRect().Encloses(tactical.TerrainSeedInput.GetGlobalRect())
            && tactical.GetGlobalRect().Encloses(tactical.NewTerrainButton.GetGlobalRect())
            && tactical.GetGlobalRect().Encloses(tactical.LaunchButton.GetGlobalRect()), "terrain seed and launch controls fit the minimum window");
        await Capture("deployment-terrain-900x500");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await Click(tactical.LaunchButton);
        var sim = tactical.Simulation!; var field = tactical.Battlefield;
        Check(tactical.Modal == CombatModal.None && sim.Paused && tactical.UnitButtons.Count == 2, "launch creates selected mirrored force and starts paused");
        Check(tactical.Selected.Count == 1 && !sim.Contacts(CombatSide.Friendly).Any(), "first friendly selected and enemy starts hidden");
        _app.StartAdvancing(true); Check(!_app.IsAdvancing, "campaign calendar cannot run during combat");
        var friendly = sim.Units.First(item => item.Side == CombatSide.Friendly);
        Check(sim.Terrain.Seed == 183, "deployment honors the entered terrain seed");
        var hilltop = sim.Terrain.Hills.OrderByDescending(hill => sim.Terrain.HeightAt(hill.Center)).First().Center;
        Check(field.GroundPoint(field.ScreenPoint(hilltop)).DistanceTo(hilltop) < .02f, "height-aware picking matches rendered hill elevation");
        var rock = sim.Terrain.Patches.First(patch => patch.Kind == TerrainKind.Rock);
        await PressKey(Key.Home);
        Check(field.GroundPoint(field.ScreenPoint(rock.Center, rock.Height)).DistanceTo(rock.Center) < .02f, "clicking a rock top picks its footprint rather than ground behind it");
        await RightClick(field.GlobalPosition + field.ScreenPoint(rock.Center, rock.Height));
        Check(friendly.Order == CombatOrder.Move && sim.Terrain.Passable(friendly.Destination)
            && friendly.Destination.DistanceTo(rock.Center) < rock.Radius + 3, "actual ground click inside rock shows a nearby reachable destination");
        await Capture("terrain-overview");
        var destination = field.GlobalPosition + field.ScreenPoint(new Vector2(0, 16));
        await RightClick(destination);
        Check(tactical.Visible && !_app.Office.Visible && friendly.Order == CombatOrder.Move && sim.Elapsed == 0, "actual right-click queues Move while paused without campaign back navigation");
        await Capture("combat-paused");
        // Actual drag selects both friendly stand-ins.
        var points = sim.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => field.GlobalPosition + field.ScreenPoint(item.Position, 1)).ToArray();
        var from = new Vector2(points.Min(point => point.X) - 24, points.Min(point => point.Y) - 24);
        var to = new Vector2(points.Max(point => point.X) + 24, points.Max(point => point.Y) + 24);
        MouseMotion(from, Vector2.Zero); MouseButton(from, Godot.MouseButton.Left, true);
        MouseMotion(to, to - from); MouseButton(to, Godot.MouseButton.Left, false); await Frames();
        Check(tactical.Selected.Count == 2, "actual drag selection selects multiple friendly mechs");
        await RightClick(destination);
        Check(sim.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => item.Destination).Distinct().Count() == 2, "UI group Move assigns distinct destinations");
        var oldSize = field.Camera.Size;
        var center = field.GlobalPosition + field.Size / 2;
        MouseButton(center, Godot.MouseButton.WheelUp, true);
        MouseButton(center, Godot.MouseButton.WheelUp, false); await Frames();
        Check(field.Camera.Size < oldSize, "battlefield wheel zoom works while paused");
        await TestCombatCamera();
        await PressKey(Key.Home);
        await PressKey(Key.Space);
        await Delay(.15);
        Check(!sim.Paused && sim.Elapsed > 0, "Space resumes real-time simulation");
        await PressKey(Key.Escape);
        var pausedAt = sim.Elapsed;
        Check(tactical.Modal == CombatModal.Menu && sim.Paused, "Escape opens suspended combat menu");
        await Delay(.15); Check(sim.Elapsed == pausedAt, "combat menu freezes combat clock");
        await Capture("combat-menu");
        await Click(tactical.ResumeButton);
        Check(!sim.Paused && tactical.Modal == CombatModal.None, "menu Resume restores running state");
        await PressKey(Key.Space); Check(sim.Paused, "Space pauses resumed combat");
        await PressKey(Key.Escape); await PressKey(Key.Escape);
        Check(sim.Paused && tactical.Modal == CombatModal.None, "menu Resume preserves an already-paused state");

        // Bring a contact into view to exercise attack/priority and enemy inspection through mouse input.
        var enemy = sim.Units.First(item => item.Side == CombatSide.Enemy);
        enemy.Position = friendly.Position + new Vector2(-5, -2); sim.RefreshDetection(); await Frames();
        var enemyAt = field.GlobalPosition + field.ScreenPoint(enemy.Position, 1);
        await RightClick(enemyAt);
        Check(tactical.Selected.Select(sim.Unit).All(item => item.Order == CombatOrder.Attack && item.AttackTarget == enemy.Id), "right-click detected enemy issues group Attack");
        var priorityButton = tactical.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "Set priority target…");
        // Scroll to the explicit priority action before using actual mouse input.
        var scroll = priorityButton.GetParent().GetParent().GetParent<ScrollContainer>();
        scroll.EnsureControlVisible(priorityButton); await Frames();
        await Click(priorityButton); Check(tactical.SettingPriority, "separate priority control arms target selection");
        MouseButton(enemyAt, Godot.MouseButton.Right, true);
        CameraMotion(enemyAt + new Vector2(6, 0), new Vector2(6, 0));
        MouseButton(enemyAt, Godot.MouseButton.Right, false); await Frames();
        Check(tactical.SettingPriority && tactical.Selected.Select(sim.Unit).All(item => item.PriorityTarget is null),
            "right-drag leaves priority targeting armed without choosing a target");
        await RightClick(enemyAt);
        Check(tactical.Selected.Select(sim.Unit).All(item => item.PriorityTarget == enemy.Id) && !tactical.SettingPriority, "priority control stores target independently of active orders");
        MouseButton(enemyAt, Godot.MouseButton.Left, true); MouseButton(enemyAt, Godot.MouseButton.Left, false); await Delay(.15);
        Check(tactical.Selected.Count == 0 && field.InspectedEnemy == enemy.Id, "enemy click inspects detected contact without selecting it as friendly");
        var displayed = tactical.FindChildren("*", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
        Check(tactical.Paperdoll.Unit == enemy && displayed.Contains(CombatPaperdoll.Readout(enemy)),
            "enemy inspection exposes the same exact armor, rear armor and internal structure as friendly inspection");
        await Click(tactical.UnitButtons[0]);
        for (var zoom = 0; zoom < 4; zoom++)
        {
            var at = field.GlobalPosition + field.Size / 2;
            MouseButton(at, Godot.MouseButton.WheelUp, true); MouseButton(at, Godot.MouseButton.WheelUp, false);
        }
        await Frames();
        await PressKey(Key.Space); await Delay(.15); await PressKey(Key.Space);
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames();
            Check(tactical.GetGlobalRect().Encloses(tactical.MenuButton.GetGlobalRect()) && field.Size.X > 500 && field.Size.Y > 300, $"combat field and menu survive {size}");
            await Capture($"combat-{size.X}x{size.Y}");
        }
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await TestTerrainUi();
        await PressKey(Key.Escape); await Click(tactical.EndButton);
        Check(tactical.Modal == CombatModal.Results && sim.Outcome == CombatOutcome.Ended, "End Simulation shows damage summary");
        await Capture("combat-results");
        await Click(tactical.ReturnButton);
        Check(_app.Hangar.Visible && !tactical.Visible && _app.Hangar.SelectedMech == roster.Mechs[1] && tabs.CurrentTab == 2, "summary returns to preserved hangar selection and tab");
        Check(JsonSerializer.Serialize(_app.State.Snapshot()) == before, "UI simulation preserves campaign date, finances, roster, equipment, assignments and warriors");
        Check(_app.Hangar.DeploymentButton.HasFocus(), "return restores focus to simulation launcher");
        // Relaunch and exercise automatic completion through the UI.
        await Click(_app.Hangar.DeploymentButton); await Click(tactical.LaunchButton);
        foreach (var unit in tactical.Simulation!.Units.Where(item => item.Side == CombatSide.Enemy)) unit.ApplyDamage(MechLocation.CenterTorso, 1000);
        tactical.TogglePause(); await Delay(.15);
        Check(tactical.Modal == CombatModal.Results && tactical.Simulation.Outcome == CombatOutcome.Victory, "automatic combat outcome opens results");
        await PressKey(Key.Escape);
        Check(_app.Hangar.Visible && JsonSerializer.Serialize(_app.State.Snapshot()) == before, "result Escape preserves campaign and returns to hangar");
        for (var i = 0; i < 4; i++) roster.Assign(roster.Mechs[i].Id, null);
        _app.Hangar.Refresh(); _app.Hangar.Select(0);
        await PressKey(Key.Escape);
    }
    private async Task TestTerrainUi()
    {
        var tactical = _app.Tactical; var sim = tactical.Simulation!; var terrain = sim.Terrain;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var target = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        foreach (var unit in sim.Units) { sim.Hold([unit.Id]); unit.Firing = FiringMode.HoldFire; }
        static IEnumerable<Vector2> Around(Vector2 center, float radius) => Enumerable.Range(0, 24).Select(index => center + Vector2.FromAngle(index * Mathf.Tau / 24) * radius);
        var rock = terrain.Patches.First(patch => patch.Kind == TerrainKind.Rock && terrain.HeightAt(patch.Center) < .01f
            && Around(patch.Center, patch.Radius + 3).All(terrain.Passable));
        friends[0].Position = rock.Center + Vector2.Left * (rock.Radius + 3);
        target.Position = rock.Center + Vector2.Right * (rock.Radius + 3);
        friends[1].Position = Around(target.Position, 6).First(point => terrain.Passable(point)
            && terrain.ClearSight(point, target.Position) && point.DistanceTo(friends[0].Position) > 3);
        sim.RefreshDetection(); tactical.SelectUnits([friends[0].Id], false); await Frames();
        sim.Attack([friends[0].Id], target.Id); await Delay(.15);
        Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("Line of fire blocked by terrain")),
            "combat information explains why a shared contact cannot be fired at through rock");
        await PressKey(Key.Home); await Capture("terrain-blocked-fire");
        var vegetation = terrain.Patches.First(patch => patch.Kind == TerrainKind.Vegetation && terrain.Passable(patch.Center)
            && Around(patch.Center, patch.Radius + 2).Any(point => terrain.Passable(point) && terrain.ClearSight(point, patch.Center)));
        target.Position = vegetation.Center;
        friends[0].Position = Around(target.Position, vegetation.Radius + 2).First(point => terrain.Passable(point) && terrain.ClearSight(point, target.Position));
        sim.RefreshDetection(); tactical.SelectUnits([friends[0].Id], false); await Delay(.15);
        Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("Vegetation: +")),
            "combat information shows the vegetation hit penalty");
        var hill = terrain.Hills.OrderByDescending(hill => terrain.HeightAt(hill.Center)).First();
        friends[0].Position = hill.Center; await Frames();
        Check(tactical.Battlefield.FindChildren("*", "Node3D", true, false).OfType<MechModel>().Any(model =>
            Math.Abs(model.Position.X - hill.Center.X) < .01f && Math.Abs(model.Position.Z - hill.Center.Y) < .01f
            && Math.Abs(model.Position.Y - terrain.HeightAt(hill.Center)) < .01f), "rendered mechs stand at the simulation hill elevation");
        await Capture("terrain-elevation");
    }

}
