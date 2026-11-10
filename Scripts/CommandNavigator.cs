using Godot;

namespace Mech;

public enum CommandScreen { MainMenu, Office, Map, Hangar, Draft, Tactical, Rolodex, Missions, Dialog, UnsavedDialog, Lobby }

/// <summary>
/// Session-only routes. Opaque screens hide their ancestors; modal panels keep them visible.
/// Suspending a route retains the actual controls, their contents, back actions and focus.
/// Campaign state and persistence deliberately stay outside this class.
/// </summary>
public sealed class CommandNavigator(Control host, Action pause)
{
    private sealed record Surface(Control View, string? Title, bool Overlay,
        Func<Control?> Focus, Action? Back, Action<float>? RightDrag, Action? RightDragEnded, Func<Vector2, bool>? RightClick);
    /// <summary>A right press that moves farther than this before release drags instead of going back.</summary>
    public const float RightDragThreshold = 5;
    private sealed record Entry(CommandScreen Screen, Control? ReturnFocus);
    private sealed record SuspendedRoute(Entry[] Entries, Control? Focus);
    private readonly Dictionary<CommandScreen, Surface> _surfaces = [];
    private readonly List<Entry> _route = [];
    private readonly List<(PopupMenu Popup, Control? Focus)> _popups = [];
    private SuspendedRoute? _suspended;
    private bool _swallowRightRelease;
    private Vector2? _rightPress;
    private bool _rightDragging;
    private bool _transitioning;
    private int _revision;

    public CommandScreen CurrentScreen => _route[^1].Screen;
    public bool CanResume => _suspended is not null;
    public bool Contains(CommandScreen screen) => _route.Any(entry => entry.Screen == screen);

    public void Register(CommandScreen screen, Control view, string? title, bool overlay,
        Func<Control?> focus, Action? back = null, Action<float>? rightDrag = null, Action? rightDragEnded = null,
        Func<Vector2, bool>? rightClick = null) =>
        _surfaces.Add(screen, new(view, title, overlay, focus, back, rightDrag, rightDragEnded, rightClick));

    public void RegisterPopup(PopupMenu popup)
    {
        popup.AboutToPopup += () =>
        {
            _popups.RemoveAll(item => item.Popup == popup);
            _popups.Add((popup, host.GetViewport().GuiGetFocusOwner()));
            pause();
        };
        popup.PopupHide += () =>
        {
            var index = _popups.FindIndex(item => item.Popup == popup);
            if (index < 0) return;
            var focus = _popups[index].Focus;
            _popups.RemoveAt(index);
            if (!_transitioning) RestoreFocus(focus);
        };
        // Native popup windows receive their own input, outside CommandApp._Input.
        popup.WindowInput += input =>
        {
            if (HandleBackInput(input)) popup.SetInputAsHandled();
        };
    }

    public void Reset(CommandScreen screen, Control? focus = null)
    {
        PrepareTransition();
        _route.Clear();
        _route.Add(new(screen, null));
        Apply();
        RestoreFocus(focus);
    }

    public void ClearResume() => _suspended = null;

    public void NavigateFromOffice(CommandScreen screen, Control returnFocus)
    {
        PrepareTransition();
        _route.Clear();
        _route.Add(new(CommandScreen.Office, null));
        _route.Add(new(screen, returnFocus));
        Apply();
    }

    public void Push(CommandScreen screen, Control? returnFocus = null)
    {
        // CampaignDialog reuses one surface for filing/finances and their confirmations.
        // Replacing its contents keeps the original parent and return focus.
        if (Contains(screen)) return;
        var focus = returnFocus ?? host.GetViewport().GuiGetFocusOwner();
        PrepareTransition();
        _route.Add(new(screen, focus));
        Apply();
    }

    public void Close(CommandScreen screen, Control? focus = null)
    {
        var index = _route.FindIndex(entry => entry.Screen == screen);
        if (index <= 0) return;
        focus ??= _route[index].ReturnFocus;
        PrepareTransition();
        _route.RemoveRange(index, _route.Count - index);
        Apply();
        RestoreFocus(focus);
    }

    public void ReturnToOffice()
    {
        var office = _route.FindIndex(entry => entry.Screen == CommandScreen.Office);
        var focus = office >= 0 && office + 1 < _route.Count ? _route[office + 1].ReturnFocus : null;
        Reset(CommandScreen.Office, focus);
    }

    public void ShowMainMenu(bool resumable)
    {
        if (!Contains(CommandScreen.MainMenu))
            _suspended = resumable ? new(_route.ToArray(), host.GetViewport().GuiGetFocusOwner()) : null;
        Reset(CommandScreen.MainMenu);
    }

    public void Resume()
    {
        if (_suspended is not { } saved || !Contains(CommandScreen.MainMenu)) return;
        PrepareTransition();
        _route.Clear();
        _route.AddRange(saved.Entries);
        _suspended = null;
        Apply();
        RestoreFocus(saved.Focus);
    }

    public void Back()
    {
        pause();
        if (_popups.Count > 0) _popups[^1].Popup.Hide();
        else _surfaces[CurrentScreen].Back?.Invoke();
    }

    /// <summary>Both strategic back gestures share one path; tactical right-click remains an order.</summary>
    public bool HandleBackInput(InputEvent input)
    {
        if (_swallowRightRelease && input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right })
        {
            _swallowRightRelease = false;
            return true;
        }
        if (_rightPress is { } origin)
        {
            if (input is InputEventMouseMotion motion)
            {
                if (!_rightDragging && motion.Position.DistanceTo(origin) <= RightDragThreshold) return false;
                _rightDragging = true;
                _surfaces[CurrentScreen].RightDrag?.Invoke(motion.Relative.X);
                return true;
            }
            if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right })
            {
                var dragged = _rightDragging; var click = _surfaces[CurrentScreen].RightClick; EndRightDrag();
                // A surface may claim a still right click (a hangar slot) instead of going back.
                if (!dragged && click?.Invoke(origin) != true) Back();
                return true;
            }
        }
        if (Contains(CommandScreen.Tactical) && CurrentScreen != CommandScreen.UnsavedDialog) return false;
        // Dragging screens go back on a right click only when the mouse stays put until release.
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } press && _popups.Count == 0
            && _surfaces[CurrentScreen].RightDrag is not null)
        {
            _rightPress = press.Position; _rightDragging = false;
            return true;
        }
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            or InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            if (input is InputEventMouseButton) _swallowRightRelease = true;
            Back();
            return true;
        }
        return false;
    }

    public void RestoreFocus(Control? preferred = null)
    {
        var revision = _revision;
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree() || revision != _revision) return;
            var target = CanFocus(preferred) ? preferred : _surfaces[CurrentScreen].Focus();
            if (CanFocus(target)) target!.GrabFocus();
        }).CallDeferred();
    }

    private static bool CanFocus(Control? control) => GodotObject.IsInstanceValid(control)
        && !control!.IsQueuedForDeletion() && control.IsVisibleInTree()
        && control.FocusMode != Control.FocusModeEnum.None && control is not BaseButton { Disabled: true };

    private void EndRightDrag()
    {
        var dragged = _rightDragging;
        var surface = _surfaces[CurrentScreen];
        _rightPress = null; _rightDragging = false;
        if (dragged) surface.RightDragEnded?.Invoke();
    }

    /// <summary>Releases a pending right drag without going back, e.g. when the window loses focus.</summary>
    public void CancelRightDrag()
    {
        if (_rightPress is null) return;
        EndRightDrag(); _swallowRightRelease = true;
    }

    private void PrepareTransition()
    {
        CancelRightDrag();
        pause();
        _revision++;
        _transitioning = true;
        foreach (var item in _popups.ToArray()) item.Popup.Hide();
        _popups.Clear();
        _transitioning = false;
    }

    private void Apply()
    {
        var first = _route.Count - 1;
        while (first > 0 && _surfaces[_route[first].Screen].Overlay) first--;
        var visible = _route.Skip(first).Select(entry => entry.Screen).ToHashSet();
        foreach (var (screen, surface) in _surfaces) surface.View.Visible = visible.Contains(screen);
        var root = _surfaces[_route[first].Screen];
        DisplayServer.WindowSetTitle(root.Title ?? "Mech · Command");
    }
}
