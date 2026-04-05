using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for querying the logistic robot network: network statistics,
/// item inventory, and robot status. All queries target the logistic network
/// that covers the player's current position.
/// </summary>
[McpServerToolType]
internal sealed class LogisticsTools(LogisticsService logistics, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get statistics for the logistic network at the player's current position. " +
        "Returns the network ID, total and available robot counts (logistic and construction), " +
        "robot limit, and counts of provider/requester/storage entities. " +
        "Use this to quickly check whether the network has robot capacity and roboport coverage.")]
    public Task<string> GetLogisticNetwork(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetLogisticNetwork), logistics.GetLogisticNetworkAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Get the complete item inventory of the logistic network at the player's position. " +
        "Returns all items currently stored in provider and storage chests, with counts. " +
        "Use this to check what resources are available for robot delivery before placing requests.")]
    public Task<string> GetNetworkContents(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetNetworkContents), logistics.GetNetworkContentsAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Get a detailed breakdown of logistic and construction robot activity in the network " +
        "at the player's position. Returns idle vs. busy robot counts for both robot types, " +
        "and a sample of up to 20 currently active logistic robot positions. " +
        "Use this to diagnose delivery bottlenecks or check whether robots are overwhelmed.")]
    public Task<string> GetRobotStatus(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetRobotStatus), logistics.GetRobotStatusAsync, cancellationToken);
    }
}
