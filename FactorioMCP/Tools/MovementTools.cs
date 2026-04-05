using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for realistic player movement. The player walks using game physics —
/// no teleportation allowed. Uses Factorio's built-in A* pathfinder for collision-aware navigation.
/// </summary>
[McpServerToolType]
internal sealed class MovementTools(PathfindingService pathfinding, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Walk to a target position using Factorio's built-in A* pathfinder. " +
        "Automatically finds a collision-free path around obstacles (buildings, water, cliffs). " +
        "Draws a debug path on the map for visualization. " +
        "Returns status: arrived, stuck, timeout, or no_path if unreachable.")]
    public Task<string> WalkToPosition(
        [Description("Target X coordinate to walk toward")]
        double targetX,
        [Description("Target Y coordinate to walk toward")]
        double targetY,
        [Description("Distance tolerance — arrived when within this many tiles of the target (default 2.0)")]
        double tolerance = 2.0,
        [Description("Maximum time to walk in seconds before giving up (default 30)")]
        double timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(WalkToPosition), ct =>
            pathfinding.WalkToAsync(targetX, targetY, tolerance, timeoutSeconds, ct),
            cancellationToken);
    }

    [McpServerTool, Description("Get the player's current map position as x,y coordinates.")]
    public Task<string> GetPlayerPosition(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetPlayerPosition), pathfinding.GetPlayerPositionAsync, cancellationToken);
    }
}
