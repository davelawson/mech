using System.Globalization;

namespace Mech;

/// <summary>Presentation only. Rule consumers use the typed definitions directly.</summary>
public static class DefinitionText
{
    public static string Damage(WeaponDamageSpec damage) => damage.Kind switch
    {
        DamageKind.Missile => $"{damage.PerProjectile.ToString(CultureInfo.InvariantCulture)} / missile",
        DamageKind.Direct => damage.TargetHeat > 0
            ? $"{damage.PerProjectile.ToString(CultureInfo.InvariantCulture)} / {damage.TargetHeat.ToString(CultureInfo.InvariantCulture)} heat"
            : damage.PerProjectile.ToString(CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(damage))
    };
    /// <summary>A weapon's combat figures for tooltips and records: damage and heat generated, range bands in metres and
    /// the ammunition it needs. Mass is left to the caller, which may already show it.</summary>
    public static string WeaponStats(WeaponSpec weapon)
    {
        static string Metres(int range) => (range * CombatRules.WeaponRangeScale).ToString(CultureInfo.InvariantCulture);
        var minimum = weapon.Minimum > 0 ? $" · minimum {Metres(weapon.Minimum)} m" : "";
        var ammo = weapon.AmmoKind is { } kind ? StorageCatalog.Bin(kind)?.Name ?? kind.ToString() : "none";
        return $"Damage {Damage(weapon.DamageProfile)} · Heat generated {weapon.Heat}\n"
            + $"Range {Metres(weapon.Short)} / {Metres(weapon.Medium)} / {Metres(weapon.Long)} m{minimum}\nAmmo: {ammo}";
    }
    /// <summary>A stored or salvaged weapon's mass and slots above its <see cref="WeaponStats"/>; null for anything not a weapon.</summary>
    public static string? WeaponDetails(string? storageId)
    {
        if (storageId is null || !storageId.StartsWith("weapon:", StringComparison.Ordinal)
            || StorageCatalog.Describe(storageId) is not { } item || !Enum.TryParse<WeaponKind>(storageId["weapon:".Length..], out var kind)
            || StorageCatalog.Weapon(kind) is not { } weapon) return null;
        return $"{item.Tons.ToString("0.#", CultureInfo.InvariantCulture)} t · {item.Slots} {(item.Slots == 1 ? "slot" : "slots")}\n{WeaponStats(weapon)}";
    }
    public static string Experience(WarriorSpec warrior) => warrior.Rank.ToString();
    public static string WeightClass(MechWeightClass weight) => weight.ToString();
    public static string HeatSinks(HeatSinkKind kind) => kind.ToString().ToLowerInvariant();
}
