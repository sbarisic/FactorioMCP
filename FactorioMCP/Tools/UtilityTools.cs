using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for utility queries — reachable entities, world item counts, and travel estimates.
/// </summary>
[McpServerToolType]
internal sealed class UtilityTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get entities within the player's reach distance, optionally filtered by entity type/name. " +
        "Useful for finding what the player can interact with right now without moving.")]
    public Task<string> GetReachableEntities(
        [Description("Optional entity type or name to filter by (e.g. 'stone-furnace', 'transport-belt')")]
        string? type = null,
        [Description("Maximum distance in tiles to search (defaults to player reach distance if omitted)")]
        double? maxDistance = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetReachableEntities), ct => factorio.GetReachableEntitiesAsync(type, maxDistance, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Count how many of a specific item exist across all nearby containers (chests, furnaces, assemblers) " +
        "and the player's inventory. Returns a breakdown showing where the items are located.")]
    public Task<string> CountItemInWorld(
        [Description("The item name to count (e.g. 'iron-plate', 'coal')")]
        string itemName,
        [Description("Search radius in tiles around the player (default 50)")]
        double radius = 50,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(CountItemInWorld), ct => factorio.CountItemInWorldAsync(itemName, radius, ct), cancellationToken);
    }
}
