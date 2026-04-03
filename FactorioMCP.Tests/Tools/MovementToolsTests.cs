using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class MovementToolsTests
{
    // ── CalculateDirection ────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 10, 0, "east")]       // +X only
    [InlineData(0, 0, -10, 0, "west")]      // -X only
    [InlineData(0, 0, 0, 10, "south")]      // +Y only (Factorio: +Y = south)
    [InlineData(0, 0, 0, -10, "north")]     // -Y only (Factorio: -Y = north)
    [InlineData(0, 0, 10, 10, "southeast")] // +X +Y
    [InlineData(0, 0, -10, 10, "southwest")] // -X +Y
    [InlineData(0, 0, 10, -10, "northeast")] // +X -Y
    [InlineData(0, 0, -10, -10, "northwest")] // -X -Y
    public void CalculateDirection_ReturnsCorrectDirection(
        double fromX, double fromY, double toX, double toY, string expected)
    {
        var direction = MovementTools.CalculateDirection(fromX, fromY, toX, toY);
        Assert.Equal(expected, direction);
    }

    [Fact]
    public void CalculateDirection_SlightlyMoreEastThanSouth_ReturnsEast()
    {
        // Angle ~11° from east axis — should still be east (within 22.5°)
        var direction = MovementTools.CalculateDirection(0, 0, 10, 2);
        Assert.Equal("east", direction);
    }

    [Fact]
    public void CalculateDirection_SlightlyMoreSouthThanEast_ReturnsSoutheast()
    {
        // Angle ~63° from east axis — should be southeast (22.5-67.5°)
        var direction = MovementTools.CalculateDirection(0, 0, 4, 8);
        Assert.Equal("southeast", direction);
    }

    [Fact]
    public void CalculateDirection_FromNonOriginPosition_CalculatesCorrectly()
    {
        // From (10, 20) to (20, 20) = pure east
        var direction = MovementTools.CalculateDirection(10, 20, 20, 20);
        Assert.Equal("east", direction);
    }

    [Fact]
    public void CalculateDirection_NegativeCoordinates_CalculatesCorrectly()
    {
        // From (-5, -5) to (-15, -5) = pure west
        var direction = MovementTools.CalculateDirection(-5, -5, -15, -5);
        Assert.Equal("west", direction);
    }

    // ── GetPerpendicularDirection ─────────────────────────────────────

    [Theory]
    [InlineData("north", 1, "east")]
    [InlineData("north", 2, "west")]
    [InlineData("south", 1, "west")]
    [InlineData("south", 2, "east")]
    [InlineData("east", 1, "south")]
    [InlineData("east", 2, "north")]
    [InlineData("west", 1, "north")]
    [InlineData("west", 2, "south")]
    [InlineData("northeast", 1, "southeast")]
    [InlineData("northeast", 2, "northwest")]
    [InlineData("southeast", 1, "southwest")]
    [InlineData("southeast", 2, "northeast")]
    public void GetPerpendicularDirection_ReturnsCorrectPerpendicular(
        string direction, int side, string expected)
    {
        var result = MovementTools.GetPerpendicularDirection(direction, side);
        Assert.Equal(expected, result);
    }

    // ── WalkToPosition — Already at target ───────────────────────────

    [Fact]
    public async Task WalkToPosition_AlreadyAtTarget_ReturnsArrivedImmediately()
    {
        var rcon = new ScriptedRconClient([
            // GetPlayerPosition → already at target
            """{"x":10,"y":20}"""
        ]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new MovementTools(factorio, queue);

        var result = await tools.WalkToPosition(10, 20, tolerance: 2.0);

        Assert.Contains("\"status\":\"arrived\"", result);
        Assert.Contains("\"x\":10", result);
        Assert.Contains("\"y\":20", result);
    }

    [Fact]
    public async Task WalkToPosition_WithinTolerance_ReturnsArrivedImmediately()
    {
        var rcon = new ScriptedRconClient([
            // GetPlayerPosition → within 1 tile of target
            """{"x":10.5,"y":20.5}"""
        ]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new MovementTools(factorio, queue);

        var result = await tools.WalkToPosition(10, 20, tolerance: 2.0);

        Assert.Contains("\"status\":\"arrived\"", result);
    }

    // ── WalkToPosition — Arrives after walking ───────────────────────

    [Fact]
    public async Task WalkToPosition_ArrivesAfterPolling_ReturnsArrived()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition (initial check)
            """{"x":0,"y":0}""",
            // 2. WalkAsync (start walking east)
            """{"status":"walking","direction":"east","x":0,"y":0}""",
            // 3. GetPlayerPosition (first poll — still moving)
            """{"x":5,"y":0}""",
            // 4. GetPlayerPosition (second poll — arrived)
            """{"x":9.5,"y":0}""",
            // 5. StopWalking
            """{"status":"stopped","x":9.5,"y":0}"""
        ]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new MovementTools(factorio, queue);

        var result = await tools.WalkToPosition(10, 0, tolerance: 2.0, pollIntervalSeconds: 0.05);

        Assert.Contains("\"status\":\"arrived\"", result);
    }

    // ── WalkToPosition — Stuck detection ─────────────────────────────

    [Fact]
    public async Task WalkToPosition_PlayerStuck_ReturnsStuck()
    {
        // Player doesn't move across multiple polls
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition (initial check)
            """{"x":0,"y":0}""",
            // 2. WalkAsync
            """{"status":"walking","direction":"east","x":0,"y":0}""",
            // Polls 3-8: player stays at same position (6 stuck polls = stuck)
            """{"x":0.01,"y":0}""",
            """{"x":0.01,"y":0}""",
            """{"x":0.01,"y":0}""",
            """{"x":0.01,"y":0}""",
            """{"x":0.01,"y":0}""",
            """{"x":0.01,"y":0}""",
            """{"x":0.01,"y":0}""",
            // StopWalking
            """{"status":"stopped","x":0.01,"y":0}"""
        ]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new MovementTools(factorio, queue);

        var result = await tools.WalkToPosition(50, 0, tolerance: 2.0, pollIntervalSeconds: 0.01);

        Assert.Contains("\"status\":\"stuck\"", result);
    }

    // ── WalkToPosition — Timeout ─────────────────────────────────────

    [Fact]
    public async Task WalkToPosition_ExceedsTimeout_ReturnsTimeout()
    {
        // Player moves slowly but target is very far away
        var responses = new List<string>
        {
            // 1. GetPlayerPosition (initial check)
            """{"x":0,"y":0}""",
            // 2. WalkAsync
            """{"status":"walking","direction":"east","x":0,"y":0}"""
        };
        // Add many polling responses that slowly move but never arrive
        for (int i = 1; i <= 50; i++)
            responses.Add($$"""{"x":{{i}},"y":0}""");
        // StopWalking + final GetPlayerPosition
        responses.Add("""{"status":"stopped","x":50,"y":0}""");
        responses.Add("""{"x":50,"y":0}""");

        var rcon = new ScriptedRconClient(responses.ToArray());
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new MovementTools(factorio, queue);

        var result = await tools.WalkToPosition(1000, 0, tolerance: 2.0,
            pollIntervalSeconds: 0.01, timeoutSeconds: 0.2);

        Assert.Contains("\"status\":\"timeout\"", result);
    }

    // ── WalkToPosition — Direction re-calculation ────────────────────

    [Fact]
    public async Task WalkToPosition_ReCalculatesDirection_WhenCourseChanges()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition → need to go east
            """{"x":0,"y":0}""",
            // 2. WalkAsync east
            """{"status":"walking","direction":"east","x":0,"y":0}""",
            // 3. Poll — moved east, but target is now more south
            """{"x":5,"y":0}""",
            // 4. WalkAsync (re-calculated to southeast)
            """{"status":"walking","direction":"southeast","x":5,"y":0}""",
            // 5. Poll — arrived near target
            """{"x":9,"y":9}""",
            // 6. StopWalking
            """{"status":"stopped","x":9,"y":9}"""
        ]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new MovementTools(factorio, queue);

        var result = await tools.WalkToPosition(10, 10, tolerance: 2.0, pollIntervalSeconds: 0.01);

        Assert.Contains("\"status\":\"arrived\"", result);
        // Verify it issued a walk command with the recalculated direction
        Assert.True(rcon.AllCommands.Any(c => c.Contains("southeast") || c.Contains("south")),
            "Expected a walk command in a southward direction after course correction");
    }

    // ── WalkToPosition — Response format ─────────────────────────────

    [Fact]
    public async Task WalkToPosition_ResponseIncludesTargetAndDistance()
    {
        var rcon = new ScriptedRconClient([
            """{"x":10,"y":20}"""
        ]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new MovementTools(factorio, queue);

        var result = await tools.WalkToPosition(10, 20, tolerance: 5.0);

        Assert.Contains("\"target_x\":10", result);
        Assert.Contains("\"target_y\":20", result);
        Assert.Contains("\"tolerance\":5", result);
        Assert.Contains("\"distance\":", result);
    }
}
