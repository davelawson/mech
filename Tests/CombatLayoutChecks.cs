using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestCombatLayouts()
    {
        await TestPaperdollScrollLayout();
        await TestContactPreviewLayout();
        await TestHeatControls();
    }

    private async Task TestPaperdollScrollLayout()
    {
        // This small layout reproduced the callback-queue exhaustion at height 355.
        // Run with an external timeout: a layout loop prevents even timers from firing.
        var panel = new PanelContainer { Size = new Vector2(318, 348) };
        panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line));
        AddChild(panel);
        try
        {
            var body = RosterUi.Scroll(panel);
            var scroll = body.GetParent<ScrollContainer>();
            var paper = new CombatPaperdoll(); body.AddChild(paper);
            body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 100) });
            var resizes = 0;
            paper.Resized += () => resizes++;
            var heights = Enumerable.Range(348, 22).ToArray();
            foreach (var height in heights.Concat(heights.Reverse()))
            {
                panel.Size = new Vector2(318, height);
                await Frames();
                var size = paper.Size;
                var minimum = paper.CustomMinimumSize;
                var scrollbar = scroll.GetVScrollBar().Visible;
                var settledResizes = resizes;
                await Frames();
                Check(paper.Size == size && paper.CustomMinimumSize == minimum
                    && scroll.GetVScrollBar().Visible == scrollbar && resizes == settledResizes,
                    $"paperdoll and automatic scrollbar settle at panel height {height}");
            }
        }
        finally
        {
            RemoveChild(panel); panel.QueueFree();
            await Frames();
        }
    }

    private async Task TestContactPreviewLayout()
    {
        var roster = ForceRoster.Create("Contact layout check", [DraftCatalog.Mech("atlas")], [DraftCatalog.Warrior("elena-voss")]);
        roster.Assign(roster.Mechs[0].Id, roster.Warriors[0].Id);
        var window = GetWindow();
        var originalSize = window.Size;
        var originalMinimum = window.MinSize;
        var tactical = new CombatScreen { Theme = Ui.CreateTheme(), Size = new Vector2(1440, 900) };
        AddChild(tactical);
        try
        {
            window.MinSize = new Vector2I(900, 500);
            tactical.Open(roster, roster.Mechs[0].Id);
            tactical.MirrorMatchButton.EmitSignal(BaseButton.SignalName.Pressed);
            tactical.TerrainSeedInput.Text = "183";
            tactical.TerrainSeedInput.EmitSignal(LineEdit.SignalName.TextChanged, "183");
            tactical.LaunchButton.EmitSignal(BaseButton.SignalName.Pressed);
            var sim = tactical.Simulation!;
            var friendly = sim.Units.Single(unit => unit.Side == CombatSide.Friendly);
            var enemy = sim.Units.Single(unit => unit.Side == CombatSide.Enemy);
            sim.EnemyAiEnabled = false;
            foreach (var unit in sim.Units)
            {
                unit.Firing = FiringMode.HoldFire;
                // Keep this fixture's identification-before-weapons case after the LRM range increase.
                foreach (var weapon in unit.Weapons) weapon.Enabled = weapon.LongRange < 48;
            }
            sim.Paused = false;
            Check(CombatRules.DetectionRadius(friendly, enemy) == 50 && friendly.Weapons.Where(weapon => weapon.Enabled).All(weapon => weapon.LongRange < 48),
                "contact layout fixture detects a target before any weapon can reach it");

            Vector2 PositionAt(float distance) => Enumerable.Range(0, 32)
                .Select(index => friendly.Position + Vector2.FromAngle(index * Mathf.Tau / 32) * distance)
                .First(point => CombatRules.Clamp(point) == point && sim.Terrain.Passable(point)
                    && sim.Terrain.ClearSight(friendly.Position, point));
            async Task ContactAt(float distance)
            {
                enemy.Position = PositionAt(distance);
                sim.RefreshDetection();
                await Delay(.15);
            }
            string[] VisibleText() => tactical.FindChildren("*", "Label", true, false).OfType<Label>()
                .Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();

            foreach (var size in new[] { new Vector2I(900, 500), new Vector2I(1440, 900) })
            {
                window.Size = size; tactical.Size = size;
                await Frames();
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    await ContactAt(60);
                    Check(!sim.Detected(friendly.Side, enemy.Id) && VisibleText().Any(text => text.Contains("No contact")),
                        $"lost contact clears the ordinary preview at {size}, cycle {cycle}");
                    await ContactAt(48);
                    Check(sim.Detected(friendly.Side, enemy.Id) && !sim.DetailsVisible(friendly.Side, enemy.Id)
                        && VisibleText().Any(text => text.Contains("Aim preview: Atlas")),
                        $"identification outside weapon range updates the ordinary preview at {size}, cycle {cycle}");
                    await ContactAt(24);
                    Check(sim.DetailsVisible(friendly.Side, enemy.Id) && tactical.Paperdoll.Unit == friendly
                        && tactical.Battlefield.InspectedEnemy is null,
                        $"close detection keeps the friendly sidebar without enemy inspection at {size}, cycle {cycle}");
                }
                await ContactAt(48);
                await Capture($"combat-contact-preview-{size.X}x{size.Y}");

                // Exercise information masking through the same UI refresh, without desktop input.
                // Production keyboard/mouse focus guards remain enabled.
                await ContactAt(24);
                tactical.Battlefield.InspectedEnemy = enemy.Id;
                tactical.SelectUnits([friendly.Id], false);
                Check(tactical.Paperdoll.Unit == enemy && VisibleText().Contains(CombatScreen.SystemsReadout(enemy))
                    && VisibleText().Any(text => text.Contains(CombatScreen.HeatReadout(enemy))),
                    $"detailed inspection shows live condition at {size}");
                await Capture($"combat-contact-details-{size.X}x{size.Y}");
                await ContactAt(48);
                Check(tactical.Paperdoll.Unit is null && !VisibleText().Any(text => text.Contains("SYSTEMS · CONDITION")
                    || text.Contains("ARMOR / STOCK") || text.Contains("Heat ")), $"model-only inspection clears detailed readouts including heat at {size}");
                await Capture($"combat-contact-model-{size.X}x{size.Y}");
                await ContactAt(60);
                Check(tactical.Battlefield.InspectedEnemy is null && tactical.Paperdoll.Unit == friendly,
                    $"lost inspection restores the friendly sidebar at {size}");
                tactical.SelectUnits([], false);
                Check(!VisibleText().Any(text => text.Contains("SYSTEMS · CONDITION")),
                    $"clearing selection removes stale systems text at {size}");
                tactical.SelectUnits([friendly.Id], false);
                Check(sim.Units.All(unit => unit.ShotsFired == 0) && sim.Outcome == CombatOutcome.Ongoing,
                    $"layout transitions remain responsive without firing at {size}");
            }
        }
        finally
        {
            RemoveChild(tactical); tactical.QueueFree();
            window.MinSize = originalMinimum; window.Size = originalSize;
            await Frames();
        }
    }
}
