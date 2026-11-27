using Godot;

namespace Mech.Tests;

internal static class TerrainChecks
{
    private static ForceRoster Roster(int count = 1)
    {
        var roster = ForceRoster.Create("Terrain checks", Enumerable.Repeat(DraftCatalog.Mech("jenner"), count), TestPilots.Warriors.Take(count));
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
    /// <summary>Every part in turn. The unit tests run the parts separately, in parallel.</summary>
    public static void Run(Action<bool, string> check)
    {
        RunGeneration(check); RunSeed7(check); RunSeedMinus41(check); RunFeatures(check); RunEngagements(check); RunLanceEngagement(check);
    }
    /// <summary>Seed 183 in every biome.</summary>
    private static Dictionary<Biome, BattlefieldTerrain> Standard() => Biomes.All.ToDictionary(spec => spec.Biome, spec => BattlefieldTerrain.Generate(183, spec.Biome));
    /// <summary>Generated maps: seeds, biomes, buildings, spawns and routes, and deployment onto them.</summary>
    public static void RunGeneration(Action<bool, string> check)
    {
        var standard = Standard();
        var first = standard[Biome.Valley]; var repeat = BattlefieldTerrain.Generate(183); var other = BattlefieldTerrain.Generate(184);
        check(first.Patches.SequenceEqual(repeat.Patches) && first.Buildings.SequenceEqual(repeat.Buildings) && SameRelief(first, repeat)
            && SameLiquids(first, repeat), "terrain seed recreates all geometry, lakes and rivers");
        CheckNavigationQueries(first, check);
        foreach (var (terrain, dropEdge) in standard.Values.Select(terrain => (terrain, ArenaEdge.South))
            .Append((BattlefieldTerrain.Generate(183, dropEdge: ArenaEdge.East), ArenaEdge.East)))
        {
            var zones = new[] { DropZone.Standard(dropEdge), DropZone.Standard(DropZone.Opposite(dropEdge)) };
            check(Enumerable.Range(0, terrain.Liquids.Count).Where(body => terrain.Liquids[body].Shape == LiquidShape.Lake)
                    .All(body => terrain.LiquidNodes(body).All(point => zones.All(zone => zone.Depth(point) >= CombatRules.DropZoneDepth)))
                && terrain.Component(zones[0].Anchor) != 0 && terrain.Component(zones[0].Anchor) == terrain.Component(zones[1].Anchor),
                $"lakes, ice and lava keep out of both drop zones, whose anchors connect ({terrain.Biome}, {dropEdge})");
        }
        check(!first.Patches.SequenceEqual(other.Patches), "different terrain seeds create different maps");
        check(first.Biome == Biome.Valley && first.Patches.Count(patch => patch.Kind == TerrainKind.Rock) > 30
            && first.Patches.Count(patch => patch.Kind == TerrainKind.Vegetation) > 36
            && first.Liquids.Count(body => body.Shape == LiquidShape.Lake) >= 12 && first.Liquids.Count(body => body.Shape == LiquidShape.River) is 1 or 2
            && first.Liquids.All(body => body.Kind == TerrainKind.Water),
            "valley maps carry rocks, forests, lakes and rivers at the agreed density");
        CheckBiomes(standard, check, 183);
        CheckBuildings(check);
        check(first.Patches.Any(patch => Math.Abs(patch.Center.X) > 1440) && first.Patches.Any(patch => Math.Abs(patch.Center.Y) > 1440),
            "terrain features populate the expanded battlefield beyond the old boundaries");
        check(first.Patches.Any(patch => !first.Patches.Any(mirror => mirror.Kind == patch.Kind && mirror.Center == new Vector2(patch.Center.X, -patch.Center.Y))), "terrain generation allows asymmetric layouts");
        foreach (var terrain in new[] { first, other, BattlefieldTerrain.Generate(int.MinValue), BattlefieldTerrain.Generate(int.MaxValue) }
            .Concat(standard.Values.Where(terrain => terrain != first)))
        {
            foreach (var x in new[] { -360f, -240, -120, 0, 120, 240, 360 }) foreach (var z in new[] { -1920f, 1920 })
            {
                var spawn = new Vector2(x, z);
                // A shallow or frozen river may run through the spawn band, so mechs may start wading in its trench. The 15 m grid
                // reaches a trench's full depth, up to 20 m where a river carved a gorge upstream.
                check(terrain.Passable(spawn) && Math.Abs(terrain.HeightAt(spawn)) <= 20
                    && new[] { TerrainKind.Rock, TerrainKind.Vegetation, TerrainKind.DeepWater, TerrainKind.Lava }.All(kind => !terrain.Contains(kind, spawn))
                    && (terrain.HeightAt(spawn) == 0 || terrain.Liquids.Any(body => body.Shape == LiquidShape.River)),
                    $"every force size has clear spawn positions ({terrain.Biome} {terrain.Seed} {spawn})");
                var route = terrain.FindPath(spawn, new Vector2(x, -z)); var previous = spawn;
                check(route.Count > 0 && route[^1].DistanceTo(new Vector2(x, -z)) < .3f, "opposing spawn is reachable");
                foreach (var waypoint in route) { check(terrain.CanTraverse(previous, waypoint), "generated route respects slopes and mech clearance"); previous = waypoint; }
            }
        }
        var roster = Roster();
        var deployed = CombatSimulation.Deploy(roster, roster.Mechs.Select(unit => unit.Id), 42, 183);
        var edge = DropZone.StandardEdge(183);
        check(deployed.Terrain.Patches.SequenceEqual(BattlefieldTerrain.Generate(183, dropEdge: edge).Patches)
            && deployed.FriendlyZone!.Edge == edge && deployed.EnemyZone!.Edge == DropZone.Opposite(edge),
            "deployment uses supplied terrain seed independently of combat seed, and draws its drop edge from it");
        var lunar = CombatSimulation.Deploy(roster, roster.Mechs.Select(unit => unit.Id), 42, 183, biome: Biome.Lunar);
        check(lunar.Terrain.Biome == Biome.Lunar && lunar.Terrain.Patches.SequenceEqual(BattlefieldTerrain.Generate(183, Biome.Lunar, dropEdge: edge).Patches)
            && lunar.Units.All(unit => unit.Climate == Biomes.Spec(Biome.Lunar)), "deployment generates the chosen biome and gives every mech its climate");
    }
    /// <summary>Hand-built rocks, hills, vegetation, water and lava: clearance, sight, navigation and movement.</summary>
    public static void RunFeatures(Action<bool, string> check)
    {
        var rocks = new BattlefieldTerrain(1, [new(TerrainKind.Rock, Vector2.Zero, 60, 120)], []);
        // Rocks and buildings reserve their footprint plus the mech radius and 1.5 metres.
        const float clearance = CombatRules.UnitRadius + 1.5f;
        check(!rocks.Passable(new Vector2(60 + clearance - .5f, 0)) && rocks.Passable(new Vector2(60 + clearance + .5f, 0)), "rocks reserve space for mech radius");
        check(!rocks.ClearSight(new Vector2(-240, 0), new Vector2(240, 0)) && rocks.ClearSight(new Vector2(-240, 120), new Vector2(240, 120)), "rocks block intersecting sight and firing rays");
        var path = rocks.FindPath(new Vector2(-240, 0), new Vector2(240, 0)); var origin = new Vector2(-240, 0);
        check(path.Count > 1, "navigation finds a detour around solid terrain");
        foreach (var waypoint in path) { check(rocks.CanTraverse(origin, waypoint), "detour segments cannot clip rock clearance"); origin = waypoint; }
        var corrected = rocks.ReachableDestination(new Vector2(-240, 0), Vector2.Zero);
        check(rocks.Passable(corrected) && corrected.DistanceTo(Vector2.Zero) < 120, "orders inside rocks resolve to nearby reachable ground");
        CheckMovement(rocks, check);
        CheckSightAndFire(rocks, check);
        var hill = new BattlefieldTerrain(2, [], [new(Vector2.Zero, 240, 180)]);
        check(hill.HeightAt(Vector2.Zero) == 180 && !hill.ClearSight(new Vector2(-300, 0), new Vector2(300, 0)), "hills have actual elevation and block low sight rays");
        check(hill.ClearSight(new Vector2(-10, 0), new Vector2(10, 0)), "elevated viewpoints can see over the hill crest");
        check(!hill.Passable(new Vector2(120, 0)) && hill.Passable(new Vector2(360, 0)), "steep hill slopes are impassable");
        var outerHill = new BattlefieldTerrain(6, [], [new(new Vector2(2160, 2100), 240, 180)]);
        check(outerHill.HeightAt(new(2160, 2100)) == 180 && !outerHill.Passable(new(2280, 2100)), "expanded heightfield samples real elevation and slope beyond the old boundaries");
        check(outerHill.RayDistance(new(2160, 360, 2100), Vector3.Down, 600) is { } ray && Math.Abs(ray - 180) < .03f,
            "ground picking reaches terrain in the expanded battlefield");
        check(outerHill.FindPath(new(1800, 2100), new(2520, 2100)).Count > 1, "navigation detours around terrain beyond the old boundaries");
        var isolated = hill.ReachableDestination(new Vector2(-360, 0), Vector2.Zero);
        check(isolated != Vector2.Zero && hill.FindPath(new Vector2(-360, 0), isolated).Count > 0, "isolated hilltops resolve to the nearest reachable component");
        var overRock = new BattlefieldTerrain(3, [new(TerrainKind.Rock, Vector2.Zero, 30, 30)],
            [new(new Vector2(-240, 0), 120, 150), new(new Vector2(240, 0), 120, 150)]);
        check(overRock.ClearSight(new Vector2(-240, 0), new Vector2(240, 0)), "height-aware sight can pass above low rocks");
        var vegetation = new BattlefieldTerrain(4, [new(TerrainKind.Vegetation, Vector2.Zero, 90), new(TerrainKind.Vegetation, new Vector2(150, 0), 60), new(TerrainKind.Vegetation, new Vector2(270, 0), 60)], []);
        check(vegetation.ClearSight(new Vector2(-240, 0), new Vector2(360, 0)) && vegetation.VegetationModifier(new Vector2(-240, 0), Vector2.Zero) == 1
            && vegetation.VegetationModifier(new Vector2(-240, 0), new Vector2(360, 0)) == 2, "vegetation protects targets and crossing shots with a capped penalty without hiding contacts");
        var coverBattle = Battle(vegetation); var f = coverBattle.Units[0]; var e = coverBattle.Units[1];
        f.Position = new Vector2(-240, 0); f.Facing = Vector2.Right; e.Position = Vector2.Zero;
        check(coverBattle.TerrainHitChance(f, e, f.Weapons[0]) < CombatSimulation.HitChance(f, e, f.Weapons[0]), "cover penalty changes the same hit probability used by combat");
        var water = new BattlefieldTerrain(5, [new(TerrainKind.Water, Vector2.Zero, 600)], []);
        check(water.Passable(Vector2.Zero) && water.SpeedFactor(Vector2.Zero) == .5f && vegetation.SpeedFactor(Vector2.Zero) == .7f, "water and vegetation are traversable with distinct movement penalties");
        var lava = new BattlefieldTerrain(7, [new(TerrainKind.Lava, Vector2.Zero, 120)], []);
        check(!lava.Passable(Vector2.Zero) && lava.ClearSight(new Vector2(-240, 0), new Vector2(240, 0)), "lava bars movement but not sight or fire");
        var around = lava.FindPath(new Vector2(-240, 0), new Vector2(240, 0)); var start = new Vector2(-240, 0);
        check(around.Count > 1 && around.All(waypoint => { var clear = lava.CanTraverse(start, waypoint); start = waypoint; return clear; }),
            "navigation detours around lava");
        CheckBridge(check);
        var dry = Travel(BattlefieldTerrain.Flat); var wet = Travel(water);
        check(wet > 0 && wet < dry * .7f, "water slowdown affects actual movement rather than only navigation cost");
        Console.WriteLine($"Terrain checks: movement on dry ground {dry:0.00}, water {wet:0.00}");
    }
    /// <summary>One-on-one battles fought to a result on generated maps.</summary>
    public static void RunEngagements(Action<bool, string> check)
    {
        var standard = Standard();
        CheckEngagement(standard[Biome.Valley], new[] { "jenner" }, check);
        CheckEngagement(BattlefieldTerrain.Generate(184), new[] { "awesome" }, check);
        CheckEngagement(standard[Biome.Arctic], new[] { "griffin" }, check);
        CheckEngagement(standard[Biome.Lunar], new[] { "catapult" }, check);
        CheckEngagement(standard[Biome.Archipelago], new[] { "jenner" }, check);
        CheckEngagement(standard[Biome.Volcanic], new[] { "jenner" }, check);
    }
    /// <summary>A four-on-four battle on the seed-183 valley, the longest single simulation.</summary>
    public static void RunLanceEngagement(Action<bool, string> check) =>
        CheckEngagement(BattlefieldTerrain.Generate(183), new[] { "firestarter", "griffin", "catapult", "awesome" }, check, 184);
    private static bool SameRelief(BattlefieldTerrain a, BattlefieldTerrain b)
    {
        for (var z = -2880f; z <= 2880; z += 90) for (var x = -2880f; x <= 2880; x += 90)
            if (a.HeightAt(new(x, z)) != b.HeightAt(new(x, z))) return false;
        return true;
    }
    private static IEnumerable<Vector2> Grid()
    {
        for (var z = -2880f; z <= 2880; z += BattlefieldTerrain.CellSize) for (var x = -2880f; x <= 2880; x += BattlefieldTerrain.CellSize) yield return new(x, z);
    }
    private static bool SameLiquids(BattlefieldTerrain a, BattlefieldTerrain b) =>
        a.Liquids.Count == b.Liquids.Count && a.Liquids.Zip(b.Liquids).All(pair => pair.First.Shape == pair.Second.Shape && pair.First.Kind == pair.Second.Kind
            && pair.First.Fords == pair.Second.Fords && pair.First.Course.SequenceEqual(pair.Second.Course))
        && Grid().All(point => a.LiquidAt(point) == b.LiquidAt(point));
    /// <summary>Lakes level in hollows the ground holds; rivers fall downstream in their trenches; liquids slow, bar or carry mechs by kind.</summary>
    private static void CheckLiquids(BattlefieldTerrain terrain, Action<bool, string> check)
    {
        var name = $"{terrain.Biome} {terrain.Seed}";
        // Mechs already stand on the ice's top; other liquids lie their depth above where mechs stand.
        float Surface(Vector2 point) => terrain.HeightAt(point) + (terrain.LiquidAt(point) is { Kind: not TerrainKind.Ice, Depth: var depth } ? depth : 0);
        var lakes = Enumerable.Range(0, terrain.Liquids.Count).Where(body => terrain.Liquids[body].Shape == LiquidShape.Lake).ToArray();
        foreach (var lake in lakes)
        {
            var nodes = terrain.LiquidNodes(lake).ToArray();
            var levels = nodes.Select(Surface).ToArray();
            check(nodes.Length > 0 && nodes.All(point => terrain.LiquidAt(point) is not null) && levels.Max() - levels.Min() < .01f, $"{name} lake {lake} is level");
            var level = levels.Max();
            // Every neighbour of a lake is liquid too, or ground at least as high as the lake.
            check(nodes.All(point => Enumerable.Range(0, 8).Select(i => point + Vector2.FromAngle(i * Mathf.Tau / 8) * BattlefieldTerrain.CellSize * (i % 2 == 0 ? 1 : Mathf.Sqrt2))
                    .All(next => terrain.LiquidAt(next) is not null || terrain.HeightAt(next) >= level - .01f)),
                $"{name} lake {lake} lies in a hollow the ground holds");
        }
        // Lakes take the shape of their hollows rather than circles.
        check(lakes.Length == 0 || lakes.Any(lake =>
        {
            var nodes = terrain.LiquidNodes(lake).ToArray();
            var middle = nodes.Aggregate(Vector2.Zero, (sum, point) => sum + point) / nodes.Length;
            var reach = nodes.Max(point => point.DistanceTo(middle)) + BattlefieldTerrain.CellSize / 2;
            return nodes.Length >= 12 && nodes.Length * BattlefieldTerrain.CellSize * BattlefieldTerrain.CellSize < Mathf.Pi * reach * reach * .6f;
        }), $"{name} lakes follow the ground's contours, not circles");
        foreach (var river in terrain.Liquids.Where(body => body.Shape == LiquidShape.River))
        {
            var wet = river.Course.Where(point => terrain.LiquidAt(point) is not null).ToArray();
            check(river.Course.Count > 20 && wet.Length >= river.Course.Count * (river.Kind == TerrainKind.Lava ? .5f : .9f),
                $"{name} {river.Kind} river runs through its trench: {wet.Length} of {river.Course.Count} course points wet");
            check(wet.Zip(wet.Skip(1)).All(pair => Surface(pair.Second) <= Surface(pair.First) + .6f), $"{name} {river.Kind} river surface only falls downstream");
            var barrier = river.Kind == TerrainKind.Lava || terrain.Biome == Biome.Tropical;
            check(!barrier || river.Fords >= 2, $"{name} deep and lava rivers have at least two fords: {river.Fords}");
            if (river.Kind != TerrainKind.Lava)
                check(Math.Max(Math.Abs(river.Course[0].X), Math.Abs(river.Course[0].Y)) > 2800 && Math.Max(Math.Abs(river.Course[^1].X), Math.Abs(river.Course[^1].Y)) > 2800,
                    $"{name} rivers run between map edges");
        }
        var deep = terrain.Biome is Biome.Tropical or Biome.Archipelago;
        check(deep || !Grid().Any(point => terrain.LiquidAt(point)?.Kind == TerrainKind.DeepWater), $"{name} has deep water only in tropical and archipelago biomes");
        var samples = Grid().Where(point => terrain.LiquidAt(point) is { Depth: > 1 }).Take(400).ToArray();
        check(samples.All(point => terrain.LiquidAt(point)!.Value.Kind switch
        {
            TerrainKind.Water => terrain.Passable(point) ? Mathf.IsEqualApprox(terrain.SpeedFactor(point) * (1 + terrain.SlopeAt(point)), BattlefieldTerrain.WaterSpeed) : true,
            TerrainKind.Ice => !terrain.Passable(point) || Mathf.IsEqualApprox(terrain.SpeedFactor(point) * (1 + terrain.SlopeAt(point)), 1),
            _ => !terrain.Passable(point),
        }), $"{name} water slows, ice keeps full speed, and lava and deep water bar mechs");
        // Mechs stand on the ice's top, not the bed beneath it; away from its shores and bridges the ice is flat open ground.
        var ice = samples.Where(point => terrain.LiquidAt(point)!.Value.Kind == TerrainKind.Ice && !terrain.OnDeck(point)).ToArray();
        check(ice.All(point => Mathf.IsEqualApprox(terrain.HeightAt(point), terrain.GroundAt(point) + terrain.LiquidAt(point)!.Value.Depth)),
            $"{name} mechs stand on top of the ice");
        bool NearBridge(Vector2 point) => terrain.Bridges.Any(bridge =>
        {
            var along = bridge.To - bridge.From;
            var t = Math.Clamp((point - bridge.From).Dot(along) / Math.Max(along.LengthSquared(), 1e-6f), 0, 1);
            return (bridge.From + along * t).DistanceTo(point) < 40;
        });
        var open = ice.Where(point => !NearBridge(point) && Math.Max(Math.Abs(point.X), Math.Abs(point.Y)) < CombatRules.ArenaHalfSize - CombatRules.UnitRadius
            && Enumerable.Range(0, 8)
            .All(i => terrain.LiquidAt(point + Vector2.FromAngle(i * Mathf.Tau / 8) * 30) is { Kind: TerrainKind.Ice })).ToArray();
        check(terrain.Biome != Biome.Arctic || open.Length > 0, $"{name} has open ice to walk on");
        // Lake ice is level and river ice falls only at its river's gentle grade, so the bed's slopes never slow a mech on top.
        bool Open(Vector2 point) => terrain.Passable(point) && terrain.SlopeAt(point) < .05f;
        check(open.All(Open), $"{name} mechs cross open ice, whatever the bed beneath: " + string.Join("; ", open.Where(point => !Open(point)).Take(4)
            .Select(point => $"{point} passable {terrain.Passable(point)} slope {terrain.SlopeAt(point):F3}")));
        check(terrain.Patches.All(patch => terrain.LiquidAt(patch.Center) is null), $"{name} rocks and vegetation stand on dry ground");
    }
    /// <summary>A hand-built bridge over a ravine: mechs cross on the deck, between its rails, and never climb on from beside it or pass beneath.</summary>
    private static void CheckBridge(Action<bool, string> check)
    {
        var relief = new float[BattlefieldTerrain.GridSize * BattlefieldTerrain.GridSize];
        var half = (BattlefieldTerrain.GridSize - 1) / 2;
        for (var index = 0; index < relief.Length; index++)
            relief[index] = -20 * Math.Clamp(1 - Math.Abs((index % BattlefieldTerrain.GridSize - half) * BattlefieldTerrain.CellSize) / 90, 0, 1);
        var road = new TerrainRoad([new(-600, 0), new(600, 0)]);
        var bridge = new TerrainBridge(new(-120, 0), new(120, 0), 0, 0, TerrainKind.Water);
        var forest = new TerrainPatch(TerrainKind.Vegetation, new(-400, 0), 60);
        var terrain = new BattlefieldTerrain(8, [forest], [], [], null, relief, null, new([road], [bridge], []));
        check(terrain.HeightAt(Vector2.Zero) == 0 && terrain.GroundAt(Vector2.Zero) == -20, "a bridge deck stands over the ground beneath it");
        // A mech keeps its centre a mech radius and 1.5 metres inside the rails, and keeps as far clear of the deck beside it.
        var footing = BattlefieldTerrain.DeckWidth / 2 - CombatRules.UnitRadius - 1.5f;
        check(terrain.Passable(Vector2.Zero) && terrain.Passable(new(0, footing - .5f)) && !terrain.Passable(new(0, BattlefieldTerrain.DeckWidth / 2 + 1)) && terrain.Passable(new(0, 70)),
            "mechs walk the deck between its rails, and the ravine floor beyond them");
        check(!terrain.CanTraverse(new(0, -80), new(0, 80)) && !terrain.CanTraverse(new(0, 70), new(0, 10)),
            "nothing passes beneath a bridge or climbs onto it from the side");
        var route = terrain.FindPath(new(-400, 0), new(400, 0)); var previous = new Vector2(-400, 0);
        check(route.Count > 0 && route[^1].DistanceTo(new(400, 0)) < .3f && route.All(point => { var clear = terrain.CanTraverse(previous, point); previous = point; return clear; }),
            "navigation crosses the bridge");
        check(Mathf.IsEqualApprox(terrain.SpeedFactor(new(-400, 0)), BattlefieldTerrain.RoadSpeed) && terrain.VegetationModifier(new(-600, 40), new(-400, 0)) == 1
            && Mathf.IsEqualApprox(terrain.SpeedFactor(new(-400, 100)), 1), "a road's speed replaces vegetation's slowdown but not its cover");
        CheckMovement(terrain, check);
    }
    /// <summary>Roads join the settlements and leave the map on graded, rock-free beds; bridges carry them over liquid.</summary>
    private static void CheckRoads(BattlefieldTerrain terrain, Action<bool, string> check)
    {
        var name = $"{terrain.Biome} {terrain.Seed}";
        bool OnDeck(Vector2 point) => terrain.HeightAt(point) > terrain.GroundAt(point) + .01f;
        var course = terrain.Roads.SelectMany(road => road.Course).ToArray();
        check(terrain.Roads.Count > 0 && course.Any(point => Math.Max(Math.Abs(point.X), Math.Abs(point.Y)) > 2840), $"{name} has roads, one leaving the map");
        if (terrain.Biome != Biome.Archipelago)
            check(terrain.Buildings.GroupBy(building => building.Cluster).All(cluster =>
            {
                var middle = cluster.Aggregate(Vector2.Zero, (sum, building) => sum + building.Center) / cluster.Count();
                var reach = cluster.Max(building => building.Center.DistanceTo(middle)) + 150;
                return course.Any(point => point.DistanceTo(middle) <= reach);
            }), $"{name} roads reach every settlement");
        var ground = course.Where(point => !OnDeck(point)).ToArray();
        check(ground.All(point => !terrain.Contains(TerrainKind.Rock, point)) && ground.Count(terrain.Passable) >= ground.Length * .97f,
            $"{name} roads are clear of rocks and passable: {ground.Count(terrain.Passable)} of {ground.Length}");
        check(ground.Where(point => terrain.LiquidAt(point) is null && terrain.Passable(point))
                .All(point => Mathf.IsEqualApprox(terrain.SpeedFactor(point) * (1 + terrain.SlopeAt(point)), BattlefieldTerrain.RoadSpeed)),
            $"{name} roads carry mechs at road speed");
        foreach (var bridge in terrain.Bridges)
        {
            var middle = (bridge.From + bridge.To) / 2; var across = (bridge.To - bridge.From).Normalized().Orthogonal();
            check(OnDeck(middle) && terrain.Passable(middle) && terrain.LiquidAt(middle) is null && !terrain.Passable(middle + across * (BattlefieldTerrain.DeckWidth / 2 + CombatRules.UnitRadius)),
                $"{name} {bridge.Over} bridge at {middle} carries mechs between its rails");
        }
    }
    private static (float Low, float High) Relief(BattlefieldTerrain terrain)
    {
        float low = float.MaxValue, high = float.MinValue;
        for (var z = -2880f; z <= 2880; z += BattlefieldTerrain.CellSize) for (var x = -2880f; x <= 2880; x += BattlefieldTerrain.CellSize)
        { var height = terrain.HeightAt(new(x, z)); low = Math.Min(low, height); high = Math.Max(high, height); }
        return (low, high);
    }
    /// <summary>Every biome's checks for another seed: each seed is its own part, since a seed's ten maps take a while to generate.</summary>
    public static void RunSeed7(Action<bool, string> check) => CheckBiomes(null, check, 7);
    public static void RunSeedMinus41(Action<bool, string> check) => CheckBiomes(null, check, -41);
    private static void CheckBiomes(IReadOnlyDictionary<Biome, BattlefieldTerrain>? standard, Action<bool, string> check, int seed)
    {
        {
            var all = standard ?? Biomes.All.ToDictionary(spec => spec.Biome, spec => BattlefieldTerrain.Generate(seed, spec.Biome));
            var valley = all[Biome.Valley]; var arctic = all[Biome.Arctic]; var lunar = all[Biome.Lunar];
            check(arctic.Biome == Biome.Arctic && arctic.Liquids.Any(body => body.Shape == LiquidShape.Lake) && arctic.Patches.Any(patch => patch.Kind == TerrainKind.Vegetation)
                && arctic.Liquids.All(body => body.Kind == TerrainKind.Ice), "arctic maps have frozen lakes and sparse forests but no open water");
            check(lunar.Biome == Biome.Lunar && lunar.Patches.Count > 60 && lunar.Patches.All(patch => patch.Kind == TerrainKind.Rock) && lunar.Liquids.Count == 0,
                "lunar maps have boulder fields and neither vegetation nor water");
            check(arctic.Patches.Where(patch => patch.Kind == TerrainKind.Vegetation).Sum(patch => patch.Radius * patch.Radius)
                < valley.Patches.Where(patch => patch.Kind == TerrainKind.Vegetation).Sum(patch => patch.Radius * patch.Radius) / 2,
                "arctic forests are sparser than valley forests");
            check(Relief(valley).High > 150 && Relief(arctic).High > 180 && Relief(lunar).Low < -40,
                "relief is shaped per biome: valley hills, arctic ridges and lunar craters");
            check(!valley.Patches.SequenceEqual(arctic.Patches) && !SameRelief(valley, arctic), "the same seed in another biome is another battlefield");
            check(Relief(all[Biome.Highlands]).High > 250 && Relief(all[Biome.Grassland]).High - Relief(all[Biome.Grassland]).Low < 80
                && Relief(all[Biome.Volcanic]).High > 120,
                "relief is shaped per biome: highland mountains, flat grassland and volcanic cones");
            check(all[Biome.Tropical].Patches.Where(patch => patch.Kind == TerrainKind.Vegetation).Sum(patch => patch.Radius * patch.Radius)
                > valley.Patches.Where(patch => patch.Kind == TerrainKind.Vegetation).Sum(patch => patch.Radius * patch.Radius),
                "tropical jungle is denser than valley forest");
            foreach (var (biome, kinds, liquid, rivers) in new (Biome, TerrainKind[], TerrainKind, (int, int))[]
            {
                (Biome.Valley, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Water, (1, 2)),
                (Biome.Arctic, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Ice, (0, 1)),
                (Biome.Highlands, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Water, (0, 1)),
                (Biome.Desert, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Water, (0, 0)),
                (Biome.Tropical, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Water, (1, 2)),
                (Biome.Grassland, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Water, (0, 1)),
                (Biome.Archipelago, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Water, (0, 0)),
                (Biome.Volcanic, [TerrainKind.Rock], TerrainKind.Lava, (1, 2)),
                (Biome.Toxic, [TerrainKind.Rock, TerrainKind.Vegetation], TerrainKind.Water, (0, 0)),
            })
            {
                var terrain = all[biome];
                check(terrain.Biome == biome && terrain.Patches.Select(patch => patch.Kind).ToHashSet().SetEquals(kinds)
                    && terrain.Liquids.Count > 0 && terrain.Liquids.All(body => body.Kind == liquid)
                    && terrain.Liquids.Count(body => body.Shape == LiquidShape.River) is var count && count >= rivers.Item1 && count <= rivers.Item2
                    && terrain.Liquids.Any(body => body.Shape == (biome == Biome.Archipelago ? LiquidShape.Sea : LiquidShape.Lake)),
                    $"{biome} maps carry exactly their biome's features, lakes and rivers");
            }
            var sea = all[Biome.Archipelago]; var share = (float)sea.LiquidNodes(0).Count() / (BattlefieldTerrain.GridSize * BattlefieldTerrain.GridSize);
            check(sea.Liquids.Count == 1 && share is > .3f and < .5f && Grid().Any(point => sea.LiquidAt(point)?.Kind == TerrainKind.DeepWater)
                && Grid().Any(point => sea.LiquidAt(point)?.Kind == TerrainKind.Water), $"archipelago islands stand in a sea with shallows and deep water: {share:P0}");
            foreach (var terrain in all.Values)
            {
                CheckLiquids(terrain, check);
                CheckRoads(terrain, check);
                var clusters = terrain.Buildings.GroupBy(building => building.Cluster).ToArray();
                check(clusters.Length is >= BattlefieldTerrain.MinClusters and <= BattlefieldTerrain.MaxClusters
                    && terrain.Buildings.Count is >= BattlefieldTerrain.MinBuildings and <= BattlefieldTerrain.MaxBuildings
                    && terrain.Buildings.All(building => building.Height is >= 15 * CombatRules.BodyScale - .001f and <= 60 * CombatRules.BodyScale + .001f),
                    $"{terrain.Biome} settlements hold 15–50 buildings, 3.5–14 m tall, in one to six clusters: {terrain.Buildings.Count} in {clusters.Length}");
                var styles = terrain.Buildings.Select(building => building.Style).ToHashSet();
                check(terrain.Biome switch
                {
                    Biome.Arctic => styles.IsSubsetOf([BuildingStyle.Block, BuildingStyle.Tank, BuildingStyle.Mast]),
                    Biome.Lunar => styles.IsSubsetOf([BuildingStyle.Dome, BuildingStyle.Module]),
                    Biome.Highlands => styles.IsSubsetOf([BuildingStyle.House, BuildingStyle.Block, BuildingStyle.Mast]),
                    Biome.Desert => styles.IsSubsetOf([BuildingStyle.Block, BuildingStyle.Tank, BuildingStyle.Dome]),
                    Biome.Tropical => styles.IsSubsetOf([BuildingStyle.House, BuildingStyle.Barn, BuildingStyle.Block]),
                    Biome.Grassland => styles.IsSubsetOf([BuildingStyle.House, BuildingStyle.Barn, BuildingStyle.Silo]),
                    Biome.Archipelago => styles.IsSubsetOf([BuildingStyle.House, BuildingStyle.Block, BuildingStyle.Tank]),
                    Biome.Volcanic => styles.IsSubsetOf([BuildingStyle.Block, BuildingStyle.Tank, BuildingStyle.Stack]),
                    Biome.Toxic => styles.IsSubsetOf([BuildingStyle.Dome, BuildingStyle.Module, BuildingStyle.Tank]),
                    _ => styles.IsSubsetOf([BuildingStyle.House, BuildingStyle.Barn, BuildingStyle.Silo, BuildingStyle.Block]),
                }, $"{terrain.Biome} buildings use the biome's styles");
                check(terrain.Buildings.All(building => terrain.Patches.All(patch => patch.Center.DistanceTo(building.Center) > patch.Radius + 30)
                    && terrain.LiquidAt(building.Center) is null), "terrain features and liquids keep clear of settlements");
                check(clusters.All(cluster => cluster.Select(building => terrain.HeightAt(building.Center)).Distinct().Count() == 1),
                    "each settlement stands on a levelled pad");
                // Every building can be approached on foot from the deployment area.
                var spawn = new Vector2(0, CombatRules.DeploymentDepth);
                check(terrain.Buildings.All(building => Enumerable.Range(0, 16).Select(index => building.Center
                        + Vector2.FromAngle(index * Mathf.Tau / 16) * (building.HalfSize.Length() + BattlefieldTerrain.StreetWidth / 2))
                    .Any(point => terrain.Passable(point) && terrain.FindPath(spawn, point) is { Count: > 0 } route && route[^1].DistanceTo(point) < .3f)),
                    $"{terrain.Biome} streets leave every building reachable");
            }
        }
    }
    private static void CheckBuildings(Action<bool, string> check)
    {
        var block = new TerrainBuilding(BuildingStyle.Block, 0, Vector2.Zero, new(30, 15), 0, 40);
        var solid = new BattlefieldTerrain(1, [], [], [block]);
        const float clearance = CombatRules.UnitRadius + 1.5f;
        check(!solid.Passable(new(30 + clearance - .5f, 0)) && solid.Passable(new(30 + clearance + .5f, 0))
            && !solid.Passable(new(0, 15 + clearance - .5f)) && solid.Passable(new(0, 15 + clearance + .5f)),
            "rectangular buildings reserve their footprint plus mech clearance");
        var turned = new BattlefieldTerrain(2, [], [], [block with { Rotation = Mathf.Pi / 2 }]);
        check(turned.Passable(new(15 + clearance + .5f, 0)) && !turned.Passable(new(0, 30 + clearance - .5f)) && turned.Passable(new(0, 30 + clearance + .5f)),
            "rotated footprints turn with the building");
        var diagonal = new BattlefieldTerrain(3, [], [], [block with { Rotation = Mathf.Pi / 4 }]);
        check(!diagonal.Passable(new(20, 20)) && diagonal.Passable(new(-20, 20)) && diagonal.Passable(new(41, 0)),
            "diagonal footprints block along their length and leave the space beside their corners open");
        check(!solid.ClearSight(new(-240, 0), new(240, 0)) && solid.ClearSight(new(-240, 120), new(240, 120)), "buildings block intersecting sight and firing rays");
        check(solid.ObstacleTop(Vector2.Zero) == 40 && solid.SolidTop(new(30 + CombatRules.UnitRadius / 2, 0)) == 0 && solid.ObstacleTop(new(30 + CombatRules.UnitRadius / 2, 0)) == 40,
            "jumping mechs must clear roofs, including the mech's own width");
        check(solid.RayDistance(new(0, 100, 0), Vector3.Down, 200) is { } roof && Math.Abs(roof - 60) < .01f
            && solid.RayDistance(new(-100, 20, 0), Vector3.Right, 200) is { } wall && Math.Abs(wall - 70) < .01f,
            "camera and shot rays stop at roofs and walls");
        var over = new BattlefieldTerrain(4, [], [new(new Vector2(-240, 0), 120, 150), new(new Vector2(240, 0), 120, 150)],
            [block with { Height = 20 }]);
        check(over.ClearSight(new(-240, 0), new(240, 0)), "height-aware sight can pass above low buildings");
        var path = solid.FindPath(new(-240, 0), new(240, 0)); var origin = new Vector2(-240, 0);
        check(path.Count > 1, "navigation detours around buildings");
        foreach (var waypoint in path) { check(solid.CanTraverse(origin, waypoint), "detours cannot clip building clearance"); origin = waypoint; }
        var dome = new BattlefieldTerrain(5, [], [], [new(BuildingStyle.Dome, 0, Vector2.Zero, new(20, 0), 1, 30)]);
        var diagonalReach = (20 + clearance) / Mathf.Sqrt2;
        check(!dome.Passable(Vector2.One * (diagonalReach - .5f)) && dome.Passable(Vector2.One * (diagonalReach + .5f)) && !dome.ClearSight(new(-240, 10), new(240, 10)),
            "domes use round footprints");
        CheckMovement(solid, check);
    }
    private static void CheckNavigationQueries(BattlefieldTerrain terrain, Action<bool, string> check)
    {
        var from = new Vector2(-360, 1920); var to = new Vector2(360, -1920);
        Vector2[] expected = [new(-705, 1515), new(-165, -990), new(120, -1410), to];
        var retained = terrain.FindPath(from, to);
        check(retained.SequenceEqual(expected), $"seeded path retains its original waypoints and tie ordering: {string.Join(", ", retained)}");
        var rocks = new BattlefieldTerrain(1, [new(TerrainKind.Rock, Vector2.Zero, 60, 120)], []);
        check(rocks.FindPath(new(-240, 0), new(240, 0)).SequenceEqual(new Vector2[] { new(30, -75), new(240, 0) }),
            $"symmetric rock detour preserves the original route choice: {string.Join(", ", rocks.FindPath(new(-240, 0), new(240, 0)))}");
        check(rocks.FiringPosition(new(-240, 0), new(240, 0), 180, 450) == new Vector2(60, -30),
            $"equally ranked clear firing positions retain row-major tie ordering: {rocks.FiringPosition(new(-240, 0), new(240, 0), 180, 450)}");
        check(rocks.FiringPosition(new(-240, 0), new(240, 0), 180, 0) == new Vector2(-240, 0),
            "firing search with no eligible cells returns its starting position");
        var routes = new IReadOnlyList<Vector2>[8];
        var firing = new Vector2[8];
        Parallel.For(0, routes.Length, index =>
        {
            routes[index] = terrain.FindPath(index % 2 == 0 ? from : to, index % 2 == 0 ? to : from);
            firing[index] = rocks.FiringPosition(new(-240, 0), new(240, 0), 180, 450);
        });
        var reverse = terrain.FindPath(to, from);
        check(routes.Select((route, index) => route.SequenceEqual(index % 2 == 0 ? expected : reverse)).All(equal => equal)
            && firing.All(point => point == new Vector2(60, -30)) && retained.SequenceEqual(expected),
            "concurrent navigation queries and buffer reuse preserve independent returned paths");

        // Wide allocation budgets catch grid-sized scratch arrays without depending on machine timing.
        terrain.FindPath(from, to);
        var before = GC.GetAllocatedBytesForCurrentThread();
        terrain.FindPath(from, to);
        var pathBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        check(pathBytes < 200_000, $"warmed navigation avoids grid-sized allocations: {pathBytes} bytes");
        rocks.FiringPosition(new(-240, 0), new(240, 0), 180, 450);
        before = GC.GetAllocatedBytesForCurrentThread();
        rocks.FiringPosition(new(-240, 0), new(240, 0), 180, 450);
        var firingBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        check(firingBytes < 16_000, $"warmed firing search avoids candidate-array allocations: {firingBytes} bytes");
    }
    private static void CheckEngagement(BattlefieldTerrain terrain, string[] definitions, Action<bool, string> check, int seed = 183)
    {
        var roster = ForceRoster.Create("Terrain engagement", definitions.Select(DraftCatalog.Mech), TestPilots.Warriors.Take(definitions.Length));
        for (var i = 0; i < definitions.Length; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), terrain, seed);
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
            // Jumping mechs may pass over rocks and steep ground; they must still land on passable terrain.
            sim.Advance(1); passable &= sim.Units.All(unit => unit.Airborne || terrain.Passable(unit.Position));
        }
        Console.WriteLine($"Terrain engagement: seed {terrain.Seed}, {definitions.Length} per side, {sim.Elapsed:0}s, {sim.Outcome}, passable {passable}");
        check(passable && sim.Outcome != CombatOutcome.Ongoing, "generated-terrain engagement completes without entering impassable terrain: "
            + string.Join("; ", sim.Units.Select(unit => $"{unit.Id} {unit.Position} {unit.Order} shots={unit.ShotsFired}")));
    }
    private static float Travel(BattlefieldTerrain terrain)
    {
        var sim = Battle(terrain); var unit = sim.Units[0]; unit.Position = new Vector2(-300, 0); unit.Facing = Vector2.Right;
        sim.Move([unit.Id], new Vector2(300, 0)); sim.Paused = false; sim.Advance(8);
        return unit.Position.X + 300;
    }
    private static void CheckMovement(BattlefieldTerrain terrain, Action<bool, string> check)
    {
        var sim = Battle(terrain); var unit = sim.Units[0]; unit.Position = new Vector2(-240, 0); unit.Facing = Vector2.Right;
        sim.Move([unit.Id], new Vector2(240, 0)); sim.Paused = false;
        var clear = true;
        for (var step = 0; step < 1600 && unit.Order != CombatOrder.Hold; step++) { sim.Advance(.05); clear &= terrain.Passable(unit.Position); }
        check(clear && unit.Position.DistanceTo(new Vector2(240, 0)) < 9 && unit.Order == CombatOrder.Hold, $"actual mech movement follows a terrain detour and arrives: {unit.Position}");
    }
    private static void CheckSightAndFire(BattlefieldTerrain terrain, Action<bool, string> check)
    {
        var sim = Battle(terrain, 2); var friend = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        var scout = sim.Units.Last(unit => unit.Side == CombatSide.Friendly); var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        friend.Position = new Vector2(-240, 0); friend.Facing = Vector2.Right; enemy.Position = new Vector2(150, 0);
        scout.Position = new Vector2(-30, 180); sim.RefreshDetection();
        check(sim.Detected(friend.Side, enemy.Id) && sim.SelectTarget(friend) is null && sim.TerrainHitChance(friend, enemy, friend.Weapons[0]) == 0,
            "shared contact does not permit a shot through a rock");
        // The fixture's rock is laid out for a 270 m stand-off, under the old unrounded Jenner default.
        friend.PreferredDistance = 270;
        sim.Attack([friend.Id], enemy.Id); sim.Paused = false; var clear = true;
        for (var step = 0; step < 1200 && friend.ShotsFired == 0; step++) { sim.Advance(.05); clear &= terrain.Passable(friend.Position); }
        check(clear && friend.ShotsFired > 0 && terrain.ClearSight(friend.Position, enemy.Position), "blocked attack orders navigate to a clear firing position");
        sim.Hold([friend.Id]); friend.Position = new Vector2(-240, 0); scout.Position = new Vector2(-2700, 2400); sim.RefreshDetection();
        check(!sim.Detected(friend.Side, enemy.Id) && sim.Contacts(friend.Side)[enemy.Id].LastKnownPosition == enemy.Position, "terrain occlusion loses detection and retains stationary last-known contact");
    }
}
