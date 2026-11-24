using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestCombatCamera()
    {
        var tactical = _app.Tactical;
        var field = tactical.Battlefield;
        var at = field.GlobalPosition + field.Size / 2;
        var commands = 0;
        var lastGround = Vector2.Zero;
        void OnCommand(Vector2 ground, string? target, MovementMode? mode) { commands++; lastGround = ground; }
        field.CommandRequested += OnCommand;
        // Manually step keyboard navigation so speed assertions do not depend on frame timing.
        field.SetProcess(false);
        try
        {
            var original = field.Camera.Transform;
            var previousMode = Input.MouseMode;
            MouseButton(at, Godot.MouseButton.Right, true); await Frames();
            Check(commands == 0 && field.Camera.Transform.IsEqualApprox(original), "right press neither orders nor rotates");
            for (var i = 0; i < 4; i++)
            {
                CameraMotion(at + new Vector2(2, 0), new Vector2(2, 0)); await Frames();
                CameraMotion(at, new Vector2(-2, 0)); await Frames();
            }
            var release = at + new Vector2(3, 4);
            CameraMotion(release, new Vector2(3, 4)); await Frames();
            Check(commands == 0 && field.Camera.Transform.IsEqualApprox(original) && Input.MouseMode == previousMode,
                "jitter and exactly five pixels of displacement leave rotation pending");
            var releaseGround = field.GroundPoint(release - field.GlobalPosition);
            MouseButton(release, Godot.MouseButton.Right, false); await Frames();
            Check(commands == 1 && lastGround.DistanceTo(releaseGround) < .3f,
                $"small right-click movement issues exactly one order at release position: commands={commands}, ground={lastGround}, expected={releaseGround}, mouse={GetViewport().GetMousePosition()}, mode={Input.MouseMode}");

            var pivot = field.GroundPoint(field.Size / 2);
            var radius = field.Camera.Position.DistanceTo(CombatViewport.World(pivot, tactical.Simulation!.Terrain.HeightAt(pivot)));
            await BeginCameraOrbit(at); await Frames();
            Check(field.Camera.Transform.IsEqualApprox(original), "crossing drag threshold discards initial camera movement");
            if (DisplayServer.GetName() != "headless")
                Check(Input.MouseMode == Input.MouseModeEnum.Captured, "right orbit captures and hides cursor");
            CameraMotion(at, new Vector2(40, -20)); await Frames();
            var orbitOffset = field.Camera.Position - CombatViewport.World(pivot, tactical.Simulation!.Terrain.HeightAt(pivot));
            Near(Mathf.RadToDeg(Mathf.Atan2(orbitOffset.X, orbitOffset.Z)), -10, "rightward drag orbits clockwise at quarter degree per pixel");
            Near(Mathf.RadToDeg(Mathf.Asin(orbitOffset.Y / radius)), Mathf.RadToDeg(Mathf.Atan2(80, 70)) + 5,
                "upward drag raises camera at quarter degree per pixel");
            Check(field.GroundPoint(field.Size / 2).DistanceTo(pivot) < .3f
                && Math.Abs(orbitOffset.Length() - radius) < .3f, "orbit preserves center-view ground pivot and radius");
            // Release back at the press point: a drag can never become a click again.
            MouseButton(at, Godot.MouseButton.Right, false); await Frames();
            Check(commands == 1 && Input.MouseMode == previousMode, "orbit release restores cursor mode and never orders at its start point");
            if (DisplayServer.GetName() != "headless")
                Check(GetViewport().GetMousePosition().DistanceTo(at) < 2, "orbit restores cursor to original press position");

            await BeginCameraOrbit(at);
            CameraMotion(at, new Vector2(0, 1000)); await Frames();
            Near(-Mathf.RadToDeg(field.Camera.Rotation.X), 15, "orbit clamps shallow elevation to fifteen degrees");
            Check(field.GroundPoint(field.Size / 2).DistanceTo(pivot) < .3f, "shallow view retains ground intersection");
            CameraMotion(at, new Vector2(0, -1000)); await Frames();
            Near(-Mathf.RadToDeg(field.Camera.Rotation.X), 85, "orbit clamps steep elevation to eighty-five degrees");
            Check(field.GroundPoint(field.Size / 2).DistanceTo(pivot) < .3f, "near-overhead view retains ground intersection");
            var beforePan = field.Camera.Position;
            var viewForward = -field.Camera.Basis.Z;
            var expectedForward = new Vector2(viewForward.X, viewForward.Z).Normalized();
            CameraStep(field, .1, Key.W);
            var actualPan = PanShift(field.Camera.Position - beforePan, viewForward);
            Check(actualPan.Normalized().DistanceTo(expectedForward) < .01f && PivotOnGround(field),
                $"W translates orbit pivot on ground relative to camera facing during rotation: actual {actualPan}, expected direction {expectedForward}, focus {field.HasFocus()}");
            MouseButton(new Vector2(-20, -20), Godot.MouseButton.Right, false); await Frames();
            Check(commands == 1 && Input.MouseMode == previousMode, "release outside battlefield ends orbit without an order");
            original = field.Camera.Transform;
            CameraMotion(at, new Vector2(20, 20));
            Check(field.Camera.Transform.IsEqualApprox(original), "mouse movement after release cannot continue orbit");

            await PressKey(Key.Home);
            Near(field.Camera.Size, 6600, "Home restores expanded overview zoom");
            Near(-Mathf.RadToDeg(field.Camera.Rotation.X), Mathf.RadToDeg(Mathf.Atan2(80, 70)), "Home restores default elevation");
            Check(field.GroundPoint(field.Size / 2).Length() < .3f, "Home restores arena center");
            var view = -field.Camera.Basis.Z;
            var start = field.Camera.Position;
            CameraStep(field, .1, Key.W);
            var cardinalDistance = PanShift(field.Camera.Position - start, view).Length();
            Near(cardinalDistance, field.Camera.Size * .05f, "WASD speed scales with zoom without acceleration");
            start = field.Camera.Position;
            CameraStep(field, .1, Key.W, Key.D);
            Near(PanShift(field.Camera.Position - start, view).Length(), cardinalDistance, "diagonal WASD speed matches cardinal speed");
            start = field.Camera.Position;
            CameraStep(field, .1, Key.Shift, Key.S);
            Near(PanShift(field.Camera.Position - start, view).Length(),
                cardinalDistance * CombatViewport.FastPanMultiplier, "Shift pans five times faster");
            start = field.Camera.Position;
            field._Process(.1);
            Check(field.Camera.Position.IsEqualApprox(start), "WASD stops immediately after release");
            MouseButton(at, Godot.MouseButton.WheelUp, true);
            MouseButton(at, Godot.MouseButton.WheelUp, false);
            start = field.Camera.Position;
            CameraStep(field, .1, Key.Up);
            Near(PanShift(field.Camera.Position - start, view).Length(), field.Camera.Size * .05f, "arrow movement retains zoom-scaled speed");
            await PressKey(Key.Home);
            field.Camera.Size = 1500;
            // Pan W (toward -Z) along the hilliest line from the arena centre, so the view crosses slopes.
            var terrain = tactical.Simulation!.Terrain;
            float Relief(float x)
            {
                var heights = Enumerable.Range(0, 21).Select(i => terrain.HeightAt(new Vector2(x, -i * 75))).ToArray();
                return heights.Max() - heights.Min();
            }
            var line = Enumerable.Range(-8, 17).Select(i => i * 250f).MaxBy(Relief);
            if (line != 0) CameraStep(field, Math.Abs(line) / (field.Camera.Size * .5f), line > 0 ? Key.D : Key.A);
            var marker = field.Camera.Position - field.Camera.Basis.Z * new Vector2(4200, 4800).Length();
            var previous = field.Unproject(marker);
            var steps = new List<Vector2>(); var reliefs = new List<float>();
            for (var i = 0; i < 20; i++)
            {
                CameraStep(field, .1, Key.W);
                var now = field.Unproject(marker); steps.Add(now - previous); previous = now;
                reliefs.Add(terrain.HeightAt(field.GroundPoint(field.Size / 2)));
                if (!PivotOnGround(field)) { Check(false, $"pan keeps orbit pivot on the ground at step {i}"); break; }
            }
            Check(reliefs.Max() - reliefs.Min() > 10, $"slope pan test crosses changing terrain: relief {reliefs.Max() - reliefs.Min():0.0} m on line x={line}");
            Check(steps.All(step => step.DistanceTo(steps[0]) < .1f),
                $"WASD pans at a steady screen pace over slopes: steps {string.Join(", ", steps.Select(step => step.ToString("0.00")))}");
            await PressKey(Key.Home);
            CameraStep(field, 100, Key.W, Key.D);
            var groundFocus = field.GroundPoint(field.Size / 2);
            Check(groundFocus.DistanceTo(new Vector2(2870.25f, -2870.25f)) < .3f, "camera pivot clamps to expanded battlefield edges");
            await PressKey(Key.Home);

            await BeginCameraOrbit(at);
            await PressKey(Key.Escape);
            Check(tactical.Modal == CombatModal.Menu && Input.MouseMode == previousMode, "opening combat menu cancels capture");
            start = field.Camera.Position;
            CameraStep(field, .1, Key.W);
            MouseButton(at, Godot.MouseButton.Right, false);
            Check(field.Camera.Position.IsEqualApprox(start) && commands == 1, "menu blocks navigation and cancels pending orders");
            await PressKey(Key.Escape);
            await BeginCameraOrbit(at);
            field.ReleaseFocus();
            Check(Input.MouseMode == previousMode, "leaving battlefield keyboard focus releases capture");
            start = field.Camera.Position;
            CameraStep(field, .1, Key.W);
            MouseButton(at, Godot.MouseButton.Right, false);
            Check(!field.Camera.Position.IsEqualApprox(start) && commands == 1,
                "WASD pans without battlefield focus, and a cancelled orbit leaves no stale click order");
            field.GrabFocus();

            await BeginCameraOrbit(at);
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            Check(Input.MouseMode == previousMode, "window focus loss releases capture");
            start = field.Camera.Position;
            CameraStep(field, .1, Key.W);
            GetWindow().EmitSignal(Window.SignalName.FocusEntered);
            MouseButton(at, Godot.MouseButton.Right, false);
            Check(field.Camera.Position.IsEqualApprox(start) && commands == 1, "window focus loss blocks movement and cancels pending order");
            await BeginCameraOrbit(at);
            field.Hide();
            Check(Input.MouseMode == previousMode, "leaving battlefield visibility releases capture");
            field.Show(); field.GrabFocus();
            MouseButton(at, Godot.MouseButton.Right, false);
            Check(commands == 1, "returning to battlefield cannot resurrect a cancelled order");
            await PressKey(Key.Home);
        }
        finally
        {
            field.CancelDrag();
            field.CommandRequested -= OnCommand;
            field.SetProcess(true);
        }
    }

    private async Task BeginCameraOrbit(Vector2 at)
    {
        MouseMotion(at, Vector2.Zero);
        MouseButton(at, Godot.MouseButton.Right, true);
        CameraMotion(at + new Vector2(6, 0), new Vector2(6, 0));
        await Frames();
    }
    private static void CameraMotion(Vector2 at, Vector2 delta) => Input.ParseInputEvent(new InputEventMouseMotion
    {
        Position = ToWindow(at), GlobalPosition = ToWindow(at), Relative = ToWindowDelta(delta), ScreenRelative = ToWindowDelta(delta),
        ButtonMask = Input.IsMouseButtonPressed(Godot.MouseButton.Right) ? MouseButtonMask.Right : 0
    });
    // The camera's horizontal shift with any slide along its view axis removed; such a slide leaves the orthographic picture unchanged.
    private static Vector2 PanShift(Vector3 delta, Vector3 view)
    {
        var level = delta - view * (delta.Y / view.Y);
        return new Vector2(level.X, level.Z);
    }
    private static bool PivotOnGround(CombatViewport field)
    {
        // The camera orbits a pivot OrbitRadius ahead along its view axis; that pivot is the ground under the view's centre.
        var ground = field.GroundPoint(field.Size / 2);
        var pivot = field.Camera.Position - field.Camera.Basis.Z * new Vector2(4200, 4800).Length();
        return new Vector2(pivot.X, pivot.Z).DistanceTo(ground) < .5f && Math.Abs(pivot.Y - field.Simulation!.Terrain.HeightAt(ground)) < .5f;
    }
    private static void CameraStep(CombatViewport field, double delta, params Key[] keys)
    {
        foreach (var key in keys) Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        field._Process(delta);
        foreach (var key in keys) Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
}
