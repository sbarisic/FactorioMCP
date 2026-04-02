using FactorioMCP.Services;
using System.Text.Json;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class BeltPlannerServiceTests
{
    private readonly BeltPlannerService _planner = new();

    // ── Same Position ────────────────────────────────────────────────

    [Fact]
    public void PlanRoute_SamePosition_ReturnsError()
    {
        var result = _planner.PlanRoute(5, 3, 5, 3);
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("same_position", doc.RootElement.GetProperty("error").GetString());
    }

    // ── Straight Horizontal Routes ───────────────────────────────────

    [Fact]
    public void PlanRoute_StraightEast_CorrectSteps()
    {
        var result = _planner.PlanRoute(2, 5, 6, 5);
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("straight", root.GetProperty("route_type").GetString());
        Assert.Equal(5, root.GetProperty("belt_count").GetInt32());

        var steps = root.GetProperty("steps");
        Assert.Equal(5, steps.GetArrayLength());

        // All belts face east
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("east", steps[i].GetProperty("direction").GetString());
            Assert.Equal(2 + i, steps[i].GetProperty("x").GetInt32());
            Assert.Equal(5, steps[i].GetProperty("y").GetInt32());
        }
    }

    [Fact]
    public void PlanRoute_StraightWest_CorrectDirection()
    {
        var result = _planner.PlanRoute(6, 5, 2, 5);
        using var doc = JsonDocument.Parse(result);
        var steps = doc.RootElement.GetProperty("steps");

        Assert.Equal(5, steps.GetArrayLength());
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("west", steps[i].GetProperty("direction").GetString());
            Assert.Equal(6 - i, steps[i].GetProperty("x").GetInt32());
        }
    }

    // ── Straight Vertical Routes ─────────────────────────────────────

    [Fact]
    public void PlanRoute_StraightSouth_CorrectSteps()
    {
        var result = _planner.PlanRoute(3, 1, 3, 4);
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("straight", root.GetProperty("route_type").GetString());
        Assert.Equal(4, root.GetProperty("belt_count").GetInt32());

        var steps = root.GetProperty("steps");
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal("south", steps[i].GetProperty("direction").GetString());
            Assert.Equal(3, steps[i].GetProperty("x").GetInt32());
            Assert.Equal(1 + i, steps[i].GetProperty("y").GetInt32());
        }
    }

    [Fact]
    public void PlanRoute_StraightNorth_CorrectDirection()
    {
        var result = _planner.PlanRoute(3, 4, 3, 1);
        using var doc = JsonDocument.Parse(result);
        var steps = doc.RootElement.GetProperty("steps");

        Assert.Equal(4, steps.GetArrayLength());
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal("north", steps[i].GetProperty("direction").GetString());
            Assert.Equal(4 - i, steps[i].GetProperty("y").GetInt32());
        }
    }

    // ── L-Shaped Routes: Horizontal First ────────────────────────────

    [Fact]
    public void PlanRoute_LShape_HorizontalFirst_EastThenSouth()
    {
        var result = _planner.PlanRoute(0, 0, 3, 2, "horizontal_first");
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("L-shaped", root.GetProperty("route_type").GetString());

        var steps = root.GetProperty("steps");
        // Horizontal: (0,0)→(1,0)→(2,0) facing east, then corner (3,0) facing south, then (3,1)→(3,2) facing south
        Assert.Equal(6, steps.GetArrayLength());

        // First 3 tiles face east (horizontal leg)
        Assert.Equal("east", steps[0].GetProperty("direction").GetString());
        Assert.Equal(0, steps[0].GetProperty("x").GetInt32());
        Assert.Equal(0, steps[0].GetProperty("y").GetInt32());

        Assert.Equal("east", steps[1].GetProperty("direction").GetString());
        Assert.Equal(1, steps[1].GetProperty("x").GetInt32());

        Assert.Equal("east", steps[2].GetProperty("direction").GetString());
        Assert.Equal(2, steps[2].GetProperty("x").GetInt32());

        // Corner tile faces south (the new direction)
        Assert.Equal("south", steps[3].GetProperty("direction").GetString());
        Assert.Equal(3, steps[3].GetProperty("x").GetInt32());
        Assert.Equal(0, steps[3].GetProperty("y").GetInt32());

        // Remaining tiles face south (vertical leg)
        Assert.Equal("south", steps[4].GetProperty("direction").GetString());
        Assert.Equal(3, steps[4].GetProperty("x").GetInt32());
        Assert.Equal(1, steps[4].GetProperty("y").GetInt32());

        Assert.Equal("south", steps[5].GetProperty("direction").GetString());
        Assert.Equal(3, steps[5].GetProperty("x").GetInt32());
        Assert.Equal(2, steps[5].GetProperty("y").GetInt32());
    }

    [Fact]
    public void PlanRoute_LShape_HorizontalFirst_WestThenNorth()
    {
        var result = _planner.PlanRoute(3, 3, 0, 0, "horizontal_first");
        using var doc = JsonDocument.Parse(result);
        var steps = doc.RootElement.GetProperty("steps");

        // Horizontal: (3,3)→(2,3)→(1,3) facing west, corner (0,3) facing north,
        // then (0,2)→(0,1)→(0,0) facing north
        Assert.Equal(7, steps.GetArrayLength());

        // First 3 tiles face west
        for (int i = 0; i < 3; i++)
            Assert.Equal("west", steps[i].GetProperty("direction").GetString());

        // Corner + remaining face north
        for (int i = 3; i < 7; i++)
            Assert.Equal("north", steps[i].GetProperty("direction").GetString());
    }

    // ── L-Shaped Routes: Vertical First ──────────────────────────────

    [Fact]
    public void PlanRoute_LShape_VerticalFirst_SouthThenEast()
    {
        var result = _planner.PlanRoute(0, 0, 3, 2, "vertical_first");
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.Equal("L-shaped", root.GetProperty("route_type").GetString());

        var steps = root.GetProperty("steps");
        // Vertical: (0,0)→(0,1) facing south, corner (0,2) facing east, then (1,2)→(2,2)→(3,2) facing east
        Assert.Equal(6, steps.GetArrayLength());

        // First 2 tiles face south (vertical leg)
        Assert.Equal("south", steps[0].GetProperty("direction").GetString());
        Assert.Equal(0, steps[0].GetProperty("x").GetInt32());
        Assert.Equal(0, steps[0].GetProperty("y").GetInt32());

        Assert.Equal("south", steps[1].GetProperty("direction").GetString());
        Assert.Equal(0, steps[1].GetProperty("x").GetInt32());
        Assert.Equal(1, steps[1].GetProperty("y").GetInt32());

        // Corner tile faces east (the new direction)
        Assert.Equal("east", steps[2].GetProperty("direction").GetString());
        Assert.Equal(0, steps[2].GetProperty("x").GetInt32());
        Assert.Equal(2, steps[2].GetProperty("y").GetInt32());

        // Remaining tiles face east (horizontal leg)
        Assert.Equal("east", steps[3].GetProperty("direction").GetString());
        Assert.Equal(1, steps[3].GetProperty("x").GetInt32());

        Assert.Equal("east", steps[4].GetProperty("direction").GetString());
        Assert.Equal(2, steps[4].GetProperty("x").GetInt32());

        Assert.Equal("east", steps[5].GetProperty("direction").GetString());
        Assert.Equal(3, steps[5].GetProperty("x").GetInt32());
    }

    // ── Default Turn Preference ──────────────────────────────────────

    [Fact]
    public void PlanRoute_DefaultTurnPreference_IsHorizontalFirst()
    {
        var resultDefault = _planner.PlanRoute(0, 0, 3, 2);
        var resultExplicit = _planner.PlanRoute(0, 0, 3, 2, "horizontal_first");

        using var docDefault = JsonDocument.Parse(resultDefault);
        using var docExplicit = JsonDocument.Parse(resultExplicit);

        var stepsDefault = docDefault.RootElement.GetProperty("steps");
        var stepsExplicit = docExplicit.RootElement.GetProperty("steps");

        Assert.Equal(stepsExplicit.GetArrayLength(), stepsDefault.GetArrayLength());
        for (int i = 0; i < stepsDefault.GetArrayLength(); i++)
        {
            Assert.Equal(
                stepsExplicit[i].GetProperty("x").GetInt32(),
                stepsDefault[i].GetProperty("x").GetInt32());
            Assert.Equal(
                stepsExplicit[i].GetProperty("y").GetInt32(),
                stepsDefault[i].GetProperty("y").GetInt32());
            Assert.Equal(
                stepsExplicit[i].GetProperty("direction").GetString(),
                stepsDefault[i].GetProperty("direction").GetString());
        }
    }

    // ── Straight Route Has No Turn Preference ────────────────────────

    [Fact]
    public void PlanRoute_StraightRoute_TurnPreferenceIsNull()
    {
        var result = _planner.PlanRoute(0, 0, 5, 0);
        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("turn_preference").ValueKind);
    }

    // ── Belt Count ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 4, 0, 5)]   // 5 tiles horizontal
    [InlineData(0, 0, 0, 3, 4)]   // 4 tiles vertical
    [InlineData(0, 0, 2, 3, 6)]   // L-shaped: 3 horizontal + 3 vertical (corner shared) = 2+1+3 = 6
    public void PlanRoute_CorrectBeltCount(double sx, double sy, double ex, double ey, int expectedCount)
    {
        var result = _planner.PlanRoute(sx, sy, ex, ey);
        using var doc = JsonDocument.Parse(result);
        Assert.Equal(expectedCount, doc.RootElement.GetProperty("belt_count").GetInt32());
    }

    // ── Coordinates Are Rounded ──────────────────────────────────────

    [Fact]
    public void PlanRoute_RoundsCoordinatesToNearestInt()
    {
        var result = _planner.PlanRoute(1.4, 2.6, 3.7, 2.6);
        using var doc = JsonDocument.Parse(result);
        var steps = doc.RootElement.GetProperty("steps");

        // 1.4 → 1, 2.6 → 3, 3.7 → 4 — so route is (1,3) to (4,3)
        Assert.Equal(1, steps[0].GetProperty("x").GetInt32());
        Assert.Equal(3, steps[0].GetProperty("y").GetInt32());
        Assert.Equal(4, steps[steps.GetArrayLength() - 1].GetProperty("x").GetInt32());
        Assert.Equal(3, steps[steps.GetArrayLength() - 1].GetProperty("y").GetInt32());
    }

    // ── Adjacent Tiles (1 tile route) ────────────────────────────────

    [Fact]
    public void PlanRoute_AdjacentTiles_ReturnsTwoSteps()
    {
        var result = _planner.PlanRoute(5, 3, 6, 3);
        using var doc = JsonDocument.Parse(result);
        var steps = doc.RootElement.GetProperty("steps");

        Assert.Equal(2, steps.GetArrayLength());
        Assert.Equal("east", steps[0].GetProperty("direction").GetString());
        Assert.Equal("east", steps[1].GetProperty("direction").GetString());
    }

    // ── Negative Coordinates ─────────────────────────────────────────

    [Fact]
    public void PlanRoute_NegativeCoordinates_WorksCorrectly()
    {
        var result = _planner.PlanRoute(-3, -2, -1, -2);
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        var steps = root.GetProperty("steps");
        Assert.Equal(3, steps.GetArrayLength());
        Assert.Equal(-3, steps[0].GetProperty("x").GetInt32());
        Assert.Equal(-2, steps[1].GetProperty("x").GetInt32());
        Assert.Equal(-1, steps[2].GetProperty("x").GetInt32());
    }

    // ── GetBeltDirectionHelp ─────────────────────────────────────────

    [Fact]
    public void GetBeltDirectionHelp_ReturnsValidJson()
    {
        var result = BeltPlannerService.GetBeltDirectionHelp();
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("directions", out var dirs));
        Assert.Equal(4, dirs.GetArrayLength());

        Assert.True(root.TryGetProperty("tips", out var tips));
        Assert.True(tips.GetArrayLength() > 0);
    }

    [Fact]
    public void GetBeltDirectionHelp_ContainsAllCardinalDirections()
    {
        var result = BeltPlannerService.GetBeltDirectionHelp();
        using var doc = JsonDocument.Parse(result);
        var dirs = doc.RootElement.GetProperty("directions");

        var dirNames = new HashSet<string>();
        for (int i = 0; i < dirs.GetArrayLength(); i++)
        {
            dirNames.Add(dirs[i].GetProperty("direction").GetString()!);
        }

        Assert.Contains("north", dirNames);
        Assert.Contains("south", dirNames);
        Assert.Contains("east", dirNames);
        Assert.Contains("west", dirNames);
    }

    // ── Single Tile Diagonal (L-shaped with 1 tile each leg) ─────────

    [Fact]
    public void PlanRoute_DiagonalOneTileEach_ReturnsLShape()
    {
        var result = _planner.PlanRoute(0, 0, 1, 1, "horizontal_first");
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.Equal("L-shaped", root.GetProperty("route_type").GetString());
        var steps = root.GetProperty("steps");
        // Horizontal leg: (0,0) east, then corner (1,0) south, then vertical (1,1) south = 3 steps
        Assert.Equal(3, steps.GetArrayLength());

        // First tile faces east (horizontal leg)
        Assert.Equal("east", steps[0].GetProperty("direction").GetString());
        Assert.Equal(0, steps[0].GetProperty("x").GetInt32());
        Assert.Equal(0, steps[0].GetProperty("y").GetInt32());

        // Corner at (1,0) faces south
        Assert.Equal("south", steps[1].GetProperty("direction").GetString());
        Assert.Equal(1, steps[1].GetProperty("x").GetInt32());
        Assert.Equal(0, steps[1].GetProperty("y").GetInt32());

        // Final tile at (1,1) faces south
        Assert.Equal("south", steps[2].GetProperty("direction").GetString());
        Assert.Equal(1, steps[2].GetProperty("x").GetInt32());
        Assert.Equal(1, steps[2].GetProperty("y").GetInt32());
    }

    // ── Route Continuity (each step is adjacent to the next) ─────────

    [Theory]
    [InlineData(0, 0, 5, 3, "horizontal_first")]
    [InlineData(0, 0, 5, 3, "vertical_first")]
    [InlineData(-2, -2, 3, 4, "horizontal_first")]
    public void PlanRoute_AllStepsAreAdjacent(double sx, double sy, double ex, double ey, string pref)
    {
        var result = _planner.PlanRoute(sx, sy, ex, ey, pref);
        using var doc = JsonDocument.Parse(result);
        var steps = doc.RootElement.GetProperty("steps");

        for (int i = 1; i < steps.GetArrayLength(); i++)
        {
            int prevX = steps[i - 1].GetProperty("x").GetInt32();
            int prevY = steps[i - 1].GetProperty("y").GetInt32();
            int currX = steps[i].GetProperty("x").GetInt32();
            int currY = steps[i].GetProperty("y").GetInt32();

            int dist = Math.Abs(currX - prevX) + Math.Abs(currY - prevY);
            Assert.Equal(1, dist); // Manhattan distance must be exactly 1
        }
    }
}
