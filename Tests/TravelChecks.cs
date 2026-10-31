namespace Mech.Tests;

internal static class TravelChecks
{
    public static void Run(Action<bool, string> check)
    {
        StarSystem System(string id, double x, double y = 0) => new(id, id, x, y, [x, y, 1, 1]);
        var chart = new StarChart([System("a", 0), System("b", 25), System("c", 50),
            System("detour", 25, 10), System("island", 1000)], 1);
        check(chart.Plot("a", "c")!.SequenceEqual(new[] { "a", "b", "c" }), "equal-hop routes prefer shortest distance");
        check(chart.Plot("a", "island") is null && chart.Plot("a", "a") is null, "unreachable and current systems cannot be embarked");
        check(chart.JumpDays("a", "b") == 5, "fractional jump duration rounds up");
        var shortChart = new StarChart([System("a", 0), System("tiny", .6), System("max", 30)], 1);
        check(shortChart.JumpDays("a", "tiny") == 1 && shortChart.JumpDays("a", "max") == 5, "atomic days and thirty-light-year duration");
        var fewer = new StarChart([System("a", 0), System("b", 25, 16), System("c", 50), System("d", 17), System("e", 34)], 1);
        check(fewer.Plot("a", "c")!.SequenceEqual(new[] { "a", "b", "c" }), "fewer jumps beat shorter total distance");

        var travel = new Travel(chart, "a");
        travel.Plot("c"); travel.Embark();
        check(travel.InOrbit && travel.HasJourney && travel.Candidate is null, "embark commits journey while remaining in orbit");
        travel.Plot("detour");
        check(travel.Journey![^1] == "c" && travel.Candidate![^1] == "detour", "candidate does not replace embarked destination");
        travel.Cancel();
        check(!travel.HasJourney && travel.Candidate![^1] == "detour" && travel.InOrbit, "cancellation preserves the candidate and current orbit");
        travel.Embark();
        check(travel.Journey![^1] == "detour", "embark explicitly replaces journey with candidate");
        travel.Plot("c");
        check(travel.AdvanceDay() is null && !travel.InOrbit && travel.Candidate is null && travel.DaysRemaining == 4,
            "departure advances first transit day and clears unembarked candidate");
        Reject(() => travel.Plot("c"), check, "cannot plot in transit");
        Reject(travel.Cancel, check, "cannot cancel in transit");
        Reject(travel.Embark, check, "cannot embark in transit");
        travel = Travel.Restore(travel.Snapshot(), chart);
        check(!travel.InOrbit && travel.DaysRemaining == 4, "mid-jump travel survives restore");
        for (var i = 0; i < 3; i++) check(travel.AdvanceDay() is null, "no early arrival");
        check(travel.AdvanceDay() == "detour" && travel.InOrbit && !travel.HasJourney, "arrival finishes final journey in orbit");

        travel = new Travel(chart, "a"); travel.Plot("c"); travel.Embark();
        for (var i = 0; i < 5; i++) travel.AdvanceDay();
        check(travel.OrbitSystemId == "b" && travel.InOrbit && travel.HasJourney, "intermediate arrival remains in orbit with onward journey");
        travel = Travel.Restore(travel.Snapshot(), chart);
        check(travel.AdvanceDay() is null && !travel.InOrbit && travel.DaysRemaining == 4, "following day automatically continues from intermediate orbit");
        Reject(() => Travel.Restore(travel.Snapshot() with { DaysRemaining = 99 }, chart), check, "reject invalid transit progress");
        Reject(() => Travel.Restore(travel.Snapshot() with { Journey = ["a", "island"] }, chart), check, "reject impossible saved jumps");
        Reject(() => Travel.Restore(travel.Snapshot() with { Candidate = ["b", "c"] }, chart), check, "reject candidate saved in transit");

        TestCampaign(check);
    }

    private static void TestCampaign(Action<bool, string> check)
    {
        var chart = StarChart.Instance;
        check(chart.Systems.Count > 1900 && chart[chart.StartingSystemId].Name == "Galatea", "full reference geography and Galatea start load");
        check(chart.Systems.All(system => system.LabelBounds.Length == 4 && system.X is >= 0 and <= 7000
            && system.Y is >= 0 and <= 6000), "all systems have image coordinates and clickable label bounds");
        var campaign = CampaignChecks.NewCompany();
        var origin = campaign.Travel.OrbitSystemId;
        var neighbor = chart.Systems.First(system => system.Id != origin && chart.Distance(origin, system.Id) is > 6 and <= 30);
        campaign.PlotJourney(neighbor.Id); campaign.Embark();
        var arrivalDate = campaign.Date.AddDays(chart.JumpDays(origin, neighbor.Id));
        check(campaign.NextEventDate == arrivalDate && campaign.Cash == Campaign.StartingCash, "embarking exposes next arrival without upfront charge");
        campaign.AdvanceOneDay();
        check(campaign.AccruedTransitDays == 1 && campaign.AccruedTransitCost == 2_000 && !campaign.Travel.InOrbit, "one daily transit charge accrues without collecting cash");
        campaign = Campaign.Restore(campaign.Snapshot());
        while (campaign.Date < arrivalDate.AddDays(-1)) campaign.AdvanceOneDay();
        check(campaign.AdvanceOneDay().Events.Single().Kind == CampaignEventKind.Arrival && campaign.Travel.InOrbit,
            "arrival is an event on the expected calendar date");
        var costs = campaign.AccruedTransitDays;
        campaign.AdvanceOneDay();
        check(campaign.AccruedTransitDays == costs && !campaign.Travel.HasJourney, "stationary orbit has no transit cost");
        foreach (var version in new[] { 1, 2, 3 })
        {
            var legacy = CampaignChecks.Legacy(CampaignChecks.NewCompany().Snapshot(), version) with { Travel = null };
            if (version == 1) legacy = legacy with { Roster = null, Missions = null };
            if (version == 2) legacy = legacy with { Missions = null };
            var loaded = Campaign.Restore(legacy);
            check(loaded.Travel.InOrbit && loaded.Travel.OrbitSystemId == origin && loaded.AccruedTransitDays == 0,
                $"version {version} migrates to Galatea without travel charges");
        }
        Reject(() => Campaign.Restore(campaign.Snapshot() with { Travel = null }), check, "current save requires travel state");
        Reject(() => Campaign.Restore(campaign.Snapshot() with { AccruedTransitDays = 93 }), check, "reject impossible quarterly transit accrual");

        campaign = Campaign.Restore(CampaignChecks.NewCompany().Snapshot() with
        {
            Date = new DateOnly(3025, 3, 31), AccruedTransitDays = 5,
            Travel = new(origin, null, [origin, neighbor.Id], 0, 1)
        });
        var boundary = campaign.AdvanceOneDay();
        check(boundary.QuarterReached && boundary.Events.Count == 0 && campaign.Travel.InOrbit,
            "billing-date arrival updates orbit while notification waits for payment");
        check(campaign.PaymentDue == 112_000 && campaign.BilledTransitDays == 6 && campaign.AccruedTransitDays == 0,
            "billing includes completed boundary transit day and freezes itemized charge");
        check(!campaign.AdvanceOneDay().Advanced, "payment blocks further transit and charges");
        var billed = Campaign.Restore(campaign.Snapshot());
        check(billed.PayNow().Single().Kind == CampaignEventKind.Arrival && billed.Cash == 2_388_000,
            "payment collects variable bill and releases same-day arrival exactly once");
        check(billed.BilledTransitDays == 0 && billed.AccruedTransitDays == 0 && billed.Snapshot().Events.Length == 0, "paid transit days cannot be billed twice");
        campaign.DeferPayment();
        check(campaign.DeferredDebt == 112_000 && campaign.Cash == Campaign.StartingCash, "deferral carries transit charges without collecting cash");
        campaign = Campaign.Restore(campaign.Snapshot());
        while (campaign.Date < new DateOnly(3025, 7, 1)) campaign.AdvanceOneDay();
        check(campaign.PaymentDue == 212_000 && !campaign.CanDefer, "next quarter combines carried transit bill with new company charge");
        var broke = Campaign.Restore(campaign.Snapshot() with { Cash = 211_999, Bankrupt = true });
        check(broke.Bankrupt && !broke.CanAdvance, "variable carried bill participates in bankruptcy");

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mech-travel-{Guid.NewGuid():N}.json");
        try
        {
            var store = new CampaignStore(path); store.Save(campaign);
            var loaded = store.Load();
            check(loaded.PaymentDue == 212_000 && loaded.DeferredDebt == 112_000 && loaded.Travel.OrbitSystemId == neighbor.Id,
                "disk save round trip preserves location and variable obligations");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    private static void Reject(Action action, Action<bool, string> check, string message)
    {
        var rejected = false;
        try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        check(rejected, message);
    }
}
