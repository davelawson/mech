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
    public static string Experience(WarriorSpec warrior) => warrior.Specialty == WarriorSpecialty.None
        ? warrior.Rank.ToString() : $"{warrior.Rank} · {warrior.Specialty}";
    public static string WeightClass(MechWeightClass weight) => weight.ToString();
    public static string HeatSinks(HeatSinkKind kind) => kind.ToString().ToLowerInvariant();
}
