using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for factory analysis — finding unpowered entities, idle machines,
/// and diagnosing missing inputs.
/// </summary>
[McpServerToolType]
internal sealed class FactoryTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Find entities that have no power or low power within the specified radius. " +
        "Returns a list of unpowered entities with their type, status, and position. " +
        "Use this to diagnose power distribution problems in your factory.")]
    public Task<string> FindUnpoweredEntities(
        [Description("Search radius in tiles (default 50)")]
        double radius = 50,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(FindUnpoweredEntities),
            ct => factorio.FindUnpoweredEntitiesAsync(radius, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Find machines that are idle (not working) within the specified radius. " +
        "Filters out passive entities (belts, pipes, poles, walls). " +
        "Returns each idle machine with its name, type, status reason, and position. " +
        "Use this to find bottlenecks — machines waiting for fuel, ingredients, or output space.")]
    public Task<string> FindIdleMachines(
        [Description("Search radius in tiles (default 50)")]
        double radius = 50,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(FindIdleMachines),
            ct => factorio.FindIdleMachinesAsync(radius, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Check which inputs a furnace or assembler at the given position is missing. " +
        "Inspects fuel, source/input inventories, and output fullness to diagnose why " +
        "a machine isn't working. Returns a list of missing items with slot, issue type, " +
        "and amounts (have vs need). " +
        "Use this to figure out exactly what a specific machine needs to start working.")]
    public Task<string> FindMissingInputs(
        [Description("X coordinate of the machine to check")]
        double x,
        [Description("Y coordinate of the machine to check")]
        double y,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(FindMissingInputs),
            ct => factorio.FindMissingInputsAsync(x, y, ct),
            cancellationToken);
    }
}
