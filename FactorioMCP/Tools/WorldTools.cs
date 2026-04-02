using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for scanning the world — nearby entities, distance checks, research status.
/// All results come from rcon.print() as structured text.
/// </summary>
[McpServerToolType]
internal sealed class WorldTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get a list of all entities near the player within a given radius. " +
        "Returns each entity's name and position.")]
    public Task<string> GetNearbyEntities(
        [Description("Search radius around the player in tiles (default 10)")]
        double radius = 10,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetNearbyEntities), ct => factorio.GetNearbyEntitiesAsync(radius, ct), cancellationToken);
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
        return queue.ExecuteAsync(nameof(CheckDistance), ct => factorio.CheckDistanceAsync(x, y, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Scan for resource patches (ores, oil, etc.) within a radius of the player. " +
        "Returns a summary of each resource type found: name, number of patches, total amount, " +
        "and approximate center coordinates. Useful for finding where to mine.")]
    public Task<string> ScanResources(
        [Description("Search radius around the player in tiles (default 50)")]
        double radius = 50,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ScanResources), ct => factorio.ScanResourcesAsync(radius, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Scan tiles around the player to get terrain information. " +
        "Returns a summary of tile types (grass, sand, water, dirt, etc.) and their counts " +
        "within the area. Useful for understanding the landscape before building.")]
    public Task<string> ScanTiles(
        [Description("Search radius around the player in tiles (default 16)")]
        double radius = 16,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ScanTiles), ct => factorio.ScanTilesAsync(radius, ct), cancellationToken);
    }
}
