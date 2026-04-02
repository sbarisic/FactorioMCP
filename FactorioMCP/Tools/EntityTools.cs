using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for placing and mining entities, plus inserter placement preview.
/// Validates inventory contents before placement and entity existence before mining.
/// Automatically tracks/untracks buildings in <see cref="BuildingMemoryService"/>.
/// </summary>
[McpServerToolType]
internal sealed class EntityTools(FactorioService factorio, MiningService mining, BuildingMemoryService buildingMemory, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Place an entity from the player's inventory at the specified map coordinates. " +
        "Validates proximity (must be within build distance), inventory contents, and position validity before placing. " +
        "Automatically tracks the placed building in memory for future queries. " +
        "INSERTER TIPS: For inserters (burner-inserter, inserter, long-handed-inserter, fast-inserter, etc.), " +
        "the 'direction' parameter controls which way items flow. An inserter PICKS UP from the OPPOSITE side " +
        "of its direction and DROPS to the side it faces. For example, a north-facing inserter picks up from " +
        "the south tile and drops to the north tile. Place inserters directly adjacent to source and target " +
        "entities (1 tile away, not 2). Use PreviewInserterPlacement to verify pickup/drop targets before placing. " +
        "BELT TIPS: For transport belts, the 'direction' is the direction items FLOW (the way arrows point). " +
        "Use PlanBeltRoute to calculate a full belt path between two points, then place each tile with this tool.")]
    public Task<string> PlaceEntity(
        [Description("Entity/item name to place (e.g. 'stone-furnace', 'transport-belt', 'burner-inserter')")]
        string entityName,
        [Description("X coordinate on the map")]
        double x,
        [Description("Y coordinate on the map")]
        double y,
        [Description("Direction the entity faces: north, south, east, west, northeast, northwest, southeast, southwest. " +
                     "For inserters: this is the DROP direction (items flow this way). Pickup is from the opposite side.")]
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlaceEntity), async ct =>
        {
            var result = await factorio.PlaceEntityAsync(entityName, x, y, direction, ct);

            if (IsSuccessResponse(result))
            {
                await buildingMemory.TrackBuildingAsync(entityName, x, y, direction, ct);
            }

            return result;
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Mine/remove a building or non-resource entity at the specified map coordinates. " +
        "Validates proximity (must be within reach distance) before mining. " +
        "Mined items are added to the player's inventory. " +
        "Automatically removes the building from memory tracking. " +
        "NOTE: For mining resource entities (ore patches like iron-ore, copper-ore, stone, coal), " +
        "use MineResource instead — it mines with realistic timing instead of instant extraction.")]
    public Task<string> MineEntity(
        [Description("X coordinate of the entity to mine")]
        double x,
        [Description("Y coordinate of the entity to mine")]
        double y,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(MineEntity), async ct =>
        {
            var result = await factorio.MineEntityAtAsync(x, y, ct);

            if (IsSuccessResponse(result))
            {
                await buildingMemory.UntrackBuildingAtAsync(x, y, ct);
            }

            return result;
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Mine resource entities (ore patches) with realistic timing. The player character mines " +
        "one unit at a time using normal game mechanics — no instant extraction. " +
        "Specify how many units to mine and the tool will start mining, wait for the specified amount " +
        "to be extracted, then stop. Returns the actual number mined. " +
        "Use this for mining iron-ore, copper-ore, stone, coal, and other resource entities. " +
        "For mining buildings (furnaces, drills, belts, etc.), use MineEntity instead.")]
    public Task<string> MineResource(
        [Description("X coordinate of the resource entity to mine")]
        double x,
        [Description("Y coordinate of the resource entity to mine")]
        double y,
        [Description("Number of resource units to mine (default 1). The tool will mine up to this many units " +
                     "or until the resource is depleted, whichever comes first.")]
        int count = 1,
        [Description("How often to check mining progress in seconds (default 0.5)")]
        double pollIntervalSeconds = 0.5,
        [Description("Maximum time to wait for mining in seconds (default 60)")]
        double timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(MineResource),
            ct => mining.MineResourceAsync(x, y, count,
                TimeSpan.FromSeconds(pollIntervalSeconds),
                TimeSpan.FromSeconds(timeoutSeconds),
                ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Preview what an inserter would pick up from and drop to if placed at the given position and direction. " +
        "Does NOT place anything — purely informational for planning inserter layouts. " +
        "Returns the calculated pickup tile position (opposite of direction) and drop tile position (in the facing direction), " +
        "plus any entities found at those positions. Use this BEFORE placing an inserter to verify it will connect " +
        "the correct source and destination entities. " +
        "INSERTER MECHANICS: An inserter facing north picks up from south (y+1) and drops to north (y-1). " +
        "An inserter facing east picks up from west (x-1) and drops to east (x+1). " +
        "The inserter must be placed directly between the source and destination entities, exactly 1 tile from each.")]
    public Task<string> PreviewInserterPlacement(
        [Description("X coordinate where the inserter would be placed")]
        double x,
        [Description("Y coordinate where the inserter would be placed")]
        double y,
        [Description("Direction the inserter would face (= the DROP direction). Pickup is from the opposite side. " +
                     "Values: north, south, east, west, northeast, northwest, southeast, southwest")]
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PreviewInserterPlacement),
            ct => factorio.PreviewInserterPlacementAsync(x, y, direction, ct),
            cancellationToken);
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
