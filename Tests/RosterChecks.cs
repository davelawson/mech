namespace Mech.Tests;

internal static class RosterChecks
{
    public static void Run(Action<bool, string> check)
    {
        void Reject(Action action, string name)
        {
            var rejected = false;
            try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
            check(rejected, name);
        }
        check(DraftCatalog.AllMechs.Length == 49 && DraftCatalog.Mech("atlas") is { Variant: "AS7-D", Tonnage: 100 }, "all 49 chassis are catalogued");
        check(DraftCatalog.Mech("atlas").Weapons.Count(weapon => weapon.Rear && weapon.Location == MechLocation.CenterTorso) == 2, "Atlas retains both stock rear-facing lasers");
        check(DraftCatalog.Warriors.Length == 120 && DraftCatalog.Warriors.Count(w => w.Rank == WarriorExperience.Regular) == 72
            && DraftCatalog.Warriors.Count(w => w.Rank == WarriorExperience.Veteran) == 24 && DraftCatalog.Warriors.Count(w => w.Rank == WarriorExperience.Green) == 24,
            "pilot catalog holds 24 Green, 72 Regular and 24 Veteran MechWarriors");
        check(DraftCatalog.Warriors.Select(w => w.Callsign).Distinct().Count() == DraftCatalog.Warriors.Length
            && DraftCatalog.Warriors.Select(w => w.Name).Distinct().Count() == DraftCatalog.Warriors.Length, "every MechWarrior has their own name and callsign");
        check(DraftCatalog.Warriors.All(w => new[] { w.Gunnery, w.Piloting, w.Sensors, w.Grit }.All(value => value is >= 2 and <= 7)
            && w.SkillPoints == w.Rank switch { WarriorExperience.Veteran => 10, WarriorExperience.Green => 4, _ => 8 }), "every authored pilot spends its quality budget across four bounded skills");
        var untrained = DraftCatalog.Warriors.Where(w => new[] { w.Gunnery, w.Piloting, w.Sensors, w.Grit }.Contains(WarriorSkills.Untrained)).ToArray();
        check(untrained.Length == 6 && untrained.All(w => w.Rank == WarriorExperience.Green && new[] { w.Gunnery, w.Piloting, w.Sensors, w.Grit }.Count(v => v == 7) == 1),
            "six Green MechWarriors each have one Untrained skill");
        check(WarriorSkills.Cost(7) == -1 && WarriorSkills.Descriptor(7) == "Untrained" && WarriorSkills.Descriptor(9) == "Untrained" && WarriorSkills.Descriptor(6) == "Green",
            "an Untrained skill reads as Untrained and gives back one point");
        check(DraftCatalog.Warrior("pell-avery").Salary == 20_000 && DraftCatalog.Warrior("mara-vale").Salary == 40_000 && DraftCatalog.Warrior("renata-kovac").Salary == 50_000
            && DraftCatalog.Warrior("bram-kettle").Salary == 20_000, "salaries are 5,000 C-bills a quarter per net skill point");
        check(DraftCatalog.Warriors.Select(w => w.Grit).Distinct().Count() >= 3 && DraftCatalog.Warriors.Any(w => w.Grit == 6),
            "Grit varies across the catalog, from Green upward");
        check(WarriorSkills.Cost(6) == 0 && WarriorSkills.Cost(4) == 2 && WarriorSkills.Cost(2) == 4,
            "skill improvements each cost one point from the novice baseline");
        Reject(() => WarriorSkills.Cost(1), "ratings better than expert rejected by skill costs");
        Reject(() => WarriorSkills.Cost(8), "ratings worse than untrained rejected by skill costs");
        check(DraftCatalog.Warriors.Any(w => w.Sensors < w.Gunnery && w.Sensors < w.Piloting)
            && DraftCatalog.Warriors.Any(w => w.Sensors == 2) && DraftCatalog.Warriors.Any(w => w.Sensors == 6),
            "catalog includes sensor specialists and both expert and novice sensor ratings");
        var seeded = InitialDraft.CreateRandom(new Random(3025));
        var replay = InitialDraft.CreateRandom(new Random(3025));
        check(seeded.ForceName == replay.ForceName && Enumerable.Range(0, 8).All(pool => seeded.Choice(pool) == replay.Choice(pool))
            && Enumerable.Range(0, 4).All(p => Enumerable.Range(0, 3).All(c => seeded.MechCandidate(p,c).Id == replay.MechCandidate(p,c).Id && seeded.WarriorCandidate(p+4,c).Id == replay.WarriorCandidate(p+4,c).Id)),
            "random draft can be reproduced with a seeded source");
        var seenMechs = new HashSet<string>(); var seenWarriors = new HashSet<string>();
        var generatedNames = new HashSet<string>();
        var generatedChoices = Enumerable.Range(0, 8).Select(_ => new HashSet<int>()).ToArray();
        for (var seed = 0; seed < 512; seed++)
        {
            var generated = InitialDraft.CreateRandom(new Random(seed));
            check(generated.Complete && generated.SelectedCount == 8 && InitialDraft.ValidName(generated.ForceName),
                "random draft has eight selections and a valid company name");
            var generatedRoster = generated.Establish();
            check(generatedRoster.Mechs.Select(item => item.Spec.Id).SequenceEqual(Enumerable.Range(0, 4)
                    .Select(pool => generated.MechCandidate(pool, generated.Choice(pool)).Id))
                && generatedRoster.Warriors.Select(item => item.Spec.Id).SequenceEqual(Enumerable.Range(4, 4)
                    .Select(pool => generated.WarriorCandidate(pool, generated.Choice(pool)).Id))
                && generatedRoster.ForceName == generated.ForceName
                && generatedRoster.Mechs.Zip(generatedRoster.Warriors).All(pair => pair.First.WarriorId == pair.Second.Id),
                "random draft establishes its selected candidates with pilots assigned by column");
            var candidates = Enumerable.Range(0,4).SelectMany(p => Enumerable.Range(0,3).Select(c => generated.MechCandidate(p,c))).ToArray();
            var pilots = Enumerable.Range(4,4).SelectMany(p => Enumerable.Range(0,3).Select(c => generated.WarriorCandidate(p,c))).ToArray();
            check(candidates.Select(m => m.Id).Distinct().Count() == 12 && pilots.Select(w => w.Id).Distinct().Count() == 12
                && Enumerable.Range(0,4).All(p => Enumerable.Range(0,3).All(c => { var t = generated.MechCandidate(p,c).Tonnage; return p == (t <= 35 ? 0 : t <= 55 ? 1 : t <= 75 ? 2 : 3); }))
                && pilots.Take(9).All(w => w.Rank == WarriorExperience.Regular) && pilots.Skip(9).All(w => w.Rank == WarriorExperience.Veteran), "draft candidates are distinct and belong to their intended pools");
            foreach (var candidate in candidates) seenMechs.Add(candidate.Id);
            foreach (var pilot in pilots) seenWarriors.Add(pilot.Id);
            generatedNames.Add(generated.ForceName);
            for (var pool = 0; pool < 8; pool++) generatedChoices[pool].Add(generated.Choice(pool));
        }
        check(generatedNames.Count > 1 && generatedChoices.All(choices => choices.SetEquals([0, 1, 2])),
            "seeded samples vary company names and reach every candidate in every pool");
        check(seenMechs.SetEquals(DraftCatalog.AllMechs.Select(m => m.Id)) && seenWarriors.SetEquals(DraftCatalog.Warriors.Where(w => w.Rank != WarriorExperience.Green).Select(w => w.Id)),
            "all catalogued mechs and every Regular and Veteran pilot can appear in the established draft");
        CheckStartingOut(check);
        CheckLances(check, Reject);
        var fresh = new Campaign();
        check(fresh.RequiresDraft && !fresh.CanAdvance && !fresh.AdvanceOneDay().Advanced, "undrafted campaign cannot advance");
        Reject(() => fresh.EstablishCompany(new InitialDraft()), "incomplete draft cannot establish a force");
        var draft = new InitialDraft { ForceName = "  Iron Lanterns  " };
        for (var p = 0; p < 8; p++) draft.Select(p, 0);
        draft.Select(0, 2); fresh.EstablishCompany(draft);
        var roster = fresh.Roster!;
        check(roster.ForceName == "Iron Lanterns" && roster.Mechs[0].Spec.Id == "panther", "draft commits revised choices and company identity");
        check(roster.ReadyWarriors == 4 && roster.Mechs.Zip(roster.Warriors).All(pair => pair.First.Operational && pair.First.WarriorId == pair.Second.Id),
            "draft starts with intact mechs and healthy pilots assigned by column");
        Reject(() => fresh.EstablishCompany(draft), "completed draft cannot reopen");
        var a = roster.Mechs[0]; var b = roster.Mechs[1]; var first = roster.Warriors[0]; var second = roster.Warriors[1];
        roster.Assign(a.Id, first.Id); roster.Assign(b.Id, second.Id);
        check(roster.ReadyWarriors == 4, "assigned healthy warriors remain counted as ready");
        Reject(() => roster.Assign(b.Id, first.Id), "occupied assignments require confirmation");
        check(a.WarriorId == first.Id && b.WarriorId == second.Id, "failed swap is atomic");
        roster.Assign(b.Id, first.Id, true);
        check(a.WarriorId == second.Id && b.WarriorId == first.Id, "swap exchanges both pilots");
        roster.Assign(a.Id, null); roster.Assign(a.Id, first.Id, true);
        check(a.WarriorId == first.Id && b.WarriorId is null, "transfer to empty mech clears former assignment");
        roster.SetHealth(first.Id, WarriorHealth.Injured, fresh.Date.AddDays(2), fresh.Date);
        check(a.WarriorId == first.Id && roster.ReadyWarriors == 3, "injury retains assignment and reduces ready count");
        first.Deployments = 3; first.Kills = 5;
        var restored = Campaign.Restore(fresh.Snapshot());
        check(restored.Roster!.Warriors[0].Deployments == 3 && restored.Roster.Warriors[0].Kills == 5, "save restores deployments and kills");
        var careerless = fresh.Snapshot(); careerless = careerless with { Roster = careerless.Roster! with { Warriors = careerless.Roster.Warriors.Select(w => new WarriorSnapshot(w.DefinitionId, w.Health, w.RecoveryDate)).ToArray() } };
        check(Campaign.Restore(careerless).Roster!.Warriors.All(w => w.Deployments == 0 && w.Kills == 0), "saves without a career record load with none");
        check(restored.Roster!.Mechs[1].WarriorId is null, "loading an established company preserves deliberately empty assignments");
        check(restored.Roster!.Warriors.All(w => w.Spec.Sensors == DraftCatalog.Warrior(w.Id).Sensors
            && w.Spec.SkillPoints == w.Spec.SkillBudget), "existing ID-based saves resolve budgeted Sensors ratings without a schema change");
        check(restored.Roster!.Mechs[0].WarriorId == first.Id && restored.Roster.Warriors[0].Health == WarriorHealth.Injured, "save restores injury, recovery and assignments");
        restored.AdvanceOneDay(); restored.AdvanceOneDay();
        check(restored.Roster.ReadyWarriors == 4 && restored.Roster.Warriors[0].RecoveryDate is null, "injury recovers on the recorded campaign date");
        roster.SetHealth(first.Id, WarriorHealth.Dead, null, fresh.Date);
        check(a.WarriorId is null && roster.Warriors.Count == 4 && roster.ReadyWarriors == 3, "death clears assignment and preserves memorial");
        Reject(() => roster.Assign(a.Id, first.Id), "dead warriors cannot be assigned");
        Reject(() => roster.SetHealth(first.Id, WarriorHealth.Healthy, null, fresh.Date), "dead warriors cannot recover");
        var snapshot = fresh.Snapshot(); var savedRoster = snapshot.Roster!;
        Reject(() => Campaign.Restore(snapshot with { Roster = savedRoster with { Mechs = [savedRoster.Mechs[0], savedRoster.Mechs[0]] } }), "duplicate mech IDs rejected");
        Reject(() => Campaign.Restore(snapshot with { Roster = savedRoster with { Warriors = [savedRoster.Warriors[0], savedRoster.Warriors[0]] } }), "duplicate warrior IDs rejected");
        var corruptMech = savedRoster.Mechs[0] with { Locations = savedRoster.Mechs[0].Locations.Select(item => item with { Armor = 999 }).ToArray() };
        Reject(() => Campaign.Restore(snapshot with { Roster = savedRoster with { Mechs = [corruptMech] } }), "impossible location armor rejected");
        corruptMech = savedRoster.Mechs[0] with { Equipment = savedRoster.Mechs[0].Equipment.Select(item => item with { Id = "unknown" }).ToArray() };
        Reject(() => Campaign.Restore(snapshot with { Roster = savedRoster with { Mechs = [corruptMech] } }), "unknown equipment state rejected");
        corruptMech = savedRoster.Mechs[0] with { WarriorId = first.Id };
        Reject(() => Campaign.Restore(snapshot with { Roster = savedRoster with { Mechs = [corruptMech] } }), "dead pilot assignment rejected on load");
        Reject(() => Campaign.Restore(snapshot with { Roster = null }), "current save must contain an established roster");
        var legacy = Campaign.Restore(snapshot with { Version = 1, Roster = null, Missions = null, Cash = 1_234_567, Date = new DateOnly(3025, 2, 10) });
        check(legacy.RequiresDraft && legacy.Cash == 1_234_567 && legacy.Date == new DateOnly(3025, 2, 10), "legacy save preserves money and date before drafting");
        legacy.EstablishCompany(draft);
        check(legacy.Cash == 1_234_567 && legacy.Roster!.ForceName == "Iron Lanterns", "legacy draft preserves finances");
        var pendingLegacy = Campaign.Restore(snapshot with
        {
            Version = 1, Roster = null, Missions = null, Date = new DateOnly(3025, 4, 1), NextQuarter = new DateOnly(3025, 7, 1), PaymentDue = Campaign.QuarterlyCharge
        });
        check(pendingLegacy.PaymentPending && !pendingLegacy.CanPay && !pendingLegacy.CanDefer, "legacy payment operations wait for draft completion");
        pendingLegacy.EstablishCompany(draft);
        check(pendingLegacy.PaymentPending && pendingLegacy.CanPay && pendingLegacy.PaymentDue == Campaign.QuarterlyCharge, "draft retains a legacy quarterly obligation");
        // Damage lives in owned state and must neither corrupt the catalog nor lose classic rear armor.
        var damaged = savedRoster.Mechs[0] with { Locations = savedRoster.Mechs[0].Locations.Select(item => item.Location == MechLocation.LeftLeg ? item with { Structure = 0 } : item).ToArray() };
        var damagedRoster = ForceRoster.Restore(savedRoster with { Mechs = [damaged] }, fresh.Date);
        check(!damagedRoster.Mechs[0].Operational && DraftCatalog.Mech(damaged.DefinitionId).Locations.Single(item => item.Location == MechLocation.LeftLeg).Structure > 0, "destroyed mobility blocks readiness without changing stock values");
        var storePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mech-draft-{Guid.NewGuid():N}.json");
        try
        {
            var store = new CampaignStore(storePath);
            Reject(() => store.Save(new Campaign()), "partial draft cannot be saved");
            check(!store.Exists, "rejected partial draft leaves no file");
            store.Save(fresh); var loaded = store.Load();
            check(loaded.Roster!.ForceName == roster.ForceName && loaded.Roster.Warriors[0].Health == WarriorHealth.Dead, "disk round trip retains roster and memorials");
        }
        finally { if (System.IO.File.Exists(storePath)) System.IO.File.Delete(storePath); }
    }

    /// <summary>The Just Starting Out draft: two light mechs over a Regular and a Green MechWarrior, on 500,000 C-bills.</summary>
    private static void CheckStartingOut(Action<bool, string> check)
    {
        for (var seed = 0; seed < 64; seed++)
        {
            var draft = InitialDraft.CreateRandom(new Random(seed), StartingScenario.StartingOut);
            var mechs = Enumerable.Range(0, 2).SelectMany(p => Enumerable.Range(0, 3).Select(c => draft.MechCandidate(p, c))).ToArray();
            check(draft.Complete && draft.Columns == 2 && draft.SelectedCount == 4 && draft.ActivePools.SequenceEqual([0, 1, 4, 5])
                && mechs.All(m => m.WeightClass == MechWeightClass.Light) && mechs.Select(m => m.Id).Distinct().Count() == 6
                && Enumerable.Range(0, 3).All(c => draft.WarriorCandidate(4, c).Rank == WarriorExperience.Regular && draft.WarriorCandidate(5, c).Rank == WarriorExperience.Green),
                "a starting-out draft offers six different light mechs, three Regulars and three Greens");
            var roster = draft.Establish();
            check(roster.Mechs.Count == 2 && roster.Warriors.Count == 2 && roster.Mechs.All(m => m.Lance == 0)
                && roster.Mechs.Zip(roster.Warriors).All(pair => pair.First.WarriorId == pair.Second.Id)
                && roster.Warriors[1].Spec.Rank == WarriorExperience.Green, "a starting-out company has two piloted light mechs in one lance");
            var offered = InitialDraft.FromOffer(draft.Offer());
            check(offered.Scenario == StartingScenario.StartingOut && offered.ActivePools.All(pool => offered.Choice(pool) == draft.Choice(pool))
                && offered.MechCandidate(1, 2).Id == draft.MechCandidate(1, 2).Id, "a starting-out draft survives being sent to another player");
        }
        var fixedDraft = new InitialDraft(StartingScenario.StartingOut) { ForceName = "Fresh Start" };
        var rejected = false;
        try { fixedDraft.Select(2, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "a starting-out draft has no Heavy pool");
        foreach (var pool in fixedDraft.ActivePools) fixedDraft.Select(pool, 0);
        var campaign = new Campaign(); campaign.EstablishCompany(fixedDraft);
        check(campaign.Cash == 500_000 && campaign.Roster!.Mechs.Count == 2, "starting out begins with 500,000 C-bills");
        var bad = fixedDraft.Offer() with { Mechs = [.. fixedDraft.Offer().Mechs.Take(5), "atlas"] };
        rejected = false;
        try { InitialDraft.FromOffer(bad); } catch (ArgumentException) { rejected = true; }
        check(rejected, "a starting-out offer with an assault mech is refused");
        var company = InitialDraft.CreateCompany(3, new Random(5), StartingScenario.StartingOut);
        var formed = InitialDraft.EstablishCompany(company);
        check(formed.LanceCount == 3 && formed.Mechs.Select(m => m.Lance).SequenceEqual(new int?[] { 0, 0, 1, 1, 2, 2 })
            && formed.Warriors.Select(w => w.Id).Distinct().Count() == 6, "a three-player starting-out company has two mechs in each player's lance");
    }

    private static void CheckLances(Action<bool, string> check, Action<Action, string> reject)
    {
        var roster = ForceRoster.Create("Lance checks", Enumerable.Repeat(DraftCatalog.Mech("jenner"), 6), DraftCatalog.Warriors.Take(6));
        check(roster.LanceCount == 1 && roster.LanceMechs(0).Count() == 4 && roster.Reserves.Select(mech => mech.Id).SequenceEqual(["mech-5", "mech-6"]),
            "a new company fills its lance with its first four mechs; the rest start in the reserves");
        reject(() => roster.SetLance("mech-5", 0), "a full lance takes no fifth mech");
        reject(() => roster.SetLance("mech-1", 1), "a mech cannot join a lance the company does not have");
        roster.SetLance("mech-1", null); roster.SetLance("mech-5", 0);
        check(roster.Mechs[0].Lance is null && roster.Mechs[4].Lance == 0, "mechs move between the lance and the reserves");
        var save = roster.Snapshot();
        check(save.Lances == 1 && ForceRoster.Restore(save, Campaign.StartDate).Mechs.Select(mech => mech.Lance).SequenceEqual(roster.Mechs.Select(mech => mech.Lance)),
            "lance membership survives a save round trip");
        var legacy = ForceRoster.Restore(save with { Lances = null, Mechs = save.Mechs.Select(mech => mech with { Lance = null }).ToArray() }, Campaign.StartDate);
        check(legacy.LanceCount == 1 && legacy.LanceMechs(0).Select(mech => mech.Id).SequenceEqual(["mech-1", "mech-2", "mech-3", "mech-4"]) && legacy.Reserves.Count() == 2,
            "saves from before lances form one lance from their first four mechs");
        reject(() => ForceRoster.Restore(save with { Mechs = save.Mechs.Select(mech => mech with { Lance = 0 }).ToArray() }, Campaign.StartDate),
            "a saved lance of more than four mechs is rejected");
        reject(() => ForceRoster.Restore(save with { Mechs = save.Mechs.Select((mech, i) => i == 0 ? mech with { Lance = 1 } : mech).ToArray() }, Campaign.StartDate),
            "a saved mech in a lance the company does not have is rejected");
        reject(() => ForceRoster.Restore(save with { Lances = ForceRoster.MaxLances + 1 }, Campaign.StartDate), "a company has at most eight lances");
        var three = ForceRoster.Create("Company", Enumerable.Repeat(DraftCatalog.Mech("jenner"), 12), DraftCatalog.Warriors.Take(12), lances: 3);
        check(three.LanceCount == 3 && Enumerable.Range(0, 3).All(lance => three.LanceMechs(lance).Count() == 4) && !three.Reserves.Any()
            && three.Controls(1, three.Mechs[4]) && !three.Controls(1, three.Mechs[0]), "each player controls their own lance and no other");
    }
}
