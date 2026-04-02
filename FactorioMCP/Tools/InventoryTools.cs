using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for inventory management and crafting. Crafting uses the real crafting queue
/// so the player must wait for items to finish.
/// </summary>
[McpServerToolType]
internal sealed class InventoryTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description("Get the contents of the player's main inventory, listing all items and their counts.")]
    public Task<string> GetInventory(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetInventory), factorio.GetInventoryAsync, cancellationToken);
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
        return queue.ExecuteAsync(nameof(Craft), ct => factorio.CraftAsync(recipe, count, ct), cancellationToken);
    }

    [McpServerTool, Description("Get the player's current crafting queue showing what is being crafted and how many.")]
    public Task<string> GetCraftingQueue(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetCraftingQueue), factorio.GetCraftingQueueAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Drop items from the player's inventory onto the ground at the player's position. " +
        "Items are scattered on the ground and can be picked up by the player or bots. " +
        "If the player has fewer items than requested, drops as many as available.")]
    public Task<string> DropItems(
        [Description("Item name to drop (e.g. 'iron-plate', 'wood', 'coal')")]
        string itemName,
        [Description("Number of items to drop")]
        int count,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(DropItems), ct => factorio.DropItemsAsync(itemName, count, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Transfer all items from an entity's inventory into the player's inventory. " +
        "Use this to quickly empty a chest, furnace, or assembler. " +
        "Only transfers items that fit in the player's inventory.")]
    public Task<string> TransferAllItems(
        [Description("X coordinate of the entity")]
        double x,
        [Description("Y coordinate of the entity")]
        double y,
        [Description("Source inventory: fuel, furnace_source, furnace_result, chest, assembling_machine_input, assembling_machine_output")]
        string inventoryType = "chest",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(TransferAllItems), ct => factorio.TransferAllItemsAsync(x, y, inventoryType, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Get the contents of a specific entity's inventory at a position. " +
        "Returns all items, counts, total slots, and empty slots. " +
        "Use this to check what a chest contains or how full a furnace is.")]
    public Task<string> GetEntityInventory(
        [Description("X coordinate of the entity")]
        double x,
        [Description("Y coordinate of the entity")]
        double y,
        [Description("Inventory to inspect: fuel, furnace_source, furnace_result, chest, assembling_machine_input, assembling_machine_output")]
        string inventoryType = "chest",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetEntityInventory), ct => factorio.GetEntityInventoryAsync(x, y, inventoryType, ct), cancellationToken);
    }
}
