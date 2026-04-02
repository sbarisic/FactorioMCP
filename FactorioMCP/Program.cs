using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton<RconClient>()
    .AddSingleton<FactorioService>()
    .AddSingleton<EnergyService>()
    .AddSingleton<BlueprintService>()
    .AddSingleton<GoalPlannerService>()
    .AddSingleton<BuildingMemoryService>()
    .AddSingleton<GameCommandQueue>()
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
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithRequestFilters(filters =>
    {
        McpRequestFilter<CallToolRequestParams, CallToolResult> filter = next =>
            async (context, cancellationToken) =>
            {
                var toolName = context.Params.Name;
                var args = context.Params.Arguments is { } a
                    ? JsonSerializer.Serialize(a)
                    : "{}";

                await Console.Error.WriteLineAsync($"[ToolCall] {toolName} {args}");

                var result = await next(context, cancellationToken);

                await Console.Error.WriteLineAsync($"[ToolCall] {toolName} completed");

                return result;
            };

        filters.AddCallToolFilter(filter);
    });

await builder.Build().RunAsync();
