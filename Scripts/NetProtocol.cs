using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Mech;

/// <summary>Where a co-op session stands: players gathering in the lobby, drafting their lances, or playing the campaign.</summary>
public enum LobbyPhase { Gathering, Drafting, Playing }

/// <summary>A player slot as the lobby shows it: the player's name (for a resumed campaign, the saved name until someone claims
/// it), whether a player holds it, and whether they have confirmed their draft.</summary>
public sealed record LobbySeat(int Slot, string? Name, bool Present, bool Drafted = false);

/// <summary>Everything a co-op session sends. Clients send commands; only the leader's machine changes the campaign.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$message")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(ClaimSeatMessage), "claim-seat")]
[JsonDerivedType(typeof(CommandMessage), "command")]
[JsonDerivedType(typeof(DraftSubmissionMessage), "draft-submission")]
[JsonDerivedType(typeof(RefusedMessage), "refused")]
[JsonDerivedType(typeof(LobbyMessage), "lobby")]
[JsonDerivedType(typeof(DraftOfferMessage), "draft-offer")]
[JsonDerivedType(typeof(CampaignMessage), "campaign")]
[JsonDerivedType(typeof(CommandRefusedMessage), "command-refused")]
[JsonDerivedType(typeof(NoticeMessage), "notice")]
[JsonDerivedType(typeof(DeploymentChoiceMessage), "deployment-choice")]
[JsonDerivedType(typeof(CombatCommandMessage), "combat-command")]
[JsonDerivedType(typeof(PingMessage), "ping")]
[JsonDerivedType(typeof(DeploymentMessage), "deployment")]
[JsonDerivedType(typeof(BattleStartMessage), "battle-start")]
[JsonDerivedType(typeof(BattleStateMessage), "battle-state")]
[JsonDerivedType(typeof(BattleResultMessage), "battle-result")]
[JsonDerivedType(typeof(BattleClosedMessage), "battle-closed")]
[JsonDerivedType(typeof(SessionEndedMessage), "session-ended")]
public abstract record NetMessage;

/// <summary>A player's force for the mission being deployed, and whether they are ready to launch.</summary>
public sealed record DeploymentSeat(int Slot, string[] Mechs, bool Ready);

// Client to leader.
public sealed record HelloMessage(int Protocol, string Build, string Name) : NetMessage;
public sealed record ClaimSeatMessage(int Slot) : NetMessage;
public sealed record CommandMessage(CampaignCommand Command) : NetMessage;
public sealed record DraftSubmissionMessage(int[] Choices) : NetMessage;
/// <summary>The mechs a player deploys from their own lance, and whether they are ready.</summary>
public sealed record DeploymentChoiceMessage(string[] Mechs, bool Ready) : NetMessage;
public sealed record CombatCommandMessage(CombatCommand Command) : NetMessage;
/// <summary>A marker on the battlefield for every player. The leader fills in the slot of the player who placed it.</summary>
public sealed record PingMessage(int Slot, Vector2 Point) : NetMessage;

// Leader to clients.
/// <summary>The leader will not seat this client; the client disconnects.</summary>
public sealed record RefusedMessage(string Reason) : NetMessage;
/// <summary>The seats, the phase, and the receiving client's own slot once it has one.</summary>
public sealed record LobbyMessage(LobbySeat[] Seats, LobbyPhase Phase, int? Slot) : NetMessage;
public sealed record DraftOfferMessage(DraftOffer Draft) : NetMessage;
public sealed record CampaignMessage(CampaignSnapshot Snapshot) : NetMessage;
public sealed record CommandRefusedMessage(string Reason) : NetMessage;
/// <summary>Something every player is told, such as the day's events or a mission's settlement.</summary>
public sealed record NoticeMessage(string Title, string Body) : NetMessage;
/// <summary>The leader has opened force selection for the current mission: every player's choice so far.</summary>
public sealed record DeploymentMessage(DeploymentSeat[] Seats) : NetMessage;
/// <summary>The battle begins: the deployed mechs in order, and the seeds that recreate the battlefield.</summary>
public sealed record BattleStartMessage(string[] Force, int Seed, int TerrainSeed) : NetMessage;
public sealed record BattleStateMessage(CombatState State) : NetMessage;
/// <summary>The leader has settled the mission into the campaign.</summary>
public sealed record BattleResultMessage(string Settlement) : NetMessage;
/// <summary>The leader closed force selection or the battle's results; every player returns to the hangar.</summary>
public sealed record BattleClosedMessage : NetMessage;
/// <summary>The leader ended the co-op game; every player returns to the main menu with the reason.</summary>
public sealed record SessionEndedMessage(string Reason) : NetMessage;

/// <summary>Encodes messages as compressed JSON. Both ends must run the same build.</summary>
public static class NetProtocol
{
    public const int Version = 1;
    public const int DefaultPort = 24525;
    /// <summary>Largest decoded message accepted; a campaign snapshot is well under a megabyte.</summary>
    public const int MaxMessageBytes = 4 * 1024 * 1024;
    /// <summary>Identifies the exact build: clients on any other build are refused.</summary>
    public static readonly string Build = typeof(NetProtocol).Assembly.ManifestModule.ModuleVersionId.ToString("N");
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(), new Vector2Converter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static byte[] Encode(NetMessage message)
    {
        using var output = new MemoryStream();
        using (var zip = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
            JsonSerializer.Serialize(zip, message, Options);
        return output.ToArray();
    }

    /// <summary>Decodes a message, or returns null for anything malformed or oversized.</summary>
    public static NetMessage? Decode(byte[] data)
    {
        try
        {
            using var zip = new BrotliStream(new MemoryStream(data), CompressionMode.Decompress);
            using var limited = new MemoryStream();
            var buffer = new byte[81920];
            for (int read; (read = zip.Read(buffer)) > 0;)
            {
                if (limited.Length + read > MaxMessageBytes) return null;
                limited.Write(buffer, 0, read);
            }
            limited.Position = 0;
            return JsonSerializer.Deserialize<NetMessage>(limited, Options);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}

public enum NetEventKind { Connected, Disconnected, Data }
/// <summary>A connection change or a packet from a peer. A client's only peer is the leader.</summary>
public readonly record struct NetEvent(NetEventKind Kind, int Peer, byte[]? Data = null);

/// <summary>Reliable ordered packets between the leader and the other players.</summary>
public interface INetTransport : IDisposable
{
    IReadOnlyList<NetEvent> Poll();
    void Send(int peer, byte[] data);
}

/// <summary>Writes a vector as [x, y]: Godot's vectors keep their components in fields, which JSON skips by default.</summary>
public sealed class Vector2Converter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected a vector.");
        reader.Read(); var x = JsonSerializer.Deserialize<float>(ref reader, options);
        reader.Read(); var y = JsonSerializer.Deserialize<float>(ref reader, options);
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray) throw new JsonException("Expected a vector.");
        return new(x, y);
    }
    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        JsonSerializer.Serialize(writer, value.X, options); JsonSerializer.Serialize(writer, value.Y, options);
        writer.WriteEndArray();
    }
}
