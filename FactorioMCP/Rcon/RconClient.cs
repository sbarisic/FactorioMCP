using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace FactorioMCP.Rcon;

/// <summary>
/// Source RCON protocol client for communicating with a Factorio server over TCP.
/// Supports authentication and command execution with Lua via /c prefix.
/// </summary>
internal sealed class RconClient : IAsyncDisposable, IDisposable
{
    private readonly TcpClient _tcp = new();
    private NetworkStream? _stream;
    private int _nextRequestId;

    /// <summary>
    /// Whether the client is currently connected to the RCON server.
    /// </summary>
    public bool IsConnected => _tcp.Connected && _stream is not null;

    /// <summary>
    /// Connect to the RCON server and authenticate with the given password.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when authentication fails.</exception>
    public async Task ConnectAndAuthenticateAsync(
        string host,
        int port,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        await _tcp.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        _stream = _tcp.GetStream();

        var authId = Interlocked.Increment(ref _nextRequestId);
        await SendPacketAsync(authId, RconPacketType.Auth, password, cancellationToken).ConfigureAwait(false);
        var response = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);

        if (response.Id == -1)
        {
            throw new InvalidOperationException("RCON authentication failed. Check the password.");
        }
    }

    /// <summary>
    /// Execute a raw command string on the server and return the response body.
    /// </summary>
    public async Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_stream is null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAndAuthenticateAsync first.");
        }

        var id = Interlocked.Increment(ref _nextRequestId);
        await SendPacketAsync(id, RconPacketType.ExecCommand, command, cancellationToken).ConfigureAwait(false);
        var response = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);

        return response.Body;
    }

    /// <summary>
    /// Execute a Lua command via Factorio's /c console command.
    /// </summary>
    public Task<string> ExecuteLuaAsync(string luaCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(luaCode);
        return ExecuteAsync($"/c {luaCode}", cancellationToken);
    }

    /// <summary>
    /// Build and send a single RCON packet over the wire.
    /// Packet format (little-endian):
    ///   [4 bytes size][4 bytes id][4 bytes type][body bytes][0x00][0x00]
    /// where size = 4 (id) + 4 (type) + body.Length + 2 (null terminators).
    /// </summary>
    private async Task SendPacketAsync(
        int id,
        RconPacketType type,
        string body,
        CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var packetSize = 4 + 4 + bodyBytes.Length + 2; // id + type + body + 2 null terminators
        var totalLength = 4 + packetSize; // 4 bytes for the size field itself

        var buffer = new byte[totalLength];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), packetSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), id);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), (int)type);
        bodyBytes.CopyTo(buffer, 12);
        buffer[totalLength - 2] = 0;
        buffer[totalLength - 1] = 0;

        await _stream!.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Read and decode a single RCON response packet from the wire.
    /// </summary>
    private async Task<RconPacket> ReadPacketAsync(CancellationToken cancellationToken)
    {
        var sizeBuffer = new byte[4];
        await ReadExactAsync(sizeBuffer, 4, cancellationToken).ConfigureAwait(false);
        var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);

        if (size < 10)
        {
            throw new InvalidOperationException($"RCON packet too small: {size} bytes (minimum 10).");
        }

        var payload = new byte[size];
        await ReadExactAsync(payload, size, cancellationToken).ConfigureAwait(false);

        var id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4));
        var type = (RconPacketType)BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
        var bodyLength = size - 10; // subtract id(4) + type(4) + 2 null terminators
        var body = bodyLength > 0
            ? Encoding.UTF8.GetString(payload, 8, bodyLength)
            : string.Empty;

        return new RconPacket(id, type, body);
    }

    /// <summary>
    /// Read exactly <paramref name="count"/> bytes from the network stream.
    /// </summary>
    /// <exception cref="IOException">Thrown when the connection is closed before all bytes are read.</exception>
    private async Task ReadExactAsync(byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = await _stream!.ReadAsync(
                buffer.AsMemory(offset, count - offset),
                cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                throw new IOException("RCON connection closed unexpectedly.");
            }

            offset += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }

        _tcp.Dispose();
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _tcp.Dispose();
    }
}
