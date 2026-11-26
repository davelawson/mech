namespace Mech;

/// <summary>One mounted weapon's record over a battle. Armour, internal damage and kills count only damage this weapon's own hits
/// applied: an ammunition explosion they set off deals no credited damage, but its destroyed locations and any kill it causes count.
/// A missile counts as a shot; a laser's burn continuation does not.</summary>
public sealed record WeaponTally(int ShotsFired = 0, int Hits = 0, int ArmorDealt = 0, int InternalDealt = 0, int SystemsDamaged = 0,
    int ComponentsDestroyed = 0, int Kills = 0)
{
    public int DamageDealt => ArmorDealt + InternalDealt;
    /// <summary>Hits per shot fired, or null when the weapon never fired.</summary>
    public double? Accuracy => ShotsFired == 0 ? null : Hits / (double)ShotsFired;
    public WeaponTally Plus(WeaponTally other) => new(ShotsFired + other.ShotsFired, Hits + other.Hits, ArmorDealt + other.ArmorDealt,
        InternalDealt + other.InternalDealt, SystemsDamaged + other.SystemsDamaged, ComponentsDestroyed + other.ComponentsDestroyed, Kills + other.Kills);
}

/// <summary>A BattleMech's condition as the battle ends, worst last. It reads the mech's final state against its full loadout, not
/// what changed in the battle.</summary>
public enum MechCondition { Undamaged, Scratched, Damaged, Crippled, Destroyed }

/// <summary>A MechWarrior's fate in the battle, best first; the worst that applies is shown. NotEngaged is a reinforcement that never
/// arrived.</summary>
public enum PilotCondition { Healthy, Extracted, Injured, Fled, Ejected, Hostage, Killed, NotEngaged }

/// <summary>A significant moment of a battle, as one sentence for the post-battle highlights.</summary>
public sealed record CombatHighlight(double Time, string UnitId, string Text)
{
    public string Line => $"{TimeSpan.FromSeconds(Time):mm\\:ss}  {Text}";
}

/// <summary>The post-battle summary's figures for one unit.</summary>
public static class BattleSummary
{
    /// <summary>Destroyed: head or center torso gone. Crippled: any location gone. Damaged: structure missing or a system damaged or
    /// destroyed. Scratched: only armour below the loadout's installed amount.</summary>
    public static MechCondition Condition(CombatUnit unit)
    {
        if (!unit.Intact(MechLocation.Head) || !unit.Intact(MechLocation.CenterTorso)) return MechCondition.Destroyed;
        if (unit.Locations.Values.Any(part => part.Structure == 0)) return MechCondition.Crippled;
        var full = unit.Spec.Locations.ToDictionary(item => item.Location);
        if (unit.Locations.Values.Any(part => part.Structure < full[part.Location].Structure)
            || unit.Equipment.Values.Any(condition => condition != ComponentCondition.Operational)) return MechCondition.Damaged;
        if (unit.Locations.Values.Any(part => part.Armor < full[part.Location].Armor || part.RearArmor < full[part.Location].RearArmor))
            return MechCondition.Scratched;
        return MechCondition.Undamaged;
    }

    public static PilotCondition Pilot(CombatUnit unit) => unit.Inbound ? PilotCondition.NotEngaged : unit.WarriorKilled ? PilotCondition.Killed
        : unit.Surrendered ? PilotCondition.Hostage : unit.Ejected ? PilotCondition.Ejected : unit.Fled ? PilotCondition.Fled
        : unit.Injuries > 0 ? PilotCondition.Injured : unit.Extracted ? PilotCondition.Extracted : PilotCondition.Healthy;

    public static string Text(PilotCondition condition) => condition switch
    {
        PilotCondition.Hostage => "Taken hostage",
        PilotCondition.NotEngaged => "Not engaged",
        _ => condition.ToString()
    };

    /// <summary>The whole mech's record: the sum of its weapons'.</summary>
    public static WeaponTally Total(CombatUnit unit) => unit.Weapons.Aggregate(new WeaponTally(), (sum, weapon) => sum.Plus(weapon.Tally));

    /// <summary>"Medium Laser (RA)", or "(RT rear)" for a rear mount.</summary>
    public static string WeaponName(CombatWeapon weapon) =>
        $"{weapon.Spec.Name} ({DraftCatalog.Abbreviation(weapon.Spec.Location)}{(weapon.Spec.Rear ? " rear" : "")})";

    /// <summary>"Atlas AS7-D (Ace)": how the highlights name a mech and its MechWarrior.</summary>
    public static string Name(CombatUnit unit) => $"{unit.Spec.DisplayName} ({unit.Warrior.Callsign})";

    /// <summary>A location as a sentence reads it: "the left arm", "the CT".</summary>
    public static string Place(MechLocation location) => location == MechLocation.CenterTorso ? "the CT"
        : "the " + DraftCatalog.LocationName(location).ToLowerInvariant();

    /// <summary>"left torso and left arm", "head, left arm and right leg".</summary>
    public static string List(IEnumerable<string> items)
    {
        var all = items.ToArray();
        return all.Length <= 1 ? string.Concat(all) : string.Join(", ", all[..^1]) + " and " + all[^1];
    }
}
