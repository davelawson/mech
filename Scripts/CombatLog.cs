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

public sealed record CombatLogEntry(double Time, CombatShot Shot, CombatLogUnit Attacker, CombatLogUnit Target, CombatLogUnit? Spotter = null)
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
            fields.Add($"{Shot.Weapon} ({Shot.Mount})" + (Shot.Burn ? " burn continues" : Shot.Impact ? " impact" : ""));
            if (!Shot.Burn && !Shot.Impact) fields.Add(FormattableString.Invariant($"heat +{Shot.GeneratedHeat} → {Attacker.Heat:0.0}"));
        }
        else if (Shot.Burn) fields.Add("burn continues");
        else if (Shot.Impact) fields.Add("impact");
        // Enemy spotters are named only at Detailed contact.
        if (Shot.Indirect && !Shot.Impact)
            fields.Add(Spotter is { } spotter && spotter.Detailed(debug) ? $"indirect, spotted by {spotter.Label(debug)}" : "indirect");
        if (debug || Attacker.Visibility != ContactLevel.Lost)
            fields.Add(FormattableString.Invariant($"{Shot.From.DistanceTo(Shot.To):0.0} hex"));
        // Lower random draws hit. Increase precision when rounding would make a hit look like a tie.
        if (Shot.Burn || Shot.Impact) { } // Continuations and impacts inherit their discharge's roll.
        else if (Shot.Missiles.Count > 0)
        {
            var hits = Shot.Missiles.Count(missile => missile.Hit);
            fields.Add(attackerDetails ? FormattableString.Invariant($"chance {Shot.Chance * 100:F1}%, {hits}/{Shot.Missiles.Count} {(Shot.Hit ? "HIT" : "MISS")}")
                : Shot.Hit ? "HIT" : "MISS");
            if (debug) fields.Add("rolls " + string.Join(", ", Shot.Missiles.Select(missile => FormattableString.Invariant(
                $"{missile.Roll * 100:F1}% {(missile.Hit ? DraftCatalog.Abbreviation(missile.Location) : "miss")}"))));
        }
        else if (attackerDetails)
        {
            var decimals = 1;
            while (decimals < 15 && Math.Round(Shot.Chance * 100, decimals) == Math.Round(Shot.Roll * 100, decimals)) decimals++;
            var pattern = "F" + decimals;
            fields.Add($"chance {(Shot.Chance * 100).ToString(pattern, CultureInfo.InvariantCulture)}%, roll {(Shot.Roll * 100).ToString(pattern, CultureInfo.InvariantCulture)}% {(Shot.Hit ? "HIT" : "MISS")}");
        }
        else fields.Add(Shot.Hit ? "HIT" : "MISS");
        if (Shot.InFlight) fields.Add(FormattableString.Invariant($"in flight {Shot.FlightSeconds:0.00}s"));
        if (Shot.Wreck) fields.Add("no damage, target destroyed");
        else if (Shot.InFlight)
        {
            if (!Shot.Hit || !Target.Detailed(debug)) { }
            else if (Shot.Missiles.Count > 0)
                fields.Add("hits " + string.Join(", ", Shot.Missiles.Where(missile => missile.Hit).GroupBy(missile => missile.Location)
                    .OrderBy(group => group.Key).Select(group => $"{DraftCatalog.Abbreviation(group.Key)}×{group.Count()}")));
            if (Shot.Hit && Target.Detailed(debug))
            {
                fields.Add($"dmg {Shot.Damage}" + (Shot.Splash > 0 ? $" + {Shot.Splash} splash" : "") + " on impact");
                if (Shot.Splash > 0) fields.Add("splash " + string.Join(", ", Shot.SplashPoints.GroupBy(location => location)
                    .OrderBy(group => group.Key).Select(group => $"{DraftCatalog.Abbreviation(group.Key)}×{group.Count()}")));
            }
        }
        else if (Target.Detailed(debug))
        {
            fields.Add($"dmg {Shot.Damage}" + (Shot.Splash > 0 ? $" + {Shot.Splash} splash" : "") + (Shot.SecondLocation is { } second
                ? FormattableString.Invariant($", burn +{Shot.SecondDamage} {DraftCatalog.Abbreviation(second)} in {CombatRules.LaserSecondHalfDelay:0.0}s") : ""));
            if (Shot.DamageApplied.Count == 0) fields.Add("armour −0, structure −0");
            foreach (var part in Shot.DamageApplied)
                fields.Add($"{DraftCatalog.Abbreviation(part.Location)} armour −{part.Armor}"
                    + (part.RearArmor > 0 ? $", rear −{part.RearArmor}" : "") + $", structure −{part.Structure}"
                    + (part.Destroyed ? " DESTROYED" : ""));
            if (Shot.SystemsLost.Count > 0) fields.Add("lost " + string.Join(", ", Shot.SystemsLost));
            if (Shot.TargetStatus is { } status) fields.Add($"target {status.ToString().ToUpperInvariant()}");
            if (Shot.TargetHeat > 0) fields.Add(FormattableString.Invariant($"target heat +{Shot.TargetHeat} → {Target.Heat:0.0}"));
        }
        if (Shot.Impact && Shot.Splash > 0)
            fields.Add(FormattableString.Invariant($"electro-interference {CombatRules.InterferenceSeconds:0}s"));
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
    /// <summary>The stable F1–F4 / E1–E4 slot label.</summary>
    public string SlotLabel(CombatUnit unit) =>
        $"{(unit.Side == CombatSide.Friendly ? "F" : "E")}{Units.Where(item => item.Side == unit.Side).TakeWhile(item => item.Id != unit.Id).Count() + 1}";
}
