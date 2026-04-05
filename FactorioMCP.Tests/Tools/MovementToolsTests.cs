using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class MovementToolsTests
{
    // ── WalkToPosition — Already at target ───────────────────────────

    [Fact]
    public async Task WalkToPosition_AlreadyAtTarget_ReturnsArrivedImmediately()
    {
        var rcon = new ScriptedRconClient([
            // PathfindingService.GetPositionAsync → already at target
            """{"x":10,"y":20}"""
        ]);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var queue = new GameCommandQueue();
        var tools = new MovementTools(pathfinding, queue);

        var result = await tools.WalkToPosition(10, 20, tolerance: 2.0);

        Assert.Contains("\"status\":\"arrived\"", result);
        Assert.Contains("\"x\":10", result);
        Assert.Contains("\"y\":20", result);
    }

    [Fact]
    public async Task WalkToPosition_WithinTolerance_ReturnsArrivedImmediately()
    {
        var rcon = new ScriptedRconClient([
            // GetPosition → within 1 tile of target
            """{"x":10.5,"y":20.5}"""
        ]);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var queue = new GameCommandQueue();
        var tools = new MovementTools(pathfinding, queue);

        var result = await tools.WalkToPosition(10, 20, tolerance: 2.0);

        Assert.Contains("\"status\":\"arrived\"", result);
    }

    // ── WalkToPosition — Pathfinding arrives ─────────────────────────

    [Fact]
    public async Task WalkToPosition_PathfindingArrives_ReturnsArrived()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPositionAsync (initial check — far from target)
            """{"x":0,"y":0}""",
            // 2. EnsurePathHandlerInstalledAsync
            """ok""",
            // 3. RequestPathAsync
            """1""",
            // 4. GetPathResultAsync (path ready with waypoints)
            """{"status":"ok","path":[{"x":5,"y":0},{"x":9.5,"y":0}]}""",
            // 5. DrawPathAsync
            """ok""",
            // 6. GetPositionAsync (poll — arrived at destination)
            """{"x":9.5,"y":0}""",
            // 7. StopWalkingAsync
            """ok"""
        ]);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var queue = new GameCommandQueue();
        var tools = new MovementTools(pathfinding, queue);

        var result = await tools.WalkToPosition(10, 0, tolerance: 2.0);

        Assert.Contains("\"status\":\"arrived\"", result);
    }

    // ── WalkToPosition — No path ─────────────────────────────────────

    [Fact]
    public async Task WalkToPosition_NoPath_ReturnsNoPath()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPositionAsync (initial check)
            """{"x":0,"y":0}""",
            // 2. EnsurePathHandlerInstalledAsync
            """ok""",
            // 3. RequestPathAsync
            """1""",
            // 4. GetPathResultAsync (no path found)
            """{"status":"no_path"}"""
        ]);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var queue = new GameCommandQueue();
        var tools = new MovementTools(pathfinding, queue);

        var result = await tools.WalkToPosition(50, 0, tolerance: 2.0);

        Assert.Contains("\"status\":\"no_path\"", result);
    }

    // ── WalkToPosition — Response format ─────────────────────────────

    [Fact]
    public async Task WalkToPosition_ResponseIncludesTargetAndDistance()
    {
        var rcon = new ScriptedRconClient([
            """{"x":10,"y":20}"""
        ]);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var queue = new GameCommandQueue();
        var tools = new MovementTools(pathfinding, queue);

        var result = await tools.WalkToPosition(10, 20, tolerance: 5.0);

        Assert.Contains("\"target_x\":10", result);
        Assert.Contains("\"target_y\":20", result);
        Assert.Contains("\"tolerance\":5", result);
        Assert.Contains("\"distance\":", result);
    }

    // ── GetPlayerPosition ────────────────────────────────────────────

    [Fact]
    public async Task GetPlayerPosition_ReturnsPosition()
    {
        var rcon = new ScriptedRconClient([
            """{"x":42,"y":-7}"""
        ]);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var queue = new GameCommandQueue();
        var tools = new MovementTools(pathfinding, queue);

        var result = await tools.GetPlayerPosition();

        Assert.Contains("\"x\":42", result);
        Assert.Contains("\"y\":-7", result);
    }
}
