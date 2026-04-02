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
    public Task<string> WaitForCrafting(
        [Description("How often to check the queue in seconds (default 1.0)")]
        double pollIntervalSeconds = 1.0,
        [Description("Maximum time to wait in seconds before giving up (default 60)")]
        double timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(WaitForCrafting), ct => factorio.WaitForCraftingAsync(
            TimeSpan.FromSeconds(pollIntervalSeconds),
            TimeSpan.FromSeconds(timeoutSeconds),
            ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Wait until the player reaches a target position within a given tolerance. " +
        "Polls the player's position periodically. Use this after starting to walk toward a " +
        "destination to wait until arrival. The player must already be walking — this tool " +
        "only waits, it does not start movement.")]
    public Task<string> WaitForPosition(
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
        return queue.ExecuteAsync(nameof(WaitForPosition), ct => factorio.WaitForPositionAsync(
            targetX, targetY, tolerance,
            TimeSpan.FromSeconds(pollIntervalSeconds),
            TimeSpan.FromSeconds(timeoutSeconds),
            ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Wait for a specified number of game ticks to elapse. Factorio runs at 60 ticks per second " +
        "at normal speed (1x). Use this for precise timing when you need to wait for game mechanics " +
        "to process (e.g. furnace smelting, inserter cycles).")]
    public Task<string> WaitForTicks(
        [Description("Number of game ticks to wait (60 ticks = 1 second at normal speed)")]
        int ticks,
        [Description("How often to check the tick count in seconds (default 0.5)")]
        double pollIntervalSeconds = 0.5,
        [Description("Maximum real-time seconds to wait before giving up (default 30)")]
        double timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(WaitForTicks), ct => factorio.WaitForTicksAsync(
            ticks,
            TimeSpan.FromSeconds(pollIntervalSeconds),
            TimeSpan.FromSeconds(timeoutSeconds),
            ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Get the current game tick. Factorio runs at 60 ticks per second at normal speed. " +
        "Useful for measuring elapsed time between operations.")]
    public Task<string> GetGameTick(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetGameTick), factorio.GetGameTickAsync, cancellationToken);
    }
}
