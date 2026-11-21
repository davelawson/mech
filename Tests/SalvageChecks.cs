using System.Text.Json;

namespace Mech.Tests;

/// <summary>Salvage and insurance settled from a mission battle.</summary>
internal static class SalvageChecks
{
    public static void Run(Action<bool, string> check)
    {
        CheckPool(check);
        CheckClaims(check);
        CheckInsurance(check);
    }

    /// <summary>A company with an accepted mission against at least two mechs for a Great House, with these salvage and insurance terms.</summary>
    private static Campaign Company(int picks, long insuranceLimit)
    {
        var campaign = CampaignChecks.NewCompany(7);
        var save = campaign.Snapshot();
        var offer = save.Missions!.Offers.First(item => item.Opposition.Length >= 2 && item.Kind != MissionKind.Patrol
            && item.Employer.Faction is { } faction && faction != Faction.ComStar);
        var changed = offer with { SalvagePicks = picks, InsuranceLimit = insuranceLimit };
        campaign = Campaign.Restore(save with { Missions = save.Missions with { Offers = [.. save.Missions.Offers.Select(item => item.Id == offer.Id ? changed : item)] } });
        campaign.AcceptMission(offer.Id);
        return campaign;
    }

    /// <summary>An enemy mech as a battle left it: listed locations lost with everything in them, listed items damaged.</summary>
    private static EnemyWreckReport Wreck(int index, string mechId, CombatStatus status, MechLocation[]? lost = null, string[]? damaged = null)
    {
        var spec = DraftCatalog.Mech(mechId); lost ??= []; damaged ??= [];
        var locations = spec.Locations.Select(loc => lost.Contains(loc.Location) ? new LocationCondition(loc.Location, 0, 0, 0)
            : new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)).ToArray();
        var items = spec.Weapons.Select(item => (item.Id, item.Location)).Concat(spec.Components.Select(item => (item.Id, item.Location)))
            .Concat(spec.Ammo.Select(item => (item.Id, item.Location)));
        var equipment = items.Select(item => lost.Contains(item.Location) ? new EquipmentCondition(item.Id, ComponentCondition.Destroyed)
            : damaged.Contains(item.Id) ? new EquipmentCondition(item.Id, ComponentCondition.Damaged, 1)
            : new EquipmentCondition(item.Id, ComponentCondition.Operational)).ToArray();
        return new($"enemy-{index + 1}", mechId, status, locations, equipment);
    }

    private static EnemyWreckReport[] Wrecks(MissionOffer offer) =>
        [.. offer.Opposition.Select((item, i) => i == 0 ? Wreck(i, item.MechId, CombatStatus.Disabled, [MechLocation.LeftLeg, MechLocation.RightLeg])
            : Wreck(i, item.MechId, CombatStatus.Destroyed, [MechLocation.CenterTorso]))];

    private static MissionReport Report(Campaign campaign, MissionOutcome outcome, SalvageClaim[] salvage, DeployedMechReport? deployed = null)
    {
        var mech = campaign.Roster!.Mechs[0];
        return new MissionReport(outcome, [deployed ?? new(mech.Id, mech.Locations.ToArray(), mech.Equipment.ToArray(), false, 0)], 0)
        {
            Enemies = Wrecks(campaign.Missions.Current!), Salvage = salvage
        };
    }

    private static void CheckPool(Action<bool, string> check)
    {
        var campaign = Company(2, 0); var offer = campaign.Missions.Current!;
        var disabled = DraftCatalog.Mech(offer.Opposition[0].MechId);
        var weapon = disabled.Weapons.First(item => item.Location is not (MechLocation.LeftLeg or MechLocation.RightLeg));
        var report = new MissionReport(MissionOutcome.Success, [], 0)
        {
            Enemies = [Wreck(0, disabled.Id, CombatStatus.Disabled, [MechLocation.LeftLeg, MechLocation.RightLeg], [weapon.Id]), .. Wrecks(offer).Skip(1)]
        };
        var pool = Salvage.Pool(report);
        var first = pool.Where(option => option.UnitId == "enemy-1").ToArray();
        check(first.Count(option => option.WholeMech) == 1 && first.Single(option => option.WholeMech).Picks == SalvageTuning.Picks(disabled.WeightClass),
            "a disabled enemy can be claimed whole, priced by its weight class");
        check(first.All(option => option.ItemId != weapon.Id), "a damaged item is not salvage");
        var legs = disabled.Weapons.Select(item => (item.Id, item.Location)).Concat(disabled.Ammo.Select(item => (item.Id, item.Location)))
            .Concat(disabled.Components.Select(item => (item.Id, item.Location))).Where(item => item.Location is MechLocation.LeftLeg or MechLocation.RightLeg)
            .Select(item => item.Id).ToHashSet();
        check(first.All(option => option.ItemId is null || !legs.Contains(option.ItemId)), "nothing in a lost location is salvage");
        check(first.Where(option => !option.WholeMech).All(option => option.Picks == 1 && option.StorageId is not null
            && Repairs.Storable(disabled, option.ItemId!) && StorageCatalog.Describe(option.StorageId) is not null),
            "each working weapon, bin, heat sink or jump jet is one pick into storage");
        var destroyed = pool.Where(option => option.UnitId == "enemy-2").ToArray();
        check(destroyed.Length > 0 && destroyed.All(option => !option.WholeMech), "a destroyed enemy leaves items but cannot be claimed whole");
        check(Salvage.Pool(report with { Enemies = [] }).Count == 0, "a report without enemies has nothing to salvage");
        check(Salvage.Pool(report with { EnemyHoldsField = true }).Count == 0, "a field the opposition still holds has nothing to salvage");
    }

    private static void CheckClaims(Action<bool, string> check)
    {
        var probe = Company(2, 0); var pool = Salvage.Pool(Report(probe, MissionOutcome.Success, []));
        var items = pool.Where(option => !option.WholeMech).ToArray();
        var whole = pool.Single(option => option.WholeMech);
        var stripped = items.First(option => option.UnitId == whole.UnitId);
        Refused(() => Company(2, 0).SettleMission(Report(probe, MissionOutcome.Success, [items[0].Claim, items[0].Claim])), check, "a pick cannot be claimed twice");
        Refused(() => Company(6, 0).SettleMission(Report(probe, MissionOutcome.Success, [whole.Claim, stripped.Claim])), check,
            "a mech claimed whole cannot also be stripped");
        Refused(() => Company(2, 0).SettleMission(Report(probe, MissionOutcome.Success, [.. items.Take(2 + SalvageTuning.ExtraPicks + 1).Select(option => option.Claim)])),
            check, "picks stop at the allotment plus the extra picks");
        Refused(() => Company(2, 0).SettleMission(Report(probe, MissionOutcome.Failure, [items[0].Claim])), check, "a failed mission takes no salvage");
        Refused(() => Company(2, 0).SettleMission(Report(probe, MissionOutcome.Success, [new("enemy-1", "nothing")])), check, "salvage must be on the field");
        var wrong = Company(2, 0);
        Refused(() => wrong.SettleMission(Report(wrong, MissionOutcome.Success, []) with { Enemies = [Wreck(0, "atlas", CombatStatus.Disabled)] }), check,
            "the reported enemies must be the mission's opposition");

        // Items into storage, a mech whole, and two picks over the allotment.
        var campaign = Company(whole.Picks + 1, 0); var roster = campaign.Roster!; var offer = campaign.Missions.Current!;
        var taken = items.Where(option => option.UnitId != whole.UnitId).Take(offer.SalvagePicks - whole.Picks + 2).ToArray();
        check(taken.Length == offer.SalvagePicks - whole.Picks + 2, "the destroyed enemy has enough items to take");
        var before = taken.Select(option => option.StorageId!).Distinct().ToDictionary(id => id, roster.Stored);
        var mechs = roster.Mechs.Count;
        var conduct = new MissionConduct(MissionOutcome.Success, [roster.Mechs[0].Definition.Id], 0, 0, 0, offer.SalvagePicks + 2, 0);
        var expected = Reputation.MissionChanges(offer, conduct);
        var allotted = Reputation.MissionChanges(offer, conduct with { SalvagePicksTaken = offer.SalvagePicks });
        var result = campaign.SettleMission(Report(campaign, MissionOutcome.Success, [whole.Claim, .. taken.Select(option => option.Claim)]));
        check(result.SalvagePicks == offer.SalvagePicks + 2 && result.ExtraPicks == 2, "settlement counts the picks used and those over the allotment");
        check(before.All(pair => roster.Stored(pair.Key) == pair.Value + taken.Count(option => option.StorageId == pair.Key))
            && result.SalvagedItems.Order().SequenceEqual(taken.Select(option => option.StorageId!).Order()), "salvaged items join storage");
        var claimed = roster.Mechs.Single(item => item.Id == result.SalvagedMechs.Single());
        var wreck = Wrecks(offer)[0];
        check(roster.Mechs.Count == mechs + 1 && claimed.Definition.Id == wreck.MechId && claimed.Lance is null && claimed.WarriorId is null
            && roster.Mechs.Select(item => item.Id).Distinct().Count() == roster.Mechs.Count, "a mech claimed whole joins the reserves with no pilot and its own id");
        check(!claimed.LocationIntact(MechLocation.LeftLeg) && !claimed.Operational
            && claimed.Ammo.All(bin => bin.Shots == claimed.Spec.Ammo.Single(stock => stock.Id == bin.Id).Shots),
            "a salvaged mech keeps its battle damage, with its bins refilled");
        var employer = offer.Employer.Faction!.Value;
        check(result.Reputation.OrderBy(pair => pair.Key).SequenceEqual(expected.OrderBy(pair => pair.Key))
            && expected[employer] - allotted[employer] == 2 * ReputationTuning.EmployerSalvagePerPick
            && expected.GetValueOrDefault(Faction.ComStar) - allotted.GetValueOrDefault(Faction.ComStar) == 2 * ReputationTuning.ComStarSalvagePerPick,
            "each pick over the allotment costs the employer and ComStar reputation");
        var restored = Campaign.Restore(campaign.Snapshot());
        check(JsonSerializer.Serialize(restored.Snapshot()) == JsonSerializer.Serialize(campaign.Snapshot()), "a campaign with salvage survives restoration");

        var declined = Company(2, 0); var count = declined.Roster!.Mechs.Count; var storage = JsonSerializer.Serialize(declined.Roster.Storage);
        var none = declined.SettleMission(Report(declined, MissionOutcome.Success, []));
        check(none.SalvagePicks == 0 && declined.Roster.Mechs.Count == count && JsonSerializer.Serialize(declined.Roster.Storage) == storage,
            "taking nothing leaves the hangar as it was");
    }

    private static void CheckInsurance(Action<bool, string> check)
    {
        var untouched = Company(0, 500_000); var cash = untouched.Cash;
        var clean = untouched.SettleMission(Report(untouched, MissionOutcome.Success, []));
        check(clean.InsuranceClaim == 0 && clean.Insurance == 0 && untouched.Cash == cash + clean.Payment, "an undamaged force claims no insurance");

        // A destroyed weapon is replaced at market price plus installation; armor is priced as a repair.
        var hurt = Company(0, 5_000_000); var mech = hurt.Roster!.Mechs[0];
        var weapon = mech.Spec.Weapons[0];
        var equipment = mech.Equipment.Select(item => item.Id == weapon.Id ? new EquipmentCondition(item.Id, ComponentCondition.Destroyed) : item).ToArray();
        cash = hurt.Cash;
        var destroyed = hurt.SettleMission(Report(hurt, MissionOutcome.Failure, [], new(mech.Id, mech.Locations.ToArray(), equipment, false, 0)));
        var replacement = EquipmentMarket.Price(StorageCatalog.WeaponId(weapon.Kind)) + EquipmentRules.Slots(weapon.Kind) * RepairTuning.InstallPricePerSlot;
        check(destroyed.InsuranceClaim == replacement && destroyed.Insurance == replacement && hurt.Cash == cash + replacement,
            $"a destroyed weapon is insured at market price plus installation, even on a failed mission ({destroyed.InsuranceClaim} for {replacement})");

        var armored = Company(0, 5_000_000); mech = armored.Roster!.Mechs[0];
        var locations = mech.Locations.Select(loc => loc with { Armor = 0 }).ToArray();
        var armor = armored.SettleMission(Report(armored, MissionOutcome.Success, [], new(mech.Id, locations, mech.Equipment.ToArray(), false, 0)));
        check(armor.InsuranceClaim > 0 && armor.InsuranceClaim == Insurance.RepairValue(mech), "insurance pays what repairing the battle's damage costs");

        // Damage brought into the battle is not insured; the payout is capped at the contract's limit.
        var worn = Company(0, 5_000_000); mech = worn.Roster!.Mechs[0];
        mech.ApplyBattle(locations, mech.Equipment);
        var old = worn.SettleMission(Report(worn, MissionOutcome.Success, [], new(mech.Id, locations, mech.Equipment.ToArray(), false, 0)));
        check(old.InsuranceClaim == 0 && old.Insurance == 0, "damage the mech carried into the battle is not insured");
        var capped = Company(0, 1_000); mech = capped.Roster!.Mechs[0];
        var limited = capped.SettleMission(Report(capped, MissionOutcome.Success, [], new(mech.Id, locations, mech.Equipment.ToArray(), false, 0)));
        check(limited.InsuranceClaim > 1_000 && limited.Insurance == 1_000, "insurance pays at most the contract's limit");
    }

    private static void Refused(Action action, Action<bool, string> check, string message)
    {
        var refused = false;
        try { action(); }
        catch (ArgumentException) { refused = true; }
        check(refused, message);
    }
}
