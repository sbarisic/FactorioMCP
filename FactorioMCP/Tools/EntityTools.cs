using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for placing and mining entities. Validates inventory contents
/// before placement and entity existence before mining.
/// </summary>
[McpServerToolType]
internal sealed class EntityTools(FactorioService factorio)
{
    [McpServerTool, Description(
        "Place an entity from the player's inventory at the specified map coordinates. " +
        "Validates proximity (must be within build distance), inventory contents, and position validity before placing.")]
    public Task<string> PlaceEntity(
        [Description("Entity/item name to place (e.g. 'stone-furnace', 'transport-belt', 'assembling-machine-1')")]
        string entityName,
        [Description("X coordinate on the map")]
        double x,
        [Description("Y coordinate on the map")]
        double y,
        [Description("Direction the entity faces: north, south, east, west, northeast, northwest, southeast, southwest")]
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        return factorio.PlaceEntityAsync(entityName, x, y, direction, cancellationToken);
    }

    [McpServerTool, Description(
        "Mine/remove an entity at the specified map coordinates. " +
        "Validates proximity (must be within reach distance) before mining. " +
        "Mined items are added to the player's inventory.")]
    public Task<string> MineEntity(
        [Description("X coordinate of the entity to mine")]
        double x,
        [Description("Y coordinate of the entity to mine")]
        double y,
        CancellationToken cancellationToken = default)
    {
        return factorio.MineEntityAtAsync(x, y, cancellationToken);
    }
}
