using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class NavigationToolsTests
{
    private static NavigationTools CreateTools(FactorioService factorio, PathfindingService pathfinding, BuildingMemoryService buildingMemory, GameCommandQueue queue)
    {
        return new NavigationTools(factorio, pathfinding, buildingMemory, queue);
    }

    // ── MoveToEntity — Target found and arrived ─────────────────────

    [Fact]
    public async Task MoveToEntity_FindsAndArrivesAtEntity()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindNearestEntity → found entity at (20, 0)
            """{"success":true,"entity":"stone-furnace","type":"furnace","x":20,"y":0,"distance":20.0,"total_found":3}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":5,"total_waypoints":5,"x":19.5,"y":0}""",
            // 5. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToEntity("stone-furnace", tolerance: 2.0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"target_type\":\"entity\"", result);
        Assert.Contains("\"target\":\"stone-furnace\"", result);
        Assert.Contains("\"walk_status\":\"arrived\"", result);
    }

    [Fact]
    public async Task MoveToEntity_AlreadyAtTarget_ReturnsArrivedImmediately()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindNearestEntity → found entity at (1, 0)
            """{"success":true,"entity":"iron-chest","type":"container","x":1,"y":0,"distance":1.0,"total_found":1}""",
            // 2. PathfindingService.GetPlayerPosition → already within tolerance
            """{"x":0.5,"y":0}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToEntity("iron-chest", tolerance: 2.0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"walk_status\":\"arrived\"", result);
    }

    // ── MoveToEntity — Target not found ─────────────────────────────

    [Fact]
    public async Task MoveToEntity_NotFound_ReturnsNotFoundError()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindNearestEntity → not found
            """{"success":false,"error":"not_found","filter":"nuclear-reactor","radius":100}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToEntity("nuclear-reactor");

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"error\":\"not_found\"", result);
        Assert.Contains("\"target_type\":\"entity\"", result);
        Assert.Contains("\"search\":\"nuclear-reactor\"", result);
    }

    // ── MoveToEntity — Stuck detection ──────────────────────────────

    [Fact]
    public async Task MoveToEntity_GetsStuck_ReturnsStuck()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindNearestEntity
            """{"success":true,"entity":"stone-furnace","type":"furnace","x":50,"y":0,"distance":50.0,"total_found":1}""",
            // 2. GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — stuck)
            """{"status":"stuck","waypoint":2,"total_waypoints":10,"x":1,"y":0}""",
            // 5. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToEntity("stone-furnace", tolerance: 2.0);

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"walk_status\":\"stuck\"", result);
    }

    // ── MoveToResource — Target found and arrived ───────────────────

    [Fact]
    public async Task MoveToResource_FindsAndArrivesAtPatch()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindBestResourcePatch → found patch centered at (30, 10)
            """{"success":true,"resource":"iron-ore","best_patch":{"center_x":30.0,"center_y":10.0,"count":50,"total_amount":10000,"distance":31.6},"total_entities":50,"total_patches":2,"alternatives":[]}""",
            // 2. GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":8,"total_waypoints":8,"x":28,"y":9}""",
            // 5. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToResource("iron-ore", tolerance: 5.0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"target_type\":\"resource\"", result);
        Assert.Contains("\"target\":\"iron-ore\"", result);
        Assert.Contains("\"walk_status\":\"arrived\"", result);
    }

    // ── MoveToResource — Target not found ───────────────────────────

    [Fact]
    public async Task MoveToResource_NotFound_ReturnsNotFoundError()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindBestResourcePatch → not found
            """{"success":false,"error":"not_found","resource":"uranium-ore","radius":200}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToResource("uranium-ore");

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"error\":\"not_found\"", result);
        Assert.Contains("\"target_type\":\"resource\"", result);
    }

    // ── MoveToBuilding — Found by label ─────────────────────────────

    [Fact]
    public async Task MoveToBuilding_FindsByLabel_AndArrives()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition (for building search — via FactorioService)
            """{"x":0,"y":0}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":3,"total_waypoints":3,"x":14,"y":0}""",
            // 5. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        await buildingMemory.TrackBuildingAsync("stone-furnace", 15, 0);
        await buildingMemory.UpdateBuildingLabelAsync(15, 0, "main smelter");
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToBuilding("main smelter", tolerance: 2.0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"target_type\":\"building\"", result);
        Assert.Contains("stone-furnace", result);
        Assert.Contains("main smelter", result);
        Assert.Contains("\"walk_status\":\"arrived\"", result);
    }

    // ── MoveToBuilding — Found by entity name ───────────────────────

    [Fact]
    public async Task MoveToBuilding_FindsByEntityName_AndArrives()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition (for building search)
            """{"x":0,"y":0}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":2,"total_waypoints":2,"x":0,"y":9}""",
            // 5. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        await buildingMemory.TrackBuildingAsync("wooden-chest", 0, 10);
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToBuilding("wooden-chest", tolerance: 2.0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"target_type\":\"building\"", result);
        Assert.Contains("wooden-chest", result);
        Assert.Contains("\"walk_status\":\"arrived\"", result);
    }

    // ── MoveToBuilding — Not found ──────────────────────────────────

    [Fact]
    public async Task MoveToBuilding_NotFound_ReturnsNotFoundError()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition (for building search)
            """{"x":0,"y":0}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToBuilding("nonexistent");

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"error\":\"not_found\"", result);
        Assert.Contains("\"target_type\":\"building\"", result);
    }

    // ── MoveToBuilding — Label takes priority over entity name ──────

    [Fact]
    public async Task MoveToBuilding_LabelMatchTakesPriorityOverEntityName()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition (for building search)
            """{"x":0,"y":0}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":1,"total_waypoints":1,"x":4,"y":0}""",
            // 5. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        // Track two buildings — one further away with matching label, one closer without label
        await buildingMemory.TrackBuildingAsync("stone-furnace", 5, 0);
        await buildingMemory.UpdateBuildingLabelAsync(5, 0, "iron smelter");
        await buildingMemory.TrackBuildingAsync("stone-furnace", 100, 0); // farther, no label
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToBuilding("iron smelter", tolerance: 2.0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("iron smelter", result);
        Assert.Contains("\"target_x\":5", result);
    }

    // ── MoveToBuilding — Case-insensitive label search ──────────────

    [Fact]
    public async Task MoveToBuilding_LabelSearchIsCaseInsensitive()
    {
        var rcon = new ScriptedRconClient([
            // 1. GetPlayerPosition (for building search)
            """{"x":0,"y":0}""",
            // 2. PathfindingService.GetPlayerPosition (walk init — already near)
            """{"x":0,"y":0}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        await buildingMemory.TrackBuildingAsync("stone-furnace", 1, 0);
        await buildingMemory.UpdateBuildingLabelAsync(1, 0, "Main Smelter");
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToBuilding("main smelter", tolerance: 2.0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("Main Smelter", result);
    }

    // ── Response format validation ──────────────────────────────────

    [Fact]
    public async Task MoveToEntity_ResponseIncludesAllFields()
    {
        var rcon = new ScriptedRconClient([
            """{"success":true,"entity":"stone-furnace","type":"furnace","x":5,"y":3,"distance":5.8,"total_found":1}""",
            """{"x":4.5,"y":2.8}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MoveToEntity("stone-furnace", tolerance: 2.0);

        Assert.Contains("\"success\":", result);
        Assert.Contains("\"target_type\":", result);
        Assert.Contains("\"target\":", result);
        Assert.Contains("\"target_x\":", result);
        Assert.Contains("\"target_y\":", result);
        Assert.Contains("\"walk_status\":", result);
        Assert.Contains("\"player_x\":", result);
        Assert.Contains("\"player_y\":", result);
        Assert.Contains("\"remaining_distance\":", result);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static BuildingMemoryService CreateBuildingMemory() =>
        new(Path.Combine(Path.GetTempPath(), $"buildings-nav-{Guid.NewGuid():N}.json"));
}
