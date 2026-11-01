using Godot;

namespace Mech.Tests;

/// <summary>Critical-slot layout, equipment storage and hangar summary rules, independent of the scene tree.</summary>
internal static class HangarChecks
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var mech in DraftCatalog.AllMechs)
        {
            var layout = CriticalLayout.For(mech);
            var fits = layout.All(pair => pair.Value.Sum(block => block.Length) == EquipmentRules.Capacity(pair.Key)
                && pair.Value.Zip(pair.Value.Skip(1)).All(item => item.First.Start + item.First.Length == item.Second.Start));
            var placed = layout.Values.SelectMany(blocks => blocks).Where(block => block.ItemId is not null)
                .GroupBy(block => block.ItemId!).ToDictionary(group => group.Key, group => group.Sum(block => block.Length));
            var expected = mech.Weapons.Select(item => (item.Id, Slots: EquipmentRules.Slots(item.Kind)))
                .Concat(mech.Components.Select(item => (item.Id, item.Slots))).Concat(mech.Ammo.Select(item => (item.Id, Slots: 1))).ToArray();
            check(fits && expected.Length == placed.Count && expected.All(item => placed.GetValueOrDefault(item.Id) == item.Slots),
                $"{mech.DisplayName} places every item in its critical slots without overflow");
        }
        var wolverine = CriticalLayout.For(DraftCatalog.Mech("wolverine"));
        check(wolverine[MechLocation.Head].Select(block => (block.Name, block.Length)).SequenceEqual(
            [("Life Support", 2), ("Sensors", 2), ("Cockpit", 1), ("Medium Laser", 1)]), "Wolverine head carries cockpit systems and its laser");
        check(wolverine[MechLocation.CenterTorso].Select(block => (block.Kind, block.Length)).SequenceEqual(
            [(SlotKind.Engine, 6), (SlotKind.Gyro, 4), (SlotKind.JumpJet, 1), (SlotKind.Empty, 1)]), "Wolverine center torso holds engine, gyro, a jump jet and one free slot");
        var crab = CriticalLayout.For(DraftCatalog.Mech("king-crab"));
        var cannon = crab[MechLocation.LeftTorso].Single(block => block.Name == "AC/20");
        var spill = crab[MechLocation.LeftArm].Single(block => block.ItemId == cannon.ItemId);
        check(cannon.Length + spill.Length == 10 && spill.Length == 3, "King Crab AC/20 splits between its side torso and arm");

        var head = DraftCatalog.Mech("wolverine").Locations.Single(item => item.Location == MechLocation.Head);
        var arm = DraftCatalog.Mech("wolverine").Locations.Single(item => item.Location == MechLocation.LeftArm);
        check(EquipmentRules.ArmorLimit(head) == head.Structure * 3 && EquipmentRules.ArmorLimit(arm) == arm.Structure * 2,
            "armor limits are three times head structure and twice elsewhere");
        check(HangarScreen.KilometresPerHour(4) == 39 && HangarScreen.KilometresPerHour(6) == 58, "4/6 movement reads 39/58 km/h");
        var spec = DraftCatalog.Mech("wolverine");
        check(HangarScreen.SpeedSummary(spec) == "Walk 49 km/h (5 MP)\nRun 78 km/h (8 MP)\nJump 49 km/h (5 MP)", "summary lists walk, run and jump speeds");
        check(HangarScreen.HeatSummary(spec) == $"12 single sinks  ·  −12/turn  ·  alpha {spec.Weapons.Sum(item => item.Heat)}", "summary lists sinks, dissipation and alpha heat");
        check(!HangarScreen.SpeedSummary(DraftCatalog.Mech("victor") with { Jump = 0 }).Contains("Jump"), "mechs without jump jets show no jump speed");

        var draft = new InitialDraft { ForceName = "Storage Test" };
        for (var pool = 0; pool < 8; pool++) draft.Select(pool, 0);
        var roster = draft.Establish();
        int Count(string id) => roster.Storage.SingleOrDefault(item => item.Id == id)?.Count ?? 0;
        var mechs = roster.Mechs.Select(item => item.Spec).ToArray();
        check(Enum.GetValues<WeaponKind>().All(kind => Count(StorageCatalog.WeaponId(kind)) == mechs.Sum(mech => mech.Weapons.Count(item => item.Kind == kind)))
            && Enum.GetValues<AmmoKind>().All(kind => Count(StorageCatalog.AmmoId(kind)) == mechs.Sum(mech => mech.Ammo.Count(item => item.AmmoKind == kind))),
            "new company stores one spare of every mounted weapon and ammunition bin");
        check(Enum.GetValues<HeatSinkKind>().All(kind => Count(StorageCatalog.HeatSinkId(kind)) == mechs.Where(mech => mech.HeatSinkType == kind).Sum(mech => mech.HeatSinks))
            && roster.Storage.Where(item => item.Id.StartsWith("jump-jet:", StringComparison.Ordinal)).Sum(item => item.Count) == mechs.Sum(EquipmentRules.JumpJets),
            "new company stores every heat sink, engine-held ones included, and every jump jet");
        check(roster.Storage.All(item => StorageCatalog.Describe(item.Id) is not null
            && new[] { "weapon:", "ammo:", "heat-sink:", "jump-jet:" }.Any(prefix => item.Id.StartsWith(prefix, StringComparison.Ordinal))),
            "storage holds only weapons, ammunition, heat sinks and jump jets: no engines, gyros, actuators or armor");
        var restored = ForceRoster.Restore(roster.Snapshot(), Campaign.StartDate);
        check(restored.Storage.SequenceEqual(roster.Storage), "storage survives save and restore");
        var legacy = ForceRoster.Restore(roster.Snapshot() with { Storage = null }, Campaign.StartDate);
        check(legacy.Storage.SequenceEqual(StorageCatalog.Stock(mechs)), "older saves are stocked once from their mechs on load");
        foreach (var (bad, reason) in new[] { (new StorageEntry("weapon:Railgun", 1), "unknown item"), (new StorageEntry("weapon:MediumLaser", 0), "empty stack") })
        {
            var rejected = false;
            try { ForceRoster.Restore(roster.Snapshot() with { Storage = [bad] }, Campaign.StartDate); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, $"storage restore rejects an {reason}");
        }
        check(StorageCatalog.Describe(StorageCatalog.JumpJetId(55))!.Tons == .5f && StorageCatalog.Describe(StorageCatalog.JumpJetId(60))!.Tons == 1
            && StorageCatalog.Describe(StorageCatalog.JumpJetId(90))!.Tons == 2 && StorageCatalog.Describe(StorageCatalog.HeatSinkId(HeatSinkKind.Double))!.Slots == 3,
            "jump jet mass follows mech weight; double heat sinks take three slots");
    }
}

public partial class SmokeTests
{
    /// <summary>Hangar layout, location panels, storage list and the blueprint's right-drag spin.</summary>
    private async Task TestHangarScreen()
    {
        var hangar = _app.Hangar;
        hangar.Select(0); var mech = hangar.SelectedMech;
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames();
            var board = hangar.Board.GetGlobalRect();
            var panels = hangar.LocationPanels.Values.ToArray();
            var inside = panels.All(panel => panel.GetGlobalRect().Position.X >= board.Position.X - 1 && panel.GetGlobalRect().End.X <= board.End.X + 1);
            var apart = panels.All(a => panels.All(b => a == b || !a.GetGlobalRect().Grow(-1).Intersects(b.GetGlobalRect().Grow(-1))));
            Check(inside && apart, $"location panels fit the blueprint without overlapping at {size}");
            Check(!hangar.StoragePanel.GetGlobalRect().Intersects(board) && !hangar.SummaryPanel.GetGlobalRect().Intersects(board)
                && hangar.StoragePanel.GetGlobalRect().End.X <= board.Position.X && hangar.SummaryPanel.GetGlobalRect().Position.X >= board.End.X,
                $"storage sits left and summary right of the blueprint at {size}");
            Check(hangar.GetGlobalRect().Encloses(hangar.DeploymentButton.GetGlobalRect()) && hangar.DeploymentButton.GetGlobalRect().Position.Y > board.End.Y
                && hangar.DeploymentButton.GetGlobalRect().End.X > hangar.RosterButtons[^1].GetGlobalRect().End.X,
                $"Simulate Deployment sits at the right of the mech buttons at {size}");
            var back = hangar.BackButton.GetGlobalRect(); var deploy = hangar.DeploymentButton.GetGlobalRect();
            var rosterScroll = hangar.RosterButtons[0].GetParent().GetParent<Control>().GetGlobalRect();
            Check(back.Position.Y > board.End.Y && back.End.X <= deploy.Position.X && back.Position.X >= rosterScroll.End.X
                && Mathf.IsEqualApprox(back.Position.Y, deploy.Position.Y),
                $"Back to office sits between the mech buttons and Simulate Deployment at {size}");
            if (size.X < HangarScreen.CompactWidth)
                Check(hangar.Compact && hangar.Board.GetVScrollBar().MaxValue > hangar.Board.Size.Y, "compact blueprint scrolls its location panels");
            else
                Check(panels.All(panel => board.Encloses(panel.GetGlobalRect().Grow(-1))), "full-size blueprint shows every location panel without scrolling");
            await Capture($"hangar-blueprint-{size.X}x{size.Y}");
        }
        GetWindow().Size = new Vector2I(1440, 900); await Frames();

        var torso = hangar.LocationPanels[MechLocation.LeftTorso];
        var state = mech.Locations.Single(item => item.Location == MechLocation.LeftTorso);
        var spec = mech.Spec.Locations.Single(item => item.Location == MechLocation.LeftTorso);
        Check(torso.ArmorText == $"Armor {state.Armor}/{spec.Armor}" && torso.RearText == $"Rear {state.RearArmor}/{spec.RearArmor}"
            && torso.StructureText == $"Structure {state.Structure}/{spec.Structure}" && torso.ArmorLimit == spec.Structure * 2
            && torso.ArmorInstalled == spec.Armor + spec.RearArmor, "torso panel shows armor, rear armor, structure and the tonnage limit");
        Check(hangar.LocationPanels.All(pair => pair.Value.Blocks.Sum(block => block.Length) == EquipmentRules.Capacity(pair.Key)),
            "every location panel lists all of its critical slots");
        var weapon = torso.Blocks.Concat(hangar.LocationPanels.Values.SelectMany(panel => panel.Blocks)).First(block => block.Kind == SlotKind.Weapon);
        var owner = hangar.LocationPanels.Values.First(panel => panel.Blocks.Contains(weapon));
        var tooltip = owner._GetTooltip(owner.BlockRect(weapon).GetCenter());
        Check(tooltip.StartsWith(weapon.Name, StringComparison.Ordinal) && tooltip.Contains(" t · ") && tooltip.Contains("Operational"),
            "hovering a slot names the item with its mass, slots and condition");
        Check(hangar.SpeedText.Text == HangarScreen.SpeedSummary(mech.Spec) && hangar.HeatText.Text == HangarScreen.HeatSummary(mech.Spec)
            && hangar.ProtectionText.Text.StartsWith("Armor ", StringComparison.Ordinal), "summary shows speeds, protection and heat");
        var lines = hangar.StorageList.GetChildren().OfType<Label>().Select(label => label.Text).ToArray();
        Check(lines.Contains("ENERGY") && lines.Contains("AMMO") && lines.Contains("EQUIPMENT")
            && lines.Any(line => line.StartsWith("Heat Sink ×", StringComparison.Ordinal)), "storage lists grouped spare equipment");

        // A right click that stays put goes back; a drag spins the blueprint and springs back to front-on.
        var model = hangar.Preview.Model!;
        var at = hangar.Board.GetGlobalRect().GetCenter();
        MouseMotion(at, Vector2.Zero);
        MouseButton(at, Godot.MouseButton.Right, true); await Frames();
        MouseMotion(at + new Vector2(3, 4), new Vector2(3, 4)); await Frames();
        Check(hangar.Visible && model.Rotation.Y == 0, "a right press within five pixels neither leaves nor spins");
        MouseMotion(at + new Vector2(43, 4), new Vector2(40, 0)); await Frames();
        Check(hangar.Visible && Math.Abs(Mathf.RadToDeg(model.Rotation.Y) - 20) < .01f, "right drag spins the blueprint half a degree per pixel");
        MouseButton(at + new Vector2(43, 4), Godot.MouseButton.Right, false); await Frames();
        Check(hangar.Visible && _app.Navigation.CurrentScreen == CommandScreen.Hangar, "releasing a drag stays in the hangar");
        hangar.Preview._Process(.1);
        Check(model.Rotation.Y > 0 && model.Rotation.Y < Mathf.DegToRad(20), "released blueprint eases back toward the front");
        hangar.Preview._Process(.25);
        Check(model.Rotation.Y == 0 && hangar.Preview.Yaw == 0, "blueprint is front-on again within 0.3 seconds");
        await RightClick(at + new Vector2(2, 0));
        Check(_app.Office.Visible && !hangar.Visible, "a still right click returns to the office");
        await Click(_app.Office.HangarButton);

        // Space hides the location panels to show the model, without pressing the focused Back button.
        hangar.BackButton.GrabFocus(); await Frames();
        await PressKey(Key.Space);
        Check(hangar.Visible && !hangar.Board.Visible && hangar.Preview.IsVisibleInTree(), "Space hides the location panels and keeps the hangar open");
        await PressKey(Key.Space);
        Check(hangar.Board.Visible, "Space shows the location panels again");
        await PressKey(Key.Space); await Click(hangar.BackButton); await Click(_app.Office.HangarButton);
        Check(hangar.Board.Visible, "reopening the hangar shows the location panels");

        // Tab cycles the preview gait still → walking → running → still, without moving button focus.
        hangar.Preview.Gait = PreviewGait.Idle; hangar.BackButton.GrabFocus(); await Frames();
        foreach (var expected in new[] { PreviewGait.Walk, PreviewGait.Run, PreviewGait.Idle })
        {
            await PressKey(Key.Tab);
            Check(hangar.Preview.Gait == expected, $"Tab switches the hangar preview to {expected}");
        }
        Check(hangar.BackButton.HasFocus(), "Tab keeps focus on the focused hangar button");

        // Debug builds: F6 picks a hardpoint, F7 cycles its visual on the preview only.
        hangar.Select(0); await Frames();
        var owned = hangar.SelectedMech; var stock = hangar.Preview.Model!;
        var first = MechModel.Hardpoints(owned.Spec.Id)[0];
        await PressKey(Key.F6);
        Check(ReferenceEquals(stock, hangar.Preview.Model), "F6 selects a hardpoint without rebuilding the preview");
        await PressKey(Key.F7);
        var cycled = hangar.Preview.Model!;
        Check(!ReferenceEquals(stock, cycled) && !cycled.Mounts.Stock
            && cycled.Mounts.Visuals.GetValueOrDefault(first.Id) != stock.Mounts.Visuals.GetValueOrDefault(first.Id),
            "F7 rebuilds the preview with the selected hardpoint's next visual");
        Check(ReferenceEquals(owned, hangar.SelectedMech) && owned.Spec == DraftCatalog.Mech(owned.Spec.Id) && hangar.Preview.MountOverride is not null,
            "the debug mount cycle leaves the owned mech untouched");
        hangar.Refresh();
        Check(ReferenceEquals(cycled, hangar.Preview.Model), "refresh keeps the debug preview");
        hangar.Select(1); hangar.Select(0);
        Check(hangar.Preview.MountOverride is null && hangar.Preview.Model!.Mounts.Stock, "selecting another mech clears the debug preview");
    }
}
