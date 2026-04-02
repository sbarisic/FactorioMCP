using System.Text.Json;
using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tool for getting a comprehensive factory status overview in a single call.
/// Aggregates game state (position, inventory, crafting, research, resources, power)
/// with C#-side state (building memory, active goal) into one response.
/// </summary>
[McpServerToolType]
internal sealed class StatusTools(
    FactorioService factorio,
    BuildingMemoryService buildingMemory,
    GoalPlannerService goalPlanner,
    GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get a comprehensive factory status snapshot. Returns player position, full inventory, " +
        "crafting queue, research progress, nearby resources and entities, electric power status, " +
        "building summary (count by type), and active goal with progress. " +
        "Use this to get a broad overview of the current game state before making decisions.")]
    public async Task<string> GetFactoryStatus(
        [Description("Radius to scan for resources (default 50)")]
        double resourceScanRadius = 50,
        [Description("Radius to scan for nearby entities (default 20)")]
        double entityScanRadius = 20,
        [Description("Radius to search for electric poles (default 50)")]
        double electricPoleRadius = 50,
        CancellationToken cancellationToken = default)
    {
        // Get game state via single RCON call (atomic snapshot)
        var gameStatusJson = await queue.ExecuteAsync(
            nameof(GetFactoryStatus),
            ct => factorio.GetFactoryStatusAsync(resourceScanRadius, entityScanRadius, electricPoleRadius, ct),
            cancellationToken);

        // Get C#-side state (no RCON needed)
        var buildingSummaryJson = await buildingMemory.GetBuildingSummaryAsync(cancellationToken);
        var activeGoalJson = await goalPlanner.GetActiveGoalAsync(cancellationToken);

        // Merge: inject building_summary and active_goal into the game state JSON
        // Game state is {...}, we append the two C# objects as additional keys
        if (gameStatusJson.Length > 1 && gameStatusJson[^1] == '}')
        {
            return $"{gameStatusJson[..^1]},\"building_summary\":{buildingSummaryJson},\"active_goal\":{activeGoalJson}}}";
        }

        // Fallback: return all three separately if game status isn't valid JSON object
        return JsonSerializer.Serialize(new
        {
            game = gameStatusJson,
            building_summary = buildingSummaryJson,
            active_goal = activeGoalJson
        });
    }
}
