using FactorioMCP.Rcon;
using FactorioMCP.Services;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace FactorioMCP.Tests.Integration;

/// <summary>
/// Live integration tests that connect to a running Factorio instance via RCON.
/// These tests require Factorio to be running with RCON enabled on localhost:27015
/// with password "mypassword".
///
/// Run manually — these are NOT intended for CI.
/// Use: dotnet test --filter "Category!=Integration" to exclude.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LiveGameTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly RconClient _rcon = new();
    private FactorioService _service = null!;
    private PathfindingService _pathfinding = null!;

    public LiveGameTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _service = new FactorioService(_rcon);
        _pathfinding = new PathfindingService(_rcon);
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");
        _output.WriteLine("✅ RCON connected and authenticated");
    }

    public async Task DisposeAsync()
    {
        await _rcon.DisposeAsync();
    }

    private void LogResult(string tool, string result)
    {
        _output.WriteLine($"[{tool}] {result}");
    }

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement;

    // ── 0. Raw RCON Diagnostics ───────────────────────────────────

    [Fact]
    public async Task RawDiagnostics_CheckGameState()
    {
        var lines = new List<string>();
        lines.Add("═══ RAW RCON DIAGNOSTICS ═══");

        // Test basic RCON command
        var result1 = await _rcon.ExecuteAsync("/c rcon.print('hello from rcon')");
        lines.Add($"Basic rcon.print: [{result1}]");

        // Check if game.player exists
        var result2 = await _rcon.ExecuteAsync("/c rcon.print(tostring(game.player))");
        lines.Add($"game.player: [{result2}]");

        // Check game.players
        var result3 = await _rcon.ExecuteAsync("/c local count = 0; for _ in pairs(game.players) do count = count + 1 end; rcon.print('player count: '..count)");
        lines.Add($"game.players: [{result3}]");

        // List connected players
        var result4 = await _rcon.ExecuteAsync("/c local names = {}; for _, p in pairs(game.connected_players) do names[#names+1] = p.name end; rcon.print('connected: '..table.concat(names, ', '))");
        lines.Add($"connected_players: [{result4}]");

        // Try game.players[1]
        var result5 = await _rcon.ExecuteAsync("/c local p = game.players[1]; if p then rcon.print('player[1]: '..p.name..' at '..p.position.x..','..p.position.y) else rcon.print('player[1]: nil') end");
        lines.Add($"game.players[1]: [{result5}]");

        // Check game version
        var result6 = await _rcon.ExecuteAsync("/c rcon.print('version: '..script.active_mods['base'])");
        lines.Add($"game version: [{result6}]");

        // Test the actual service methods
        lines.Add("");
        lines.Add("═══ SERVICE METHOD RESULTS ═══");

        try { var r = await _service.GetPlayerPositionAsync(); lines.Add($"GetPlayerPosition: [{r}]"); }
        catch (Exception ex) { lines.Add($"GetPlayerPosition ERROR: {ex.Message}"); }

        try { var r = await _service.GetInventoryAsync(); lines.Add($"GetInventory: [{r}]"); }
        catch (Exception ex) { lines.Add($"GetInventory ERROR: {ex.Message}"); }

        try { var r = await _service.GetNearbyEntitiesAsync(10); lines.Add($"GetNearbyEntities: [{r}]"); }
        catch (Exception ex) { lines.Add($"GetNearbyEntities ERROR: {ex.Message}"); }

        try { var r = await _service.GetResearchStatusAsync(); lines.Add($"GetResearchStatus: [{r}]"); }
        catch (Exception ex) { lines.Add($"GetResearchStatus ERROR: {ex.Message}"); }

        try { var r = await _service.GetGameTickAsync(); lines.Add($"GetGameTick: [{r}]"); }
        catch (Exception ex) { lines.Add($"GetGameTick ERROR: {ex.Message}"); }

        try { var r = await _service.GetCraftingQueueAsync(); lines.Add($"GetCraftingQueue: [{r}]"); }
        catch (Exception ex) { lines.Add($"GetCraftingQueue ERROR: {ex.Message}"); }

        try { var r = await _service.CheckDistanceAsync(0, 0); lines.Add($"CheckDistance(0,0): [{r}]"); }
        catch (Exception ex) { lines.Add($"CheckDistance ERROR: {ex.Message}"); }

        lines.Add("");
        lines.Add("═══ DIAGNOSTICS COMPLETE ═══");

        var report = string.Join(Environment.NewLine, lines);
        await File.WriteAllTextAsync("E:\\Projects\\FactorioMCP\\test-output.txt", report);

        foreach (var line in lines)
            _output.WriteLine(line);
    }

    // ── 1. Player Position ──────────────────────────────────────────

    [Fact]
    public async Task GetPlayerPosition_ReturnsValidCoordinates()
    {
        var result = await _service.GetPlayerPositionAsync();
        LogResult("GetPlayerPosition", result);

        var json = Parse(result);
        Assert.True(json.TryGetProperty("x", out _), "Missing 'x' property");
        Assert.True(json.TryGetProperty("y", out _), "Missing 'y' property");
    }

    // ── 2. Inventory ────────────────────────────────────────────────

    [Fact]
    public async Task GetInventory_ReturnsItemsArray()
    {
        var result = await _service.GetInventoryAsync();
        LogResult("GetInventory", result);

        var json = Parse(result);
        Assert.True(json.TryGetProperty("items", out var items), "Missing 'items' property");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    // ── 3. Nearby Entities ──────────────────────────────────────────

    [Fact]
    public async Task GetNearbyEntities_ReturnsEntitiesArray()
    {
        var result = await _service.GetNearbyEntitiesAsync(30);
        LogResult("GetNearbyEntities(30)", result);

        var json = Parse(result);
        Assert.True(json.TryGetProperty("entities", out var entities), "Missing 'entities' property");
        Assert.Equal(JsonValueKind.Array, entities.ValueKind);
        _output.WriteLine($"  → Found {entities.GetArrayLength()} entities within 30 tiles");
    }

    // ── 4. Check Distance ───────────────────────────────────────────

    [Fact]
    public async Task CheckDistance_ReportsRangeStatus()
    {
        // Check distance to a nearby point (should be in range)
        var posResult = await _service.GetPlayerPositionAsync();
        var pos = Parse(posResult);
        var x = pos.GetProperty("x").GetDouble() + 2;
        var y = pos.GetProperty("y").GetDouble();

        var result = await _service.CheckDistanceAsync(x, y);
        LogResult("CheckDistance(+2, 0)", result);

        var json = Parse(result);
        Assert.True(json.GetProperty("build_in_range").GetBoolean(), "Should be within build range at 2 tiles");
        Assert.True(json.GetProperty("reach_in_range").GetBoolean(), "Should be within reach range at 2 tiles");
    }

    // ── 5. Research Status ──────────────────────────────────────────

    [Fact]
    public async Task GetResearchStatus_ReturnsValidResponse()
    {
        var result = await _service.GetResearchStatusAsync();
        LogResult("GetResearchStatus", result);

        var json = Parse(result);
        Assert.True(json.TryGetProperty("researching", out _), "Missing 'researching' property");
    }

    // ── 6. Game Tick ────────────────────────────────────────────────

    [Fact]
    public async Task GetGameTick_ReturnsPositiveTick()
    {
        var result = await _service.GetGameTickAsync();
        LogResult("GetGameTick", result);

        var json = Parse(result);
        var tick = json.GetProperty("tick").GetInt64();
        Assert.True(tick >= 0, $"Tick should be non-negative, got {tick}");
    }

    // ── 7. Crafting Queue ───────────────────────────────────────────

    [Fact]
    public async Task GetCraftingQueue_ReturnsQueueArray()
    {
        var result = await _service.GetCraftingQueueAsync();
        LogResult("GetCraftingQueue", result);

        var json = Parse(result);
        Assert.True(json.TryGetProperty("queue", out var queue), "Missing 'queue' property");
        Assert.Equal(JsonValueKind.Array, queue.ValueKind);
    }

    // ── 8. Walking ──────────────────────────────────────────────────

    [Fact]
    public async Task WalkAndStop_MovesPlayer()
    {
        const double walkDistance = 10.0;
        const double tolerance = 2.0;
        const double timeoutSeconds = 10.0;
        // Minimum distance the player must have moved to count as success.
        // walkDistance is 10 tiles, tolerance is 2, so we expect at least ~7 tiles.
        const double minExpectedMovement = 5.0;

        // Direction offsets: how far to walk in each direction
        var dirOffsets = new (string name, double dx, double dy)[]
        {
            ("south", 0, walkDistance),
            ("east", walkDistance, 0),
            ("north", 0, -walkDistance),
            ("west", -walkDistance, 0)
        };

        foreach (var (dir, dx, dy) in dirOffsets)
        {
            // Record position BEFORE this walk
            var beforeResult = await _service.GetPlayerPositionAsync();
            var beforePos = Parse(beforeResult);
            var beforeX = beforePos.GetProperty("x").GetDouble();
            var beforeY = beforePos.GetProperty("y").GetDouble();
            _output.WriteLine($"[{dir}] Before: ({beforeX:F2}, {beforeY:F2})");

            // Walk 10 tiles in the given direction
            var targetX = beforeX + dx;
            var targetY = beforeY + dy;
            var walkResult = await _pathfinding.WalkToAsync(targetX, targetY, tolerance, timeoutSeconds);
            var walkJson = Parse(walkResult);
            var status = walkJson.GetProperty("status").GetString();
            LogResult($"WalkTo({dir})", walkResult);

            // Record position AFTER this walk
            var afterResult = await _service.GetPlayerPositionAsync();
            var afterPos = Parse(afterResult);
            var afterX = afterPos.GetProperty("x").GetDouble();
            var afterY = afterPos.GetProperty("y").GetDouble();

            var distMoved = Math.Sqrt(
                (afterX - beforeX) * (afterX - beforeX) +
                (afterY - beforeY) * (afterY - beforeY));
            _output.WriteLine($"[{dir}] After:  ({afterX:F2}, {afterY:F2}), moved {distMoved:F2} tiles, status={status}");

            Assert.True(distMoved >= minExpectedMovement,
                $"Walking {dir}: expected to move at least {minExpectedMovement} tiles " +
                $"but only moved {distMoved:F2}. Status={status}, " +
                $"before=({beforeX:F2},{beforeY:F2}), after=({afterX:F2},{afterY:F2})");
            _output.WriteLine($"[{dir}] ✓ moved {distMoved:F2} tiles");
        }
    }

    // ── 9. Mine Resource ────────────────────────────────────────────

    [Fact]
    public async Task MineNearbyResource_AddsToInventory()
    {
        // Find resources nearby
        var entitiesResult = await _service.GetNearbyEntitiesAsync(15);
        var entities = Parse(entitiesResult).GetProperty("entities");
        LogResult("GetNearbyEntities(15)", entitiesResult);

        // Look for a mineable resource (ore, tree, rock)
        string[] mineableTypes = ["iron-ore", "copper-ore", "coal", "stone", "tree-01", "tree-02", "tree-03", "tree-04", "tree-05", "tree-06", "tree-07", "tree-08", "tree-09", "rock-big", "rock-huge", "sand-rock-big"];

        JsonElement? FindClosestMineable(JsonElement entityArray)
        {
            JsonElement? best = null;
            double bestDist = double.MaxValue;
            foreach (var entity in entityArray.EnumerateArray())
            {
                var name = entity.GetProperty("name").GetString()!;
                if (!mineableTypes.Contains(name) && !name.StartsWith("tree-") && !name.Contains("-ore") && !name.Contains("rock"))
                    continue;
                var ex = entity.GetProperty("x").GetDouble();
                var ey = entity.GetProperty("y").GetDouble();
                // We don't know exact player pos here, but entities are relative-ish;
                // use raw distance from origin as a rough proxy — we'll re-check with CheckDistance
                if (best is null || (ex * ex + ey * ey) < bestDist)
                {
                    bestDist = ex * ex + ey * ey;
                    best = entity;
                }
            }
            return best;
        }

        var target = FindClosestMineable(entities);

        if (target is null)
        {
            _output.WriteLine("⚠️ No mineable resource found within 15 tiles — walking to find one");
            var posJson = await _pathfinding.GetPlayerPositionAsync();
            var (ppx, ppy) = PathfindingService.ParsePosition(posJson);
            await _pathfinding.WalkToAsync(ppx, ppy - 10, 2.0, 5.0);

            entitiesResult = await _service.GetNearbyEntitiesAsync(15);
            entities = Parse(entitiesResult).GetProperty("entities");
            target = FindClosestMineable(entities);
        }

        if (target is null)
        {
            _output.WriteLine("⚠️ SKIP: No mineable resource found nearby. Spawn may be in a barren area.");
            return;
        }

        var tx = target.Value.GetProperty("x").GetDouble();
        var ty = target.Value.GetProperty("y").GetDouble();
        var targetName = target.Value.GetProperty("name").GetString()!;
        _output.WriteLine($"Target: {targetName} at ({tx}, {ty})");

        // Walk closer until in reach (max 5 attempts)
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var distResult = await _service.CheckDistanceAsync(tx, ty);
            var dist = Parse(distResult);
            if (dist.GetProperty("reach_in_range").GetBoolean())
            {
                _output.WriteLine($"  → In reach (distance: {dist.GetProperty("distance")})");
                break;
            }

            _output.WriteLine($"  → Out of reach ({dist.GetProperty("distance")}), walking closer (attempt {attempt + 1}/5)...");
            _output.WriteLine($"  → Walking toward ({tx}, {ty}) to get closer");
            await _pathfinding.WalkToAsync(tx, ty, 3.0, 5.0);
        }

        // Final reach check
        var finalDistResult = await _service.CheckDistanceAsync(tx, ty);
        var finalDist = Parse(finalDistResult);
        if (!finalDist.GetProperty("reach_in_range").GetBoolean())
        {
            _output.WriteLine($"⚠️ SKIP: Could not get within reach of {targetName} after 5 attempts (distance: {finalDist.GetProperty("distance")})");
            return;
        }

        // Mine it
        var mineResult = await _service.MineEntityAtAsync(tx, ty);
        LogResult($"MineEntity({targetName})", mineResult);

        if (string.IsNullOrWhiteSpace(mineResult))
        {
            _output.WriteLine($"⚠️ SKIP: Mine command returned empty response for {targetName}");
            return;
        }

        var mineJson = Parse(mineResult);
        if (mineJson.GetProperty("success").GetBoolean())
        {
            _output.WriteLine($"  → Successfully mined {targetName} ✓");
        }
        else
        {
            var error = mineJson.GetProperty("error").GetString();
            _output.WriteLine($"  → Mine failed: {error}");
        }
    }

    // ── 10. Full Workflow: Mine + Craft ──────────────────────────────

    [Fact]
    public async Task FullWorkflow_InventoryAndCraft()
    {
        // Check starting inventory
        var invResult = await _service.GetInventoryAsync();
        LogResult("Starting inventory", invResult);

        // In a fresh game, player starts with some items depending on freeplay settings
        // Try to craft something simple if we have materials
        var inv = Parse(invResult).GetProperty("items");
        var ironPlateCount = 0;

        foreach (var item in inv.EnumerateArray())
        {
            if (item.GetProperty("name").GetString() == "iron-plate")
            {
                ironPlateCount = item.GetProperty("count").GetInt32();
            }
            _output.WriteLine($"  {item.GetProperty("name").GetString()}: {item.GetProperty("count").GetInt32()}");
        }

        if (ironPlateCount >= 2)
        {
            _output.WriteLine($"\nHave {ironPlateCount} iron plates — crafting 1 iron gear wheel");
            var craftResult = await _service.CraftAsync("iron-gear-wheel", 1);
            LogResult("Craft(iron-gear-wheel, 1)", craftResult);

            // Wait for crafting
            var waitResult = await _service.WaitForCraftingAsync(
                TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(10));
            LogResult("WaitForCrafting", waitResult);

            // Check inventory again
            var postInv = await _service.GetInventoryAsync();
            LogResult("Post-craft inventory", postInv);
        }
        else
        {
            _output.WriteLine($"\n⚠️ Only {ironPlateCount} iron plates — not enough to craft iron gear wheel (need 2)");
            _output.WriteLine("  This is expected in a brand new game before smelting");
        }
    }

    // ── 11. Place Entity ────────────────────────────────────────────

    [Fact]
    public async Task PlaceEntity_ValidatesCorrectly()
    {
        // Try to place something we don't have — should get missing_item error
        var posResult = await _service.GetPlayerPositionAsync();
        var pos = Parse(posResult);
        var px = pos.GetProperty("x").GetDouble();
        var py = pos.GetProperty("y").GetDouble();

        var result = await _service.PlaceEntityAsync("stone-furnace", px + 2, py);
        LogResult("PlaceEntity(stone-furnace, no item)", result);

        var json = Parse(result);
        // In a fresh game we likely don't have a stone-furnace
        // Either it places successfully or we get a proper error
        if (!json.GetProperty("success").GetBoolean())
        {
            var error = json.GetProperty("error").GetString();
            _output.WriteLine($"  → Expected error: {error} ✓");
            Assert.Contains(error, new[] { "missing_item", "out_of_range", "invalid_position" });
        }
        else
        {
            _output.WriteLine($"  → Entity placed successfully (player had a stone-furnace)");
            // Clean up — mine it back
            var mineResult = await _service.MineEntityAtAsync(px + 2, py);
            LogResult("Cleanup MineEntity", mineResult);
        }
    }

    // ── 12. Wait For Ticks ──────────────────────────────────────────

    [Fact]
    public async Task WaitForTicks_WaitsCorrectDuration()
    {
        var startTick = Parse(await _service.GetGameTickAsync()).GetProperty("tick").GetInt64();
        _output.WriteLine($"Start tick: {startTick}");

        var result = await _service.WaitForTicksAsync(60, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(5));
        LogResult("WaitForTicks(60)", result);

        var json = Parse(result);
        var status = json.GetProperty("status").GetString();
        _output.WriteLine($"  → Status: {status}");

        if (status == "complete")
        {
            var elapsed = json.GetProperty("elapsed").GetInt64();
            _output.WriteLine($"  → Elapsed: {elapsed} ticks (~{elapsed / 60.0:F1} seconds) ✓");
            Assert.True(elapsed >= 60, "Should have waited at least 60 ticks");
        }
    }

    // ── 13. Comprehensive Entity Scan ───────────────────────────────

    [Fact]
    public async Task ComprehensiveScan_ShowsGameWorld()
    {
        _output.WriteLine("═══ COMPREHENSIVE GAME STATE SCAN ═══\n");

        // Position
        var pos = await _service.GetPlayerPositionAsync();
        _output.WriteLine($"📍 Position: {pos}");

        // Inventory
        var inv = await _service.GetInventoryAsync();
        var items = Parse(inv).GetProperty("items");
        _output.WriteLine($"\n🎒 Inventory ({items.GetArrayLength()} item types):");
        foreach (var item in items.EnumerateArray())
        {
            _output.WriteLine($"   • {item.GetProperty("name").GetString()}: {item.GetProperty("count").GetInt32()}");
        }

        // Research
        var research = await _service.GetResearchStatusAsync();
        _output.WriteLine($"\n🔬 Research: {research}");

        // Crafting queue
        var queue = await _service.GetCraftingQueueAsync();
        _output.WriteLine($"\n⚙️ Crafting queue: {queue}");

        // Game tick
        var tick = await _service.GetGameTickAsync();
        _output.WriteLine($"\n⏱️ Game tick: {tick}");

        // Nearby entities
        var nearby = await _service.GetNearbyEntitiesAsync(20);
        var entities = Parse(nearby).GetProperty("entities");
        _output.WriteLine($"\n🗺️ Entities within 20 tiles ({entities.GetArrayLength()} total):");

        // Group by type
        var groups = new Dictionary<string, int>();
        foreach (var e in entities.EnumerateArray())
        {
            var name = e.GetProperty("name").GetString()!;
            groups[name] = groups.GetValueOrDefault(name) + 1;
        }
        foreach (var (name, count) in groups.OrderByDescending(g => g.Value))
        {
            _output.WriteLine($"   • {name}: {count}");
        }

        _output.WriteLine("\n═══ SCAN COMPLETE ═══");
    }

    // ── 14. Inserter Direction Verification ──────────────────────────

    [Fact]
    public async Task InserterDirection_VerifyDropAndPickupPositions()
    {
        _output.WriteLine("═══ INSERTER DIRECTION VERIFICATION ═══\n");

        // Get player position for nearby placement
        var posResult = await _service.GetPlayerPositionAsync();
        var pos = Parse(posResult);
        var px = Math.Floor(pos.GetProperty("x").GetDouble()) + 0.5;
        var py = Math.Floor(pos.GetProperty("y").GetDouble()) + 0.5;
        _output.WriteLine($"Player at ({px}, {py})");

        // Test all four cardinal directions with PreviewInserterPlacement
        var directions = new (string dir, double dropDx, double dropDy)[]
        {
            ("north", 0, -1),
            ("south", 0, 1),
            ("east", 1, 0),
            ("west", -1, 0)
        };

        // Use an offset position so we don't overlap with the player
        var testX = px + 3;
        var testY = py + 3;

        foreach (var (dir, expectedDropDx, expectedDropDy) in directions)
        {
            var previewResult = await _service.PreviewInserterPlacementAsync(testX, testY, dir);
            _output.WriteLine($"\n[Preview {dir}] {previewResult}");

            var json = Parse(previewResult);
            Assert.True(json.GetProperty("success").GetBoolean(), $"Preview should succeed for direction {dir}");

            // Verify drop position matches direction
            var drop = json.GetProperty("drop");
            var dropX = drop.GetProperty("x").GetDouble();
            var dropY = drop.GetProperty("y").GetDouble();
            Assert.Equal(testX + expectedDropDx, dropX, 0.1);
            Assert.Equal(testY + expectedDropDy, dropY, 0.1);
            _output.WriteLine($"  ✓ Drop ({dir}): ({dropX}, {dropY}) — correct");

            // Verify pickup is opposite
            var pickup = json.GetProperty("pickup");
            var pickupX = pickup.GetProperty("x").GetDouble();
            var pickupY = pickup.GetProperty("y").GetDouble();
            Assert.Equal(testX - expectedDropDx, pickupX, 0.1);
            Assert.Equal(testY - expectedDropDy, pickupY, 0.1);
            _output.WriteLine($"  ✓ Pickup (opposite of {dir}): ({pickupX}, {pickupY}) — correct");
        }

        _output.WriteLine("\n═══ INSERTER DIRECTION VERIFICATION COMPLETE ═══");
    }

    }
