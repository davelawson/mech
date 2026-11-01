using System.Collections.Immutable;

namespace Mech;

public enum HardpointClass { Ballistic, Energy, Missile, Ams }

/// <summary>An MW5 mount slot (`&lt;Bone&gt;_&lt;Class&gt;&lt;n&gt;`) in a damage location, with the weapon visuals prepared for it.
/// Stock hardpoints name the visual, main-model part and firing markers the prepared model already shows there.</summary>
public sealed record Hardpoint(string Id, MechLocation Location, HardpointClass Class, string[] Visuals,
    string? Stock = null, string? StockPart = null, string[]? StockMarkers = null);

/// <summary>The hardpoint → visual assignment a model is built from, and the hardpoint carrying each weapon.
/// A stock map builds exactly the prepared model and leaves firing markers to the shared heuristics.</summary>
public sealed record MountMap(ImmutableDictionary<string, string> Visuals, ImmutableDictionary<string, string> WeaponHardpoints,
    ImmutableArray<string> Unmounted, bool Stock)
{
    public static readonly MountMap Empty = new(ImmutableDictionary<string, string>.Empty,
        ImmutableDictionary<string, string>.Empty, [], true);
}

/// <summary>Chooses weapon visuals on a chassis's hardpoints for a list of game weapons.</summary>
public static class WeaponMounts
{
    public const string Blank = "Blank";

    private static (HardpointClass Class, string Family, int Size) Want(WeaponKind kind) => kind switch
    {
        WeaponKind.SmallLaser or WeaponKind.MediumLaser or WeaponKind.LargeLaser or WeaponKind.ERLargeLaser
            => (HardpointClass.Energy, "Laser", 0),
        WeaponKind.PPC => (HardpointClass.Energy, "PPC", 0),
        WeaponKind.Flamer => (HardpointClass.Energy, "Flamer", 0),
        WeaponKind.MachineGun => (HardpointClass.Ballistic, "MG", 0),
        WeaponKind.AC2 => (HardpointClass.Ballistic, "AC", 2),
        WeaponKind.AC5 => (HardpointClass.Ballistic, "AC", 5),
        WeaponKind.AC10 => (HardpointClass.Ballistic, "AC", 10),
        WeaponKind.AC20 => (HardpointClass.Ballistic, "AC", 20),
        WeaponKind.SRM2 => (HardpointClass.Missile, "Missile", 2),
        WeaponKind.SRM4 => (HardpointClass.Missile, "Missile", 4),
        WeaponKind.SRM6 => (HardpointClass.Missile, "Missile", 6),
        WeaponKind.LRM5 => (HardpointClass.Missile, "Missile", 5),
        WeaponKind.LRM10 => (HardpointClass.Missile, "Missile", 10),
        WeaponKind.LRM15 => (HardpointClass.Missile, "Missile", 15),
        WeaponKind.LRM20 => (HardpointClass.Missile, "Missile", 20),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static int? Size(string visual, string family) =>
        visual.StartsWith(family, StringComparison.Ordinal) && int.TryParse(visual[family.Length..], out var size) ? size : null;

    /// <summary>The visual a weapon shows on a hardpoint, or null when the hardpoint can't carry it. Sized families
    /// (autocannons, missile racks) use the largest visual no bigger than the weapon, else the smallest larger one.</summary>
    public static string? Visual(WeaponKind kind, Hardpoint hardpoint)
    {
        var (type, family, size) = Want(kind);
        if (hardpoint.Class != type) return null;
        if (size == 0) return hardpoint.Visuals.Contains(family) ? family : null;
        var sizes = hardpoint.Visuals.Select(visual => Size(visual, family)).OfType<int>().ToArray();
        if (sizes.Length == 0) return null;
        return family + (sizes.Any(value => value <= size) ? sizes.Where(value => value <= size).Max() : sizes.Min());
    }

    // A stock visual of the weapon's family (Atlas's LRM 20 on its ten-tube rack) stays when the stock weapon does.
    private static bool SameFamily(WeaponKind kind, string visual)
    {
        var (_, family, size) = Want(kind);
        return size == 0 ? visual == family : Size(visual, family) is not null;
    }

    private static string Key(WeaponSpec weapon) => $"{weapon.Location}:{weapon.Kind}:{weapon.Rear}";

    public static MountMap Resolve(MechSpec spec) =>
        Resolve(spec, MechModel.Hardpoints(spec.Id), DraftCatalog.AllMechs.FirstOrDefault(mech => mech.Id == spec.Id));

    /// <summary>The catalog loadout keeps the prepared model. Otherwise each weapon keeps a stock hardpoint still holding
    /// its stock weapon, then takes the first free compatible hardpoint in its location (stock-filled first), then an
    /// unused stock hardpoint elsewhere with its stock visual (Crusader's leg SRMs). Emptied stock hardpoints show a
    /// blank cover where one exists. Weapons left over are unmounted.</summary>
    public static MountMap Resolve(MechSpec spec, IReadOnlyList<Hardpoint> hardpoints, MechSpec? stock)
    {
        var stockVisuals = hardpoints.Where(item => item.Stock is not null).ToImmutableDictionary(item => item.Id, item => item.Stock!);
        if (stock is not null && spec.Weapons.Select(Key).Order().SequenceEqual(stock.Weapons.Select(Key).Order()))
            return new(stockVisuals, ImmutableDictionary<string, string>.Empty, [], true);

        var stockWeapons = (stock?.Weapons ?? []).GroupBy(Key).ToDictionary(group => group.Key, group => group.Count());
        var free = hardpoints.OrderBy(item => item.Stock is null).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
        var visuals = ImmutableDictionary.CreateBuilder<string, string>();
        var carriers = ImmutableDictionary.CreateBuilder<string, string>();
        var pending = spec.Weapons.ToList();

        void Pass(Func<WeaponSpec, Hardpoint, string?> choose)
        {
            foreach (var weapon in pending.ToArray())
                foreach (var hardpoint in free)
                {
                    if (choose(weapon, hardpoint) is not { } visual) continue;
                    visuals[hardpoint.Id] = visual; carriers[weapon.Id] = hardpoint.Id;
                    free.Remove(hardpoint); pending.Remove(weapon);
                    break;
                }
        }
        string? KeepStock(WeaponSpec weapon, Hardpoint hardpoint)
        {
            if (hardpoint.Stock is not { } visual || hardpoint.Class != Want(weapon.Kind).Class) return null;
            if (Visual(weapon.Kind, hardpoint) != visual
                && !(SameFamily(weapon.Kind, visual) && stockWeapons.GetValueOrDefault(Key(weapon)) > 0)) return null;
            stockWeapons[Key(weapon)] = stockWeapons.GetValueOrDefault(Key(weapon)) - 1;
            return visual;
        }
        Pass((weapon, hardpoint) => hardpoint.Location == weapon.Location ? KeepStock(weapon, hardpoint) : null);
        Pass((weapon, hardpoint) => hardpoint.Location == weapon.Location ? Visual(weapon.Kind, hardpoint) : null);
        Pass(KeepStock);
        foreach (var hardpoint in free.Where(item => item.Stock is not null && item.Visuals.Contains(Blank)))
            visuals[hardpoint.Id] = Blank;
        return new(visuals.ToImmutable(), carriers.ToImmutable(), [.. pending.Select(weapon => weapon.Id)], false);
    }
}
