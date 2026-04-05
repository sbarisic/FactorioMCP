using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for tracing how items flow through the factory via belts, inserters, and
/// mining drills. Returns directed graphs so the AI can understand logistics chains,
/// diagnose clogged belts, and plan new routes.
/// </summary>
[McpServerToolType]
internal sealed class FlowTools(FlowService flow, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Build a directed item-flow graph for the area around the player. " +
        "Scans all belts, inserters, and mining drills within the given radius and returns " +
        "a list of directed edges showing which entity feeds which other entity, " +
        "along with connection type (belt, inserter_pickup, inserter_drop, drill_output). " +
        "Use this to get a broad overview of how items move through a factory section. " +
        "Keep the radius small (≤30) to avoid very large graphs.")]
    public Task<string> GetFlowGraph(
        [Description("Search radius in tiles around the player. Default 30. Keep small for readability.")]
        double radius = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetFlowGraph), ct => flow.GetFlowGraphAsync(radius, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Trace the downstream flow of items starting from a specific entity. " +
        "Performs a breadth-first search from the entity at the given coordinates, " +
        "following inserter drops and belt outputs, up to the specified depth. " +
        "Belt segments are automatically collapsed into single nodes with length info " +
        "so depth budget is spent on machines and inserters, not individual belt tiles. " +
        "Returns the chain of nodes (entities) and directed edges the items pass through. " +
        "Use this to answer questions like 'where does ore from this miner end up?' " +
        "or 'what does this chest feed into?'.")]
    public Task<string> TraceItemFlow(
        [Description("X coordinate of the starting entity")]
        double x,
        [Description("Y coordinate of the starting entity")]
        double y,
        [Description("Maximum number of hops to follow downstream. Default 5. Belt segments count as 0 hops.")]
        int depth = 5,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(TraceItemFlow), ct => flow.TraceItemFlowAsync(x, y, depth, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Preview what a transport belt placed at (x,y) facing the given direction would connect to. " +
        "Shows the output side (where items flow to), three input sides (behind, left, right), " +
        "nearby inserters that interact with this position, existing entities at the position, " +
        "and whether placement is possible. The belt equivalent of PreviewInserterPlacement. " +
        "BELT DIRECTION: The 'direction' is where items FLOW (the way arrows point). " +
        "Use this BEFORE placing a belt to verify connections.")]
    public Task<string> PreviewBeltPlacement(
        [Description("X coordinate where the belt would be placed")]
        double x,
        [Description("Y coordinate where the belt would be placed")]
        double y,
        [Description("Direction items would flow on the belt (= arrow direction). Values: north, south, east, west")]
        string direction = "north",
        [Description("Belt type to check placement for. Default: transport-belt. Options: transport-belt, fast-transport-belt, express-transport-belt, turbo-transport-belt")]
        string beltType = "transport-belt",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PreviewBeltPlacement),
            ct => flow.PreviewBeltPlacementAsync(x, y, direction, beltType, ct),
            cancellationToken);
    }
}
