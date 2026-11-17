using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>The time panel: elapsed time, cassette icon buttons, their tooltips and hotkeys, and fast-forward. Starts and ends paused at 1x.</summary>
    private async Task TestTimePanel()
    {
        var tactical = _app.Tactical; var field = tactical.Battlefield; var sim = tactical.Simulation!;
        var panel = tactical.PauseButton.GetParent().GetParent().GetParent<Control>();
        var rect = panel.GetGlobalRect(); var screen = tactical.GetGlobalRect();
        Check(Math.Abs(rect.GetCenter().X - screen.GetCenter().X) <= 1 && rect.Position.Y - screen.Position.Y < 8 && rect.End.Y - screen.Position.Y <= 66,
            $"the time panel sits at the top center ({rect})");
        Check(new[] { tactical.PauseButton, tactical.PlayButton, tactical.FastForwardButton }.All(button => button.Text == ""
            && button.GetChildren().OfType<TimeControlIcon>().Any()), "the time buttons show icons rather than text");
        Check(tactical.PauseButton.TooltipText == "Pause  [Space]" && tactical.PlayButton.TooltipText == "1x  [,]"
            && tactical.FastForwardButton.TooltipText == "4x  [.]", "time button tooltips name their speeds and hotkeys");
        Check(sim.Paused && tactical.TimeMode == "Pause" && sim.TimeScale == 1, "the time panel shows the paused battle at 1x");
        await Frames();
        var elapsed = TimeSpan.FromSeconds(sim.Elapsed);
        Check(tactical.TimeLabel.Text == elapsed.ToString(@"mm\:ss"), $"the time panel shows the elapsed time ({tactical.TimeLabel.Text})");
        await Capture("combat-time-panel");

        await Click(tactical.PauseButton);
        Check(!sim.Paused && tactical.TimeMode == "1x", "the pause button resumes a paused battle, as Space does");
        await Click(tactical.PauseButton);
        Check(sim.Paused && tactical.TimeMode == "Pause", "the pause button pauses, as Space does");

        tactical.OpenMenu();
        await PressKey(Key.Period);
        Check(sim.Paused && sim.TimeScale == 1, "speed hotkeys do nothing while a modal is open");
        await PressKey(Key.Escape);
        // Clicking a time button leaves no control holding the keyboard: the hotkeys still reach the battle.
        await Click(tactical.PlayButton); await Click(tactical.PauseButton);
        Check(sim.Paused && GetViewport().GuiGetFocusOwner() is not BaseButton, "clicked time buttons never take keyboard focus");
        await PressKey(Key.Period);
        Check(!sim.Paused && sim.TimeScale == 4 && tactical.TimeMode == "4x", ". resumes the battle at 4x right after clicking Pause");
        var from = sim.Elapsed; var started = Time.GetTicksMsec();
        await Delay(.3);
        var rate = (sim.Elapsed - from) / ((Time.GetTicksMsec() - started) / 1000d);
        Check(rate > 2.5, $"fast-forward runs the battle about four times as fast ({rate:0.0}x)");
        await PressKey(Key.Space);
        Check(sim.Paused && sim.TimeScale == 4 && tactical.TimeMode == "Pause", "Space pauses a fast-forwarding battle and keeps its speed");
        await PressKey(Key.Comma);
        Check(!sim.Paused && sim.TimeScale == 1 && tactical.TimeMode == "1x", ", resumes the battle at 1x");
        await Click(tactical.FastForwardButton);
        Check(!sim.Paused && sim.TimeScale == 4 && !tactical.FastForwardButton.HasFocus(), "the fast-forward button sets 4x without taking focus");
        await Click(tactical.PlayButton);
        Check(sim.TimeScale == 1, "the play button sets 1x");
        await PressKey(Key.Space);
        Check(sim.Paused && sim.TimeScale == 1, "the battle is paused at 1x again");
    }
}
