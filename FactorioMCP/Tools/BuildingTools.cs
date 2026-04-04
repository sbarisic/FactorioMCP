using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for querying and managing the AI's building memory.
/// Buildings are automatically tracked when placed and untracked when mined.
/// These tools provide spatial queries and factory awareness.
/// </summary>
[McpServerToolType]
internal sealed class BuildingTools(BuildingMemoryService buildings)
{
    [McpServerTool, Description(
        "Get all buildings the AI has placed, with their names, positions, directions, and labels. " +
        "Use this to review the full factory layout.")]
    public Task<string> GetAllBuildings(CancellationToken cancellationToken = default)
    {
        return buildings.GetAllBuildingsAsync(cancellationToken);
    }

    [McpServerTool, Description(
        "Get buildings near a specific position within a radius. " +
        "Returns buildings sorted by distance. Useful for checking what's already " +
        "built in an area before placing new entities.")]
    public Task<string> GetBuildingsNear(
        [Description("X coordinate of the center position")]
        double x,
        [Description("Y coordinate of the center position")]
        double y,
        [Description("Search radius in tiles (default 20)")]
        double radius = 20,
        CancellationToken cancellationToken = default)
    {
        return buildings.GetBuildingsNearAsync(x, y, radius, cancellationToken);
    }

    [McpServerTool, Description(
        "Find all buildings of a specific entity type. " +
        "Example: FindBuildingsByType('stone-furnace') returns all placed stone furnaces.")]
    public Task<string> FindBuildingsByType(
        [Description("Entity name to search for (e.g. 'stone-furnace', 'transport-belt')")]
        string entityName,
        CancellationToken cancellationToken = default)
    {
        return buildings.FindBuildingsByTypeAsync(entityName, cancellationToken);
    }

    [McpServerTool, Description(
        "Get a summary of all building types and their counts. " +
        "Shows how many of each entity type the AI has placed. " +
        "Useful for factory planning and resource estimation.")]
    public Task<string> GetBuildingSummary(CancellationToken cancellationToken = default)
    {
        return buildings.GetBuildingSummaryAsync(cancellationToken);
    }

    [McpServerTool, Description(
        "Set or update a label on a building at the specified position. " +
        "Labels help identify the purpose of buildings (e.g. 'iron smelter #1', 'main bus belt'). " +
        "Pass null to remove the label.")]
    public Task<string> UpdateBuildingLabel(
        [Description("X coordinate of the building")]
        double x,
        [Description("Y coordinate of the building")]
        double y,
        [Description("Label text, or null to remove")]
        string? label,
        CancellationToken cancellationToken = default)
    {
        return buildings.UpdateBuildingLabelAsync(x, y, label, cancellationToken);
    }

    [McpServerTool, Description(
        "Clear all tracked buildings from memory. " +
        "Use this when starting a new factory or if the tracking data is out of sync. " +
        "This does NOT remove buildings from the game world — only from the AI's memory.")]
    public Task<string> ClearBuildingMemory(CancellationToken cancellationToken = default)
    {
        return buildings.ClearAllBuildingsAsync(cancellationToken);
    }

    [McpServerTool, Description(
        "Validate all tracked buildings against the actual game world. " +
        "Checks each tracked building position via RCON to see if the entity still exists. " +
        "Removes any buildings from memory that were destroyed, manually removed, or otherwise missing. " +
        "Call this periodically or when building memory seems out of sync with the game world.")]
    public Task<string> ValidateBuildingMemory(CancellationToken cancellationToken = default)
    {
        return buildings.ValidateBuildingMemoryAsync(cancellationToken);
    }
}
