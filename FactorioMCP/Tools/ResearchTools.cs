using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for managing technology research — querying available technologies,
/// starting research, and checking progress.
/// </summary>
[McpServerToolType]
internal sealed class ResearchTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get the current research status and progress for the player's force. " +
        "Shows the technology being researched and its completion percentage.")]
    public Task<string> GetResearchStatus(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetResearchStatus), factorio.GetResearchStatusAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Get all technologies currently available for research. " +
        "Returns technologies that are enabled, not yet researched, and have all prerequisites met. " +
        "Each entry includes the technology name, research unit cost, and required science pack ingredients.")]
    public Task<string> GetAvailableTechnologies(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetAvailableTechnologies), factorio.GetAvailableTechnologiesAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Start researching a technology by adding it to the research queue. " +
        "If no research is currently in progress, it begins immediately. " +
        "Use GetAvailableTechnologies first to see which technologies can be researched.")]
    public Task<string> StartResearch(
        [Description("Technology name to research (e.g. 'automation', 'logistics', 'steel-processing')")]
        string technology,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(StartResearch), ct => factorio.StartResearchAsync(technology, ct), cancellationToken);
    }
}
