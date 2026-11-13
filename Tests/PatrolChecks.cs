using Godot;
using System.Text.Json;

namespace Mech.Tests;

/// <summary>Patrol battles: layout, passive and hunting enemy groups, visiting points and extraction. Engine-free.</summary>
internal static class PatrolChecks
{
    private static ForceRoster Company(int count)
    {
        var roster = ForceRoster.Create("Patrol checks", Enumerable.Repeat(DraftCatalog.Mech("wolverine"), count), DraftCatalog.Warriors.Take(count));
        for (var i = 0; i < count; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        return roster;
    }

    private static MissionOffer Offer(int locations)
    {
        var opposition = Enumerable.Range(0, locations - 1).SelectMany(group => new[]
        {
            new EnemyMech("locust", EnemyPilotRank.Green, group), new EnemyMech("jenner", EnemyPilotRank.Green, group),
        }).ToArray();
        return new("offer-1", "Patrol of Checks", "Briefing.", MissionTuning.PatrolObjective, StarChart.Instance.StartingSystemId,
            Campaign.StartDate, opposition, 100_000, 0, 0, Party.Of(Faction.Steiner), Party.Pirates, MissionKind.Patrol, locations);
    }

    private static CombatSimulation Patrol(int locations = 3, int company = 2, int terrainSeed = 4242, int seed = 7)
    {
        var roster = Company(company);
        return CombatSimulation.DeployMission(roster, roster.Mechs.Select(mech => mech.Id), Offer(locations), seed, terrainSeed, Biome.Grassland);
    }

    public static void Run(Action<bool, string> check)
    {
        CheckLayout(check);
        CheckPassive(check);
        CheckFire(check);
        CheckSearch(check);
        CheckObjectives(check);
        CheckReplica(check);
        CheckPlaythrough(check);
    }

    /// <summary>A whole patrol with enemy AI on: the company marches the route, fighting whatever it wakes.</summary>
    private static void CheckPlaythrough(Action<bool, string> check)
    {
        var roster = ForceRoster.Create("Patrol march", new[] { "atlas", "atlas", "awesome", "awesome" }.Select(DraftCatalog.Mech), DraftCatalog.Warriors.Take(4));
        for (var i = 0; i < 4; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var sim = CombatSimulation.DeployMission(roster, roster.Mechs.Select(mech => mech.Id), Offer(3), 21, 5150, Biome.Grassland);
        sim.Paused = false;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var company = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        for (var second = 0; second < 1500 && sim.Outcome == CombatOutcome.Ongoing; second += 5)
        {
            var goal = sim.Patrol!.Layout.Locations[sim.Patrol.Visited];
            var idle = company.Where(unit => !unit.Defeated && unit.Order != CombatOrder.Attack && (unit.Order != CombatOrder.Move || unit.Destination.DistanceTo(goal) > 120))
                .Select(unit => unit.Id).ToArray();
            if (idle.Length > 0) sim.Move(idle, goal);
            sim.Advance(5);
        }
        Console.WriteLine($"Patrol march: {sim.Outcome} after {sim.Elapsed:0} s, visited {sim.Patrol!.Visited}, {clock.Elapsed.TotalSeconds:0.0} s to simulate");
        check(sim.Patrol.Groups.All(group => !group.Passive) && sim.Units.Where(unit => unit.Side == CombatSide.Enemy).Sum(unit => unit.ShotsFired) > 0,
            "marching the patrol route wakes every group, and they fight");
        check(sim.Outcome != CombatOutcome.Ongoing, $"a marched patrol is decided ({sim.Outcome}, {sim.Patrol.Visited} of {sim.Patrol.Layout.PatrolPoints} points)");
    }

    private static float Half => CombatRules.ArenaHalfSize;
    private static float EdgeDistance(Vector2 point) => Half - Math.Max(Math.Abs(point.X), Math.Abs(point.Y));

    private static void CheckLayout(Action<bool, string> check)
    {
        foreach (var locations in new[] { 2, 3, 4 })
        {
            var sim = Patrol(locations, terrainSeed: 100 + locations);
            var layout = sim.Patrol!.Layout;
            var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
            var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
            check(layout.Locations.Length == locations && layout.Posts.Length == locations - 1 && sim.Patrol.Groups.Count == locations - 1,
                $"a {locations}-location patrol has {locations - 1} patrol {(locations == 2 ? "point" : "points")} plus extraction and one group per leg");
            check(layout.StartEdge != layout.ExtractionEdge && Math.Abs(EdgeDistance(layout.Extraction) - CombatRules.ExtractionRadius) < 1,
                $"the {locations}-location extraction zone touches a different edge from the start");
            check(layout.Locations[..^1].All(point => EdgeDistance(point) >= CombatRules.PatrolEdgeMargin - 1),
                $"{locations}-location patrol points keep 400 m from every edge");
            check(friends.All(unit => Math.Abs(Half + unit.Position.Dot(PatrolLayout.Inward(layout.StartEdge)) - CombatRules.PatrolStartInset) < 1
                && unit.Facing.IsEqualApprox(PatrolLayout.Inward(layout.StartEdge))),
                $"the company starts 960 m in from its edge, facing into the arena ({locations} locations)");
            check(enemies.All(unit => unit.Group is { } group && group < layout.Posts.Length
                && unit.Position.DistanceTo(layout.Posts[group]) < 250 && sim.Passive(unit)),
                $"each enemy group gathers, passive, around its post ({locations} locations)");
            var legs = Enumerable.Range(0, layout.Posts.Length).All(group =>
            {
                var from = layout.Locations[group]; var to = layout.Locations[group + 1];
                return layout.Posts[group].DistanceTo((from + to) / 2) <= CombatRules.PatrolGroupOffset + 150;
            });
            check(legs, $"a group's post lies near the middle of its leg between consecutive locations ({locations} locations)");
            var reachable = layout.Locations.Append(layout.Starts[0]).All(point => sim.Terrain.Passable(point))
                && layout.Locations.All(point => sim.Terrain.FindPath(layout.Starts[0], point).Count > 0);
            check(reachable, $"every patrol location is clear and reachable from the start ({locations} locations)");
        }
        var a = Patrol(terrainSeed: 55); var b = Patrol(terrainSeed: 55);
        check(JsonSerializer.Serialize(a.Patrol!.Layout.Locations.Select(point => new[] { point.X, point.Y }))
            == JsonSerializer.Serialize(b.Patrol!.Layout.Locations.Select(point => new[] { point.X, point.Y }))
            && a.Units.Select(unit => unit.Position).SequenceEqual(b.Units.Select(unit => unit.Position)),
            "the same terrain seed lays out the same patrol, so co-op replicas match");

        // The chosen order walks no further than any other order of the same points.
        var sim4 = Patrol(4, terrainSeed: 909); var layout4 = sim4.Patrol!.Layout;
        float Walk(Vector2 from, Vector2 to)
        {
            var length = 0f; var at = from;
            foreach (var point in sim4.Terrain.FindPath(from, to)) { length += at.DistanceTo(point); at = point; }
            return length;
        }
        float Route(IEnumerable<Vector2> points)
        {
            var at = layout4.StartCenter; var total = 0f;
            foreach (var point in points.Append(layout4.Extraction)) { total += Walk(at, point); at = point; }
            return total;
        }
        var points = layout4.Locations[..^1];
        var chosen = Route(points);
        var permutations = new[] { new[] { 0, 1, 2 }, [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0] };
        check(permutations.All(order => chosen <= Route(order.Select(index => points[index])) + 1),
            "patrol points are visited in the order that walks least from the start to extraction");
    }

    private static void CheckPassive(Action<bool, string> check)
    {
        var sim = Patrol(3, terrainSeed: 77); sim.Paused = false;
        var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        var start = enemies.Select(unit => (unit.Position, unit.Facing)).ToArray();
        sim.Advance(10);
        check(enemies.Select(unit => (unit.Position, unit.Facing)).SequenceEqual(start) && enemies.All(sim.Passive)
            && enemies.All(unit => unit.TorsoTwist == 0 && unit.ShotsFired == 0),
            "passive groups stand still, without turning, twisting or firing, while the company is out of contact");

        // One company mech walks up to group 0; only that group wakes.
        var scout = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        var post = sim.Patrol!.Layout.Posts[0];
        scout.Position = sim.Terrain.ReachableDestination(post, post + (sim.Patrol.Layout.Posts[0] - sim.Patrol.Layout.Locations[0]).Normalized() * -150);
        sim.Advance(.1);
        var woke = !sim.Patrol.Groups[0].Passive;
        var other = sim.Patrol.Groups[1].Passive || sim.Units.Where(unit => unit.Group == 1).Any(unit => sim.Units.Where(f => f.Side == CombatSide.Friendly)
            .Any(f => f.Position.DistanceTo(unit.Position) < 1000));
        check(woke && other, "a group wakes when one of its own mechs detects the company, and other groups stay passive");
        // Its side now knows where the scout is, but a passive group ignores that.
        check(sim.Detected(CombatSide.Enemy, scout.Id) && (sim.Patrol.Groups[1].Passive || !sim.Units.Where(unit => unit.Group == 1).All(sim.Passive)),
            "a passive group ignores contacts the rest of its side has made");
        sim.Advance(5);
        check(sim.Units.Where(unit => unit.Group == 0 && unit.Active).Any(unit => unit.Order == CombatOrder.Attack && unit.AttackTarget == scout.Id),
            "an awakened group attacks the company mech its side detects");
    }

    private static void CheckFire(Action<bool, string> check)
    {
        var sim = Patrol(2, terrainSeed: 31); sim.Paused = false;
        var target = sim.Units.First(unit => unit.Group == 0);
        var shooter = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        // A long-range salvo from beyond the group's sensors: 2 s in flight, then a second to react.
        sim.HeardFire(shooter, target, new CombatShot(shooter.Id, target.Id, shooter.Position, target.Position, "LRM 20", false, MechLocation.CenterTorso, 0)
        { FlightSeconds = 2 });
        sim.Advance(2.9);
        var waiting = sim.Patrol!.Groups[0].Passive;
        sim.Advance(.2);
        check(waiting && !sim.Patrol.Groups[0].Passive, "a group wakes one second after an attack on it resolves, hit or miss");
        var laser = Patrol(2, terrainSeed: 31); laser.Paused = false;
        var struck = laser.Units.First(unit => unit.Group == 0); var firing = laser.Units.First(unit => unit.Side == CombatSide.Friendly);
        laser.HeardFire(firing, struck, new CombatShot(firing.Id, struck.Id, firing.Position, struck.Position, "Medium Laser", true, MechLocation.LeftArm, 25));
        laser.Advance(.9); var still = laser.Patrol!.Groups[0].Passive; laser.Advance(.2);
        check(still && !laser.Patrol.Groups[0].Passive, "a laser's attack resolves as it fires, so its target's group wakes a second later");
        sim.Advance(4);
        var hunters = sim.Units.Where(unit => unit.Group == 0 && unit.Active).ToArray();
        check(!sim.Detected(CombatSide.Enemy, shooter.Id) && hunters.Any(unit => unit.Order == CombatOrder.Move
            && unit.Destination.DistanceTo(sim.Terrain.ReachableDestination(unit.Position, shooter.Position)) < 150),
            "a group fired on from beyond its sensors heads for where the fire came from");
    }

    private static void CheckSearch(Action<bool, string> check)
    {
        var sim = Patrol(2, terrainSeed: 64); sim.Paused = false;
        var group = sim.Units.Where(unit => unit.Group == 0).ToArray();
        var shooter = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        // A shot heard from just beside the post sends the group a short way to search.
        var post = sim.Patrol!.Layout.Posts[0];
        var near = sim.Terrain.ReachableDestination(post, post + new Vector2(200, 0));
        var origin = shooter.Position; shooter.Position = near;
        sim.HeardFire(shooter, group[0], new CombatShot(shooter.Id, group[0].Id, near, group[0].Position, "Medium Laser", false, MechLocation.CenterTorso, 0));
        shooter.Position = origin;
        var searched = false; var returned = false; double? began = null;
        for (var second = 0; second < 240 && !returned; second++)
        {
            sim.Advance(1);
            if (sim.Patrol.Groups[0].Search is not null) { searched = true; began ??= sim.Elapsed; }
            if (searched && sim.Patrol.Groups[0].Search is null && group.Where(unit => unit.Active).All(unit => unit.Order == CombatOrder.Move && unit.Destination.DistanceTo(unit.Post) < 60
                || unit.Position.DistanceTo(unit.Post) < 60)) returned = true;
        }
        check(searched && returned && sim.Elapsed - began!.Value >= CombatRules.PatrolSearchSeconds - 1,
            "a group that finds nothing searches for 30 seconds, then returns to its posts");
        check(!sim.Patrol.Groups[0].Passive, "a group back at its posts stays alert");
    }

    private static void CheckObjectives(Action<bool, string> check)
    {
        // Teleporting past the opposition: enemy AI is off and the battle is checked step by step.
        var sim = Patrol(3, company: 2, terrainSeed: 12); sim.EnemyAiEnabled = false; sim.Paused = false;
        var layout = sim.Patrol!.Layout;
        var (lead, wing) = (sim.Units.First(unit => unit.Side == CombatSide.Friendly), sim.Units.Last(unit => unit.Side == CombatSide.Friendly));
        lead.Position = layout.Locations[1];
        sim.Advance(.05);
        check(sim.Patrol.Visited == 0, "patrol points count only in order");
        lead.Position = layout.Locations[0] + new Vector2(CombatRules.PatrolVisitRadius - 5, 0);
        sim.Advance(.05);
        check(sim.Patrol.Visited == 1 && sim.Patrol.Events.Last().Text.StartsWith("Patrol point 1 of 2", StringComparison.Ordinal),
            "a company mech within 100 m of the current point visits it, and the log records it");
        wing.Position = layout.Extraction; sim.Advance(.05);
        check(wing.Extracted && wing.Status == CombatStatus.Extracted && wing.Defeated && !wing.Solid && !sim.Detected(CombatSide.Enemy, wing.Id),
            "a mech entering the extraction zone leaves the battle at any time");
        lead.Position = layout.Locations[1]; sim.Advance(.05);
        check(sim.Patrol.AllVisited && sim.Outcome == CombatOutcome.Ongoing, "visiting every point does not end the patrol while a mech is on the field");
        lead.Position = layout.Extraction + PatrolLayout.Inward(layout.ExtractionEdge) * 100; sim.Advance(.05);
        sim.Advance(CombatRules.WindDownSeconds + .1);
        check(sim.Outcome == CombatOutcome.Victory && sim.MissionReport().Outcome == MissionOutcome.Success,
            "the patrol succeeds once every point is visited and every remaining mech has extracted");

        var early = Patrol(3, company: 1, terrainSeed: 12); early.EnemyAiEnabled = false; early.Paused = false;
        early.Units.First(unit => unit.Side == CombatSide.Friendly).Position = early.Patrol!.Layout.Extraction;
        early.Advance(.05); early.Advance(CombatRules.WindDownSeconds + .1);
        check(early.Outcome == CombatOutcome.Ended && early.MissionReport().Outcome == MissionOutcome.Failure,
            "extracting before every point is visited fails the patrol");

        var lost = Patrol(2, company: 1, terrainSeed: 12); lost.EnemyAiEnabled = false; lost.Paused = false;
        var victim = lost.Units.First(unit => unit.Side == CombatSide.Friendly);
        foreach (var part in new[] { MechLocation.Head, MechLocation.CenterTorso }) victim.ApplyDamage(part, 10_000, victim.Position);
        foreach (var enemy in lost.Units.Where(unit => unit.Side == CombatSide.Enemy)) enemy.ApplyDamage(MechLocation.CenterTorso, 10_000, enemy.Position);
        lost.Advance(CombatRules.WindDownSeconds + .2);
        check(lost.Outcome == CombatOutcome.Defeat, "a patrol whose company is all destroyed is lost");

        var cleared = Patrol(2, company: 1, terrainSeed: 12); cleared.EnemyAiEnabled = false; cleared.Paused = false;
        foreach (var enemy in cleared.Units.Where(unit => unit.Side == CombatSide.Enemy)) enemy.ApplyDamage(MechLocation.CenterTorso, 10_000, enemy.Position);
        cleared.Advance(CombatRules.WindDownSeconds + 1);
        check(cleared.Outcome == CombatOutcome.Ongoing, "destroying every enemy does not end a patrol");
    }

    private static void CheckReplica(Action<bool, string> check)
    {
        var roster = Company(2); var force = roster.Mechs.Select(mech => mech.Id).ToArray();
        var leader = CombatSimulation.DeployMission(roster, force, Offer(3), 3, 808, Biome.Grassland);
        var replica = CombatSimulation.DeployMission(roster, force, Offer(3), 3, 808, Biome.Grassland); replica.BecomeReplica();
        leader.EnemyAiEnabled = false; leader.Paused = false;
        var layout = leader.Patrol!.Layout;
        leader.Units.First(unit => unit.Side == CombatSide.Friendly).Position = layout.Locations[0];
        leader.Units.Last(unit => unit.Side == CombatSide.Friendly).Position = layout.Extraction;
        leader.Advance(.05);
        var target = leader.Units.First(unit => unit.Group == 1);
        leader.HeardFire(leader.Units.First(unit => unit.Side == CombatSide.Friendly), target,
            new CombatShot("x", target.Id, Vector2.Zero, target.Position, "Medium Laser", false, MechLocation.CenterTorso, 0));
        leader.Advance(1.1);
        var state = ((BattleStateMessage)NetProtocol.Decode(NetProtocol.Encode(new BattleStateMessage(leader.State())))!).State;
        replica.Apply(state);
        check(replica.Patrol!.Visited == 1 && replica.Units.Last(unit => unit.Side == CombatSide.Friendly).Extracted
            && !replica.Patrol.Groups[1].Passive && replica.Patrol.Events.Count == leader.Patrol.Events.Count,
            "a co-op replica takes on visited points, extractions, awake groups and the patrol's log");
    }
}
