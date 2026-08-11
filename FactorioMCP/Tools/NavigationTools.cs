using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for smart navigation — find a target and walk to it in a single call.
/// Combines scanning/searching with pathfinding to eliminate multi-step
/// scan → parse → walk sequences that waste LLM tokens.
/// </summary>
[McpServerToolType]
internal sealed class NavigationTools(FactorioService factorio, PathfindingService pathfinding, BuildingMemoryService buildingMemory, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Find the nearest entity matching a name or type and walk to it in one call. " +
        "Searches by entity name first (e.g. 'stone-furnace', 'iron-ore'), then falls back " +
        "to entity type (e.g. 'furnace', 'resource', 'inserter'). " +
        "Uses A* pathfinding around obstacles. " +
        "Replaces the manual pattern of FindNearest → read position → WalkToPosition.")]
    public Task<string> MoveToEntity(
        [Description("Entity name or type to search for (e.g. 'stone-furnace', 'iron-ore', 'resource', 'inserter')")]
        string entityType,
        [Description("Search radius in tiles (default 100)")]
        double radius = 100,
        [Description("Stop when within this many tiles of the entity (default 2.0)")]
        double tolerance = 2.0,
        [Description("Maximum walk time in seconds before giving up (default 30)")]
        double timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(MoveToEntity), async ct =>
        {
            var json = await factorio.FindNearestEntityAsync(entityType, radius, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.GetProperty("success").GetBoolean())
                return FormatNotFound("entity", entityType, radius);

            var targetX = root.GetProperty("x").GetDouble();
            var targetY = root.GetProperty("y").GetDouble();
            var name = root.GetProperty("entity").GetString() ?? entityType;

            var walkResult = await pathfinding.WalkToAsync(targetX, targetY, tolerance, timeoutSeconds, ct);
            return FormatResult("entity", name, targetX, targetY, walkResult);
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Find the best resource patch and walk to its center in one call. " +
        "Selects the optimal patch using a heuristic that balances distance and richness. " +
        "Uses A* pathfinding around obstacles. " +
        "Replaces the manual pattern of FindBestResourcePatch → read center → WalkToPosition.")]
    public Task<string> MoveToResource(
        [Description("Resource name (e.g. 'iron-ore', 'copper-ore', 'stone', 'coal', 'crude-oil')")]
        string resourceName,
        [Description("Search radius in tiles (default 200)")]
        double radius = 200,
        [Description("Stop when within this many tiles of the patch center (default 3.0)")]
        double tolerance = 3.0,
        [Description("Maximum walk time in seconds before giving up (default 60)")]
        double timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(MoveToResource), async ct =>
        {
            var json = await factorio.FindBestResourcePatchAsync(resourceName, radius, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.GetProperty("success").GetBoolean())
                return FormatNotFound("resource", resourceName, radius);

            var patch = root.GetProperty("best_patch");
            var targetX = patch.GetProperty("center_x").GetDouble();
            var targetY = patch.GetProperty("center_y").GetDouble();

            var walkResult = await pathfinding.WalkToAsync(targetX, targetY, tolerance, timeoutSeconds, ct);
            return FormatResult("resource", resourceName, targetX, targetY, walkResult);
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Find a tracked building by label or entity type and walk to it in one call. " +
        "Searches AI building memory — first by label (case-insensitive match), " +
        "then by entity name. Walks to the closest match using A* pathfinding. " +
        "Use MoveToEntity to navigate to any world entity, not just AI-placed buildings.")]
    public Task<string> MoveToBuilding(
        [Description("Building label or entity name to search for (e.g. 'main smelter', 'stone-furnace', 'iron output chest')")]
        string searchTerm,
        [Description("Stop when within this many tiles of the building (default 2.0)")]
        double tolerance = 2.0,
        [Description("Maximum walk time in seconds before giving up (default 30)")]
        double timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(MoveToBuilding), async ct =>
        {
            var posJson = await factorio.GetPlayerPositionAsync(ct);
            var (playerX, playerY) = ParsePosition(posJson);

            var building = await buildingMemory.FindClosestBuildingAsync(searchTerm, playerX, playerY, ct);

            if (building is null)
                return FormatNotFound("building", searchTerm, 0);

            var displayName = building.Label is not null
                ? $"{building.EntityName} ({building.Label})"
                : building.EntityName;

            var walkResult = await pathfinding.WalkToAsync(building.X, building.Y, tolerance, timeoutSeconds, ct);
            return FormatResult("building", displayName, building.X, building.Y, walkResult);
        }, cancellationToken);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static (double x, double y) ParsePosition(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble());
    }

    private static string FormatNotFound(string targetType, string search, double radius)
    {
        return radius > 0
            ? string.Create(CultureInfo.InvariantCulture,
                $$"""{"success":false,"target_type":"{{targetType}}","search":"{{search}}","radius":{{radius}},"error":"not_found"}""")
            : $$"""{"success":false,"target_type":"{{targetType}}","search":"{{search}}","error":"not_found"}""";
    }

    private static string FormatResult(string targetType, string targetName, double targetX, double targetY, string walkResultJson)
    {
        // Parse the walk result to extract status and position
        using var doc = JsonDocument.Parse(walkResultJson);
        var root = doc.RootElement;
        var status = root.GetProperty("status").GetString() ?? "unknown";
        var px = root.GetProperty("x").GetDouble();
        var py = root.GetProperty("y").GetDouble();
        var remaining = root.TryGetProperty("distance", out var distProp)
            ? distProp.GetDouble()
            : 0.0;

        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"success":{{(status == "arrived" ? "true" : "false")}},"target_type":"{{targetType}}","target":"{{targetName}}","target_x":{{targetX}},"target_y":{{targetY}},"walk_status":"{{status}}","player_x":{{px}},"player_y":{{py}},"remaining_distance":{{remaining}}}""");
    }
}
