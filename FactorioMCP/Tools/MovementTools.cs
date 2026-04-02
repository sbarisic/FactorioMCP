using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for realistic player movement. The player walks using game physics —
/// no teleportation allowed.
/// </summary>
[McpServerToolType]
internal sealed class MovementTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Walk toward a target position until arrival, getting stuck, or timeout. " +
        "Automatically calculates the best walking direction from the player's current position. " +
        "Combines walking, direction calculation, position polling, and stuck detection into a single call. " +
        "The walking handler includes automatic obstacle avoidance. " +
        "Re-calculates direction periodically to correct course as the player moves.")]
    public Task<string> WalkToPosition(
        [Description("Target X coordinate to walk toward")]
        double targetX,
        [Description("Target Y coordinate to walk toward")]
        double targetY,
        [Description("Distance tolerance — arrived when within this many tiles of the target (default 2.0)")]
        double tolerance = 2.0,
        [Description("How often to check position and adjust direction in seconds (default 0.5)")]
        double pollIntervalSeconds = 0.5,
        [Description("Maximum time to walk in seconds before giving up (default 30)")]
        double timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(WalkToPosition), async ct =>
        {
            var pollInterval = TimeSpan.FromSeconds(Math.Max(pollIntervalSeconds, 0.1));
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 0.5));

            // Get initial position and check if already there
            var posJson = await factorio.GetPlayerPositionAsync(ct);
            var (px, py) = ParsePosition(posJson);
            var dist = Distance(px, py, targetX, targetY);

            if (dist <= tolerance)
                return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);

            // Start walking in the calculated direction
            var direction = CalculateDirection(px, py, targetX, targetY);
            await factorio.WalkAsync(direction, ct);

            double prevX = px, prevY = py;
            int stuckPolls = 0;
            const int maxStuckPolls = 6; // 6 polls with no movement = stuck
            const double minMovement = 0.15; // minimum distance per poll to count as moving

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(pollInterval, ct);

                    posJson = await factorio.GetPlayerPositionAsync(ct);
                    (px, py) = ParsePosition(posJson);
                    dist = Distance(px, py, targetX, targetY);

                    // Check if arrived
                    if (dist <= tolerance)
                    {
                        await factorio.StopWalkingAsync(ct);
                        return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);
                    }

                    // Check if stuck (not moving between polls)
                    var pollMovement = Distance(px, py, prevX, prevY);
                    if (pollMovement < minMovement)
                    {
                        stuckPolls++;
                        if (stuckPolls >= maxStuckPolls)
                        {
                            await factorio.StopWalkingAsync(ct);
                            return FormatResult("stuck", px, py, targetX, targetY, dist, tolerance);
                        }
                    }
                    else
                    {
                        stuckPolls = 0;
                    }

                    // Re-calculate direction to correct course
                    var newDirection = CalculateDirection(px, py, targetX, targetY);
                    if (newDirection != direction)
                    {
                        direction = newDirection;
                        await factorio.WalkAsync(direction, ct);
                    }

                    prevX = px;
                    prevY = py;
                }
            }
            catch
            {
                // Ensure we stop walking on any exception
                try { await factorio.StopWalkingAsync(CancellationToken.None); } catch { }
                throw;
            }

            // Timeout
            await factorio.StopWalkingAsync(ct);
            posJson = await factorio.GetPlayerPositionAsync(ct);
            (px, py) = ParsePosition(posJson);
            dist = Distance(px, py, targetX, targetY);
            return FormatResult("timeout", px, py, targetX, targetY, dist, tolerance);
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

    /// <summary>
    /// Calculate the best 8-direction compass direction from one position to another.
    /// Uses the angle between the two points to pick the closest of the 8 directions.
    /// </summary>
    internal static string CalculateDirection(double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;

        // atan2 with Factorio coordinates: +X = east, +Y = south
        var angle = Math.Atan2(dy, dx) * (180.0 / Math.PI); // -180 to 180, 0 = east

        // Normalize to 0-360 range
        if (angle < 0) angle += 360;

        // Map angle to 8 directions (each covers 45 degrees)
        // 0° = east, 90° = south, 180° = west, 270° = north
        return angle switch
        {
            >= 337.5 or < 22.5 => "east",
            >= 22.5 and < 67.5 => "southeast",
            >= 67.5 and < 112.5 => "south",
            >= 112.5 and < 157.5 => "southwest",
            >= 157.5 and < 202.5 => "west",
            >= 202.5 and < 247.5 => "northwest",
            >= 247.5 and < 292.5 => "north",
            _ => "northeast"
        };
    }

    private static (double x, double y) ParsePosition(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble());
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string FormatResult(string status, double x, double y, double targetX, double targetY, double distance, double tolerance)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"status":"{{status}}","x":{{x}},"y":{{y}},"target_x":{{targetX}},"target_y":{{targetY}},"distance":{{string.Format(CultureInfo.InvariantCulture, "{0:F2}", distance)}},"tolerance":{{tolerance}}}""");
    }
}
