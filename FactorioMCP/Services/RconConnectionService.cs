using FactorioMCP.Rcon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FactorioMCP.Services;

/// <summary>
/// Hosted service that connects and authenticates the RCON client on application startup.
/// Reads connection settings from environment variables.
/// </summary>
internal sealed class RconConnectionService(RconClient rcon, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var host = configuration["FACTORIO_RCON_HOST"] ?? "127.0.0.1";
        var port = int.Parse(configuration["FACTORIO_RCON_PORT"] ?? "27015");
        var password = configuration["FACTORIO_RCON_PASSWORD"]
            ?? throw new InvalidOperationException("FACTORIO_RCON_PASSWORD environment variable is required.");

        await rcon.ConnectAndAuthenticateAsync(host, port, password, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
