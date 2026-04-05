using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for utility queries — world item counts and diagnostics.
/// </summary>
[McpServerToolType]
internal sealed class UtilityTools(FactorioService factorio, GameCommandQueue queue)
{
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
