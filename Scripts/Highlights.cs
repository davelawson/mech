namespace Mech;

public sealed partial class CombatUnit
{
    /// <summary>The current shutdown is already in the highlights; cleared once the mech restarts.</summary>
    internal bool ShutdownHighlighted { get; set; }
    /// <summary>The pilot's first panic is a highlight; a pilot who calms and panics again is not noted twice.</summary>
    internal bool PanicHighlighted { get; set; }
}

/// <summary>The battle's highlights: one sentence per significant moment, in order, naming each mech with its MechWarrior. They are
/// written once the moment happens, with full details on both sides, and shown only after the battle.</summary>
public sealed partial class CombatSimulation
{
    private readonly List<CombatHighlight> _highlights = [];
    public IReadOnlyList<CombatHighlight> Highlights => _highlights;

    private void Highlight(CombatUnit unit, string text) => _highlights.Add(new(Elapsed, unit.Id, text));

    private static string Name(CombatUnit unit) => BattleSummary.Name(unit);

    private void HighlightShutdowns()
    {
        foreach (var unit in Units)
        {
            if (!unit.Shutdown) { unit.ShutdownHighlighted = false; continue; }
            if (unit.ShutdownHighlighted || unit.Defeated) continue;
            unit.ShutdownHighlighted = true;
            Highlight(unit, $"{Name(unit)} shut down from overheating.");
        }
    }

    /// <summary>What one resolved hit did: explosions, lost locations, a fall, destruction, the MechWarrior's death or ejection, and
    /// disabling. The arguments before the hit are its status, ejection, the pilot's survival and whether head and center torso stood.</summary>
    private void HighlightShot(CombatShot shot, CombatUnit target, IReadOnlyList<CombatCritical> criticals, IReadOnlySet<MechLocation> destroyed,
        CombatPilotingCheck? piloting, CombatStatus status, bool ejected, bool killed, bool whole)
    {
        var attacker = Unit(shot.AttackerId);
        var source = $"{Name(attacker)}'s {shot.Weapon}";
        string Bin(MechLocation location, string id) =>
            $"{target.Spec.Ammo.Single(item => item.Id == id).Name} in {BattleSummary.Place(location)}";
        foreach (var explosion in criticals.Where(item => item.Explosion > 0))
            Highlight(target, $"{Name(target)}'s {Bin(explosion.Location, explosion.ItemId)} exploded for {explosion.Explosion} damage "
                + $"after a critical hit from {source}.");
        var lost = destroyed.Where(location => location is not (MechLocation.Head or MechLocation.CenterTorso)).Order().ToArray();
        if (lost.Length > 0)
            Highlight(target, $"{Name(target)} lost its {BattleSummary.List(lost.Select(item => DraftCatalog.LocationName(item).ToLowerInvariant()))} to {source}.");
        if (piloting is { Fell: true })
            Highlight(target, $"{Name(target)} was knocked down by {source}{(target.Airborne ? " in mid-jump" : "")}.");
        var nowWhole = target.Intact(MechLocation.Head) && target.Intact(MechLocation.CenterTorso);
        if (whole && !nowWhole)
        {
            if (_fatalExplosion is { } fatal)
                Highlight(target, $"{Name(target)} was destroyed when its {Bin(fatal.Location, fatal.BinId)} exploded after a hit from {source}.");
            else
            {
                var struck = shot.Missiles.Count > 0 ? shot.Missiles.Where(item => item.Hit).Select(item => item.Location).Distinct().Order().ToArray()
                    : [shot.Location];
                Highlight(target, $"{Name(target)} was destroyed by {Name(attacker)}: {shot.Weapon} to {BattleSummary.List(struck.Select(BattleSummary.Place))}.");
            }
        }
        if (!ejected && target.Ejected) Highlight(target, $"{target.Warrior.Callsign} ejected as the {target.Spec.DisplayName}'s center torso was destroyed.");
        if (!killed && target.WarriorKilled)
            Highlight(target, $"{target.Warrior.Callsign} was killed when {source} "
                + $"{(target.Intact(MechLocation.Head) ? "struck the cockpit" : "destroyed the head")} of the {target.Spec.DisplayName}.");
        else if (!CombatUnit.IsDefeated(status) && target.Status == CombatStatus.Disabled && nowWhole && !target.Ejected)
            Highlight(target, !target.Intact(MechLocation.LeftLeg) && !target.Intact(MechLocation.RightLeg)
                ? $"{Name(target)} was disabled when {source} took its last leg." : $"{Name(target)} was disabled by {source}.");
    }
}
