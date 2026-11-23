using Godot;

namespace Mech.Tests;

/// <summary>Ends a battle's drop phase for checks about what follows it.</summary>
internal static class Drops
{
    /// <summary>Every lance lands on its default site and the battle begins, as if every player chose it and was ready.</summary>
    public static void Land(CombatSimulation sim)
    {
        if (sim is not { Drop: { } drop, Replica: false }) return;
        foreach (var slot in sim.Players) sim.Execute(new DropSiteCommand(drop.DefaultSite(slot)), slot);
        sim.Execute(new ForceDropCommand());
    }
}

public partial class SmokeTests
{
    /// <summary>Lands a launched battle's lances on their default sites; see <see cref="Drops.Land"/>.</summary>
    private async Task Land(CombatScreen screen)
    {
        if (screen.Simulation is { } sim) Drops.Land(sim);
        await Frames();
    }

    /// <summary>A launched battle opens in its drop phase: the banner, choosing a site by clicking, invalid sites and Ready.</summary>
    private async Task CheckDropUi(CombatScreen tactical)
    {
        var sim = tactical.Simulation!; var drop = sim.Drop;
        Check(drop is not null && tactical.DropBanner.Visible && tactical.Battlefield.DropPlacing && tactical.DropText.Text.Contains("DROP PHASE")
            && tactical.DropReadyButton.Disabled && !tactical.ForceDropButton.Visible && sim.Units.Where(unit => unit.Side == CombatSide.Friendly).All(sim.AwaitingDrop),
            "a launched battle opens in its drop phase, with Ready unavailable until a site is chosen and no Force Start alone");
        tactical.TogglePause(); await PressKey(Key.Period);
        Check(sim.Paused && sim.Drop is not null, "the battle cannot be started during the drop phase");
        tactical.ChooseDropSite(PatrolLayout.EdgePoint(drop!.Zone.Edge, 0, 100)); await Frames();
        Check(drop.Site(tactical.Slot).Site is null && tactical.DropText.Text.Contains("Cannot drop there: Outside the drop zone"),
            "a site in the edge padding is refused, saying why");
        var site = drop.DefaultSite(tactical.Slot);
        tactical.ChooseDropSite(site); await Frames();
        Check(drop.Site(tactical.Slot).Site == site && !tactical.DropReadyButton.Disabled && tactical.DropText.Text.Contains("land somewhere inside it")
            && sim.Units.Where(unit => unit.Side == CombatSide.Friendly).All(sim.AwaitingDrop),
            "choosing a site makes Ready available, while the lance stays hidden until the drop ends");
        await Capture("drop-phase");
        // A left-click on the battlefield chooses the site too.
        var field = tactical.Battlefield; var moved = site + drop.Zone.Along * 300;
        if (sim.DropSiteProblem(tactical.Slot, moved) is null && field.GetGlobalRect().HasPoint(field.GetGlobalTransformWithCanvas() * field.ScreenPoint(moved)))
        {
            var at = field.ScreenPoint(moved);
            MouseButton(field.GlobalPosition + at, Godot.MouseButton.Left, true);
            MouseButton(field.GlobalPosition + at, Godot.MouseButton.Left, false); await Frames();
            Check(drop.Site(tactical.Slot).Site is { } clicked && clicked.DistanceTo(moved) < 40, "a left-click inside the drop zone moves the drop site");
            tactical.ChooseDropSite(site); await Frames();
        }
        await Click(tactical.DropReadyButton);
        Check(sim.Drop is null && !tactical.DropBanner.Visible && !tactical.Battlefield.DropPlacing && sim.Paused && tactical.TimeMode == "Pause"
            && sim.Units.Where(unit => unit.Side == CombatSide.Friendly).All(unit => !sim.AwaitingDrop(unit) && unit.Position.DistanceTo(site) <= CombatRules.DropSiteRadius + 1e-3f),
            "Ready ends a single player's drop phase: the lance lands across its site and the battle begins paused");
    }
}
