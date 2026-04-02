using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactorioMCP.Services;

/// <summary>
/// Serializes game operations to prevent race conditions when multiple
/// MCP tool calls arrive concurrently. Ensures one game operation
/// completes before the next begins, so multi-step operations
/// (e.g. walk + delay + stop) cannot interleave with other commands.
/// </summary>
internal sealed class GameCommandQueue : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<GameCommandQueue> _logger;

    public GameCommandQueue(ILogger<GameCommandQueue>? logger = null)
    {
        _logger = logger ?? NullLogger<GameCommandQueue>.Instance;
    }

    /// <summary>
    /// Execute an operation exclusively — no other queued operation can run concurrently.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Executing: {Operation}", operationName);
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
