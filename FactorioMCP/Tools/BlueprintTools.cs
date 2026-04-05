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
internal sealed class BlueprintTools(BlueprintService blueprints, BlueprintCodecService codec, BlueprintAnalysisService analysis, GameCommandQueue queue)
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
        "Place multiple ghost entities in a single call. Much more efficient than calling " +
        "PlaceGhostEntity repeatedly — places N ghosts in one RCON round-trip. " +
        "Input is a JSON array of placement entries. Returns placed/skipped counts and any errors. " +
        "Use this for batch layout operations like placing an entire smelter line at once.")]
    public Task<string> PlaceGhostBatch(
        [Description("JSON array of placements: [{\"name\":\"stone-furnace\",\"x\":0,\"y\":0,\"direction\":\"north\"}, ...]. " +
                     "Each entry needs 'name', 'x', 'y'. 'direction' defaults to 'north' if omitted.")]
        string placementsJson,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlaceGhostBatch),
            ct => blueprints.PlaceGhostBatchAsync(placementsJson, ct),
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

    [McpServerTool, Description(
        "Validate ghost entity placements in an area. " +
        "Checks each ghost for placement issues: blocked positions and " +
        "inserters pointing at nothing useful (no pickup or drop target). " +
        "Use this after placing ghosts to verify the plan before committing real entities.")]
    public Task<string> ValidateGhostPlacements(
        [Description("Search radius in tiles. Default 50.")]
        double radius = 50,
        [Description("Optional X coordinate of scan center. If omitted, uses player position.")]
        double? centerX = null,
        [Description("Optional Y coordinate of scan center. If omitted, uses player position.")]
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ValidateGhostPlacements),
            ct => blueprints.ValidateGhostPlacementsAsync(radius, centerX, centerY, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Decode a Factorio blueprint string into readable JSON. " +
        "Use this to analyze player blueprints — understand what entities are in them, " +
        "check layouts, count machines, and verify designs. " +
        "Supports blueprints, blueprint books, deconstruction planners, and upgrade planners.")]
    public Task<string> DecodeBlueprintString(
        [Description("The blueprint string to decode (starts with '0', base64-encoded)")]
        string blueprintString,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(DecodeBlueprintString),
            _ => Task.FromResult(codec.DecodeBlueprintString(blueprintString)),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Encode a JSON object into a Factorio blueprint string that can be imported into the game. " +
        "Input must be valid blueprint JSON with a 'blueprint' or 'blueprint_book' root key. " +
        "Use this to create shareable blueprint strings from planned layouts.")]
    public Task<string> EncodeBlueprintString(
        [Description("Blueprint JSON to encode. Must have a 'blueprint' or 'blueprint_book' root key. " +
                     "Example: {\"blueprint\":{\"item\":\"blueprint\",\"entities\":[{\"entity_number\":1,\"name\":\"transport-belt\",\"position\":{\"x\":0.5,\"y\":0.5},\"direction\":2}],\"version\":562949954076672}}")]
        string blueprintJson,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(EncodeBlueprintString),
            _ => Task.FromResult(codec.EncodeBlueprintString(blueprintJson)),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Analyze a blueprint string for layout quality and item flow. " +
        "Decodes the blueprint, builds a flow graph from entity positions and directions, " +
        "traces belt chains, identifies inserter connections (pickup/drop targets), " +
        "and detects issues like orphaned inserters or dead-end belts. " +
        "Works entirely offline — no need to place the blueprint in-game first. " +
        "Use this when a player shares a blueprint and asks 'is my layout OK?'")]
    public Task<string> AnalyzeBlueprint(
        [Description("The blueprint string to analyze (starts with '0', base64-encoded)")]
        string blueprintString,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(AnalyzeBlueprint),
            _ => Task.FromResult(analysis.AnalyzeBlueprint(blueprintString)),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Trace item flow from a specific entity in a blueprint. " +
        "Performs a breadth-first search from the given entity number, following " +
        "belt connections and inserter drop paths downstream. Belt-to-belt hops are free " +
        "(don't count toward depth), so the trace focuses on machine-to-machine flow. " +
        "Use after AnalyzeBlueprint to drill into a specific flow path.")]
    public Task<string> TraceBlueprintFlow(
        [Description("The blueprint string to trace (starts with '0', base64-encoded)")]
        string blueprintString,
        [Description("Entity number to start tracing from (from AnalyzeBlueprint output)")]
        int startEntityNumber,
        [Description("Maximum depth to trace (belt hops are free). Default 10.")]
        int maxDepth = 10,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(TraceBlueprintFlow),
            _ => Task.FromResult(analysis.TraceBlueprintFlow(blueprintString, startEntityNumber, maxDepth)),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Analyze a blueprint's production throughput using live recipe data from the game. " +
        "Calculates per-machine output rates, total item production/consumption balance, " +
        "identifies inserter bottlenecks (where inserter throughput < machine demand), " +
        "and recommends belt tiers based on throughput requirements. " +
        "Requires a running Factorio game for recipe lookups. " +
        "Use this to validate that a blueprint's production chain is balanced.")]
    public Task<string> AnalyzeBlueprintProduction(
        [Description("The blueprint string to analyze (starts with '0', base64-encoded)")]
        string blueprintString,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(AnalyzeBlueprintProduction),
            _ => analysis.AnalyzeBlueprintProductionAsync(blueprintString, cancellationToken),
            cancellationToken);
    }
}
