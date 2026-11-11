namespace Mech.Tests;

internal static class CampaignChecks
{
    public static void Run(Action<bool, string> check)
    {
        var campaign = NewCompany();
        check(campaign.Date == new DateOnly(3025, 1, 1) && campaign.Cash == 2_500_000, "new company defaults");
        check(campaign.DaysUntilQuarter == 90 && campaign.NextEventDate == new DateOnly(3025, 4, 1), "quarter guarantees the next event");
        campaign.Schedule(new("arrival", new DateOnly(3025, 1, 3), CampaignEventKind.Arrival, "Arrival"));
        check(campaign.NextEventDate == new DateOnly(3025, 1, 3), "earlier scheduled event takes precedence");
        check(campaign.AdvanceOneDay().Events.Count == 0, "no early event processing");
        check(campaign.AdvanceOneDay().Events.Single().Id == "arrival", "event delivered on its date");
        check(campaign.NextEventDate == new DateOnly(3025, 4, 1), "completed event removed");
        Throws(() => campaign.Schedule(new("past", campaign.Date, CampaignEventKind.Arrival, "Past")), check, "reject past events");
        campaign.Schedule(new("mission", new DateOnly(3025, 4, 1), CampaignEventKind.MissionResult, "Mission result"));
        AdvanceTo(campaign, new DateOnly(3025, 3, 31));
        var quarter = campaign.AdvanceOneDay();
        check(quarter.QuarterReached && quarter.Events.Count == 0 && campaign.PaymentDue == 100_000, "quarter blocks same-day settlement");
        check(!campaign.AdvanceOneDay().Advanced && campaign.Date == new DateOnly(3025, 4, 1), "payment blocks every time control");
        check(campaign.Cash == 2_500_000 && campaign.CanPay && campaign.CanDefer, "bill does not collect automatically");
        var pending = Campaign.Restore(campaign.Snapshot());
        check(pending.PaymentPending && pending.PayNow().Single().Id == "mission", "save restores blocked same-day events");
        check(pending.Cash == 2_400_000 && !pending.PaymentPending && pending.DeferredDebt == 0, "full payment clears obligation");
        check(pending.AdvanceOneDay().Events.Count == 0, "settlement delivered only once");
        check(campaign.DeferPayment().Single().Id == "mission", "deferral releases same-day commitments");
        check(campaign.Cash == 2_500_000 && campaign.DeferredDebt == 100_000 && campaign.CanAdvance, "deferral preserves cash and unlocks time");
        check(campaign.DaysUntilQuarter == 91, "countdown rolls to following quarter");
        AdvanceTo(campaign, new DateOnly(3025, 7, 1));
        check(campaign.PaymentDue == 200_000 && !campaign.CanDefer && !campaign.Bankrupt, "next quarter requires both full bills");
        Throws(() => campaign.DeferPayment(), check, "cannot defer a second quarter");
        campaign.PayNow();
        check(campaign.Cash == 2_300_000 && campaign.DeferredDebt == 0, "payment clears old debt and new bill");
        foreach (var due in new[] { new DateOnly(3025, 10, 1), new DateOnly(3026, 1, 1) })
        {
            AdvanceTo(campaign, due);
            check(campaign.PaymentDue == 100_000 && campaign.PaymentPending, $"recurring boundary {due}");
            campaign.PayNow();
        }

        var buyer = NewCompany(); var buyerMech = buyer.Roster!.Mechs.First(item => item.Spec.Ammo.Length > 0); var buyerBin = buyerMech.Spec.Ammo[0];
        var swap = buyerMech.Plan with { Removed = buyerMech.Removed.Add(buyerBin.Id), Installed = [new(Loadout.NextId([]), StorageCatalog.AmmoId(buyerBin.AmmoKind), buyerBin.Location)] };
        var broke = Campaign.Restore(buyer.Snapshot() with { Cash = 0 }); var brokeStorage = broke.Roster!.Storage.ToArray();
        Throws(() => broke.ApplyLoadout(buyerMech.Id, swap, [StorageCatalog.AmmoId(buyerBin.AmmoKind)]), check, "reject an unaffordable purchase");
        check(broke.Roster.Storage.SequenceEqual(brokeStorage) && broke.Roster.Mechs.First(item => item.Id == buyerMech.Id).Installed.IsEmpty,
            "a refused purchase changes nothing");
        var binStored = buyer.Roster.Storage.Single(item => item.Id == StorageCatalog.AmmoId(buyerBin.AmmoKind)).Count;
        buyer.ApplyLoadout(buyerMech.Id, swap, [StorageCatalog.AmmoId(buyerBin.AmmoKind)]);
        check(buyer.Cash == Campaign.StartingCash - EquipmentMarket.NominalPrice && buyerMech.Installed.Length == 1
            && buyer.Roster.Storage.Single(item => item.Id == StorageCatalog.AmmoId(buyerBin.AmmoKind)).Count == binStored + 1,
            "applying a loadout pays for its purchases, which install without drawing on storage");
        var insolvent = Campaign.Restore(NewCompany().Snapshot() with { Cash = 99_999 });
        AdvanceTo(insolvent, new DateOnly(3025, 4, 1));
        check(!insolvent.CanPay && insolvent.CanDefer && !insolvent.Bankrupt, "first shortfall permits grace");
        Throws(() => insolvent.PayNow(), check, "reject partial payment");
        check(insolvent.Cash == 99_999, "failed payment leaves cash intact");
        insolvent.DeferPayment();
        AdvanceTo(insolvent, new DateOnly(3025, 7, 1));
        check(insolvent.Bankrupt && !insolvent.CanAdvance && !insolvent.CanPay, "insufficient cash at grace deadline causes bankruptcy");
        check(Campaign.Restore(insolvent.Snapshot()).Bankrupt, "bankruptcy survives restoration");

        var exact = Campaign.Restore(NewCompany().Snapshot() with { Cash = 200_000 });
        AdvanceTo(exact, new DateOnly(3025, 4, 1));
        exact.DeferPayment();
        AdvanceTo(exact, new DateOnly(3025, 7, 1));
        check(!exact.Bankrupt && exact.CanPay, "exact funds avoid bankruptcy");
        exact.PayNow();
        check(exact.Cash == 0, "exact full payment is accepted");

        var leap = Campaign.Restore(NewCompany().Snapshot() with { Date = new DateOnly(3028, 2, 28), NextQuarter = new DateOnly(3028, 4, 1) });
        leap.AdvanceOneDay();
        check(leap.Date == new DateOnly(3028, 2, 29), "calendar includes leap day");
        leap.AdvanceOneDay();
        check(leap.Date == new DateOnly(3028, 3, 1), "calendar crosses leap February");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { Version = 99 }), check, "reject incompatible save versions");
        var worn = NewCompany().Snapshot();
        worn = worn with { Roster = worn.Roster! with { Mechs = worn.Roster.Mechs.Select((mech, i) => i > 0 ? mech : mech with
            { Locations = mech.Locations.Select((loc, j) => j > 0 ? loc : loc with { Armor = 30, Structure = 10 }).ToArray() }).ToArray() } };
        var migrated = Campaign.Restore(Legacy(worn, 4)).Roster!.Snapshot();
        check(worn.Version == Campaign.SaveVersion && migrated.Mechs.Zip(worn.Roster!.Mechs).All(pair => pair.First.Locations.SequenceEqual(pair.Second.Locations)),
            "version 4 saves count armor and structure in tenths of a point after loading");
        Throws(() => Campaign.Restore(worn with { Version = 4 }), check, "a version 4 save cannot hold more than stock protection once rescaled");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { Cash = -1 }), check, "reject corrupt finance state");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { NextQuarter = campaign.Date }), check, "reject invalid payment recurrence");

        TestStorage(check); TestSaveLibrary(check);
        TestLances(check);
    }

    /// <summary>A two-player company: the draft's first two mechs in the leader's lance, the last two in the second player's.</summary>
    internal static Campaign TwoPlayerCompany()
    {
        var save = NewCompany(7).Snapshot();
        var roster = save.Roster!;
        return Campaign.Restore(save with { Roster = roster with { Lances = 2, Mechs = roster.Mechs.Select((mech, i) => mech with { Lance = i < 2 ? 0 : 1 }).ToArray() },
            Missions = MissionBoard.Create(7, save.Date, StarChart.Instance.StartingSystemId, new Reputation(), lances: 2).Snapshot() });
    }

    private static void TestLances(Action<bool, string> check)
    {
        var solo = NewCompany();
        check(solo.Players == 1 && solo.Roster!.LanceCount == 1 && solo.Roster.Mechs.All(mech => mech.Lance == 0),
            "a single-player company forms one lance from its four drafted mechs");
        Throws(() => solo.Execute(new AdvanceDayCommand(), 1), check, "a single-player campaign has no second player slot");
        check(solo.Execute(new AdvanceDayCommand()).Advanced, "the leader advances time through a command");

        var campaign = TwoPlayerCompany();
        var roster = campaign.Roster!;
        var (leaderMech, playerMech) = (roster.Mechs[0], roster.Mechs[2]);
        check(campaign.Players == 2 && roster.LanceMechs(0).Count() == 2 && roster.LanceMechs(1).Count() == 2, "a two-player company has two lances");
        var offer = campaign.Missions.Available.First(item => campaign.DeadlineDays(item) is not null);
        Throws(() => campaign.Execute(new AcceptMissionCommand(offer.Id), 1), check, "only the leader accepts missions");
        Throws(() => campaign.Execute(new AdvanceDayCommand(), 1), check, "only the leader advances time");
        Throws(() => campaign.Execute(new PlotJourneyCommand(campaign.Travel.OrbitSystemId), 1), check, "only the leader plots travel");
        var date = campaign.Date;
        campaign.Execute(new AcceptMissionCommand(offer.Id), Campaign.LeaderSlot);
        check(campaign.Missions.Current?.Id == offer.Id && campaign.Date == date, "the leader accepts a mission");

        Throws(() => campaign.Execute(new SetLanceCommand(leaderMech.Id, null), 1), check, "a player cannot move another player's mech to the lance pool");
        Throws(() => campaign.Execute(new ApplyLoadoutCommand(leaderMech.Id, leaderMech.Plan, []), 1), check, "a player cannot refit another player's mech");
        Throws(() => campaign.Execute(new AssignWarriorCommand(playerMech.Id, leaderMech.WarriorId, true), 1), check,
            "a player cannot take a MechWarrior from another player's mech");
        campaign.Execute(new SetLanceCommand(playerMech.Id, null), 1);
        check(playerMech.Lance is null && roster.Pool.Single() == playerMech, "a player moves their own mech to the lance pool, MechWarrior and all");
        Throws(() => campaign.Execute(new SetLanceCommand(playerMech.Id, Campaign.LeaderSlot), 1), check, "a player only adds mechs to their own lance");
        campaign.Execute(new AssignWarriorCommand(playerMech.Id, null), Campaign.LeaderSlot);
        check(playerMech.WarriorId is null, "any player acts on lance pool mechs");
        campaign.Execute(new SetLanceCommand(playerMech.Id, Campaign.LeaderSlot), Campaign.LeaderSlot);
        check(playerMech.Lance == 0 && roster.LanceMechs(0).Count() == 3, "the leader takes a lance pool mech into their lance");
        var restored = Campaign.Restore(campaign.Snapshot());
        check(restored.Players == 2 && restored.Roster!.Mechs.Select(mech => mech.Lance).SequenceEqual(roster.Mechs.Select(mech => mech.Lance)),
            "lances survive a save round trip");
    }

    /// <summary>The snapshot as an older version would have saved it: whole stock points of armor and structure.</summary>
    internal static CampaignSnapshot Legacy(CampaignSnapshot save, int version) => save with
    {
        Version = version,
        Roster = save.Roster is not { } roster ? null : roster with { Mechs = roster.Mechs.Select(mech => mech with
        {
            Locations = mech.Locations.Select(loc => loc with { Armor = loc.Armor / CombatRules.DamageScale,
                RearArmor = loc.RearArmor / CombatRules.DamageScale, Structure = loc.Structure / CombatRules.DamageScale }).ToArray()
        }).ToArray() }
    };

    internal static Campaign NewCompany(ulong? missionSeed = null)
    {
        var campaign = new Campaign(missionSeed);
        var draft = new InitialDraft { ForceName = "Test Company" };
        for (var pool = 0; pool < 8; pool++) draft.Select(pool, 0);
        campaign.EstablishCompany(draft);
        return campaign;
    }

    private static void TestStorage(Action<bool, string> check)
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mech-campaign-{Guid.NewGuid():N}");
        var path = System.IO.Path.Combine(directory, "campaign.json");
        try
        {
            var store = new CampaignStore(path);
            var campaign = NewCompany();
            campaign.AdvanceOneDay();
            check(!store.Exists, "advancing never autosaves");
            campaign.Schedule(new("repair", new DateOnly(3025, 5, 1), CampaignEventKind.RepairCompletion, "Repair complete"));
            AdvanceTo(campaign, new DateOnly(3025, 4, 1));
            store.Save(campaign);
            var loaded = new CampaignStore(path).Load();
            check(loaded.Date == campaign.Date && loaded.Cash == campaign.Cash && loaded.PaymentPending, "disk round trip restores pending payment");
            loaded.DeferPayment();
            store.Save(loaded);
            var deferred = new CampaignStore(path).Load();
            check(deferred.DeferredDebt == 100_000 && deferred.CanAdvance && deferred.NextEventDate == new DateOnly(3025, 5, 1), "disk round trip restores debt and future events");
            check(System.IO.Directory.GetFiles(directory).Length == 1, "atomic saves leave a single campaign file");
            System.IO.File.WriteAllText(path, "{broken");
            var rejected = false;
            try { store.Load(); }
            catch (System.Text.Json.JsonException) { rejected = true; }
            check(rejected, "corrupt JSON is rejected");
            check(deferred.DeferredDebt == 100_000, "failed load cannot mutate the live campaign");
        }
        finally
        {
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        }
    }

    private static void TestSaveLibrary(Action<bool, string> check)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mech-saves-{Guid.NewGuid():N}");
        try
        {
            var solo = new SaveLibrary(System.IO.Path.Combine(root, "solo"), coop: false);
            var coop = new SaveLibrary(System.IO.Path.Combine(root, "coop"), coop: true);
            check(solo.List().Count == 0 && !solo.Any(), "a missing save folder lists no saves");
            var campaign = NewCompany();
            var first = solo.Save(campaign, null);
            campaign.AdvanceOneDay();
            var second = solo.Save(campaign, "  Second  ");
            var slots = solo.List();
            check(slots.Select(slot => slot.Id).SequenceEqual([second, first]), "saves list the most recently written first");
            check(slots[1].Info!.Name == campaign.Roster!.ForceName && slots[0].Info!.Name == "Second" && slots[0].Info!.Date == campaign.Date
                && slots[0].Info!.Cash == campaign.Cash && slots[0].Info!.Players is null, "a slot records its name (defaulting to the company), date and balance");
            check(solo.Save(campaign, "First again", first) == first && solo.List() is [{ Info.Name: "First again" } top, _] && top.Id == first,
                "overwriting keeps the slot and moves it to the top");
            check(solo.Load(second).Date == campaign.Date, "a slot loads its campaign");
            check(solo.Save(campaign, new string('x', 60)) is var longId && solo.Info(longId).Name.Length == CampaignStore.MaxNameLength, "long names are cut to the limit");
            Throws(() => coop.Save(campaign, null), check, "a solo campaign cannot be saved among co-op saves");
            System.IO.Directory.CreateDirectory(coop.Directory);
            System.IO.File.Copy(solo.PathOf(second), coop.PathOf("misplaced"));
            Throws(() => coop.Load("misplaced"), check, "a co-op library refuses a solo save");
            Throws(() => solo.PathOf("../escape"), check, "slot ids cannot leave the folder");
            System.IO.File.WriteAllText(solo.PathOf("broken"), "{broken");
            check(solo.List() is { Count: 4 } all && all[^1] is { Id: "broken", Info: null }, "an unreadable save is listed last, without information");

            var legacyPath = System.IO.Path.Combine(root, "campaign.json");
            System.IO.File.WriteAllText(legacyPath, System.Text.Json.JsonSerializer.Serialize(campaign.Snapshot(), CampaignStore.JsonOptions));
            var written = System.IO.File.GetLastWriteTimeUtc(legacyPath);
            var fresh = new SaveLibrary(System.IO.Path.Combine(root, "fresh-solo"), coop: false);
            var freshCoop = new SaveLibrary(System.IO.Path.Combine(root, "fresh-coop"), coop: true);
            SaveLibrary.ImportLegacy(legacyPath, fresh, freshCoop);
            check(!System.IO.File.Exists(legacyPath) && fresh.List() is [{ Info: { } legacy } imported] && legacy.Name == campaign.Roster.ForceName
                && legacy.SavedAt == written && fresh.Load(imported.Id).Date == campaign.Date && freshCoop.List().Count == 0,
                "a save from before slots moves into the solo folder, keeping its time");
        }
        finally
        {
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
        }
    }

    private static void AdvanceTo(Campaign campaign, DateOnly date)
    {
        while (campaign.Date < date)
            if (!campaign.AdvanceOneDay().Advanced) throw new InvalidOperationException("Unexpected time blocker in test.");
    }

    private static void Throws(Action action, Action<bool, string> check, string message)
    {
        var rejected = false;
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        check(rejected, message);
    }
}
