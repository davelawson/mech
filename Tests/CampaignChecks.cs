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
        Throws(() => Campaign.Restore(campaign.Snapshot() with { Cash = -1 }), check, "reject corrupt finance state");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { NextQuarter = campaign.Date }), check, "reject invalid payment recurrence");

        TestStorage(check);
    }

    internal static Campaign NewCompany()
    {
        var campaign = new Campaign();
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
