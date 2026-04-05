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
        "INSERTER DIRECTION (CRITICAL): The 'direction' parameter is the PICKUP direction — the side where the inserter's " +
        "arm reaches out to GRAB items. Drop is ALWAYS on the OPPOSITE side. Think: 'direction = where items come FROM'. " +
        "Example: To move items FROM a chest (south) INTO a furnace (north), the inserter between them faces SOUTH " +
        "(picks up south from chest, drops north into furnace). " +
        "ALWAYS use PreviewInserterPlacement first to verify pickup/drop targets before placing an inserter. " +
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
                     "INSERTER CRITICAL: direction = PICKUP side (where the arm reaches to grab items). Drop is OPPOSITE. " +
                     "To move items from A to B, point direction TOWARD A.")]
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
        "Returns the calculated pickup tile position (in the facing direction) and drop tile position (opposite of direction), " +
        "plus any entities found at those positions. Use this BEFORE placing an inserter to verify it will connect " +
        "the correct source and destination entities. " +
        "INSERTER MECHANICS: An inserter facing north picks up from north (y-1) and drops to south (y+1). " +
        "An inserter facing east picks up from east (x+1) and drops to west (x-1). " +
        "The inserter must be placed directly between the source and destination entities, exactly 1 tile from each.")]
    public Task<string> PreviewInserterPlacement(
        [Description("X coordinate where the inserter would be placed")]
        double x,
        [Description("Y coordinate where the inserter would be placed")]
        double y,
        [Description("Direction the inserter would face (= the PICKUP direction). Drop is on the opposite side. " +
                     "Values: north, south, east, west, northeast, northwest, southeast, southwest")]
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PreviewInserterPlacement),
            ct => factorio.PreviewInserterPlacementAsync(x, y, direction, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Rotate a building or entity at the specified map coordinates. " +
        "Cycles through valid directions for the entity type (e.g. north → east → south → west for most entities). " +
        "Uses the game engine's built-in rotation which respects entity-type constraints. " +
        "Validates proximity (must be within reach distance) before rotating. " +
        "Automatically updates the building's direction in memory tracking. " +
        "Essential for correcting belt direction, inserter orientation, and assembler setup.")]
    public Task<string> RotateEntity(
        [Description("X coordinate of the entity to rotate")]
        double x,
        [Description("Y coordinate of the entity to rotate")]
        double y,
        [Description("If true, rotate counter-clockwise instead of clockwise (default false)")]
        bool reverse = false,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(RotateEntity), async ct =>
        {
            var result = await factorio.RotateEntityAsync(x, y, reverse, ct);

            if (IsSuccessResponse(result, out var newDirection) && newDirection is not null)
            {
                await buildingMemory.UpdateBuildingDirectionAsync(x, y, newDirection, ct);
            }

            return result;
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Place an inserter adjacent to a target entity on a specific side with automatic direction calculation. " +
        "Specify the target entity position, which side to place the inserter on (north/south/east/west), " +
        "and whether the inserter should move items INTO the target (inbound) or OUT OF the target (outbound). " +
        "The tool calculates the correct inserter tile position using the target's bounding box and sets " +
        "the inserter direction automatically. No geometry math needed — just say where and which way items flow.")]
    public Task<string> PlaceInserter(
        [Description("Name of the inserter to place (e.g. 'burner-inserter', 'inserter', 'fast-inserter', 'long-handed-inserter')")]
        string inserterName,
        [Description("X coordinate of the TARGET entity (not the inserter position)")]
        double targetX,
        [Description("Y coordinate of the TARGET entity (not the inserter position)")]
        double targetY,
        [Description("Which side of the target entity to place the inserter on: north, south, east, west")]
        string side,
        [Description("If true (inbound), the inserter drops items INTO the target. " +
                     "If false (outbound), the inserter picks items FROM the target. " +
                     "Example: inbound=true on a furnace means the inserter feeds ore into it.")]
        bool inbound = true,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlaceInserter), async ct =>
        {
            var result = await factorio.PlaceInserterAsync(inserterName, targetX, targetY, side, inbound, ct);

            if (IsSuccessResponse(result))
            {
                // Parse the placed position from the response to track it
                try
                {
                    using var doc = JsonDocument.Parse(result);
                    var root = doc.RootElement;
                    var ix = root.GetProperty("x").GetDouble();
                    var iy = root.GetProperty("y").GetDouble();
                    var dir = root.GetProperty("direction").GetString() ?? "north";
                    await buildingMemory.TrackBuildingAsync(inserterName, ix, iy, dir, ct);
                }
                catch (JsonException) { }
            }

            return result;
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Automatically place an inserter between two adjacent entities to move items from source to destination. " +
        "The tool finds both entities, calculates the midpoint for inserter placement, and orients the inserter " +
        "so items flow from source to destination. Both entities must be adjacent (1 tile gap between them). " +
        "This is the easiest way to set up item transfer — no need to calculate positions or directions manually.")]
    public Task<string> InsertBetween(
        [Description("Name of the inserter to place (e.g. 'burner-inserter', 'inserter', 'fast-inserter')")]
        string inserterName,
        [Description("X coordinate of the SOURCE entity (where items come FROM)")]
        double sourceX,
        [Description("Y coordinate of the SOURCE entity")]
        double sourceY,
        [Description("X coordinate of the DESTINATION entity (where items go TO)")]
        double destX,
        [Description("Y coordinate of the DESTINATION entity")]
        double destY,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(InsertBetween), async ct =>
        {
            var result = await factorio.InsertBetweenAsync(inserterName, sourceX, sourceY, destX, destY, ct);

            if (IsSuccessResponse(result))
            {
                try
                {
                    using var doc = JsonDocument.Parse(result);
                    var root = doc.RootElement;
                    var ix = root.GetProperty("x").GetDouble();
                    var iy = root.GetProperty("y").GetDouble();
                    var dir = root.GetProperty("direction").GetString() ?? "north";
                    await buildingMemory.TrackBuildingAsync(inserterName, ix, iy, dir, ct);
                }
                catch (JsonException) { }
            }

            return result;
        }, cancellationToken);
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

    [McpServerTool, Description(
        "Automatically place an entity near a target position. Searches outward in a spiral " +
        "from the given coordinates for the nearest valid placement position using can_place_entity. " +
        "Validates inventory and build distance. " +
        "Use this instead of PlaceEntity when you want the backend to pick the best spot — " +
        "e.g. place a stone-furnace near an ore patch, or a drill near iron-ore.")]
    public Task<string> PlaceEntitySmart(
        [Description("Entity/item name to place (e.g. 'stone-furnace', 'burner-mining-drill', 'transport-belt')")]
        string entityName,
        [Description("X coordinate to place near")]
        double nearX,
        [Description("Y coordinate to place near")]
        double nearY,
        [Description("Direction the entity faces: north, south, east, west")]
        string direction = "north",
        [Description("How far from the target to search in tiles (default 10)")]
        double searchRadius = 10,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlaceEntitySmart), async ct =>
        {
            var result = await factorio.PlaceEntitySmartAsync(entityName, nearX, nearY, direction, searchRadius, ct);

            if (IsSuccessResponse(result))
            {
                try
                {
                    using var doc = JsonDocument.Parse(result);
                    var root = doc.RootElement;
                    var px = root.GetProperty("x").GetDouble();
                    var py = root.GetProperty("y").GetDouble();
                    await buildingMemory.TrackBuildingAsync(entityName, px, py, direction, ct);
                }
                catch (JsonException) { }
            }

            return result;
        }, cancellationToken);
    }

    private static bool IsSuccessResponse(string json, out string? newDirection)
    {
        newDirection = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("success", out var prop) || !prop.GetBoolean())
                return false;

            if (doc.RootElement.TryGetProperty("new_direction", out var dirProp))
                newDirection = dirProp.GetString();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
