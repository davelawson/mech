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
        CheckSides(chart, check);
        CheckSettlement(check);
        CheckLances(check);
        CheckChallenge(check);
        CheckPatrols(chart, check);

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
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Opposition = [] }, .. good.Offers[1..]] }, campaign.Date), check, "an empty opposition is rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Kind = MissionKind.Patrol, PatrolLocations = 3 }, .. good.Offers[1..]] }, campaign.Date),
            check, "a patrol whose opposition is not grouped by leg is rejected");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Kind = 0 }, .. good.Offers[1..]] }, campaign.Date), check, "an offer without a contract kind is rejected");
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
            var objective = offer.Kind switch
            {
                MissionKind.Raid => MissionTuning.RaidObjective, MissionKind.Defense => MissionTuning.DefenseObjective, _ => MissionTuning.PatrolObjective,
            };
            var grouped = offer.Kind == MissionKind.Patrol
                ? offer.PatrolLocations is >= MissionTuning.MinPatrolLocations and <= MissionTuning.MaxPatrolLocations
                    && Enumerable.Range(0, offer.PatrolGroups).All(group => offer.Opposition.Any(item => item.Group == group))
                    && offer.Opposition.All(item => item.Group is { } group && group < offer.PatrolGroups)
                : offer.PatrolLocations == 0 && offer.Opposition.All(item => item.Group is null);
            var valid = offer.Opposition.Length > 0 && grouped
                && offer.Objective == objective && !string.IsNullOrWhiteSpace(offer.Briefing)
                && offer.Payment >= low * MissionTuning.MinPaymentShare - MissionTuning.RewardRounding
                && offer.Payment + offer.InsuranceLimit <= high + 2 * MissionTuning.RewardRounding
                && offer.SalvagePicks is >= 0 and <= MissionTuning.MaxSalvagePicks && offer.InsuranceLimit >= 0
                && offer.Payment % MissionTuning.RewardRounding == 0 && offer.InsuranceLimit % MissionTuning.RewardRounding == 0;
            if (!valid) { check(false, $"{offer.Name} has an opposition suited to its kind and rewards scaled to its difficulty"); return; }
        }
        check(true, "offers have an opposition suited to their kind and rewards scaled to their difficulty");
    }

    private static void CheckLances(Action<bool, string> check)
    {
        var chart = StarChart.Instance;
        var single = MissionGenerator.Generate(new MissionRandom(77), 1, Campaign.StartDate, chart.StartingSystemId, chart);
        check(JsonSerializer.Serialize(single) == JsonSerializer.Serialize(MissionGenerator.Generate(new MissionRandom(77), 1, Campaign.StartDate, chart.StartingSystemId, chart, lances: 1)),
            "a one-lance offer is generated as before");
        var offers = Enumerable.Range(0, 300).Select(i => MissionGenerator.Generate(new MissionRandom((ulong)i), 1, Campaign.StartDate, chart.StartingSystemId, chart, lances: 3)).ToArray();
        var singleTons = Enumerable.Range(0, 300).Select(i => MissionGenerator.Generate(new MissionRandom((ulong)i), 1, Campaign.StartDate, chart.StartingSystemId, chart))
            .Average(offer => offer.OppositionTonnage / (offer.Kind == MissionKind.Patrol ? MissionTuning.PatrolTonnageMultiplier : 1));
        var tripleTons = offers.Average(offer => offer.OppositionTonnage / (offer.Kind == MissionKind.Patrol ? MissionTuning.PatrolTonnageMultiplier : 1));
        check(tripleTons / singleTons is > 2.7 and < 3.3, $"each lance adds its own share of opposition tonnage ({singleTons:0} t for one lance, {tripleTons:0} t for three)");
        var board = MissionBoard.Create(5, Campaign.StartDate, chart.StartingSystemId, new Reputation(), lances: 2);
        var restored = MissionBoard.Restore(board.Snapshot(), Campaign.StartDate);
        check(board.Snapshot().Lances == 2 && restored.Lances == 2 && JsonSerializer.Serialize(restored.Snapshot()) == JsonSerializer.Serialize(board.Snapshot()),
            "a board for several lances keeps its lance count through a save");
        check(MissionBoard.Create(5, Campaign.StartDate, chart.StartingSystemId, new Reputation()).Snapshot().Lances is null, "a one-lance board saves as before");
        var mean = (double)offers.Average(offer => offer.Payment + offer.InsuranceLimit + offer.SalvagePicks * MissionTuning.CBillsPerSalvagePick);
        var singles = Enumerable.Range(0, 300).Select(i => MissionGenerator.Generate(new MissionRandom((ulong)i), 1, Campaign.StartDate, chart.StartingSystemId, chart)).ToArray();
        check(mean > 2 * singles.Average(offer => offer.Payment + offer.InsuranceLimit + offer.SalvagePicks * MissionTuning.CBillsPerSalvagePick),
            "rewards grow with the opposition of every lance");
    }

    private static void CheckChallenge(Action<bool, string> check)
    {
        var none = new Reputation();
        check(Math.Abs(MissionGenerator.DefaultTonnage(none) - MissionTuning.MinChallengeTons) < 1e-9, "no reputation sets the minimum default tonnage");
        var mixed = new Reputation();
        mixed.Adjust(Faction.Davion, 400); mixed.Adjust(Faction.Kurita, -300); mixed.Adjust(Faction.Steiner, 200);
        check(Math.Abs(MissionGenerator.DefaultTonnage(mixed) - 160) < 1e-9,
            "each faction counts from zero up: Davion +40, Kurita −30 and Steiner +20 sum to 60, a 160 t default");
        var hated = new Reputation();
        foreach (var faction in Enum.GetValues<Faction>()) hated.Adjust(faction, -1000);
        check(Math.Abs(MissionGenerator.DefaultTonnage(hated) - MissionTuning.MinChallengeTons) < 1e-9, "negative standing never lowers the default below the minimum");
        var honored = new Reputation();
        foreach (var faction in Enum.GetValues<Faction>()) honored.Adjust(faction, 1000);
        var capped = new Reputation();
        foreach (var faction in new[] { Faction.Davion, Faction.Kurita, Faction.Liao }) capped.Adjust(faction, 1000);
        check(Math.Abs(MissionGenerator.DefaultTonnage(honored) - MissionTuning.MaxChallengeTons) < 1e-9
            && Math.Abs(MissionGenerator.DefaultTonnage(capped) - MissionTuning.MaxChallengeTons) < 1e-9,
            "a summed standing of 300 or more sets the 400 t maximum");
        var random = new MissionRandom(11);
        var draws = Enumerable.Range(0, 4000).Select(_ => MissionGenerator.ChallengeTonnage(random, none, 1) / MissionTuning.MinChallengeTons).ToArray();
        var near = draws.Count(value => Math.Abs(value - 1) <= .15) / (double)draws.Length;
        check(near is > .45 and < .55 && draws.All(value => value >= MissionTuning.MinChallengeMultiplier - 1e-9 && value <= MissionTuning.MaxChallengeMultiplier + 1e-9),
            $"half of all challenge rolls lie within ±15% of the default ({near:P0}), clamped to 0.5–1.75×");
        var two = new MissionRandom(11); var one = new MissionRandom(11);
        check(Math.Abs(MissionGenerator.ChallengeTonnage(two, mixed, 2) - 2 * MissionGenerator.ChallengeTonnage(one, mixed, 1)) < 1e-9,
            "the challenge tonnage is per company lance");
        var fills = Enumerable.Range(0, 300).Select(i => (Target: 80.0 + i * 2, Mechs: MissionGenerator.Fill(new MissionRandom((ulong)i), 80 + i * 2, MissionTuning.WeightOdds))).ToArray();
        var worst = fills.Max(item => Math.Abs(item.Mechs.Sum(mech => mech.Tonnage) - item.Target));
        check(fills.All(item => item.Mechs.Count > 0) && worst <= 20, $"an opposition is filled to within 20 t of its target (worst {worst:0} t)");
    }

    private static void CheckPatrols(StarChart chart, Action<bool, string> check)
    {
        var patrols = Enumerable.Range(0, 3000).Select(i => MissionGenerator.Generate(new MissionRandom((ulong)i), 1, Campaign.StartDate, chart.StartingSystemId, chart))
            .Where(offer => offer.Kind == MissionKind.Patrol).ToArray();
        var others = Enumerable.Range(0, 3000).Select(i => MissionGenerator.Generate(new MissionRandom((ulong)i), 1, Campaign.StartDate, chart.StartingSystemId, chart))
            .Where(offer => offer.Kind != MissionKind.Patrol).ToArray();
        var ratio = patrols.Average(offer => offer.OppositionTonnage) / others.Average(offer => offer.OppositionTonnage);
        check(ratio is > 1.35 and < 1.65, $"patrols field half as much tonnage again as other contracts ({ratio:0.00}×)");
        var light = patrols.SelectMany(offer => offer.Opposition).Average(item => DraftCatalog.Mech(item.MechId).Tonnage);
        var usual = others.SelectMany(offer => offer.Opposition).Average(item => DraftCatalog.Mech(item.MechId).Tonnage);
        check(light < usual - 5, $"patrol mechs are lighter on average ({light:0} t against {usual:0} t)");
        var counts = patrols.GroupBy(offer => offer.PatrolLocations).ToDictionary(group => group.Key, group => group.Count() * 100.0 / patrols.Length);
        check(counts.Keys.Order().SequenceEqual([2, 3, 4]) && counts[2] is > 18 and < 32 && counts[3] is > 38 and < 52 && counts[4] is > 23 and < 37,
            $"patrols have 2, 3 or 4 locations about 25/45/30% of the time ({string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}: {pair.Value:0}%"))})");
        var unbalanced = patrols.Where(offer => offer.PatrolGroups > 1).Select(offer =>
        {
            var tons = Enumerable.Range(0, offer.PatrolGroups).Select(group => offer.Opposition.Where(item => item.Group == group)
                .Sum(item => DraftCatalog.Mech(item.MechId).Tonnage)).ToArray();
            return tons.Max() - tons.Min();
        }).Max();
        check(unbalanced <= 45, $"a patrol's groups carry similar tonnage (largest spread {unbalanced} t)");
        check(patrols.All(offer => offer.PatrolGroups == offer.PatrolLocations - 1 && offer.PatrolPoints == offer.PatrolLocations - 1
            && offer.Name.EndsWith(chart[offer.SystemId].Name) && offer.Briefing.Contains("patrolled")),
            "a patrol has one group per leg between locations and names its location");

        // An offer saved before contract kinds carried only a raid flag.
        var legacy = """{"Id":"offer-1","Name":"Defense of Galatea","Briefing":"B.","Objective":"O.","SystemId":"x","Posted":"3025-01-01","Opposition":[],"Payment":1,"SalvagePicks":0,"InsuranceLimit":0,"Employer":null,"Opponent":null,"Raid":false}""";
        var old = JsonSerializer.Deserialize<MissionOffer>(legacy, CampaignStore.JsonOptions)!.Upgraded();
        var raid = JsonSerializer.Deserialize<MissionOffer>(legacy.Replace("\"Raid\":false", "\"Raid\":true"), CampaignStore.JsonOptions)!.Upgraded();
        check(old.Kind == MissionKind.Defense && raid.Kind == MissionKind.Raid && old.LegacyRaid is null
            && !JsonSerializer.Serialize(old, CampaignStore.JsonOptions).Contains("\"Raid\""), "an offer saved with a raid flag loads as a raid or a defense");
    }

    private static void CheckSides(StarChart chart, Action<bool, string> check)
    {
        StarSystem Named(string name) => chart.Systems.Single(system => system.Name == name);
        bool IsHouse(Faction? faction) => faction is { } f && Factions.Spec(f).GreatHouse;
        bool Borders(string id, Faction house) => chart.WithinJumps(id, MissionTuning.BorderJumps).Any(near => chart[near].Faction == house);
        var random = new MissionRandom(2025);
        var offers = new List<MissionOffer>();
        foreach (var origin in new[] { "Galatea", "Luthien", "New Avalon", "Terra", "Sian", "Alpheratz" })
            for (var i = 0; i < 400; i++)
                offers.Add(MissionGenerator.Generate(random, i + 1, Campaign.StartDate, Named(origin).Id, chart));
        CheckOffers(offers, check);
        string Rule(MissionOffer offer)
        {
            var (employer, opponent, place) = (offer.Employer, offer.Opponent, chart[offer.SystemId]);
            if (employer.Kind == PartyKind.LocalForces) return "local forces never hire";
            if (employer.Kind == PartyKind.Pirates)
                return offer.Kind != MissionKind.Raid ? "pirates always raid"
                    : opponent.Kind == PartyKind.Faction && opponent.Faction != place.Faction ? "pirates only fight the House holding the target"
                    : opponent.Kind == PartyKind.Pirates ? "pirates never fight pirates" : "";
            if (employer.Faction == Faction.ComStar)
                return place.Owner is null ? "ComStar works at held systems"
                    : opponent.Kind == PartyKind.Faction && !IsHouse(opponent.Faction) ? "ComStar fights Houses, pirates or local forces"
                    : opponent.Faction is { } house && house != place.Faction && !Borders(offer.SystemId, house) ? "ComStar fights the House holding or bordering the location" : "";
            var own = employer.Faction!.Value;
            return opponent.Kind switch
            {
                PartyKind.Pirates when offer.Kind == MissionKind.Raid || place.Faction != own => "Houses defend their own worlds against pirates",
                PartyKind.LocalForces when offer.Kind != MissionKind.Raid || place.Faction == own => "Houses raid local forces away from their own worlds",
                PartyKind.Faction when opponent.Faction == Faction.ComStar && (offer.Kind != MissionKind.Raid || place.Faction != own) => "Houses remove ComStar from their own worlds",
                PartyKind.Faction when IsHouse(opponent.Faction) && offer.Kind == MissionKind.Raid && (place.Faction != opponent.Faction || !Borders(offer.SystemId, own))
                    => "a House raid targets a bordering rival's world",
                PartyKind.Faction when IsHouse(opponent.Faction) && offer.Kind != MissionKind.Raid && (place.Faction != own || !Borders(offer.SystemId, opponent.Faction!.Value))
                    => "a House defends its own world against a bordering rival",
                PartyKind.Faction when opponent.Faction == own => "a House never fights itself",
                _ => "",
            };
        }
        var broken = offers.Select(offer => (offer, rule: Rule(offer))).FirstOrDefault(item => item.rule.Length > 0);
        check(broken.offer is null, broken.offer is null ? "employers and opponents follow the contract rules"
            : $"{broken.offer.Name}: {broken.offer.Employer.Name} vs {broken.offer.Opponent.Name}: {broken.rule}");
        var employers = offers.GroupBy(offer => offer.Employer.Kind == PartyKind.Pirates ? "pirates" : offer.Employer.Faction == Faction.ComStar ? "ComStar" : "House")
            .ToDictionary(group => group.Key, group => group.Count() * 100.0 / offers.Count);
        check(employers["House"] is > 68 and < 82 && employers["ComStar"] is > 6 and < 14 && employers["pirates"] is > 11 and < 19,
            $"Houses, ComStar and pirates hire about 75/10/15% of the time ({string.Join(", ", employers.Select(pair => $"{pair.Key} {pair.Value:0}%"))})");
        check(offers.Any(offer => offer.Opponent.Kind == PartyKind.LocalForces) && offers.Any(offer => offer.Opponent.Faction == Faction.ComStar)
            && offers.Any(offer => offer.Kind == MissionKind.Raid && IsHouse(offer.Opponent.Faction) && IsHouse(offer.Employer.Faction))
            && offers.Any(offer => offer.Kind == MissionKind.Defense && IsHouse(offer.Opponent.Faction) && IsHouse(offer.Employer.Faction)),
            "offers include House raids and defenses, ComStar opponents and local forces");
        var houseOffers = offers.Where(offer => IsHouse(offer.Employer.Faction)).ToArray();
        var patrolShare = houseOffers.Count(offer => offer.Kind == MissionKind.Patrol) * 100.0 / houseOffers.Length;
        check(patrolShare is > 15 and < 35 && offers.Any(offer => offer.Kind == MissionKind.Patrol && offer.Employer.Faction == Faction.ComStar)
            && offers.All(offer => offer.Kind != MissionKind.Patrol || offer.Employer.Kind == PartyKind.Faction),
            $"Houses and ComStar post patrols, about a quarter of House offers ({patrolShare:0}%), and pirates never do");
        check(offers.All(offer => offer.Name.EndsWith(chart[offer.SystemId].Name) && offer.Briefing.Contains(offer.Employer.Name)),
            "the offer names its location and the briefing its employer");

        var reputation = new Reputation();
        reputation.Adjust(Faction.Kurita, -600);
        reputation.Adjust(Faction.ComStar, -600);
        var hated = Enumerable.Range(1, 400).Select(i => MissionGenerator.Generate(random, i, Campaign.StartDate, Named("Luthien").Id, chart, reputation)).ToArray();
        check(hated.All(offer => offer.Employer.Faction is not (Faction.Kurita or Faction.ComStar)) && hated.Any(offer => offer.Opponent.Faction == Faction.Kurita),
            "a faction that hates the force posts no offers, but can still be fought");

        var campaign = CampaignChecks.NewCompany();
        var good = campaign.Snapshot().Missions!;
        var first = good.Offers[0];
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Employer = new(PartyKind.LocalForces, null, "Militia") }, .. good.Offers[1..]] }, campaign.Date),
            check, "local forces cannot hire");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Opponent = new(PartyKind.Faction, null, "Nobody") }, .. good.Offers[1..]] }, campaign.Date),
            check, "a faction party needs a tracked faction");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Opponent = first.Employer }, .. good.Offers[1..]] }, campaign.Date),
            check, "an employer cannot fight itself");
        Reject(() => MissionBoard.Restore(good with { Offers = [first with { Employer = null! }, .. good.Offers[1..]] }, campaign.Date), check, "an offer needs an employer");
        var legacy = Campaign.Restore(campaign.Snapshot() with { Version = 7 });
        check(legacy.Missions.Available.Count == MissionTuning.BoardSize && legacy.Missions.HasUnseenOffers && legacy.Missions.Current is null,
            "version 7 saves receive a fresh board of offers with employers");
    }

    /// <summary>A quick-launched mission settles its battle into the campaign once.</summary>
    private static void CheckSettlement(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany(); var roster = campaign.Roster!;
        Throws(() => campaign.SettleMission(new(MissionOutcome.Success, [], 0)), check, "settling without a current mission is refused");
        var offer = campaign.Missions.Available[0]; campaign.AcceptMission(offer.Id);
        var deployed = roster.Mechs.Take(2).ToArray(); var pilots = deployed.Select(mech => roster.Pilot(mech)!).ToArray();
        var sim = CombatSimulation.DeployMission(roster, deployed.Select(mech => mech.Id), offer, 5, 183);
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        friends[0].ApplyDamage(MechLocation.Head, 10000);
        friends[1].ApplyDamage(MechLocation.LeftArm, 20);
        sim.Units.First(unit => unit.Side == CombatSide.Enemy).ApplyDamage(MechLocation.Head, 10000);
        sim.End();
        var report = sim.MissionReport();
        check(report.Outcome == MissionOutcome.Failure && report.EnemyWarriorsSpared == offer.Opposition.Length - 1
            && report.Mechs.Select(item => item.MechId).SequenceEqual(deployed.Select(mech => mech.Id)) && report.Mechs[0].WarriorKilled && !report.Mechs[1].WarriorKilled,
            "withdrawing reports failure, the deployed mechs and the enemy MechWarriors who survived");
        var conduct = new MissionConduct(MissionOutcome.Failure, deployed.Select(mech => mech.Definition.Id).ToArray(), friends.Sum(unit => unit.ArmorLost), report.EnemyWarriorsSpared, 0, 0, 0);
        var expected = Reputation.MissionChanges(offer, conduct);
        var cash = campaign.Cash; var date = campaign.Date;
        var result = campaign.SettleMission(report);
        check(result.Payment == 0 && campaign.Cash == cash && campaign.Date == date.AddDays(1) && result.Day.Advanced, "a failed mission pays nothing and takes a day");
        check(result.Reputation.OrderBy(pair => pair.Key).SequenceEqual(expected.OrderBy(pair => pair.Key))
            && Factions.All.All(faction => campaign.Reputation.Tenths(faction.Faction) == expected.GetValueOrDefault(faction.Faction)),
            "settlement applies the mission's reputation changes");
        check(campaign.Missions.Current is null && campaign.Missions.Find(offer.Id) is null, "a settled mission is finished");
        check(pilots[0].Health == WarriorHealth.Dead && deployed[0].WarriorId is null && result.Killed.SequenceEqual([pilots[0].Id])
            && pilots[1].Health == WarriorHealth.Healthy && deployed[1].WarriorId == pilots[1].Id, "a MechWarrior whose head was destroyed dies and leaves the cockpit");
        check(pilots.All(pilot => pilot.Deployments == 1 && pilot.Kills == 0) && roster.Warriors.Except(pilots).All(warrior => warrior.Deployments == 0),
            "settlement counts a deployment for each deployed pilot, the fallen included");
        check(!deployed[0].LocationIntact(MechLocation.Head) && deployed[1].Damaged
            && deployed[1].Locations.All(loc => friends[1].Locations[loc.Location] is var after
                && (loc.Armor, loc.RearArmor, loc.Structure) == (after.Armor, after.RearArmor, after.Structure))
            && deployed.All(mech => mech.Ammo.All(bin => bin.Shots == mech.Spec.Ammo.Single(stock => stock.Id == bin.Id).Shots))
            && roster.Mechs.Skip(2).All(mech => !mech.Damaged), "deployed mechs keep their damage and refill ammunition; others are untouched");
        Throws(() => campaign.SettleMission(report), check, "a mission settles only once");
        var restored = Campaign.Restore(campaign.Snapshot());
        check(Json(restored) == Json(campaign), "a settled campaign survives restoration");

        var winner = CampaignChecks.NewCompany(); var won = winner.Missions.Available[1]; winner.AcceptMission(won.Id);
        var mech = winner.Roster!.Mechs[0];
        cash = winner.Cash;
        var success = winner.SettleMission(new(MissionOutcome.Success, [new(mech.Id, mech.Locations.ToArray(), mech.Equipment.ToArray(), false, 0, 2)], won.Opposition.Length));
        var victor = winner.Roster.Pilot(mech)!;
        check(victor.Deployments == 1 && victor.Kills == 2, "settlement adds the reported kills to the pilot's record");
        check(success.Payment == won.Payment && winner.Cash == cash + won.Payment && success.Killed.Length == 0
            && (won.Employer.Faction is not { } employer || winner.Reputation.Tenths(employer) > 0), "a successful mission pays and pleases the employer");
        var broke = CampaignChecks.NewCompany(); broke.AcceptMission(broke.Missions.Available[0].Id);
        while (!broke.PaymentPending) broke.AdvanceOneDay();
        Throws(() => broke.SettleMission(new(MissionOutcome.Success, [new(mech.Id, mech.Locations.ToArray(), mech.Equipment.ToArray(), false, 0)], 0)), check,
            "a mission cannot settle while the quarterly payment is pending");
    }

    private static void Throws(Action action, Action<bool, string> check, string message)
    {
        var thrown = false;
        try { action(); } catch (InvalidOperationException) { thrown = true; }
        check(thrown, message);
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
