using System.Net;
using System.Net.Sockets;
using Godot;

namespace Mech;

/// <summary>Where other players reach the leader: the internet address the home router reports over UPnP, and this computer's
/// addresses on the local network. Nothing is asked of any service outside the local network.</summary>
public static class HostAddress
{
    /// <summary>Asks the router for its internet address; null when no router answers, as when UPnP is off. Discovery blocks for
    /// up to two seconds, so it runs in the background.</summary>
    public static Task<string?> PublicAsync() => Task.Run(() =>
    {
        try
        {
            var upnp = new Upnp();
            if (upnp.Discover(2000, 2, "InternetGatewayDevice") != (int)Upnp.UpnpResult.Success || upnp.GetGateway()?.IsValidGateway() != true) return null;
            var address = upnp.QueryExternalAddress();
            return IPAddress.TryParse(address, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork ? address : null;
        }
        catch (Exception) { return (string?)null; }
    });

    /// <summary>This computer's private IPv4 addresses, for players on the same network.</summary>
    public static IReadOnlyList<string> Local() => IP.GetLocalAddresses().Where(IsPrivate).Order(StringComparer.Ordinal).ToArray();

    /// <summary>An IPv4 address in 10/8, 172.16/12 or 192.168/16.</summary>
    public static bool IsPrivate(string address)
    {
        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = parsed.GetAddressBytes();
        return bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168;
    }

    /// <summary>The lines the hosting screens show: the addresses, asked of the router or not answered, then the state of the
    /// co-op port on the router.</summary>
    public static string Text(string? wan, IReadOnlyList<string> lan, int port, bool looking, RouterPortState router)
    {
        var internet = looking ? "Internet address: asking your router…"
            : wan is null ? "Internet address: your router did not say (UPnP may be off). Look it up on the router, or search \"what is my IP\"."
            : $"Internet: {wan}:{port}";
        var local = lan.Count == 0 ? "" : $" · Local network: {string.Join(", ", lan.Select(address => $"{address}:{port}"))}";
        var forwarding = router switch
        {
            RouterPortState.Opening => $"Opening UDP port {port} on your router…",
            RouterPortState.Opened => $"UDP port {port} is open on your router.",
            RouterPortState.Failed => $"Your router did not open UDP port {port} (UPnP may be off, or the port is forwarded to another computer). Forward it on the router for internet play.",
            _ => $"UDP port {port} is opened on your router when you host, if it supports UPnP.",
        };
        return $"{internet}{local}\n{forwarding}";
    }
}
