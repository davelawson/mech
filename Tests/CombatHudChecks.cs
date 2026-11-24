using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>The tactical HUD: a full-window battlefield under translucent overlays, card clicks that carry the camera,
    /// enemy contact cards, the details panel hidden with nothing selected, the objective and fading notices.</summary>
    private async Task TestCombatHud()
    {
        var roster = ForceRoster.Create("HUD check", [DraftCatalog.Mech("locust"), DraftCatalog.Mech("griffin")], TestPilots.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var tactical = new CombatScreen { Theme = Ui.CreateTheme(), Size = new Vector2(1440, 900) };
        AddChild(tactical);
        try
        {
            tactical.Open(roster, roster.Mechs[0].Id);
            tactical.MirrorMatchButton.EmitSignal(BaseButton.SignalName.Pressed);
            foreach (var choice in tactical.ForceChoices) choice.ButtonPressed = true;
            tactical.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed); await Land(tactical);
            var sim = tactical.Simulation!; sim.Paused = true; sim.NerveChecksEnabled = false;
            foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
            var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
            var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
            var field = tactical.Battlefield; var cards = tactical.UnitButtons;
            await Delay(.15);

            Check(field.GetGlobalRect().IsEqualApprox(tactical.GetGlobalRect()), $"the battlefield fills the window: {field.GetGlobalRect()}");
            float Alpha(Control control) => control.GetThemeStylebox("normal") is StyleBoxFlat box ? box.BgColor.A : 1;
            Check(CombatScreen.HudFill.A < 1 && Alpha(tactical.PlayButton) < 1 && tactical.PauseButton.GetParent().GetParent().GetParent<Control>().GetThemeStylebox("panel") is StyleBoxFlat { BgColor.A: < 1 } && cards.All(card => Alpha(card) < 1),
                "the time controls and mech cards are translucent");
            Check(tactical.ObjectiveLabel.Visible && tactical.ObjectiveLabel.Text == "Defeat the opposing force.", $"the objective floats at the top left: {tactical.ObjectiveLabel.Text}");

            tactical.SelectUnits([], false); await Delay(.15);
            Check(!tactical.Sidebar.Visible, "with nothing selected the details panel is hidden");
            await Capture("combat-hud-nothing-selected");
            await Click(cards[0]);
            Check(tactical.Selected.SequenceEqual([friends[0].Id]) && tactical.Sidebar.Visible && !field.Gliding,
                "a card click selects its mech and shows the details panel without moving the camera");
            var size = field.Camera.Size;
            await Click(cards[0]);
            Check(field.Gliding && tactical.Selected.SequenceEqual([friends[0].Id]), "clicking the card of the only selected mech carries the camera to it");
            await Delay(CombatViewport.FocusSeconds + .2);
            Check(!field.Gliding && field.FocusPoint.DistanceTo(friends[0].Position) < 1 && field.Camera.Size == size,
                $"the camera arrives at the mech with its zoom unchanged: {field.FocusPoint} vs {friends[0].Position}");
            tactical.SelectUnits([.. friends.Select(unit => unit.Id)], false);
            await Click(cards[1]);
            Check(tactical.Selected.SequenceEqual([friends[1].Id]) && !field.Gliding, "with a group selected a card click only selects its mech");

            Check(tactical.ContactButtons.Count == 0, "no contact cards before anything is detected");
            enemy.Position = friends[0].Position + new Vector2(0, -250); sim.RefreshDetection(); await Delay(.15);
            var contact = tactical.ContactButtons.SingleOrDefault();
            Check(contact is not null && tactical.ContactIds[0] == enemy.Id && contact.GetChildren().OfType<Label>().Single().Text == enemy.Spec.Chassis
                && contact.GetGlobalRect().Position.DistanceTo(tactical.GlobalPosition + new Vector2(8, 8)) < 1 && Alpha(contact) < 1,
                "a detected enemy gets a translucent card at the top left, named by its chassis");
            Check(tactical.ObjectiveLabel.GetGlobalRect().Position.Y > contact!.GetGlobalRect().End.Y, "the objective sits below the contact cards");
            await Capture("combat-hud-contact");
            var selected = tactical.Selected.ToArray();
            await Click(contact);
            Check(field.InspectedEnemy == enemy.Id && tactical.Selected.SequenceEqual(selected) && field.Gliding,
                "clicking a contact card inspects it, keeps the selection and carries the camera to it");
            await Delay(CombatViewport.FocusSeconds + .2);
            Check(field.FocusPoint.DistanceTo(enemy.Position) < 1, "the camera arrives at the contact");
            var seen = enemy.Position;
            // The far corner from the company, well beyond its sensors.
            enemy.Position = new Vector2(-Math.Sign(friends[0].Position.X), -Math.Sign(friends[0].Position.Y)) * (CombatRules.ArenaHalfSize - 100); sim.RefreshDetection(); await Delay(.15);
            Check(tactical.ContactButtons.Count == 1 && contact.Modulate.A < 1 && field.InspectedEnemy is null, $"a lost contact keeps its card, dimmed: {tactical.ContactButtons.Count} cards, alpha {contact.Modulate.A}, inspected {field.InspectedEnemy}, detected {sim.Detected(CombatSide.Friendly, enemy.Id)}, friend {friends[0].Position}");
            await Click(contact); await Delay(CombatViewport.FocusSeconds + .2);
            Check(field.FocusPoint.DistanceTo(seen) < 1 && field.InspectedEnemy is null, "a lost contact's card carries the camera to where it was last seen");

            tactical.SelectUnits([friends[0].Id], false); tactical.ToggleJump(); await Frames();
            Check(tactical.Notice?.StartsWith("Cannot jump") == true && tactical.NoticeStrip.Visible
                && tactical.NoticeLabel.GetThemeColor("font_color").R > .8f && tactical.NoticeLabel.GetThemeColor("font_color").G < .5f
                && tactical.NoticeStrip.GetGlobalRect().Position.Y > tactical.PauseButton.GetGlobalRect().End.Y,
                $"a refused jump shows a red notice below the time panel: {tactical.Notice}");
            await Capture("combat-hud-notice");
            await Delay(CombatScreen.NoticeSeconds + .3);
            Check(tactical.Notice is null && !tactical.NoticeStrip.Visible, "the notice fades after a few seconds");
        }
        finally { RemoveChild(tactical); tactical.QueueFree(); await Frames(); }
    }
}
