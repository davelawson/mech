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
        void OnCommand(Vector2 ground, string? target) { commands++; lastGround = ground; }
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
            Check(commands == 1 && lastGround.DistanceTo(releaseGround) < .01f,
                "small right-click movement issues exactly one order at release position");

            var pivot = field.GroundPoint(field.Size / 2);
            var radius = field.Camera.Position.DistanceTo(CombatViewport.World(pivot));
            await BeginCameraOrbit(at); await Frames();
            Check(field.Camera.Transform.IsEqualApprox(original), "crossing drag threshold discards initial camera movement");
            if (DisplayServer.GetName() != "headless")
                Check(Input.MouseMode == Input.MouseModeEnum.Captured, "right orbit captures and hides cursor");
            CameraMotion(at, new Vector2(40, -20)); await Frames();
            var orbitOffset = field.Camera.Position - CombatViewport.World(pivot);
            Near(Mathf.RadToDeg(Mathf.Atan2(orbitOffset.X, orbitOffset.Z)), -10, "rightward drag orbits clockwise at quarter degree per pixel");
            Near(Mathf.RadToDeg(Mathf.Asin(orbitOffset.Y / radius)), Mathf.RadToDeg(Mathf.Atan2(80, 70)) + 5,
                "upward drag raises camera at quarter degree per pixel");
            Check(field.GroundPoint(field.Size / 2).DistanceTo(pivot) < .01f
                && Math.Abs(orbitOffset.Length() - radius) < .01f, "orbit preserves center-view ground pivot and radius");
            // Release back at the press point: a drag can never become a click again.
            MouseButton(at, Godot.MouseButton.Right, false); await Frames();
            Check(commands == 1 && Input.MouseMode == previousMode, "orbit release restores cursor mode and never orders at its start point");
            if (DisplayServer.GetName() != "headless")
                Check(GetViewport().GetMousePosition().DistanceTo(at) < 2, "orbit restores cursor to original press position");

            await BeginCameraOrbit(at);
            CameraMotion(at, new Vector2(0, 1000)); await Frames();
            Near(-Mathf.RadToDeg(field.Camera.Rotation.X), 15, "orbit clamps shallow elevation to fifteen degrees");
            Check(field.GroundPoint(field.Size / 2).DistanceTo(pivot) < .01f, "shallow view retains ground intersection");
            CameraMotion(at, new Vector2(0, -1000)); await Frames();
            Near(-Mathf.RadToDeg(field.Camera.Rotation.X), 85, "orbit clamps steep elevation to eighty-five degrees");
            Check(field.GroundPoint(field.Size / 2).DistanceTo(pivot) < .01f, "near-overhead view retains ground intersection");
            var beforePan = field.Camera.Position;
            var viewForward = -field.Camera.Basis.Z;
            var expectedForward = new Vector2(viewForward.X, viewForward.Z).Normalized();
            CameraStep(field, .1, Key.W);
            var actualPan = field.Camera.Position - beforePan;
            Check(new Vector2(actualPan.X, actualPan.Z).Normalized().DistanceTo(expectedForward) < .01f
                && Math.Abs(actualPan.Y) < .01f, $"W translates orbit pivot on ground relative to camera facing during rotation: actual {actualPan}, expected direction {expectedForward}, focus {field.HasFocus()}");
            MouseButton(new Vector2(-20, -20), Godot.MouseButton.Right, false); await Frames();
            Check(commands == 1 && Input.MouseMode == previousMode, "release outside battlefield ends orbit without an order");
            original = field.Camera.Transform;
            CameraMotion(at, new Vector2(20, 20));
            Check(field.Camera.Transform.IsEqualApprox(original), "mouse movement after release cannot continue orbit");

            await PressKey(Key.Home);
            Near(field.Camera.Size, 110, "Home restores overview zoom");
            Near(-Mathf.RadToDeg(field.Camera.Rotation.X), Mathf.RadToDeg(Mathf.Atan2(80, 70)), "Home restores default elevation");
            Check(field.GroundPoint(field.Size / 2).Length() < .01f, "Home restores arena center");
            var start = field.Camera.Position;
            CameraStep(field, .1, Key.W);
            var cardinalDistance = field.Camera.Position.DistanceTo(start);
            Near(cardinalDistance, field.Camera.Size * .05f, "WASD speed scales with zoom without acceleration");
            start = field.Camera.Position;
            CameraStep(field, .1, Key.W, Key.D);
            Near(field.Camera.Position.DistanceTo(start), cardinalDistance, "diagonal WASD speed matches cardinal speed");
            start = field.Camera.Position;
            field._Process(.1);
            Check(field.Camera.Position.IsEqualApprox(start), "WASD stops immediately after release");
            MouseButton(at, Godot.MouseButton.WheelUp, true);
            MouseButton(at, Godot.MouseButton.WheelUp, false);
            start = field.Camera.Position;
            CameraStep(field, .1, Key.Up);
            Near(field.Camera.Position.DistanceTo(start), field.Camera.Size * .05f, "arrow movement retains zoom-scaled speed");
            CameraStep(field, 100, Key.W, Key.D);
            var groundFocus = field.Camera.Position - field.Camera.Basis.Z * (field.Camera.Position.Y / field.Camera.Basis.Z.Y);
            Check(groundFocus.DistanceTo(new Vector3(48, 0, -48)) < .01f, "camera pivot clamps to battlefield edges");
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
            tactical.FindChildren("*", "SpinBox", true, false).OfType<SpinBox>().Single().GetLineEdit().GrabFocus();
            Check(Input.MouseMode == previousMode, "leaving battlefield keyboard focus releases capture");
            start = field.Camera.Position;
            CameraStep(field, .1, Key.W);
            MouseButton(at, Godot.MouseButton.Right, false);
            Check(field.Camera.Position.IsEqualApprox(start) && commands == 1, "focused UI controls block WASD and prevent stale click orders");
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
        Position = at, GlobalPosition = at, Relative = delta, ScreenRelative = delta,
        ButtonMask = Input.IsMouseButtonPressed(Godot.MouseButton.Right) ? MouseButtonMask.Right : 0
    });
    private static void CameraStep(CombatViewport field, double delta, params Key[] keys)
    {
        foreach (var key in keys) Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        field._Process(delta);
        foreach (var key in keys) Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
}
