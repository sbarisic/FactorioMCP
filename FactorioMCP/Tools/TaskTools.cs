using FactorioMCP.Models;
using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace FactorioMCP.Tools;

/// <summary>
/// High-level compound MCP tools that collapse multi-step operations into single calls.
/// Each tool orchestrates target-finding, walking, interacting, and waiting internally,
/// reducing 10–20 atomic tool calls down to 1.
/// </summary>
[McpServerToolType]
internal sealed class TaskTools(
    FactorioService factorio,
    PathfindingService pathfinding,
    MiningService mining,
    BuildingMemoryService buildingMemory,
    GameCommandQueue queue)
{

    [McpServerTool, Description(
        "Find a resource patch, walk to it, and mine a specified number of units — all in one call. " +
        "Combines FindBestResourcePatch → walk → find nearest entity → MineResource. " +
        "Uses realistic tick-based mining, not instant extraction. " +
        "Returns walk result, mine result, and actual amount mined.")]
    public Task<string> GatherResource(
        [Description("Resource name to mine (e.g. 'iron-ore', 'copper-ore', 'stone', 'coal')")]
        string resource,
        [Description("Number of resource units to mine (default 10)")]
        int count = 10,
        [Description("Search radius for finding resource patches (default 200)")]
        double searchRadius = 200,
        [Description("Maximum time for the entire operation in seconds (default 120)")]
        double timeoutSeconds = 120,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GatherResource), async ct =>
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1));

            // Step 1: Find the best resource patch
            var patchJson = await factorio.FindBestResourcePatchAsync(resource, searchRadius, ct);
            using var patchDoc = JsonDocument.Parse(patchJson);
            var patchRoot = patchDoc.RootElement;

            if (!patchRoot.GetProperty("success").GetBoolean())
            {
                return FormatError("gather", resource, "not_found",
                    $"No {resource} found within {searchRadius} tiles");
            }

            var patch = patchRoot.GetProperty("best_patch");
            var patchX = patch.GetProperty("center_x").GetDouble();
            var patchY = patch.GetProperty("center_y").GetDouble();

            // Step 2: Walk to the patch center
            var walkTimeout = Math.Max(RemainingSeconds(deadline), 1);
            var walkJson = await pathfinding.WalkToAsync(patchX, patchY, 3.0, walkTimeout, ct);
            var walk = ParseWalkResult(walkJson);

            if (walk.Status is "stuck" or "timeout" or "no_path")
            {
                return FormatGatherResult(resource, count, 0, patchX, patchY, walk, "walk_failed");
            }

            // Step 3: Find nearest resource entity from current position
            var nearJson = await factorio.FindNearestEntityAsync(resource, 10, ct);
            using var nearDoc = JsonDocument.Parse(nearJson);
            var nearRoot = nearDoc.RootElement;

            double entityX, entityY;
            if (nearRoot.GetProperty("success").GetBoolean())
            {
                entityX = nearRoot.GetProperty("x").GetDouble();
                entityY = nearRoot.GetProperty("y").GetDouble();
            }
            else
            {
                // Fall back to patch center if no entity found nearby
                entityX = patchX;
                entityY = patchY;
            }

            // Step 4: Mine the resource
            var mineTimeout = Math.Max(RemainingSeconds(deadline), 1);
            var mineResult = await mining.MineResourceAsync(
                entityX, entityY, count,
                TimeSpan.FromSeconds(0.5),
                TimeSpan.FromSeconds(mineTimeout),
                ct);

            using var mineDoc = JsonDocument.Parse(mineResult);
            var mineRoot = mineDoc.RootElement;

            var mined = 0;
            var mineStatus = "error";
            if (mineRoot.TryGetProperty("success", out var mineSuccess) && mineSuccess.GetBoolean())
            {
                mined = mineRoot.GetProperty("mined").GetInt32();
                mineStatus = mineRoot.GetProperty("status").GetString() ?? "unknown";
            }
            else
            {
                var error = mineRoot.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "unknown"
                    : "unknown";
                mineStatus = error;
            }

            var overallStatus = mined >= count ? "complete"
                : mined > 0 ? "partial"
                : "mine_failed";

            return FormatGatherResult(resource, count, mined, entityX, entityY, walk, overallStatus, mineStatus);
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Walk to an entity and insert fuel into it — all in one call. " +
        "Checks player inventory for the fuel item, walks to the entity if needed, " +
        "and inserts the specified amount of fuel. " +
        "Use this to keep burner drills, furnaces, and boilers fueled.")]
    public Task<string> RefuelEntity(
        [Description("X coordinate of the entity to refuel")]
        double x,
        [Description("Y coordinate of the entity to refuel")]
        double y,
        [Description("Fuel item name (e.g. 'coal', 'wood', 'solid-fuel')")]
        string fuelItem = "coal",
        [Description("Number of fuel items to insert (default 5)")]
        int count = 5,
        [Description("Maximum time for walking in seconds (default 30)")]
        double walkTimeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(RefuelEntity), async ct =>
        {
            // Step 1: Check if we need to walk to the entity
            var posJson = await pathfinding.GetPlayerPositionAsync(ct);
            var (px, py) = ParsePosition(posJson);
            var dist = Distance(px, py, x, y);

            WalkResult? walk = null;
            if (dist > 2.0) // Beyond typical reach distance threshold
            {
                var walkJson = await pathfinding.WalkToAsync(x, y, 2.0, walkTimeoutSeconds, ct);
                walk = ParseWalkResult(walkJson);
                if (walk.Value.Status is "stuck" or "timeout" or "no_path")
                {
                    return FormatRefuelResult(x, y, fuelItem, count, 0, walk.Value, "walk_failed");
                }
            }

            // Step 2: Insert fuel
            var insertResult = await factorio.InsertItemsAsync(x, y, fuelItem, count, "fuel", ct);
            using var insertDoc = JsonDocument.Parse(insertResult);
            var insertRoot = insertDoc.RootElement;

            if (!insertRoot.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                var error = insertRoot.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "unknown"
                    : "unknown";
                return FormatRefuelResult(x, y, fuelItem, count, 0,
                    walk ?? new WalkResult("not_needed", px, py, dist), error);
            }

            var inserted = insertRoot.GetProperty("inserted").GetInt32();
            var entityName = insertRoot.GetProperty("entity").GetString() ?? "unknown";

            return FormatRefuelResult(x, y, fuelItem, count, inserted,
                walk ?? new WalkResult("not_needed", px, py, dist), "complete", entityName);
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Find a furnace, walk to it, insert ore and fuel, wait for smelting, and collect output — all in one call. " +
        "Searches building memory first, then world entities for the nearest furnace. " +
        "Inserts ore into furnace_source and fuel into the fuel slot, waits for smelting to finish, " +
        "then collects the output item from furnace_result. " +
        "Use this to smelt ores into plates without multiple manual tool calls.")]
    public Task<string> Smelt(
        [Description("Ore item name to smelt (e.g. 'iron-ore', 'copper-ore', 'stone')")]
        string ore,
        [Description("Expected output item name (e.g. 'iron-plate', 'copper-plate', 'stone-brick')")]
        string outputItem,
        [Description("Number of ore to insert and smelt (default 10)")]
        int count = 10,
        [Description("Fuel item name (default 'coal')")]
        string fuel = "coal",
        [Description("Number of fuel items to insert (default 5). Ensure enough fuel for the smelting duration.")]
        int fuelCount = 5,
        [Description("Optional: X coordinate of a specific furnace to use. If omitted, searches for the nearest furnace.")]
        double? furnaceX = null,
        [Description("Optional: Y coordinate of a specific furnace to use. If omitted, searches for the nearest furnace.")]
        double? furnaceY = null,
        [Description("Maximum time for the entire operation in seconds (default 180)")]
        double timeoutSeconds = 180,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(Smelt), async ct =>
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1));
            double targetX, targetY;
            string furnaceName;

            // Step 1: Find a furnace
            if (furnaceX.HasValue && furnaceY.HasValue)
            {
                targetX = furnaceX.Value;
                targetY = furnaceY.Value;
                furnaceName = "furnace";
            }
            else
            {
                var (found, fx, fy, name) = await FindFurnaceAsync(ct);
                if (!found)
                {
                    return FormatError("smelt", ore, "no_furnace",
                        "No furnace found in building memory or nearby");
                }
                targetX = fx;
                targetY = fy;
                furnaceName = name;
            }

            // Step 2: Walk to the furnace
            var walkTimeout = Math.Max(RemainingSeconds(deadline), 1);
            var walkJson = await pathfinding.WalkToAsync(targetX, targetY, 2.0, walkTimeout, ct);
            var walk = ParseWalkResult(walkJson);

            if (walk.Status is "stuck" or "timeout" or "no_path")
            {
                return FormatSmeltResult(ore, count, outputItem, furnaceName,
                    targetX, targetY, walk, 0, 0, 0, "walk_failed");
            }

            // Step 3: Insert ore into furnace_source
            var oreResult = await factorio.InsertItemsAsync(targetX, targetY, ore, count, "furnace_source", ct);
            var oreInserted = ParseInsertedCount(oreResult);

            if (oreInserted == 0)
            {
                var error = ParseErrorString(oreResult) ?? "insert_failed";
                return FormatSmeltResult(ore, count, outputItem, furnaceName,
                    targetX, targetY, walk, 0, 0, 0, error);
            }

            // Step 4: Insert fuel
            var fuelResult = await factorio.InsertItemsAsync(targetX, targetY, fuel, fuelCount, "fuel", ct);
            var fuelInserted = ParseInsertedCount(fuelResult);

            // Step 5: Wait for smelting to complete (poll until source is empty)
            var waitTimeout = Math.Max(RemainingSeconds(deadline), 1);
            var outputCollected = await WaitForSmeltingAndCollectAsync(
                targetX, targetY, ore, outputItem,
                TimeSpan.FromSeconds(1.0),
                TimeSpan.FromSeconds(waitTimeout),
                ct);

            var overallStatus = outputCollected > 0 ? "complete" : "timeout";

            return FormatSmeltResult(ore, count, outputItem, furnaceName,
                targetX, targetY, walk, oreInserted, fuelInserted, outputCollected, overallStatus);
        }, cancellationToken);
    }

    // ── Furnace Finding ─────────────────────────────────────────────

    private static readonly string[] FurnaceNames = ["stone-furnace", "steel-furnace", "electric-furnace"];

    private async Task<(bool found, double x, double y, string name)> FindFurnaceAsync(CancellationToken ct)
    {
        // Try building memory first — search each known furnace type
        var posJson = await pathfinding.GetPlayerPositionAsync(ct);
        var (px, py) = ParsePosition(posJson);

        TrackedBuilding? closest = null;
        double closestDist = double.MaxValue;

        foreach (var furnaceName in FurnaceNames)
        {
            var building = await buildingMemory.FindClosestBuildingAsync(furnaceName, px, py, ct);
            if (building is not null)
            {
                var dist = Distance(px, py, building.X, building.Y);
                if (dist < closestDist)
                {
                    closest = building;
                    closestDist = dist;
                }
            }
        }

        if (closest is not null)
            return (true, closest.X, closest.Y, closest.EntityName);

        // Fall back to world entity search
        var searchJson = await factorio.FindNearestEntityAsync("furnace", 100, ct);
        using var doc = JsonDocument.Parse(searchJson);
        var root = doc.RootElement;

        if (root.GetProperty("success").GetBoolean())
        {
            var x = root.GetProperty("x").GetDouble();
            var y = root.GetProperty("y").GetDouble();
            var name = root.GetProperty("entity").GetString() ?? "furnace";
            return (true, x, y, name);
        }

        return (false, 0, 0, "");
    }

    // ── Smelting Poll & Collect ─────────────────────────────────────

    private async Task<int> WaitForSmeltingAndCollectAsync(
        double x, double y,
        string ore, string outputItem,
        TimeSpan pollInterval, TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        // Poll until furnace source is empty (all ore consumed)
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(pollInterval, ct);

            var statusJson = await QueryFurnaceStateAsync(x, y, ore, outputItem, ct);
            if (statusJson is null) break; // Entity disappeared

            using var doc = JsonDocument.Parse(statusJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out _)) break;

            var sourceCount = root.GetProperty("source_count").GetInt32();
            var status = root.GetProperty("status").GetString();

            // All ore consumed — wait one more cycle for the last item to finish
            if (sourceCount == 0 && status != "working")
            {
                await Task.Delay(pollInterval, ct);
                break;
            }
        }

        // Collect all output
        return await CollectOutputAsync(x, y, outputItem, ct);
    }

    private async Task<string?> QueryFurnaceStateAsync(
        double x, double y, string ore, string outputItem, CancellationToken ct)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local entities = game.connected_players[1].surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            table.sort(entities, function(a, b)
                local a_res = a.type == "resource" and 1 or 0
                local b_res = b.type == "resource" and 1 or 0
                return a_res < b_res
            end)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"error":"no_entity"}')
                return
            end
            local src = e.get_inventory(defines.inventory.furnace_source)
            local dst = e.get_inventory(defines.inventory.furnace_result)
            local src_count = src and src.get_item_count("{{ore}}") or 0
            local dst_count = dst and dst.get_item_count("{{outputItem}}") or 0
            local status_names = {}
            for k, v in pairs(defines.entity_status) do status_names[v] = k end
            local st = status_names[e.status] or "unknown"
            rcon.print('{"source_count":'..src_count..',"result_count":'..dst_count..',"status":"'..st..'"}')
            """);

        var result = await factorio.ExecuteRawLuaAsync(lua, ct);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private async Task<int> CollectOutputAsync(double x, double y, string outputItem, CancellationToken ct)
    {
        // Try to collect a large amount — RemoveItems will cap to what's available
        var removeResult = await factorio.RemoveItemsAsync(x, y, outputItem, 1000, "furnace_result", ct);
        using var doc = JsonDocument.Parse(removeResult);
        var root = doc.RootElement;

        if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
        {
            return root.GetProperty("removed").GetInt32();
        }

        return 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private readonly record struct WalkResult(string Status, double X, double Y, double RemainingDistance);

    private static WalkResult ParseWalkResult(string walkResultJson)
    {
        using var doc = JsonDocument.Parse(walkResultJson);
        var root = doc.RootElement;
        var status = root.GetProperty("status").GetString() ?? "unknown";
        var x = root.GetProperty("x").GetDouble();
        var y = root.GetProperty("y").GetDouble();
        var dist = root.TryGetProperty("distance", out var distProp)
            ? distProp.GetDouble()
            : 0;
        return new WalkResult(status, x, y, dist);
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

    private static double RemainingSeconds(DateTime deadline) =>
        Math.Max((deadline - DateTime.UtcNow).TotalSeconds, 0);

    private static int ParseInsertedCount(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var s) && s.GetBoolean()
                && root.TryGetProperty("inserted", out var i))
                return i.GetInt32();
        }
        catch (JsonException) { }
        return 0;
    }

    private static string? ParseErrorString(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e))
                return e.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    // ── Formatting ──────────────────────────────────────────────────

    private static string FormatError(string operation, string target, string error, string message)
    {
        return $$"""{"success":false,"operation":"{{operation}}","target":"{{target}}","error":"{{error}}","message":"{{message}}"}""";
    }

    private static string FormatGatherResult(
        string resource, int requested, int mined,
        double targetX, double targetY, WalkResult walk,
        string status, string? mineStatus = null)
    {
        var mine = mineStatus is not null
            ? $",\"mine_status\":\"{mineStatus}\""
            : "";

        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"success":{{(mined > 0 ? "true" : "false")}},"operation":"gather","resource":"{{resource}}","requested":{{requested}},"mined":{{mined}},"status":"{{status}}","walk_status":"{{walk.Status}}","target_x":{{targetX}},"target_y":{{targetY}},"player_x":{{walk.X}},"player_y":{{walk.Y}}{{mine}}}""");
    }

    private static string FormatRefuelResult(
        double x, double y, string fuel, int requested, int inserted,
        WalkResult walk, string status, string? entityName = null)
    {
        var entity = entityName is not null
            ? $",\"entity\":\"{entityName}\""
            : "";

        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"success":{{(inserted > 0 ? "true" : "false")}},"operation":"refuel","fuel":"{{fuel}}","requested":{{requested}},"inserted":{{inserted}},"status":"{{status}}","walk_status":"{{walk.Status}}","x":{{x}},"y":{{y}},"player_x":{{walk.X}},"player_y":{{walk.Y}}{{entity}}}""");
    }

    private static string FormatSmeltResult(
        string ore, int count, string outputItem, string furnace,
        double furnaceX, double furnaceY, WalkResult walk,
        int oreInserted, int fuelInserted, int outputCollected,
        string status)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"success":{{(outputCollected > 0 ? "true" : "false")}},"operation":"smelt","ore":"{{ore}}","output_item":"{{outputItem}}","furnace":"{{furnace}}","status":"{{status}}","ore_inserted":{{oreInserted}},"fuel_inserted":{{fuelInserted}},"output_collected":{{outputCollected}},"requested":{{count}},"walk_status":"{{walk.Status}}","furnace_x":{{furnaceX}},"furnace_y":{{furnaceY}},"player_x":{{walk.X}},"player_y":{{walk.Y}}}""");
    }
}
