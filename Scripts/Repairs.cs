using System.Text.Json.Serialization;

namespace Mech;

/// <summary>What a repair order restores: armor alone, or everything (armor, internal structure and every damaged or destroyed
/// component, weapon and ammunition bin).</summary>
public enum RepairScope { Armor = 1, All }

/// <summary>A mech's repair order. <see cref="Days"/> and <see cref="Cost"/> are fixed when it is ordered; orders start in
/// <see cref="Sequence"/> order as repair bays free up, and <see cref="Started"/> is the day a bay took it and it was paid for.</summary>
public sealed record RepairOrder(RepairScope Scope, int Days, long Cost, long Sequence, DateOnly? Started = null)
{
    [JsonIgnore] public DateOnly? Completes => Started?.AddDays(Days);
}

public static class RepairTuning
{
    /// <summary>Repair orders worked on at once, one per mech hangar.</summary>
    public const int Bays = 4;
    public const double ArmorTonsPerDay = 4, StructureTonsPerDay = 1, DaysPerSystem = .5;
    public const long ArmorPricePerTon = 10_000, StructurePricePerTon = 40_000, DamagedSystemPrice = 10_000, DestroyedSystemPrice = 25_000;
    /// <summary>Standard internal structure weighs a tenth of the mech's rated tonnage.</summary>
    public const double StructureMassFraction = .1;
}

public readonly record struct RepairEstimate(int Days, long Cost);

public static class Repairs
{
    /// <summary>The days and C-bills to repair a mech, or null when the scope has nothing to repair. Armor repairs skip destroyed
    /// locations, which need their structure rebuilt first.</summary>
    public static RepairEstimate? Estimate(OwnedMech mech, RepairScope scope)
    {
        var all = scope == RepairScope.All;
        var spec = mech.Spec; var armorPoints = 0; var structurePoints = 0;
        foreach (var state in mech.Locations)
        {
            if (!all && state.Structure <= 0) continue;
            var full = spec.Locations.Single(item => item.Location == state.Location);
            armorPoints += full.Armor - state.Armor + full.RearArmor - state.RearArmor;
            if (all) structurePoints += full.Structure - state.Structure;
        }
        var damaged = all ? mech.Equipment.Count(item => item.Condition == ComponentCondition.Damaged) : 0;
        var destroyed = all ? mech.Equipment.Count(item => item.Condition == ComponentCondition.Destroyed) : 0;
        if (armorPoints <= 0 && structurePoints <= 0 && damaged + destroyed == 0) return null;
        var armorTons = armorPoints / (double)(EquipmentRules.ArmorPointsPerTon * CombatRules.DamageScale);
        var structureTons = spec.StructureTotal == 0 ? 0
            : structurePoints / (double)spec.StructureTotal * mech.Definition.Tonnage * RepairTuning.StructureMassFraction;
        var days = armorTons / RepairTuning.ArmorTonsPerDay + structureTons / RepairTuning.StructureTonsPerDay
            + (damaged + destroyed) * RepairTuning.DaysPerSystem;
        var cost = armorTons * RepairTuning.ArmorPricePerTon + structureTons * RepairTuning.StructurePricePerTon
            + damaged * RepairTuning.DamagedSystemPrice + destroyed * RepairTuning.DestroyedSystemPrice;
        // A small tolerance keeps exact multiples, such as 4 t of armor, from rounding up a day.
        return new(Math.Max(1, (int)Math.Ceiling(days - 1e-9)), (long)Math.Ceiling(cost - 1e-6));
    }

    /// <summary>"N days · X C-bills".</summary>
    public static string Text(RepairEstimate estimate) =>
        $"{estimate.Days} {(estimate.Days == 1 ? "day" : "days")} · {HangarScreen.Money(estimate.Cost)}";
}
