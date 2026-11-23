using Godot;

namespace Mech.Tests;

/// <summary>The drop phase: zones, choosing and confirming drop sites, Force Start and how lances land.</summary>
internal static partial class CombatChecks
{
    private static CombatSimulation DropBattle(int lances = 2, int terrainSeed = 183, int seed = 21)
    {
        var count = lances * 2;
        var roster = ForceRoster.Create("Drop checks", Enumerable.Repeat(DraftCatalog.Mech("jenner"), count), DraftCatalog.Warriors.Take(count), lances: lances);
        for (var i = 0; i < count; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        foreach (var (mech, i) in roster.Mechs.Select((mech, i) => (mech, i))) roster.SetLance(mech.Id, i / 2);
        return CombatSimulation.Deploy(roster, roster.Mechs.Select(mech => mech.Id), seed, terrainSeed, dropPhase: true);
    }
    private static IEnumerable<CombatUnit> Lance(CombatSimulation sim, int slot) => sim.Units.Where(unit => unit.Side == CombatSide.Friendly && unit.Lance == slot);
    /// <summary>Every mech of a lance stands inside its site, apart from its lancemates, on passable ground, facing into the arena, holding.</summary>
    private static bool LandedOn(CombatSimulation sim, int slot, Vector2 site, Vector2 inward)
    {
        var lance = Lance(sim, slot).ToArray();
        return lance.All(unit => !sim.AwaitingDrop(unit) && unit.Position.DistanceTo(site) <= CombatRules.DropSiteRadius + 1e-3f
            && sim.Terrain.Passable(unit.Position) && unit.Facing.IsEqualApprox(inward) && unit.Destination == unit.Position)
            && lance.All(unit => lance.All(other => other == unit || other.Position.DistanceTo(unit.Position) >= CombatRules.DropMechSpacing - 1e-3f));
    }
    /// <summary>Chooses a site for every lance in turn, then readies them all.</summary>
    private static Vector2[] DropAll(CombatSimulation sim)
    {
        var sites = new List<Vector2>(); var along = -600f;
        foreach (var slot in sim.Players)
        {
            var site = ValidSite(sim, slot, along); sim.Execute(new DropSiteCommand(site), slot);
            sites.Add(site); along = site.Dot(sim.FriendlyZone!.Along) + 250;
        }
        foreach (var slot in sim.Players) sim.Execute(new DropReadyCommand(true), slot);
        return [.. sites];
    }
    /// <summary>A valid site for a lance, found by walking along the zone's middle.</summary>
    private static Vector2 ValidSite(CombatSimulation sim, int slot, float from)
    {
        var zone = sim.FriendlyZone!;
        for (var along = from; along < CombatRules.ArenaHalfSize; along += 30)
        {
            var point = PatrolLayout.EdgePoint(zone.Edge, along, zone.Depth(zone.Anchor));
            if (sim.DropSiteProblem(slot, point) is null) return point;
        }
        throw new InvalidOperationException("No valid drop site.");
    }

    private static void CheckDropPhase(Action<bool, string> check)
    {
        var zone = new DropZone(ArenaEdge.South, Vector2.Zero);
        const float half = CombatRules.ArenaHalfSize;
        check(!zone.Contains(new(0, half - 150)) && zone.Contains(new(0, half - 200)) && zone.Contains(new(0, half - 1152))
            && !zone.Contains(new(0, half - 1200)) && !zone.Contains(new(half - 150, half - 600)) && zone.Contains(new(half - 200, half - 600))
            && Math.Abs(CombatRules.DropZoneDepth - 1152) < 1e-3f,
            "a drop zone runs the whole edge less 200 m of padding, and reaches 1152 m (a fifth of the arena) in from it");
        check(DropZone.Opposite(ArenaEdge.East) == ArenaEdge.West && DropZone.Opposite(ArenaEdge.North) == ArenaEdge.South
            && Enumerable.Range(0, 40).Select(DropZone.StandardEdge).Distinct().Count() == 4, "standard battles drop on every edge, the enemy on the opposite one");

        var sim = DropBattle();
        var drop = sim.Drop!;
        check(drop.Zone == sim.FriendlyZone && drop.EnemyZone == sim.EnemyZone && sim.Paused && sim.Units.Where(unit => unit.Side == CombatSide.Friendly).All(sim.AwaitingDrop)
            && sim.Contacts(CombatSide.Friendly).Count == 0 && drop.Sites.All(site => site.Site is null && !site.Ready),
            "a battle opens in its drop phase, paused, with no lance landed and nothing seen");
        sim.Paused = false; sim.Advance(1);
        check(sim.Paused && sim.Elapsed == 0, "the battle cannot run during the drop phase");
        sim.Paused = true;

        var first = ValidSite(sim, 0, -600);
        var before = Lance(sim, 0).Select(unit => unit.Position).ToArray();
        sim.Execute(new DropSiteCommand(first), 0);
        check(drop.Site(0).Site == first && Lance(sim, 0).All(sim.AwaitingDrop) && Lance(sim, 0).Select(unit => unit.Position).SequenceEqual(before),
            "choosing a drop site records it without landing the lance, so where its mechs land stays unknown");

        sim.Execute(new DropSiteCommand(first + drop.Zone.Along * 150), 1);
        check(drop.Site(1).Site is null && drop.Site(1).Bounced == 1 && sim.DropSiteProblem(1, first + drop.Zone.Along * 150) == "Overlaps another lance's drop site",
            "a drop site overlapping another lance's is turned away");
        sim.Execute(new DropSiteCommand(PatrolLayout.EdgePoint(drop.Zone.Edge, 0, 100)), 1);
        check(drop.Site(1).Site is null && drop.Site(1).Bounced == 2, "a drop site inside the edge padding is turned away");
        sim.Execute(new DropReadyCommand(true), 1);
        check(!drop.Site(1).Ready, "a player cannot be ready without a drop site");

        sim.Execute(new DropReadyCommand(true), 0);
        sim.Execute(new DropSiteCommand(first + drop.Zone.Along * 400), 0);
        check(drop.Site(0).Ready && drop.Site(0).Site == first && sim.Drop is not null, "a ready player's site is locked until they reopen it");
        sim.Execute(new DropReadyCommand(false), 0); var moved = ValidSite(sim, 0, first.Dot(drop.Zone.Along) + 300);
        sim.Execute(new DropSiteCommand(moved), 0);
        check(drop.Site(0).Site == moved && Lance(sim, 0).All(sim.AwaitingDrop), "reopening a site lets the player move it");
        sim.Execute(new DropReadyCommand(true), 0);
        var second = ValidSite(sim, 1, moved.Dot(drop.Zone.Along) + 250);
        sim.Execute(new DropSiteCommand(second), 1); sim.Execute(new DropReadyCommand(true), 1);
        check(sim.Drop is null && sim.Paused && sim.PauseHolds.SetEquals(sim.Players) && sim.Units.All(unit => !sim.AwaitingDrop(unit)),
            "once every player is ready the drop phase ends and the battle begins, paused");
        check(LandedOn(sim, 0, moved, drop.Zone.Inward) && LandedOn(sim, 1, second, drop.Zone.Inward),
            "as the drop phase ends every lance lands across its chosen site, apart, facing into the arena");
        var one = DropBattle(seed: 31); var oneSites = DropAll(one);
        var other = DropBattle(seed: 32); var otherSites = DropAll(other);
        check(oneSites.SequenceEqual(otherSites) && !Lance(one, 0).Select(unit => unit.Position).SequenceEqual(Lance(other, 0).Select(unit => unit.Position)),
            "the same drop site lands a lance differently in another battle");
        sim.Paused = false; sim.Advance(1);
        check(sim.Elapsed > 0, "the battle runs once the drop phase is over");

        var forced = DropBattle();
        var site = ValidSite(forced, 0, 0); forced.Execute(new DropSiteCommand(site), 0);
        forced.Execute(new ForceDropCommand(), 1);
        check(forced.Drop is not null, "only the leader can force the battle to start");
        var waiting = forced.Drop!;
        forced.Execute(new ForceDropCommand(), 0);
        check(forced.Drop is null && waiting.Site(0).Site == site && waiting.Site(1).Site is { } random && forced.FriendlyZone!.Contains(random)
            && random.DistanceTo(site) >= CombatRules.DropSiteRadius * 2 && LandedOn(forced, 0, site, waiting.Zone.Inward) && LandedOn(forced, 1, random, waiting.Zone.Inward),
            "Force Start keeps chosen sites and drops every other lance at a random valid site in the zone");

        var enemies = forced.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        check(enemies.All(unit => forced.EnemyZone!.Depth(unit.Position) <= CombatRules.DropZoneDepth + CombatRules.DropSiteRadius
            && forced.Terrain.Passable(unit.Position) && unit.Facing.IsEqualApprox(forced.EnemyZone.Inward)), "enemy lances drop in the enemy zone, facing into the arena");

        var solo = DropBattle(1, 4242);
        var only = ValidSite(solo, 0, 0); solo.Execute(new DropSiteCommand(only), 0); solo.Execute(new DropReadyCommand(true), 0);
        check(solo.Drop is null && solo.Paused && LandedOn(solo, 0, only, solo.FriendlyZone!.Inward), "a single player's drop phase ends when they are ready");
    }
}
