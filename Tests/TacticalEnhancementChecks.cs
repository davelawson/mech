using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestTacticalEnhancements()
    {
        var window = GetWindow(); var oldSize = window.Size;
        window.Size = new Vector2I(1440, 900);
        var roster = ForceRoster.Create("Tactical shortcuts", Enumerable.Repeat(DraftCatalog.Mech("atlas"), 4), DraftCatalog.Warriors.Take(4));
        for (var i = 0; i < 4; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var screen = new CombatScreen { Theme = Ui.CreateTheme(), Size = new Vector2(1440, 900) };
        AddChild(screen);
        try
        {
            screen.Open(roster, roster.Mechs[0].Id);
            screen.MirrorMatchButton.EmitSignal(BaseButton.SignalName.Pressed);
            foreach (var choice in screen.ForceChoices) choice.ButtonPressed = true;
            screen.TerrainSeedInput.Text = "183";
            screen.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            var sim = screen.Simulation!; var field = screen.Battlefield;
            var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
            bool KeyPress(Key key, bool shift = false, Key physical = Key.None) => screen.HandleKey(new InputEventKey
                { Keycode = key, PhysicalKeycode = physical, Pressed = true, ShiftPressed = shift });
            Check(!screen.BattleLog.Visible && screen.BattleLog.Tabs.GetTabTitle(0) == "Combat"
                && screen.BattleLog.Tabs.GetTabTitle(1) == "Debug", "battle log starts hidden with Combat and Debug tabs");
            field.InspectedEnemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy).Id;
            KeyPress(Key.Key4);
            Check(screen.Selected.SequenceEqual([friends[3].Id]) && field.InspectedEnemy is null,
                "number key replaces selection using stable deployment slot and clears enemy inspection");
            KeyPress(Key.Exclam, true, Key.Key1); KeyPress(Key.Key4, true);
            Check(screen.Selected.SequenceEqual([friends[0].Id]), "shift-number adds and removes slots even with shifted key symbols");
            KeyPress(Key.Key1, true);
            Check(screen.Selected.Count == 0, "shift-number can toggle the last selected mech off");
            field.Camera.Size = 220; KeyPress(Key.Quoteleft);
            Check(Math.Abs(field.Camera.Size - 50) < .01 && field.GroundPoint(field.Size / 2).DistanceTo(new Vector2(0, 64)) < .02,
                "backtick with no selection restores tactical zoom and centers all friendly slots");

            friends[0].Position = new(-90, -80); friends[1].Position = new(90, 80);
            screen.SelectUnits([friends[0].Id, friends[1].Id], false);
            field._GuiInput(new InputEventKey { Keycode = Key.Home, Pressed = true });
            KeyPress(Key.Quoteleft);
            Check(field.Camera.Size > 50 && field.GroundPoint(field.Size / 2).Length() < .02,
                "backtick centers the group and widens reset zoom to fit spread-out mechs");
            Check(Math.Abs(field.Camera.Rotation.Y) < .001
                && Math.Abs(-field.Camera.Rotation.X - Mathf.Atan2(80, 70)) < .001,
                "selection framing resets yaw and pitch");
            Check(friends.Take(2).All(unit => new Rect2(Vector2.One * 16, field.Size - Vector2.One * 32)
                .HasPoint(field.ScreenPoint(unit.Position, 1))), "selection framing keeps both widely spaced mechs within the view");

            var width = field.Size.X; var camera = field.Camera.Transform;
            KeyPress(Key.Asciitilde, true, Key.Quoteleft); await Frames();
            Check(screen.BattleLog.Visible && field.Size.X < width && field.Position.X == screen.BattleLog.Size.X
                && field.Camera.Transform.IsEqualApprox(camera), "tilde docks the log beside the battlefield without triggering camera reset");
            foreach (var size in new[] { new Vector2I(900, 500), new Vector2I(1440, 900) })
            {
                window.Size = size; screen.Size = size; await Frames();
                Check(screen.GetGlobalRect().Encloses(screen.BattleLog.GetGlobalRect()) && field.Size.X > 250
                    && !field.GetGlobalRect().Intersects(screen.BattleLog.GetGlobalRect()),
                    $"log docking preserves a usable unobscured battlefield at {size}");
                KeyPress(Key.Quoteleft);
                Check(friends.Take(2).All(unit => new Rect2(Vector2.One * 16, field.Size - Vector2.One * 32)
                    .HasPoint(field.ScreenPoint(unit.Position, 1))), $"group framing accounts for docked viewport aspect at {size}");
            }
            var input = screen.FindChildren("*", "SpinBox", true, false).OfType<SpinBox>().Single().GetLineEdit();
            input.GrabFocus();
            Check(!KeyPress(Key.Key3) && !KeyPress(Key.Quoteleft), "combat shortcuts preserve editing in numeric input");
            field.GrabFocus(); screen.OpenMenu();
            Check(!KeyPress(Key.Key3) && !KeyPress(Key.Quoteleft, true), "combat modal blocks selection and log shortcuts");
            screen.Resume();
            friends[3].ApplyDamage(MechLocation.Head, 1000); KeyPress(Key.Key4);
            Check(screen.Selected.SequenceEqual([friends[3].Id]) && screen.Paperdoll.Unit == friends[3],
                "destroyed friendly slots remain selectable for inspection");

            // Generate real resolved shots without damaging this screen's paused deployment.
            var spec = DraftCatalog.Mech("atlas") with { HeatSinks = 0, Components = [], Ammo = [],
                Weapons = [new("log-laser", "Log Laser", MechLocation.RightArm, false, 0, new(DamageKind.Direct, 1, 1, 0), 0, 20, 25, 30, null, WeaponKind.Flamer)] };
            CombatUnit Unit(CombatSide side) => new(side.ToString(), spec, DraftCatalog.Warriors[0] with { Gunnery = -10 }, side,
                spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)),
                [new EquipmentCondition("log-laser", ComponentCondition.Operational)], [], side == CombatSide.Friendly ? Vector2.Zero : new Vector2(2, 0));
            var source = Unit(CombatSide.Friendly); var target = Unit(CombatSide.Enemy);
            source.Facing = Vector2.Right; target.Facing = Vector2.Left; target.Firing = FiringMode.HoldFire;
            var logSim = new CombatSimulation([source, target], 41) { Paused = false, EnemyAiEnabled = false };
            void Fire(int count)
            {
                for (var i = 0; i < count; i++) { source.Weapons[0].Remaining = 0; logSim.Advance(.05); }
            }
            screen.BattleLog.Display(logSim); Fire(100); await Frames(); await Frames();
            var combat = screen.BattleLog.Combat; var bar = combat.GetVScrollBar();
            Check(logSim.History.Count == 100 && combat.GetParsedText().Contains(logSim.History[0].CombatText!)
                && combat.GetParsedText().Contains(logSim.History[^1].CombatText!), "log retains entries beyond the initial twelve");
            Check(bar.MaxValue > bar.Page && bar.Value >= bar.MaxValue - bar.Page - 2, "new log entries follow the newest event when at the bottom");
            bar.Value = (bar.MaxValue - bar.Page) / 2; var scrolled = bar.Value;
            Fire(4); await Frames(); await Frames();
            Check(Math.Abs(bar.Value - scrolled) < 2, "incoming events preserve a reader's scrollback position");
            var at = combat.GlobalPosition + combat.Size / 2;
            if (DisplayServer.GetName() != "headless") GetViewport().WarpMouse(at);
            MouseMotion(at, Vector2.Zero); var zoom = field.Camera.Size;
            MouseButton(at, Godot.MouseButton.WheelUp, true); MouseButton(at, Godot.MouseButton.WheelUp, false); await Frames();
            Check(bar.Value < scrolled && field.Camera.Size == zoom, "wheel over the log scrolls history without zooming the camera");
            var combatScroll = bar.Value;
            screen.BattleLog.Tabs.CurrentTab = 1; await Frames(); await Frames();
            var debug = screen.BattleLog.Debug.GetVScrollBar();
            Check(debug.Value >= debug.MaxValue - debug.Page - 2, "Debug initially opens at the newest retained event");
            Fire(3); await Frames(); await Frames();
            screen.BattleLog.Tabs.CurrentTab = 0; await Frames();
            Check(Math.Abs(bar.Value - combatScroll) < 2, "Combat and Debug maintain independent scroll positions");
            bar.Value = bar.MaxValue; Fire(3); await Frames(); await Frames();
            Check(bar.Value >= bar.MaxValue - bar.Page - 2, "scrolling back to the bottom resumes following events");
            await Capture("combat-log-combat");
            screen.BattleLog.Tabs.CurrentTab = 1; await Frames(); await Capture("combat-log-debug");
            KeyPress(Key.Quoteleft, true); KeyPress(Key.Quoteleft, true); await Frames();
            Check(screen.BattleLog.Tabs.CurrentTab == 1 && screen.BattleLog.Debug.GetParsedText().Contains(logSim.History[0].DebugText),
                "hiding the log preserves its tab and complete battle history");
            screen.BattleLog.Display(sim); await Frames();
            Check(screen.BattleLog.Tabs.CurrentTab == 0 && screen.BattleLog.Combat.GetTotalCharacterCount() == 0
                && screen.BattleLog.Debug.GetTotalCharacterCount() == 0, "a new battle resets tabs and both histories");
        }
        finally
        {
            RemoveChild(screen); screen.QueueFree(); window.Size = oldSize; await Frames();
        }
    }
}
