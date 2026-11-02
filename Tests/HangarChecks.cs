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
        LoadoutChecks(check);
        InstallChecks(check);
        check(StorageCatalog.Describe(StorageCatalog.JumpJetId(55))!.Tons == .5f && StorageCatalog.Describe(StorageCatalog.JumpJetId(60))!.Tons == 1
            && StorageCatalog.Describe(StorageCatalog.JumpJetId(90))!.Tons == 2 && StorageCatalog.Describe(StorageCatalog.HeatSinkId(HeatSinkKind.Double))!.Slots == 3,
            "jump jet mass follows mech weight; double heat sinks take three slots");
    }

    /// <summary>Installing spares: placement after the stock items, mounts, location and compatibility rules, storage, tonnage and saves.</summary>
    private static void InstallChecks(Action<bool, string> check)
    {
        var wolverine = DraftCatalog.Mech("wolverine");
        var roster = ForceRoster.Create("Install Test", [wolverine, wolverine], DraftCatalog.Warriors.Take(2));
        var mech = roster.Mechs[0]; var stock = mech.Spec;
        int Count(string id) => roster.Storage.SingleOrDefault(item => item.Id == id)?.Count ?? 0;
        int Free(OwnedMech owned, MechLocation location) => EquipmentRules.Capacity(location) - CriticalLayout.Used(CriticalLayout.For(owned.Spec)[location]);
        check(Loadout.NextId([new("fit:1", "ammo:AC5", MechLocation.LeftTorso), new("fit:3", "ammo:AC5", MechLocation.LeftTorso)]) == "fit:4",
            "installed item ids are never reused");

        // A removed weapon frees its mount for a spare, which goes after everything else in the location.
        var weapon = stock.Weapons.First(item => EquipmentRules.Slots(item.Kind) <= EquipmentRules.Capacity(item.Location)
            && Loadout.FreeMounts(Loadout.Strip(stock, new HashSet<string> { item.Id }), item.Location, WeaponMounts.ClassOf(item.Kind)) > 0);
        var weaponId = StorageCatalog.WeaponId(weapon.Kind);
        var unmounted = Enum.GetValues<MechLocation>().First(location => Loadout.FreeMounts(stock, location, WeaponMounts.ClassOf(weapon.Kind)) <= 0
            && Free(mech, location) >= EquipmentRules.Slots(weapon.Kind));
        check(Loadout.InstallProblem(mech, weaponId, unmounted) == $"No free {WeaponMounts.ClassOf(weapon.Kind).ToString().ToLowerInvariant()} mount",
            "a weapon needs a free mount of its class in the location");
        var removed = LoadoutPlan.Removing([weapon.Id]);
        check(Loadout.InstallProblem(mech.WithLoadout(removed), weaponId, weapon.Location) is null, "removing a weapon frees its mount");
        var spare = new InstalledItem("fit:1", weaponId, weapon.Location);
        var plan = removed with { Installed = [spare] }; var refused = false;
        var fitted = mech.WithLoadout(plan);
        var blocks = CriticalLayout.For(fitted.Spec)[weapon.Location];
        check(blocks.Last(block => block.Kind != SlotKind.Empty).ItemId == spare.Id
            && blocks.SkipWhile(block => block.Kind != SlotKind.Empty).All(block => block.Kind == SlotKind.Empty)
            && fitted.Tons == stock.Tonnage && fitted.Condition(spare.Id) == ComponentCondition.Operational
            && fitted.Spec.Weapons.Single(item => item.Id == spare.Id) is { Rear: false } installed && installed.Kind == weapon.Kind && mech.Spec == stock,
            "an installed spare takes the first empty slots after the rest, operational and facing forward");

        // Applying moves spares out of storage, and taking an installed item out returns it.
        var before = Count(weaponId);
        roster.ApplyLoadout(mech.Id, plan);
        check(Count(weaponId) == before && mech.Installed.Single() == spare && mech.Spec.Weapons.Length == stock.Weapons.Length,
            "applying swaps the removed weapon into storage and the spare out of it");
        var restored = ForceRoster.Restore(roster.Snapshot(), Campaign.StartDate);
        check(restored.Mechs[0].Installed.SequenceEqual(mech.Installed) && restored.Mechs[0].Spec.Weapons.Any(item => item.Id == spare.Id),
            "installed items survive save and restore");
        roster.ApplyLoadout(mech.Id, mech.Plan with { Installed = [] });
        check(Count(weaponId) == before + 1 && mech.Installed.IsEmpty && mech.Spec.Weapons.All(item => item.Id != spare.Id),
            "taking an installed item out returns it to storage");

        // Mount slots: other items fill regular slots first, then cover mounts bottom-up, or the mount they were dropped on.
        var binId = StorageCatalog.AmmoId(stock.Ammo[0].AmmoKind);
        var opened = mech.WithLoadout(removed);
        var mounts = MountLayout.For(opened.Spec, opened.Installed)[weapon.Location];
        var empty = Free(opened, weapon.Location); var regular = empty - mounts.Free.Length;
        InstalledItem[] Bins(int count, HardpointClass? mount = null) => [.. Enumerable.Range(1, count).Select(n => new InstalledItem($"fit:{n}", binId, weapon.Location, n == 1 ? mount : null))];
        LocationMounts MountsWith(InstalledItem[] items) => MountLayout.For(Loadout.Build(stock, removed.Removed, items), items)[weapon.Location];
        check(mounts.Free.Contains(WeaponMounts.ClassOf(weapon.Kind)) && regular >= 0 && mounts.Free.SequenceEqual(mounts.Free.Order()),
            "a location lists its free mounts Ballistic, Energy then Missile");
        var filled = MountsWith(Bins(regular));
        check(filled.Free.SequenceEqual(mounts.Free) && filled.Covered.IsEmpty, "other items fill regular slots before weapon mounts");
        var spilled = MountsWith(Bins(regular + 1));
        check(spilled.Free.SequenceEqual(mounts.Free.Take(mounts.Free.Length - 1)) && spilled.Covered.Count == 1
            && spilled.Covered[$"fit:{regular + 1}"].SequenceEqual([mounts.Free[^1]]), "an item with no regular slot left covers the bottom-most free mount");
        var chosen = MountsWith(Bins(1, mounts.Free[0]));
        check(chosen.Covered["fit:1"].SequenceEqual([mounts.Free[0]]) && chosen.Free.SequenceEqual(mounts.Free.Skip(1)),
            "an item dropped on a mount covers that mount even with regular slots free");
        var type = WeaponMounts.ClassOf(weapon.Kind); var ofType = mounts.Free.Count(item => item == type);
        var blocking = Enumerable.Range(1, ofType).Select(n => new InstalledItem($"fit:{n}", binId, weapon.Location, type)).ToArray();
        check(Loadout.InstallProblem(mech.WithLoadout(removed with { Installed = [.. blocking] }), weaponId, weapon.Location) == $"No free {type.ToString().ToLowerInvariant()} mount",
            "a weapon cannot use a mount another item covers");
        var coveredSave = roster.Snapshot();
        coveredSave = coveredSave with { Mechs = [coveredSave.Mechs[0] with { Removed = [weapon.Id], Installed = Bins(1, mounts.Free[0]),
            Equipment = [.. coveredSave.Mechs[0].Equipment.Where(item => item.Id != weapon.Id), new("fit:1", ComponentCondition.Operational)],
            Ammo = [.. coveredSave.Mechs[0].Ammo, new("fit:1", stock.Ammo[0].Shots)] }, .. coveredSave.Mechs[1..]] };
        check(ForceRoster.Restore(coveredSave, Campaign.StartDate).Mechs[0].Installed.Single().Mount == mounts.Free[0], "a covered mount survives save and restore");
        refused = false;
        try { ForceRoster.Restore(coveredSave with { Mechs = [coveredSave.Mechs[0] with { Installed = [new("fit:1", binId, weapon.Location, HardpointClass.Ams)] }, .. coveredSave.Mechs[1..]] }, Campaign.StartDate); }
        catch (ArgumentException) { refused = true; }
        check(refused, "restore rejects an item covering an unknown mount class");

        // Heat sinks add cooling and mass; an overweight loadout cannot be applied.
        var sinkId = StorageCatalog.HeatSinkId(stock.HeatSinkType);
        var full = roster.Mechs[1];
        var room = Enum.GetValues<MechLocation>().First(location => Free(full, location) >= EquipmentRules.HeatSinkSlots(stock.HeatSinkType));
        var heavy = full.WithLoadout(full.Plan with { Installed = [new(Loadout.NextId(full.Installed), sinkId, room)] });
        check(heavy.Spec.HeatSinks == stock.HeatSinks + 1 && heavy.Tons == stock.Tonnage + 1 && heavy.OverTonnage, "a heat sink adds cooling and a ton");
        refused = false;
        try { roster.ApplyLoadout(full.Id, heavy.Plan); }
        catch (ArgumentException) { refused = true; }
        check(refused && full.Installed.IsEmpty, "an overweight loadout cannot be applied");
        var other = stock.HeatSinkType == HeatSinkKind.Single ? HeatSinkKind.Double : HeatSinkKind.Single;
        check(Loadout.InstallProblem(mech, StorageCatalog.HeatSinkId(other), room) == $"Takes {DefinitionText.HeatSinks(stock.HeatSinkType)} heat sinks only",
            "heat sinks must match the mech's type");

        // Jump jets: torsos and legs only, the mech's own mass class, and no more jump than walking MP.
        var jetId = StorageCatalog.JumpJetId(stock.Tonnage);
        check(Loadout.InstallProblem(mech, jetId, MechLocation.LeftArm) == "Jump jets: torso or legs only"
            && Loadout.InstallProblem(mech, StorageCatalog.JumpJetId(100), MechLocation.CenterTorso) == $"Wrong jump jets for {stock.Tonnage} t",
            "jump jets go in torsos and legs and match the mech's mass class");
        var jet = stock.Components.First(item => item.Kind == ComponentKind.JumpJet);
        var grounded = mech.WithLoadout(mech.Plan with { Removed = mech.Removed.Add(jet.Id) });
        check(stock.Jump == stock.Walk ? Loadout.InstallProblem(mech, jetId, jet.Location) == "Jump MP cannot exceed walking MP"
                && Loadout.InstallProblem(grounded, jetId, jet.Location) is null
            : Loadout.InstallProblem(mech, jetId, jet.Location) is null,
            "jump jets cannot raise jump MP above walking MP");

        // Full and destroyed locations take nothing.
        var bin = StorageCatalog.AmmoId(stock.Ammo[0].AmmoKind);
        var free = Free(mech, room);
        var packed = mech.WithLoadout(mech.Plan with { Installed = [.. Enumerable.Range(1, free).Select(n => new InstalledItem($"fit:{n}", bin, room))] });
        check(Free(packed, room) == 0 && Loadout.InstallProblem(packed, bin, room) == "Needs 1 free slot", "a full location takes nothing more");
        var save = roster.Snapshot();
        var wrecked = ForceRoster.Restore(save with { Mechs = [save.Mechs[0] with { Locations = [.. save.Mechs[0].Locations
            .Select(item => item.Location == room ? item with { Armor = 0, RearArmor = 0, Structure = 0 } : item)] }, .. save.Mechs[1..]] }, Campaign.StartDate);
        check(Loadout.InstallProblem(wrecked.Mechs[0], bin, room) == "Location destroyed", "a destroyed location takes nothing");

        refused = false;
        try { ForceRoster.Restore(save with { Mechs = [save.Mechs[0] with { Installed = [new("fit:1", "weapon:Railgun", room)] }, .. save.Mechs[1..]] }, Campaign.StartDate); }
        catch (ArgumentException) { refused = true; }
        check(refused, "restore rejects an unknown installed item");
    }

    /// <summary>Individual heat sinks and jump jets, removal, storage on apply, scrapping, suspect items and saved loadouts.</summary>
    private static void LoadoutChecks(Action<bool, string> check)
    {
        check(DraftCatalog.AllMechs.All(mech => mech.Components.All(c => c.Kind switch
            {
                ComponentKind.HeatSink => c.HeatSinkCount == 1 && c.Slots == EquipmentRules.HeatSinkSlots(mech.HeatSinkType),
                ComponentKind.JumpJet => c.Slots == 1, _ => true
            }) && EquipmentRules.JumpJets(mech) == mech.Jump),
            "every slotted heat sink and jump jet is its own item");
        var wolverine = DraftCatalog.Mech("wolverine");
        check(CriticalLayout.For(wolverine).Values.SelectMany(blocks => blocks).Count(block => block.Kind == SlotKind.JumpJet) == wolverine.Jump,
            "Wolverine shows each of its jump jets as a separate slot");

        ForceRoster Company() => ForceRoster.Create("Loadout Test", [wolverine, wolverine], DraftCatalog.Warriors.Take(2));
        var roster = Company(); var mech = roster.Mechs[0]; var stock = mech.Spec;
        int Count(string id) => roster.Storage.SingleOrDefault(item => item.Id == id)?.Count ?? 0;
        var weapon = stock.Weapons.First(item => item.AmmoKind is not null);
        var jet = stock.Components.First(item => item.Kind == ComponentKind.JumpJet);
        var sink = stock.Components.FirstOrDefault(item => item.Kind == ComponentKind.HeatSink);
        var removed = new[] { weapon.Id, jet.Id }.Concat(sink is null ? [] : [sink.Id]).ToHashSet();
        var preview = mech.WithLoadout(LoadoutPlan.Removing(removed));
        check(preview.Spec.Weapons.Length == stock.Weapons.Length - 1 && preview.Spec.Jump == stock.Jump - 1
            && preview.Spec.HeatSinks == stock.HeatSinks - (sink is null ? 0 : 1) && preview.Equipment.All(item => !removed.Contains(item.Id))
            && mech.Spec == stock && mech.Removed.IsEmpty, "a pending loadout previews removals without changing the owned mech");
        var mass = EquipmentRules.Tons(weapon.Kind) + EquipmentRules.JumpJetTons(stock.Tonnage) + (sink is null ? 0 : 1);
        check(Mathf.IsEqualApprox(preview.Tons, stock.Tonnage - mass) && HangarScreen.TonnageSummary(mech) == $"{stock.Tonnage} / {stock.Tonnage} t",
            "current tonnage is the rating less the mass of removed items");
        var before = (Weapon: Count(StorageCatalog.WeaponId(weapon.Kind)), Jet: Count(StorageCatalog.JumpJetId(stock.Tonnage)));
        roster.ApplyLoadout(mech.Id, LoadoutPlan.Removing(removed));
        check(mech.Removed.SetEquals(removed) && mech.Spec.Jump == stock.Jump - 1 && Count(StorageCatalog.WeaponId(weapon.Kind)) == before.Weapon + 1
            && Count(StorageCatalog.JumpJetId(stock.Tonnage)) == before.Jet + 1, "applying a loadout moves the removed items into storage");
        var restored = ForceRoster.Restore(roster.Snapshot(), Campaign.StartDate);
        check(restored.Mechs[0].Removed.SetEquals(removed) && restored.Mechs[0].Spec.Weapons.Length == stock.Weapons.Length - 1
            && restored.Storage.SequenceEqual(roster.Storage), "a removed loadout survives save and restore");
        var rejected = false;
        try { roster.ApplyLoadout(mech.Id, LoadoutPlan.Removing(removed.Append(stock.Components.First(item => item.Kind == ComponentKind.Gyro).Id))); }
        catch (ArgumentException) { rejected = true; }
        check(rejected && mech.Removed.SetEquals(removed), "fixed systems cannot be removed");
        rejected = false;
        try { ForceRoster.Restore(roster.Snapshot() with { Mechs = [roster.Snapshot().Mechs[0] with { Removed = ["component-1"] }, .. roster.Snapshot().Mechs[1..]] }, Campaign.StartDate); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "restore rejects a loadout that removes a fixed system");

        // A destroyed item is scrapped rather than stored.
        var fresh = Company(); var target = fresh.Mechs[1];
        var save = fresh.Snapshot();
        var broken = save with { Mechs = [.. save.Mechs.Select(item => item.Id != target.Id ? item : item with
            { Equipment = [.. item.Equipment.Select(e => e.Id == weapon.Id ? e with { Condition = ComponentCondition.Destroyed } : e)] })] };
        fresh = ForceRoster.Restore(broken, Campaign.StartDate);
        var stored = fresh.Storage.Single(item => item.Id == StorageCatalog.WeaponId(weapon.Kind)).Count;
        fresh.ApplyLoadout(target.Id, LoadoutPlan.Removing([weapon.Id]));
        check(fresh.Storage.Single(item => item.Id == StorageCatalog.WeaponId(weapon.Kind)).Count == stored && fresh.Mechs[1].Removed.Contains(weapon.Id),
            "a destroyed item is scrapped when removed");

        // Saves from before individual items record one condition per group.
        var grouped = fresh.Snapshot();
        var legacy = grouped with { Mechs = [.. grouped.Mechs.Select(item => item with { Equipment = [.. item.Equipment
            .Select(e => e with { Id = e.Id.Contains('.') ? e.Id[..e.Id.IndexOf('.')] : e.Id }).DistinctBy(e => e.Id)] })] };
        check(ForceRoster.Restore(legacy, Campaign.StartDate).Mechs.All(item => item.Equipment.Count == item.Spec.Weapons.Length + item.Spec.Components.Length + item.Spec.Ammo.Length),
            "older saves give each heat sink and jump jet its group's condition");

        // Suspect items: a weapon with no compatible bin aboard, and a bin no weapon fires.
        var bins = stock.Ammo.Where(bin => bin.AmmoKind == weapon.AmmoKind).Select(bin => bin.Id).ToHashSet();
        check(Loadout.Suspect(stock).IsEmpty && Loadout.Suspect(Loadout.Strip(stock, bins)).SetEquals(stock.Weapons.Where(item => item.AmmoKind == weapon.AmmoKind).Select(item => item.Id))
            && Loadout.Suspect(Loadout.Strip(stock, stock.Weapons.Where(item => item.AmmoKind == weapon.AmmoKind).Select(item => item.Id).ToHashSet())).SetEquals(bins),
            "weapons without ammunition and ammunition without weapons are suspect");
    }
}

public partial class SmokeTests
{
    /// <summary>A point on the blueprint board clear of every location panel, where a right click goes back.</summary>
    private static Vector2 BoardBackground(HangarScreen hangar)
    {
        var board = hangar.Board.GetGlobalRect();
        for (var y = board.Position.Y + 4; y < board.End.Y; y += 8)
            for (var x = board.Position.X + 4; x < board.End.X; x += 8)
                if (hangar.LocationPanels.Values.All(panel => !panel.GetGlobalRect().Grow(2).HasPoint(new(x, y)))) return new(x, y);
        throw new InvalidOperationException("The hangar board has no background clear of its location panels.");
    }

    private static Vector2 BlockCentre(HangarLocationPanel panel, CriticalBlock block) =>
        panel.GetGlobalTransformWithCanvas() * panel.BlockRect(block).GetCenter();
    private static (HangarLocationPanel Panel, CriticalBlock Block) FindBlock(HangarScreen hangar, Func<CriticalBlock, bool> match) =>
        hangar.LocationPanels.Values.SelectMany(panel => panel.Blocks.Select(block => (panel, block))).First(item => match(item.block));
    private static IEnumerable<string> StorageTexts(HangarScreen hangar) =>
        hangar.StorageList.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text);
    private static int StorageCount(HangarScreen hangar, string name) => StorageTexts(hangar).Where(text => text.StartsWith(name + " ×", StringComparison.Ordinal))
        .Select(text => int.Parse(text[(name.Length + 2)..])).SingleOrDefault();

    /// <summary>Right-click removal, pending loadouts per mech, Apply and Reset, the ⚠ confirmation, the leave warning and simulations.</summary>
    private async Task TestHangarLoadout()
    {
        var hangar = _app.Hangar; var roster = _app.State.Roster!;
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        var index = roster.Mechs.ToList().FindIndex(mech => mech.Spec.Weapons.Any(item => item.AmmoKind is not null) && mech.Spec.Weapons.Any(item => item.AmmoKind is null));
        hangar.Select(index); await Frames();
        var mech = hangar.SelectedMech; var stock = mech.Spec;
        Check(hangar.ApplyButton.Disabled && hangar.ResetButton.Disabled && hangar.TonnageText.Text == $"{stock.Tonnage} / {stock.Tonnage} t",
            "a stock mech shows full tonnage, with Apply and Reset disabled");

        // A fixed system ignores the right click and does not leave the hangar.
        var (fixedPanel, fixedBlock) = FindBlock(hangar, block => block.Kind is SlotKind.Engine or SlotKind.Actuator);
        await RightClick(BlockCentre(fixedPanel, fixedBlock));
        Check(hangar.Visible && !hangar.HasPendingChanges, "right-clicking a fixed system neither removes it nor leaves the hangar");

        var weapon = stock.Weapons.First(item => item.AmmoKind is null);
        var (panel, block) = FindBlock(hangar, item => item.ItemId == weapon.Id);
        var stored = StorageCount(hangar, weapon.Name);
        Check(panel._GetTooltip(panel.BlockRect(block).GetCenter()).Contains("Right-click to remove"), "removable items say how to remove them");
        await RightClick(BlockCentre(panel, block));
        Check(hangar.Visible && hangar.SelectedHasPendingChanges && panel.Blocks.All(item => item.ItemId != weapon.Id)
            && panel.Blocks.Sum(item => item.Length) == EquipmentRules.Capacity(panel.Location), "right-clicking a weapon removes it and frees its slots");
        Check(hangar.TonnageText.Text == HangarScreen.TonnageSummary(hangar.DisplayedMech) && hangar.DisplayedMech.Tons == stock.Tonnage - EquipmentRules.Tons(weapon.Kind)
            && StorageCount(hangar, weapon.Name) == stored + 1, "removal lowers the tonnage and shows the item in storage");
        Check(!hangar.ApplyButton.Disabled && !hangar.ResetButton.Disabled && hangar.RosterButtons[index].Text.EndsWith(" •", StringComparison.Ordinal)
            && mech.Spec == stock, "pending changes enable Apply and Reset, mark the mech button and leave the owned mech alone");

        // Pending changes stay with their mech while another is selected.
        var other = (index + 1) % roster.Mechs.Count;
        await Click(hangar.RosterButtons[other]);
        Check(hangar.ApplyButton.Disabled && hangar.ResetButton.Disabled && hangar.HasPendingChanges, "another mech's buttons follow its own pending state");
        await Click(hangar.RosterButtons[index]);
        Check(hangar.SelectedHasPendingChanges && hangar.DisplayedMech.Spec.Weapons.All(item => item.Id != weapon.Id), "returning to a mech keeps its pending changes");
        await Click(hangar.ResetButton);
        Check(!hangar.HasPendingChanges && hangar.ApplyButton.Disabled && StorageCount(hangar, weapon.Name) == stored
            && hangar.LocationPanels.Values.Any(item => item.Blocks.Any(b => b.ItemId == weapon.Id)), "Reset restores the stock loadout and storage");

        // A simulated deployment uses the pending loadout and keeps it pending.
        hangar.Remove(weapon.Id); await Frames();
        await Click(hangar.DeploymentButton);
        Check(_app.Navigation.CurrentScreen == CommandScreen.Tactical && _app.Tactical.Roster.Mechs[index].Removed.Contains(weapon.Id)
            && !ReferenceEquals(_app.Tactical.Roster, roster), "a simulated deployment fields the pending loadout without warning");
        await Click(_app.Tactical.BackButton);
        Check(_app.Navigation.CurrentScreen == CommandScreen.Hangar && hangar.SelectedHasPendingChanges, "pending changes survive a simulated deployment");

        // Apply commits to the company.
        await Click(hangar.ApplyButton);
        Check(mech.Removed.Contains(weapon.Id) && !hangar.HasPendingChanges && hangar.ApplyButton.Disabled
            && roster.Storage.Single(item => item.Id == StorageCatalog.WeaponId(weapon.Kind)).Count == stored + 1 && _app.HasUnsavedChanges,
            "Apply commits the loadout and storage to the company");

        // Removing every bin a weapon uses marks it ⚠ and Apply asks first.
        var gun = stock.Weapons.First(item => item.AmmoKind is not null);
        foreach (var bin in stock.Ammo.Where(item => item.AmmoKind == gun.AmmoKind)) hangar.Remove(bin.Id);
        await Frames();
        var (gunPanel, gunBlock) = FindBlock(hangar, item => item.ItemId == gun.Id);
        Check(gunPanel.Suspect.Contains(gun.Id) && gunPanel._GetTooltip(gunPanel.BlockRect(gunBlock).GetCenter()).Contains("No compatible ammunition"),
            "a weapon without ammunition is marked ⚠");
        await Capture("hangar-loadout-suspect");
        await Click(hangar.ApplyButton);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Apply loadout?", "applying a loadout with ⚠ items asks first");
        await Click(DialogButton("Cancel"));
        Check(!_app.Dialog.Visible && hangar.SelectedHasPendingChanges, "cancelling keeps the changes pending");
        await Click(hangar.ApplyButton); await Click(DialogButton("Apply anyway"));
        Check(!hangar.HasPendingChanges && mech.Spec.Ammo.All(item => item.AmmoKind != gun.AmmoKind), "Apply anyway commits the loadout");

        // Leaving with pending changes warns; staying keeps them, abandoning discards them.
        hangar.Remove(gun.Id); await Frames();
        await Click(hangar.BackButton);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Unapplied changes" && hangar.Visible, "leaving with pending changes warns");
        await Click(DialogButton("Stay in hangar"));
        Check(_app.Navigation.CurrentScreen == CommandScreen.Hangar && hangar.SelectedHasPendingChanges, "staying keeps the pending changes");
        await PressKey(Key.Escape);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Unapplied changes", "Escape warns too");
        await Click(DialogButton("Abandon changes and leave"));
        Check(_app.Office.Visible && !hangar.HasPendingChanges && mech.Spec.Weapons.Any(item => item.Id == gun.Id), "abandoning leaves without changing the mech");
        await Click(_app.Office.HangarButton);
    }

    /// <summary>Starts dragging a storage line with the mouse, leaving the button held over the given point.</summary>
    private async Task<HangarStorageLine> StartStorageDrag(HangarScreen hangar, string storageId, Vector2 over)
    {
        var line = hangar.StorageList.GetChildren().OfType<HangarStorageLine>().Single(item => item.Item.Id == storageId);
        Node scroll = line;
        while (scroll is not ScrollContainer) scroll = scroll.GetParent();
        ((ScrollContainer)scroll).EnsureControlVisible(line); await Frames();
        var from = line.GetGlobalRect().GetCenter();
        MouseMotion(from, Vector2.Zero); MouseButton(from, Godot.MouseButton.Left, true); await Frames();
        MouseMotion(from + new Vector2(20, 0), new Vector2(20, 0)); await Frames();
        MouseMotion(over, over - from - new Vector2(20, 0)); await Frames();
        return line;
    }

    /// <summary>Dragging spares from storage onto location panels, drop feedback, cancelling, and the over-tonnage warning.</summary>
    private async Task TestHangarInstall()
    {
        var hangar = _app.Hangar; var roster = _app.State.Roster!;
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        var index = roster.Mechs.ToList().FindIndex(mech => mech.Removed.IsEmpty && mech.Spec.Weapons.Any(item => EquipmentRules.Slots(item.Kind) <= EquipmentRules.Capacity(item.Location)
            && Loadout.FreeMounts(Loadout.Strip(mech.Spec, new HashSet<string> { item.Id }), item.Location, WeaponMounts.ClassOf(item.Kind)) > 0));
        hangar.Select(index); await Frames();
        var stock = hangar.SelectedMech.Spec;
        var weapon = stock.Weapons.First(item => EquipmentRules.Slots(item.Kind) <= EquipmentRules.Capacity(item.Location)
            && Loadout.FreeMounts(Loadout.Strip(stock, new HashSet<string> { item.Id }), item.Location, WeaponMounts.ClassOf(item.Kind)) > 0);
        var storageId = StorageCatalog.WeaponId(weapon.Kind);
        hangar.Remove(weapon.Id); await Frames();
        var stored = StorageCount(hangar, weapon.Name);
        var target = hangar.LocationPanels[weapon.Location];

        // Escape cancels the drag without leaving the hangar; hidden panels reappear while dragging.
        await PressKey(Key.Space);
        Check(!hangar.Board.Visible, "Space hides the panels before the drag");
        await StartStorageDrag(hangar, storageId, hangar.Board.GetGlobalRect().GetCenter());
        Check(GetViewport().GuiIsDragging() && hangar.Board.Visible && target.DragItem == storageId && target.DropProblem is null
            && hangar.LocationPanels.Values.Any(panel => panel.DropProblem is not null), "dragging a spare shows the panels, marking where it can go and why not");
        await Capture("hangar-install-drag");
        await PressKey(Key.Escape);
        MouseButton(target.GetGlobalRect().GetCenter(), Godot.MouseButton.Left, false); await Frames();
        Check(!GetViewport().GuiIsDragging() && _app.Navigation.CurrentScreen == CommandScreen.Hangar && !_app.Dialog.Visible
            && StorageCount(hangar, weapon.Name) == stored && target.DragItem is null, "Escape cancels the drag and stays in the hangar");

        // Dropping on a panel installs the spare in its first empty slots.
        await StartStorageDrag(hangar, storageId, target.GetGlobalRect().GetCenter());
        MouseButton(target.GetGlobalRect().GetCenter(), Godot.MouseButton.Left, false); await Frames();
        var installed = hangar.SelectedPlan.Installed.SingleOrDefault();
        Check(installed is not null && installed.StorageId == storageId && installed.Location == weapon.Location
            && target.Blocks.Last(block => block.Kind != SlotKind.Empty).ItemId == installed.Id && StorageCount(hangar, weapon.Name) == stored - 1
            && hangar.SelectedHasPendingChanges, "dropping a spare on a panel installs it as a pending change");

        // Taking a pending spare out returns it to storage.
        var (panel, block) = FindBlock(hangar, item => item.ItemId == installed!.Id);
        await RightClick(BlockCentre(panel, block));
        Check(hangar.SelectedPlan.Installed.IsEmpty && StorageCount(hangar, weapon.Name) == stored, "right-clicking a pending spare returns it to storage");

        // The freed mount is labelled on an empty slot; a bin dropped on it covers that mount.
        var mountSlot = target.Blocks.First(item => target.MountAt(item) is not null);
        var type = target.MountAt(mountSlot)!.Value;
        Check(target.MountAt(target.Blocks.First(item => item.Kind == SlotKind.Empty)) == target.Mounts.Free[0]
            && target._GetTooltip(target.BlockRect(mountSlot).GetCenter()).StartsWith($"{type} mount\nFree mounts here: ", StringComparison.Ordinal),
            "empty slots are labelled with the location's free weapon mounts");
        var cover = roster.Storage.Select(item => item.Id).First(id => id.StartsWith("ammo:", StringComparison.Ordinal));
        // Rendered runs place drops at the real cursor rather than the injected one, so drop straight onto the slot.
        target._DropData(target.BlockRect(mountSlot).GetCenter(), cover); await Frames();
        var covering = hangar.SelectedPlan.Installed.SingleOrDefault();
        Check(covering is not null && covering.Mount == type && target.Mounts.Covered[covering.Id].Contains(type)
            && target._GetTooltip(target.BlockRect(target.Blocks.Single(item => item.ItemId == covering.Id)).GetCenter()).Contains($"Covering a"),
            "a bin dropped on a mount slot covers that mount");
        await Capture("hangar-covered-mount");
        await Click(hangar.ResetButton);

        // Going over tonnage warns and disables Apply until something comes out.
        var bin = roster.Storage.Select(item => item.Id).First(id => id.StartsWith("ammo:", StringComparison.Ordinal));
        var room = hangar.LocationPanels.First(pair => hangar.InstallProblem(bin, pair.Key) is null).Key;
        Check(!hangar.OverTonnageText.Visible && hangar.Install(bin, room), "a bin installs in a location with room");
        await Frames();
        Check(hangar.OverTonnageText.Visible && hangar.SelectedOverTonnage && hangar.ApplyButton.Disabled && !hangar.ResetButton.Disabled
            && hangar.ApplyButton.TooltipText == "Over tonnage by 1 t", "an overweight mech shows Over Tonnage and cannot apply");
        await Capture("hangar-over-tonnage");
        await Click(hangar.ResetButton);
        Check(!hangar.OverTonnageText.Visible && !hangar.HasPendingChanges, "Reset clears the over-tonnage warning");
    }

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
        var lines = StorageTexts(hangar).ToArray();
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
        await RightClick(BoardBackground(hangar));
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
