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
        "Returns the chain of nodes (entities) and directed edges the items pass through. " +
        "Use this to answer questions like 'where does ore from this miner end up?' " +
        "or 'what does this chest feed into?'.")]
    public Task<string> TraceItemFlow(
        [Description("X coordinate of the starting entity")]
        double x,
        [Description("Y coordinate of the starting entity")]
        double y,
        [Description("Maximum number of hops to follow downstream. Default 5.")]
        int depth = 5,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(TraceItemFlow), ct => flow.TraceItemFlowAsync(x, y, depth, ct), cancellationToken);
    }
}
