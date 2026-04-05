using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for waiting and polling game state. These enable realistic pacing
/// so the AI waits for operations to complete instead of spamming commands.
/// </summary>
[McpServerToolType]
internal sealed class WaitTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Wait for the crafting queue to empty. Polls the queue periodically until all items " +
        "are crafted or the timeout is reached. Use this after calling Craft to wait for items " +
        "to finish before using them.")]
    public async Task<string> WaitForCrafting(
        [Description("How often to check the queue in seconds (default 1.0)")]
        double pollIntervalSeconds = 1.0,
        [Description("Maximum time to wait in seconds before giving up (default 60)")]
        double timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await queue.ExecuteAsync(nameof(WaitForCrafting), ct => factorio.WaitForCraftingAsync(
                TimeSpan.FromSeconds(pollIntervalSeconds),
                TimeSpan.FromSeconds(timeoutSeconds),
                ct), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return """{"status":"timeout","reason":"cancelled"}""";        }
    }

    [McpServerTool, Description(
        "Wait until the player reaches a target position within a given tolerance. " +
        "Polls the player's position periodically. Use this after starting to walk toward a " +
        "destination to wait until arrival. The player must already be walking — this tool " +
        "only waits, it does not start movement.")]
    public async Task<string> WaitForPosition(
        [Description("Target X coordinate to reach")]
        double targetX,
        [Description("Target Y coordinate to reach")]
        double targetY,
        [Description("Distance tolerance — arrived when within this many tiles of the target (default 2.0)")]
        double tolerance = 2.0,
        [Description("How often to check position in seconds (default 0.5)")]
        double pollIntervalSeconds = 0.5,
        [Description("Maximum time to wait in seconds before giving up (default 30)")]
        double timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await queue.ExecuteAsync(nameof(WaitForPosition), ct => factorio.WaitForPositionAsync(
                targetX, targetY, tolerance,
                TimeSpan.FromSeconds(pollIntervalSeconds),
                TimeSpan.FromSeconds(timeoutSeconds),
                ct), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return """{"status":"timeout","reason":"cancelled"}""";        }
    }

    [McpServerTool, Description(
        "Wait until the player's inventory contains at least the specified count of an item. " +
        "Use this after starting crafting or mining to reactively wait for items to appear " +
        "instead of polling GetInventory repeatedly.")]
    public async Task<string> WaitForItemCount(
        [Description("Internal item name (e.g. 'iron-plate', 'electronic-circuit')")]
        string itemName,
        [Description("Minimum item count to wait for")]
        int targetCount,
        [Description("How often to check inventory in seconds (default 1.0)")]
        double pollIntervalSeconds = 1.0,
        [Description("Maximum time to wait in seconds before giving up (default 60)")]
        double timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await queue.ExecuteAsync(nameof(WaitForItemCount), ct => factorio.WaitForItemCountAsync(
                itemName, targetCount,
                TimeSpan.FromSeconds(pollIntervalSeconds),
                TimeSpan.FromSeconds(timeoutSeconds),
                ct), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return """{"status":"timeout","reason":"cancelled"}""";        }
    }

    [McpServerTool, Description(
        "Wait until an entity at the specified position reaches a target status. " +
        "Status names follow defines.entity_status: 'working', 'no_fuel', 'no_power', " +
        "'item_ingredient_shortage', 'no_recipe', 'output_full', 'idle', etc. " +
        "Use this to reactively wait for machines to finish or detect problems.")]
    public async Task<string> WaitForEntityStatus(
        [Description("X coordinate of the entity")]
        double x,
        [Description("Y coordinate of the entity")]
        double y,
        [Description("Target status name (e.g. 'working', 'idle', 'no_fuel')")]
        string targetStatus,
        [Description("How often to check entity status in seconds (default 1.0)")]
        double pollIntervalSeconds = 1.0,
        [Description("Maximum time to wait in seconds before giving up (default 60)")]
        double timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await queue.ExecuteAsync(nameof(WaitForEntityStatus), ct => factorio.WaitForEntityStatusAsync(
                x, y, targetStatus,
                TimeSpan.FromSeconds(pollIntervalSeconds),
                TimeSpan.FromSeconds(timeoutSeconds),
                ct), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return """{"status":"timeout","reason":"cancelled"}""";        }
    }

    [McpServerTool, Description(
        "Wait until an entity's inventory contains at least the specified count of an item. " +
        "Use this to wait for furnaces to finish smelting, assemblers to produce output, or " +
        "chests to accumulate items. Inventory types: 'fuel', 'furnace_source', 'furnace_result', " +
        "'chest', 'assembling_machine_input', 'assembling_machine_output'.")]
    public async Task<string> WaitForEntityInventory(
        [Description("X coordinate of the entity")]
        double x,
        [Description("Y coordinate of the entity")]
        double y,
        [Description("Internal item name to check for (e.g. 'iron-plate')")]
        string itemName,
        [Description("Minimum item count to wait for")]
        int targetCount,
        [Description("Inventory type: 'fuel', 'furnace_source', 'furnace_result', 'chest', 'assembling_machine_input', 'assembling_machine_output'")]
        string inventoryType = "chest",
        [Description("How often to check inventory in seconds (default 1.0)")]
        double pollIntervalSeconds = 1.0,
        [Description("Maximum time to wait in seconds before giving up (default 60)")]
        double timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await queue.ExecuteAsync(nameof(WaitForEntityInventory), ct => factorio.WaitForEntityInventoryAsync(
                x, y, itemName, targetCount, inventoryType,
                TimeSpan.FromSeconds(pollIntervalSeconds),
                TimeSpan.FromSeconds(timeoutSeconds),
                ct), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return """{"status":"timeout","reason":"cancelled"}""";        }
    }
}
