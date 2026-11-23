using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>A group led by a mech without ammo weapons still shows the ammo gauge, and it sets only the mechs that carry ammo.</summary>
    private async Task TestMixedAmmoGauge()
    {
        var roster = ForceRoster.Create("Mixed ammo", [DraftCatalog.Mech("awesome"), DraftCatalog.Mech("thunderbolt"), DraftCatalog.Mech("thunderbolt")],
            DraftCatalog.Warriors.Take(3));
        for (var i = 0; i < 3; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var tactical = new CombatScreen { Theme = Ui.CreateTheme(), Size = new Vector2(1440, 900) };
        AddChild(tactical);
        try
        {
            tactical.Open(roster, roster.Mechs[0].Id);
            tactical.MirrorMatchButton.EmitSignal(BaseButton.SignalName.Pressed);
            foreach (var choice in tactical.ForceChoices) choice.ButtonPressed = true;
            tactical.ChooseBiome(Biome.Valley);
            tactical.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed); await Land(tactical);
            var sim = tactical.Simulation!; sim.Paused = true;
            var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
            var energy = friends.Single(unit => !unit.HasAmmoWeapons);
            var carriers = friends.Where(unit => unit.HasAmmoWeapons).ToArray();
            var sidebar = tactical.Paperdoll;
            tactical.SelectUnits([energy.Id], false); await Frames();
            Check(sidebar.Unit == energy && !sidebar.ShowsAmmo, "a mech without ammo weapons selected alone shows no ammo gauge");
            tactical.SelectUnits([energy.Id, .. carriers.Select(unit => unit.Id)], false); await Frames();
            Check(sidebar.Unit == energy && sidebar.AmmoUnit == carriers[0] && sidebar.ShowsAmmo && sidebar.EditsAmmoEconomy
                && sidebar.DisplayedAmmoEconomy == 0, "a group led by a mech without ammo weapons shows its first ammo carrier's gauge");
            Vector2 At(int step) => sidebar.GetGlobalTransform() * new Vector2(sidebar.AmmoTrackX(step), sidebar.AmmoTrack.GetCenter().Y);
            MouseMotion(At(0), Vector2.Zero);
            MouseButton(At(0), Godot.MouseButton.Left, true); await Frames();
            MouseMotion(At(5), At(5) - At(0)); await Frames();
            MouseButton(At(5), Godot.MouseButton.Left, false); await Frames(); await Frames();
            Check(carriers.All(unit => unit.AmmoEconomy == 5) && energy.AmmoEconomy == 0,
                $"dragging the gauge sets every selected ammo carrier and leaves the others: {string.Join(", ", friends.Select(unit => unit.AmmoEconomy))}");
            Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Ammo economy: 5")),
                "the group readout gives the ammo carriers' economy");
        }
        finally
        {
            RemoveChild(tactical); tactical.QueueFree(); await Frames();
        }
    }

    private async Task TestHeatControls()
    {
        var roster = ForceRoster.Create("Heat controls", Enumerable.Repeat(DraftCatalog.Mech("thunderbolt"), 4), DraftCatalog.Warriors.Take(4));
        for (var i = 0; i < 4; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var tactical = new CombatScreen { Theme = Ui.CreateTheme(), Size = new Vector2(1440, 900) };
        AddChild(tactical);
        try
        {
            tactical.Open(roster, roster.Mechs[0].Id);
            tactical.MirrorMatchButton.EmitSignal(BaseButton.SignalName.Pressed);
            foreach (var choice in tactical.ForceChoices) choice.ButtonPressed = true;
            tactical.ChooseBiome(Biome.Valley);
            tactical.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed); await Land(tactical);
            var sim = tactical.Simulation!;
            var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
            tactical.SelectUnits(friends.Select(unit => unit.Id).ToArray(), false);
            var sidebar = tactical.Paperdoll; await Frames();
            Check(friends.All(unit => unit.HeatLimit == CombatRules.DefaultHeatLimit) && sidebar.DisplayedHeatLimit == CombatRules.DefaultHeatLimit,
                "every friendly starts with a heat limit of 10, shown on the sidebar handle");
            Check(tactical.FindChildren("*", "Button", true, false).OfType<Button>().All(button => !button.Text.Contains("heat")),
                "the Normal and Aggressive heat buttons are gone");
            Check(sidebar.HeatLimitAt(sidebar.HeatTrackY(30)) == 30 && sidebar.HeatLimitAt(sidebar.HeatTrack.Position.Y - 50) == 30
                && sidebar.HeatLimitAt(sidebar.HeatTrackY(12.4)) == 12 && sidebar.HeatLimitAt(sidebar.HeatTrack.End.Y + 50) == 0,
                "the heat track maps to whole limits 0–30, clamped at its ends");
            await DragHeatLimit(sidebar, CombatRules.DefaultHeatLimit, 30);
            Check(friends.All(unit => unit.HeatLimit == 30) && sidebar.DisplayedHeatLimit == 30, "dragging the handle edits all selected friendlies");
            friends[0].AddHeat(35); friends[1].HeatLimit = 20;
            tactical.SelectUnits(friends.Select(unit => unit.Id).ToArray(), false);
            Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree()
                && label.Text.Contains("Heat limit: Mixed")), "group readout explicitly reports mixed heat limits");
            await ClickHeatLimit(sidebar, 12);
            Check(friends.All(unit => unit.HeatLimit == 12), "clicking the bar sets the limit for the group, including shut-down units");
            tactical.SelectUnits([friends[0].Id], false); await Frames();
            Check(sidebar.EditsHeatLimit, "a single shut-down selection keeps the heat-limit handle");
            await ClickHeatLimit(sidebar, 5);
            Check(friends[0].HeatLimit == 5 && friends[1].HeatLimit == 12, "a shut-down mech's limit can be changed before restart");
            var strip = sidebar.SpeedStrip; var ammo = sidebar.AmmoStrip;
            Check(sidebar.HeatBarRect.Position.Y <= 4 && strip.Position.Y >= sidebar.HeatBarRect.End.Y - 1 && strip.Position.Y - sidebar.HeatBarRect.End.Y <= 4
                && Math.Abs(ammo.Position.Y - strip.End.Y) < .5f && ammo.End.Y <= sidebar.Size.Y + .5f && ammo.End.Y >= sidebar.Size.Y - 4,
                $"the sidebar doll has no gaps: the schematic fills its top, then the speed and ammo gauges: size {sidebar.Size} heat {sidebar.HeatBarRect} speed {strip} ammo {ammo}");
            await CheckSpeedGauge(tactical, friends);
            await CheckAmmoGauge(tactical, friends);
            var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
            sidebar.Display(enemy);
            Check(!sidebar.EditsHeatLimit && sidebar.DisplayedHeatLimit is null && !CombatScreen.HeatReadout(enemy).Contains("limit"),
                "an inspected enemy shows neither a heat-limit handle nor its limit");
            sidebar.Display(friends[0]);
            Check(!tactical.UnitButtons[0].Text.Contains("H 35.0") && tactical.UnitButtons[0].TooltipText.Contains("Automatic restart below 10"),
                "friendly card leaves heat to its bar and keeps restart information in the tooltip");
            Check(CombatPaperdoll.HeatColor(9.9) == CombatPaperdoll.LightDamage && CombatPaperdoll.HeatColor(10) == CombatPaperdoll.ModerateDamage
                && CombatPaperdoll.HeatColor(19.9) == CombatPaperdoll.ModerateDamage && CombatPaperdoll.HeatColor(20) == CombatPaperdoll.CriticalDamage,
                "heat bar is yellow below 10, orange below 20 and red from 20");
            Check(CombatPaperdoll.HeatFraction(friends[0]) == 1 && CombatPaperdoll.HeatFraction(friends[2]) == 0, "heat bar fills to the shutdown threshold and caps there");
            await Frames();
            foreach (var doll in tactical.UnitDolls.Append(tactical.Paperdoll))
            {
                var bounds = new Rect2(Vector2.Zero, doll.Size);
                var heatBar = doll.HeatBarRect; var fuelBar = doll.FuelBarRect;
                var shapes = Enumerable.Range(0, (int)doll.Size.X).Where(x => doll.LocationAt(new(x, heatBar.GetCenter().Y)) is not null || doll.LocationAt(new(x, fuelBar.GetCenter().Y)) is not null).ToArray();
                Check(bounds.Encloses(heatBar) && bounds.Encloses(fuelBar) && fuelBar.End.X < heatBar.Position.X && heatBar.Size.Y > 0
                    && shapes.Length > 0 && fuelBar.End.X <= shapes.Min() && heatBar.Position.X >= shapes.Max(),
                    $"{(doll.Compact ? "card" : "sidebar")} doll keeps fuel left and heat right of its schematic, inside the control: size {doll.Size} fuel {fuelBar} heat {heatBar} shapes {(shapes.Length > 0 ? $"{shapes.Min()}–{shapes.Max()}" : "none")}");
                Check(!doll.ShowsFuel, "mechs without jump jets reserve an empty fuel slot");
            }
            Check(tactical.Paperdoll.HeatBarRect.Size.X > tactical.UnitDolls[0].HeatBarRect.Size.X, "sidebar bars are wider than card bars");
            await CheckSidebarLayout(tactical, friends[0]);
            Check(!tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree()
                && (label.Text.Contains("Cooling ") || label.Text.Contains("Ambient ") || label.Text.Contains("Automatic restart"))),
                "selected details leave heat, cooling and ambient readouts to the heat bar and the card tooltip");
            Check(tactical.UnitButtons[0].TooltipText.Contains(CombatScreen.HeatReadout(friends[0])), "the card tooltip keeps the exact heat readout");
            var toggle = tactical.FindChildren("*", "CheckButton", true, false).OfType<CheckButton>().First();
            Check(!toggle.Disabled && toggle.Text.Contains("Thermal shutdown"), "shutdown weapon readout explains firing restriction while allowing configuration");
            toggle.ButtonPressed = false;
            Check(!friends[0].Weapons[0].Enabled, "weapon configuration remains editable during shutdown");
            tactical.Size = new Vector2(900, 500); await Frames();
            Check(tactical.UnitButtons.All(button => tactical.GetGlobalRect().Encloses(button.GetGlobalRect())),
                "four long-name heat cards stay within the minimum window: " + string.Join("; ", tactical.UnitButtons.Select(button => $"{button.GetGlobalRect()} min {button.GetCombinedMinimumSize()}")));
            tactical.Size = new Vector2(1440, 900);
            await Frames();
            await Capture("combat-heat-shutdown");
            friends[0].ApplyDamage(MechLocation.CenterTorso, 1000);
            tactical.SelectUnits([friends[0].Id], false);
            await Frames();
            Check(!tactical.Paperdoll.EditsHeatLimit, "physically defeated units do not retain the heat-limit handle");
        }
        finally
        {
            RemoveChild(tactical); tactical.QueueFree(); await Frames();
        }
    }

    /// <summary>The title and paperdoll stay pinned above the padded scrolling details, which start with the hangar-style pilot block.</summary>
    private async Task CheckSidebarLayout(CombatScreen tactical, CombatUnit unit)
    {
        tactical.SelectUnits([unit.Id], false); await Frames();
        var title = tactical.FindChildren("*", "Label", true, false).OfType<Label>().First(label => label.Text == $"{unit.Warrior.Callsign} · {unit.Spec.Chassis}");
        Check(title.HorizontalAlignment == HorizontalAlignment.Center && !tactical.SidebarScroll.IsAncestorOf(title)
            && !tactical.SidebarScroll.IsAncestorOf(tactical.Paperdoll), "the centred title and paperdoll sit outside the scrolling details");
        Check(tactical.PilotRow.IsVisibleInTree() && tactical.PilotPortrait.Texture is not null
            && tactical.PilotInfo.Text == CombatScreen.PilotText(unit.Warrior) && tactical.PilotInfo.Text.Contains(RosterUi.SkillLines(unit.Warrior)),
            "the pilot block shows the portrait, callsign, name and skills");
        Check(!tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree()
            && (label.Text.Contains("ARMOR / STOCK") || label.Text.Contains("Ground markers") || label.Text.Contains("Primary unit's") || label.Text.Contains("SYSTEMS ·"))),
            "the sidebar has no armour, systems, ground-marker or primary-unit text");
        var scrollRect = tactical.SidebarScroll.GetGlobalRect(); var pilotRect = tactical.PilotRow.GetGlobalRect();
        Check(pilotRect.Position.X - scrollRect.Position.X >= 9 && pilotRect.Position.Y - scrollRect.Position.Y >= 9,
            $"the scrolling details are padded: scroll {scrollRect} pilot {pilotRect}");
        var titleAt = title.GetGlobalRect(); var dollAt = tactical.Paperdoll.GetGlobalRect();
        tactical.SidebarScroll.ScrollVertical = 100000; await Frames();
        Check(title.GetGlobalRect() == titleAt && tactical.Paperdoll.GetGlobalRect() == dollAt && tactical.PilotRow.GetGlobalRect() != pilotRect,
            "scrolling the details leaves the title and paperdoll pinned");
        tactical.SidebarScroll.ScrollVertical = 0; await Frames();
    }

    /// <summary>The speed gauge along the sidebar doll's foot: the pointer's mapping, snapping and limits, dragging it for a group,
    /// the readout, the tooltip and colours, and no pointer for an enemy.</summary>
    private async Task CheckSpeedGauge(CombatScreen tactical, CombatUnit[] friends)
    {
        static float Speed(float kmh) => kmh / CombatRules.KmhPerMetrePerSecond;
        var sidebar = tactical.Paperdoll; var sim = tactical.Simulation!;
        var mover = friends[2];
        tactical.SelectUnits(friends.Skip(2).Select(unit => unit.Id).ToArray(), false); await Frames();
        Check(sidebar.Unit == mover && sidebar.ShowsSpeed && sidebar.EditsDesiredSpeed && sidebar.DisplayedDesiredSpeed == mover.DesiredSpeed,
            "a friendly's sidebar doll shows the speed gauge with its desired-speed pointer");
        Check(!tactical.UnitDolls.Any(doll => doll.ShowsSpeed), "unit cards have no speed gauge");
        var track = sidebar.SpeedTrack;
        Check(Math.Abs(sidebar.SpeedTrackX(CombatRules.SpeedGaugeMaximum) - track.End.X) < .01f && Math.Abs(sidebar.SpeedTrackX(0) - track.Position.X) < .01f,
            "the gauge runs from 0 at its left edge to 150 km/h at its right");
        Check(sidebar.DesiredSpeedAt(sidebar.SpeedTrackX(mover.CurrentWalkingSpeed) + 2) == mover.CurrentWalkingSpeed
            && sidebar.DesiredSpeedAt(sidebar.SpeedTrackX(mover.CurrentRunningSpeed) - 2) == mover.CurrentRunningSpeed,
            "the pointer snaps to the walking and running notches");
        Check(sidebar.DesiredSpeedAt(track.Position.X - 50) == CombatRules.MinimumDesiredSpeed && sidebar.DesiredSpeedAt(track.End.X + 50) == mover.CurrentRunningSpeed,
            "the pointer is held between 10 km/h and the running speed");
        Check(CombatPaperdoll.Kmh(sidebar.DesiredSpeedAt(sidebar.SpeedTrackX(Speed(22)))) == 22, "the pointer picks whole km/h between the notches");
        Vector2 At(float speed) => sidebar.GetGlobalTransform() * new Vector2(sidebar.SpeedTrackX(speed), track.GetCenter().Y);
        MouseMotion(At(mover.DesiredSpeed), Vector2.Zero);
        MouseButton(At(mover.DesiredSpeed), Godot.MouseButton.Left, true); await Frames();
        MouseMotion(At(Speed(22)), At(Speed(22)) - At(mover.DesiredSpeed)); await Frames();
        MouseButton(At(Speed(22)), Godot.MouseButton.Left, false); await Frames();
        Check(friends.Skip(2).All(unit => CombatPaperdoll.Kmh(unit.DesiredSpeed) == 22), "dragging the pointer sets the same desired speed for the selection");
        await Frames(); await Frames();
        Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Speed: 22 km/h")),
            "the group readout reports the desired speed");

        var (speed, moving, desired) = (mover.CurrentSpeed, mover.Moving, mover.DesiredSpeed);
        try
        {
            mover.DesiredSpeed = Speed(60); mover.Moving = true; mover.CurrentSpeed = Speed(55);
            Check(CombatPaperdoll.SpeedTooltip(mover, mover.DesiredSpeed) == "Running 55 km/h / 60 km/h" && CombatPaperdoll.SpeedColor(mover) == CombatPaperdoll.Healthy,
                $"a running mech's tooltip gives its speed and desired speed: {CombatPaperdoll.SpeedTooltip(mover, mover.DesiredSpeed)}");
            Check(sidebar._GetTooltip(sidebar.SpeedStrip.GetCenter()) == "Running 55 km/h / 60 km/h", "hovering the gauge shows that tooltip");
            mover.CurrentSpeed = Speed(-16);
            Check(CombatPaperdoll.SpeedTooltip(mover, Speed(30)) == "Reverse -16 km/h / 30 km/h" && CombatPaperdoll.SpeedColor(mover) == CombatPaperdoll.ReverseColor
                && Math.Abs(CombatPaperdoll.SpeedFraction(mover) - 16f / 150) < .001f,
                "reversing fills the gauge as a positive speed in dark green, written negative in the tooltip");
            mover.CurrentSpeed = Speed(25);
            Check(CombatPaperdoll.SpeedTooltip(mover, Speed(30)) == "Walking 25 km/h / 30 km/h", "walking pace reads Walking");
            mover.Moving = false; mover.CurrentSpeed = 0;
            Check(CombatPaperdoll.SpeedTooltip(mover, Speed(30)) == "Stopped 0 km/h / 30 km/h", "a still mech reads Stopped");
            await Frames(); await Capture("combat-speed-gauge");
        }
        finally { (mover.CurrentSpeed, mover.Moving, mover.DesiredSpeed) = (speed, moving, desired); }

        var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        sidebar.Display(enemy);
        Check(sidebar.ShowsSpeed && !sidebar.EditsDesiredSpeed && sidebar.DisplayedDesiredSpeed is null
            && !sidebar._GetTooltip(sidebar.SpeedStrip.GetCenter()).Contains(" / "), "an inspected enemy's gauge has no pointer or desired speed");
        tactical.SelectUnits(friends.Select(unit => unit.Id).ToArray(), false); await Frames();
    }

    /// <summary>The ammo economy gauge under the speed gauge: steps, dragging for a group, the fill, tooltip and readout,
    /// no pointer for an enemy and no gauge for a mech without ammo weapons.</summary>
    private async Task CheckAmmoGauge(CombatScreen tactical, CombatUnit[] friends)
    {
        var sidebar = tactical.Paperdoll; var sim = tactical.Simulation!;
        var shooter = friends[2];
        tactical.SelectUnits(friends.Skip(2).Select(unit => unit.Id).ToArray(), false); await Frames();
        Check(sidebar.Unit == shooter && sidebar.ShowsAmmo && sidebar.EditsAmmoEconomy && sidebar.DisplayedAmmoEconomy == 0,
            "a friendly with ammo weapons shows the ammo gauge with its pointer fully left");
        Check(Enumerable.Range(0, 11).All(step => sidebar.AmmoEconomyAt(sidebar.AmmoTrackX(step)) == step)
            && sidebar.AmmoEconomyAt(sidebar.AmmoTrack.Position.X - 40) == 0 && sidebar.AmmoEconomyAt(sidebar.AmmoTrack.End.X + 40) == 10,
            "the ammo track maps to whole steps 0–10, clamped at its ends");
        Vector2 At(int step) => sidebar.GetGlobalTransform() * new Vector2(sidebar.AmmoTrackX(step), sidebar.AmmoTrack.GetCenter().Y);
        MouseMotion(At(0), Vector2.Zero);
        MouseButton(At(0), Godot.MouseButton.Left, true); await Frames();
        MouseMotion(At(4), At(4) - At(0)); await Frames();
        MouseMotion(At(7), At(7) - At(4)); await Frames();
        MouseButton(At(7), Godot.MouseButton.Left, false); await Frames();
        Check(friends.Skip(2).All(unit => unit.AmmoEconomy == 7) && friends[0].AmmoEconomy == 0, "dragging the pointer sets ammo economy for the selection only");
        await Frames(); await Frames();
        Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Ammo economy: 7")),
            "the group readout reports the ammo economy");
        var (left, stock) = CombatPaperdoll.AmmoShots(shooter);
        Check(stock > 0 && left == stock && CombatPaperdoll.AmmoFraction(shooter) == 1 && CombatPaperdoll.AmmoColor == CombatPaperdoll.LightDamage,
            "the yellow fill shows a full ammunition load");
        Check(sidebar._GetTooltip(sidebar.AmmoStrip.GetCenter()) == $"Economy 7 · fires at +3 or better · {stock}/{stock} shots",
            $"hovering the gauge describes the economy and the shots left: {sidebar._GetTooltip(sidebar.AmmoStrip.GetCenter())}");
        Check(CombatPaperdoll.AmmoTooltip(shooter, 0) == $"No restriction · {stock}/{stock} shots"
            && CombatPaperdoll.AmmoTooltip(shooter, 10) == $"Optimal shots only · {stock}/{stock} shots", "the gauge's ends read no restriction and optimal shots only");
        await Capture("combat-ammo-gauge");
        var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        sidebar.Display(enemy);
        Check(sidebar.ShowsAmmo && !sidebar.EditsAmmoEconomy && sidebar.DisplayedAmmoEconomy is null
            && sidebar._GetTooltip(sidebar.AmmoStrip.GetCenter()) == $"{stock}/{stock} shots", "an inspected enemy's ammo gauge has no pointer, only its shots");
        var energyRoster = ForceRoster.Create("Energy only", [DraftCatalog.Mech("awesome")], DraftCatalog.Warriors.Take(1));
        energyRoster.Assign(energyRoster.Mechs[0].Id, energyRoster.Warriors[0].Id);
        var energyOnly = CombatSimulation.DeployOnTerrain(energyRoster, [energyRoster.Mechs[0].Id], BattlefieldTerrain.Flat, 41)
            .Units.First(unit => unit.Side == CombatSide.Friendly);
        sidebar.Display(energyOnly);
        Check(!sidebar.ShowsAmmo && !sidebar.EditsAmmoEconomy && sidebar.ShowsSpeed, "a mech without ammo weapons shows no ammo gauge");
        foreach (var unit in friends) unit.AmmoEconomy = 0;
        tactical.SelectUnits(friends.Select(unit => unit.Id).ToArray(), false); await Frames();
    }

    /// <summary>Presses the sidebar heat bar at one limit and drags the handle to another.</summary>
    private async Task DragHeatLimit(CombatPaperdoll doll, int from, int to)
    {
        Vector2 At(int limit) => doll.GetGlobalTransform() * new Vector2(doll.HeatTrack.GetCenter().X, doll.HeatTrackY(limit));
        MouseMotion(At(from), Vector2.Zero);
        MouseButton(At(from), Godot.MouseButton.Left, true); await Frames();
        MouseMotion(At((from + to) / 2), At((from + to) / 2) - At(from)); await Frames();
        MouseMotion(At(to), At(to) - At((from + to) / 2)); await Frames();
        MouseButton(At(to), Godot.MouseButton.Left, false); await Frames();
    }
    private Task ClickHeatLimit(CombatPaperdoll doll, int limit) => DragHeatLimit(doll, limit, limit);
}
