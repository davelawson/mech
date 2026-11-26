namespace Mech;

/// <summary>Numbers for salvage, recorded in docs/design.md.</summary>
public static class SalvageTuning
{
    /// <summary>C-bills paid for each hostage when a won mission settles, by EnemyPilotRank order: Corporal, Sergeant, Lieutenant, Captain.</summary>
    public static readonly long[] Ransom = [20_000, 40_000, 80_000, 150_000];
    public static long RansomFor(EnemyPilotRank rank) => Ransom[(int)rank];
    /// <summary>Picks a player may take beyond the contract's allotment, each at a reputation cost.</summary>
    public const int ExtraPicks = 3;
    /// <summary>Picks to claim a whole mech, by MechWeightClass order.</summary>
    public static readonly int[] MechPicks = [2, 3, 4, 5];
    public static int Picks(MechWeightClass weight) => MechPicks[(int)weight];
}

/// <summary>One thing salvage can take: an item from an enemy mech (StorageId set) or the whole mech (ItemId null).</summary>
public sealed record SalvageOption(string UnitId, string? ItemId, string Name, string? StorageId, int Picks)
{
    public SalvageClaim Claim => new(UnitId, ItemId);
    public bool WholeMech => ItemId is null;
}

/// <summary>What a won battle leaves to salvage, and checking a player's choice from it.</summary>
public static class Salvage
{
    /// <summary>Every enemy mech left on the field gives its working weapons, ammunition bins, heat sinks and jump jets in locations
    /// with structure left, one pick each; one not destroyed (disabled, ejected, headless or unarmed) can also be claimed whole,
    /// priced by weight class. Surrendered mechs stay with their pilots' side, and fled mechs are gone. A field the opposition
    /// still holds, as when a patrol extracts past mechs still fighting, gives nothing.</summary>
    public static IReadOnlyList<SalvageOption> Pool(MissionReport report)
    {
        var pool = new List<SalvageOption>();
        if (report.EnemyHoldsField) return pool;
        foreach (var enemy in report.Enemies.Where(item => item.Status is not (CombatStatus.Surrendered or CombatStatus.Fled or CombatStatus.Extracted or CombatStatus.Inbound)))
        {
            var spec = DraftCatalog.Mech(enemy.MechId);
            if (enemy.Status != CombatStatus.Destroyed)
                pool.Add(new(enemy.UnitId, null, spec.DisplayName, null, SalvageTuning.Picks(spec.WeightClass)));
            foreach (var item in enemy.Equipment.Where(item => item.Condition == ComponentCondition.Operational && Repairs.Storable(spec, item.Id)))
                pool.Add(new(enemy.UnitId, item.Id, ItemName(spec, item.Id), Loadout.StorageId(spec, item.Id), 1));
        }
        return pool;
    }

    private static string ItemName(MechSpec spec, string id) =>
        spec.Weapons.FirstOrDefault(item => item.Id == id)?.Name ?? spec.Ammo.FirstOrDefault(item => item.Id == id)?.Name
        ?? spec.Components.Single(item => item.Id == id).Name;

    /// <summary>The picks a choice uses; refused unless every claim is in the pool once, no mech is taken both whole and in pieces, and
    /// the picks stay within the allotment plus the extra picks.</summary>
    public static int Validate(IReadOnlyList<SalvageOption> pool, IReadOnlyCollection<SalvageClaim> claims, int allotment)
    {
        if (claims.Distinct().Count() != claims.Count) throw new ArgumentException("A salvage pick was claimed twice.");
        var chosen = claims.Select(claim => pool.SingleOrDefault(option => option.Claim == claim)
            ?? throw new ArgumentException("That salvage is not on the field.")).ToArray();
        var whole = chosen.Where(option => option.WholeMech).Select(option => option.UnitId).ToHashSet(StringComparer.Ordinal);
        if (chosen.Any(option => !option.WholeMech && whole.Contains(option.UnitId)))
            throw new ArgumentException("A mech claimed whole cannot also be stripped.");
        var picks = chosen.Sum(option => option.Picks);
        if (picks > allotment + SalvageTuning.ExtraPicks) throw new ArgumentException("Too many salvage picks.");
        return picks;
    }
}

/// <summary>Contract insurance: the cost of repairing the damage a battle did.</summary>
public static class Insurance
{
    /// <summary>What a full repair of the mech as it stands would cost, buying every missing part at market price.</summary>
    public static long RepairValue(OwnedMech mech) => Repairs.Estimate(mech, RepairScope.All, _ => int.MaxValue) is { } estimate
        ? estimate.Cost + estimate.Parts.Sum(EquipmentMarket.Price) : 0;
}
