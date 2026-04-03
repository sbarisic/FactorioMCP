using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for finding the nearest entity, best resource patch, and closest tracked building.
/// Eliminates token-expensive scan→parse→compare→decide chains by returning the best target directly.
/// </summary>
[McpServerToolType]
internal sealed class TargetTools(FactorioService factorio, BuildingMemoryService buildingMemory, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Find the nearest entity matching a name or type within a search radius. " +
        "Searches by exact entity name first (e.g. 'stone-furnace', 'iron-ore'), " +
        "then falls back to entity type (e.g. 'furnace', 'resource', 'inserter', 'transport-belt'). " +
        "Returns the closest match with position, distance, direction, and total count found. " +
        "Much faster than scanning all entities and manually comparing distances.")]
    public Task<string> FindNearest(
        [Description("Entity name or type to search for (e.g. 'stone-furnace', 'iron-ore', 'resource', 'inserter')")]
        string entityType,
        [Description("Search radius in tiles (default 100)")]
        double radius = 100,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(FindNearest),
            ct => factorio.FindNearestEntityAsync(entityType, radius, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Find the best resource patch of a specific resource type. " +
        "Groups nearby resource entities into patches and ranks them using a heuristic " +
        "that balances distance and richness (amount / (distance + 10)). " +
        "Returns the best patch center, total amount, entity count, and up to 3 alternatives. " +
        "Use this instead of ScanResources when you need to pick the optimal mining location.")]
    public Task<string> FindBestResourcePatch(
        [Description("Resource entity name (e.g. 'iron-ore', 'copper-ore', 'stone', 'coal', 'crude-oil')")]
        string resourceName,
        [Description("Search radius in tiles (default 200)")]
        double radius = 200,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(FindBestResourcePatch),
            ct => factorio.FindBestResourcePatchAsync(resourceName, radius, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Find the closest tracked building of a specific type from the AI's building memory. " +
        "Only searches buildings the AI has placed (tracked in memory), not all world entities. " +
        "Returns the closest match with position, direction, label, distance, and up to 3 other matches. " +
        "Use FindNearest for searching all world entities including ones not placed by the AI.")]
    public Task<string> GetClosestBuildingOfType(
        [Description("Entity name to search for (e.g. 'stone-furnace', 'burner-mining-drill', 'wooden-chest')")]
        string entityName,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetClosestBuildingOfType), async ct =>
        {
            // Get player position first for distance calculation
            var posJson = await factorio.GetPlayerPositionAsync(ct);
            var (playerX, playerY) = ParsePlayerPosition(posJson);
            return await buildingMemory.GetClosestBuildingOfTypeAsync(entityName, playerX, playerY, ct);
        }, cancellationToken);
    }

    private static (double x, double y) ParsePlayerPosition(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var x = root.GetProperty("x").GetDouble();
            var y = root.GetProperty("y").GetDouble();
            return (x, y);
        }
        catch (JsonException)
        {
            return (0, 0);
        }
    }
}
