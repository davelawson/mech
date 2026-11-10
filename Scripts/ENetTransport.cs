using Godot;

namespace Mech;

/// <summary>Raw reliable packets over Godot's ENet peer, without the scene multiplayer API: the co-op session carries its own
/// messages. The leader hosts; each client's only peer is the leader, peer 1.</summary>
public sealed class ENetTransport : INetTransport
{
    private readonly ENetMultiplayerPeer _peer = new();
    private readonly List<NetEvent> _events = [];
    // Reported after the packets of the same poll, so a peer's last words arrive before its departure.
    private readonly List<NetEvent> _disconnected = [];
    private readonly bool _client;
    private bool _connected, _closed;
    private const int TimeoutLimit = 32;

    private ENetTransport(bool client)
    {
        _client = client;
        _peer.PeerConnected += id =>
        {
            _connected = true; _events.Add(new(NetEventKind.Connected, (int)id));
            // A dropped connection is noticed within ten seconds rather than ENet's thirty.
            _peer.GetPeer((int)id)?.SetTimeout(TimeoutLimit, 3000, 10000);
        };
        _peer.PeerDisconnected += id => _disconnected.Add(new(NetEventKind.Disconnected, (int)id));
    }

    /// <summary>Listens for up to this many other players. Throws when the port cannot be opened.</summary>
    public static ENetTransport Host(int port, int clients)
    {
        var transport = new ENetTransport(false);
        // A spare connection lets a player on another build, or one joining a full game, be told why before leaving.
        var error = transport._peer.CreateServer(port, clients + 1);
        if (error != Error.Ok) throw new IOException($"Port {port} could not be opened ({error}).");
        return transport;
    }

    public static ENetTransport Connect(string address, int port)
    {
        var transport = new ENetTransport(true);
        var error = transport._peer.CreateClient(address, port);
        if (error != Error.Ok) throw new IOException($"Could not connect to {address}:{port} ({error}).");
        return transport;
    }

    public IReadOnlyList<NetEvent> Poll()
    {
        if (_closed) return [];
        _peer.Poll();
        while (_peer.GetAvailablePacketCount() > 0)
        {
            var from = _peer.GetPacketPeer();
            _events.Add(new(NetEventKind.Data, from, _peer.GetPacket()));
        }
        // A client that never connects reports no peer; its status alone says the attempt failed.
        if (_client && !_connected && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
        { _connected = true; _events.Add(new(NetEventKind.Disconnected, CoopClient.LeaderPeer)); }
        _events.AddRange(_disconnected); _disconnected.Clear();
        var events = _events.ToArray(); _events.Clear();
        return events;
    }

    public void Send(int peer, byte[] data)
    {
        if (_closed || _peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected) return;
        _peer.TransferMode = MultiplayerPeer.TransferModeEnum.Reliable;
        _peer.SetTargetPeer(peer);
        _peer.PutPacket(data);
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        // Deliver what is queued, such as the leader's word that the game has ended, before disconnecting.
        if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected) _peer.Poll();
        _peer.Close();
    }
}
