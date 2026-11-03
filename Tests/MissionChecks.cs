using System.Text.Json;

namespace Mech.Tests;

internal static class MissionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var chart = StarChart.Instance;
        var campaign = CampaignChecks.NewCompany();
        var board = campaign.Missions;
        var nearby = chart.WithinJumps(chart.StartingSystemId, MissionTuning.LocationJumps);
        check(board.Current is null && board.Available.Count == MissionTuning.BoardSize && board.HasUnseenOffers
            && board.Available.All(item => item.Posted == campaign.Date), "new company has a full board of unseen offers posted today");
        check(board.Available.All(item => nearby.Contains(item.SystemId)) && nearby.Contains(chart.StartingSystemId),
            "starting offers lie within three jumps of Galatea");
        CheckOffers(board.Available, check);

        var unseen = Campaign.Restore(campaign.Snapshot());
        check(unseen.Missions.HasUnseenOffers && Json(unseen) == Json(campaign), "offers and unseen state survive save restoration");
        board.MarkOffersSeen();
        check(!board.HasUnseenOffers && board.Current is null, "opening offer list marks offers seen without accepting");

        // Every day the oldest offer may go, and the board is refilled to ten.
        var removals = 0;
        for (var day = 0; day < 200; day++)
        {
            var before = board.Available.ToArray();
            campaign.AdvanceOneDay();
            var after = board.Available;
            var kept = before.Skip(1).All(item => after.Any(other => other.Id == item.Id));
            var removedOldest = !after.Any(item => item.Id == before[0].Id);
            if (removedOldest) removals++;
            if (after.Count != MissionTuning.BoardSize || !kept || after.Count(item => item.Posted == campaign.Date) != (removedOldest ? 1 : 0)
                || after.Zip(after.Skip(1)).Any(pair => pair.Second.Posted < pair.First.Posted))
            { check(false, $"day {campaign.Date} keeps ten offers, oldest first, replacing at most the oldest"); break; }
            if (campaign.PaymentPending) campaign.PayNow();
        }
        check(removals is > 70 and < 130, $"about half of all days remove an offer ({removals} of 200)");
        check(board.HasUnseenOffers, "replacement offers arrive unseen");

        var twin = CampaignChecks.NewCompany(77); var other = CampaignChecks.NewCompany(77);
        for (var day = 0; day < 40; day++) { twin.AdvanceOneDay(); other.AdvanceOneDay(); }
        check(Json(twin) == Json(other), "the same seed generates the same offers and removals");
        var reloaded = Campaign.Restore(twin.Snapshot());
        for (var day = 0; day < 20; day++) { twin.AdvanceOneDay(); reloaded.AdvanceOneDay(); }
        check(Json(twin) == Json(reloaded), "a restored save repeats the rolls it would have made");

        // Acceptance takes the offer off the board until the next day, and fixes the deadline.
        var cash = campaign.Cash; var date = campaign.Date;
        var offer = board.Available[3];
        var deadlineDays = campaign.DeadlineDays(offer);
        campaign.AcceptMission(offer.Id);
        check(board.Current == offer && board.Available.Count == MissionTuning.BoardSize - 1 && !board.Available.Contains(offer),
            "accepted offer moves to current mission and leaves a gap on the board");
        check(deadlineDays == campaign.TravelDaysTo(offer.SystemId) + MissionTuning.DeadlineSlackDays && board.Deadline == date.AddDays(deadlineDays!.Value),
            "deadline is travel time plus fourteen days after acceptance");
        check(campaign.Cash == cash && campaign.Date == date && campaign.Snapshot().Events.Length == 0,
            "acceptance does not charge cash, advance time, or schedule an event");
        Reject(() => campaign.AcceptMission(board.Available[0].Id), check, "cannot accept while a mission is active");
        Reject(() => board.Accept(offer.Id, date), check, "the accepted mission is no longer on the board");
        var restored = Campaign.Restore(campaign.Snapshot());
        check(restored.Missions.Current?.Id == offer.Id && restored.Missions.Deadline == board.Deadline, "current mission and deadline survive restoration");
        campaign.AdvanceOneDay();
        check(board.Available.Count == MissionTuning.BoardSize && board.Current == offer, "the next day refills the board");
        for (var day = 0; day < 30; day++) campaign.AdvanceOneDay();
        check(board.Current == offer && board.Available.All(item => item.Id != offer.Id), "the daily roll never removes the accepted mission");
        board.Abandon();
        check(board.Current is null && board.Deadline is null && board.Find(offer.Id) is null && board.Available.Count == MissionTuning.BoardSize,
            "an abandoned mission is gone for good");
        check(campaign.Cash == cash, "abandonment has no financial effects");

        // Offers posted in transit lie around the destination of the current jump.
        var traveller = CampaignChecks.NewCompany();
        var target = chart.WithinJumps(chart.StartingSystemId, 1).First(id => id != chart.StartingSystemId && chart.JumpDays(chart.StartingSystemId, id) >= 3);
        traveller.PlotJourney(target); traveller.Embark();
        traveller.AcceptMission(traveller.Missions.Available[0].Id);
        traveller.AdvanceOneDay();
        var posted = traveller.Missions.Available.Where(item => item.Posted == traveller.Date).ToArray();
        check(!traveller.Travel.InOrbit && traveller.MissionOrigin == target && posted.Length >= 1
            && posted.All(item => chart.WithinJumps(target, MissionTuning.LocationJumps).Contains(item.SystemId)),
            "offers generated in transit are near the system being jumped to");
        check(traveller.TravelDaysTo(target) == traveller.Travel.DaysRemaining, "travel estimates finish the current jump first");

        var good = campaign.Snapshot().Missions!;
        var first = good.Offers[0];
        Reject(() => MissionBoard.Restore(good with { Offers = [.. good.Offers, first with { Id = "offer-9999" }] }, campaign.Date), check, "more than ten offers are rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { SystemId = "nowhere" }, .. good.Offers[1..]] }, campaign.Date), check, "unknown locations are rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Opposition = [new("missing", EnemyPilotRank.Regular), .. first.Opposition[1..]] }, .. good.Offers[1..]] }, campaign.Date),
            check, "unknown enemy mechs are rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Opposition = first.Opposition[..1] }, .. good.Offers[1..]] }, campaign.Date), check, "undersized opposition is rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Posted = campaign.Date.AddDays(1) }, .. good.Offers[1..]] }, campaign.Date), check, "future offers are rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Id = good.Offers[1].Id }, .. good.Offers[1..]] }, campaign.Date), check, "duplicate offer IDs are rejected");
        Reject(() => MissionBoard.Restore(good with { NextOfferNumber = 1 }, campaign.Date), check, "offer IDs must precede the next offer number");
        Reject(() => MissionBoard.Restore(good with { Deadline = campaign.Date }, campaign.Date), check, "a deadline needs a current mission");
        Reject(() => MissionBoard.Restore(good with { SeenMissionIds = ["missing"] }, campaign.Date), check, "unknown seen IDs are rejected");
        Reject(() => MissionBoard.Restore(good with { SeenMissionIds = [first.Id, first.Id] }, campaign.Date), check, "duplicate seen IDs are rejected");
        Reject(() => MissionBoard.Restore(good with { SeenMissionIds = null! }, campaign.Date), check, "null seen list is rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = null! }, campaign.Date), check, "null offer list is rejected");
        Reject(() => Campaign.Restore(campaign.Snapshot() with { Missions = null }), check, "current saves require mission state");

        foreach (var version in new[] { 1, 2, 5 })
        {
            var legacy = version == 5 ? campaign.Snapshot() with { Version = 5, Missions = new(null!, null, null, null!, 0, 0) }
                : CampaignChecks.Legacy(campaign.Snapshot(), version) with { Missions = null };
            if (version == 1) legacy = legacy with { Roster = null };
            var migrated = Campaign.Restore(legacy);
            check(migrated.Missions.Current is null && migrated.Missions.HasUnseenOffers && migrated.Missions.Available.Count == MissionTuning.BoardSize
                && migrated.Cash == campaign.Cash, $"version {version} saves receive a fresh unseen board without changing finances");
        }

        campaign.AcceptMission(board.Available[0].Id);
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mech-missions-{Guid.NewGuid():N}.json");
        try
        {
            var store = new CampaignStore(path); store.Save(campaign);
            var loaded = store.Load();
            check(Json(loaded) == Json(campaign), "disk round trip preserves offers, selection, deadline and browsing history");
            loaded.Missions.Abandon(); store.Save(loaded);
            check(store.Load().Missions.Current is null, "disk round trip preserves abandonment");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    private static void CheckOffers(IEnumerable<MissionOffer> offers, Action<bool, string> check)
    {
        foreach (var offer in offers)
        {
            var low = MissionGenerator.Difficulty(offer.Opposition) * MissionTuning.CBillsPerDifficulty * (1 - MissionTuning.RewardVariation);
            var high = MissionGenerator.Difficulty(offer.Opposition) * MissionTuning.CBillsPerDifficulty * (1 + MissionTuning.RewardVariation);
            var valid = offer.Opposition.Length is >= MissionTuning.MinOpposition and <= MissionTuning.MaxOpposition
                && offer.Objective == MissionTuning.Objective && !string.IsNullOrWhiteSpace(offer.Briefing)
                && offer.Payment >= low * MissionTuning.MinPaymentShare - MissionTuning.RewardRounding
                && offer.Payment + offer.InsuranceLimit <= high + 2 * MissionTuning.RewardRounding
                && offer.SalvagePicks is >= 0 and <= MissionTuning.MaxSalvagePicks && offer.InsuranceLimit >= 0
                && offer.Payment % MissionTuning.RewardRounding == 0 && offer.InsuranceLimit % MissionTuning.RewardRounding == 0;
            if (!valid) { check(false, $"{offer.Name} has a 2–4 mech opposition and rewards scaled to its difficulty"); return; }
        }
        check(true, "offers have a 2–4 mech opposition and rewards scaled to their difficulty");
    }

    private static string Json(Campaign campaign) => JsonSerializer.Serialize(campaign.Snapshot());

    private static void Reject(Action action, Action<bool, string> check, string message)
    {
        var rejected = false;
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        check(rejected, message);
    }
}
