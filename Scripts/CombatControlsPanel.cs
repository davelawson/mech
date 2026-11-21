using Godot;

namespace Mech;

/// <summary>Battle controls reference, toggled with ? (Shift+/). It is not a modal: the battle keeps running and every
/// order and hotkey still works while it is open.</summary>
public partial class CombatControlsPanel : PanelContainer
{
    public event Action? CloseRequested;
    public Button CloseButton { get; private set; } = null!;
    private VBoxContainer _body = null!;
    private bool? _coop;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line));
        var column = new VBoxContainer(); AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        var title = Ui.Label(header, "CONTROLS  [?]", 14, Ui.Gold); title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CloseButton = Ui.Button(header, "×"); CloseButton.TooltipText = "Hide controls [?]";
        CloseButton.Pressed += () => CloseRequested?.Invoke();
        _body = RosterUi.Scroll(column, margin: 6);
        Display(false);
        Hide();
    }

    /// <summary>Fills the reference; co-op battles add the ping and the leader-only speed notes.</summary>
    public void Display(bool coop)
    {
        if (_body is null || _coop == coop) return;
        _coop = coop; RosterUi.Clear(_body);
        Section("SELECTION",
            ("Click / drag", "Select a friendly mech / box-select a group"),
            ("Shift+click", "Add to or remove from the selection"),
            ("1–4 / Shift+1–4", "Select / toggle a deployment slot (or click its card)"),
            ("Click an enemy", "Inspect it; the friendly selection stays as the range reference"),
            ("Hover an enemy", "Range lines from the selected friendlies"),
            ("Hover the paperdoll", "Systems installed in that location, by condition"));
        (string, string)[] orders =
        [
            ("Right-click ground", "Move there"),
            ("Right-click enemy", "Set it as the priority target"),
            ("Right-click friendly", "Follow it"),
            ("Alt / Ctrl+right-click", "The same, also setting Walk / Run"),
            ("Z", "Stop and clear orders"),
            ("J, then right-click", "Jump to the ground point, within each mech's range circle (Escape cancels)"),
            ("Clear priority target", "Sidebar button: drop the selection's priority target"),
        ];
        Section("ORDERS", coop ? [.. orders, ("V", "Ping the ground under the cursor for every player")] : orders);
        Section("FIRING AND WEAPONS",
            ("G / F", "Hold fire / Fire at will"),
            ("Weapon toggles", "The WEAPONS list in the sidebar switches each weapon of the primary selection on or off. "
                + "A disabled weapon stays off whatever the orders; it is not weapon loss and comes back on when toggled."),
            ("Ammo gauge", "The yellow gauge under the speed gauge shows ammunition left. Drag its pointer to set ammo economy for the selection: "
                + "fully left, ammo weapons fire freely; each step right accepts one less penalty modifier (range, movement, cover, heat, "
                + "damage), until fully right fires only perfect shots. Energy weapons are unaffected."));
        Section("HEAT LIMIT",
            ("Drag / click heat bar", "Set the heat limit (0–30) on the sidebar paperdoll's heat bar, for the whole selection, "
                + "shut-down mechs included, also while paused."),
            ("", "Weapons and jumps wait while heat is above the limit. A volley may carry heat past it. Every mech starts at 10."));
        Section("SPEED",
            ("Speed gauge", "Drag or click the gauge under the sidebar paperdoll to set the selection's desired speed, "
                + "from 10 km/h up to the running speed (it snaps to the walk and run notches). Hover it for the actual and desired speed."),
            ("X / C", "Set the desired speed to the walking / running speed"),
            ("", "Walking or running is read from actual speed: faster than walking speed counts as running for heat and aim."),
            ("Maintain Range", "Tick the box to keep the slider's distance from the priority target when there is no Move or "
                + "Follow order. The slider sets that distance, applied when you let go."),
            ("Space", coop ? "Pause or resume (your own pause; the battle waits while any player holds one)" : "Pause or resume"),
            (", / .", "Run at 1x / fast-forward at 4x" + (coop ? " (the leader only)" : "")),
            ("", "Orders can be given while paused."));
        Section("STRESS AND EJECTION",
            ("Stress", "Each MechWarrior is Calm, Steady, Excited, Stressed or Panicked, shown on the unit card and above the paperdoll. "
                + "A nerve check every 5 s (2d6 + Grit + heat, damage, injuries, being shot at, losses, being outnumbered) moves it a step. "
                + "Stressed plays every skill but Grit 1 worse, Panicked 2. No pilot grows Stressed before their mech is damaged. "
                + "A Panicked pilot who fails again may eject, but only once their mech has taken internal damage."),
            ("", "Pull stressed pilots out of the fight: not being shot at calms them."),
            ("Eject", "Sidebar button, pressed twice to confirm: the pilot ejects, injured but alive, and the mech is left disabled. "
                + "A destroyed center torso always ejects the pilot."),
            ("Surrender", "An enemy that loses every weapon, or is crippled and losing, may offer to surrender. The battle pauses: "
                + (coop ? "the leader " : "") + "Accept takes the pilot hostage (no salvage of that mech); Refuse makes an unarmed mech flee."));
        Section("CAMERA",
            ("WASD / arrows / middle-drag", "Pan (hold Shift for 5× speed)"),
            ("Wheel", "Zoom (scrolls a panel under the cursor)"),
            ("Right-drag, Q/E, Shift+middle-drag", "Rotate"),
            ("Backtick (`)", "Frame the selection"),
            ("Home", "Arena overview"));
        Section("PANELS",
            ("?", "Show or hide these controls (the battle keeps running)"),
            ("~ (Shift+`)", "Show or hide the battle log"),
            ("Escape", "Close these controls; otherwise open the combat menu"));
    }

    private void Section(string heading, params (string Input, string Action)[] rows)
    {
        RosterUi.Text(_body, heading, 12, Ui.Gold);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 4);
        _body.AddChild(grid);
        foreach (var (input, action) in rows)
        {
            var key = RosterUi.Text(grid, input, 12, Ui.Gold); key.CustomMinimumSize = new Vector2(110, 0);
            key.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            var text = RosterUi.Text(grid, action, 12); text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.VerticalAlignment = VerticalAlignment.Top;
        }
    }
}
