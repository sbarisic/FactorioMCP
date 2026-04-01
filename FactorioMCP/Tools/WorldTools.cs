using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for scanning the world — nearby entities, distance checks, research status.
/// All results come from rcon.print() as structured text.
/// </summary>
[McpServerToolType]
internal sealed class WorldTools(FactorioService factorio)
{
    [McpServerTool, Description(
        "Get a list of all entities near the player within a given radius. " +
        "Returns each entity's name and position.")]
    public Task<string> GetNearbyEntities(
        [Description("Search radius around the player in tiles (default 10)")]
        double radius = 10,
        CancellationToken cancellationToken = default)
    {
        return factorio.GetNearbyEntitiesAsync(radius, cancellationToken);
    }

    [McpServerTool, Description(
        "Check the distance from the player to target map coordinates and whether the target " +
        "is within build range (for placing entities) and reach range (for mining/interacting). " +
        "Use this before PlaceEntity or MineEntity to verify the player is close enough.")]
    public Task<string> CheckDistance(
        [Description("X coordinate of the target position")]
        double x,
        [Description("Y coordinate of the target position")]
        double y,
        CancellationToken cancellationToken = default)
    {
        return factorio.CheckDistanceAsync(x, y, cancellationToken);
    }

    [McpServerTool, Description(
        "Get the current research status and progress for the player's force. " +
        "Shows the technology being researched and its completion percentage.")]
    public Task<string> GetResearchStatus(CancellationToken cancellationToken = default)
    {
        return factorio.GetResearchStatusAsync(cancellationToken);
    }
}
