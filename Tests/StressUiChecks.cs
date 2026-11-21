using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Stress readouts on friendlies only, the two-press Eject button, the surrender notice and its answer.</summary>
    private async Task TestStressUi()
    {
        var roster = ForceRoster.Create("Stress check", [DraftCatalog.Mech("griffin"), DraftCatalog.Mech("griffin")],
            [DraftCatalog.Warrior("mara-vale"), DraftCatalog.Warrior("ivo-chen")]);
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var tactical = new CombatScreen { Theme = Ui.CreateTheme(), Size = new Vector2(1440, 900) };
        AddChild(tactical);
        try
        {
            tactical.Open(roster, roster.Mechs[0].Id);
            tactical.MirrorMatchButton.EmitSignal(BaseButton.SignalName.Pressed);
            foreach (var choice in tactical.ForceChoices) choice.ButtonPressed = true;
            tactical.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed);
            var sim = tactical.Simulation!;
            Check(sim.PromptSurrender, "a battle on screen waits for the leader to answer a surrender");
            sim.NerveChecksEnabled = false;
            foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
            var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
            var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
            friends[0].Stress = StressLevel.Stressed;
            tactical.SelectUnits([friends[0].Id], false); await Delay(.15);
            var face = tactical.UnitFaces[0];
            Check(face.IsVisibleInTree() && face.Texture == CombatPaperdoll.StressIcon(StressLevel.Stressed) && !tactical.UnitButtons[0].Text.Contains("Stressed")
                && face.SelfModulate == CombatPaperdoll.StressColor(StressLevel.Stressed) && tactical.UnitButtons[0].GetGlobalRect().Encloses(face.GetGlobalRect())
                && tactical.Paperdoll.ShowsStress
                && tactical.Paperdoll._GetTooltip(tactical.Paperdoll.StressRect.GetCenter()).StartsWith("Stress: Stressed · skills play 1 worse"),
                "a friendly's stress shows as a tinted face on its card and its paperdoll, with what it does");
            Check(Enum.GetValues<StressLevel>().Select(CombatPaperdoll.StressIcon).Distinct().Count() == 5, "each stress level has its own face");
            var sidebar = string.Join("\n", tactical.FindChildren("*", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text));
            Check(sidebar.Contains("Stress: Stressed (skills −1)"), "the sidebar names the selected pilot's stress and its penalty");
            await Capture("combat-stress");

            // Enemy stress is never shown.
            enemies[0].Position = friends[0].Position + new Vector2(0, -200); enemies[0].Stress = StressLevel.Panicked; sim.RefreshDetection();
            tactical.Battlefield.InspectedEnemy = enemies[0].Id; await Delay(.15);
            Check(!tactical.Paperdoll.ShowsStress && !string.Join("\n", tactical.FindChildren("*", "Label", true, false).OfType<Label>()
                .Where(label => label.IsVisibleInTree()).Select(label => label.Text)).Contains("Panicked"), "an enemy's stress is hidden");
            tactical.Battlefield.InspectedEnemy = null;

            // Eject needs two presses.
            tactical.SelectUnits([friends[1].Id], false); await Delay(.15);
            tactical.EjectButton.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(!friends[1].Ejected && tactical.EjectButton.Text == "Confirm Eject", "the first press only arms the Eject button");
            tactical.EjectButton.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(friends[1].Ejected && friends[1].Status == CombatStatus.Disabled && !friends[1].WarriorKilled, "the second press ejects the MechWarrior");
            await Frames();
            Check(!tactical.UnitFaces[1].Visible, "a defeated mech's card shows no stress face");

            // A disarmed enemy offers to surrender: the battle pauses and the leader answers.
            foreach (var location in enemies[0].Weapons.Select(item => item.Spec.Location).Distinct().ToArray())
                enemies[0].ApplyDamage(location, enemies[0].Locations[location].Armor + enemies[0].Locations[location].Structure);
            sim.Paused = false; await Delay(.3);
            Check(sim.SurrenderOffers.Contains(enemies[0].Id) && sim.Paused && tactical.SurrenderBanner.Visible
                && tactical.SurrenderText.Text.Contains("offers to surrender") && tactical.AcceptSurrenderButton.Visible,
                "a surrender offer pauses the battle and asks the leader to accept or refuse");
            await Capture("combat-surrender");
            tactical.AcceptSurrenderButton.EmitSignal(BaseButton.SignalName.Pressed); await Delay(.15);
            Check(enemies[0].Surrendered && !tactical.SurrenderBanner.Visible && sim.SurrenderOffers.Count == 0, "accepting takes the pilot hostage and lifts the notice");
            Check(sim.Events.Any(item => item.Public && item.Text.Contains("surrender accepted")), "the battle log records the surrender");
            var panelText = "";
            tactical.ControlsPanel.Display(false);
            panelText = string.Join("\n", tactical.ControlsPanel.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text));
            Check(panelText.Contains("STRESS AND EJECTION") && panelText.Contains("Eject") && panelText.Contains("Surrender"),
                "the controls panel explains stress, ejection and surrender");
        }
        finally
        {
            RemoveChild(tactical); tactical.QueueFree();
            await Frames();
        }
    }
}
