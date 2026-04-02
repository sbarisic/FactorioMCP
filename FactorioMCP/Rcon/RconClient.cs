using System.Buffers.Binary;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactorioMCP.Rcon;

/// <summary>
/// Source RCON protocol client for communicating with a Factorio server over TCP.
/// Supports authentication, command execution with Lua via /c prefix,
/// and automatic reconnection with exponential backoff on connection loss.
/// </summary>
internal class RconClient : IAsyncDisposable, IDisposable
{
    private readonly ILogger<RconClient> _logger;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private int _nextRequestId;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Stored connection parameters for reconnection
    private string? _host;
    private int _port;
    private string? _password;

    private const int MaxReconnectAttempts = 3;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);

    public RconClient(ILogger<RconClient>? logger = null)
    {
        _logger = logger ?? NullLogger<RconClient>.Instance;
    }

    /// <summary>
    /// Whether the client is currently connected to the RCON server.
    /// </summary>
    public bool IsConnected => _tcp is { Connected: true } && _stream is not null;

    /// <summary>
    /// Connect to the RCON server and authenticate with the given password.
    /// Stores connection parameters for automatic reconnection on failure.
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

        _host = host;
        _port = port;
        _password = password;

        _logger.LogDebug("Connecting to RCON server at {Host}:{Port}", host, port);
        await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("RCON connected and authenticated at {Host}:{Port}", host, port);
    }

    /// <summary>
    /// Execute a raw command string on the server and return the response body.
    /// Automatically attempts reconnection on connection failure.
    /// </summary>
    public virtual async Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_stream is null || _host is null)
            {
                throw new InvalidOperationException("Not connected. Call ConnectAndAuthenticateAsync first.");
            }

            try
            {
                return await ExecuteCoreAsync(command, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsConnectionException(ex))
            {
                // Connection lost — attempt reconnection then retry the command
                _logger.LogWarning(ex, "RCON connection lost during command execution. Attempting reconnection");
                await ReconnectWithBackoffAsync(cancellationToken).ConfigureAwait(false);
                return await ExecuteCoreAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Execute a Lua command via Factorio's /silent-command (suppresses chat output).
    /// </summary>
    public Task<string> ExecuteLuaAsync(string luaCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(luaCode);
        return ExecuteAsync($"/silent-command {luaCode}", cancellationToken);
    }

    /// <summary>
    /// Create a fresh TCP connection and authenticate with stored credentials.
    /// </summary>
    private async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        CloseConnection();

        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host!, _port, cancellationToken).ConfigureAwait(false);
        _stream = _tcp.GetStream();

        var authId = Interlocked.Increment(ref _nextRequestId);
        await SendPacketAsync(authId, RconPacketType.Auth, _password!, cancellationToken).ConfigureAwait(false);
        var response = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);

        if (response.Id == -1)
        {
            throw new InvalidOperationException("RCON authentication failed. Check the password.");
        }
    }

    /// <summary>
    /// Send a command and read the response on an already-established connection.
    /// Factorio returns the complete response in a single packet.
    /// </summary>
    private async Task<string> ExecuteCoreAsync(string command, CancellationToken cancellationToken)
    {
        var commandId = Interlocked.Increment(ref _nextRequestId);

        await SendPacketAsync(commandId, RconPacketType.ExecCommand, command, cancellationToken).ConfigureAwait(false);

        var packet = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);

        return packet.Body;
    }

    /// <summary>
    /// Attempt to reconnect with exponential backoff, up to <see cref="MaxReconnectAttempts"/> times.
    /// </summary>
    /// <exception cref="IOException">Thrown when all reconnection attempts fail.</exception>
    private async Task ReconnectWithBackoffAsync(CancellationToken cancellationToken)
    {
        var delay = InitialBackoff;

        for (var attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
        {
            _logger.LogInformation(
                "RCON reconnection attempt {Attempt}/{Max} to {Host}:{Port}",
                attempt, MaxReconnectAttempts, _host, _port);

            try
            {
                await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("RCON reconnected successfully on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex) when (attempt < MaxReconnectAttempts && IsConnectionException(ex))
            {
                _logger.LogWarning(
                    ex,
                    "RCON reconnection attempt {Attempt}/{Max} failed. Retrying in {Delay}s",
                    attempt, MaxReconnectAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay *= 2; // Exponential backoff
            }
        }

        _logger.LogError(
            "RCON reconnection failed after {Max} attempts to {Host}:{Port}",
            MaxReconnectAttempts, _host, _port);

        throw new IOException(
            $"Failed to reconnect to RCON server at {_host}:{_port} after {MaxReconnectAttempts} attempts.");
    }

    /// <summary>
    /// Determines whether the exception indicates a connection-level failure
    /// that can potentially be recovered by reconnecting.
    /// </summary>
    private static bool IsConnectionException(Exception ex) =>
        ex is IOException or SocketException or ObjectDisposedException;

    /// <summary>
    /// Close and dispose the current TCP connection if one exists.
    /// </summary>
    private void CloseConnection()
    {
        if (_stream is not null || _tcp is not null)
        {
            _logger.LogDebug("Closing RCON connection");
        }

        _stream?.Dispose();
        _stream = null;
        _tcp?.Dispose();
        _tcp = null;
    }

    /// <summary>
    /// Build and send a single RCON packet over the wire.
    /// </summary>
    private async Task SendPacketAsync(
        int id,
        RconPacketType type,
        string body,
        CancellationToken cancellationToken)
    {
        var buffer = new RconPacket(id, type, body).ToBytes();

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

        var payload = new byte[size];
        await ReadExactAsync(payload, size, cancellationToken).ConfigureAwait(false);

        return RconPacket.FromPayload(payload);
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

        _tcp?.Dispose();
        _lock.Dispose();
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        _lock.Dispose();
    }
}
