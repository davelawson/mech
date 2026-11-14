using System.Text.Json.Serialization;

namespace Mech;

/// <summary>What a repair order restores: armor alone, or everything (armor, internal structure and every damaged or destroyed
/// component, weapon and ammunition bin).</summary>
public enum RepairScope { Armor = 1, All }

/// <summary>A mech's repair order. <see cref="Days"/> and <see cref="Cost"/> are fixed when it is ordered; orders start in
/// <see cref="Sequence"/> order as repair bays free up, and <see cref="Started"/> is the day a bay took it and it was paid for.</summary>
public sealed record RepairOrder(RepairScope Scope, int Days, long Cost, long Sequence, DateOnly? Started = null,
    string[]? Parts = null, string[]? Missing = null)
{
    [JsonIgnore] public DateOnly? Completes => Started?.AddDays(Days);
}

public static class RepairTuning
{
    /// <summary>Repair orders worked on at once, one per mech hangar.</summary>
    public const int Bays = 4;
    public const double ArmorTonsPerDay = 4, StructureTonsPerDay = 1;
    public const long ArmorPricePerTon = 10_000, StructurePricePerTon = 40_000;
    /// <summary>Standard internal structure weighs a tenth of the mech's rated tonnage.</summary>
    public const double StructureMassFraction = .1;
    /// <summary>Weapons, bins, heat sinks and jump jets: a hit slot takes this long to repair, and so does each slot of a destroyed
    /// item to replace from a spare.</summary>
    public const double DaysPerSlot = .5;
    /// <summary>Fitting a spare in place of a destroyed item, per slot it takes.</summary>
    public const long InstallPricePerSlot = 10_000;
    /// <summary>A 400-rated engine costs this per damage level; the price falls linearly to nothing at rating 0.</summary>
    public const long EnginePricePerLevelAt400 = 100_000;
    /// <summary>A 100-ton mech's gyro costs this per damage level; the price scales linearly with tonnage.</summary>
    public const long GyroPricePerLevelAt100Tons = 50_000;
    public const double EngineDaysPerLevel = 1, GyroDaysPerLevel = 1, SystemDaysPerLevel = .5;

    /// <summary>Damage levels that destroy a critical system.</summary>
    public static int Levels(ComponentKind kind) => kind switch
    { ComponentKind.FusionEngine => 3, ComponentKind.Gyro => 2, ComponentKind.Sensors => 2, _ => 1 };

    /// <summary>Price per damage level of a critical system on this mech.</summary>
    public static double PricePerLevel(ComponentKind kind, MechSpec spec) => kind switch
    {
        ComponentKind.FusionEngine => EngineRating(spec) / 400d * EnginePricePerLevelAt400,
        ComponentKind.Gyro => spec.Tonnage / 100d * GyroPricePerLevelAt100Tons,
        ComponentKind.Cockpit => 30_000, ComponentKind.Sensors => 20_000, ComponentKind.LifeSupport => 15_000,
        ComponentKind.Hip => 20_000, ComponentKind.Shoulder => 15_000, _ => 10_000
    };

    public static double DaysPerLevel(ComponentKind kind) => kind switch
    { ComponentKind.FusionEngine => EngineDaysPerLevel, ComponentKind.Gyro => GyroDaysPerLevel, _ => SystemDaysPerLevel };

    /// <summary>The engine's rating, read from its name ("300 Fusion Engine"), or walking MP × tonnage when the name has none.</summary>
    public static int EngineRating(MechSpec spec)
    {
        var digits = new string(spec.Engine.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var rating) ? rating : spec.Walk * spec.Tonnage;
    }
}

/// <summary>What a repair takes. <see cref="Lines"/> describe the work; <see cref="Parts"/> are the storage spares it uses and
/// <see cref="Missing"/> the destroyed items it leaves because storage has no spare for them. Days is 0 when nothing can be done.</summary>
public sealed record RepairEstimate(int Days, long Cost, IReadOnlyList<string> Lines, IReadOnlyList<string> Parts, IReadOnlyList<string> Missing,
    IReadOnlyList<string> MissingParts)
{
    public bool Doable => Days > 0;
}

public static class Repairs
{
    /// <summary>Weapons, ammunition bins, heat sinks and jump jets: items storage holds, replaced from a spare when destroyed.</summary>
    public static bool Storable(MechSpec spec, string id) => spec.Weapons.Any(item => item.Id == id) || spec.Ammo.Any(item => item.Id == id)
        || spec.Components.Any(item => item.Id == id && item.Kind is ComponentKind.HeatSink or ComponentKind.JumpJet);

    /// <summary>A storable item's critical slots, or a critical system's damage levels: how many hits destroy it.</summary>
    public static int Levels(MechSpec spec, string id) =>
        spec.Weapons.FirstOrDefault(item => item.Id == id) is { } weapon ? EquipmentRules.Slots(weapon.Kind)
        : spec.Ammo.Any(item => item.Id == id) ? 1
        : spec.Components.Single(item => item.Id == id) is var component && component.Kind is ComponentKind.HeatSink or ComponentKind.JumpJet
            ? Math.Max(1, component.Slots) : RepairTuning.Levels(component.Kind);

    /// <summary>Hits an item has taken: none when working, every level when destroyed, otherwise its recorded hits (one for saves
    /// from before hits were counted).</summary>
    public static int Hits(MechSpec spec, EquipmentCondition item) => item.Condition switch
    {
        ComponentCondition.Operational => 0,
        ComponentCondition.Destroyed => Levels(spec, item.Id),
        _ => Math.Clamp(item.Hits ?? 1, 1, Levels(spec, item.Id))
    };

    private static string Name(MechSpec spec, string id) =>
        spec.Weapons.FirstOrDefault(item => item.Id == id)?.Name ?? spec.Ammo.FirstOrDefault(item => item.Id == id)?.Name
        ?? spec.Components.Single(item => item.Id == id).Name;

    /// <summary>The work, days and C-bills to repair a mech, or null when the scope has nothing to repair. Armor repairs skip
    /// destroyed locations, which need their structure rebuilt first. A full repair also mends every damaged item and replaces each
    /// destroyed weapon, bin, heat sink or jump jet with a spare from storage, as far as <paramref name="available"/> spares allow;
    /// critical systems never need parts.</summary>
    public static RepairEstimate? Estimate(OwnedMech mech, RepairScope scope, Func<string, int>? available = null)
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
        var damaged = all ? mech.Equipment.Where(item => item.Condition != ComponentCondition.Operational).ToArray() : [];
        if (armorPoints <= 0 && structurePoints <= 0 && damaged.Length == 0) return null;
        var lines = new List<string>(); var parts = new List<string>(); var missing = new List<string>(); var missingParts = new List<string>();
        var armorTons = armorPoints / (double)(EquipmentRules.ArmorPointsPerTon * CombatRules.DamageScale);
        var structureTons = spec.StructureTotal == 0 ? 0
            : structurePoints / (double)spec.StructureTotal * mech.Definition.Tonnage * RepairTuning.StructureMassFraction;
        var days = armorTons / RepairTuning.ArmorTonsPerDay + structureTons / RepairTuning.StructureTonsPerDay;
        var cost = armorTons * RepairTuning.ArmorPricePerTon + structureTons * RepairTuning.StructurePricePerTon;
        if (armorPoints > 0) lines.Add($"Armor {Tons(armorTons)} t");
        if (structurePoints > 0) lines.Add($"Structure {Tons(structureTons)} t");
        var spares = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in damaged)
        {
            var levels = Levels(spec, item.Id); var hits = Hits(spec, item); var name = Name(spec, item.Id);
            if (!Storable(spec, item.Id))
            {
                var kind = spec.Components.Single(component => component.Id == item.Id).Kind;
                days += hits * RepairTuning.DaysPerLevel(kind); cost += hits * RepairTuning.PricePerLevel(kind, spec);
                lines.Add($"{name} · {hits} {(hits == 1 ? "level" : "levels")}");
            }
            else if (hits < levels)
            {
                days += hits * RepairTuning.DaysPerSlot; cost += hits / (double)levels * EquipmentMarket.Price(Loadout.StorageId(spec, item.Id));
                lines.Add($"{name} · {hits} of {levels} slots");
            }
            else
            {
                var storageId = Loadout.StorageId(spec, item.Id);
                var left = spares.TryGetValue(storageId, out var counted) ? counted : available?.Invoke(storageId) ?? 0;
                if (left <= 0) { missing.Add(item.Id); missingParts.Add(storageId); spares[storageId] = 0; continue; }
                spares[storageId] = left - 1; parts.Add(storageId);
                days += levels * RepairTuning.DaysPerSlot; cost += levels * RepairTuning.InstallPricePerSlot;
                lines.Add($"{name} · replaced");
            }
        }
        if (parts.Count > 0) lines.Add($"Parts used: {PartsText(parts)}");
        if (missingParts.Count > 0) lines.Add($"Missing parts: {PartsText(missingParts)}");
        // A small tolerance keeps exact multiples, such as 4 t of armor, from rounding up a day.
        var doable = armorPoints > 0 || structurePoints > 0 || damaged.Length > missing.Count;
        return new(doable ? Math.Max(1, (int)Math.Ceiling(days - 1e-9)) : 0, doable ? (long)Math.Ceiling(cost - 1e-6) : 0, lines, parts, missing, missingParts);
    }

    private static string Tons(double tons) => tons.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"PPC ×1, AC/20 ammunition ×2".</summary>
    public static string PartsText(IEnumerable<string> storageIds) => string.Join(", ", storageIds.GroupBy(id => id, StringComparer.Ordinal)
        .Select(group => $"{StorageCatalog.Describe(group.Key)?.Name ?? group.Key} ×{group.Count()}"));

    /// <summary>"N days · X C-bills".</summary>
    public static string Text(RepairEstimate estimate) =>
        $"{estimate.Days} {(estimate.Days == 1 ? "day" : "days")} · {HangarScreen.Money(estimate.Cost)}";
}
