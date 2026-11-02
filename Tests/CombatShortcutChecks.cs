using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Order hotkeys, Alt/Ctrl right-click movement modes, the location systems tooltip and unit-card paperdolls.</summary>
    private async Task TestCombatShortcuts()
    {
        var tactical = _app.Tactical; var field = tactical.Battlefield; var sim = tactical.Simulation!;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var selection = tactical.Selected.ToArray();
        var settings = friends.Select(unit => (unit.Movement, unit.Firing)).ToArray();
        CheckLocationSystems(Check);
        try
        {
            tactical.SelectUnits(friends.Select(unit => unit.Id).ToArray(), false); field.GrabFocus();
            await PressKey(Key.C);
            Check(friends.All(unit => unit.Movement == MovementMode.Run), "C sets the selected mechs to Run");
            await PressKey(Key.X);
            Check(friends.All(unit => unit.Movement == MovementMode.Walk), "X sets the selected mechs to Walk");
            await PressKey(Key.G);
            Check(friends.All(unit => unit.Firing == FiringMode.HoldFire), "G sets Hold fire");
            await PressKey(Key.F);
            Check(friends.All(unit => unit.Firing == FiringMode.FireAtWill), "F sets Fire at will");
            tactical.IssueCommand(new Vector2(0, 600), null);
            Check(friends.All(unit => unit.Order == CombatOrder.Move), "a ground command moves the group");
            await PressKey(Key.Z);
            Check(friends.All(unit => unit.Order == CombatOrder.Hold), "Z stops the selected mechs and clears their orders");
            var labels = tactical.FindChildren("*", "Button", true, false).OfType<Button>().Select(button => button.Text).ToArray();
            Check(new[] { "[X]", "[C]", "[F]", "[G]", "[Z]" }.All(key => labels.Any(text => text.Contains(key))),
                "order buttons show their hotkeys");

            tactical.PauseButton.GrabFocus();
            await PressKey(Key.C);
            Check(friends.All(unit => unit.Movement == MovementMode.Walk), "order hotkeys need battlefield focus");
            field.GrabFocus();

            var at = field.GlobalPosition + field.Size / 2;
            async Task ModifiedRightClick(bool ctrl, bool alt)
            {
                MouseMotion(at, Vector2.Zero);
                foreach (var pressed in new[] { true, false })
                    Input.ParseInputEvent(new InputEventMouseButton
                    {
                        Position = ToWindow(at), GlobalPosition = ToWindow(at), ButtonIndex = Godot.MouseButton.Right,
                        Pressed = pressed, CtrlPressed = ctrl, AltPressed = alt
                    });
                await Frames();
            }
            await ModifiedRightClick(ctrl: true, alt: false);
            Check(friends.All(unit => unit.Movement == MovementMode.Run && unit.Order == CombatOrder.Move),
                "Ctrl+right-click orders the group to run to the target");
            await ModifiedRightClick(ctrl: false, alt: true);
            Check(friends.All(unit => unit.Movement == MovementMode.Walk && unit.Order == CombatOrder.Move),
                "Alt+right-click orders the group to walk to the target");
            await ModifiedRightClick(ctrl: false, alt: false);
            Check(friends.All(unit => unit.Movement == MovementMode.Walk), "a plain right-click keeps each mech's movement mode");
            var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
            tactical.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text.StartsWith("Set priority"))
                .EmitSignal(BaseButton.SignalName.Pressed);
            tactical.IssueCommand(Vector2.Zero, enemy.Id, MovementMode.Run);
            Check(!tactical.SettingPriority && friends.All(unit => unit.Movement == MovementMode.Walk && unit.Order == CombatOrder.Move),
                "choosing a priority target ignores the movement modifier");
            sim.SetPriority(tactical.Selected, null); sim.Hold(tactical.Selected);

            tactical.SelectUnits([friends[0].Id], false); await Frames();
            var doll = tactical.Paperdoll;
            var found = new HashSet<MechLocation>();
            var hits = new Dictionary<MechLocation, Vector2>();
            for (var x = 0f; x < doll.Size.X; x += 3)
                for (var y = 0f; y < doll.Size.Y; y += 3)
                    if (doll.LocationAt(new(x, y)) is { } location && found.Add(location)) hits[location] = new(x, y);
            Check(found.Count == 8, $"every paperdoll location can be hovered: {string.Join(", ", found)}");
            var tooltip = doll._GetTooltip(hits[MechLocation.CenterTorso]);
            Check(tooltip.StartsWith(DraftCatalog.LocationName(MechLocation.CenterTorso).ToUpperInvariant() + "\n"),
                $"hovering the center torso lists its systems: {tooltip}");
            Check(doll._MakeCustomTooltip(tooltip) is VBoxContainer box && box.GetChildCount() >= 2, "the systems tooltip is a coloured list");

            Check(tactical.UnitDolls.Count == tactical.UnitButtons.Count
                && tactical.UnitDolls.Select((card, i) => card.Unit == friends[i] && card.Compact && card.Visible
                    && tactical.UnitButtons[i].GetGlobalRect().Encloses(card.GetGlobalRect()) && card.Size.X > 40).All(item => item),
                "each unit card carries a compact paperdoll of its mech");
            Check(tactical.UnitDolls[0]._GetTooltip(tactical.UnitDolls[0].Size / 2) == "", "unit card paperdolls have no location tooltip");
        }
        finally
        {
            for (var i = 0; i < friends.Length; i++) (friends[i].Movement, friends[i].Firing) = settings[i];
            tactical.SelectUnits(selection, false); field.GrabFocus();
        }
    }

    /// <summary>J and the Jump button arm jump targeting; Escape disarms it; a ground right-click jumps and a mech click keeps it armed.
    /// Uses its own Jenner and Awesome rather than the randomly drafted company, which may have no jump jets.</summary>
    private async Task TestCombatJumping()
    {
        var roster = ForceRoster.Create("Jump checks", new[] { "jenner", "awesome" }.Select(DraftCatalog.Mech), DraftCatalog.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        // Open through navigation, as the hangar does, so keys route to the combat screen.
        var tactical = _app.Tactical; _app.Navigation.Push(CommandScreen.Tactical, _app.Hangar.DeploymentButton);
        tactical.Open(roster, roster.Mechs[0].Id); await Frames();
        await Click(tactical.MirrorMatchButton);
        if (!tactical.ForceChoices[1].ButtonPressed) await Click(tactical.ForceChoices[1]);
        await Click(tactical.LaunchButton);
        var field = tactical.Battlefield; var sim = tactical.Simulation!;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var jumper = friends.Single(unit => unit.Spec.Id == "jenner"); var grounded = friends.Single(unit => unit.Spec.Id == "awesome");
        tactical.SelectUnits([grounded.Id], false); field.GrabFocus(); await PressKey(Key.J);
        Check(!tactical.SettingJump && tactical.JumpButton.Disabled && tactical.JumpButton.TooltipText.Contains("No jump jets"),
            $"mechs without jump jets cannot arm a jump and say why: {tactical.JumpButton.TooltipText}");
        Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("Jump: no jump jets")),
            "selected details report a mech without jump jets");
        tactical.SelectUnits([jumper.Id], false); field.GrabFocus();
        await PressKey(Key.J);
        Check(tactical.SettingJump && field.JumpArmed && tactical.JumpButton.Text.StartsWith("●"),
            "J arms jump targeting");
        await PressKey(Key.Escape);
        Check(!tactical.SettingJump && !field.JumpArmed && tactical.Modal == CombatModal.None, "Escape cancels jump targeting before opening the menu");
        tactical.JumpButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(tactical.SettingJump, "the Jump button arms jump targeting");
        await Frames(); await Capture("combat-jump-targeting");
        var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        tactical.IssueCommand(enemy.Position, enemy.Id);
        Check(tactical.SettingJump && !jumper.Airborne, "right-clicking a mech keeps jump targeting armed");
        var order = jumper.Order;
        tactical.IssueCommand(jumper.Position + jumper.Facing * 100, null);
        Check(!tactical.SettingJump && jumper.Airborne && jumper.JumpTo.DistanceTo(jumper.Position) <= jumper.JumpRange + .01f,
            "a ground right-click sends the selection on a jump and disarms targeting");
        Check(order != CombatOrder.Attack || jumper.Order == CombatOrder.Attack, "jumping keeps an Attack order");
        var paused = sim.Paused; sim.Paused = false; sim.Advance(1); sim.Paused = paused; await Frames();
        await Capture("combat-jump-flight");
        Check(jumper.Altitude > 10 && tactical.JumpButton.Disabled && tactical.JumpButton.TooltipText.Contains("Airborne"),
            $"the mech rises in flight and cannot jump again until it lands: {tactical.JumpButton.TooltipText}");
        tactical.SelectUnits([jumper.Id, grounded.Id], false); await Frames();
        Check(tactical.JumpButton.Disabled, "a group with no mech able to jump cannot arm a jump");
        sim.Paused = false; sim.Advance(CombatRules.JumpRechargeSeconds + 5); sim.Paused = true; tactical.SelectUnits([jumper.Id, grounded.Id], false);
        Check(!tactical.JumpButton.Disabled && tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("Ready")),
            "a group jumps when any member can, and details report ready jets");
        await PressKey(Key.Escape); await Click(tactical.EndButton); await Click(tactical.ReturnButton);
    }
    private static void CheckLocationSystems(Action<bool, string> check)
    {
        WeaponSpec Laser(string id, bool rear = false) =>
            new(id, "Medium Laser", MechLocation.LeftTorso, rear, 3, new(DamageKind.Direct, 5, 1, 0), 0, 3, 6, 9, null, WeaponKind.MediumLaser);
        var spec = DraftCatalog.Mech("atlas") with
        {
            Weapons = [Laser("ml1"), Laser("ml2"), Laser("ml3"), Laser("ml4", rear: true), Laser("ml5") with { Location = MechLocation.RightArm }],
            Components = [new("hs1", "Heat Sink", MechLocation.LeftTorso, 1, ComponentKind.HeatSink, 1)],
            Ammo = [new("ac", "AC/20 Ammo", MechLocation.LeftTorso, AmmoKind.AC20, 5)]
        };
        var conditions = new Dictionary<string, ComponentCondition>
        {
            ["ml1"] = ComponentCondition.Operational, ["ml2"] = ComponentCondition.Destroyed, ["ml3"] = ComponentCondition.Operational,
            ["ml4"] = ComponentCondition.Operational, ["ml5"] = ComponentCondition.Operational,
            ["hs1"] = ComponentCondition.Damaged, ["ac"] = ComponentCondition.Operational
        };
        CombatUnit Unit() => new("systems", spec, DraftCatalog.Warriors[0], CombatSide.Friendly,
            spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)),
            conditions.Select(item => new EquipmentCondition(item.Key, item.Value)), [new AmmoCondition("ac", 5)], Vector2.Zero);
        var unit = Unit();
        var groups = CombatPaperdoll.LocationSystems(unit, MechLocation.LeftTorso);
        check(groups.SequenceEqual([
            new SystemGroup(2, "Medium Laser", ComponentCondition.Operational), new SystemGroup(1, "Medium Laser", ComponentCondition.Destroyed),
            new SystemGroup(1, "Medium Laser (rear)", ComponentCondition.Operational), new SystemGroup(1, "Heat Sink", ComponentCondition.Damaged),
            new SystemGroup(1, "AC/20 Ammo", ComponentCondition.Operational)]),
            $"location systems group by name, rear mounting and condition: {string.Join("; ", groups)}");
        check(groups[0].Text == "2 x Medium Laser" && groups[1].Text == "Medium Laser", "grouped systems read as a count");
        check(CombatPaperdoll.SystemColor(ComponentCondition.Operational) == CombatPaperdoll.Healthy
            && CombatPaperdoll.SystemColor(ComponentCondition.Damaged) == CombatPaperdoll.LightDamage
            && CombatPaperdoll.SystemColor(ComponentCondition.Destroyed) == CombatPaperdoll.CriticalDamage,
            "systems are green when healthy, yellow when damaged and red when destroyed");
        unit.ApplyDamage(MechLocation.LeftTorso, 10000);
        check(!unit.Intact(MechLocation.LeftTorso) && CombatPaperdoll.LocationSystems(unit, MechLocation.LeftTorso)
            .All(item => item.Condition == ComponentCondition.Destroyed), "every system in a lost location is destroyed");
    }
}
