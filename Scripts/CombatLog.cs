using System.Globalization;

namespace Mech;

public sealed record CombatDamage(MechLocation Location, int Armor, int RearArmor, int Structure, bool Destroyed);

/// <summary>Immutable identity, information tier and heat at the instant of a combat event.</summary>
public sealed record CombatLogUnit(CombatSide Side, int Slot, string Chassis, string Name, ContactLevel Visibility, double Heat)
{
    public string Label(bool debug) => Visibility == ContactLevel.Lost && !debug ? "Unknown attacker"
        : $"{(Side == CombatSide.Friendly ? "F" : "E")}{Slot} {(debug || Visibility == ContactLevel.Detailed ? Name : Chassis)}";
    public bool Detailed(bool debug) => debug || Visibility == ContactLevel.Detailed;
}

public sealed record CombatLogEntry(double Time, CombatShot Shot, CombatLogUnit Attacker, CombatLogUnit Target)
{
    public string? CombatText => Attacker.Side == CombatSide.Friendly || Attacker.Visibility != ContactLevel.Lost
        || Shot.Hit && Target.Side == CombatSide.Friendly ? Format(false) : null;
    public string DebugText => Format(true);

    private string Format(bool debug)
    {
        var fields = new List<string>
        {
            $"{TimeSpan.FromSeconds(Time):mm\\:ss\\.ff} {Attacker.Label(debug)} → {Target.Label(debug)}"
        };
        var attackerDetails = Attacker.Detailed(debug);
        if (attackerDetails)
        {
            fields.Add($"{Shot.Weapon} ({Shot.Mount})");
            fields.Add(FormattableString.Invariant($"heat +{Shot.GeneratedHeat} → {Attacker.Heat:0.0}"));
        }
        if (debug || Attacker.Visibility != ContactLevel.Lost)
            fields.Add(FormattableString.Invariant($"{Shot.From.DistanceTo(Shot.To):0.0} hex"));
        // Lower random draws hit. Increase precision when rounding would make a hit look like a tie.
        if (attackerDetails)
        {
            var decimals = 1;
            while (decimals < 15 && Math.Round(Shot.Chance * 100, decimals) == Math.Round(Shot.Roll * 100, decimals)) decimals++;
            var pattern = "F" + decimals;
            fields.Add($"chance {(Shot.Chance * 100).ToString(pattern, CultureInfo.InvariantCulture)}%, roll {(Shot.Roll * 100).ToString(pattern, CultureInfo.InvariantCulture)}% {(Shot.Hit ? "HIT" : "MISS")}");
        }
        else fields.Add(Shot.Hit ? "HIT" : "MISS");
        if (Target.Detailed(debug))
        {
            fields.Add($"dmg {Shot.Damage}");
            if (Shot.DamageApplied.Count == 0) fields.Add("armour −0, structure −0");
            foreach (var part in Shot.DamageApplied)
                fields.Add($"{DraftCatalog.Abbreviation(part.Location)} armour −{part.Armor}"
                    + (part.RearArmor > 0 ? $", rear −{part.RearArmor}" : "") + $", structure −{part.Structure}"
                    + (part.Destroyed ? " DESTROYED" : ""));
            if (Shot.SystemsLost.Count > 0) fields.Add("lost " + string.Join(", ", Shot.SystemsLost));
            if (Shot.TargetStatus is { } status) fields.Add($"target {status.ToString().ToUpperInvariant()}");
            if (Shot.TargetHeat > 0) fields.Add(FormattableString.Invariant($"target heat +{Shot.TargetHeat} → {Target.Heat:0.0}"));
        }
        return string.Join(" · ", fields);
    }
}

public sealed partial class CombatSimulation
{
    private CombatLogUnit LogUnit(CombatUnit unit)
    {
        var slot = Units.Where(item => item.Side == unit.Side).TakeWhile(item => item.Id != unit.Id).Count() + 1;
        var level = unit.Side == CombatSide.Friendly ? ContactLevel.Detailed
            : Contacts(CombatSide.Friendly).TryGetValue(unit.Id, out var contact) ? contact.Level : ContactLevel.Lost;
        return new(unit.Side, slot, unit.Spec.Chassis, unit.Spec.DisplayName, level, unit.Heat);
    }
}
