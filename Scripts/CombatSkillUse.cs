namespace Mech;

/// <summary>Counting how friendly MechWarriors use their skills in battle, and the skill gain checks rolled from it once the battle is
/// over (see <see cref="SkillGrowth"/>). Gunnery's hits and kills are the unit's own tallies; Piloting checks and nerve checks count
/// themselves where they are rolled. This counts movement in contact and the Sensors uses, once per step on the leader's machine.</summary>
public sealed partial class CombatSimulation
{
    private const int SkillSeedSalt = 0x5ca1ab1e;
    /// <summary>Enemies the friendly side has identified at least once.</summary>
    private readonly HashSet<string> _identifiedOnce = new(StringComparer.Ordinal);

    private void RecordSkillUse()
    {
        var friendly = Units.Where(item => item.Side == CombatSide.Friendly).ToArray();
        var enemies = Units.Where(item => item.Side == CombatSide.Enemy && !item.OffField).ToArray();
        var contact = enemies.Any(item => !item.Defeated && Detected(CombatSide.Friendly, item.Id));
        foreach (var unit in friendly)
        {
            if (unit.Defeated) { unit.CountedPosition = null; continue; }
            if (contact && unit.CountedPosition is { } from)
                unit.Use = unit.Use with { MetresInContact = unit.Use.MetresInContact + from.DistanceTo(unit.Position) };
            unit.CountedPosition = unit.Position;
        }
        var observers = friendly.Where(item => item.Active).ToArray();
        foreach (var target in enemies)
        {
            if (!Detected(CombatSide.Friendly, target.Id)) continue;
            var first = !_identifiedOnce.Contains(target.Id);
            var aimed = !target.Defeated && friendly.Any(item => item.Active && (item.CurrentTarget == target.Id || item.CurrentRearTarget == target.Id));
            if (!first && !aimed) continue;
            var seeing = observers.Where(observer => Identifies(observer, target)).ToArray();
            if (seeing.Length == 0) continue;
            if (first)
            {
                _identifiedOnce.Add(target.Id);
                var spotter = seeing.MinBy(observer => observer.Position.DistanceSquaredTo(target.Position))!;
                spotter.Use = spotter.Use with { FirstIdentified = [.. spotter.Use.Identified, target.Id] };
            }
            if (aimed && seeing.Length == 1)
                seeing[0].Use = seeing[0].Use with { SoleSpotterSeconds = seeing[0].Use.SoleSpotterSeconds + CombatRules.StepSeconds };
        }
    }
    /// <summary>The observer itself identifies the target: within its detection radius, with clear sight.</summary>
    private bool Identifies(CombatUnit observer, CombatUnit target)
    {
        var radius = CombatRules.DetectionRadius(observer, target);
        return observer.Position.DistanceSquaredTo(target.Position) <= radius * radius && Sight(observer, target);
    }

    /// <summary>The skills whose gain checks succeeded for a friendly MechWarrior who survived the battle: none for an enemy or a
    /// pilot killed. The draw depends only on the battle's seed, the unit and what it did, so a co-op replica rolls the same.</summary>
    public WarriorSkill[] SkillGains(CombatUnit unit)
    {
        if (unit.Side != CombatSide.Friendly || unit.WarriorKilled) return [];
        var random = new Random(unchecked(_seed * 37 + Units.ToList().IndexOf(unit) + SkillSeedSalt));
        var defeated = unit.Use.Identified.Count(id => Units.Any(item => item.Id == id && item.Defeated));
        // Extracting counts as seeing the battle out; ejecting or being disabled does not.
        var stayedIn = unit.Fighting || unit.Extracted;
        return SkillGrowth.Roll(random, unit.Warrior, SkillGrowth.Scores(unit.Use, unit.Hits), SkillGrowth.ExtraChecks(unit.Use, unit.Kills, defeated, stayedIn));
    }
}
