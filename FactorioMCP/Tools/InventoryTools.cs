using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for inventory management and crafting. Crafting uses the real crafting queue
/// so the player must wait for items to finish.
/// </summary>
[McpServerToolType]
internal sealed class InventoryTools(FactorioService factorio)
{
    [McpServerTool, Description("Get the contents of the player's main inventory, listing all items and their counts.")]
    public Task<string> GetInventory(CancellationToken cancellationToken = default)
    {
        return factorio.GetInventoryAsync(cancellationToken);
    }

    [McpServerTool, Description(
        "Begin crafting items using a recipe. Respects real crafting time — " +
        "the items are queued and the player must wait for them to finish.")]
    public Task<string> Craft(
        [Description("Recipe name to craft (e.g. 'iron-gear-wheel', 'electronic-circuit')")]
        string recipe,
        [Description("Number of items to craft")]
        int count,
        CancellationToken cancellationToken = default)
    {
        return factorio.CraftAsync(recipe, count, cancellationToken);
    }

    [McpServerTool, Description("Get the player's current crafting queue showing what is being crafted and how many.")]
    public Task<string> GetCraftingQueue(CancellationToken cancellationToken = default)
    {
        return factorio.GetCraftingQueueAsync(cancellationToken);
    }
}
