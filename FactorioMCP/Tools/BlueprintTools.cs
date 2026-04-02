using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for blueprint and ghost entity operations.
/// Supports placing ghost entities for planned construction, building blueprint strings,
/// scanning for existing ghosts, capturing areas as blueprints, and revoking ghosts.
/// </summary>
[McpServerToolType]
internal sealed class BlueprintTools(BlueprintService blueprints, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Place a ghost entity (construction plan) at the specified position. " +
        "Ghost entities are transparent overlays that bots or players can fill in later. " +
        "Does NOT require the item in inventory — ghosts are free placement plans. " +
        "Use this to plan factory layouts before committing resources.")]
    public Task<string> PlaceGhostEntity(
        [Description("Entity prototype name (e.g. 'stone-furnace', 'transport-belt', 'assembling-machine-1')")]
        string entityName,
        [Description("X coordinate on the map")]
        double x,
        [Description("Y coordinate on the map")]
        double y,
        [Description("Direction the entity faces: north, south, east, west, northeast, northwest, southeast, southwest")]
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlaceGhostEntity),
            ct => blueprints.PlaceGhostEntityAsync(entityName, x, y, direction, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Build a blueprint from a blueprint string at the specified position. " +
        "Blueprint strings are base64-encoded strings that encode entity layouts " +
        "(e.g. copied from the game or generated). Entities are placed as ghosts " +
        "unless materials are available. Use 'forced' build mode to auto-deconstruct trees/rocks.")]
    public Task<string> PlaceBlueprintString(
        [Description("Blueprint string (base64-encoded, starts with '0')")]
        string blueprintString,
        [Description("X coordinate for the blueprint center")]
        double x,
        [Description("Y coordinate for the blueprint center")]
        double y,
        [Description("Direction to rotate the blueprint: north, south, east, west")]
        string direction = "north",
        [Description("Build mode: 'normal' (fail if blocked), 'forced' (deconstruct nature), 'superforced' (deconstruct all)")]
        string buildMode = "normal",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlaceBlueprintString),
            ct => blueprints.PlaceBlueprintStringAsync(blueprintString, x, y, direction, buildMode, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Scan for ghost entities (planned constructions) near a position. " +
        "Returns ghost names, positions, and directions. " +
        "Use this to see what has been planned but not yet built.")]
    public Task<string> GetGhostEntities(
        [Description("Search radius in tiles. Default 50.")]
        double radius = 50,
        [Description("Optional X coordinate of scan center. If omitted, uses player position.")]
        double? centerX = null,
        [Description("Optional Y coordinate of scan center. If omitted, uses player position.")]
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetGhostEntities),
            ct => blueprints.GetGhostEntitiesAsync(radius, centerX, centerY, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Capture entities in a rectangular area as a blueprint string. " +
        "The resulting string can be saved and used later with PlaceBlueprintString " +
        "to reproduce the same layout elsewhere. " +
        "Use this to copy existing factory sections for replication.")]
    public Task<string> CreateBlueprintFromArea(
        [Description("Left X coordinate of the capture area")]
        double x1,
        [Description("Top Y coordinate of the capture area")]
        double y1,
        [Description("Right X coordinate of the capture area")]
        double x2,
        [Description("Bottom Y coordinate of the capture area")]
        double y2,
        [Description("Whether to include tiles (like concrete) in the blueprint. Default false.")]
        bool includeTiles = false,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(CreateBlueprintFromArea),
            ct => blueprints.CreateBlueprintFromAreaAsync(x1, y1, x2, y2, includeTiles, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Remove/revoke ghost entities at a position. " +
        "Destroys planned construction ghosts within a radius of the given coordinates. " +
        "Use this to cancel planned constructions that are no longer needed.")]
    public Task<string> RevokeGhostEntity(
        [Description("X coordinate to search for ghosts")]
        double x,
        [Description("Y coordinate to search for ghosts")]
        double y,
        [Description("Search radius around the position. Default 1 tile.")]
        double radius = 1,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(RevokeGhostEntity),
            ct => blueprints.RevokeGhostEntityAsync(x, y, radius, ct),
            cancellationToken);
    }
}
