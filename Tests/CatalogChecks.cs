using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Mech.Tests;

internal static class CatalogChecks
{
    private static string Fixture(string name)
    {
        using var stream = typeof(CatalogChecks).Assembly.GetManifestResourceStream($"Mech.Tests.Fixtures.{name}.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    private static string CatalogJson()
    {
        using var stream = typeof(DraftCatalog).Assembly.GetManifestResourceStream("Mech.Data.mech-catalog.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static void Run(Action<bool, string> check)
    {
        CheckBaseline(check);
        CheckImmutability(check);
        CheckTextIndependence(check);
        CheckValidation(check);
        CheckSave(check);
    }
    private static void CheckBaseline(Action<bool, string> check)
    {
        var baseline = JsonNode.Parse(Fixture("catalog-baseline"))!;
        var expected = baseline["Mechs"]!.AsArray();
        check(expected.Select(m => m!["Id"]!.GetValue<string>()).SequenceEqual(DraftCatalog.AllMechs.Select(m => m.Id)),
            "typed catalog preserves all stock identities and their deterministic order");
        // The fixture keeps stock points. The game counts tenths of a point and cuts machine gun damage from 2 to 0.5.
        const int scale = CombatRules.DamageScale;
        static int StockPerProjectile(WeaponSpec w) => w.Kind == WeaponKind.MachineGun ? 2 : w.DamageProfile.PerProjectile / scale;
        foreach (var (m, old) in DraftCatalog.AllMechs.Zip(expected))
        {
            var projection = JsonSerializer.SerializeToNode(new
            {
                m.Id, m.Chassis, m.Variant, m.Tonnage, WeightClass = DefinitionText.WeightClass(m.WeightClass), m.Introduced,
                m.Role, m.Walk, m.Run, m.Jump, m.HeatSinks, m.Engine, HeatSinkType = m.HeatSinkType.ToString(),
                Locations = m.Locations.Select(l => new { Location = l.Location.ToString(), Armor = l.Armor / scale, RearArmor = l.RearArmor / scale,
                    Structure = l.Structure / scale }),
                Weapons = m.Weapons.Select(w => new { w.Id, w.Name, Location = w.Location.ToString(), w.Rear, w.Heat,
                    Damage = DefinitionText.Damage(w.DamageProfile with { PerProjectile = StockPerProjectile(w) }), w.Minimum, w.Short, w.Medium, w.Long, AmmoType = AmmoText(w.AmmoKind) }),
                Components = m.Components.Select(c => new { c.Id, c.Name, Location = c.Location.ToString(), c.Slots }),
                Ammo = m.Ammo.Select(a => new { a.Id, a.Name, Location = a.Location.ToString(), AmmoType = AmmoText(a.AmmoKind), a.Shots }),
                m.Source
            });
            check(m.Locations.All(l => l.Armor % scale == 0 && l.RearArmor % scale == 0 && l.Structure % scale == 0)
                && m.Weapons.All(w => w.DamageProfile.PerProjectile == (w.Kind == WeaponKind.MachineGun ? 5 : StockPerProjectile(w) * scale)),
                $"{m.Id} counts protection and damage in tenths of a stock point, with machine guns at 0.5");
            check(JsonNode.DeepEquals(projection, old), $"{m.Id} preserves original facts, equipment IDs/grouping/order, provenance and display text");
            foreach (var (w, prior) in m.Weapons.Zip(old!["Weapons"]!.AsArray()))
            {
                // Deliberately use the previous representation as the independent oracle.
                var text = prior!["Damage"]!.GetValue<string>(); var name = prior["Name"]!.GetValue<string>();
                var packet = int.Parse(text.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
                if (text.Contains("/ missile", StringComparison.Ordinal)) packet *= int.Parse(name.Split(' ')[^1], System.Globalization.CultureInfo.InvariantCulture);
                packet = name == "Machine Gun" ? 5 : packet * scale;
                var cooldown = name switch { "Machine Gun" => 2, "Flamer" or "Small Laser" => 6, "Medium Laser" => 8, _ => 10 };
                check(CombatRules.Damage(w) == packet && CombatRules.Cooldown(w) == cooldown
                    && CombatRules.FlamerHeat(w) == (name == "Flamer" ? 2 : 0), $"{m.Id}/{w.Id} preserves damage, cadence and target heat");
            }
            check(m.Components.All(c => c.HeatSinkCount == (c.Kind == ComponentKind.HeatSink ? c.Slots : 0)),
                $"{m.Id} preserves explicit sink contributions");
        }
        var warriors = JsonSerializer.SerializeToNode(DraftCatalog.Warriors.Select(w => new
            { w.Id, w.Name, w.Callsign, w.Gunnery, w.Piloting, w.Sensors, w.Experience }));
        check(JsonNode.DeepEquals(warriors, baseline["Warriors"]), "all authored warriors preserve identity, skills, labels and order");
        var meta = DraftCatalog.Metadata;
        check(meta.SchemaVersion == 1 && meta.Source == baseline["Source"]!.GetValue<string>()
            && meta.Revision == baseline["Revision"]!.GetValue<string>() && meta.License == baseline["License"]!.GetValue<string>()
            && meta.Attribution == baseline["Attribution"]!.GetValue<string>() && meta.LicenseUrl == baseline["LicenseUrl"]!.GetValue<string>(),
            "typed catalog preserves pinned source and complete attribution");
    }
    private static string? AmmoText(AmmoKind? kind) => kind switch
    {
        null => null, AmmoKind.MachineGun => "Machine Gun", AmmoKind.AC2 => "AC/2", AmmoKind.AC5 => "AC/5",
        AmmoKind.AC10 => "AC/10", AmmoKind.AC20 => "AC/20", AmmoKind.LRM5 => "LRM 5", AmmoKind.LRM10 => "LRM 10",
        AmmoKind.LRM15 => "LRM 15", AmmoKind.LRM20 => "LRM 20", AmmoKind.SRM2 => "SRM 2", AmmoKind.SRM4 => "SRM 4", AmmoKind.SRM6 => "SRM 6",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static void CheckImmutability(Action<bool, string> check)
    {
        void ReadOnly<T>(ImmutableArray<T> items, string label)
        {
            var original = items[0]; var rejected = false;
            try { ((IList<T>)items)[0] = original; } catch (NotSupportedException) { rejected = true; }
            check(rejected && EqualityComparer<T>.Default.Equals(items[0], original), $"{label} rejects mutation through IList");
        }
        var stock = DraftCatalog.Mech("firestarter"); var source = stock.Weapons.ToArray();
        var detached = stock with { Weapons = source.ToImmutableArray() }; var originalWeapon = source[0];
        source[0] = source[0] with { Heat = 999 };
        check(detached.Weapons[0] == originalWeapon && stock.Weapons[0] == originalWeapon, "definition construction copies caller-owned arrays");
        ReadOnly(stock.Locations, "locations"); ReadOnly(stock.Weapons, "weapons");
        ReadOnly(DraftCatalog.Mech("awesome").Components, "components"); ReadOnly(stock.Ammo, "ammunition");
        ReadOnly(DraftCatalog.AllMechs, "complete mech catalog"); ReadOnly(DraftCatalog.Mechs, "draft subset");
        ReadOnly(DraftCatalog.Warriors, "warrior catalog"); ReadOnly(DraftCatalog.PoolNames, "pool labels");
        var roster = ForceRoster.Create("Shared definitions", [stock, stock], DraftCatalog.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var sim = CombatSimulation.Deploy(roster, roster.Mechs.Select(m => m.Id), seed: 41, terrainSeed: 41);
        var changed = stock with { Weapons = stock.Weapons.SetItem(0, originalWeapon with
            { DamageProfile = originalWeapon.DamageProfile with { PerProjectile = 999 } }) };
        sim.Units[0].ApplyDamage(MechLocation.LeftArm, 10000);
        check(ReferenceEquals(roster.Mechs[0].Spec, roster.Mechs[1].Spec) && sim.Units.All(u => ReferenceEquals(u.Spec, stock))
            && changed.Weapons[0].DamageProfile.PerProjectile == 999 && stock.Weapons[0].DamageProfile.PerProjectile == 50
            && roster.Mechs.All(m => m.LocationIntact(MechLocation.LeftArm)) && sim.Units[1].Intact(MechLocation.LeftArm),
            "two owned mechs and mirrored combat units safely share definitions while copies and condition change independently");
    }
    private static CombatUnit Unit(MechSpec spec, string? destroyed = null) => new("fixture", spec, DraftCatalog.Warriors[0], CombatSide.Friendly,
        spec.Locations.Select(l => new LocationCondition(l.Location, l.Armor, l.RearArmor, l.Structure)),
        spec.Weapons.Select(w => w.Id).Concat(spec.Components.Select(c => c.Id)).Concat(spec.Ammo.Select(a => a.Id))
            .Select(id => new EquipmentCondition(id, id == destroyed ? ComponentCondition.Destroyed : ComponentCondition.Operational)),
        spec.Ammo.Select(a => new AmmoCondition(a.Id, a.Shots)), Vector2.Zero);
    private static void CheckTextIndependence(Action<bool, string> check)
    {
        foreach (var stock in DraftCatalog.AllMechs)
        {
            var renamed = stock with { Weapons = stock.Weapons.Select(w => w with { Name = "Localized weapon" }).ToImmutableArray(),
                Components = stock.Components.Select(c => c with { Name = "Localized component" }).ToImmutableArray(),
                Ammo = stock.Ammo.Select(a => a with { Name = "Localized bin" }).ToImmutableArray() };
            var before = Unit(stock); var after = Unit(renamed);
            check(stock.Weapons.Zip(renamed.Weapons).All(pair => CombatRules.Damage(pair.First) == CombatRules.Damage(pair.Second)
                && CombatRules.Cooldown(pair.First) == CombatRules.Cooldown(pair.Second)
                && CombatRules.FlamerHeat(pair.First) == CombatRules.FlamerHeat(pair.Second))
                && before.Weapons.All(before.WeaponUsable) && after.Weapons.All(after.WeaponUsable)
                && before.PreferredDistance == after.PreferredDistance && before.CoolingRate == after.CoolingRate,
                $"{stock.Id} display names cannot change weapon rules, ammunition, preference or cooling");
            var roster = ForceRoster.Create("Names", [stock], DraftCatalog.Warriors.Take(1)); var snapshot = roster.Snapshot().Mechs[0];
            check(new OwnedMech(snapshot, renamed).Operational, $"{stock.Id} renamed core systems retain operational status");
            var core = stock.Components.First(c => c.Kind == ComponentKind.Cockpit);
            var damaged = snapshot with { Equipment = snapshot.Equipment.Select(e => e.Id == core.Id ? e with { Condition = ComponentCondition.Destroyed } : e).ToArray() };
            check(!new OwnedMech(damaged, stock).Operational && !new OwnedMech(damaged, renamed).Operational,
                $"{stock.Id} renamed destroyed core systems still block readiness");
        }
        var sink = new ComponentSpec("sink", "Unrelated label", MechLocation.LeftArm, 3, ComponentKind.HeatSink, 1);
        var custom = DraftCatalog.Mech("mauler") with { Components = [sink] };
        var unit = Unit(custom, sink.Id);
        check(unit.EffectiveHeatSinks == 10 && unit.CoolingRate == 2, "sink contribution is independent of name and critical-slot count");
        unit.ApplyDamage(sink.Location, 1000);
        check(unit.EffectiveHeatSinks == 10, "destroyed and lost sink groups are subtracted once");
        var mauler = Unit(DraftCatalog.Mech("mauler"));
        check(mauler.EffectiveHeatSinks == 11 && mauler.CoolingRate == 2.2, "Mauler retains all eleven internal double sinks");
        check(DraftCatalog.Warriors.All(w => (w with { Specialty = WarriorSpecialty.None }).SkillBudget == w.SkillBudget),
            "warrior specialty labels do not determine budgets");
    }
    private static void CheckValidation(Action<bool, string> check)
    {
        var json = CatalogJson();
        void Reject(string label, Action<JsonNode> change, string diagnostic)
        {
            var node = JsonNode.Parse(json)!; change(node); var rejected = false;
            try { using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString())); _ = CatalogLoader.Read(stream); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains(diagnostic, StringComparison.Ordinal); }
            check(rejected, $"catalog rejects {label} with field context");
        }
        static JsonNode M(JsonNode n) => n["Mechs"]![0]!;
        static JsonNode W(JsonNode n) => M(n)["Weapons"]![0]!;
        Reject("missing schema", n => n.AsObject().Remove("SchemaVersion"), "SchemaVersion");
        Reject("unsupported schema", n => n["SchemaVersion"] = 2, "SchemaVersion");
        Reject("empty document collection", n => n["Mechs"] = new JsonArray(), "Mechs");
        Reject("null mech", n => n["Mechs"]![0] = null, "Mechs");
        Reject("missing field", n => W(n).AsObject().Remove("Heat"), "Heat");
        Reject("duplicate mech ID", n => n["Mechs"]![1]!["Id"] = M(n)["Id"]!.DeepClone(), "Id");
        Reject("empty mech ID", n => M(n)["Id"] = "", "Id");
        Reject("integer enum", n => W(n)["Kind"] = 0, "Kind");
        Reject("unsupported enum", n => W(n)["Kind"] = "FutureGun", "Kind");
        Reject("null equipment list", n => M(n)["Components"] = null, "Equipment");
        Reject("null weapon", n => M(n)["Weapons"]![0] = null, "Weapons");
        Reject("duplicate equipment ID across categories", n => M(n)["Ammo"]![0]!["Id"] = W(n)["Id"]!.DeepClone(), "weapon-0");
        Reject("invalid mount", n => W(n)["Location"] = "Tail", "Location");
        Reject("duplicate location", n => M(n)["Locations"]![1]!["Location"] = "Head", "Locations");
        Reject("negative armor", n => M(n)["Locations"]![0]!["Armor"] = -1, "Head");
        Reject("zero structure", n => M(n)["Locations"]![0]!["Structure"] = 0, "Head");
        Reject("rear armor on head", n => M(n)["Locations"]![0]!["RearArmor"] = 1, "RearArmor");
        Reject("negative movement", n => M(n)["Walk"] = -1, "Movement");
        Reject("incorrect weight class", n => M(n)["WeightClass"] = "Assault", "WeightClass");
        Reject("negative heat", n => W(n)["Heat"] = -1, "weapon-0");
        Reject("reversed ranges", n => W(n)["Medium"] = 1, "weapon-0");
        Reject("missing damage profile", n => W(n)["DamageProfile"] = null, "DamageProfile");
        Reject("overflowing damage", n => { W(n)["DamageProfile"]!["PerProjectile"] = int.MaxValue; W(n)["DamageProfile"]!["ProjectileCount"] = 2; }, "DamageProfile");
        Reject("incorrect rack count", n => W(n)["DamageProfile"]!["ProjectileCount"] = 2, "DamageProfile");
        Reject("incorrect target heat", n => W(n)["DamageProfile"]!["TargetHeat"] = 2, "TargetHeat");
        Reject("incorrect ammunition category", n => W(n)["AmmoKind"] = "MachineGun", "AmmoKind");
        Reject("missing compatible bin", n => M(n)["Ammo"] = new JsonArray(), "AmmoKind");
        Reject("empty ammunition capacity", n => M(n)["Ammo"]![0]!["Shots"] = 0, "Shots");
        Reject("invalid component slots", n => M(n)["Components"]![0]!["Slots"] = 0, "Slots");
        Reject("non-sink cooling contribution", n => M(n)["Components"]![0]!["HeatSinkCount"] = 1, "HeatSinkCount");
        Reject("missing core system", n => { var c = M(n)["Components"]!.AsArray(); c.Remove(c.First(x => x!["Kind"]!.GetValue<string>() == "Cockpit")); }, "Cockpit");
        Reject("excess explicit sinks", n => { var c = M(n)["Components"]![0]!; c["Kind"] = "HeatSink"; c["HeatSinkCount"] = 999; }, "HeatSinks");
        Reject("missing attribution", n => n["Attribution"] = null, "Attribution");
        Reject("unpinned revision", n => n["Revision"] = "main", "Revision");
        Reject("mismatched entry provenance", n => M(n)["Source"] = "https://example.com/unit", "Source");
        void RejectWarrior(string label, params WarriorSpec[] warriors)
        {
            var rejected = false;
            try { _ = CatalogLoader.ValidateWarriors(warriors); } catch (InvalidOperationException error) { rejected = error.Message.Contains("warrior", StringComparison.Ordinal); }
            check(rejected, $"warrior catalog rejects {label}");
        }
        var regular = DraftCatalog.Warriors[0];
        RejectWarrior("duplicate identities", regular, regular);
        RejectWarrior("unknown experience", regular with { Rank = (WarriorExperience)99 });
        RejectWarrior("unknown specialty", regular with { Specialty = (WarriorSpecialty)99 });
        RejectWarrior("unbounded skills", regular with { Gunnery = -10 });
        RejectWarrior("incorrect budget", regular with { Rank = WarriorExperience.Veteran });
    }
    private static void CheckSave(Action<bool, string> check)
    {
        var fixture = Fixture("definition-save"); var path = Path.Combine(Path.GetTempPath(), $"definition-save-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, fixture); var store = new CampaignStore(path); var campaign = store.Load();
            var mech = campaign.Roster!.Mechs[0]; var warrior = campaign.Roster.Warriors[0];
            check(ReferenceEquals(mech.Spec, DraftCatalog.Mech("firestarter")) && mech.WarriorId == "mara-vale"
                && warrior.Spec.SkillBudget == 6 && warrior.Health == WarriorHealth.Injured
                && warrior.RecoveryDate == new DateOnly(3025, 1, 3) && mech.Condition("weapon-0") == ComponentCondition.Damaged
                && mech.Ammo.All(a => a.Shots == mech.Spec.Ammo.Single(s => s.Id == a.Id).Shots / 2),
                "pre-refactor campaign save resolves typed definitions and retains damage, ammo, assignment and injury");
            store.Save(campaign);
            // Resaving upgrades to version 5, which counts armor and structure in tenths of a point.
            var expected = JsonNode.Parse(fixture)!; expected["Version"] = 5;
            foreach (var location in expected["Roster"]!["Mechs"]!.AsArray().SelectMany(m => m!["Locations"]!.AsArray()))
                foreach (var field in new[] { "Armor", "RearArmor", "Structure" })
                    location![field] = location[field]!.GetValue<int>() * CombatRules.DamageScale;
            check(JsonNode.DeepEquals(expected, JsonNode.Parse(File.ReadAllText(path))),
                "a version-four save keeps its shape and values when resaved, with protection rescaled");
            campaign.AdvanceOneDay(); campaign.AdvanceOneDay();
            check(warrior.Health == WarriorHealth.Healthy && warrior.RecoveryDate is null, "old saved recovery date still resolves normally");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
