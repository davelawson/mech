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
        check(DraftCatalog.AllMechs.Count == 49 && DraftCatalog.Mech("atlas") is { Variant: "AS7-D", Tonnage: 100 }, "all 49 chassis are catalogued");
        check(DraftCatalog.Mech("atlas").Weapons.Count(weapon => weapon.Rear && weapon.Location == MechLocation.CenterTorso) == 2, "Atlas retains both stock rear-facing lasers");
        check(DraftCatalog.Warriors.Count == 24 && DraftCatalog.Warriors.Count(w => w.Experience.StartsWith("Regular")) == 18
            && DraftCatalog.Warriors.Count(w => w.Experience.StartsWith("Veteran")) == 6, "expanded pilot catalog preserves regular and veteran balance");
        check(DraftCatalog.Warriors.All(w => new[] { w.Gunnery, w.Piloting, w.Sensors }.All(value => value is >= 2 and <= 6)
            && w.SkillPoints == (w.Experience.StartsWith("Veteran") ? 8 : 6)), "every authored pilot spends its quality budget across three bounded skills");
        check(WarriorSkills.Cost(6) == 0 && WarriorSkills.Cost(4) == 2 && WarriorSkills.Cost(2) == 4,
            "skill improvements each cost one point from the novice baseline");
        Reject(() => WarriorSkills.Cost(1), "ratings better than expert rejected by skill costs");
        Reject(() => WarriorSkills.Cost(7), "ratings worse than novice rejected by skill costs");
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
                && generatedRoster.ForceName == generated.ForceName && generatedRoster.Mechs.All(item => item.WarriorId is null),
                "random draft establishes its selected candidates from each pool with unassigned pilots");
            var candidates = Enumerable.Range(0,4).SelectMany(p => Enumerable.Range(0,3).Select(c => generated.MechCandidate(p,c))).ToArray();
            var pilots = Enumerable.Range(4,4).SelectMany(p => Enumerable.Range(0,3).Select(c => generated.WarriorCandidate(p,c))).ToArray();
            check(candidates.Select(m => m.Id).Distinct().Count() == 12 && pilots.Select(w => w.Id).Distinct().Count() == 12
                && Enumerable.Range(0,4).All(p => Enumerable.Range(0,3).All(c => { var t = generated.MechCandidate(p,c).Tonnage; return p == (t <= 35 ? 0 : t <= 55 ? 1 : t <= 75 ? 2 : 3); }))
                && pilots.Take(9).All(w => w.Experience.StartsWith("Regular")) && pilots.Skip(9).All(w => w.Experience.StartsWith("Veteran")), "draft candidates are distinct and belong to their intended pools");
            foreach (var candidate in candidates) seenMechs.Add(candidate.Id);
            foreach (var pilot in pilots) seenWarriors.Add(pilot.Id);
            generatedNames.Add(generated.ForceName);
            for (var pool = 0; pool < 8; pool++) generatedChoices[pool].Add(generated.Choice(pool));
        }
        check(generatedNames.Count > 1 && generatedChoices.All(choices => choices.SetEquals([0, 1, 2])),
            "seeded samples vary company names and reach every candidate in every pool");
        check(seenMechs.SetEquals(DraftCatalog.AllMechs.Select(m => m.Id)) && seenWarriors.SetEquals(DraftCatalog.Warriors.Select(w => w.Id)), "all catalogued mechs and pilots can appear in the draft");
        var fresh = new Campaign();
        check(fresh.RequiresDraft && !fresh.CanAdvance && !fresh.AdvanceOneDay().Advanced, "undrafted campaign cannot advance");
        Reject(() => fresh.EstablishCompany(new InitialDraft()), "incomplete draft cannot establish a force");
        var draft = new InitialDraft { ForceName = "  Iron Lanterns  " };
        for (var p = 0; p < 8; p++) draft.Select(p, 0);
        draft.Select(0, 2); fresh.EstablishCompany(draft);
        var roster = fresh.Roster!;
        check(roster.ForceName == "Iron Lanterns" && roster.Mechs[0].Spec.Id == "panther", "draft commits revised choices and company identity");
        check(roster.ReadyWarriors == 4 && roster.Mechs.All(item => item.Operational && item.WarriorId is null), "draft starts with healthy unassigned pilots and intact mechs");
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
        var restored = Campaign.Restore(fresh.Snapshot());
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
}
