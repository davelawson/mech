namespace Mech.Tests;

internal static class MissionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany();
        var board = campaign.Missions;
        var easy = MissionCatalog.EasyPeasy;
        check(board.Current is null && board.Available.Single() == easy && board.HasUnseenOffers, "new company has one unseen Easy Peasy offer");
        var unseen = Campaign.Restore(campaign.Snapshot());
        check(unseen.Missions.HasUnseenOffers, "unseen offers survive save restoration");
        board.MarkOffersSeen();
        check(!board.HasUnseenOffers && board.Current is null, "opening offer list marks offers seen without accepting");
        var before = campaign.Snapshot();
        board.Accept(easy.Id);
        check(board.Current == easy && !board.Available.Any(), "accepted offer moves to current mission without duplication");
        check(campaign.Cash == before.Cash && campaign.Date == before.Date && campaign.Snapshot().Events.Length == before.Events.Length,
            "acceptance does not charge cash, advance time, or schedule a deadline");
        Reject(() => board.Accept(easy.Id), check, "cannot accept while a mission is active");
        var restored = Campaign.Restore(campaign.Snapshot());
        check(restored.Missions.Current == easy && !restored.Missions.HasUnseenOffers, "selected mission and seen state survive restoration");

        // Simulate a later catalog entry arriving after the player accepted the first mission.
        var second = easy with { Id = "second-offer", Name = "Second Offer" };
        var expanded = MissionBoard.Restore(board.Snapshot(), [easy, second]);
        check(expanded.HasUnseenOffers && expanded.Current == easy && expanded.Available.Single() == second,
            "new offers remain available and notify while a mission is selected");
        Reject(() => expanded.Accept(second.Id), check, "browsing another offer cannot replace an accepted mission");
        expanded.MarkOffersSeen();
        check(!expanded.HasUnseenOffers && expanded.Current == easy, "opening list clears new-offer notification while retaining current mission");
        board.Abandon();
        check(board.Current is null && board.Available.Single() == easy && !board.HasUnseenOffers, "abandonment returns the seen offer to the pool");
        check(campaign.Cash == before.Cash && campaign.Date == before.Date, "abandonment has no financial or calendar effects");
        board.Accept(easy.Id);
        Reject(() => new MissionBoard().Accept("missing"), check, "unknown missions cannot be accepted");
        Reject(() => MissionBoard.Restore(new("missing", [easy.Id])), check, "unknown selected missions are rejected");
        Reject(() => MissionBoard.Restore(new(easy.Id, [])), check, "accepted mission must have been seen");
        Reject(() => MissionBoard.Restore(new(null, ["missing"])), check, "unknown seen IDs are rejected");
        Reject(() => MissionBoard.Restore(new(null, [easy.Id, easy.Id])), check, "duplicate seen IDs are rejected");
        Reject(() => MissionBoard.Restore(new(null, null!)), check, "null seen list is rejected");
        Reject(() => Campaign.Restore(campaign.Snapshot() with { Missions = null }), check, "current saves require mission state");
        foreach (var version in new[] { 1, 2 })
        {
            var legacy = campaign.Snapshot() with { Version = version, Missions = null };
            if (version == 1) legacy = legacy with { Roster = null };
            var migrated = Campaign.Restore(legacy);
            check(migrated.Missions.Current is null && migrated.Missions.HasUnseenOffers && migrated.Cash == campaign.Cash,
                $"version {version} saves receive an unseen offer without changing finances");
        }
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mech-missions-{Guid.NewGuid():N}.json");
        try
        {
            var store = new CampaignStore(path); store.Save(campaign);
            var loaded = store.Load();
            check(loaded.Missions.Current == easy && !loaded.Missions.HasUnseenOffers, "disk round trip preserves mission selection and browsing history");
            loaded.Missions.Abandon(); store.Save(loaded);
            check(store.Load().Missions.Current is null && !store.Load().Missions.HasUnseenOffers, "disk round trip preserves abandonment without new notification");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    private static void Reject(Action action, Action<bool, string> check, string message)
    {
        var rejected = false;
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        check(rejected, message);
    }
}
