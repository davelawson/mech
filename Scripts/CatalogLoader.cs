using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mech;

internal sealed record CatalogMetadata(int SchemaVersion, string Source, string Revision, string License,
    string Attribution, string LicenseUrl);
internal sealed record GameCatalog(CatalogMetadata Metadata, ImmutableArray<MechSpec> Mechs);

/// <summary>Validates transport data before publishing any shared definition.</summary>
internal static class CatalogLoader
{
    private sealed record CatalogFile(int SchemaVersion, string Source, string Revision, string License,
        string Attribution, string LicenseUrl, MechFile[] Mechs);
    private sealed record MechFile(string Id, string Chassis, string Variant, int Tonnage, MechWeightClass WeightClass,
        int Introduced, string Role, int Walk, int Run, int Jump, int HeatSinks, string Engine,
        LocationSpec[] Locations, WeaponSpec[] Weapons, ComponentSpec[] Components, AmmoSpec[] Ammo,
        string Source, HeatSinkKind HeatSinkType);
    private static readonly JsonSerializerOptions Options = new()
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    internal static GameCatalog Read(Stream stream)
    {
        CatalogFile file;
        try { file = JsonSerializer.Deserialize<CatalogFile>(stream, Options) ?? throw Invalid("catalog", "empty document"); }
        catch (JsonException error) { throw Invalid("catalog", $"{error.Path}: {error.Message}"); }
        Require(file.SchemaVersion == 1, "catalog.SchemaVersion", "unsupported version");
        Require(file.Source == "MegaMek/mm-data", "catalog.Source", "unsupported source");
        Require(file.Revision is { Length: 40 } && file.Revision.All(Uri.IsHexDigit), "catalog.Revision", "expected a pinned SHA");
        Text(file.License, "catalog.License"); Text(file.Attribution, "catalog.Attribution");
        Require(Uri.TryCreate(file.LicenseUrl, UriKind.Absolute, out var license) && license.Scheme == "https",
            "catalog.LicenseUrl", "expected an HTTPS license URL");
        Require(file.Mechs is { Length: > 0 }, "catalog.Mechs", "missing definitions");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var mechs = ImmutableArray.CreateBuilder<MechSpec>();
        foreach (var m in file.Mechs!)
        {
            Require(m is not null, "catalog.Mechs", "null definition");
            var path = $"mech {m!.Id}";
            Text(m.Id, path + ".Id"); Require(ids.Add(m.Id), path + ".Id", "duplicate identity");
            Text(m.Chassis, path + ".Chassis"); Text(m.Variant, path + ".Variant"); Text(m.Engine, path + ".Engine");
            Require(m.Role is not null, path + ".Role", "null text");
            Require(m.Tonnage is >= 20 and <= 100 && m.Tonnage % 5 == 0, path + ".Tonnage", "unsupported stock mass");
            Defined(m.WeightClass, path + ".WeightClass"); Defined(m.HeatSinkType, path + ".HeatSinkType");
            var weight = m.Tonnage <= 35 ? MechWeightClass.Light : m.Tonnage <= 55 ? MechWeightClass.Medium
                : m.Tonnage <= 75 ? MechWeightClass.Heavy : MechWeightClass.Assault;
            Require(m.WeightClass == weight, path + ".WeightClass", "does not match tonnage");
            Require(m.Walk >= 0 && m.Run >= m.Walk && m.Jump >= 0, path + ".Movement", "invalid movement points");
            Require(m.HeatSinks >= 0 && m.Introduced > 0, path, "invalid heat sinks or introduction year");
            var prefix = $"https://raw.githubusercontent.com/MegaMek/mm-data/{file.Revision}/data/mekfiles/";
            Require(m.Source is not null && m.Source.StartsWith(prefix, StringComparison.Ordinal)
                && Uri.TryCreate(m.Source, UriKind.Absolute, out _), path + ".Source", "does not match pinned revision");
            Require(m.Locations is { Length: 8 } && m.Locations.All(l => l is not null)
                && m.Locations.Select(l => l.Location).Distinct().Count() == 8, path + ".Locations", "expected eight distinct locations");
            foreach (var l in m.Locations!)
            {
                Defined(l.Location, path + ".Locations");
                Require(l.Armor >= 0 && l.RearArmor >= 0 && l.Structure > 0, path + $".{l.Location}", "invalid protection");
                Require(l.RearArmor == 0 || l.Location is MechLocation.CenterTorso or MechLocation.LeftTorso or MechLocation.RightTorso,
                    path + $".{l.Location}.RearArmor", "rear armor requires a torso");
            }
            Require(m.Weapons is { Length: > 0 } && m.Components is not null && m.Ammo is not null,
                path + ".Equipment", "missing collection or weapons");
            var equipmentIds = new HashSet<string>(StringComparer.Ordinal);
            void Equipment(string id, string name, MechLocation location)
            {
                Text(id, path + ".Equipment.Id"); Text(name, path + $".{id}.Name");
                Require(equipmentIds.Add(id), path + $".{id}", "duplicate equipment identity");
                Defined(location, path + $".{id}.Location");
            }
            foreach (var w in m.Weapons!)
            {
                Require(w is not null, path + ".Weapons", "null weapon");
                Equipment(w!.Id, w.Name, w.Location); var p = path + $".{w.Id}";
                Defined(w.Kind, p + ".Kind");
                Require(w.Heat >= 0 && w.Minimum >= 0 && w.Minimum <= w.Short && w.Short > 0
                    && w.Short <= w.Medium && w.Medium <= w.Long, p + ".Ranges/Heat", "invalid boundaries or heat");
                Require(w.DamageProfile is not null, p + ".DamageProfile", "missing damage");
                var d = w.DamageProfile!; Defined(d.Kind, p + ".DamageProfile.Kind");
                Require(d.PerProjectile > 0 && d.ProjectileCount > 0 && d.TargetHeat >= 0
                    && (long)d.PerProjectile * d.ProjectileCount <= int.MaxValue, p + ".DamageProfile", "invalid or overflowing damage");
                var definition = WeaponDefinitions.Of(w.Kind);
                Require(d.ProjectileCount == definition.Rack && d.Kind == (definition.FiresSalvo ? DamageKind.Missile : DamageKind.Direct),
                    p + ".DamageProfile", "does not match weapon category");
                Require(d.TargetHeat == (definition.Family == WeaponFamily.Flamer ? 2 : 0), p + ".DamageProfile.TargetHeat", "does not match weapon category");
                Require(w.AmmoKind == definition.Ammo, p + ".AmmoKind", "does not match weapon category");
                if (w.AmmoKind is { } kind) Defined(kind, p + ".AmmoKind");
            }
            long externalSinks = 0;
            foreach (var c in m.Components!)
            {
                Require(c is not null, path + ".Components", "null component");
                Equipment(c!.Id, c.Name, c.Location); Defined(c.Kind, path + $".{c.Id}.Kind");
                Require(c.Slots > 0 && (c.Kind == ComponentKind.HeatSink ? c.HeatSinkCount > 0 : c.HeatSinkCount == 0),
                    path + $".{c.Id}.Slots/HeatSinkCount", "invalid grouped contribution");
                externalSinks += c.HeatSinkCount;
            }
            Require(externalSinks <= m.HeatSinks, path + ".HeatSinks", "explicit groups exceed stock total");
            foreach (var required in new[] { ComponentKind.Cockpit, ComponentKind.FusionEngine, ComponentKind.Gyro,
                ComponentKind.Hip, ComponentKind.UpperLegActuator, ComponentKind.LowerLegActuator })
                Require(m.Components.Any(c => c.Kind == required), path + ".Components", $"missing {required}");
            foreach (var a in m.Ammo!)
            {
                Require(a is not null, path + ".Ammo", "null bin");
                Equipment(a!.Id, a.Name, a.Location); Defined(a.AmmoKind, path + $".{a.Id}.AmmoKind");
                Require(a.Shots > 0, path + $".{a.Id}.Shots", "invalid stock capacity");
            }
            foreach (var w in m.Weapons.Where(w => w.AmmoKind is not null))
                Require(m.Ammo.Any(a => a.AmmoKind == w.AmmoKind), path + $".{w.Id}.AmmoKind", "no compatible bin");
            mechs.Add(new(m.Id, m.Chassis, m.Variant, m.Tonnage, m.WeightClass, m.Introduced, m.Role!, m.Walk, m.Run,
                m.Jump, m.HeatSinks, m.Engine, m.Locations.ToImmutableArray(), m.Weapons.ToImmutableArray(),
                Individual(m.Components, m.HeatSinkType, path), m.Ammo.ToImmutableArray(), m.Source!, m.HeatSinkType));
        }
        return new(new(file.SchemaVersion, file.Source, file.Revision, file.License, file.Attribution, file.LicenseUrl), mechs.ToImmutable());
    }

    /// <summary>Splits grouped heat sink and jump jet entries into one item per sink or jet, ids suffixed `.1`, `.2`, ….</summary>
    private static ImmutableArray<ComponentSpec> Individual(IEnumerable<ComponentSpec> components, HeatSinkKind sinks, string path) =>
        components.SelectMany(c =>
        {
            var count = c.Kind switch { ComponentKind.HeatSink => c.HeatSinkCount, ComponentKind.JumpJet => c.Slots, _ => 1 };
            if (count == 1) return [c];
            var slots = c.Kind == ComponentKind.HeatSink ? EquipmentRules.HeatSinkSlots(sinks) : 1;
            Require(c.Slots == count * slots, path + $".{c.Id}.Slots", "does not divide into individual items");
            return Enumerable.Range(1, count).Select(index => c with
                { Id = GroupedId(c.Id, index), Slots = slots, HeatSinkCount = c.Kind == ComponentKind.HeatSink ? 1 : 0 });
        }).ToImmutableArray();
    internal static string GroupedId(string group, int index) => $"{group}.{index}";

    internal static ImmutableArray<WarriorSpec> ValidateWarriors(IEnumerable<WarriorSpec> source)
    {
        var warriors = source.ToImmutableArray(); var ids = new HashSet<string>(StringComparer.Ordinal);
        Require(warriors.Length > 0, "warriors", "empty catalog");
        foreach (var w in warriors)
        {
            Require(w is not null, "warriors", "null definition"); var path = $"warrior {w!.Id}";
            Text(w.Id, path + ".Id"); Text(w.Name, path + ".Name"); Text(w.Callsign, path + ".Callsign");
            Require(ids.Add(w.Id), path + ".Id", "duplicate identity");
            Defined(w.Rank, path + ".Rank"); Defined(w.Specialty, path + ".Specialty");
            Require(new[] { w.Gunnery, w.Piloting, w.Sensors }.All(v => v is >= WarriorSkills.Expert and <= WarriorSkills.Novice),
                path + ".Skills", "ratings must be between 2 and 6");
            Require(w.SkillPoints == w.SkillBudget, path + ".Skills", $"must spend exactly {w.SkillBudget} points");
        }
        return warriors;
    }
    private static void Defined<T>(T value, string path) where T : struct, Enum => Require(Enum.IsDefined(value), path, "unsupported category");
    private static void Text(string? value, string path) => Require(!string.IsNullOrWhiteSpace(value), path, "missing text");
    private static void Require(bool condition, string path, string message) { if (!condition) throw Invalid(path, message); }
    private static InvalidOperationException Invalid(string path, string message) => new($"Invalid {path}: {message}.");
}
