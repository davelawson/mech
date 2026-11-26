using System.Text.Json;

namespace Mech.Tests;

/// <summary>MechWarriors' service histories: what is recorded and when, its wording and order, and its saving.</summary>
internal static class ServiceRecordChecks
{
    public static void Run(Action<bool, string> check)
    {
        CheckWording(check);
        CheckFoundingAndHiring(check);
        CheckBattle(check);
        CheckAce(check);
        CheckPromotion(check);
        CheckDeployments(check);
        CheckSaves(check);
    }

    private static void Rejects(Action action, Action<bool, string> check, string name)
    {
        var rejected = false;
        try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        check(rejected, name);
    }

    private static string[] Lines(RosterWarrior warrior) => [.. warrior.History.Select(ServiceRecord.Describe)];
    private static ServiceEventKind[] Kinds(RosterWarrior warrior) => [.. warrior.History.Select(item => item.Kind)];
    private static DeployedMechReport Report(OwnedMech mech, bool killed = false, int kills = 0, int days = 0) =>
        new(mech.Id, mech.Locations.ToArray(), mech.Equipment.ToArray(), killed, 0, kills, days);

    /// <summary>Accepts the first reachable offer and returns its name.</summary>
    private static string Accept(Campaign campaign)
    {
        var offer = campaign.Missions.Available.First(item => campaign.DeadlineDays(item) is not null);
        campaign.AcceptMission(offer.Id);
        return offer.Name;
    }

    private static void CheckWording(Action<bool, string> check)
    {
        var day = new DateOnly(3025, 1, 14);
        ServiceEvent At(ServiceEventKind kind) => new(day, kind) { Place = "Raid on Hesperus II" };
        var expected = new (ServiceEvent Event, string Text)[]
        {
            (new(day, ServiceEventKind.Founded) { Place = "Wolf's Hounds" }, "Founding member of Wolf's Hounds."),
            (new(day, ServiceEventKind.Hired) { Place = "Hesperus II" }, "Hired at Hesperus II."),
            (At(ServiceEventKind.Deployed) with { Mech = "Wolverine WVR-6R", Lance = 1 }, "Took the Wolverine WVR-6R into Raid on Hesperus II with Lance 2."),
            (At(ServiceEventKind.Deployed) with { Mech = "Wolverine WVR-6R" }, "Took the Wolverine WVR-6R into Raid on Hesperus II."),
            (At(ServiceEventKind.Deployed) with { Lance = 1 }, "Fought with Lance 2 for the first time in Raid on Hesperus II."),
            (At(ServiceEventKind.Deployed) with { Lance = 0, Rejoined = true }, "Rejoined Lance 1 for Raid on Hesperus II."),
            (At(ServiceEventKind.Kill) with { Enemy = "Atlas AS7-D", Weapon = "PPC", Manner = KillManner.Destroyed },
                "Destroyed an enemy Atlas AS7-D with a PPC during Raid on Hesperus II."),
            (At(ServiceEventKind.Kill) with { Enemy = "Atlas AS7-D", Weapon = "LRM 20", Manner = KillManner.Disabled },
                "Disabled an enemy Atlas AS7-D with an LRM 20 during Raid on Hesperus II."),
            (At(ServiceEventKind.Kill) with { Enemy = "Marauder MAD-3R", Weapon = "Medium Laser", Manner = KillManner.Cockpit },
                "Killed the MechWarrior of an enemy Marauder MAD-3R with a Medium Laser strike to the cockpit during Raid on Hesperus II."),
            (At(ServiceEventKind.Kill) with { Enemy = "Marauder MAD-3R", Weapon = "AC/20", Manner = KillManner.Head },
                "Killed the MechWarrior of an enemy Marauder MAD-3R by destroying the head with an AC/20 during Raid on Hesperus II."),
            (At(ServiceEventKind.Ace), "Became an ace with their fifth kill."),
            (At(ServiceEventKind.Ejected) with { Mech = "Griffin GRF-1N", Days = 14, Destroyed = true, AsDestroyed = true },
                "Ejected as their Griffin GRF-1N was destroyed during Raid on Hesperus II; 14 days to recover."),
            (At(ServiceEventKind.Ejected) with { Mech = "Griffin GRF-1N", Days = 12, Destroyed = true },
                "Ejected from their Griffin GRF-1N during Raid on Hesperus II, and it was later destroyed; 12 days to recover."),
            (At(ServiceEventKind.Ejected) with { Mech = "Griffin GRF-1N", Days = 12 }, "Ejected from their Griffin GRF-1N during Raid on Hesperus II; 12 days to recover."),
            (At(ServiceEventKind.MechLost) with { Mech = "Griffin GRF-1N" }, "Their Griffin GRF-1N was destroyed during Raid on Hesperus II; they walked away."),
            (At(ServiceEventKind.Injured) with { Causes = [InjuryCause.AmmoExplosion], Days = 14 },
                "Injured by an ammunition explosion during Raid on Hesperus II; 14 days to recover."),
            (At(ServiceEventKind.Injured) with { Causes = [InjuryCause.AmmoExplosion, InjuryCause.LifeSupport, InjuryCause.AmmoExplosion] },
                "Injured by two ammunition explosions and overheating with damaged life support during Raid on Hesperus II."),
            (new(day, ServiceEventKind.Recovered) { Days = 14 }, "Returned to duty after 14 days' recovery."),
            (new(day, ServiceEventKind.Recovered), "Returned to duty."),
            (At(ServiceEventKind.ComradeKilled) with { Comrade = "“Ace” Jane Doe" }, "Saw “Ace” Jane Doe killed during Raid on Hesperus II."),
            (new(day, ServiceEventKind.SkillAdvanced) { Skill = WarriorSkill.Gunnery, Rating = 4 }, "Gunnery advanced to Veteran."),
            (new(day, ServiceEventKind.Promoted) { Rank = WarriorExperience.Sergeant }, "Promoted to Sergeant."),
            (new(day, ServiceEventKind.Promoted) { Rank = WarriorExperience.PrivateFirstClass }, "Promoted to Private First Class."),
            (At(ServiceEventKind.Killed) with { Enemy = "Jenner JR7-D", Weapon = "Medium Laser", Manner = KillManner.Cockpit },
                "Killed during Raid on Hesperus II when an enemy Jenner JR7-D's Medium Laser struck the cockpit."),
            (At(ServiceEventKind.Killed) with { Enemy = "Jenner JR7-D", Weapon = "SRM 4", Manner = KillManner.Head },
                "Killed during Raid on Hesperus II when an enemy Jenner JR7-D's SRM 4 destroyed the head."),
            (At(ServiceEventKind.Killed) with { Enemy = "Jenner JR7-D", Weapon = "PPC", Manner = KillManner.Head, Exploded = "SRM 6 ammunition" },
                "Killed during Raid on Hesperus II when their SRM 6 ammunition exploded after a hit from an enemy Jenner JR7-D's PPC."),
            (At(ServiceEventKind.Killed), "Killed during Raid on Hesperus II."),
        };
        foreach (var (item, text) in expected)
            check(ServiceRecord.Describe(item) == text, $"a {item.Kind} entry reads “{text}”, not “{ServiceRecord.Describe(item)}”");
        check(Enum.GetValues<ServiceEventKind>().All(kind => expected.Any(entry => entry.Event.Kind == kind)), "every kind of entry has its wording checked");
        check(new[] { "Medium Laser", "PPC", "Large Laser", "Machine Gun", "Flamer" }.All(name => ServiceRecord.Article(name) == "a " + name)
            && new[] { "LRM 15", "SRM 6", "AC/10", "AC/2" }.All(name => ServiceRecord.Article(name) == "an " + name),
            "weapon names take the article they are spoken with");
        check(ServiceRecord.DateText(day) == "14 JAN 3025" && ServiceRecord.EarlierNote(day) == "No service record before 14 JAN 3025",
            "entries are dated like the calendar");
    }

    private static void CheckFoundingAndHiring(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany();
        check(campaign.Roster!.Warriors.All(warrior => warrior.RecordsSince is null && warrior.History.Count == 1
                && warrior.History[0] == new ServiceEvent(Campaign.StartDate, ServiceEventKind.Founded) { Place = "Test Company" }),
            "every drafted MechWarrior's history begins as a founding member, on the day the company formed");
        var coop = new Campaign();
        coop.EstablishCompany(InitialDraft.CreateCompany(2, new Random(5), StartingScenario.StartingOut), ["Ann", "Sam"]);
        check(coop.Roster!.Warriors.All(warrior => Kinds(warrior).SequenceEqual([ServiceEventKind.Founded]) && warrior.History[0].Place == coop.Roster.ForceName),
            "every player's drafted MechWarriors are founding members of the co-op company");
        campaign.AdvanceOneDay(); campaign.AdvanceOneDay();
        var recruit = PilotGenerator.Generate(new Random(3), new PilotsInUse(campaign.Roster.Warriors.Select(item => item.Spec)), WarriorExperience.Corporal);
        campaign.PlaceRecruits(recruit);
        campaign.Recruit(recruit.Id);
        var hired = campaign.Roster.Warriors.Single(item => item.Id == recruit.Id);
        check(hired.History.Count == 1 && hired.History[0].Date == campaign.Date && hired.History[0].Kind == ServiceEventKind.Hired
            && Lines(hired)[0] == $"Hired at {StarChart.Instance[campaign.Travel.OrbitSystemId].Name}.", "a recruit's history begins with where and when they were hired");
    }

    /// <summary>One mission's kills, a death, an ejection and an injury, and the recovery after it.</summary>
    private static void CheckBattle(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany(); var roster = campaign.Roster!;
        var mission = Accept(campaign); var day = campaign.Date;
        var mechs = roster.Mechs.Take(4).ToArray(); var pilots = mechs.Select(mech => roster.Pilot(mech)!).ToArray();
        var report = new MissionReport(MissionOutcome.Failure,
        [
            Report(mechs[0], kills: 1) with { KillLog = [new(10, "Atlas AS7-D", "PPC", KillManner.Destroyed), new(30, "Jenner JR7-D", "Medium Laser", KillManner.Cockpit, false)] },
            Report(mechs[1], killed: true) with { Death = new(20, "Jenner JR7-D", "Medium Laser", KillManner.Cockpit) },
            Report(mechs[2], days: 30) with { Ejected = true, AutoEjected = true, MechDestroyed = true, InjuryCauses = [InjuryCause.AmmoExplosion] },
            Report(mechs[3], days: 14) with { InjuryCauses = [InjuryCause.AmmoExplosion] },
        ], 0);
        Rejects(() => campaign.SettleMission(report with { Mechs = [report.Mechs[0] with { Kills = 0 }] }), check, "a report cannot credit more kills than it counts");
        campaign.SettleMission(report);
        var comrade = $"“{pilots[1].Spec.Callsign}” {pilots[1].Spec.Name}";
        check(pilots.All(pilot => pilot.History.Skip(1).All(item => item.Date == day)), "a mission's entries are dated its settlement day");
        check(Lines(pilots[0]).Skip(1).SequenceEqual([
                $"Took the {mechs[0].Spec.DisplayName} into {mission} with Lance 1.",
                $"Destroyed an enemy Atlas AS7-D with a PPC during {mission}.",
                $"Saw {comrade} killed during {mission}.",
                $"Killed the MechWarrior of an enemy Jenner JR7-D with a Medium Laser strike to the cockpit during {mission}."]),
            $"a first deployment names the mech and lance, then kills and a fallen comrade follow in battle order ({string.Join(" | ", Lines(pilots[0]))})");
        check(Lines(pilots[1]).Skip(1).SequenceEqual([
                $"Took the {mechs[1].Spec.DisplayName} into {mission} with Lance 1.",
                $"Killed during {mission} when an enemy Jenner JR7-D's Medium Laser struck the cockpit."]),
            "a fallen MechWarrior's history ends with how they died, without seeing their own death");
        check(Lines(pilots[2]).Skip(2).SequenceEqual([
                $"Saw {comrade} killed during {mission}.",
                $"Ejected as their {mechs[2].Spec.DisplayName} was destroyed during {mission}; 30 days to recover.",
                $"Injured by an ammunition explosion during {mission}."]),
            "an ejection carries the recovery time and the mech's loss; another injury then only names its cause");
        check(Lines(pilots[3]).Skip(2).SequenceEqual([$"Saw {comrade} killed during {mission}.", $"Injured by an ammunition explosion during {mission}; 14 days to recover."]),
            "an injury names its cause and recovery time");
        while (pilots[3].Health == WarriorHealth.Injured) campaign.AdvanceOneDay();
        check(pilots[3].History[^1] == new ServiceEvent(day.AddDays(14), ServiceEventKind.Recovered) { Days = 14 }
            && Lines(pilots[3])[^1] == "Returned to duty after 14 days' recovery.", "a recovery is recorded on the day the MechWarrior returns to duty");
        while (pilots[2].Health == WarriorHealth.Injured) campaign.AdvanceOneDay();
        check(pilots[2].History[^1] is { Kind: ServiceEventKind.Recovered, Days: 30 }, "an ejected MechWarrior's recovery counts the ejection's days");

        var lost = CampaignChecks.NewCompany(); var name = Accept(lost); var mech = lost.Roster!.Mechs[0]; var pilot = lost.Roster.Pilot(mech)!;
        lost.SettleMission(new(MissionOutcome.Failure, [Report(mech) with { MechDestroyed = true }], 0));
        check(Lines(pilot)[^1] == $"Their {mech.Spec.DisplayName} was destroyed during {name}; they walked away.", "a mech destroyed under a MechWarrior who did not eject is recorded");
    }

    private static void CheckAce(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany(); var mech = campaign.Roster!.Mechs[0]; var pilot = campaign.Roster.Pilot(mech)!;
        pilot.Kills = 4; Accept(campaign);
        campaign.SettleMission(new(MissionOutcome.Success, [Report(mech, kills: 2) with
            { KillLog = [new(5, "Locust LCT-1V", "Medium Laser", KillManner.Destroyed), new(9, "Stinger STG-3R", "Medium Laser", KillManner.Disabled)] }], 0));
        check(Kinds(pilot).Skip(2).SequenceEqual([ServiceEventKind.Kill, ServiceEventKind.Ace, ServiceEventKind.Kill]), "a MechWarrior becomes an ace with their fifth kill");
        Accept(campaign);
        campaign.SettleMission(new(MissionOutcome.Success, [Report(mech, kills: 1) with { KillLog = [new(5, "Locust LCT-1V", "Medium Laser", KillManner.Destroyed)] }], 0));
        check(pilot.History.Count(item => item.Kind == ServiceEventKind.Ace) == 1, "an ace is made only once");
    }

    /// <summary>A skill gain that reaches a level and crosses a rank boundary records both, the promotion after.</summary>
    private static void CheckPromotion(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany(); var mech = campaign.Roster!.Mechs[0]; var pilot = campaign.Roster.Pilot(mech)!;
        WarriorSkill? promoting = null;
        for (var guard = 0; promoting is null && guard < 200; guard++)
        {
            var skill = SkillGrowth.Skills.First(item => !SkillGrowth.Maxed(SkillGrowth.Rating(pilot.Spec, item)));
            var (after, _, advanced) = SkillGrowth.Gain(pilot.Spec, pilot.Progress, skill);
            if (advanced && after.Rank > pilot.Spec.Rank) promoting = skill; else pilot.GainSkill(skill);
        }
        if (promoting is not { } gained) { check(false, "a promotion can be reached"); return; }
        var rank = SkillGrowth.Gain(pilot.Spec, pilot.Progress, gained).Warrior.Rank;
        Accept(campaign);
        campaign.SettleMission(new(MissionOutcome.Failure, [Report(mech) with { SkillGains = [gained] }], 0));
        check(pilot.History[^2] == new ServiceEvent(campaign.Date.AddDays(-1), ServiceEventKind.SkillAdvanced) { Skill = gained, Rating = SkillGrowth.Rating(pilot.Spec, gained) }
            && pilot.History[^1] == new ServiceEvent(campaign.Date.AddDays(-1), ServiceEventKind.Promoted) { Rank = rank },
            "a skill reaching a level is recorded, and a promotion after it");
        var banked = pilot.History.Count; Accept(campaign);
        var growing = SkillGrowth.Skills.First(item => !SkillGrowth.Maxed(SkillGrowth.Rating(pilot.Spec, item)) && SkillGrowth.Threshold(SkillGrowth.Rating(pilot.Spec, item)) > 1);
        campaign.SettleMission(new(MissionOutcome.Failure, [Report(mech) with { SkillGains = [growing] }], 0));
        check(pilot.History.Count == banked, "a success banked short of a level records nothing");
    }

    /// <summary>A deployment is recorded only when the MechWarrior takes a different mech or lance into a mission than last time.</summary>
    private static void CheckDeployments(Action<bool, string> check)
    {
        var campaign = new Campaign();
        campaign.EstablishCompany(InitialDraft.CreateCompany(2, new Random(5), StartingScenario.StartingOut), ["Ann", "Sam"]);
        var roster = campaign.Roster!; var first = roster.Mechs[0]; var second = roster.Mechs[1]; var pilot = roster.Pilot(first)!;
        ServiceEvent? Fight()
        {
            var count = pilot.History.Count; var mech = roster.Assignment(pilot.Id)!;
            Accept(campaign); campaign.SettleMission(new(MissionOutcome.Failure, [Report(mech)], 0));
            return pilot.History.Count > count ? pilot.History[count] : null;
        }
        check(Fight() is { Kind: ServiceEventKind.Deployed, Lance: 0 } entry && entry.Mech == first.Spec.DisplayName, "a first deployment records the mech and lance");
        check(Fight() is null, "deploying again in the same mech and lance records nothing");
        var entries = pilot.History.Count;
        roster.Assign(second.Id, pilot.Id, true); roster.Assign(first.Id, pilot.Id, true); roster.Assign(second.Id, pilot.Id, true);
        check(pilot.History.Count == entries, "reassigning in the hangar records nothing on its own");
        check(Fight() is { Kind: ServiceEventKind.Deployed, Lance: null } swapped && swapped.Mech == second.Spec.DisplayName, "deploying in another mech records it");
        roster.SetLance(roster.LanceMechs(1).First().Id, null); roster.SetLance(second.Id, 1);
        check(Fight() is { Kind: ServiceEventKind.Deployed, Mech: null, Lance: 1, Rejoined: false } moved
            && ServiceRecord.Describe(moved).StartsWith("Fought with Lance 2 for the first time"), "deploying with a new lance records it");
        roster.SetLance(second.Id, 0);
        check(Fight() is { Kind: ServiceEventKind.Deployed, Mech: null, Lance: 0, Rejoined: true } back
            && ServiceRecord.Describe(back).StartsWith("Rejoined Lance 1 for"), "returning to an earlier lance records the return");
        roster.Assign(first.Id, pilot.Id, true); roster.SetLance(first.Id, 1);
        check(Fight() is { Kind: ServiceEventKind.Deployed, Lance: 1 } both && both.Mech == first.Spec.DisplayName, "a new mech and lance together make one entry");
        check(Campaign.Restore(campaign.Snapshot()).Roster!.Warriors.Single(item => item.Id == pilot.Id) is { LastMech: var last, LastLance: 1 } && last == first.Id,
            "the previous deployment's mech and lance are saved");
    }

    private static void CheckSaves(Action<bool, string> check)
    {
        var campaign = CampaignChecks.NewCompany(); var mech = campaign.Roster!.Mechs[0];
        Accept(campaign);
        campaign.SettleMission(new(MissionOutcome.Failure, [Report(mech, days: 12) with { Ejected = true }], 0));
        var save = campaign.Snapshot();
        var restored = Campaign.Restore(save);
        check(JsonSerializer.Serialize(restored.Snapshot()) == JsonSerializer.Serialize(save)
            && restored.Roster!.Warriors.Zip(campaign.Roster.Warriors).All(pair => pair.First.History.SequenceEqual(pair.Second.History)),
            "service histories survive a save round trip");
        var json = JsonSerializer.Deserialize<CampaignSnapshot>(JsonSerializer.Serialize(save))!;
        check(json.Roster!.Warriors.Zip(save.Roster!.Warriors).All(pair => pair.First.History!.SequenceEqual(pair.Second.History!)), "service histories survive JSON");
        var old = save with { Roster = save.Roster with { Warriors = [.. save.Roster.Warriors.Select(item => item with { History = null })] } };
        var loaded = Campaign.Restore(old);
        check(loaded.Roster!.Warriors.All(warrior => warrior.History.Count == 0 && warrior.RecordsSince == save.Date),
            "MechWarriors from a save made before service histories start an empty one on the day it loads");
        loaded.AdvanceOneDay();
        check(Campaign.Restore(loaded.Snapshot()).Roster!.Warriors.All(warrior => warrior.RecordsSince == save.Date), "the day a history began is saved");
        var warriors = save.Roster.Warriors;
        Rejects(() => Campaign.Restore(save with { Roster = save.Roster with { Warriors = [warriors[0] with { History = [new(save.Date.AddDays(1), ServiceEventKind.Recovered)] }, .. warriors[1..]] } }),
            check, "an entry dated after the save is refused");
        Rejects(() => Campaign.Restore(save with { Roster = save.Roster with { Warriors = [warriors[0] with { History = [new(save.Date, (ServiceEventKind)99)] }, .. warriors[1..]] } }),
            check, "an unknown kind of entry is refused");
        Rejects(() => Campaign.Restore(save with { Roster = save.Roster with { Warriors = [warriors[0] with { History = [null!] }, .. warriors[1..]] } }),
            check, "a missing entry is refused");
    }
}
