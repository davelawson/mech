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
            Check(tactical.NormalHeatButton.Text.StartsWith("●") && friends.All(unit => unit.HeatPolicy == HeatPolicy.Normal), "heat controls show the initial shared Normal policy");
            tactical.AggressiveHeatButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(friends.All(unit => unit.HeatPolicy == HeatPolicy.Aggressive) && tactical.AggressiveHeatButton.Text.StartsWith("●"), "Aggressive heat button edits all selected friendlies");
            friends[0].AddHeat(35); friends[1].HeatPolicy = HeatPolicy.Normal;
            tactical.SelectUnits(friends.Select(unit => unit.Id).ToArray(), false);
            Check(!tactical.NormalHeatButton.Text.StartsWith("●") && !tactical.AggressiveHeatButton.Text.StartsWith("●"), "mixed heat policies leave both mode indicators unselected");
            Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree()
                && label.Text.Contains("Heat policy: Mixed")), "group readout explicitly reports mixed heat policies");
            tactical.NormalHeatButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(friends.All(unit => unit.HeatPolicy == HeatPolicy.Normal), "group policy edits include shut-down units");
            tactical.SelectUnits([friends[0].Id], false);
            Check(tactical.NormalHeatButton.IsVisibleInTree() && tactical.AggressiveHeatButton.IsVisibleInTree(), "single shut-down selection retains command and heat controls");
            tactical.AggressiveHeatButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(friends[0].HeatPolicy == HeatPolicy.Aggressive, "shutdown policy can be changed before restart");
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
            Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree()
                && label.Text.Contains(CombatScreen.HeatReadout(friends[0]))), "selected details show heat band, policy, effective cooling and restart threshold");
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
            Check(!tactical.NormalHeatButton.IsVisibleInTree(), "physically defeated units do not retain heat policy controls");
        }
        finally
        {
            RemoveChild(tactical); tactical.QueueFree(); await Frames();
        }
    }
}
