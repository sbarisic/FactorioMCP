using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for realistic player movement. The player walks using game physics —
/// no teleportation allowed.
/// </summary>
[McpServerToolType]
internal sealed class MovementTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Walk in a direction for a specified duration (in seconds), then stop. " +
        "The player moves using real game physics. Returns the player's position after walking. " +
        "Includes automatic obstacle avoidance — if the player gets stuck on an entity, " +
        "the walking handler will try perpendicular directions to navigate around it.")]
    public Task<string> WalkForDuration(
        [Description("Direction to walk: north, south, east, west, northeast, northwest, southeast, southwest")]
        string direction,
        [Description("Duration to walk in seconds (e.g. 2.5)")]
        double seconds,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(WalkForDuration), async ct =>
        {
            var startResult = await factorio.GetPlayerPositionAsync(ct);
            await factorio.WalkAsync(direction, ct);
            await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            await factorio.StopWalkingAsync(ct);
            var endResult = await factorio.GetPlayerPositionAsync(ct);

            // Parse start and end positions to detect if the player was completely stuck
            try
            {
                var startJson = System.Text.Json.JsonDocument.Parse(startResult).RootElement;
                var endJson = System.Text.Json.JsonDocument.Parse(endResult).RootElement;
                var sx = startJson.GetProperty("x").GetDouble();
                var sy = startJson.GetProperty("y").GetDouble();
                var ex = endJson.GetProperty("x").GetDouble();
                var ey = endJson.GetProperty("y").GetDouble();
                var distMoved = Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy));
                if (distMoved < 0.1)
                    return $$"""{"status":"stuck","x":{{ex.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"y":{{ey.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"distance_moved":{{distMoved.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}},"warning":"Player could not move — likely fully blocked by entities. Try a different direction or clear obstacles."}""";
            }
            catch
            {
                // If parsing fails, just return the position
            }

            return endResult;
        }, cancellationToken);
    }

    [McpServerTool, Description("Stop the player from walking immediately.")]
    public Task<string> StopWalking(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(StopWalking), async ct =>
        {
            await factorio.StopWalkingAsync(ct);
            return "Stopped walking.";
        }, cancellationToken);
    }

    [McpServerTool, Description("Get the player's current map position as x,y coordinates.")]
    public Task<string> GetPlayerPosition(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetPlayerPosition), factorio.GetPlayerPositionAsync, cancellationToken);
    }
}
