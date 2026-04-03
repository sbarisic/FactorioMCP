using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for smart navigation — find a target and walk to it in a single call.
/// Combines scanning/searching with WalkToPosition to eliminate multi-step
/// scan → parse → walk sequences that waste LLM tokens.
/// </summary>
[McpServerToolType]
internal sealed class NavigationTools(FactorioService factorio, BuildingMemoryService buildingMemory, GameCommandQueue queue)
{
    /// <summary>
    /// Interval between position polls during walking. Exposed for test overrides.
    /// </summary>
    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(0.5);
    [McpServerTool, Description(
        "Find the nearest entity matching a name or type and walk to it in one call. " +
        "Searches by entity name first (e.g. 'stone-furnace', 'iron-ore'), then falls back " +
        "to entity type (e.g. 'furnace', 'resource', 'inserter'). " +
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

            var walk = await WalkToTargetAsync(targetX, targetY, tolerance, timeoutSeconds, ct);
            return FormatResult("entity", name, targetX, targetY, walk);
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Find the best resource patch and walk to its center in one call. " +
        "Selects the optimal patch using a heuristic that balances distance and richness. " +
        "Replaces the manual pattern of FindBestResourcePatch → read center → WalkToPosition.")]
    public Task<string> MoveToResource(
        [Description("Resource name (e.g. 'iron-ore', 'copper-ore', 'stone', 'coal', 'crude-oil')")]
        string resourceName,
        [Description("Search radius in tiles (default 200)")]
        double radius = 200,
        [Description("Stop when within this many tiles of the patch center (default 5.0)")]
        double tolerance = 5.0,
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

            var walk = await WalkToTargetAsync(targetX, targetY, tolerance, timeoutSeconds, ct);
            return FormatResult("resource", resourceName, targetX, targetY, walk);
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Find a tracked building by label or entity type and walk to it in one call. " +
        "Searches AI building memory — first by label (case-insensitive match), " +
        "then by entity name. Walks to the closest match. " +
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

            var walk = await WalkToTargetAsync(building.X, building.Y, tolerance, timeoutSeconds, ct);
            return FormatResult("building", displayName, building.X, building.Y, walk);
        }, cancellationToken);
    }

    // ── Walk Logic ──────────────────────────────────────────────────

    private async Task<WalkResult> WalkToTargetAsync(
        double targetX, double targetY, double tolerance, double timeoutSeconds, CancellationToken ct)
    {
        var pollInterval = PollInterval;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 0.5));

        var posJson = await factorio.GetPlayerPositionAsync(ct);
        var (px, py) = ParsePosition(posJson);
        var dist = Distance(px, py, targetX, targetY);

        if (dist <= tolerance)
            return new WalkResult("arrived", px, py, dist);

        var direction = MovementTools.CalculateDirection(px, py, targetX, targetY);
        await factorio.WalkAsync(direction, ct);

        double prevX = px, prevY = py;
        int stuckPolls = 0;
        const int maxStuckPolls = 6;
        const double minMovement = 0.15;

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(pollInterval, ct);

                posJson = await factorio.GetPlayerPositionAsync(ct);
                (px, py) = ParsePosition(posJson);
                dist = Distance(px, py, targetX, targetY);

                if (dist <= tolerance)
                {
                    await factorio.StopWalkingAsync(ct);
                    return new WalkResult("arrived", px, py, dist);
                }

                var pollMovement = Distance(px, py, prevX, prevY);
                if (pollMovement < minMovement)
                {
                    stuckPolls++;
                    if (stuckPolls >= maxStuckPolls)
                    {
                        await factorio.StopWalkingAsync(ct);
                        return new WalkResult("stuck", px, py, dist);
                    }
                }
                else
                {
                    stuckPolls = 0;
                }

                var newDirection = MovementTools.CalculateDirection(px, py, targetX, targetY);
                if (newDirection != direction)
                {
                    direction = newDirection;
                    await factorio.WalkAsync(direction, ct);
                }

                prevX = px;
                prevY = py;
            }
        }
        catch
        {
            try { await factorio.StopWalkingAsync(CancellationToken.None); } catch { }
            throw;
        }

        // Timeout
        await factorio.StopWalkingAsync(ct);
        posJson = await factorio.GetPlayerPositionAsync(ct);
        (px, py) = ParsePosition(posJson);
        dist = Distance(px, py, targetX, targetY);
        return new WalkResult("timeout", px, py, dist);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private readonly record struct WalkResult(string Status, double X, double Y, double RemainingDistance);

    private static (double x, double y) ParsePosition(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble());
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string FormatNotFound(string targetType, string search, double radius)
    {
        return radius > 0
            ? string.Create(CultureInfo.InvariantCulture,
                $$"""{"success":false,"target_type":"{{targetType}}","search":"{{search}}","radius":{{radius}},"error":"not_found"}""")
            : $$"""{"success":false,"target_type":"{{targetType}}","search":"{{search}}","error":"not_found"}""";
    }

    private static string FormatResult(string targetType, string targetName, double targetX, double targetY, WalkResult walk)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"success":{{(walk.Status == "arrived" ? "true" : "false")}},"target_type":"{{targetType}}","target":"{{targetName}}","target_x":{{targetX}},"target_y":{{targetY}},"walk_status":"{{walk.Status}}","player_x":{{walk.X}},"player_y":{{walk.Y}},"remaining_distance":{{string.Format(CultureInfo.InvariantCulture, "{0:F2}", walk.RemainingDistance)}}}""");
    }
}
