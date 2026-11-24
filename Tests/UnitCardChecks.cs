using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Unit cards: one line of callsign and chassis, a capped width with the spare bar in the gaps, and the status glyph.</summary>
    private async Task TestUnitCardsUi()
    {
        var roster = ForceRoster.Create("Card check", [DraftCatalog.Mech("locust"), DraftCatalog.Mech("griffin"), DraftCatalog.Mech("griffin")],
            TestPilots.Warriors.Take(3));
        for (var i = 0; i < 3; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
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
            await Delay(.15);
            var cards = tactical.UnitButtons;
            Check(cards.Select((card, i) => card.Text == $"{friends[i].Warrior.Callsign} · {friends[i].Spec.Chassis}").All(item => item)
                && cards[0].Text.EndsWith(" · Locust"), "each card reads callsign · chassis on one line: " + string.Join(" | ", cards.Select(card => card.Text)));
            var bar = cards[0].GetParent<Control>().GetGlobalRect();
            var rects = cards.Select(card => card.GetGlobalRect()).ToArray();
            var gaps = rects.Zip(rects.Skip(1), (left, right) => right.Position.X - left.End.X).ToArray();
            Check(rects.All(rect => rect.Size.X <= CombatScreen.MaxUnitCardWidth + .5f && rect.Size.X >= CombatScreen.MaxUnitCardWidth - 1)
                && Math.Abs(rects[0].Position.X - bar.Position.X) < 1 && Math.Abs(rects[^1].End.X - bar.End.X) < 3
                && gaps.All(gap => gap > 100 && Math.Abs(gap - gaps[0]) < 1),
                $"cards stop at their maximum width and the spare bar becomes even gaps: bar {bar} cards {string.Join("; ", rects)}");
            Check(tactical.UnitStatusIcons.All(icon => !icon.Visible), "an active mech on its feet shows no status glyph");
            await Capture("combat-unit-cards");

            friends[0].AddHeat(35); friends[1].Prone = true; friends[1].StandUpRemaining = 5; friends[2].Fleeing = true; await Delay(.15);
            void CheckIcon(int i, string name)
            {
                var icon = tactical.UnitStatusIcons[i]; var status = CombatPaperdoll.StatusIcon(friends[i]);
                Check(icon.IsVisibleInTree() && status is { } shown && icon.Texture == shown.Icon && icon.SelfModulate == shown.Tint
                    && shown.Icon.ResourcePath.EndsWith($"status-{name}.svg")
                    && cards[i].GetGlobalRect().Encloses(icon.GetGlobalRect()) && icon.GetGlobalRect().End.X < cards[i].GetGlobalRect().GetCenter().X
                    && icon.GetGlobalRect().End.Y < cards[i].GetGlobalRect().GetCenter().Y,
                    $"card {i} shows the {name} glyph in its top left corner");
            }
            CheckIcon(0, "shutdown"); CheckIcon(1, "prone"); CheckIcon(2, "fleeing");
            Check(cards[0].TooltipText.Contains("Shutdown") && cards[1].TooltipText.Contains("Prone · stands in") && cards[2].TooltipText.Contains("Fleeing"),
                "the card tooltip keeps the full status text");
            friends[1].ApplyDamage(MechLocation.CenterTorso, 1000); friends[2].Surrendered = true; await Delay(.15);
            CheckIcon(1, "destroyed"); CheckIcon(2, "surrendered");
            Check(new[] { "shutdown", "prone", "fleeing", "disabled", "destroyed", "surrendered", "fled", "extracted" }
                .All(name => ResourceLoader.Exists($"res://Assets/Icons/status-{name}.svg")), "every status has its own glyph");
            await Capture("combat-unit-card-status");

            tactical.Size = new Vector2(900, 500); await Frames();
            rects = cards.Select(card => card.GetGlobalRect()).ToArray();
            Check(rects.All(rect => tactical.GetGlobalRect().Encloses(rect)) && rects.Zip(rects.Skip(1), (left, right) => right.Position.X - left.End.X).All(gap => gap >= 5),
                $"on a narrow window the cards shrink below their maximum and keep a small gap: {string.Join("; ", rects)}");
        }
        finally { RemoveChild(tactical); tactical.QueueFree(); await Frames(); }
    }
}
