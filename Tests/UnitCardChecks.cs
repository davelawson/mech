using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Unit cards: one line of callsign and chassis, a capped width, a 2%-of-window gap, left-justified clear of the details panel, the panel to the window's foot, and the status glyph.</summary>
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
            // The gap is 2% of the window width, the first card sits one gap from the left edge, and the row stops a gap short of the details panel.
            bool SpacedFromLeft(Rect2[] cardRects)
            {
                var window = tactical.GetGlobalRect(); var details = tactical.Sidebar.GetGlobalRect();
                var gap = Mathf.Round(tactical.Size.X * CombatScreen.UnitCardGapShare);
                return cardRects.Zip(cardRects.Skip(1), (left, right) => right.Position.X - left.End.X).All(between => Math.Abs(between - gap) < 1)
                    && Math.Abs(cardRects[0].Position.X - window.Position.X - gap) < 1 && cardRects[^1].End.X <= details.Position.X - gap + 1;
            }
            // The details panel runs from the top margin to the window's foot, beside the cards.
            bool DetailsToFoot() => tactical.Sidebar.IsVisibleInTree()
                && Math.Abs(tactical.Sidebar.GetGlobalRect().End.Y - tactical.GetGlobalRect().End.Y) < 1;
            Check(rects.All(rect => rect.Size.X <= CombatScreen.MaxUnitCardWidth + .5f && rect.Size.X >= CombatScreen.MaxUnitCardWidth - 1)
                && SpacedFromLeft(rects),
                $"cards stop at their maximum width, 2% of the window apart and left-justified: bar {bar} cards {string.Join("; ", rects)} details {tactical.Sidebar.GetGlobalRect()}");
            Check(DetailsToFoot(), $"the details panel reaches the window's foot: {tactical.Sidebar.GetGlobalRect()} in {tactical.GetGlobalRect()}");
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
            Check(rects.All(rect => tactical.GetGlobalRect().Encloses(rect)) && SpacedFromLeft(rects) && DetailsToFoot(),
                $"on a smaller window the gap follows the window width, the row stays left-justified and the details panel reaches the foot: {string.Join("; ", rects)}");
            tactical.Size = new Vector2(640, 500); await Frames();
            rects = cards.Select(card => card.GetGlobalRect()).ToArray();
            Check(rects.All(rect => tactical.GetGlobalRect().Encloses(rect) && rect.Size.X < CombatScreen.MaxUnitCardWidth - 1) && SpacedFromLeft(rects),
                $"on a narrow window the cards shrink below their maximum, keep the 2% gap and stay clear of the details panel: {string.Join("; ", rects)}");
        }
        finally { RemoveChild(tactical); tactical.QueueFree(); await Frames(); }
    }
}
