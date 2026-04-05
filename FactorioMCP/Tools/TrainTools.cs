using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for managing and inspecting trains and train stations.
/// Uses game.train_manager for network-wide queries and LuaTrain for per-train control.
/// </summary>
[McpServerToolType]
internal sealed class TrainTools(TrainService trains, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "List all trains on the player's current surface. " +
        "Returns each train's ID, state (on_the_path, wait_station, manual_control, etc.), " +
        "position, speed, locomotive count, cargo wagon count, and which station it is stopped at. " +
        "Use this to get a broad overview of your rail network traffic.")]
    public Task<string> GetTrains(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetTrains), trains.GetTrainsAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "List all train stops on the player's current surface. " +
        "Returns each stop's name, position, and the ID of any train currently docked there. " +
        "Use this to plan new rail routes or find unused/busy stations.")]
    public Task<string> GetTrainStops(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetTrainStops), trains.GetTrainStopsAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Inspect a specific train by its numeric ID. " +
        "Returns full details: state, current speed, schedule (ordered list of station names), " +
        "and current cargo contents (item name + count). " +
        "Use GetTrains first to find the train ID you want to inspect.")]
    public Task<string> InspectTrain(
        [Description("Numeric train ID as returned by GetTrains")]
        uint trainId,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(InspectTrain), ct => trains.InspectTrainAsync(trainId, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Switch a train between manual mode and automatic (schedule-driven) mode. " +
        "When manual_mode=true the train stops and waits for player/script commands. " +
        "When manual_mode=false the train follows its schedule autonomously. " +
        "Use GetTrains to find the train ID.")]
    public Task<string> SetTrainMode(
        [Description("Numeric train ID")]
        uint trainId,
        [Description("True to switch to manual control, false to resume automatic schedule")]
        bool manual,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(SetTrainMode), ct => trains.SetTrainModeAsync(trainId, manual, ct), cancellationToken);
    }
}
