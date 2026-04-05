using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class FlowServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly FlowService _service;

    public FlowServiceTests()
    {
        _service = new FlowService(_rcon);
    }

    // ── GetFlowGraphAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetFlowGraphAsync_SendsSilentCommand()
    {
        await _service.GetFlowGraphAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetFlowGraphAsync_ScansBelts()
    {
        await _service.GetFlowGraphAsync();

        Assert.Contains("transport-belt", _rcon.LastCommand!);
        Assert.Contains("belt_neighbours", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFlowGraphAsync_ScansInserters()
    {
        await _service.GetFlowGraphAsync();

        Assert.Contains("inserter", _rcon.LastCommand!);
        Assert.Contains("pickup_target", _rcon.LastCommand!);
        Assert.Contains("drop_position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFlowGraphAsync_ScansMiningDrills()
    {
        await _service.GetFlowGraphAsync();

        Assert.Contains("mining-drill", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFlowGraphAsync_UsesDefaultRadius()
    {
        await _service.GetFlowGraphAsync();

        Assert.Contains("30", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFlowGraphAsync_UsesCustomRadius()
    {
        await _service.GetFlowGraphAsync(radius: 50);

        Assert.Contains("50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFlowGraphAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetFlowGraphAsync(radius: 0));
    }

    [Fact]
    public async Task GetFlowGraphAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetFlowGraphAsync(radius: -5));
    }

    [Fact]
    public async Task GetFlowGraphAsync_OutputsEdgeList()
    {
        await _service.GetFlowGraphAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"edge_count\"", _rcon.LastCommand!);
        Assert.Contains("\"edges\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFlowGraphAsync_IncludesEdgeTypes()
    {
        await _service.GetFlowGraphAsync();

        Assert.Contains("inserter_pickup", _rcon.LastCommand!);
        Assert.Contains("inserter_drop", _rcon.LastCommand!);
        Assert.Contains("drill_output", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFlowGraphAsync_UsesConnectedPlayers()
    {
        await _service.GetFlowGraphAsync();

        Assert.Contains("game.connected_players[1]", _rcon.LastCommand!);
    }

    // ── TraceItemFlowAsync ───────────────────────────────────────────

    [Fact]
    public async Task TraceItemFlowAsync_SendsSilentCommand()
    {
        await _service.TraceItemFlowAsync(10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task TraceItemFlowAsync_UsesProvidedCoordinates()
    {
        await _service.TraceItemFlowAsync(15, -8);

        Assert.Contains("15", _rcon.LastCommand!);
        Assert.Contains("-8", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TraceItemFlowAsync_UsesDefaultDepth()
    {
        await _service.TraceItemFlowAsync(0, 0);

        Assert.Contains("5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TraceItemFlowAsync_UsesCustomDepth()
    {
        await _service.TraceItemFlowAsync(0, 0, depth: 3);

        Assert.Contains("3", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TraceItemFlowAsync_ThrowsOnZeroDepth()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.TraceItemFlowAsync(0, 0, depth: 0));
    }

    [Fact]
    public async Task TraceItemFlowAsync_PerformsBFS()
    {
        await _service.TraceItemFlowAsync(0, 0);

        Assert.Contains("bfs_entities", _rcon.LastCommand!);
        Assert.Contains("bfs_depths", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TraceItemFlowAsync_FollowsBeltsAndInserters()
    {
        await _service.TraceItemFlowAsync(0, 0);

        Assert.Contains("belt_neighbours", _rcon.LastCommand!);
        Assert.Contains("pickup_target", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TraceItemFlowAsync_OutputsNodesAndEdges()
    {
        await _service.TraceItemFlowAsync(0, 0);

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"node_count\"", _rcon.LastCommand!);
        Assert.Contains("\"edges\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TraceItemFlowAsync_FiltersOutResourcesAndPlayer()
    {
        await _service.TraceItemFlowAsync(0, 0);

        Assert.Contains("resource", _rcon.LastCommand!);
        Assert.Contains("character", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TraceItemFlowAsync_CollapsesBeltSegments()
    {
        await _service.TraceItemFlowAsync(0, 0);

        Assert.Contains("belt_segment", _rcon.LastCommand!);
        Assert.Contains("belt_length", _rcon.LastCommand!);
        Assert.Contains("follow_belt_chain", _rcon.LastCommand!);
    }

    // ── PreviewBeltPlacementAsync ─────────────────────────────────────

    [Fact]
    public async Task PreviewBeltPlacementAsync_SendsSilentCommand()
    {
        await _service.PreviewBeltPlacementAsync(5, 10, "east");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_UsesProvidedCoordinates()
    {
        await _service.PreviewBeltPlacementAsync(15, -8, "north");

        Assert.Contains("15", _rcon.LastCommand!);
        Assert.Contains("-8", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_UsesDirection()
    {
        await _service.PreviewBeltPlacementAsync(0, 0, "west");

        Assert.Contains("defines.direction.west", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_ShowsAllInputSides()
    {
        await _service.PreviewBeltPlacementAsync(0, 0, "north");

        Assert.Contains("input_behind", _rcon.LastCommand!);
        Assert.Contains("input_left", _rcon.LastCommand!);
        Assert.Contains("input_right", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_ShowsOutputSide()
    {
        await _service.PreviewBeltPlacementAsync(0, 0, "north");

        Assert.Contains("output", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_ChecksCanPlace()
    {
        await _service.PreviewBeltPlacementAsync(0, 0, "north");

        Assert.Contains("can_place_entity", _rcon.LastCommand!);
        Assert.Contains("can_place", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_FindsNearbyInserters()
    {
        await _service.PreviewBeltPlacementAsync(0, 0, "north");

        Assert.Contains("inserter", _rcon.LastCommand!);
        Assert.Contains("picks_from_belt", _rcon.LastCommand!);
        Assert.Contains("drops_onto_belt", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_UsesBeltType()
    {
        await _service.PreviewBeltPlacementAsync(0, 0, "east", "fast-transport-belt");

        Assert.Contains("fast-transport-belt", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_ThrowsOnEmptyDirection()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.PreviewBeltPlacementAsync(0, 0, ""));
    }

    [Fact]
    public async Task PreviewBeltPlacementAsync_ShowsExistingEntities()
    {
        await _service.PreviewBeltPlacementAsync(0, 0, "south");

        Assert.Contains("existing_at_position", _rcon.LastCommand!);
    }
}
