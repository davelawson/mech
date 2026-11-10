using Godot;

namespace Mech;

/// <summary>What the hosting screens say about the co-op port on the home router.</summary>
public enum RouterPortState { NotHosting, Opening, Opened, Failed }

/// <summary>The co-op port, forwarded to this computer by the home router over UPnP while the leader hosts. Disposing removes
/// the forwarding.</summary>
public sealed class RouterPort : IDisposable
{
    private readonly Upnp _upnp;
    private readonly int _port;
    private bool _closed;
    private const string Protocol = "UDP";

    private RouterPort(Upnp upnp, int port) { _upnp = upnp; _port = port; }

    /// <summary>Asks the router to forward the UDP port; null when it does not, as when UPnP is off or the port is forwarded to
    /// another computer. Discovery blocks for up to two seconds, so it runs in the background.</summary>
    public static Task<IDisposable?> OpenAsync(int port) => Task.Run(() =>
    {
        try
        {
            var upnp = new Upnp();
            if (upnp.Discover(2000, 2, "InternetGatewayDevice") != (int)Upnp.UpnpResult.Success || upnp.GetGateway()?.IsValidGateway() != true) return null;
            // No lease: some routers refuse timed ones. The forwarding is removed when the session ends.
            return upnp.AddPortMapping(port, port, "Mech co-op", Protocol, 0) == (int)Upnp.UpnpResult.Success ? new RouterPort(upnp, port) : (IDisposable?)null;
        }
        catch (Exception) { return null; }
    });

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        try { _upnp.DeletePortMapping(_port, Protocol); }
        catch (Exception) { }
    }
}
