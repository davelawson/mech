using System.Text.Json;

namespace Mech.Tests;

internal static class ReputationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany();
        var reputation = campaign.Reputation;
        check(Enum.GetValues<Faction>().All(faction => reputation.Tenths(faction) == 0 && reputation.Tier(faction) == ReputationTier.Neutral),
            "a new company is neutral with every faction");
        check(Factions.All.Count == 6 && Factions.GreatHouses.Count == 5 && !Factions.GreatHouses.Contains(Faction.ComStar),
            "the five Great Houses and ComStar are tracked");

        check(Reputation.TierFor(-60) == ReputationTier.Hated && Reputation.TierFor(-59) == ReputationTier.Hostile
            && Reputation.TierFor(-20) == ReputationTier.Hostile && Reputation.TierFor(-19) == ReputationTier.Neutral
            && Reputation.TierFor(19) == ReputationTier.Neutral && Reputation.TierFor(20) == ReputationTier.Friendly
            && Reputation.TierFor(59) == ReputationTier.Friendly && Reputation.TierFor(60) == ReputationTier.Honored,
            "tiers change at −60, −20, +20 and +60");

        reputation.Adjust(Faction.Kurita, 245);
        reputation.Adjust(Faction.Liao, -315);
        reputation.Adjust(Faction.Marik, 5);
        check(reputation.Value(Faction.Kurita) == 25 && reputation.Text(Faction.Kurita) == "Friendly +25"
            && reputation.Text(Faction.Liao) == "Hostile −32" && reputation.Value(Faction.Marik) == 1 && reputation.Text(Faction.Davion) == "Neutral 0",
            "standing reads as a tier and whole points, rounding half away from zero");
        reputation.Adjust(Faction.Steiner, 5000); reputation.Adjust(Faction.Davion, -5000);
        check(reputation.Value(Faction.Steiner) == 100 && reputation.Value(Faction.Davion) == -100 && reputation.Hates(Faction.Davion)
            && !reputation.Hates(Faction.Liao), "standing stays within ±100");

        var luthien = StarChart.Instance.Systems.Single(system => system.Name == "Luthien");
        var alpheratz = StarChart.Instance.Systems.Single(system => system.Name == "Alpheratz");
        check(reputation.OwnerText(luthien) == "Owner: Draconis Combine · Friendly +25" && reputation.OwnerText(alpheratz) == "Owner: Outworlds Alliance",
            "a system names its owner, with standing only for tracked factions");

        var restored = Campaign.Restore(campaign.Snapshot());
        check(Enum.GetValues<Faction>().All(faction => restored.Reputation.Tenths(faction) == reputation.Tenths(faction)),
            "reputation survives save restoration");
        var legacy = Campaign.Restore(campaign.Snapshot() with { Version = 6, Reputation = null });
        check(Enum.GetValues<Faction>().All(faction => legacy.Reputation.Tenths(faction) == 0), "saves before version 7 start neutral");
        Reject(() => Campaign.Restore(campaign.Snapshot() with { Reputation = null }), check, "a version 7 save needs reputation");
        Reject(() => Reputation.Restore(new(new() { ["Kurita"] = 5 })), check, "reputation must cover every faction");
        var tenths = reputation.Snapshot().Tenths;
        Reject(() => Reputation.Restore(new(new(tenths) { ["Kurita"] = 1001 })), check, "reputation beyond ±100 is rejected");
        var renamed = new Dictionary<string, int>(tenths); renamed.Remove("Kurita"); renamed["3"] = 0;
        Reject(() => Reputation.Restore(new(renamed)), check, "reputation is keyed by faction name");
        check(JsonSerializer.Serialize(campaign.Snapshot()).Contains("\"Kurita\":245"), "saves record standing in tenths by faction name");
        CheckMissionChanges(check);
    }

    private static void CheckMissionChanges(Action<bool, string> check)
    {
        var galatea = StarChart.Instance.StartingSystemId; // Lyran Commonwealth
        var offer = new MissionOffer("offer-1", "Raid on Galatea", "Briefing.", MissionTuning.RaidObjective, galatea, Campaign.StartDate,
            [new("locust", EnemyPilotRank.Regular), new("locust", EnemyPilotRank.Regular)], 100_000, 2, 50_000,
            Party.Of(Faction.Davion), Party.Of(Faction.Kurita), MissionKind.Raid);
        string[] force = ["locust", "atlas"];
        var maximum = new MissionConduct(MissionOutcome.Failure, force, 0, 0, 0, 0, 0).MaximumArmor;
        MissionConduct Conduct(MissionOutcome outcome, int armorLost = 0, int spared = 0, int fled = 0, int salvage = 0, int buildings = 0) =>
            new(outcome, force, armorLost, spared, fled, salvage, buildings);
        bool Is(MissionOffer offer, MissionConduct conduct, params (Faction Faction, int Tenths)[] expected)
        {
            var changes = Reputation.MissionChanges(offer, conduct);
            return changes.Count == expected.Length && expected.All(item => changes.GetValueOrDefault(item.Faction) == item.Tenths);
        }

        check(maximum == (DraftCatalog.Mech("locust").Locations.Sum(EquipmentRules.ArmorLimit) + DraftCatalog.Mech("atlas").Locations.Sum(EquipmentRules.ArmorLimit)) * CombatRules.DamageScale,
            "effort is measured against the most armor the deployed tonnage allows");
        check(Is(offer, Conduct(MissionOutcome.Success, spared: 4), (Faction.Davion, 60), (Faction.Kurita, 10), (Faction.ComStar, 30)),
            "a clean win raises the employer by 6, the opponent by 1 and ComStar by 3");
        check(Is(offer, Conduct(MissionOutcome.Success), (Faction.Davion, 60), (Faction.Kurita, -30), (Faction.ComStar, 10)),
            "a ruthless win costs 3 with the opponent");
        check(Is(offer, Conduct(MissionOutcome.Success, spared: 1, fled: 1), (Faction.Davion, 60), (Faction.Kurita, -10), (Faction.ComStar, 20)),
            "fleeing mechs count like spared MechWarriors");
        check(Is(offer, Conduct(MissionOutcome.Failure, armorLost: maximum / 4 + 1), (Faction.Davion, -20), (Faction.Kurita, -30)),
            "a failure after losing a quarter of the possible armor costs the employer 2");
        check(Is(offer, Conduct(MissionOutcome.Failure, armorLost: maximum / 4 - 1), (Faction.Davion, -80), (Faction.Kurita, -30), (Faction.ComStar, -10)),
            "a failure without that effort costs the employer 8 and ComStar 1");
        check(Is(offer, Conduct(MissionOutcome.Abandoned), (Faction.Davion, -80), (Faction.ComStar, -10))
            && Is(offer, Conduct(MissionOutcome.MissedDeadline), (Faction.Davion, -80), (Faction.ComStar, -10)),
            "abandoning or missing the deadline counts as no effort, and no battle touches the opponent");
        check(Is(offer, Conduct(MissionOutcome.Success, salvage: 4), (Faction.Davion, 20), (Faction.Kurita, -30), (Faction.ComStar, -10)),
            "each salvage pick over the allotment costs 2 with the employer and 1 with ComStar");
        check(Is(offer, Conduct(MissionOutcome.Success, buildings: 5), (Faction.Davion, 60), (Faction.Kurita, -30), (Faction.Steiner, -20), (Faction.ComStar, 0))
            && Is(offer, Conduct(MissionOutcome.Success, buildings: 3), (Faction.Davion, 60), (Faction.Kurita, -30), (Faction.ComStar, 10)),
            "each building beyond three costs 1 with the system's owner and 0.5 with ComStar");

        var pirates = offer with { Employer = Party.Pirates, Opponent = Party.Of(Faction.Steiner) };
        check(Is(pirates, Conduct(MissionOutcome.Success), (Faction.Steiner, -50), (Faction.ComStar, -10)),
            "raiding for pirates costs 2 with ComStar and 2 with the target world's owner");
        var local = offer with { Opponent = new(PartyKind.LocalForces, null, "Galatea Militia") };
        check(Is(local, Conduct(MissionOutcome.Success, spared: 2), (Faction.Davion, 60), (Faction.ComStar, 20)),
            "local forces hold no reputation, but sparing them still earns ComStar's respect");
        var comstar = offer with { Employer = Party.Of(Faction.ComStar), Opponent = Party.Pirates };
        check(Is(comstar, Conduct(MissionOutcome.Success), (Faction.ComStar, 70)), "ComStar as employer adds its honor changes to its employer gain");

        var reputation = new Reputation();
        var applied = reputation.ApplyMissionOutcome(offer, Conduct(MissionOutcome.Success, spared: 4));
        check(reputation.Tenths(Faction.Davion) == 60 && reputation.Tenths(Faction.Kurita) == 10 && reputation.Tenths(Faction.ComStar) == 30
            && applied.Count == 3, "applying a mission outcome adjusts standing and reports the changes");
    }

    private static void Reject(Action action, Action<bool, string> check, string message)
    {
        var rejected = false;
        try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        check(rejected, message);
    }
}
