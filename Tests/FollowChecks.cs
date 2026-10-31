using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static void CheckFollowing(Action<bool, string> check)
    {
        var sim = Battle(3, "jenner"); sim.EnemyAiEnabled = false;
        foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var leader = friends[1]; var followers = new[] { friends[0], friends[2] };
        friends[0].Position = new Vector2(-8, 2); leader.Position = Vector2.Zero; friends[2].Position = new Vector2(8, 2);
        foreach (var unit in friends) unit.Facing = Vector2.Up;
        friends[0].Movement = MovementMode.Run;
        sim.Move([leader.Id], new Vector2(0, -12));
        sim.Follow(friends.Select(unit => unit.Id), leader.Id);
        check(leader.Order == CombatOrder.Move && leader.Destination == new Vector2(0, -12) && leader.FollowTarget is null
            && followers.All(unit => unit.Order == CombatOrder.Follow && unit.FollowTarget == leader.Id),
            "group Follow preserves the selected leader's order and follows with the other units");
        var start = friends[0].Position; sim.Advance(1);
        check(friends[0].Position == start && sim.Elapsed == 0, "Follow queues without moving while paused");
        sim.Paused = false; sim.Advance(80);
        check(leader.Order == CombatOrder.Hold && followers.All(unit => unit.Order == CombatOrder.Follow
            && Math.Abs(unit.Position.DistanceTo(leader.Position) - CombatRules.FollowDistance) < .5f),
            "followers catch a moving leader, stop about three hexes away and keep their Follow orders: "
                + string.Join("; ", friends.Select(unit => $"{unit.Id} {unit.Position} {unit.Order}")));
        check(friends[0].Movement == MovementMode.Run && followers.All(unit => unit.Firing == FiringMode.HoldFire && unit.ShotsFired == 0),
            "Follow retains each follower's movement and firing settings");
        var rested = followers.Select(unit => unit.Position).ToArray();
        sim.Advance(3);
        check(followers.Select((unit, i) => unit.Position.DistanceTo(rested[i]) < .01f).All(still => still), "followers wait while the leader is stopped");
        sim.Move([leader.Id], new Vector2(0, 12)); sim.Advance(90);
        check(followers.All(unit => unit.Position.Y > 7 && unit.Order == CombatOrder.Follow
            && unit.Position.DistanceTo(leader.Position) < 3.5f), "followers automatically resume when their leader moves again: "
                + string.Join("; ", friends.Select(unit => $"{unit.Id} {unit.Position} {unit.Order} speed={unit.CurrentSpeed}")));

        var enemy = Enemy(sim);
        sim.Follow([friends[0].Id], enemy.Id);
        check(friends[0].FollowTarget == leader.Id, "Follow cannot target an opposing unit");
        sim.Move([friends[0].Id], new Vector2(-12, 0));
        check(friends[0].Order == CombatOrder.Move && friends[0].FollowTarget is null, "Move replaces Follow and clears its leader");
        sim.Follow([friends[0].Id], leader.Id); sim.Hold([friends[0].Id]);
        check(friends[0].Order == CombatOrder.Hold && friends[0].FollowTarget is null, "Hold cancels Follow");
        enemy.Position = friends[0].Position + Vector2.Left * 5; sim.RefreshDetection();
        sim.SetPriority([friends[0].Id], enemy.Id); sim.Follow([friends[0].Id], leader.Id);
        sim.Attack([friends[0].Id], enemy.Id);
        check(friends[0].Order == CombatOrder.Attack && friends[0].FollowTarget is null && friends[0].PriorityTarget == enemy.Id,
            "Attack replaces Follow without clearing the persistent priority target");

        foreach (var destroyed in new[] { false, true })
        {
            sim = Battle(2); sim.EnemyAiEnabled = false;
            foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
            var follower = Friendly(sim); leader = sim.Units.Last(unit => unit.Side == CombatSide.Friendly);
            sim.Follow([follower.Id], leader.Id);
            if (destroyed) leader.ApplyDamage(MechLocation.Head, 10000);
            else foreach (var location in new[] { MechLocation.LeftLeg, MechLocation.RightLeg })
                leader.ApplyDamage(location, leader.Locations[location].Armor + leader.Locations[location].Structure);
            sim.Paused = false; sim.Advance(.05);
            check(follower.Order == CombatOrder.Hold && follower.FollowTarget is null && follower.Route.Count == 0,
                $"followers Hold when their leader is {(destroyed ? "destroyed" : "disabled")}");
            sim.Follow([follower.Id], leader.Id);
            check(follower.Order == CombatOrder.Hold, "inactive leaders cannot receive new Follow orders");
        }

        var terrain = new BattlefieldTerrain(1, [new(TerrainKind.Rock, Vector2.Zero, 2, 4)], []);
        var roster = Roster(2, "jenner");
        sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), terrain);
        sim.EnemyAiEnabled = false;
        foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
        var routed = Friendly(sim); leader = sim.Units.Last(unit => unit.Side == CombatSide.Friendly);
        routed.Position = new Vector2(-8, 0); routed.Facing = Vector2.Right; leader.Position = new Vector2(8, 0);
        sim.Follow([routed.Id], leader.Id); sim.Paused = false;
        var passable = true;
        for (var step = 0; step < 1600; step++) { sim.Advance(.05); passable &= terrain.Passable(routed.Position); }
        check(passable && routed.Position.DistanceTo(leader.Position) < 3.5f && routed.Order == CombatOrder.Follow,
            $"Follow navigates around terrain using normal mech clearance: {routed.Position}");

        foreach (var destroyed in new[] { false, true })
        {
            sim = Battle(2, "jenner"); sim.EnemyAiEnabled = false;
            foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
            var follower = Friendly(sim); leader = sim.Units.Last(unit => unit.Side == CombatSide.Friendly);
            follower.Position = new(0, 9); follower.Facing = Vector2.Up; leader.Position = Vector2.Zero;
            var obstacle = Enemy(sim); obstacle.Position = new(0, CombatRules.FollowDistance);
            if (destroyed) obstacle.ApplyDamage(MechLocation.Head, 10000);
            else foreach (var location in new[] { MechLocation.LeftLeg, MechLocation.RightLeg })
                obstacle.ApplyDamage(location, obstacle.Locations[location].Armor + obstacle.Locations[location].Structure);
            sim.Follow([follower.Id], leader.Id); sim.Paused = false; sim.Advance(40);
            check(destroyed ? follower.Position.DistanceTo(obstacle.Position) < .3f && Math.Abs(follower.Position.X) < .001f
                : follower.Position.DistanceTo(obstacle.Position) >= CombatRules.UnitRadius * 2 - .0001f,
                $"Follow {(destroyed ? "accepts a destination occupied by a wreck" : "keeps clearance from a disabled mech")}");
        }

        sim = Battle(2); Arrange(sim); var shooter = Friendly(sim); leader = sim.Units.Last(unit => unit.Side == CombatSide.Friendly);
        leader.Position = new Vector2(0, 12); Enemy(sim).Firing = FiringMode.HoldFire;
        sim.RefreshDetection(); sim.SetPriority([shooter.Id], Enemy(sim).Id); sim.Follow([shooter.Id], leader.Id);
        sim.Paused = false; sim.Advance(.1);
        check(shooter.ShotsFired > 0 && shooter.Order == CombatOrder.Follow && shooter.FollowTarget == leader.Id
            && shooter.PriorityTarget == Enemy(sim).Id, "Follow fires opportunistically while retaining its leader and priority target");
    }
}
