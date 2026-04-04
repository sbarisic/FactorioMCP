using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class PathfindingServiceTests
{
    // ── ProjectOntoSegment ──────────────────────────────────────────

    [Fact]
    public void ProjectOntoSegment_AtStart_ReturnsZero()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, 0, 0);
        Assert.Equal(0.0, t, precision: 3);
    }

    [Fact]
    public void ProjectOntoSegment_AtEnd_ReturnsOne()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, 10, 0);
        Assert.Equal(1.0, t, precision: 3);
    }

    [Fact]
    public void ProjectOntoSegment_Midpoint_ReturnsHalf()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, 5, 0);
        Assert.Equal(0.5, t, precision: 3);
    }

    [Fact]
    public void ProjectOntoSegment_PastEnd_ReturnsGreaterThanOne()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, 15, 0);
        Assert.True(t > 1.0);
    }

    [Fact]
    public void ProjectOntoSegment_BeforeStart_ReturnsNegative()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, -5, 0);
        Assert.True(t < 0);
    }

    [Fact]
    public void ProjectOntoSegment_PerpendicularOffset_ProjectsCorrectly()
    {
        // Player is at (5, 3) — perpendicular to midpoint of horizontal segment
        var waypoints = new List<(double, double)> { (0, 0), (10, 0) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, 5, 3);
        Assert.Equal(0.5, t, precision: 3);
    }

    [Fact]
    public void ProjectOntoSegment_DiagonalSegment_ProjectsCorrectly()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 10) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, 5, 5);
        Assert.Equal(0.5, t, precision: 3);
    }

    [Fact]
    public void ProjectOntoSegment_DegenerateSegment_ReturnOne()
    {
        // Start and end are the same point
        var waypoints = new List<(double, double)> { (5, 5), (5, 5) };
        var t = PathfindingService.ProjectOntoSegment(waypoints, 0, 5, 5);
        Assert.Equal(1.0, t, precision: 3);
    }

    // ── AdvanceSegment ──────────────────────────────────────────────

    [Fact]
    public void AdvanceSegment_PlayerBeforeEndpoint_StaysOnSegment()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        var result = PathfindingService.AdvanceSegment(waypoints, 0, 5, 0);
        Assert.Equal(0, result);
    }

    [Fact]
    public void AdvanceSegment_PlayerPastEndpoint_AdvancesToNext()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        var result = PathfindingService.AdvanceSegment(waypoints, 0, 12, 0);
        Assert.Equal(1, result);
    }

    [Fact]
    public void AdvanceSegment_PlayerPastMultipleEndpoints_AdvancesMultiple()
    {
        var waypoints = new List<(double, double)> { (0, 0), (5, 0), (10, 0), (20, 0) };
        // Player at x=12 is past both segment 0→1 and 1→2
        var result = PathfindingService.AdvanceSegment(waypoints, 0, 12, 0);
        Assert.Equal(2, result);
    }

    [Fact]
    public void AdvanceSegment_AtLastSegment_AdvancesToEnd()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0) };
        // Only one segment (index 0), player past end — segIndex advances to Count-1
        // which signals "arrived" in FollowWaypointsAsync
        var result = PathfindingService.AdvanceSegment(waypoints, 0, 15, 0);
        Assert.Equal(1, result); // waypoints.Count - 1, caller treats as arrived
    }

    [Fact]
    public void AdvanceSegment_LShapedPath_AdvancesOnlyPastProjectedSegments()
    {
        // L-shaped path: right then down
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (10, 10) };
        // Player at (12, 0) — past first segment endpoint but projection onto
        // second segment (vertical) should be at t=0 since player is at start Y
        var result = PathfindingService.AdvanceSegment(waypoints, 0, 12, 0);
        Assert.Equal(1, result); // Advanced past first, stays on second
    }

    [Fact]
    public void AdvanceSegment_PlayerPerpendicularToSegment_StaysOnSegment()
    {
        // Player is off to the side but hasn't passed the endpoint
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        var result = PathfindingService.AdvanceSegment(waypoints, 0, 5, 5);
        Assert.Equal(0, result); // t = 0.5, hasn't passed endpoint
    }

    // ── GetPathProgress ─────────────────────────────────────────────

    [Fact]
    public void GetPathProgress_AtStart_ReturnsZero()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        var progress = PathfindingService.GetPathProgress(waypoints, 0, 0, 0);
        Assert.Equal(0.0, progress, precision: 2);
    }

    [Fact]
    public void GetPathProgress_MidFirstSegment_ReturnsHalfSegmentLength()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        var progress = PathfindingService.GetPathProgress(waypoints, 0, 5, 0);
        Assert.Equal(5.0, progress, precision: 2);
    }

    [Fact]
    public void GetPathProgress_OnSecondSegment_IncludesFirstSegmentLength()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        // On segment 1, at x=15 (midpoint of second segment)
        var progress = PathfindingService.GetPathProgress(waypoints, 1, 15, 0);
        Assert.Equal(15.0, progress, precision: 2); // 10 (first seg) + 5 (half of second)
    }

    [Fact]
    public void GetPathProgress_ClampsProjectionToSegmentLength()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        // Player past current segment end — projection should be clamped to 1.0
        var progress = PathfindingService.GetPathProgress(waypoints, 0, 15, 0);
        Assert.Equal(10.0, progress, precision: 2); // Clamped to full first segment
    }

    [Fact]
    public void GetPathProgress_ClampsNegativeProjection()
    {
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (20, 0) };
        // Player before segment start
        var progress = PathfindingService.GetPathProgress(waypoints, 0, -5, 0);
        Assert.Equal(0.0, progress, precision: 2); // Clamped to 0
    }

    [Fact]
    public void GetPathProgress_MultiSegmentPath_AccumulatesCorrectly()
    {
        // 3 segments of length 10 each
        var waypoints = new List<(double, double)> { (0, 0), (10, 0), (10, 10), (0, 10) };
        // On third segment (index 2), at midpoint (5, 10)
        var progress = PathfindingService.GetPathProgress(waypoints, 2, 5, 10);
        Assert.Equal(25.0, progress, precision: 2); // 10 + 10 + 5
    }

    // ── CalculateDirection ──────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 0, -10, 0)]  // North
    [InlineData(0, 0, 10, -10, 2)]  // NE
    [InlineData(0, 0, 10, 0, 4)]   // East
    [InlineData(0, 0, 10, 10, 6)]  // SE
    [InlineData(0, 0, 0, 10, 8)]   // South
    [InlineData(0, 0, -10, 10, 10)] // SW
    [InlineData(0, 0, -10, 0, 12)] // West
    [InlineData(0, 0, -10, -10, 14)] // NW
    public void CalculateDirection_ReturnsCorrectFactorio2Direction(
        double fromX, double fromY, double toX, double toY, int expected)
    {
        var result = PathfindingService.CalculateDirection(fromX, fromY, toX, toY);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateDirection_SmallDxDominantDy_ReturnsCardinal()
    {
        // Nearly vertical — should be North, not NE
        var result = PathfindingService.CalculateDirection(0, 0, 0.1, -10);
        Assert.Equal(0, result); // North
    }

    [Fact]
    public void CalculateDirection_SmallDyDominantDx_ReturnsCardinal()
    {
        // Nearly horizontal — should be East, not NE
        var result = PathfindingService.CalculateDirection(0, 0, 10, 0.1);
        Assert.Equal(4, result); // East
    }

    // ── ParsePosition ───────────────────────────────────────────────

    [Fact]
    public void ParsePosition_ValidJson_ReturnsCoordinates()
    {
        var (x, y) = PathfindingService.ParsePosition("""{"x":10.5,"y":-3.2}""");
        Assert.Equal(10.5, x);
        Assert.Equal(-3.2, y);
    }

    // ── Distance ────────────────────────────────────────────────────

    [Fact]
    public void Distance_SamePoint_ReturnsZero()
    {
        Assert.Equal(0.0, PathfindingService.Distance(5, 5, 5, 5));
    }

    [Fact]
    public void Distance_KnownDistance_ReturnsCorrect()
    {
        Assert.Equal(5.0, PathfindingService.Distance(0, 0, 3, 4), precision: 6);
    }

    // ── FormatResult ────────────────────────────────────────────────

    [Fact]
    public void FormatResult_ContainsAllFields()
    {
        var result = PathfindingService.FormatResult("arrived", 1.0, 2.0, 3.0, 4.0, 2.83, 5.0);
        Assert.Contains("\"status\":\"arrived\"", result);
        Assert.Contains("\"x\":1", result);
        Assert.Contains("\"y\":2", result);
        Assert.Contains("\"target_x\":3", result);
        Assert.Contains("\"target_y\":4", result);
        Assert.Contains("\"distance\":", result);
        Assert.Contains("2.83", result);
        Assert.Contains("\"tolerance\":5", result);
    }
}
