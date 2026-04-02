using FactorioMCP.Rcon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactorioMCP.Services;

/// <summary>
/// Hosted service that connects and authenticates the RCON client on application startup.
/// Reads connection settings from environment variables.
/// Retries connection with exponential backoff if the server is not yet available.
/// After connecting, initializes game-level listeners (e.g. chat message capture).
/// </summary>
internal sealed class RconConnectionService(
    RconClient rcon,
    FactorioService factorioService,
    IConfiguration configuration,
    ILogger<RconConnectionService> logger) : IHostedService
{
    private const int MaxStartupAttempts = 5;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(2);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var host = configuration["FACTORIO_RCON_HOST"] ?? "127.0.0.1";
        var port = int.Parse(configuration["FACTORIO_RCON_PORT"] ?? "27015");
        var password = configuration["FACTORIO_RCON_PASSWORD"] ?? "mypassword";

        var delay = InitialBackoff;
        for (var attempt = 1; attempt <= MaxStartupAttempts; attempt++)
        {
            try
            {
                await rcon.ConnectAndAuthenticateAsync(host, port, password, cancellationToken);
                logger.LogInformation("RCON connected to {Host}:{Port}", host, port);

                await InitializeGameListenersAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxStartupAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "RCON connection attempt {Attempt}/{Max} failed. Retrying in {Delay}s...",
                    attempt, MaxStartupAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
        }

        // Final attempt — let the exception propagate
        await rcon.ConnectAndAuthenticateAsync(host, port, password, cancellationToken);
        await InitializeGameListenersAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitializeGameListenersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await factorioService.InitializeChatListenerAsync(cancellationToken);
            logger.LogInformation("Chat message listener initialized");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize chat listener — chat tools may not work until manually re-initialized");
        }
    }
}
