namespace FactorioMCP.Rcon;

/// <summary>
/// Represents a decoded Source RCON protocol packet.
/// </summary>
internal sealed record RconPacket(int Id, RconPacketType Type, string Body);

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
