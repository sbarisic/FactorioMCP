using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for placing and mining entities. Validates inventory contents
/// before placement and entity existence before mining.
/// Automatically tracks/untracks buildings in <see cref="BuildingMemoryService"/>.
/// </summary>
[McpServerToolType]
internal sealed class EntityTools(FactorioService factorio, BuildingMemoryService buildingMemory)
{
    [McpServerTool, Description(
        "Place an entity from the player's inventory at the specified map coordinates. " +
        "Validates proximity (must be within build distance), inventory contents, and position validity before placing. " +
        "Automatically tracks the placed building in memory for future queries.")]
    public async Task<string> PlaceEntity(
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
        var result = await factorio.PlaceEntityAsync(entityName, x, y, direction, cancellationToken);

        if (IsSuccessResponse(result))
        {
            await buildingMemory.TrackBuildingAsync(entityName, x, y, direction, cancellationToken);
        }

        return result;
    }

    [McpServerTool, Description(
        "Mine/remove an entity at the specified map coordinates. " +
        "Validates proximity (must be within reach distance) before mining. " +
        "Mined items are added to the player's inventory. " +
        "Automatically removes the building from memory tracking.")]
    public async Task<string> MineEntity(
        [Description("X coordinate of the entity to mine")]
        double x,
        [Description("Y coordinate of the entity to mine")]
        double y,
        CancellationToken cancellationToken = default)
    {
        var result = await factorio.MineEntityAtAsync(x, y, cancellationToken);

        if (IsSuccessResponse(result))
        {
            await buildingMemory.UntrackBuildingAtAsync(x, y, cancellationToken);
        }

        return result;
    }

    private static bool IsSuccessResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("success", out var prop) && prop.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
