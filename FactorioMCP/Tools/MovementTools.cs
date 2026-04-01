using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for realistic player movement. The player walks using game physics —
/// no teleportation allowed.
/// </summary>
[McpServerToolType]
internal sealed class MovementTools(FactorioService factorio)
{
    [McpServerTool, Description(
        "Walk in a direction for a specified duration (in seconds), then stop. " +
        "The player moves using real game physics. Returns the player's position after walking.")]
    public async Task<string> WalkForDuration(
        [Description("Direction to walk: north, south, east, west, northeast, northwest, southeast, southwest")]
        string direction,
        [Description("Duration to walk in seconds (e.g. 2.5)")]
        double seconds,
        CancellationToken cancellationToken = default)
    {
        await factorio.WalkAsync(direction, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
        await factorio.StopWalkingAsync(cancellationToken);
        return await factorio.GetPlayerPositionAsync(cancellationToken);
    }

    [McpServerTool, Description("Stop the player from walking immediately.")]
    public async Task<string> StopWalking(CancellationToken cancellationToken = default)
    {
        await factorio.StopWalkingAsync(cancellationToken);
        return "Stopped walking.";
    }

    [McpServerTool, Description("Get the player's current map position as x,y coordinates.")]
    public Task<string> GetPlayerPosition(CancellationToken cancellationToken = default)
    {
        return factorio.GetPlayerPositionAsync(cancellationToken);
    }
}
