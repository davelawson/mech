using System.Collections.Immutable;

namespace Mech;

public enum WeaponFamily { Laser, Ppc, Flamer, MachineGun, Autocannon, Lrm, Srm }

/// <summary>The fixed facts of one weapon kind that the catalog does not carry: stock BattleTech mass and critical slots,
/// the game's cooldown, its ammunition and how it mounts. <see cref="Size"/> is the autocannon class or missile rack size.</summary>
public sealed record WeaponDefinition(WeaponKind Kind, WeaponFamily Family, int Size, int Slots, float Tons, float Cooldown, AmmoKind? Ammo)
{
    public StorageGroup Group => Family switch
    {
        WeaponFamily.Laser or WeaponFamily.Ppc or WeaponFamily.Flamer => StorageGroup.Energy,
        WeaponFamily.MachineGun or WeaponFamily.Autocannon => StorageGroup.Ballistic,
        _ => StorageGroup.Missile
    };
    public HardpointClass Hardpoint => Group switch
    {
        StorageGroup.Energy => HardpointClass.Energy, StorageGroup.Ballistic => HardpointClass.Ballistic, _ => HardpointClass.Missile
    };
    public bool FiresSalvo => Family is WeaponFamily.Lrm or WeaponFamily.Srm;
    /// <summary>Missiles per salvo; every other weapon fires one projectile.</summary>
    public int Rack => FiresSalvo ? Size : 1;
    /// <summary>The weapon-library visual family a hardpoint must offer; both missile kinds share racks.</summary>
    public string MountFamily => Family switch
    {
        WeaponFamily.Ppc => "PPC", WeaponFamily.MachineGun => "MG", WeaponFamily.Autocannon => "AC",
        WeaponFamily.Lrm or WeaponFamily.Srm => "Missile", _ => Family.ToString()
    };
    /// <summary>The muzzle-marker name fragment a model's firing markers use for this weapon.</summary>
    public string MarkerKind => Family switch
    {
        WeaponFamily.Ppc => "PPC", WeaponFamily.MachineGun => "MG", WeaponFamily.Autocannon => "AC",
        WeaponFamily.Lrm => "LRM", WeaponFamily.Srm => "SRM", _ => Family.ToString()
    };
}

/// <summary>The single table of weapon kinds. Every <see cref="WeaponKind"/> has exactly one entry.</summary>
public static class WeaponDefinitions
{
    public static ImmutableArray<WeaponDefinition> All { get; } =
    [
        new(WeaponKind.SmallLaser, WeaponFamily.Laser, 0, 1, .5f, 3, null),
        new(WeaponKind.MediumLaser, WeaponFamily.Laser, 0, 1, 1, 5, null),
        new(WeaponKind.LargeLaser, WeaponFamily.Laser, 0, 2, 5, 7, null),
        new(WeaponKind.ERLargeLaser, WeaponFamily.Laser, 0, 2, 5, 7, null),
        new(WeaponKind.PPC, WeaponFamily.Ppc, 0, 3, 7, 10, null),
        new(WeaponKind.Flamer, WeaponFamily.Flamer, 0, 1, 1, 6, null),
        new(WeaponKind.MachineGun, WeaponFamily.MachineGun, 0, 1, .5f, 2, AmmoKind.MachineGun),
        new(WeaponKind.AC2, WeaponFamily.Autocannon, 2, 1, 6, 10, AmmoKind.AC2),
        new(WeaponKind.AC5, WeaponFamily.Autocannon, 5, 4, 8, 10, AmmoKind.AC5),
        new(WeaponKind.AC10, WeaponFamily.Autocannon, 10, 7, 12, 10, AmmoKind.AC10),
        new(WeaponKind.AC20, WeaponFamily.Autocannon, 20, 10, 14, 10, AmmoKind.AC20),
        new(WeaponKind.LRM5, WeaponFamily.Lrm, 5, 1, 2, 10, AmmoKind.LRM5),
        new(WeaponKind.LRM10, WeaponFamily.Lrm, 10, 2, 5, 10, AmmoKind.LRM10),
        new(WeaponKind.LRM15, WeaponFamily.Lrm, 15, 3, 7, 10, AmmoKind.LRM15),
        new(WeaponKind.LRM20, WeaponFamily.Lrm, 20, 5, 10, 10, AmmoKind.LRM20),
        new(WeaponKind.SRM2, WeaponFamily.Srm, 2, 1, 1, 10, AmmoKind.SRM2),
        new(WeaponKind.SRM4, WeaponFamily.Srm, 4, 1, 2, 10, AmmoKind.SRM4),
        new(WeaponKind.SRM6, WeaponFamily.Srm, 6, 2, 3, 10, AmmoKind.SRM6),
    ];

    private static readonly ImmutableDictionary<WeaponKind, WeaponDefinition> ByKind = All.ToImmutableDictionary(item => item.Kind);

    public static WeaponDefinition Of(WeaponKind kind) =>
        ByKind.TryGetValue(kind, out var definition) ? definition : throw new ArgumentOutOfRangeException(nameof(kind), kind, "no weapon definition");
}
