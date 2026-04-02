using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for inspecting electric networks and entity power status.
/// Uses electric pole network statistics for network-wide data and entity
/// properties for per-entity power diagnostics.
/// </summary>
[McpServerToolType]
internal sealed class EnergyTools(EnergyService energy)
{
    [McpServerTool, Description(
        "Get electric network statistics from the nearest electric pole within a radius. " +
        "Returns power production and consumption rates (watts) by entity type, " +
        "overall satisfaction percentage, and accumulator charge levels. " +
        "Use this to monitor power supply/demand balance and diagnose brownouts.")]
    public Task<string> GetElectricNetwork(
        [Description("Search radius around the player to find electric poles. Default 50 tiles.")]
        double radius = 50,
        CancellationToken cancellationToken = default)
    {
        return energy.GetElectricNetworkAsync(radius, cancellationToken);
    }

    [McpServerTool, Description(
        "Inspect the power status of an entity at specific coordinates. " +
        "Returns whether the entity is connected to an electric network, " +
        "energy stored, buffer size, drain rate, and generation rate. " +
        "Use this to diagnose why a specific machine has no power.")]
    public Task<string> InspectEntityPower(
        [Description("X coordinate of the entity to inspect")]
        double x,
        [Description("Y coordinate of the entity to inspect")]
        double y,
        CancellationToken cancellationToken = default)
    {
        return energy.InspectEntityPowerAsync(x, y, cancellationToken);
    }
}
