using Godot;
using System.Text.Json;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestCombatUi()
    {
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
        await Capture("deployment-selection");
        await Click(tactical.LaunchButton);
        var sim = tactical.Simulation!; var field = tactical.Battlefield;
        Check(tactical.Modal == CombatModal.None && sim.Paused && tactical.UnitButtons.Count == 2, "launch creates selected mirrored force and starts paused");
        Check(tactical.Selected.Count == 1 && !sim.Contacts(CombatSide.Friendly).Any(), "first friendly selected and enemy starts hidden");
        _app.StartAdvancing(true); Check(!_app.IsAdvancing, "campaign calendar cannot run during combat");
        var friendly = sim.Units.First(item => item.Side == CombatSide.Friendly);
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
        await RightClick(enemyAt);
        Check(tactical.Selected.Select(sim.Unit).All(item => item.PriorityTarget == enemy.Id) && !tactical.SettingPriority, "priority control stores target independently of active orders");
        MouseButton(enemyAt, Godot.MouseButton.Left, true); MouseButton(enemyAt, Godot.MouseButton.Left, false); await Delay(.15);
        Check(tactical.Selected.Count == 0 && field.InspectedEnemy == enemy.Id, "enemy click inspects detected contact without selecting it as friendly");
        var displayed = tactical.FindChildren("*", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
        Check(displayed.Any(text => text.Contains("Observable damage only")), "enemy inspection exposes observable damage rather than exact internals");
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
}
