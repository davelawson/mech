using Godot;

namespace Mech.Tests;

/// <summary>Co-op sessions over an in-memory transport: seating, drafts, commands and disconnection, without the engine.</summary>
internal static class NetworkChecks
{
    /// <summary>A leader and its clients; the leader numbers clients from 2, as ENet does, and is each client's peer 1.</summary>
    internal sealed class MemoryNetwork
    {
        private readonly Dictionary<int, Endpoint> _clients = [];
        public Endpoint Leader { get; }
        public MemoryNetwork() { Leader = new Endpoint(this, 0); }
        public Endpoint Connect()
        {
            var client = new Endpoint(this, _clients.Count + 2); _clients[client.Id] = client;
            Leader.Queue(new(NetEventKind.Connected, client.Id)); client.Queue(new(NetEventKind.Connected, CoopClient.LeaderPeer));
            return client;
        }
        public void Drop(Endpoint client)
        {
            if (!_clients.Remove(client.Id)) return;
            Leader.Queue(new(NetEventKind.Disconnected, client.Id)); client.Queue(new(NetEventKind.Disconnected, CoopClient.LeaderPeer));
        }
        internal void Deliver(Endpoint from, int peer, byte[] data)
        {
            if (from == Leader) { if (_clients.TryGetValue(peer, out var client)) client.Queue(new(NetEventKind.Data, CoopClient.LeaderPeer, data)); }
            else if (_clients.ContainsKey(from.Id)) Leader.Queue(new(NetEventKind.Data, from.Id, data));
        }
        internal sealed class Endpoint(MemoryNetwork network, int id) : INetTransport
        {
            private readonly List<NetEvent> _inbox = [];
            public int Id => id;
            public int Sent { get; private set; }
            public void Queue(NetEvent item) => _inbox.Add(item);
            public IReadOnlyList<NetEvent> Poll() { var items = _inbox.ToArray(); _inbox.Clear(); return items; }
            public void Send(int peer, byte[] data) { Sent++; network.Deliver(this, peer, data); }
            public void Dispose() { if (this != network.Leader) network.Drop(this); }
        }
    }

    /// <summary>Every part in turn. The unit tests run the parts separately, in parallel.</summary>
    public static void Run(Action<bool, string> check)
    {
        RunSessions(check);
        RunReplica(check);
    }
    /// <summary>Protocol, sessions, resuming, co-op battles and the host address.</summary>
    public static void RunSessions(Action<bool, string> check)
    {
        CheckProtocol(check);
        CheckSession(check);
        CheckResume(check);
        CheckCombatProtocol(check);
        CheckBattle(check);
        CheckHostAddress(check);
    }
    /// <summary>A whole battle replayed on a replica and matched field by field, the longest single check.</summary>
    public static void RunReplica(Action<bool, string> check) => CheckReplica(check);

    private static void CheckHostAddress(Action<bool, string> check)
    {
        check(HostAddress.Text("203.0.113.7", ["192.168.1.20"], 24600, false, RouterPortState.Opened)
            == "Internet: 203.0.113.7:24600 · Local network: 192.168.1.20:24600\nUDP port 24600 is open on your router.",
            "the leader's addresses are shown with the port");
        check(HostAddress.Text(null, [], 24525, true, RouterPortState.NotHosting).StartsWith("Internet address: asking your router…"), "the address reads as asked while the router answers");
        check(HostAddress.Text(null, [], 24525, false, RouterPortState.NotHosting).Contains("your router did not say"), "a router that does not answer is explained");
        check(HostAddress.Text(null, [], 24525, false, RouterPortState.NotHosting).EndsWith("UDP port 24525 is opened on your router when you host, if it supports UPnP.")
            && HostAddress.Text(null, [], 24525, false, RouterPortState.Opening).EndsWith("Opening UDP port 24525 on your router…")
            && HostAddress.Text(null, [], 24525, false, RouterPortState.Failed).EndsWith("Forward it on the router for internet play."),
            "the hosting screens say whether the router forwards the port");
        check(new[] { "10.0.0.4", "172.16.2.1", "172.31.255.1", "192.168.0.9" }.All(HostAddress.IsPrivate)
            && !new[] { "127.0.0.1", "172.32.0.1", "8.8.8.8", "fe80::1", "nonsense" }.Any(HostAddress.IsPrivate), "only private IPv4 addresses are local-network addresses");
    }

    private static void CheckProtocol(Action<bool, string> check)
    {
        var plan = LoadoutPlan.Removing(["weapon-1"]) with { Armor = [new(MechLocation.Head, 10, 0)] };
        var commands = new CampaignCommand[]
        {
            new AcceptMissionCommand("m-1"), new AbandonMissionCommand(), new PlotJourneyCommand("terra"), new EmbarkCommand(), new CancelJourneyCommand(),
            new AdvanceDayCommand(), new PayNowCommand(), new DeferPaymentCommand(), new ApplyLoadoutCommand("mech-1", plan, ["weapon:medium-laser"]),
            new AssignWarriorCommand("mech-1", null, true), new SetLanceCommand("mech-2", 1),
            new SettleMissionCommand(new(MissionOutcome.Success, [new("mech-1", [new(MechLocation.Head, 1, 0, 2)], [new("x", ComponentCondition.Damaged)], false, 3)], 2))
        };
        foreach (var command in commands)
        {
            var decoded = NetProtocol.Decode(NetProtocol.Encode(new CommandMessage(command))) as CommandMessage;
            check(decoded?.Command.GetType() == command.GetType() && System.Text.Json.JsonSerializer.Serialize<object>(decoded.Command) == System.Text.Json.JsonSerializer.Serialize<object>(command),
                $"{command.GetType().Name} survives encoding");
        }
        var loadout = (ApplyLoadoutCommand)((CommandMessage)NetProtocol.Decode(NetProtocol.Encode(new CommandMessage(commands[8])))!).Command;
        check(loadout.Plan.SameAs(plan) && loadout.Plan.Armor.SequenceEqual(plan.Armor), "a loadout plan survives encoding");
        var snapshot = CampaignChecks.NewCompany(5).Snapshot();
        var state = NetProtocol.Decode(NetProtocol.Encode(new CampaignMessage(snapshot))) as CampaignMessage;
        check(state is not null && System.Text.Json.JsonSerializer.Serialize(state.Snapshot) == System.Text.Json.JsonSerializer.Serialize(snapshot), "a campaign snapshot survives encoding");
        check(NetProtocol.Encode(new CampaignMessage(snapshot)).Length < System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(snapshot).Length / 4, "campaign snapshots are compressed");
        check(NetProtocol.Decode([1, 2, 3]) is null && NetProtocol.Decode(System.Text.Encoding.UTF8.GetBytes("{}")) is null, "malformed packets are ignored");
    }

    private static void CheckSession(Action<bool, string> check)
    {
        var network = new MemoryNetwork();
        using var host = new CoopHost(network.Leader, "Dave", 2);
        var stranger = network.Connect();
        stranger.Send(CoopClient.LeaderPeer, NetProtocol.Encode(new HelloMessage(NetProtocol.Version, "another-build", "Old build")));
        var sam = new CoopClient(network.Connect(), "Sam");
        void Pump() { for (var i = 0; i < 4; i++) { host.Update(CoopHost.SyncInterval); sam.Update(); } }
        Pump();
        check(stranger.Poll().Select(item => item.Data is null ? null : NetProtocol.Decode(item.Data)).OfType<RefusedMessage>().SingleOrDefault()?.Reason
            == "The leader is running a different build of the game.", "a player on another build is refused");
        check(sam.Connected && sam.Slot is null && sam.Seats.Count == 2 && sam.Seats[0] is { Name: "Dave", Present: true } && !sam.Seats[1].Present,
            "a joining player sees the lobby before taking a seat");
        check(!host.CanStart, "the leader cannot start with an empty seat");
        sam.Claim(0); Pump();
        check(sam.Slot is null, "the leader's seat cannot be claimed");
        sam.Claim(1); Pump();
        check(sam.Slot == 1 && host.Seats[1] is { Name: "Sam", Present: true } && host.CanStart, "a player claims a free seat");
        var late = new CoopClient(network.Connect(), "Late"); string? full = null; late.Ended += reason => full = reason;
        Pump(); late.Update(); host.Update(0); late.Update();
        check(full == "Every seat in this campaign is taken." && late.Closed, "a player joining a full lobby is refused");

        host.Start(new Random(11)); Pump();
        check(host.Phase == LobbyPhase.Drafting && sam.Phase == LobbyPhase.Drafting && sam.Draft is { NamesCompany: false } && host.LeaderDraft is { NamesCompany: true },
            "each player receives their own draft; only the leader's names the company");
        var leaderWarriors = Enumerable.Range(4, 4).SelectMany(p => Enumerable.Range(0, 3).Select(c => host.LeaderDraft!.WarriorCandidate(p, c).Id)).ToHashSet();
        check(Enumerable.Range(4, 4).SelectMany(p => Enumerable.Range(0, 3).Select(c => sam.Draft!.WarriorCandidate(p, c).Id)).All(id => !leaderWarriors.Contains(id)),
            "no MechWarrior candidate appears in two players' drafts");
        sam.SubmitDraft(); Pump();
        check(host.Seats[1].Drafted && host.Campaign is null && sam.Campaign is null, "the company waits for every draft");
        host.LeaderDraft!.ForceName = "Co-op Company"; host.SubmitLeaderDraft(); Pump();
        var campaign = host.Campaign!;
        check(campaign.Players == 2 && campaign.PlayerNames!.SequenceEqual(["Dave", "Sam"]) && campaign.Roster!.ForceName == "Co-op Company"
            && campaign.Roster.LanceMechs(0).Count() == 4 && campaign.Roster.LanceMechs(1).Count() == 4 && campaign.Roster.Mechs.All(mech => mech.WarriorId is not null),
            "the drafts form a company of one lance per player");
        check(sam.Phase == LobbyPhase.Playing && sam.Campaign is not null && Same(sam.Campaign, campaign), "each player receives the campaign");

        string? refused = null; sam.CommandRefused += reason => refused = reason;
        sam.Send(new AdvanceDayCommand()); Pump();
        check(refused == "Only the leader can do that." && campaign.Date == Campaign.StartDate, "the leader refuses another player's leader-only command");
        var leaderMech = campaign.Roster!.LanceMechs(0).First(); var samMech = campaign.Roster.LanceMechs(1).First();
        refused = null; sam.Send(new SetLanceCommand(leaderMech.Id, null)); Pump();
        check(refused is not null && leaderMech.Lance == 0, "the leader refuses changes to another player's lance");
        sam.Send(new SetLanceCommand(samMech.Id, null)); Pump();
        check(samMech.Lance is null && sam.Campaign!.Roster!.Mechs.Single(mech => mech.Id == samMech.Id).Lance is null, "a player's command changes the campaign everyone sees");

        campaign.Execute(new AdvanceDayCommand()); host.MarkDirty(); host.Update(CoopHost.SyncInterval);
        var sent = network.Leader.Sent;
        campaign.Execute(new AdvanceDayCommand()); host.MarkDirty(); host.Update(0);
        check(network.Leader.Sent == sent, "campaign changes are held back until the sync interval passes");
        host.Update(CoopHost.SyncInterval); sam.Update();
        check(network.Leader.Sent == sent + 1 && sam.Campaign!.Date == campaign.Date, "changes reach the players once the sync interval passes");

        string? left = null; host.PlayerLeft += name => left = name;
        sam.Dispose(); host.Update(0);
        check(left == "Sam" && !host.AllPresent && host.Seats[1] is { Name: "Sam", Present: false }, "the leader learns that a player disconnected");
        var backEnd = network.Connect(); var back = new CoopClient(backEnd, "Sam");
        for (var i = 0; i < 3; i++) { host.Update(0); back.Update(); }
        back.Claim(1); for (var i = 0; i < 3; i++) { host.Update(0); back.Update(); }
        check(host.AllPresent && back.Slot == 1 && back.Campaign is not null && Same(back.Campaign, campaign), "a returning player retakes the empty seat and receives the campaign");
        string? ended = null; back.Ended += reason => ended = reason;
        network.Drop(backEnd);
        back.Update();
        check(ended == "The connection to the leader was lost." && back.Closed && back.LostConnection, "a player is told when the connection to the leader is lost");
    }

    private static void CheckResume(Action<bool, string> check)
    {
        var solo = CampaignChecks.NewCompany();
        var rejected = false;
        try { _ = new CoopHost(new MemoryNetwork().Leader, "Dave", 2, solo); } catch (ArgumentException) { rejected = true; }
        check(rejected, "a single-player campaign cannot be hosted as co-op");
        check(InitialDraft.MaxPlayers == 2 && InitialDraft.CompanyProblem(3) is { } problem && problem.Contains("27 regular and 9 veteran"),
            "three-player companies wait for enough MechWarriors in the catalog");

        var network = new MemoryNetwork();
        var setup = new CoopHost(network.Leader, "Dave", 2); var sam = new CoopClient(network.Connect(), "Sam");
        for (var i = 0; i < 3; i++) { setup.Update(0); sam.Update(); }
        sam.Claim(1); for (var i = 0; i < 3; i++) { setup.Update(0); sam.Update(); }
        setup.Start(new Random(3)); sam.Update(); sam.SubmitDraft(); setup.LeaderDraft!.ForceName = "Saved Company"; setup.Update(0); setup.SubmitLeaderDraft();
        var saved = Campaign.Restore(setup.Campaign!.Snapshot());
        check(saved.PlayerNames!.SequenceEqual(["Dave", "Sam"]), "a co-op save keeps its players' names");

        var second = new MemoryNetwork();
        using var host = new CoopHost(second.Leader, "Dave", 3, saved);
        check(host.Players == 2 && host.Phase == LobbyPhase.Gathering && host.Seats[1] is { Name: "Sam", Present: false }, "a resumed lobby uses the saved player count and names");
        var alex = new CoopClient(second.Connect(), "Alex");
        for (var i = 0; i < 3; i++) { host.Update(0); alex.Update(); }
        alex.Claim(1); for (var i = 0; i < 3; i++) { host.Update(0); alex.Update(); }
        host.Start(); alex.Update();
        check(host.Phase == LobbyPhase.Playing && alex.Campaign is not null && saved.PlayerNames!.SequenceEqual(["Dave", "Alex"]),
            "any player may take a saved seat, and the campaign records who resumed it");
    }

    private static void CheckCombatProtocol(Action<bool, string> check)
    {
        var commands = new CombatCommand[]
        {
            new MoveCommand(["a", "b"], new Vector2(1.5f, -2.25f)), new AttackCommand(["a"], "enemy-1"), new FollowCommand(["a"], "b"),
            new JumpCommand(["a"], new Vector2(3, 4)), new HoldCommand(["a"]), new PriorityCommand(["a"], null), new MovementCommand(["a"], MovementMode.Run),
            new FiringCommand(["a"], FiringMode.HoldFire), new HeatPolicyCommand(["a"], HeatPolicy.Aggressive), new DistanceCommand(["a"], 300),
            new WeaponCommand("a", 2, false), new PauseCommand(false)
        };
        foreach (var command in commands)
        {
            var decoded = (NetProtocol.Decode(NetProtocol.Encode(new CombatCommandMessage(command))) as CombatCommandMessage)?.Command;
            check(decoded is not null && decoded.GetType() == command.GetType() && Encoded(decoded) == Encoded(command), $"{command.GetType().Name} survives encoding");
        }
        check((NetProtocol.Decode(NetProtocol.Encode(new PingMessage(1, new Vector2(.1f, 1e7f)))) as PingMessage)?.Point == new Vector2(.1f, 1e7f), "points survive encoding exactly");
        static string Encoded(CombatCommand command) => System.Text.Encoding.UTF8.GetString(NetProtocol.Encode(new CombatCommandMessage(command)));
    }

    /// <summary>A replica that takes on every state the leader sends matches the leader's battle in everything a screen reads.</summary>
    private static void CheckReplica(Action<bool, string> check)
    {
        var roster = InitialDraft.CreateRandom(new Random(4)).Establish();
        var force = roster.LanceMechs(0).Select(mech => mech.Id).ToArray();
        CombatSimulation Deploy() => CombatSimulation.Deploy(roster, force, seed: 9, terrainSeed: 12, opponent: SimulationOpponent.Randomized);
        var battle = Deploy(); var replica = Deploy(); replica.BecomeReplica();
        var shots = 0; replica.ShotFired += _ => shots++;
        battle.Paused = false;
        battle.Execute(new MovementCommand(force, MovementMode.Run)); battle.Execute(new HeatPolicyCommand(force, HeatPolicy.Aggressive));
        battle.Execute(new MoveCommand(force, Vector2.Zero));
        var sent = 0; var largest = 0; var differences = new List<string>();
        for (var tick = 0; tick < 9000 && battle.Outcome == CombatOutcome.Ongoing; tick++)
        {
            if (tick == 300) battle.Execute(new JumpCommand(force, new Vector2(0, CombatRules.DeploymentDepth - 400)));
            battle.Advance(CombatSimulation.StateInterval);
            var data = NetProtocol.Encode(new BattleStateMessage(battle.State(sent))); largest = Math.Max(largest, data.Length);
            var state = ((BattleStateMessage)NetProtocol.Decode(data)!).State; sent += state.History.Length;
            replica.Apply(state); replica.Advance(CombatSimulation.StateInterval);
            if (tick % 150 == 0 || battle.Outcome != CombatOutcome.Ongoing) differences.AddRange(Differences(battle, replica).Select(item => $"{battle.Elapsed:0.0}s {item}"));
        }
        check(battle.History.Count > 50 && battle.Units.Any(unit => unit.Defeated) && battle.Units.Any(unit => unit.LandedAt is not null),
            $"the replicated battle fights, jumps and defeats mechs ({battle.History.Count} shots, {battle.Units.Count(unit => unit.Defeated)} defeated, "
            + $"{battle.Units.Count(unit => unit.LandedAt is not null)} landed, jump jets {string.Join("/", battle.Units.Select(unit => unit.Spec.Jump))}, {battle.Elapsed:0}s)");
        check(differences.Count == 0, "a replica matches the leader's battle: " + string.Join("; ", differences.Take(6)));
        check(shots == battle.History.Count && Json(replica.History) == Json(battle.History), "a replica fires every shot and logs every entry once, in order");
        check(largest < 16_384, $"a battle state is sent in under 16 KB ({largest} bytes)");
        var other = CombatSimulation.Deploy(roster, force.Take(2), seed: 9, terrainSeed: 12); other.BecomeReplica();
        Reject(() => other.Apply(battle.State()), check, "a replica refuses another battle's state");
        Reject(() => replica.Execute(new HoldCommand(force)), check, "a replica's orders go to the leader");
    }

    private static readonly HashSet<string> LeaderOnlyState = ["Route", "RouteIndex", "BlockedSeconds", "RouteGoal", "FiringPosition", "NextFiringSearch", "Spec", "Warrior", "Climate"];
    private static IEnumerable<string> Differences(CombatSimulation a, CombatSimulation b)
    {
        foreach (var name in new[] { nameof(CombatSimulation.Elapsed), nameof(CombatSimulation.Outcome), nameof(CombatSimulation.WindDownEnds), nameof(CombatSimulation.Paused) })
        {
            var property = typeof(CombatSimulation).GetProperty(name)!;
            if (Describe(property.GetValue(a)) != Describe(property.GetValue(b))) yield return name;
        }
        if (!a.PauseHolds.SetEquals(b.PauseHolds)) yield return "PauseHolds";
        foreach (var side in Enum.GetValues<CombatSide>())
            if (Describe(a.Contacts(side).Values.OrderBy(item => item.UnitId)) != Describe(b.Contacts(side).Values.OrderBy(item => item.UnitId))) yield return $"{side} contacts";
        foreach (var unit in a.Units)
            foreach (var property in typeof(CombatUnit).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                if (!LeaderOnlyState.Contains(property.Name) && Describe(property.GetValue(unit)) != Describe(property.GetValue(b.Unit(unit.Id))))
                    yield return $"{unit.Id}.{property.Name}";
    }
    /// <summary>A value as exact text: numbers round-trip, and objects list their properties.</summary>
    private static string Describe(object? value, int depth = 0) => value switch
    {
        null => "null",
        float number => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        double number => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        Vector2 vector => $"({Describe(vector.X)}, {Describe(vector.Y)})",
        string or Enum or bool or int or long => value.ToString()!,
        System.Collections.IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(item => Describe(item, depth + 1))) + "]",
        _ when depth > 3 => value.ToString()!,
        _ => "{" + string.Join(", ", value.GetType().GetProperties().Where(property => property.GetIndexParameters().Length == 0 && !LeaderOnlyState.Contains(property.Name))
            .Select(property => $"{property.Name}: {Describe(property.GetValue(value), depth + 1)}")) + "}"
    };
    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });

    /// <summary>A co-op mission from force selection to the results, over the in-memory network.</summary>
    private static void CheckBattle(Action<bool, string> check)
    {
        var network = new MemoryNetwork();
        using var host = new CoopHost(network.Leader, "Dave", 2);
        var sam = new CoopClient(network.Connect(), "Sam");
        void Pump(double delta = CoopHost.SyncInterval) { for (var i = 0; i < 4; i++) { host.Update(delta); sam.Update(); } }
        Pump(); sam.Claim(1); Pump(); host.Start(new Random(5)); Pump();
        sam.SubmitDraft(); host.LeaderDraft!.ForceName = "Battle Company"; host.SubmitLeaderDraft(); Pump();
        var campaign = host.Campaign!; var roster = campaign.Roster!;
        check(campaign.Missions.Lances == 2 && campaign.Missions.Available.All(offer => offer.Opposition.Length is >= 4 and <= 8),
            "a two-lance company's offers field opposition for both lances");
        check(host.DeploymentProblem == "Accept a mission from the office phone first.", "force selection needs an accepted mission");
        var offer = campaign.Missions.Available.First(item => campaign.DeadlineDays(item) is not null);
        campaign.Execute(new AcceptMissionCommand(offer.Id)); host.MarkDirty();
        host.OpenDeployment(); Pump();
        check(sam.Deployment is { Count: 2 } && sam.Campaign!.Missions.Current?.Id == offer.Id, "force selection reaches every player, with the accepted mission");
        string? refused = null; sam.CommandRefused += reason => refused = reason;
        var leaderMechs = roster.LanceMechs(0).Take(2).Select(mech => mech.Id).ToArray();
        var samMechs = roster.LanceMechs(1).Take(3).Select(mech => mech.Id).ToArray();
        sam.ChooseDeployment(leaderMechs, true); Pump();
        check(refused == "Deploy only BattleMechs from your own lance." && !host.OthersReady, "a player deploys only from their own lance");
        refused = null; sam.ChooseDeployment([], true); Pump();
        check(refused is not null && !host.OthersReady, "a ready player deploys at least one mech");
        refused = null; sam.Send(new SetLanceCommand(samMechs[0], null)); Pump();
        check(refused == "Wait until the mission's battle is over." && roster.Mechs.Single(mech => mech.Id == samMechs[0]).Lance == 1,
            "the campaign is held while a mission is deployed");
        Reject(() => host.Launch(leaderMechs, 77), check, "the leader cannot launch before every player is ready");
        sam.ChooseDeployment(samMechs, true); Pump();
        check(host.OthersReady && sam.Deployment![1] is { Ready: true, Mechs.Length: 3 }, "a player marks their force ready");

        var battle = host.Launch(leaderMechs, 77, seed: 8); Pump();
        var replica = sam.Battle;
        check(replica is { Replica: true } && replica.Terrain.Seed == 77 && replica.Units.Select(unit => unit.Id).SequenceEqual(battle.Units.Select(unit => unit.Id))
            && sam.Deployment is null, "every player sees the leader's battle on the same terrain");
        check(battle.Units.Count(unit => unit.Side == CombatSide.Enemy) == offer.Opposition.Length
            && battle.Units.Where(unit => unit.Side == CombatSide.Friendly).Select(unit => (unit.Id, unit.Lance))
                .SequenceEqual(leaderMechs.Select(id => (id, (int?)0)).Concat(samMechs.Select(id => (id, (int?)1)))),
            "each player's force fights in their own lance against the whole opposition");
        sam.Send(new MovementCommand([samMechs[0], leaderMechs[0]], MovementMode.Run)); Pump();
        check(battle.Unit(samMechs[0]).Movement == MovementMode.Run && battle.Unit(leaderMechs[0]).Movement == MovementMode.Walk
            && replica!.Unit(samMechs[0]).Movement == MovementMode.Run, "a player orders only their own lance, and sees the result");
        check(battle.Paused && battle.PauseHolds.SetEquals([0, 1]), "every player's pause toggle starts on");
        battle.Execute(new PauseCommand(false)); Pump();
        check(battle.Paused && replica!.Paused && replica.PauseHolds.SetEquals([1]), "the battle waits for every toggle to come off");
        sam.Send(new PauseCommand(false)); Pump();
        check(!battle.Paused && !replica!.Paused, "the battle runs once every toggle is off");
        for (var i = 0; i < 90; i++) { battle.Advance(CombatSimulation.StateInterval); host.Update(CombatSimulation.StateInterval); sam.Update(); replica!.Advance(CombatSimulation.StateInterval); }
        check(replica!.Elapsed == battle.Elapsed && replica.Units.All(unit => unit.Position == battle.Unit(unit.Id).Position), "the battle's state reaches every player as it runs");
        int? heard = null, shown = null; host.Pinged += (slot, _) => heard = slot; sam.Pinged += (slot, point) => shown = point == new Vector2(10, 20) ? slot : null;
        sam.Ping(new Vector2(10, 20)); Pump();
        check(heard == 1 && shown == 1, "a ping reaches every player, marked with whoever placed it");

        battle.End(); campaign.SettleMission(battle.MissionReport()); host.MarkDirty(); host.FinishBattle("Mission failed"); Pump();
        check(replica.Outcome == CombatOutcome.Ended && sam.Settlement == "Mission failed" && sam.Campaign!.Missions.Current is null,
            "every player sees the battle's end and the leader's settlement");
        var closed = false; sam.BattleClosed += () => closed = true;
        host.CloseBattle(); Pump();
        check(closed && sam.Battle is null && host.Battle is null, "the leader leaving the results closes the battle for everyone");

        var next = campaign.Missions.Available.First(item => campaign.DeadlineDays(item) is not null);
        campaign.Execute(new AcceptMissionCommand(next.Id)); host.MarkDirty(); host.OpenDeployment(); Pump();
        sam.ChooseDeployment(samMechs, true); Pump();
        sam.Dispose(); Pump();
        check(!host.OthersReady && host.Deployment![1] is { Ready: false }, "a player who leaves force selection is no longer ready");
        var back = new CoopClient(network.Connect(), "Sam");
        void Rejoin() { for (var i = 0; i < 4; i++) { host.Update(0); back.Update(); } back.Claim(1); for (var i = 0; i < 4; i++) { host.Update(0); back.Update(); } }
        Rejoin();
        check(back.Deployment is { Count: 2 }, "a rejoining player returns to force selection");
        back.ChooseDeployment(samMechs, true); for (var i = 0; i < 4; i++) { host.Update(0); back.Update(); }
        battle = host.Launch(leaderMechs, 78);
        for (var i = 0; i < 4; i++) { host.Update(0); back.Update(); }
        battle.Paused = false; battle.Advance(1);
        back.Dispose(); host.Update(0);
        check(battle.Paused && battle.PauseHolds.SetEquals([1]) && host.Absent.SequenceEqual(["Sam"]), "a player who disconnects holds the battle paused");
        back = new CoopClient(network.Connect(), "Sam"); Rejoin();
        check(back.Battle is { } rejoined && rejoined.Elapsed == battle.Elapsed && rejoined.History.Count == battle.History.Count && host.Absent.Count == 0,
            "a rejoining player returns to the battle as it stands");
        string? reason = null; back.Ended += item => reason = item;
        host.Shutdown("The leader stopped waiting."); back.Update();
        check(reason == "The leader stopped waiting." && back.Closed && !back.LostConnection, "the leader ending the game tells every player why");
    }

    private static void Reject(Action action, Action<bool, string> check, string message)
    {
        var rejected = false;
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        check(rejected, message);
    }

    private static bool Same(Campaign a, Campaign b) =>
        System.Text.Json.JsonSerializer.Serialize(a.Snapshot()) == System.Text.Json.JsonSerializer.Serialize(b.Snapshot());
}
