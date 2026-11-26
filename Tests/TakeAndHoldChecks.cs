using Godot;

namespace Mech.Tests;

/// <summary>Take and Hold battles and the control zones they use: control, the objective's layout, the garrison and its waves, leading
/// the advance, the outcome and replication. Engine-free.</summary>
internal static class TakeAndHoldChecks
{
    private static ForceRoster Company(int count, string mech = "wolverine")
    {
        var roster = ForceRoster.Create("Hold checks", Enumerable.Repeat(DraftCatalog.Mech(mech), count), TestPilots.Warriors.Take(count));
        for (var i = 0; i < count; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        return roster;
    }

    /// <summary>A garrison of two, then one mech in each of <paramref name="waves"/> waves.</summary>
    internal static MissionOffer Offer(int waves = 2)
    {
        EnemyMech[] opposition = [new("locust", EnemyPilotRank.Corporal, Wave: 0), new("jenner", EnemyPilotRank.Corporal, Wave: 0),
            .. Enumerable.Range(1, waves).Select(wave => new EnemyMech("commando", EnemyPilotRank.Corporal, Wave: wave))];
        return new("offer-1", "Take and Hold at Checks", "Briefing.", MissionTuning.TakeAndHoldObjective(waves), StarChart.Instance.StartingSystemId,
            Campaign.StartDate, opposition, 100_000, 0, 0, Party.Of(Faction.Steiner), Party.Pirates, MissionKind.TakeAndHold);
    }

    private static CombatSimulation Hold(int waves = 2, int company = 2, int terrainSeed = 4242, int seed = 7, Biome biome = Biome.Grassland)
    {
        var roster = Company(company);
        return CombatSimulation.DeployMission(roster, roster.Mechs.Select(mech => mech.Id), Offer(waves), seed, terrainSeed, biome);
    }

    public static void Run(Action<bool, string> check)
    {
        CheckControl(check);
        CheckLayout(check);
        CheckCapture(check);
        CheckWaves(check);
        CheckOutcomes(check);
        CheckLeadingTheAdvance(check);
        CheckAi(check);
        CheckReplica(check);
    }

    private static CombatUnit[] Side(CombatSimulation sim, CombatSide side) => sim.Units.Where(unit => unit.Side == side && !unit.Inbound).ToArray();
    private static void Destroy(CombatUnit unit) => unit.ApplyDamage(MechLocation.CenterTorso, 10_000, unit.Position);
    private static Vector2 Spot(CombatSimulation sim, float distance, float angle = 0) =>
        sim.Objective!.Center + Vector2.FromAngle(angle) * distance;

    private static void CheckControl(Action<bool, string> check)
    {
        var sim = Hold(company: 3); sim.EnemyAiEnabled = false;
        var friends = Side(sim, CombatSide.Friendly); var garrison = Side(sim, CombatSide.Enemy);
        var zone = new ControlZone("TEST", sim.Objective!.Center, CombatRules.ControlZoneRadius, CombatRules.CaptureSeconds, CombatSide.Enemy);
        foreach (var unit in sim.Units) unit.Position = zone.Center + new Vector2(2000, 0);
        check(zone.Advance(sim.Units, 10) == ControlZoneChange.None && zone.Control == 0 && zone.Owner == CombatSide.Enemy && !zone.Contested,
            "a zone with nobody inside keeps its control");
        friends[0].Position = Spot(sim, 100);
        check(zone.Advance(sim.Units, 1) == ControlZoneChange.Contested && Math.Abs(zone.Control - 1 / 30.0) < 1e-9 && zone.Contested && zone.Owner == CombatSide.Enemy,
            "one company mech leading the zone moves control a thirtieth a second, contesting it");
        friends[1].Position = Spot(sim, 150, 1); garrison[0].Position = Spot(sim, 200, 2); friends[2].Position = Spot(sim, 300, 3);
        zone.Advance(sim.Units, 3);
        check(Math.Abs(zone.Control - 7 / 30.0) < 1e-9, "two company mechs ahead of the nearest enemy move control twice as fast; the third behind it adds nothing");
        garrison[1].Position = Spot(sim, 50, 4);
        zone.Advance(sim.Units, 1);
        check(Math.Abs(zone.Control - 6 / 30.0) < 1e-9, "the nearest enemy to the centre leads, moving control back toward the opposition");
        garrison[1].Position = Spot(sim, CombatRules.ControlZoneRadius + 10, 4);
        friends[2].Position = Spot(sim, 120, 3); garrison[0].Position = zone.Center + new Vector2(2000, 0);
        check(zone.Leaders(sim.Units) == (CombatSide.Friendly, 3), "units outside the zone do not count");
        Destroy(friends[2]);
        check(zone.Leaders(sim.Units) == (CombatSide.Friendly, 2), "a defeated unit no longer counts");
        var change = ControlZoneChange.None;
        for (var i = 0; i < 400 && change == ControlZoneChange.None; i++) change = zone.Advance(sim.Units, CombatRules.StepSeconds);
        check(change == ControlZoneChange.OwnerChanged && zone.Control == 1 && zone.Owner == CombatSide.Friendly && !zone.Contested,
            "the zone changes hands when control reaches the far end");
        foreach (var friend in friends) friend.Position = zone.Center + new Vector2(2000, 0);
        garrison[0].Position = Spot(sim, 10);
        check(zone.Advance(sim.Units, 15) == ControlZoneChange.Contested && Math.Abs(zone.Control - .5) < 1e-9 && zone.Owner == CombatSide.Friendly,
            "a company zone being drained is contested, but stays the company's until control reaches nothing");
    }

    private static void CheckLayout(Action<bool, string> check)
    {
        var sim = Hold();
        var zone = sim.Objective!; var layout = sim.TakeAndHold!.Layout;
        var settlement = sim.Terrain.Buildings.Where(building => building.Cluster == 0).ToArray();
        check(sim.ControlZones.Count == 1 && zone.Center == layout.Objective && zone.Radius == CombatRules.ControlZoneRadius
            && zone.Owner == CombatSide.Enemy && zone.Control == 0, "a Take and Hold has one control zone, held by the opposition, centred on the objective");
        var placements = new[] { 4242, 7, 99, 31337, 2025 }.SelectMany(seed => new[] { Biome.Grassland, Biome.Highlands, Biome.Arctic }.Select(biome => (seed, biome)))
            .Select(item => TakeAndHoldLayout.Create(item.seed, item.biome)).ToArray();
        check(placements.All(item => item.Layout.Objective.Length() <= CombatRules.ObjectiveOffset + 1
                && item.Layout.Buildings is >= CombatRules.MinObjectiveBuildings and <= CombatRules.MaxObjectiveBuildings
                && item.Terrain.Buildings.Count(building => building.Cluster == 0) == item.Layout.Buildings
                && item.Terrain.Buildings.Where(building => building.Cluster == 0).All(building => building.Center.DistanceTo(item.Layout.Objective)
                    + building.HalfSize.Length() <= CombatRules.ControlZoneRadius)
                && item.Layout.DropEdge == DropZone.StandardEdge(item.Terrain.Seed)),
            "the objective settlement of 15–25 buildings lies within 800 m of the arena centre, every building inside its zone");
        check(placements.All(item => DropZone.Standard(item.Layout.DropEdge).Depth(item.Layout.Objective) - CombatRules.ControlZoneRadius >= CombatRules.DropZoneDepth),
            "the objective's zone keeps out of the company's drop zone");
        var again = TakeAndHoldLayout.Create(4242, Biome.Grassland);
        check(again.Layout == layout && again.Terrain.Buildings.Count == sim.Terrain.Buildings.Count, "the layout follows from the terrain seed");
        var garrison = sim.Units.Where(unit => unit.Wave == 0).ToArray();
        var component = sim.Terrain.Component(sim.FriendlyZone!.Anchor);
        check(garrison.Length == 2 && garrison.All(unit => unit.Active && zone.Contains(unit.Position) && sim.Terrain.Passable(unit.Position)
                && sim.Terrain.Component(unit.Position) == component && unit.Post == unit.Position)
            && garrison[0].Position.DistanceTo(garrison[1].Position) >= CombatRules.GarrisonSpacing,
            "the garrison stands at posts inside the zone, apart, on ground connected to the company's");
        var waves = sim.Units.Where(unit => unit.Wave > 0).ToArray();
        check(waves.Length == 2 && waves.All(unit => unit.Inbound && unit.Status == CombatStatus.Inbound && unit.Defeated && !unit.Solid && unit.OffField
                && !unit.Fighting && !sim.Detected(CombatSide.Friendly, unit.Id)),
            "the waves wait off the field, out of the fight and unseen");
        check(sim.EnemyZone is null && sim.FriendlyZone!.Edge == layout.DropEdge, "the company drops on its edge and the opposition has no drop zone");
        check(sim.TakeAndHoldObjectiveText() == "Take the objective" && sim.ReinforcementText() == "Reinforcements: 2 waves once the objective is taken",
            "before the capture the objective reads Take the objective, with the waves to come");
        var single = Company(1);
        var standard = CombatSimulation.DeployMission(single, [single.Mechs[0].Id], CombatChecksOffer(), 1, 4242, Biome.Grassland);
        check(standard.ControlZones.Count == 0 && standard.TakeAndHold is null, "other battles have no control zones");
    }

    private static MissionOffer CombatChecksOffer() => new("offer-1", "Raid on Checks", "Briefing.", MissionTuning.RaidObjective, StarChart.Instance.StartingSystemId,
        Campaign.StartDate, [new("locust", EnemyPilotRank.Corporal)], 100_000, 0, 0, Party.Pirates, new(PartyKind.LocalForces, null, "Checks Guard"), MissionKind.Raid);

    /// <summary>Puts the company at the centre with the garrison destroyed and runs until the capture.</summary>
    private static CombatSimulation Captured(int waves = 2, int company = 1)
    {
        var sim = Hold(waves, company); sim.EnemyAiEnabled = false; sim.Paused = false;
        foreach (var unit in sim.Units.Where(unit => unit.Wave == 0)) Destroy(unit);
        var friends = Side(sim, CombatSide.Friendly);
        for (var i = 0; i < friends.Length; i++) friends[i].Position = sim.Terrain.ReachableDestination(sim.FriendlyZone!.Anchor, Spot(sim, 60, i));
        for (var i = 0; i < 2000 && !sim.TakeAndHold!.Taken; i++) sim.Advance(CombatRules.StepSeconds);
        return sim;
    }

    private static void CheckCapture(Action<bool, string> check)
    {
        var sim = Captured();
        var hold = sim.TakeAndHold!;
        check(hold.Taken && Math.Abs(hold.TakenAt!.Value - CombatRules.CaptureSeconds) < .2 && sim.Objective!.Owner == CombatSide.Friendly,
            $"one company mech takes the objective in 30 seconds (took {hold.TakenAt:0.00} s)");
        check(hold.Events.Any(item => item.Text == "Objective contested") && hold.Events.Any(item => item.Text == "Objective taken")
            && sim.Highlights.Any(item => item.Text.EndsWith("took the objective.", StringComparison.Ordinal)),
            "the log and the highlights record the capture");
        check(sim.TakeAndHoldObjectiveText() == "Hold the objective" && sim.Outcome == CombatOutcome.Ongoing, "after the capture the objective reads Hold the objective");
        var pair = Captured(company: 2);
        check(Math.Abs(pair.TakeAndHold!.TakenAt!.Value - CombatRules.CaptureSeconds / 2) < .2, $"two company mechs take it twice as fast ({pair.TakeAndHold.TakenAt:0.00} s)");

        var early = Hold(); early.EnemyAiEnabled = false; early.Paused = false;
        foreach (var unit in early.Units.Where(unit => unit.Wave == 0)) Destroy(unit);
        early.Advance(60);
        check(early.TakeAndHold!.Arrived == 0 && early.TakeAndHold.NextWaveAt is null && early.Outcome == CombatOutcome.Ongoing,
            "destroying the garrison brings no wave until the objective is taken");
    }

    private static void CheckWaves(Action<bool, string> check)
    {
        var sim = Captured(waves: 3);
        var hold = sim.TakeAndHold!; var zone = sim.Objective!;
        var taken = hold.TakenAt!.Value;
        check(hold.NextWaveAt is { } first && Math.Abs(first - (taken + CombatRules.WaveClearSeconds)) < .1,
            "with no enemy fighting, the first wave is due five seconds after the capture");
        for (var i = 0; i < 400 && hold.Arrived == 0; i++) sim.Advance(CombatRules.StepSeconds);
        var wave = sim.Units.Where(unit => unit.Wave == 1).ToArray();
        var distance = wave[0].Position.DistanceTo(zone.Center);
        check(hold.Arrived == 1 && Math.Abs(sim.Elapsed - (taken + CombatRules.WaveClearSeconds)) < .1 && wave.All(unit => !unit.Inbound && unit.Active && unit.Solid)
            && Math.Abs(distance - (zone.Radius + CombatRules.WaveSpawnDistance)) <= CombatRules.WaveLanceSpread + CombatRules.DropSiteRadius
            && wave.All(unit => Math.Max(Math.Abs(unit.Position.X), Math.Abs(unit.Position.Y)) <= CombatRules.ArenaHalfSize - CombatRules.WaveSpawnPadding + CombatRules.DropSiteRadius
                && sim.Terrain.Passable(unit.Position))
            && wave.All(unit => unit.Facing.Dot((zone.Center - unit.Position).Normalized()) > .99f),
            $"a wave arrives about 1,500 m beyond the zone's edge, inside the arena, facing the objective ({distance:0} m from the centre)");
        check(hold.Events.Last().Text.StartsWith("Reinforcement wave 1 of 3 arriving from the ", StringComparison.Ordinal)
            && sim.Highlights.Last().Text.StartsWith("Reinforcement wave 1 of 3 arrived from the ", StringComparison.Ordinal)
            && sim.Highlights.Last().UnitId == wave[0].Id, "the log and the highlights name the wave and its bearing");
        check(sim.ReinforcementText() == "Reinforcements: wave 2 of 3 in 3:00", $"the next wave counts down from three minutes ({sim.ReinforcementText()})");
        var arrived = sim.Elapsed;
        for (var i = 0; i < 4000 && hold.Arrived == 1; i++) sim.Advance(CombatRules.StepSeconds);
        check(hold.Arrived == 2 && Math.Abs(sim.Elapsed - (arrived + CombatRules.WaveIntervalSeconds)) < .1,
            $"while the last wave still fights, the next arrives three minutes after it ({sim.Elapsed - arrived:0.0} s)");
        foreach (var unit in sim.Units.Where(unit => unit.Side == CombatSide.Enemy && !unit.Inbound)) Destroy(unit);
        var cleared = sim.Elapsed;
        for (var i = 0; i < 400 && hold.Arrived == 2; i++) sim.Advance(CombatRules.StepSeconds);
        check(hold.Arrived == 3 && sim.Elapsed - cleared is > 4.9 and < 5.2, $"five seconds after no enemy is fighting the next wave arrives ({sim.Elapsed - cleared:0.00} s)");
        check(sim.TakeAndHoldObjectiveText() == "Destroy the remaining enemies" && sim.ReinforcementText() == "Reinforcements: all 3 waves have arrived",
            "once every wave has arrived the objective reads Destroy the remaining enemies");
        check(CombatSimulation.Bearing(Vector2.Zero, new Vector2(0, -10)) == "north" && CombatSimulation.Bearing(Vector2.Zero, new Vector2(10, 10)) == "south-east"
            && CombatSimulation.Bearing(Vector2.Zero, new Vector2(-10, 0)) == "west", "bearings read north as −Y and east as +X");
    }

    private static void CheckOutcomes(Action<bool, string> check)
    {
        var sim = Captured(waves: 1);
        var hold = sim.TakeAndHold!;
        for (var i = 0; i < 400 && hold.Arrived == 0; i++) sim.Advance(CombatRules.StepSeconds);
        check(sim.Outcome == CombatOutcome.Ongoing, "the battle goes on while the last wave fights");
        foreach (var unit in sim.Units.Where(unit => unit.Wave == 1)) Destroy(unit);
        sim.Advance(CombatRules.WindDownSeconds + .2);
        check(sim.Outcome == CombatOutcome.Victory && sim.MissionReport().Outcome == MissionOutcome.Success && sim.TakeAndHoldResultText() == "The objective was held.",
            "with every wave arrived and destroyed and the objective held, the mission succeeds");

        var retake = Captured(waves: 2);
        for (var i = 0; i < 400 && retake.TakeAndHold!.Arrived == 0; i++) retake.Advance(CombatRules.StepSeconds);
        var raider = retake.Units.Single(unit => unit.Wave == 1);
        // Neither side fires, so only control decides this.
        foreach (var unit in retake.Units) unit.Firing = FiringMode.HoldFire;
        raider.Position = retake.Terrain.ReachableDestination(retake.FriendlyZone!.Anchor, Spot(retake, 20, 2));
        foreach (var friend in Side(retake, CombatSide.Friendly)) friend.Position = retake.Terrain.ReachableDestination(retake.FriendlyZone.Anchor, Spot(retake, 300, 4));
        for (var i = 0; i < 2000 && retake.Outcome == CombatOutcome.Ongoing; i++) retake.Advance(CombatRules.StepSeconds);
        check(retake.TakeAndHold!.Retaken && retake.Outcome == CombatOutcome.Defeat && retake.MissionReport().Outcome == MissionOutcome.Failure
            && retake.TakeAndHold.Events.Any(item => item.Text == "Objective retaken by the enemy") && retake.TakeAndHoldResultText() == "The objective was retaken.",
            "the enemy retaking the objective fails the mission");
        check(retake.MissionReport().Enemies.Count(item => item.Status == CombatStatus.Inbound) == 1 && retake.MissionReport().EnemyWarriorsSpared == 3
            && BattleSummary.Text(BattleSummary.Pilot(retake.Units.Single(unit => unit.Wave == 2))) == "Not engaged"
            && Salvage.Pool(retake.MissionReport() with { Outcome = MissionOutcome.Success }).All(option => option.UnitId != retake.Units.Single(unit => unit.Wave == 2).Id),
            "a wave that never arrived is reported Not engaged, and cannot be salvaged");

        var beaten = Hold(); beaten.EnemyAiEnabled = false; beaten.Paused = false;
        foreach (var friend in Side(beaten, CombatSide.Friendly)) Destroy(friend);
        beaten.Advance(CombatRules.WindDownSeconds + .2);
        check(beaten.Outcome == CombatOutcome.Defeat, "a company that is beaten loses the Take and Hold");
    }

    private static void CheckLeadingTheAdvance(Action<bool, string> check)
    {
        var sim = Hold(company: 2); sim.EnemyAiEnabled = false; sim.Paused = false;
        foreach (var unit in sim.Units.Where(unit => unit.Wave == 0)) Destroy(unit);
        var (lead, wing) = (Side(sim, CombatSide.Friendly)[0], Side(sim, CombatSide.Friendly)[1]);
        var anchor = sim.FriendlyZone!.Anchor;
        lead.Position = sim.Terrain.ReachableDestination(anchor, Spot(sim, 400));
        sim.Advance(1);
        wing.Position = sim.Terrain.ReachableDestination(anchor, Spot(sim, 40, 2));
        for (var i = 0; i < 2000 && !sim.TakeAndHold!.Taken; i++) sim.Advance(CombatRules.StepSeconds);
        check(lead.Use.LedAdvanceAt is { } led && Math.Abs(led - sim.TakeAndHold!.TakenAt!.Value) < 1e-6 && wing.Use.LedAdvanceAt is null
            && sim.Highlights.Any(item => item.UnitId == lead.Id && item.Text.EndsWith("led the advance and held the objective.", StringComparison.Ordinal)),
            "the first company mech in, there until the capture, leads the advance even when another stood nearer the centre");
        sim.End();
        check(sim.MissionReport().Mechs.Single(item => item.MechId == lead.Id).LedAdvanceAt == lead.Use.LedAdvanceAt
            && sim.MissionReport().Mechs.Single(item => item.MechId == wing.Id).LedAdvanceAt is null, "the mission report carries who led the advance, and when");
        check(SkillGrowth.ExtraChecks(lead.Use, 0, 0, false)[(int)WarriorSkill.Piloting] == 1 && SkillGrowth.ExtraChecks(wing.Use, 0, 0, false)[(int)WarriorSkill.Piloting] == 0,
            "leading the advance adds one Piloting skill gain check");

        var left = Hold(company: 2); left.EnemyAiEnabled = false; left.Paused = false;
        foreach (var unit in left.Units.Where(unit => unit.Wave == 0)) Destroy(unit);
        var (first, second) = (Side(left, CombatSide.Friendly)[0], Side(left, CombatSide.Friendly)[1]);
        first.Position = left.Terrain.ReachableDestination(anchor, Spot(left, 100));
        left.Advance(1);
        second.Position = left.Terrain.ReachableDestination(anchor, Spot(left, 100, 3));
        left.Advance(1);
        first.Position = left.Terrain.ReachableDestination(anchor, Spot(left, CombatRules.ControlZoneRadius + 200));
        for (var i = 0; i < 2000 && !left.TakeAndHold!.Taken; i++) left.Advance(CombatRules.StepSeconds);
        check(left.TakeAndHold!.Taken && first.Use.LedAdvanceAt is null && second.Use.LedAdvanceAt is null,
            "a first mech that leaves the zone before the capture loses the bonus, and nobody else takes it");
    }

    private static void CheckAi(Action<bool, string> check)
    {
        var sim = Captured(waves: 1);
        for (var i = 0; i < 400 && sim.TakeAndHold!.Arrived == 0; i++) sim.Advance(CombatRules.StepSeconds);
        var raider = sim.Units.Single(unit => unit.Wave == 1);
        var start = raider.Position.DistanceTo(sim.Objective!.Center);
        sim.EnemyAiEnabled = true;
        sim.Advance(60);
        check(raider.Position.DistanceTo(sim.Objective.Center) < start - 300, $"a wave heads for the objective ({start:0} m, then {raider.Position.DistanceTo(sim.Objective.Center):0} m)");

        var guard = Hold(); guard.Paused = false;
        var post = guard.Units.First(unit => unit.Wave == 0);
        var wanderer = guard.Units.Last(unit => unit.Wave == 0); Destroy(wanderer);
        post.Position = guard.Terrain.ReachableDestination(guard.FriendlyZone!.Anchor, Spot(guard, CombatRules.ControlZoneRadius + CombatRules.GarrisonLeash + 200));
        guard.Advance(1);
        check(post.Order == CombatOrder.Move && post.Destination.DistanceTo(post.Post) < 60, "a garrison mech beyond its leash returns to its post");
    }

    private static void CheckReplica(Action<bool, string> check)
    {
        var roster = Company(1); var force = roster.Mechs.Select(mech => mech.Id).ToArray();
        var leader = CombatSimulation.DeployMission(roster, force, Offer(2), 3, 808, Biome.Grassland);
        var replica = CombatSimulation.DeployMission(roster, force, Offer(2), 3, 808, Biome.Grassland); replica.BecomeReplica();
        leader.EnemyAiEnabled = false; leader.Paused = false;
        foreach (var unit in leader.Units.Where(unit => unit.Wave == 0)) Destroy(unit);
        var friend = leader.Units.First(unit => unit.Side == CombatSide.Friendly);
        friend.Position = leader.Terrain.ReachableDestination(leader.FriendlyZone!.Anchor, leader.Objective!.Center + new Vector2(40, 0));
        for (var i = 0; i < 2000 && leader.TakeAndHold!.Arrived == 0; i++) leader.Advance(CombatRules.StepSeconds);
        var state = ((BattleStateMessage)NetProtocol.Decode(NetProtocol.Encode(new BattleStateMessage(leader.State())))!).State;
        replica.Apply(state); replica.Advance(1);
        var arrived = replica.Units.Single(unit => unit.Wave == 1);
        check(replica.TakeAndHold!.Taken && replica.TakeAndHold.Arrived == 1 && replica.Objective!.Owner == CombatSide.Friendly && replica.Objective.Control == 1
            && !arrived.Inbound && arrived.Position == leader.Units.Single(unit => unit.Wave == 1).Position && replica.Units.Single(unit => unit.Wave == 2).Inbound
            && replica.TakeAndHold.Events.Count == leader.TakeAndHold!.Events.Count && replica.ReinforcementText() == leader.ReinforcementText()
            && replica.Units.First(unit => unit.Side == CombatSide.Friendly).Use.LedAdvanceAt == friend.Use.LedAdvanceAt,
            "a co-op replica takes on the objective's control, the capture, arrived waves, the countdown and the log");
    }
}
