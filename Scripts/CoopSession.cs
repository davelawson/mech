using Godot;

namespace Mech;

/// <summary>The leader's side of a co-op session. It seats players, runs their drafts, carries out their commands on the one
/// authoritative campaign, and sends every client the campaign as it changes. The leader always holds slot 0.</summary>
public sealed class CoopHost : IDisposable
{
    /// <summary>The campaign is sent at most this often while it keeps changing.</summary>
    public const double SyncInterval = .2;
    private readonly INetTransport _transport;
    private readonly LobbySeat[] _seats;
    // Peers that have said hello, with the slot each holds once seated.
    private readonly Dictionary<int, int?> _peers = [];
    private readonly HashSet<int> _refused = [];
    private IReadOnlyList<InitialDraft>? _drafts;
    private bool _dirty;
    private double _sinceSync;

    public int Players => _seats.Length;
    public LobbyPhase Phase { get; private set; }
    public IReadOnlyList<LobbySeat> Seats => _seats;
    /// <summary>The campaign: resumed from a save, or formed once every draft is in.</summary>
    public Campaign? Campaign { get; private set; }
    /// <summary>The leader's own draft, while the company is being drafted.</summary>
    public InitialDraft? LeaderDraft => _drafts?[Campaign.LeaderSlot];
    public bool AllPresent => _seats.All(seat => seat.Present);
    /// <summary>The names of the seated players who have disconnected and not yet returned.</summary>
    public IReadOnlyList<string> Absent => _seats.Where(seat => !seat.Present && seat.Name is not null && (Phase != LobbyPhase.Gathering || Campaign is not null))
        .Select(seat => seat.Name!).ToArray();
    public bool CanStart => Phase == LobbyPhase.Gathering && AllPresent;
    /// <summary>The seats or phase changed.</summary>
    public event Action? Changed;
    /// <summary>A player's command changed the campaign.</summary>
    public event Action? CampaignChanged;
    /// <summary>Every draft is in and the company is formed.</summary>
    public event Action? CompanyEstablished;
    public event Action<string>? PlayerLeft;
    public event Action<string>? PlayerJoined;
    /// <summary>Force selection for the current mission, one seat per player, while it is open.</summary>
    public IReadOnlyList<DeploymentSeat>? Deployment => _deployment;
    private DeploymentSeat[]? _deployment;
    /// <summary>The battle being fought: the only one that is stepped. Other players see replicas of it.</summary>
    public CombatSimulation? Battle { get; private set; }
    private BattleStartMessage? _battleStart;
    private int _sentHistory;
    private double _sinceState;
    private byte[]? _lastState;
    /// <summary>A player changed their force or readiness.</summary>
    public event Action? DeploymentChanged;
    /// <summary>A player pinged the battlefield.</summary>
    public event Action<int, Vector2>? Pinged;

    /// <summary>Hosts a new campaign for this many players, or resumes a saved co-op campaign with its own player count.</summary>
    public CoopHost(INetTransport transport, string leaderName, int players, Campaign? resume = null)
    {
        if (!Campaign.ValidPlayerName(leaderName)) throw new ArgumentException("Enter a player name.", nameof(leaderName));
        if (resume is not null) players = resume.PlayerNames?.Count ?? throw new ArgumentException("This campaign is not a co-op campaign.", nameof(resume));
        else if (players < 2) throw new ArgumentOutOfRangeException(nameof(players), "A co-op campaign has two or three players.");
        else if (InitialDraft.CompanyProblem(players) is { } problem) throw new ArgumentOutOfRangeException(nameof(players), problem);
        _transport = transport; Campaign = resume;
        _seats = Enumerable.Range(0, players).Select(slot => new LobbySeat(slot, slot == 0 ? leaderName : resume?.PlayerNames![slot], slot == 0)).ToArray();
    }

    /// <summary>Begins once every seat is filled: a new campaign goes to the drafts, a resumed one straight into play.</summary>
    public void Start(Random? random = null)
    {
        if (!CanStart) throw new InvalidOperationException("Every seat must be filled first.");
        if (Campaign is { } resumed)
        {
            resumed.RenamePlayers(_seats.Select(seat => seat.Name!).ToArray());
            Phase = LobbyPhase.Playing; BroadcastLobby(); SendCampaign();
        }
        else
        {
            _drafts = InitialDraft.CreateCompany(Players, random);
            Phase = LobbyPhase.Drafting; BroadcastLobby();
            foreach (var (peer, slot) in _peers) if (slot is { } seated) Send(peer, new DraftOfferMessage(_drafts[seated].Offer()));
        }
        Changed?.Invoke();
    }

    /// <summary>The leader has confirmed their own draft.</summary>
    public void SubmitLeaderDraft()
    {
        if (Phase != LobbyPhase.Drafting || LeaderDraft is not { Complete: true }) throw new InvalidOperationException("Complete your draft first.");
        MarkDrafted(Campaign.LeaderSlot);
    }

    /// <summary>The campaign changed on the leader's machine; clients receive it at the next sync.</summary>
    public void MarkDirty() => _dirty = true;

    /// <summary>Tells every client something, such as a scheduled event.</summary>
    public void Broadcast(string title, string body) => Broadcast(new NoticeMessage(title, body));

    /// <summary>Handles what arrived and sends the campaign if it changed and the sync interval has passed, and the battle
    /// at its own interval.</summary>
    public void Update(double delta)
    {
        if (_closed) return;
        if (ShuttingDown)
        {
            foreach (var item in _transport.Poll()) if (item.Kind == NetEventKind.Disconnected) _peers.Remove(item.Peer);
            _sinceShutdown += delta;
            if (_peers.Count == 0 || _sinceShutdown >= ShutdownSeconds) Dispose();
            return;
        }
        foreach (var item in _transport.Poll()) Handle(item);
        _sinceSync += delta;
        if (_dirty && _sinceSync >= SyncInterval) SendCampaign();
        _sinceState += delta;
        if (Battle is not null && _sinceState >= CombatSimulation.StateInterval) SendBattle();
    }

    /// <summary>Why force selection cannot open now, or null.</summary>
    public string? DeploymentProblem => Phase != LobbyPhase.Playing || Campaign?.Roster is null ? "The campaign has not started."
        : Battle is not null || _deployment is not null ? "A battle is already under way."
        : !AllPresent ? $"Every player must be present: waiting for {string.Join(" and ", _seats.Where(seat => !seat.Present).Select(seat => seat.Name))}."
        : Campaign.Missions.Current is null ? "Accept a mission from the office phone first."
        : !Campaign.CanAdvance ? "Settle the quarterly payment first." : null;
    /// <summary>The leader may launch once every other player is present and ready.</summary>
    public bool OthersReady => _deployment is not null && AllPresent && _deployment.All(seat => seat.Slot == Campaign.LeaderSlot || seat.Ready);

    /// <summary>Opens force selection for the current mission and pulls every player into it.</summary>
    public void OpenDeployment()
    {
        if (DeploymentProblem is { } problem) throw new InvalidOperationException(problem);
        _deployment = _seats.Select(seat => new DeploymentSeat(seat.Slot, [], false)).ToArray();
        Flush(); Broadcast(new DeploymentMessage([.. _deployment]));
    }

    /// <summary>Closes force selection without a battle; every player returns to the hangar.</summary>
    public void CancelDeployment()
    {
        if (_deployment is null) return;
        _deployment = null; Broadcast(new BattleClosedMessage());
    }

    /// <summary>Launches the mission once everyone is ready: the leader's force, then each other player's, against the mission's
    /// opposition. Every player is sent the same deployment, which their replica recreates.</summary>
    public CombatSimulation Launch(IReadOnlyList<string> leaderMechs, int terrainSeed, int? seed = null)
    {
        if (_deployment is null) throw new InvalidOperationException("Force selection is not open.");
        if (!OthersReady) throw new InvalidOperationException("Every player must be ready.");
        if (ForceProblem(Campaign.LeaderSlot, leaderMechs, true) is { } problem) throw new ArgumentException(problem);
        var force = leaderMechs.Concat(_deployment.Where(seat => seat.Slot != Campaign.LeaderSlot).OrderBy(seat => seat.Slot).SelectMany(seat => seat.Mechs)).ToArray();
        var combatSeed = seed ?? System.Random.Shared.Next();
        var battle = CombatSimulation.DeployMission(Campaign!.Roster!, force, Campaign.Missions.Current!, combatSeed, terrainSeed);
        Flush();
        _deployment = null; Battle = battle; _battleStart = new(force, combatSeed, terrainSeed); _sentHistory = 0; _lastState = null;
        Broadcast(_battleStart); SendBattle();
        return battle;
    }

    /// <summary>The leader has settled the battle: every player sees its final state and the settlement.</summary>
    public void FinishBattle(string settlement)
    {
        if (Battle is null) return;
        SendBattle(); Broadcast(new BattleResultMessage(settlement)); Flush();
    }

    /// <summary>The leader has left the battle's results; every player returns to the hangar.</summary>
    public void CloseBattle()
    {
        if (Battle is null) return;
        Battle = null; _battleStart = null; Broadcast(new BattleClosedMessage());
    }

    /// <summary>Marks a point of the battlefield for every player.</summary>
    public void Ping(Vector2 point)
    {
        if (Battle is null || !point.IsFinite()) return;
        Broadcast(new PingMessage(Campaign.LeaderSlot, point)); Pinged?.Invoke(Campaign.LeaderSlot, point);
    }

    /// <summary>Why a player cannot deploy these mechs: each must be ready, in their own lance, and listed once; a ready player
    /// deploys one to four.</summary>
    private string? ForceProblem(int slot, IReadOnlyList<string>? mechs, bool ready)
    {
        var roster = Campaign?.Roster;
        if (roster is null || mechs is null || mechs.Any(id => id is null)) return "Select one to four of your BattleMechs.";
        if (mechs.Distinct().Count() != mechs.Count || mechs.Count > ForceRoster.LanceSize || ready && mechs.Count == 0) return "Select one to four of your BattleMechs.";
        foreach (var id in mechs)
        {
            var mech = roster.Mechs.SingleOrDefault(item => item.Id == id);
            if (mech is null || mech.Lance != slot) return "Deploy only BattleMechs from your own lance.";
            if (CombatSimulation.UnavailableReason(roster, mech) is { } reason) return $"{mech.Spec.DisplayName}: {reason}";
        }
        return null;
    }

    /// <summary>Sends the campaign now, if it changed.</summary>
    public void Flush() { if (_dirty) SendCampaign(); }

    private void Handle(NetEvent item)
    {
        switch (item.Kind)
        {
            case NetEventKind.Connected: break;
            case NetEventKind.Disconnected: Leave(item.Peer); break;
            case NetEventKind.Data when !_refused.Contains(item.Peer):
                if (NetProtocol.Decode(item.Data!) is { } message) Receive(item.Peer, message);
                break;
        }
    }

    private void Receive(int peer, NetMessage message)
    {
        var slot = _peers.GetValueOrDefault(peer);
        switch (message)
        {
            case HelloMessage hello when !_peers.ContainsKey(peer):
                var reason = hello.Protocol != NetProtocol.Version || hello.Build != NetProtocol.Build ? "The leader is running a different build of the game."
                    : !Campaign.ValidPlayerName(hello.Name) ? "Enter a player name of 1–24 characters."
                    : _seats.All(seat => seat.Present) ? "Every seat in this campaign is taken." : null;
                if (reason is not null) { Send(peer, new RefusedMessage(reason)); _refused.Add(peer); return; }
                _peers[peer] = null; _names[peer] = hello.Name; Send(peer, Lobby(null));
                break;
            case ClaimSeatMessage claim when _peers.ContainsKey(peer) && slot is null:
                if (claim.Slot <= 0 || claim.Slot >= Players || _seats[claim.Slot].Present) { Send(peer, Lobby(null)); return; }
                _peers[peer] = claim.Slot;
                _seats[claim.Slot] = _seats[claim.Slot] with { Name = _names[peer], Present = true };
                BroadcastLobby();
                if (Phase == LobbyPhase.Drafting && !_seats[claim.Slot].Drafted) Send(peer, new DraftOfferMessage(_drafts![claim.Slot].Offer()));
                if (Phase == LobbyPhase.Playing) { Campaign!.RenamePlayers(_seats.Select(seat => seat.Name!).ToArray()); SendCampaign(); }
                // A player rejoining a mission returns to its force selection, or to the battle as it stands.
                if (_deployment is not null) Send(peer, new DeploymentMessage([.. _deployment]));
                if (Battle is not null && _battleStart is not null) { Send(peer, _battleStart); Send(peer, new BattleStateMessage(Battle.State())); }
                if (Phase != LobbyPhase.Gathering) PlayerJoined?.Invoke(_names[peer]);
                Changed?.Invoke();
                break;
            case DraftSubmissionMessage submission when slot is { } drafter && Phase == LobbyPhase.Drafting && !_seats[drafter].Drafted:
                try { _drafts![drafter].Accept(submission.Choices, null); }
                catch (ArgumentException error) { Send(peer, new CommandRefusedMessage(error.Message)); return; }
                MarkDrafted(drafter);
                break;
            case DeploymentChoiceMessage choice when slot is { } chooser && _deployment is not null:
                if (ForceProblem(chooser, choice.Mechs, choice.Ready) is { } problem) { Send(peer, new CommandRefusedMessage(problem)); Send(peer, new DeploymentMessage([.. _deployment])); return; }
                _deployment[chooser] = new(chooser, [.. choice.Mechs], choice.Ready);
                Broadcast(new DeploymentMessage([.. _deployment])); DeploymentChanged?.Invoke();
                break;
            case CombatCommandMessage order when slot is { } commander && Battle is { Outcome: CombatOutcome.Ongoing } battle:
                try { battle.Execute(order.Command, commander); }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException)
                { Send(peer, new CommandRefusedMessage("That order could not be carried out.")); }
                break;
            case PingMessage ping when slot is { } pinger && Battle is not null && ping.Point.IsFinite():
                Broadcast(new PingMessage(pinger, ping.Point)); Pinged?.Invoke(pinger, ping.Point);
                break;
            case CommandMessage command when slot is { } sender:
                if (Phase != LobbyPhase.Playing || Campaign is null) { Send(peer, new CommandRefusedMessage("The campaign has not started.")); return; }
                if (_deployment is not null || Battle is not null) { Send(peer, new CommandRefusedMessage("Wait until the mission's battle is over.")); return; }
                try { Campaign.Execute(command.Command, sender); }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException)
                { Send(peer, new CommandRefusedMessage(error.Message)); return; }
                MarkDirty(); CampaignChanged?.Invoke();
                break;
        }
    }
    private readonly Dictionary<int, string> _names = [];

    private void MarkDrafted(int slot)
    {
        _seats[slot] = _seats[slot] with { Drafted = true };
        if (_seats.All(seat => seat.Drafted))
        {
            var campaign = new Campaign();
            campaign.EstablishCompany(_drafts!, _seats.Select(seat => seat.Name!).ToArray());
            Campaign = campaign; _drafts = null; Phase = LobbyPhase.Playing;
            BroadcastLobby(); SendCampaign(); Changed?.Invoke(); CompanyEstablished?.Invoke();
            return;
        }
        BroadcastLobby(); Changed?.Invoke();
    }

    private void Leave(int peer)
    {
        _names.Remove(peer, out var name); _refused.Remove(peer);
        if (!_peers.Remove(peer, out var slot) || slot is not { } seated) return;
        // A seat in a new lobby opens again; once drafting starts it keeps its name and draft for whoever rejoins it.
        _seats[seated] = _seats[seated] with { Present = false, Name = Phase == LobbyPhase.Gathering && Campaign is null ? null : _seats[seated].Name };
        BroadcastLobby(); Changed?.Invoke();
        // An absent player holds the battle paused; their seat in force selection is no longer ready.
        Battle?.HoldPause(seated, true);
        if (_deployment is not null) { _deployment[seated] = _deployment[seated] with { Ready = false }; Broadcast(new DeploymentMessage([.. _deployment])); DeploymentChanged?.Invoke(); }
        if (Phase != LobbyPhase.Gathering) PlayerLeft?.Invoke(name ?? $"Player {seated + 1}");
    }

    private LobbyMessage Lobby(int? slot) => new([.. _seats], Phase, slot);
    private void BroadcastLobby() { foreach (var (peer, slot) in _peers) Send(peer, Lobby(slot)); }
    private void Broadcast(NetMessage message) { foreach (var (peer, slot) in _peers) if (slot is not null) Send(peer, message); }
    private void SendCampaign()
    {
        _dirty = false; _sinceSync = 0;
        if (Campaign is not null && Phase == LobbyPhase.Playing) Broadcast(new CampaignMessage(Campaign.Snapshot()));
    }
    /// <summary>Sends the battle as it stands with the log entries not yet sent, unless nothing has changed.</summary>
    private void SendBattle()
    {
        _sinceState = 0;
        if (Battle is null) return;
        var state = Battle.State(_sentHistory);
        var data = NetProtocol.Encode(new BattleStateMessage(state));
        if (state.History.Length == 0 && _lastState is not null && data.AsSpan().SequenceEqual(_lastState)) return;
        _sentHistory += state.History.Length; _lastState = state.History.Length == 0 ? data : null;
        foreach (var (peer, slot) in _peers) if (slot is not null) _transport.Send(peer, data);
    }
    private void Send(int peer, NetMessage message) => _transport.Send(peer, NetProtocol.Encode(message));

    /// <summary>Ends the game for every player, telling them why. The connection stays open until they have gone or
    /// <see cref="ShutdownSeconds"/> pass, so the message reaches them; keep calling <see cref="Update"/> until <see cref="Finished"/>.</summary>
    public void Shutdown(string reason)
    {
        if (_closed || ShuttingDown) return;
        ShuttingDown = true; Battle = null; _deployment = null;
        foreach (var peer in _peers.Keys) Send(peer, new SessionEndedMessage(reason));
        if (_peers.Count == 0) Dispose();
    }
    public const double ShutdownSeconds = 1;
    public bool ShuttingDown { get; private set; }
    /// <summary>The connection is closed.</summary>
    public bool Finished => _closed;
    private double _sinceShutdown;
    private bool _closed;
    public void Dispose() { _closed = true; _transport.Dispose(); }
}

/// <summary>A player's side of a co-op session: it joins the leader's lobby, claims a seat, drafts, and keeps a copy of the
/// campaign as the leader last sent it. Its commands go to the leader, which may refuse them.</summary>
public sealed class CoopClient : IDisposable
{
    /// <summary>The leader is always the transport's peer 1.</summary>
    public const int LeaderPeer = 1;
    private readonly INetTransport _transport;
    private readonly string _name;
    public bool Connected { get; private set; }
    public bool Closed { get; private set; }
    /// <summary>The session ended because the connection dropped, rather than the leader ending the game or refusing this player.</summary>
    public bool LostConnection { get; private set; }
    public int? Slot { get; private set; }
    public LobbyPhase Phase { get; private set; }
    public IReadOnlyList<LobbySeat> Seats { get; private set; } = [];
    public InitialDraft? Draft { get; private set; }
    public Campaign? Campaign { get; private set; }
    public event Action? Changed;
    public event Action? DraftOffered;
    /// <summary>A newer campaign arrived from the leader.</summary>
    public event Action? CampaignReceived;
    public event Action<string>? CommandRefused;
    public event Action<string, string>? Notice;
    /// <summary>The session ended: refused, or the connection to the leader was lost.</summary>
    public event Action<string>? Ended;
    /// <summary>Force selection for the current mission, while the leader has it open.</summary>
    public IReadOnlyList<DeploymentSeat>? Deployment { get; private set; }
    /// <summary>A replica of the leader's battle, built from the same deployment and kept to the leader's state.</summary>
    public CombatSimulation? Battle { get; private set; }
    /// <summary>The leader's settlement of the battle, once made.</summary>
    public string? Settlement { get; private set; }
    public event Action? DeploymentChanged;
    public event Action? BattleStarted;
    public event Action? BattleSettled;
    /// <summary>The leader closed force selection or the battle's results.</summary>
    public event Action? BattleClosed;
    public event Action<int, Vector2>? Pinged;

    public CoopClient(INetTransport transport, string name)
    {
        if (!Campaign.ValidPlayerName(name)) throw new ArgumentException("Enter a player name.", nameof(name));
        _transport = transport; _name = name;
    }

    public void Claim(int slot) => Send(new ClaimSeatMessage(slot));
    public void Send(CampaignCommand command) => Send(new CommandMessage(command));
    public void Send(CombatCommand command) => Send(new CombatCommandMessage(command));
    public void ChooseDeployment(IEnumerable<string> mechs, bool ready) => Send(new DeploymentChoiceMessage([.. mechs], ready));
    public void Ping(Vector2 point) => Send(new PingMessage(Slot ?? 0, point));
    /// <summary>Confirms this player's draft.</summary>
    public void SubmitDraft()
    {
        if (Draft is not { Complete: true }) throw new InvalidOperationException("Complete your draft first.");
        Send(new DraftSubmissionMessage(Enumerable.Range(0, 8).Select(Draft.Choice).ToArray()));
    }

    public void Update()
    {
        foreach (var item in _transport.Poll())
        {
            if (Closed) return;
            switch (item.Kind)
            {
                case NetEventKind.Connected: Connected = true; Send(new HelloMessage(NetProtocol.Version, NetProtocol.Build, _name)); break;
                case NetEventKind.Disconnected: LostConnection = Connected; End(Connected ? "The connection to the leader was lost." : "Could not reach the leader."); break;
                case NetEventKind.Data: if (NetProtocol.Decode(item.Data!) is { } message) Receive(message); break;
            }
        }
    }

    private void Receive(NetMessage message)
    {
        switch (message)
        {
            case RefusedMessage refused: End(refused.Reason); break;
            case SessionEndedMessage ended: End(ended.Reason); break;
            case LobbyMessage lobby: Seats = lobby.Seats; Phase = lobby.Phase; Slot = lobby.Slot; Changed?.Invoke(); break;
            case DraftOfferMessage offer:
                try { Draft = InitialDraft.FromOffer(offer.Draft); } catch (ArgumentException) { End("The leader sent an invalid draft."); return; }
                DraftOffered?.Invoke();
                break;
            case CampaignMessage state:
                try { Campaign = Campaign.Restore(state.Snapshot); } catch (ArgumentException) { End("The leader sent an invalid campaign."); return; }
                Draft = null; CampaignReceived?.Invoke();
                break;
            case CommandRefusedMessage refused: CommandRefused?.Invoke(refused.Reason); break;
            case NoticeMessage notice: Notice?.Invoke(notice.Title, notice.Body); break;
            case DeploymentMessage deployment: Deployment = deployment.Seats; DeploymentChanged?.Invoke(); break;
            case BattleStartMessage start:
                if (Campaign?.Roster is not { } roster || Campaign.Missions.Current is not { } mission) { End("The leader's battle could not be recreated."); return; }
                try { Battle = CombatSimulation.DeployMission(roster, start.Force, mission, start.Seed, start.TerrainSeed); }
                catch (ArgumentException) { End("The leader's battle could not be recreated."); return; }
                Battle.BecomeReplica(); Deployment = null; Settlement = null; BattleStarted?.Invoke();
                break;
            case BattleStateMessage state when Battle is not null:
                try { Battle.Apply(state.State); } catch (ArgumentException) { End("The leader sent an invalid battle."); }
                break;
            case BattleResultMessage result: Settlement = result.Settlement; BattleSettled?.Invoke(); break;
            case BattleClosedMessage: Deployment = null; Battle = null; Settlement = null; BattleClosed?.Invoke(); break;
            case PingMessage ping when Battle is not null: Pinged?.Invoke(ping.Slot, ping.Point); break;
        }
    }

    private void End(string reason)
    {
        if (Closed) return;
        Closed = true; _transport.Dispose(); Ended?.Invoke(reason);
    }
    private void Send(NetMessage message) { if (!Closed) _transport.Send(LeaderPeer, NetProtocol.Encode(message)); }
    public void Dispose() { Closed = true; _transport.Dispose(); }
}
