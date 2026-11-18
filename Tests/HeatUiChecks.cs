using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
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
            tactical.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed);
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
            Check(sidebar.HeatBarRect.Position.Y <= 4 && sidebar.HeatBarRect.End.Y >= sidebar.Size.Y - 4,
                $"the sidebar doll has no label or legend gaps above or below its schematic: size {sidebar.Size} heat {sidebar.HeatBarRect}");
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
                "four long-name heat cards stay within the minimum window");
            tactical.Size = new Vector2(1440, 900);
            await Frames();
            await Capture("combat-heat-shutdown");
            friends[0].ApplyDamage(MechLocation.Head, 1000);
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
        var scrollRect = tactical.SidebarScroll.GetGlobalRect(); var pilotRect = tactical.PilotRow.GetGlobalRect();
        Check(pilotRect.Position.X - scrollRect.Position.X >= 9 && pilotRect.Position.Y - scrollRect.Position.Y >= 9,
            $"the scrolling details are padded: scroll {scrollRect} pilot {pilotRect}");
        var titleAt = title.GetGlobalRect(); var dollAt = tactical.Paperdoll.GetGlobalRect();
        tactical.SidebarScroll.ScrollVertical = 100000; await Frames();
        Check(title.GetGlobalRect() == titleAt && tactical.Paperdoll.GetGlobalRect() == dollAt && tactical.PilotRow.GetGlobalRect() != pilotRect,
            "scrolling the details leaves the title and paperdoll pinned");
        tactical.SidebarScroll.ScrollVertical = 0; await Frames();
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
