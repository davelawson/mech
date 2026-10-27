using Godot;

namespace Mech.Tests;

internal static class TerrainChecks
{
    private static ForceRoster Roster(int count = 1)
    {
        var roster = ForceRoster.Create("Terrain checks", Enumerable.Repeat(DraftCatalog.Mech("jenner"), count), DraftCatalog.Warriors.Take(count));
        for (var i = 0; i < count; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        return roster;
    }
    private static CombatSimulation Battle(BattlefieldTerrain terrain, int count = 1)
    {
        var roster = Roster(count);
        var simulation = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), terrain);
        simulation.EnemyAiEnabled = false;
        foreach (var unit in simulation.Units) unit.Firing = FiringMode.HoldFire;
        return simulation;
    }
    public static void Run(Action<bool, string> check)
    {
        var first = BattlefieldTerrain.Generate(183); var repeat = BattlefieldTerrain.Generate(183); var other = BattlefieldTerrain.Generate(184);
        check(first.Patches.SequenceEqual(repeat.Patches) && first.Hills.SequenceEqual(repeat.Hills), "terrain seed recreates all geometry");
        check(!first.Patches.SequenceEqual(other.Patches), "different terrain seeds create different maps");
        check(Enum.GetValues<TerrainKind>().All(kind => first.Patches.Any(patch => patch.Kind == kind)) && first.Hills.Count > 0, "generated maps include rocks, vegetation, water and hills");
        check(first.Patches.Any(patch => !first.Patches.Any(mirror => mirror.Kind == patch.Kind && mirror.Center == new Vector2(patch.Center.X, -patch.Center.Y))), "terrain generation allows asymmetric layouts");
        foreach (var terrain in new[] { first, other, BattlefieldTerrain.Generate(int.MinValue), BattlefieldTerrain.Generate(int.MaxValue) })
        {
            foreach (var x in new[] { -6f, -4, -2, 0, 2, 4, 6 }) foreach (var z in new[] { -32f, 32 })
            {
                var spawn = new Vector2(x, z);
                check(terrain.Passable(spawn) && terrain.HeightAt(spawn) == 0 && !Enum.GetValues<TerrainKind>().Any(kind => terrain.Contains(kind, spawn)), "every force size has clear spawn positions");
                var route = terrain.FindPath(spawn, new Vector2(x, -z)); var previous = spawn;
                check(route.Count > 0 && route[^1].DistanceTo(new Vector2(x, -z)) < .01f, "opposing spawn is reachable");
                foreach (var waypoint in route) { check(terrain.CanTraverse(previous, waypoint), "generated route respects slopes and mech clearance"); previous = waypoint; }
            }
        }
        var roster = Roster();
        var deployed = CombatSimulation.Deploy(roster, roster.Mechs.Select(unit => unit.Id), 42, 183);
        check(deployed.Terrain.Patches.SequenceEqual(first.Patches), "deployment uses supplied terrain seed independently of combat seed");
        var rocks = new BattlefieldTerrain(1, [new(TerrainKind.Rock, Vector2.Zero, 2, 4)], []);
        check(!rocks.Passable(new Vector2(2.5f, 0)) && rocks.Passable(new Vector2(3, 0)), "rocks reserve space for mech radius");
        check(!rocks.ClearSight(new Vector2(-8, 0), new Vector2(8, 0)) && rocks.ClearSight(new Vector2(-8, 4), new Vector2(8, 4)), "rocks block intersecting sight and firing rays");
        var path = rocks.FindPath(new Vector2(-8, 0), new Vector2(8, 0)); var origin = new Vector2(-8, 0);
        check(path.Count > 1, "navigation finds a detour around solid terrain");
        foreach (var waypoint in path) { check(rocks.CanTraverse(origin, waypoint), "detour segments cannot clip rock clearance"); origin = waypoint; }
        var corrected = rocks.ReachableDestination(new Vector2(-8, 0), Vector2.Zero);
        check(rocks.Passable(corrected) && corrected.DistanceTo(Vector2.Zero) < 4, "orders inside rocks resolve to nearby reachable ground");
        CheckMovement(rocks, check);
        CheckSightAndFire(rocks, check);
        var hill = new BattlefieldTerrain(2, [], [new(Vector2.Zero, 8, 6)]);
        check(hill.HeightAt(Vector2.Zero) == 6 && !hill.ClearSight(new Vector2(-10, 0), new Vector2(10, 0)), "hills have actual elevation and block low sight rays");
        check(hill.ClearSight(new Vector2(-2, 0), new Vector2(2, 0)), "elevated viewpoints can see over the hill crest");
        check(!hill.Passable(new Vector2(4, 0)) && hill.Passable(new Vector2(12, 0)), "steep hill slopes are impassable");
        var isolated = hill.ReachableDestination(new Vector2(-12, 0), Vector2.Zero);
        check(isolated != Vector2.Zero && hill.FindPath(new Vector2(-12, 0), isolated).Count > 0, "isolated hilltops resolve to the nearest reachable component");
        var overRock = new BattlefieldTerrain(3, [new(TerrainKind.Rock, Vector2.Zero, 1, 1)],
            [new(new Vector2(-8, 0), 4, 5), new(new Vector2(8, 0), 4, 5)]);
        check(overRock.ClearSight(new Vector2(-8, 0), new Vector2(8, 0)), "height-aware sight can pass above low rocks");
        var vegetation = new BattlefieldTerrain(4, [new(TerrainKind.Vegetation, Vector2.Zero, 3), new(TerrainKind.Vegetation, new Vector2(5, 0), 2), new(TerrainKind.Vegetation, new Vector2(9, 0), 2)], []);
        check(vegetation.ClearSight(new Vector2(-8, 0), new Vector2(12, 0)) && vegetation.VegetationModifier(new Vector2(-8, 0), Vector2.Zero) == 1
            && vegetation.VegetationModifier(new Vector2(-8, 0), new Vector2(12, 0)) == 2, "vegetation protects targets and crossing shots with a capped penalty without hiding contacts");
        var coverBattle = Battle(vegetation); var f = coverBattle.Units[0]; var e = coverBattle.Units[1];
        f.Position = new Vector2(-8, 0); e.Position = Vector2.Zero;
        check(coverBattle.TerrainHitChance(f, e, f.Weapons[0]) < CombatSimulation.HitChance(f, e, f.Weapons[0]), "cover penalty changes the same hit probability used by combat");
        var water = new BattlefieldTerrain(5, [new(TerrainKind.Water, Vector2.Zero, 20)], []);
        check(water.Passable(Vector2.Zero) && water.SpeedFactor(Vector2.Zero) == .5f && vegetation.SpeedFactor(Vector2.Zero) == .7f, "water and vegetation are traversable with distinct movement penalties");
        var dry = Travel(BattlefieldTerrain.Flat); var wet = Travel(water);
        check(wet > 0 && wet < dry * .7f, "water slowdown affects actual movement rather than only navigation cost");
        GD.Print($"Terrain checks: movement on dry ground {dry:0.00}, water {wet:0.00}");
        CheckEngagement(first, new[] { "jenner" }, check);
        CheckEngagement(other, new[] { "awesome" }, check);
        CheckEngagement(first, new[] { "firestarter", "griffin", "catapult", "awesome" }, check);
    }
    private static void CheckEngagement(BattlefieldTerrain terrain, string[] definitions, Action<bool, string> check)
    {
        var roster = ForceRoster.Create("Terrain engagement", definitions.Select(DraftCatalog.Mech), DraftCatalog.Warriors.Take(definitions.Length));
        for (var i = 0; i < definitions.Length; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), terrain, 183);
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        sim.Move(friends.Select(unit => unit.Id), Vector2.Zero); sim.Paused = false;
        var passable = true;
        for (var second = 0; second < 1200 && sim.Outcome == CombatOutcome.Ongoing; second++)
        {
            foreach (var unit in friends.Where(unit => unit.Active))
            {
                var target = sim.Units.Where(target => target.Side != unit.Side && target.Active && sim.Detected(unit.Side, target.Id))
                    .OrderBy(target => unit.Position.DistanceSquaredTo(target.Position)).FirstOrDefault();
                if (target is not null) sim.Attack([unit.Id], target.Id);
            }
            sim.Advance(1); passable &= sim.Units.All(unit => terrain.Passable(unit.Position));
        }
        GD.Print($"Terrain engagement: seed {terrain.Seed}, {definitions.Length} per side, {sim.Elapsed:0}s, {sim.Outcome}");
        check(passable && sim.Outcome != CombatOutcome.Ongoing, "generated-terrain engagement completes without entering impassable terrain: "
            + string.Join("; ", sim.Units.Select(unit => $"{unit.Id} {unit.Position} {unit.Order} shots={unit.ShotsFired}")));
    }
    private static float Travel(BattlefieldTerrain terrain)
    {
        var sim = Battle(terrain); var unit = sim.Units[0]; unit.Position = new Vector2(-10, 0); unit.Facing = Vector2.Right;
        sim.Move([unit.Id], new Vector2(10, 0)); sim.Paused = false; sim.Advance(8);
        return unit.Position.X + 10;
    }
    private static void CheckMovement(BattlefieldTerrain terrain, Action<bool, string> check)
    {
        var sim = Battle(terrain); var unit = sim.Units[0]; unit.Position = new Vector2(-8, 0); unit.Facing = Vector2.Right;
        sim.Move([unit.Id], new Vector2(8, 0)); sim.Paused = false;
        var clear = true;
        for (var step = 0; step < 1600 && unit.Order != CombatOrder.Hold; step++) { sim.Advance(.05); clear &= terrain.Passable(unit.Position); }
        check(clear && unit.Position.DistanceTo(new Vector2(8, 0)) < .3f && unit.Order == CombatOrder.Hold, $"actual mech movement follows a terrain detour and arrives: {unit.Position}");
    }
    private static void CheckSightAndFire(BattlefieldTerrain terrain, Action<bool, string> check)
    {
        var sim = Battle(terrain, 2); var friend = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        var scout = sim.Units.Last(unit => unit.Side == CombatSide.Friendly); var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        friend.Position = new Vector2(-8, 0); friend.Facing = Vector2.Right; enemy.Position = new Vector2(5, 0);
        scout.Position = new Vector2(-1, 6); sim.RefreshDetection();
        check(sim.Detected(friend.Side, enemy.Id) && sim.SelectTarget(friend) is null && sim.TerrainHitChance(friend, enemy, friend.Weapons[0]) == 0,
            "shared contact does not permit a shot through a rock");
        sim.Attack([friend.Id], enemy.Id); sim.Paused = false; var clear = true;
        for (var step = 0; step < 1200 && friend.ShotsFired == 0; step++) { sim.Advance(.05); clear &= terrain.Passable(friend.Position); }
        check(clear && friend.ShotsFired > 0 && terrain.ClearSight(friend.Position, enemy.Position), "blocked attack orders navigate to a clear firing position");
        sim.Hold([friend.Id]); friend.Position = new Vector2(-8, 0); scout.Position = new Vector2(-30, 20); sim.RefreshDetection();
        check(!sim.Detected(friend.Side, enemy.Id) && sim.Contacts(friend.Side)[enemy.Id].LastKnownPosition == enemy.Position, "terrain occlusion loses detection and retains stationary last-known contact");
    }
}
