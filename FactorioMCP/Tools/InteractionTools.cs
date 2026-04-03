using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for interacting with placed entities — inserting/removing items,
/// reading machine status, and inspecting entity inventories.
/// </summary>
[McpServerToolType]
internal sealed class InteractionTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Insert items from the player's inventory into a machine/entity at the specified position. " +
        "Use this to fuel burner entities, load furnaces with ore, or stock assembling machines.")]
    public Task<string> InsertItems(
        [Description("X coordinate of the target entity")]
        double x,
        [Description("Y coordinate of the target entity")]
        double y,
        [Description("Item name to insert (e.g. 'coal', 'iron-ore', 'copper-plate')")]
        string itemName,
        [Description("Number of items to insert")]
        int count,
        [Description("Target inventory: fuel, furnace_source, furnace_result, chest, assembling_machine_input, assembling_machine_output")]
        string inventoryType = "fuel",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(InsertItems), ct => factorio.InsertItemsAsync(x, y, itemName, count, inventoryType, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Remove items from a machine/entity's inventory at the specified position into the player's inventory. " +
        "Use this to collect smelted plates from furnaces, take crafted items from assemblers, or empty chests.")]
    public Task<string> RemoveItems(
        [Description("X coordinate of the target entity")]
        double x,
        [Description("Y coordinate of the target entity")]
        double y,
        [Description("Item name to remove (e.g. 'iron-plate', 'copper-plate', 'electronic-circuit')")]
        string itemName,
        [Description("Number of items to remove")]
        int count,
        [Description("Source inventory: fuel, furnace_source, furnace_result, chest, assembling_machine_input, assembling_machine_output")]
        string inventoryType = "furnace_result",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(RemoveItems), ct => factorio.RemoveItemsAsync(x, y, itemName, count, inventoryType, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Inspect an entity at the specified position to see its status, direction, inventory contents, " +
        "fuel level, recipe, and other details. Use this to check if a furnace is working, " +
        "what a machine is producing, or how much fuel remains. " +
        "For inserters, also shows pickup/drop tile positions and what entities are at each side — " +
        "use this to verify an inserter is correctly oriented after placement.")]
    public Task<string> InspectEntity(
        [Description("X coordinate of the entity to inspect")]
        double x,
        [Description("Y coordinate of the entity to inspect")]
        double y,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(InspectEntity), ct => factorio.InspectEntityAsync(x, y, ct), cancellationToken);
    }
}
