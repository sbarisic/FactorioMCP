using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for batch entity operations — execute multiple actions in a single MCP call.
/// Each tool iterates over an array of targets sequentially, failing fast on the first error.
/// Reduces LLM round-trips by collapsing N individual tool calls into 1.
/// </summary>
[McpServerToolType]
internal sealed class BatchTools(
    FactorioService factorio,
    PathfindingService pathfinding,
    BuildingMemoryService buildingMemory,
    GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Mine multiple entities in a single call. Processes each target sequentially, " +
        "failing fast on the first error. Automatically untracks mined buildings from memory. " +
        "Returns an array of results (one per target) and aggregate counts.")]
    public Task<string> MineEntityMultiple(
        [Description("JSON array of targets, each with x and y coordinates. " +
                      "Example: [{\"x\":1,\"y\":2},{\"x\":3,\"y\":4}]")]
        string targets,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(MineEntityMultiple), async ct =>
        {
            var positions = ParseTargets(targets);
            var results = new List<string>();
            int succeeded = 0, failed = 0;

            foreach (var (x, y) in positions)
            {
                var result = await factorio.MineEntityAtAsync(x, y, ct);
                results.Add(WrapResult(x, y, result));

                if (IsSuccess(result))
                {
                    succeeded++;
                    await buildingMemory.UntrackBuildingAtAsync(x, y, ct);
                }
                else
                {
                    failed++;
                    return FormatBatchResult(results, succeeded, failed, positions.Count, "failed");
                }
            }

            return FormatBatchResult(results, succeeded, failed, positions.Count, "complete");
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Inspect multiple entities in a single call. Processes each target sequentially, " +
        "failing fast on the first error. " +
        "Returns an array of inspection results (one per target) and aggregate counts.")]
    public Task<string> InspectEntityMultiple(
        [Description("JSON array of targets, each with x and y coordinates. " +
                      "Example: [{\"x\":1,\"y\":2},{\"x\":3,\"y\":4}]")]
        string targets,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(InspectEntityMultiple), async ct =>
        {
            var positions = ParseTargets(targets);
            var results = new List<string>();
            int succeeded = 0, failed = 0;

            foreach (var (x, y) in positions)
            {
                var result = await factorio.InspectEntityAsync(x, y, ct);
                results.Add(WrapResult(x, y, result));

                if (IsSuccess(result))
                    succeeded++;
                else
                {
                    failed++;
                    return FormatBatchResult(results, succeeded, failed, positions.Count, "failed");
                }
            }

            return FormatBatchResult(results, succeeded, failed, positions.Count, "complete");
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Insert items into multiple entities in a single call. Processes each target sequentially, " +
        "failing fast on the first error. " +
        "Each target specifies coordinates, item name, count, and inventory type. " +
        "Returns an array of results and aggregate counts.")]
    public Task<string> InsertItemsMultiple(
        [Description("JSON array of targets, each with x, y, item, count, and optional inventoryType. " +
                      "Example: [{\"x\":1,\"y\":2,\"item\":\"coal\",\"count\":5},{\"x\":3,\"y\":4,\"item\":\"iron-ore\",\"count\":10,\"inventoryType\":\"furnace_source\"}]")]
        string targets,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(InsertItemsMultiple), async ct =>
        {
            var items = ParseInsertTargets(targets);
            var results = new List<string>();
            int succeeded = 0, failed = 0;

            foreach (var item in items)
            {
                var result = await factorio.InsertItemsAsync(item.X, item.Y, item.Item, item.Count, item.InventoryType, ct);
                results.Add(WrapResult(item.X, item.Y, result));

                if (IsSuccess(result))
                    succeeded++;
                else
                {
                    failed++;
                    return FormatBatchResult(results, succeeded, failed, items.Count, "failed");
                }
            }

            return FormatBatchResult(results, succeeded, failed, items.Count, "complete");
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Refuel multiple entities in a single call. Walks to each entity and inserts fuel. " +
        "Processes sequentially, failing fast on the first error. " +
        "Returns an array of results and aggregate counts.")]
    public Task<string> RefuelEntityMultiple(
        [Description("JSON array of targets, each with x, y, and optional fuelItem and count. " +
                      "Example: [{\"x\":1,\"y\":2},{\"x\":3,\"y\":4,\"fuelItem\":\"wood\",\"count\":10}]")]
        string targets,
        [Description("Default fuel item name for targets that don't specify one (default 'coal')")]
        string defaultFuel = "coal",
        [Description("Default fuel count for targets that don't specify one (default 5)")]
        int defaultCount = 5,
        [Description("Maximum time for walking to each entity in seconds (default 30)")]
        double walkTimeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(RefuelEntityMultiple), async ct =>
        {
            var refuelTargets = ParseRefuelTargets(targets, defaultFuel, defaultCount);
            var results = new List<string>();
            int succeeded = 0, failed = 0;

            foreach (var target in refuelTargets)
            {
                // Walk to entity if needed
                var posJson = await pathfinding.GetPlayerPositionAsync(ct);
                var (px, py) = ParsePosition(posJson);
                var dist = Distance(px, py, target.X, target.Y);

                if (dist > 2.0)
                {
                    var walkJson = await pathfinding.WalkToAsync(target.X, target.Y, 2.0, walkTimeoutSeconds, ct);
                    using var walkDoc = JsonDocument.Parse(walkJson);
                    var walkStatus = walkDoc.RootElement.GetProperty("status").GetString();
                    if (walkStatus is "stuck" or "timeout" or "no_path")
                    {
                        failed++;
                        results.Add(string.Create(CultureInfo.InvariantCulture,
                            $"{{\"x\":{target.X},\"y\":{target.Y},\"success\":false,\"error\":\"walk_failed\",\"walk_status\":\"{walkStatus}\"}}"));
                        return FormatBatchResult(results, succeeded, failed, refuelTargets.Count, "failed");
                    }
                }

                var insertResult = await factorio.InsertItemsAsync(target.X, target.Y, target.FuelItem, target.Count, "fuel", ct);
                results.Add(WrapResult(target.X, target.Y, insertResult));

                if (IsSuccess(insertResult))
                    succeeded++;
                else
                {
                    failed++;
                    return FormatBatchResult(results, succeeded, failed, refuelTargets.Count, "failed");
                }
            }

            return FormatBatchResult(results, succeeded, failed, refuelTargets.Count, "complete");
        }, cancellationToken);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static List<(double X, double Y)> ParseTargets(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<(double, double)>();
        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var x = elem.GetProperty("x").GetDouble();
            var y = elem.GetProperty("y").GetDouble();
            result.Add((x, y));
        }
        if (result.Count == 0)
            throw new ArgumentException("Targets array must not be empty.");
        return result;
    }

    private readonly record struct InsertTarget(double X, double Y, string Item, int Count, string InventoryType);

    private static List<InsertTarget> ParseInsertTargets(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<InsertTarget>();
        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var x = elem.GetProperty("x").GetDouble();
            var y = elem.GetProperty("y").GetDouble();
            var item = elem.GetProperty("item").GetString() ?? throw new ArgumentException("Each target must have an 'item' property.");
            var count = elem.GetProperty("count").GetInt32();
            var invType = elem.TryGetProperty("inventoryType", out var inv) ? inv.GetString() ?? "fuel" : "fuel";
            result.Add(new InsertTarget(x, y, item, count, invType));
        }
        if (result.Count == 0)
            throw new ArgumentException("Targets array must not be empty.");
        return result;
    }

    private readonly record struct RefuelTarget(double X, double Y, string FuelItem, int Count);

    private static List<RefuelTarget> ParseRefuelTargets(string json, string defaultFuel, int defaultCount)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<RefuelTarget>();
        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var x = elem.GetProperty("x").GetDouble();
            var y = elem.GetProperty("y").GetDouble();
            var fuel = elem.TryGetProperty("fuelItem", out var f) ? f.GetString() ?? defaultFuel : defaultFuel;
            var count = elem.TryGetProperty("count", out var c) ? c.GetInt32() : defaultCount;
            result.Add(new RefuelTarget(x, y, fuel, count));
        }
        if (result.Count == 0)
            throw new ArgumentException("Targets array must not be empty.");
        return result;
    }

    private static bool IsSuccess(string json)
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

    private static string WrapResult(double x, double y, string innerJson)
    {
        // Inject x/y coordinates if not already present in the result
        return string.Create(CultureInfo.InvariantCulture,
            $"{{\"target_x\":{x},\"target_y\":{y},\"result\":{innerJson}}}");
    }

    private static string FormatBatchResult(List<string> results, int succeeded, int failed, int total, string status)
    {
        var resultsJson = string.Join(",", results);
        return $$"""{"success":{{(failed == 0 ? "true" : "false")}},"status":"{{status}}","total":{{total}},"succeeded":{{succeeded}},"failed":{{failed}},"results":[{{resultsJson}}]}""";
    }

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
}
