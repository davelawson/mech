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

        var buyer = NewCompany(); var buyerMech = buyer.Roster!.Mechs.First(item => item.Spec.Ammo.Any(Sold)); var buyerBin = buyerMech.Spec.Ammo.First(Sold);
        bool Sold(AmmoSpec bin) => MarketTuning.Spec(StorageCatalog.AmmoId(bin.AmmoKind))!.Value.Tech <= SystemRatings.Tech(StarChart.Instance[buyer.Travel.OrbitSystemId]);
        buyer = WithMarket(buyer, new StorageEntry(StorageCatalog.AmmoId(buyerBin.AmmoKind), 1)); buyerMech = buyer.Roster!.Mechs.Single(item => item.Id == buyerMech.Id);
        var swap = buyerMech.Plan with { Removed = buyerMech.Removed.Add(buyerBin.Id), Installed = [new(Loadout.NextId([]), StorageCatalog.AmmoId(buyerBin.AmmoKind), buyerBin.Location)] };
        var broke = Campaign.Restore(buyer.Snapshot() with { Cash = 0 }); var brokeStorage = broke.Roster!.Storage.ToArray();
        Throws(() => broke.ApplyLoadout(buyerMech.Id, swap, [StorageCatalog.AmmoId(buyerBin.AmmoKind)]), check, "reject an unaffordable purchase");
        check(broke.Roster.Storage.SequenceEqual(brokeStorage) && broke.Roster.Mechs.First(item => item.Id == buyerMech.Id).Installed.IsEmpty,
            "a refused purchase changes nothing");
        var binStored = buyer.Roster.Storage.Single(item => item.Id == StorageCatalog.AmmoId(buyerBin.AmmoKind)).Count;
        buyer.ApplyLoadout(buyerMech.Id, swap, [StorageCatalog.AmmoId(buyerBin.AmmoKind)]);
        check(buyer.Cash == Campaign.StartingCash - EquipmentMarket.Price(StorageCatalog.AmmoId(buyerBin.AmmoKind)) && buyerMech.Installed.Length == 1
            && buyer.Roster.Storage.Single(item => item.Id == StorageCatalog.AmmoId(buyerBin.AmmoKind)).Count == binStored + 1,
            "applying a loadout pays for its purchases, which install without drawing on storage");
        check(buyer.OrbitMarket!.Stock(StorageCatalog.AmmoId(buyerBin.AmmoKind)) == 0, "an applied purchase leaves the market");
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
        var worn = NewCompany().Snapshot();
        worn = worn with { Roster = worn.Roster! with { Mechs = worn.Roster.Mechs.Select((mech, i) => i > 0 ? mech : mech with
            { Locations = mech.Locations.Select((loc, j) => j > 0 ? loc : loc with { Armor = 30, Structure = 10 }).ToArray() }).ToArray() } };
        var migrated = Campaign.Restore(Legacy(worn, 4)).Roster!.Snapshot();
        check(worn.Version == Campaign.SaveVersion && migrated.Mechs.Zip(worn.Roster!.Mechs).All(pair => pair.First.Locations.SequenceEqual(pair.Second.Locations)),
            "version 4 saves count armor and structure in tenths of a point after loading");
        Throws(() => Campaign.Restore(worn with { Version = 4 }), check, "a version 4 save cannot hold more than stock protection once rescaled");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { Cash = -1 }), check, "reject corrupt finance state");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { NextQuarter = campaign.Date }), check, "reject invalid payment recurrence");

        TestStorage(check); TestSaveLibrary(check);
        TestLances(check);
        TestRepairs(check);
        TestMarkets(check);
    }

    /// <summary>The campaign with the orbited system's market holding exactly these items, last refreshed today.</summary>
    internal static Campaign WithMarket(Campaign campaign, params StorageEntry[] items) => Campaign.Restore(campaign.Snapshot() with
    { Markets = [new MarketSnapshot(campaign.Travel.OrbitSystemId, campaign.Date, items)] });

    private static void TestMarkets(Action<bool, string> check)
    {
        StarSystem Named(string name) => StarChart.Instance.Systems.Single(item => item.Name == name);
        check(SystemRatings.Tech(Named("Galatea")) == TechLevel.Regressed && SystemRatings.Capacity(Named("Galatea")) == IndustrialCapacity.Developing
            && SystemRatings.Text(Named("Galatea")) == "Regressed tech · Developing", "Galatea (D-D-C-D-D) is a Regressed, Developing world");
        check(SystemRatings.Tech(Named("Terra")) == TechLevel.StarLeague && SystemRatings.Tech(Named("Tharkad")) == TechLevel.StarLeague
            && SystemRatings.Tech(Named("Luthien")) == TechLevel.Advanced && SystemRatings.Tech(Named("Coventry")) == TechLevel.Standard,
            "Star League worlds rise one level above their technology grade");
        check(SystemRatings.Capacity(Named("Hesperus II")) == IndustrialCapacity.FactoryWorld && SystemRatings.Capacity(Named("Luthien")) == IndustrialCapacity.FactoryWorld
            && SystemRatings.Capacity(Named("Tharkad")) == IndustrialCapacity.MajorIndustrial, "factory worlds hold the most; output B is Major Industrial");
        var empty = StarChart.Instance.Systems.First(item => item.Climate!.SocioIndustrial.StartsWith('X'));
        check(!SystemRatings.HasMarket(empty) && SystemRatings.Text(empty) == "No market", "an uninhabited world has no market");
        check(MarketTuning.Stock(TechLevel.Primitive).All(item => MarketTuning.Spec(item.Id)!.Value.Tech == TechLevel.Primitive)
            && MarketTuning.Stock(TechLevel.StarLeague).Count == StorageCatalog.All.Length
            && !MarketTuning.Stock(TechLevel.Standard).Any(item => item.Id == StorageCatalog.WeaponId(WeaponKind.ERLargeLaser) || item.Id == StorageCatalog.HeatSinkId(HeatSinkKind.Double))
            && MarketTuning.Spec(StorageCatalog.AmmoId(AmmoKind.SRM4))!.Value.Weight == 2 * MarketTuning.Spec(StorageCatalog.WeaponId(WeaponKind.SRM4))!.Value.Weight,
            "markets stock only what their technology allows, ammunition at twice its weapon's weight");
        check(StorageCatalog.All.All(item => EquipmentMarket.Price(item.Id) > 0) && EquipmentMarket.Price(StorageCatalog.WeaponId(WeaponKind.PPC)) == 200_000,
            "every item has a base price");
        check(SystemMarket.Rollover(50, 0) == 0 && SystemMarket.Rollover(50, 1) == 3 && SystemMarket.Rollover(100, 14) == 51 && SystemMarket.Rollover(20, 1000) == 20,
            "rollover replaces 1 − 0.95^days of the market, rounded");
        var factory = SystemMarket.Generate(Named("New Avalon"), Campaign.StartDate, new Random(3));
        check(factory.Count == 300 && factory.Items.Sum(item => item.Count) == 300, "a factory world stocks 300 items");

        var campaign = NewCompany(); var home = campaign.Travel.OrbitSystemId;
        check(campaign.Market(home) is null && campaign.OrbitMarket is null, "a market is not stocked before it is viewed");
        campaign.ViewMarket();
        var market = campaign.OrbitMarket!;
        check(market.Count == 50 && market.Refreshed == campaign.Date && market.Items.All(item => MarketTuning.Spec(item.Id)!.Value.Tech <= TechLevel.Regressed),
            "viewing a market stocks it to its size with what its technology allows");
        var first = System.Text.Json.JsonSerializer.Serialize(market.Items);
        campaign.ViewMarket();
        check(System.Text.Json.JsonSerializer.Serialize(market.Items) == first, "viewing again on the same day changes nothing");
        var twin = Campaign.Restore(campaign.Snapshot() with { Markets = null }); twin.ViewMarket();
        check(System.Text.Json.JsonSerializer.Serialize(twin.OrbitMarket!.Items) == first, "the same campaign stocks the same way");

        var item = market.Items[0].Id; var price = EquipmentMarket.Price(item); var cash = campaign.Cash; var stored = campaign.Roster!.Stored(item);
        var held = market.Stock(item);
        campaign.BuyToStorage(item, 1);
        check(campaign.Cash == cash - price && campaign.Roster.Stored(item) == stored + 1 && market.Stock(item) == held - 1 && market.Count == 49,
            "buying into storage pays at once, adds a spare and leaves a gap");
        Throws(() => campaign.BuyToStorage(item, held), check, "the market cannot sell more than it holds");
        check(market.Shortage(Enumerable.Repeat(item, held)) == $"Only {held - 1} {StorageCatalog.Describe(item)!.Name} left in the market", "a shortage names what is left");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { Cash = 0 }).BuyToStorage(item, 1), check, "a purchase must be affordable");
        var saved = Campaign.Restore(campaign.Snapshot());
        check(System.Text.Json.JsonSerializer.Serialize(saved.OrbitMarket!.Items) == System.Text.Json.JsonSerializer.Serialize(market.Items)
            && saved.OrbitMarket.Refreshed == market.Refreshed && saved.MarketSeed == campaign.MarketSeed, "markets survive a save");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { Markets = [new(home, campaign.Date, [new(StorageCatalog.WeaponId(WeaponKind.PPC), 1)])] }), check,
            "a save cannot stock what the world's technology does not allow");

        // A day later the gap fills first: 3 items roll over, one into the gap and two in place of held items.
        var before = market.Items.ToDictionary(entry => entry.Id, entry => entry.Count);
        campaign.AdvanceOneDay(); campaign.ViewMarket();
        var kept = market.Items.Sum(entry => Math.Min(entry.Count, before.GetValueOrDefault(entry.Id)));
        check(market.Count == 50 && market.Refreshed == campaign.Date && kept >= 47, "a day's rollover refills the gap before replacing held items");
        var sold = Campaign.Restore(campaign.Snapshot()); sold.BuyToStorage(sold.OrbitMarket!.Items[0].Id, 1);
        foreach (var entry in sold.OrbitMarket.Items.ToArray()) sold.OrbitMarket.Sell([.. Enumerable.Repeat(entry.Id, entry.Count)]);
        sold.AdvanceOneDay(); sold.ViewMarket();
        check(sold.OrbitMarket.Count == 3, "an emptied market refills at the daily rate");
        var coop = TwoPlayerCompany(); coop.Execute(new ViewMarketCommand(), 1);
        check(coop.OrbitMarket is not null, "any player viewing the market stocks it");
    }

    /// <summary>Damages a mech: front armor stripped from every intact location, plus the given structure and component damage.</summary>
    internal static void Damage(OwnedMech mech, int structure = 0, int damaged = 0, int destroyed = 0)
    {
        var locations = mech.Locations.Select(item => item with { Armor = 0,
            Structure = item.Location == MechLocation.LeftArm ? Math.Max(0, item.Structure - structure) : item.Structure }).ToArray();
        var equipment = mech.Equipment.Select((item, i) => item with { Condition = i < damaged ? ComponentCondition.Damaged
            : i < damaged + destroyed ? ComponentCondition.Destroyed : item.Condition }).ToArray();
        mech.ApplyBattle(locations, equipment);
    }

    private static void TestRepairs(Action<bool, string> check)
    {
        // Six mechs, so a fifth order finds every bay busy.
        var company = NewCompany().Snapshot();
        var spares = company.Roster!.Mechs.Take(2).Select((item, i) => item with { Id = $"mech-spare-{i}", WarriorId = null, Lance = null });
        var campaign = Campaign.Restore(company with { Roster = company.Roster with { Mechs = [.. company.Roster.Mechs, .. spares] } });
        var roster = campaign.Roster!; var mech = roster.Mechs[0];
        check(Repairs.Estimate(mech, RepairScope.Armor) is null && Repairs.Estimate(mech, RepairScope.All) is null, "an intact mech has nothing to repair");
        Throws(() => campaign.OrderRepair(mech.Id, RepairScope.All), check, "an intact mech cannot be ordered a repair");

        // Armor: 4 t a day at 10,000 C-bills a ton; structure: 1 t a day at 40,000.
        var front = mech.Spec.Locations.Sum(item => item.Armor); var armorTons = front / 160d;
        Damage(mech);
        var armor = Repairs.Estimate(mech, RepairScope.Armor)!;
        check(armor.Days == Math.Max(1, (int)Math.Ceiling(armorTons / 4 - 1e-9)) && armor.Cost == (long)Math.Ceiling(armorTons * 10_000 - 1e-6),
            $"armor repairs take a day per 4 t and cost 10,000 C-bills a ton ({armor.Days} days, {armor.Cost})");
        check(Repairs.Estimate(mech, RepairScope.All) is { } same && same.Days == armor.Days && same.Cost == armor.Cost,
            "with only armor lost, both repairs cost the same");

        // A full repair: structure by the ton, critical systems by damage level, a destroyed weapon replaced from a spare.
        var systems = roster.Mechs[1]; var spec = systems.Spec;
        var engine = spec.Components.First(item => item.Kind == ComponentKind.FusionEngine);
        var gyro = spec.Components.First(item => item.Kind == ComponentKind.Gyro);
        var lost = spec.Weapons[0]; var lostSlots = EquipmentRules.Slots(lost.Kind); var lostPart = StorageCatalog.WeaponId(lost.Kind);
        var worn = spec.Weapons.Skip(1).FirstOrDefault(item => EquipmentRules.Slots(item.Kind) >= 2);
        Damage(systems, structure: 10);
        systems.ApplyBattle(systems.Locations, systems.Equipment.Select(item => item.Id == engine.Id ? item with { Condition = ComponentCondition.Damaged, Hits = 2 }
            : item.Id == gyro.Id || item.Id == lost.Id ? item with { Condition = ComponentCondition.Destroyed }
            : item.Id == worn?.Id ? item with { Condition = ComponentCondition.Damaged, Hits = 1 } : item));
        var rating = RepairTuning.EngineRating(spec);
        var structureTons = 10d / spec.StructureTotal * systems.Definition.Tonnage * .1; var systemsArmorTons = spec.Locations.Sum(item => item.Armor) / 160d;
        var expectedDays = systemsArmorTons / 4 + structureTons + 2 * 1 + 2 * 1 + lostSlots * .5 + (worn is null ? 0 : .5);
        var expectedCost = systemsArmorTons * 10_000 + structureTons * 40_000 + 2 * rating / 400d * 100_000 + 2 * systems.Definition.Tonnage / 100d * 50_000
            + lostSlots * 10_000 + (worn is null ? 0 : EquipmentMarket.Price(StorageCatalog.WeaponId(worn.Kind)) / (double)EquipmentRules.Slots(worn.Kind));
        var all = Repairs.Estimate(systems, RepairScope.All, roster.Stored)!;
        check(rating > 0 && all.Days == (int)Math.Ceiling(expectedDays - 1e-9) && all.Cost == (long)Math.Ceiling(expectedCost - 1e-6)
            && all.Parts.SequenceEqual([lostPart]) && all.Missing.Count == 0,
            $"a full repair prices structure, engine and gyro levels, a worn weapon's hit slots and a replaced weapon ({all.Days} days, {all.Cost})");
        check(all.Lines.Contains($"{engine.Name} · 2 levels") && all.Lines.Contains($"Parts used: {StorageCatalog.Describe(lostPart)!.Name} ×1"),
            "a full repair lists its work");
        var short_ = Repairs.Estimate(systems, RepairScope.All, _ => 0)!;
        check(short_.Doable && short_.Missing.SequenceEqual([lost.Id]) && short_.Days == (int)Math.Ceiling(expectedDays - lostSlots * .5 - 1e-9)
            && short_.Lines.Contains($"Missing parts: {StorageCatalog.Describe(lostPart)!.Name} ×1"), "a destroyed weapon with no spare is left out of the repair");
        var cut = roster.Mechs[2];
        Damage(cut, structure: 1000);
        var lostArm = cut.Spec.Locations.Single(item => item.Location == MechLocation.LeftArm);
        check(Repairs.Estimate(cut, RepairScope.Armor)!.Cost == (long)Math.Ceiling((cut.Spec.Locations.Sum(item => item.Armor) - lostArm.Armor) / 160d * 10_000 - 1e-6),
            "an armor repair skips a destroyed location");
        var bare = NewCompany().Roster!.Mechs[0];
        bare.ApplyBattle(bare.Locations, bare.Equipment.Select(item => item.Id == bare.Spec.Weapons[0].Id ? item with { Condition = ComponentCondition.Destroyed } : item));
        check(Repairs.Estimate(bare, RepairScope.All, _ => 0) is { Doable: false } nothing && nothing.MissingParts.Count == 1,
            "a repair that only lacks parts cannot be done");

        // Ordering: paid when a bay starts it; a fifth order waits for a bay.
        var cash = campaign.Cash;
        campaign.OrderRepair(mech.Id, RepairScope.Armor);
        check(mech.UnderRepair && mech.Repair!.Started == campaign.Date && campaign.Cash == cash - armor.Cost && campaign.ActiveRepairs == 1,
            "a repair starts at once in a free bay and is paid for then");
        Throws(() => campaign.OrderRepair(mech.Id, RepairScope.All), check, "a mech holds one repair order");
        check(CombatSimulation.UnavailableReason(roster, mech)!.Contains("Under repair"), "a mech under repair cannot deploy");
        Throws(() => campaign.ApplyLoadout(mech.Id, mech.Plan with { Armor = [] }, []), check, "a mech under repair cannot change its loadout");
        check(campaign.NextEventDate == mech.Repair!.Completes, "the calendar stops when a repair completes");
        foreach (var other in roster.Mechs.Skip(1).Take(4).Where(item => item != systems && item != cut)) Damage(other);
        var spareCount = roster.Stored(lostPart);
        foreach (var other in new[] { systems, cut }.Concat(roster.Mechs.Skip(3).Take(2))) campaign.OrderRepair(other.Id, RepairScope.All);
        check(roster.Stored(lostPart) == spareCount - 1 && systems.Repair!.Parts!.SequenceEqual([lostPart]), "ordering a repair takes its parts from storage");
        var queued = roster.Mechs[4];
        check(campaign.ActiveRepairs == 4 && queued.Repair is { Started: null } && campaign.RepairsAhead(queued) == 0
            && CombatSimulation.UnavailableReason(roster, queued)!.Contains("Repair queued"), "a fifth order queues for a bay");
        var saved = Campaign.Restore(campaign.Snapshot());
        check(System.Text.Json.JsonSerializer.Serialize(saved.Snapshot().Roster) == System.Text.Json.JsonSerializer.Serialize(campaign.Snapshot().Roster),
            "repair orders survive a save");
        Throws(() => Campaign.Restore(campaign.Snapshot() with { Roster = campaign.Snapshot().Roster! with { Mechs = [.. campaign.Snapshot().Roster!.Mechs
            .Select(item => item.Repair is null ? item : item with { Repair = item.Repair with { Started = campaign.Date } })] } }), check,
            "a save cannot have more repairs started than bays");

        // Cancelling: a queued order costs nothing; work in a bay forfeits its payment and frees the bay.
        var before = campaign.Cash;
        campaign.CancelRepair(queued.Id);
        check(queued.Repair is null && campaign.Cash == before, "cancelling a queued repair costs nothing");
        campaign.OrderRepair(queued.Id, RepairScope.All);
        campaign.CancelRepair(cut.Id);
        check(cut.Repair is null && cut.InternalsDamaged && queued.UnderRepair && campaign.Cash == before - queued.Repair!.Cost,
            "cancelling started work keeps the damage and the payment, and the next order takes the bay");

        // Completion restores the mech and reports.
        var done = mech.Repair!.Completes!.Value; var systemsDone = systems.Repair!.Completes!.Value; CampaignEvent[] events = [];
        while (campaign.Date < done) events = [.. campaign.AdvanceOneDay().Events];
        check(mech.Repair is null && !mech.ArmorDamaged && events.Any(item => item.Kind == CampaignEventKind.RepairCompletion && item.Title.Contains(mech.Spec.DisplayName)),
            "a finished repair restores the armor and reports on its day");
        if (campaign.Date < systemsDone) AdvanceTo(campaign, systemsDone);
        check(!systems.Damaged && systems.Operational, "a full repair returns the mech intact");
        var held = NewCompany(); var heldMech = held.Roster!.Mechs[0]; var heldWeapon = heldMech.Spec.Weapons[0];
        Damage(heldMech);
        heldMech.ApplyBattle(heldMech.Locations, heldMech.Equipment.Select(item => item.Id == heldWeapon.Id ? item with { Condition = ComponentCondition.Destroyed } : item));
        var heldPart = StorageCatalog.WeaponId(heldWeapon.Kind); var heldSpares = held.Roster.Stored(heldPart);
        held.OrderRepair(heldMech.Id, RepairScope.All, new Dictionary<string, int>());
        check(heldMech.Repair!.Missing!.SequenceEqual([heldWeapon.Id]) && held.Roster.Stored(heldPart) == heldSpares,
            "spares claimed by pending changes are not used");
        held.CancelRepair(heldMech.Id);
        held.OrderRepair(heldMech.Id, RepairScope.All);
        check(held.Roster.Stored(heldPart) == heldSpares - 1, "an order takes an available spare");
        held.CancelRepair(heldMech.Id);
        check(held.Roster.Stored(heldPart) == heldSpares, "cancelling started work returns its parts");
        held.OrderRepair(heldMech.Id, RepairScope.All, new Dictionary<string, int>());
        AdvanceTo(held, heldMech.Repair!.Completes!.Value);
        check(heldMech.Condition(heldWeapon.Id) == ComponentCondition.Destroyed && !heldMech.ArmorDamaged, "a destroyed item with no spare stays destroyed");

        var poor = NewCompany(); Damage(poor.Roster!.Mechs[0]);
        poor = Campaign.Restore(poor.Snapshot() with { Cash = 1 });
        Throws(() => poor.OrderRepair(poor.Roster!.Mechs[0].Id, RepairScope.Armor), check, "a repair must be affordable to order");
        var coop = TwoPlayerCompany(); Damage(coop.Roster!.Mechs[3]);
        Throws(() => coop.Execute(new OrderRepairCommand(coop.Roster.Mechs[3].Id, RepairScope.All), 0), check, "a player cannot repair another player's lance");
        coop.Execute(new OrderRepairCommand(coop.Roster.Mechs[3].Id, RepairScope.All), 1);
        check(coop.Roster.Mechs[3].UnderRepair, "a player orders repairs for their own lance");
    }

    /// <summary>A two-player company: the draft's first two mechs in the leader's lance, the last two in the second player's.</summary>
    internal static Campaign TwoPlayerCompany()
    {
        var save = NewCompany(7).Snapshot();
        var roster = save.Roster!;
        return Campaign.Restore(save with { Roster = roster with { Lances = 2, Mechs = roster.Mechs.Select((mech, i) => mech with { Lance = i < 2 ? 0 : 1 }).ToArray() },
            Missions = MissionBoard.Create(7, save.Date, StarChart.Instance.StartingSystemId, new Reputation(), lances: 2).Snapshot() });
    }

    private static void TestLances(Action<bool, string> check)
    {
        var solo = NewCompany();
        check(solo.Players == 1 && solo.Roster!.LanceCount == 1 && solo.Roster.Mechs.All(mech => mech.Lance == 0),
            "a single-player company forms one lance from its four drafted mechs");
        Throws(() => solo.Execute(new AdvanceDayCommand(), 1), check, "a single-player campaign has no second player slot");
        check(solo.Execute(new AdvanceDayCommand()).Advanced, "the leader advances time through a command");

        var campaign = TwoPlayerCompany();
        var roster = campaign.Roster!;
        var (leaderMech, playerMech) = (roster.Mechs[0], roster.Mechs[2]);
        check(campaign.Players == 2 && roster.LanceMechs(0).Count() == 2 && roster.LanceMechs(1).Count() == 2, "a two-player company has two lances");
        var offer = campaign.Missions.Available.First(item => campaign.DeadlineDays(item) is not null);
        Throws(() => campaign.Execute(new AcceptMissionCommand(offer.Id), 1), check, "only the leader accepts missions");
        Throws(() => campaign.Execute(new AdvanceDayCommand(), 1), check, "only the leader advances time");
        Throws(() => campaign.Execute(new PlotJourneyCommand(campaign.Travel.OrbitSystemId), 1), check, "only the leader plots travel");
        var date = campaign.Date;
        campaign.Execute(new AcceptMissionCommand(offer.Id), Campaign.LeaderSlot);
        check(campaign.Missions.Current?.Id == offer.Id && campaign.Date == date, "the leader accepts a mission");

        Throws(() => campaign.Execute(new SetLanceCommand(leaderMech.Id, null), 1), check, "a player cannot move another player's mech to the lance pool");
        Throws(() => campaign.Execute(new ApplyLoadoutCommand(leaderMech.Id, leaderMech.Plan, []), 1), check, "a player cannot refit another player's mech");
        Throws(() => campaign.Execute(new AssignWarriorCommand(playerMech.Id, leaderMech.WarriorId, true), 1), check,
            "a player cannot take a MechWarrior from another player's mech");
        campaign.Execute(new SetLanceCommand(playerMech.Id, null), 1);
        check(playerMech.Lance is null && roster.Pool.Single() == playerMech, "a player moves their own mech to the lance pool, MechWarrior and all");
        Throws(() => campaign.Execute(new SetLanceCommand(playerMech.Id, Campaign.LeaderSlot), 1), check, "a player only adds mechs to their own lance");
        campaign.Execute(new AssignWarriorCommand(playerMech.Id, null), Campaign.LeaderSlot);
        check(playerMech.WarriorId is null, "any player acts on lance pool mechs");
        campaign.Execute(new SetLanceCommand(playerMech.Id, Campaign.LeaderSlot), Campaign.LeaderSlot);
        check(playerMech.Lance == 0 && roster.LanceMechs(0).Count() == 3, "the leader takes a lance pool mech into their lance");
        var restored = Campaign.Restore(campaign.Snapshot());
        check(restored.Players == 2 && restored.Roster!.Mechs.Select(mech => mech.Lance).SequenceEqual(roster.Mechs.Select(mech => mech.Lance)),
            "lances survive a save round trip");
    }

    /// <summary>The snapshot as an older version would have saved it: whole stock points of armor and structure.</summary>
    internal static CampaignSnapshot Legacy(CampaignSnapshot save, int version) => save with
    {
        Version = version,
        Roster = save.Roster is not { } roster ? null : roster with { Mechs = roster.Mechs.Select(mech => mech with
        {
            Locations = mech.Locations.Select(loc => loc with { Armor = loc.Armor / CombatRules.DamageScale,
                RearArmor = loc.RearArmor / CombatRules.DamageScale, Structure = loc.Structure / CombatRules.DamageScale }).ToArray()
        }).ToArray() }
    };

    internal static Campaign NewCompany(ulong? missionSeed = null)
    {
        var campaign = new Campaign(missionSeed);
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

    private static void TestSaveLibrary(Action<bool, string> check)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mech-saves-{Guid.NewGuid():N}");
        try
        {
            var solo = new SaveLibrary(System.IO.Path.Combine(root, "solo"), coop: false);
            var coop = new SaveLibrary(System.IO.Path.Combine(root, "coop"), coop: true);
            check(solo.List().Count == 0 && !solo.Any(), "a missing save folder lists no saves");
            var campaign = NewCompany();
            var first = solo.Save(campaign, null);
            campaign.AdvanceOneDay();
            var second = solo.Save(campaign, "  Second  ");
            var slots = solo.List();
            check(slots.Select(slot => slot.Id).SequenceEqual([second, first]), "saves list the most recently written first");
            check(slots[1].Info!.Name == campaign.Roster!.ForceName && slots[0].Info!.Name == "Second" && slots[0].Info!.Date == campaign.Date
                && slots[0].Info!.Cash == campaign.Cash && slots[0].Info!.Players is null, "a slot records its name (defaulting to the company), date and balance");
            check(solo.Save(campaign, "First again", first) == first && solo.List() is [{ Info.Name: "First again" } top, _] && top.Id == first,
                "overwriting keeps the slot and moves it to the top");
            check(solo.Load(second).Date == campaign.Date, "a slot loads its campaign");
            check(solo.Save(campaign, new string('x', 60)) is var longId && solo.Info(longId).Name.Length == CampaignStore.MaxNameLength, "long names are cut to the limit");
            Throws(() => coop.Save(campaign, null), check, "a solo campaign cannot be saved among co-op saves");
            System.IO.Directory.CreateDirectory(coop.Directory);
            System.IO.File.Copy(solo.PathOf(second), coop.PathOf("misplaced"));
            Throws(() => coop.Load("misplaced"), check, "a co-op library refuses a solo save");
            Throws(() => solo.PathOf("../escape"), check, "slot ids cannot leave the folder");
            System.IO.File.WriteAllText(solo.PathOf("broken"), "{broken");
            check(solo.List() is { Count: 4 } all && all[^1] is { Id: "broken", Info: null }, "an unreadable save is listed last, without information");

            var legacyPath = System.IO.Path.Combine(root, "campaign.json");
            System.IO.File.WriteAllText(legacyPath, System.Text.Json.JsonSerializer.Serialize(campaign.Snapshot(), CampaignStore.JsonOptions));
            var written = System.IO.File.GetLastWriteTimeUtc(legacyPath);
            var fresh = new SaveLibrary(System.IO.Path.Combine(root, "fresh-solo"), coop: false);
            var freshCoop = new SaveLibrary(System.IO.Path.Combine(root, "fresh-coop"), coop: true);
            SaveLibrary.ImportLegacy(legacyPath, fresh, freshCoop);
            check(!System.IO.File.Exists(legacyPath) && fresh.List() is [{ Info: { } legacy } imported] && legacy.Name == campaign.Roster.ForceName
                && legacy.SavedAt == written && fresh.Load(imported.Id).Date == campaign.Date && freshCoop.List().Count == 0,
                "a save from before slots moves into the solo folder, keeping its time");
        }
        finally
        {
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
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
