using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for scanning the world — entities, resources, tiles, and distance checks.
/// Supports both player-centered and remote area scanning via optional center coordinates.
/// </summary>
[McpServerToolType]
internal sealed class WorldTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get a list of all entities within a given radius. " +
        "Defaults to scanning around the player. Provide centerX/centerY to scan a remote area without walking there.")]
    public Task<string> GetNearbyEntities(
        [Description("Search radius in tiles (default 10)")]
        double radius = 10,
        [Description("Optional X coordinate to center the scan on (omit to use player position)")]
        double? centerX = null,
        [Description("Optional Y coordinate to center the scan on (omit to use player position)")]
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetNearbyEntities), ct => factorio.GetNearbyEntitiesAsync(radius, centerX, centerY, ct), cancellationToken);
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
        "Scan for resource patches (ores, oil, etc.) within a radius. " +
        "Returns a summary of each resource type found: name, number of patches, total amount, " +
        "and approximate center coordinates. Defaults to scanning around the player. " +
        "Provide centerX/centerY to scan a remote area without walking there.")]
    public Task<string> ScanResources(
        [Description("Search radius in tiles (default 50)")]
        double radius = 50,
        [Description("Optional X coordinate to center the scan on (omit to use player position)")]
        double? centerX = null,
        [Description("Optional Y coordinate to center the scan on (omit to use player position)")]
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ScanResources), ct => factorio.ScanResourcesAsync(radius, centerX, centerY, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Scan tiles to get terrain information. " +
        "Returns a summary of tile types (grass, sand, water, dirt, etc.) and their counts. " +
        "Defaults to scanning around the player. Provide centerX/centerY to scan a remote area without walking there.")]
    public Task<string> ScanTiles(
        [Description("Search radius in tiles (default 16)")]
        double radius = 16,
        [Description("Optional X coordinate to center the scan on (omit to use player position)")]
        double? centerX = null,
        [Description("Optional Y coordinate to center the scan on (omit to use player position)")]
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ScanTiles), ct => factorio.ScanTilesAsync(radius, centerX, centerY, ct), cancellationToken);
    }
}
