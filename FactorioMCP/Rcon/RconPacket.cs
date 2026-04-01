using System.Buffers.Binary;
using System.Text;

namespace FactorioMCP.Rcon;

/// <summary>
/// Represents a decoded Source RCON protocol packet.
/// Provides serialization to/from the wire format.
/// </summary>
/// <remarks>
/// Wire format (all integers are little-endian):
/// <code>
/// [4 bytes size][4 bytes id][4 bytes type][body bytes][0x00][0x00]
/// </code>
/// where size = 4 (id) + 4 (type) + body.Length + 2 (null terminators).
/// </remarks>
internal sealed record RconPacket(int Id, RconPacketType Type, string Body)
{
    /// <summary>
    /// Minimum payload size: 4 (id) + 4 (type) + 2 (null terminators).
    /// </summary>
    internal const int MinPayloadSize = 10;

    /// <summary>
    /// Serialize this packet to the complete RCON wire format including the size prefix.
    /// </summary>
    public byte[] ToBytes()
    {
        var bodyBytes = Encoding.UTF8.GetBytes(Body);
        var packetSize = 4 + 4 + bodyBytes.Length + 2; // id + type + body + 2 null terminators
        var totalLength = 4 + packetSize; // 4 bytes for the size field itself

        var buffer = new byte[totalLength];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), packetSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), Id);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), (int)Type);
        bodyBytes.CopyTo(buffer, 12);
        buffer[totalLength - 2] = 0;
        buffer[totalLength - 1] = 0;

        return buffer;
    }

    /// <summary>
    /// Deserialize an RCON packet from a payload buffer (the bytes after the 4-byte size field).
    /// The payload must be at least <see cref="MinPayloadSize"/> bytes.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the payload is too small.</exception>
    public static RconPacket FromPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < MinPayloadSize)
        {
            throw new InvalidOperationException(
                $"RCON packet too small: {payload.Length} bytes (minimum {MinPayloadSize}).");
        }

        var id = BinaryPrimitives.ReadInt32LittleEndian(payload[..4]);
        var type = (RconPacketType)BinaryPrimitives.ReadInt32LittleEndian(payload[4..8]);
        var bodyLength = payload.Length - MinPayloadSize;
        var body = bodyLength > 0
            ? Encoding.UTF8.GetString(payload.Slice(8, bodyLength))
            : string.Empty;

        return new RconPacket(id, type, body);
    }
}

/// <summary>
/// Source RCON packet types.
/// </summary>
internal enum RconPacketType
{
    /// <summary>Response to an auth request or a command execution.</summary>
    ResponseValue = 0,

    /// <summary>Execute a command on the server.</summary>
    ExecCommand = 2,

    /// <summary>Authenticate with the server.</summary>
    Auth = 3,
}
