using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton<RconClient>()
    .AddSingleton<FactorioService>()
    .AddHostedService<RconConnectionService>()
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "FactorioMCP",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
