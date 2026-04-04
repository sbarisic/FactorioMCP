using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class TaskToolsTests
{
    private static TaskTools CreateTools(FactorioService factorio, PathfindingService pathfinding,
        MiningService mining, BuildingMemoryService buildingMemory, GameCommandQueue queue)
    {
        return new TaskTools(factorio, pathfinding, mining, buildingMemory, queue);
    }

    private static BuildingMemoryService CreateBuildingMemory() =>
        new(Path.Combine(Path.GetTempPath(), $"buildings-task-{Guid.NewGuid():N}.json"));

    // ══════════════════════════════════════════════════════════════════
    // GatherResource
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GatherResource_FindsPatchWalksAndMines()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindBestResourcePatch → patch at (30, 10)
            """{"success":true,"resource":"iron-ore","best_patch":{"center_x":30.0,"center_y":10.0,"count":50,"total_amount":10000,"distance":31.6},"total_entities":50,"total_patches":2,"alternatives":[]}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":5,"total_waypoints":5,"x":29,"y":9}""",
            // 5. CleanupAsync
            """ok""",
            // 6. FindNearestEntity → resource entity nearby
            """{"success":true,"entity":"iron-ore","type":"resource","x":29.5,"y":9.5,"distance":0.7,"total_found":10}""",
            // 7. MiningService.StartMiningResource
            """{"success":true,"entity":"iron-ore","amount":50,"mining_time_per_unit":0.50,"x":29.5,"y":9.5,"status":"mining_started"}""",
            // 8. MiningService.GetMiningStatus — mined enough
            """{"is_mining":true,"depleted":false,"remaining":40,"mined":10,"entity":"iron-ore"}""",
            // 9. MiningService.StopMining
            """{"success":true,"status":"mining_stopped"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.GatherResource("iron-ore", count: 10);

        Assert.Contains("\"operation\":\"gather\"", result);
        Assert.Contains("\"resource\":\"iron-ore\"", result);
        Assert.Contains("\"walk_status\":\"arrived\"", result);
    }

    [Fact]
    public async Task GatherResource_ResourceNotFound_ReturnsError()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindBestResourcePatch → not found
            """{"success":false,"error":"not_found","resource":"uranium-ore","radius":200}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.GatherResource("uranium-ore");

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"error\":\"not_found\"", result);
        Assert.Contains("\"operation\":\"gather\"", result);
    }

    [Fact]
    public async Task GatherResource_WalkGetStuck_ReturnsWalkFailed()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindBestResourcePatch → patch at (50, 0)
            """{"success":true,"resource":"coal","best_patch":{"center_x":50.0,"center_y":0.0,"count":100,"total_amount":50000,"distance":50.0},"total_entities":100,"total_patches":1,"alternatives":[]}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
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
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.GatherResource("coal", count: 5);

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"status\":\"walk_failed\"", result);
        Assert.Contains("\"walk_status\":\"stuck\"", result);
        Assert.Contains("\"mined\":0", result);
    }

    [Fact]
    public async Task GatherResource_NearbyEntityNotFound_FallsBackToPatchCenter()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindBestResourcePatch → patch at (10, 5)
            """{"success":true,"resource":"stone","best_patch":{"center_x":10.0,"center_y":5.0,"count":20,"total_amount":5000,"distance":11.2},"total_entities":20,"total_patches":1,"alternatives":[]}""",
            // 2. PathfindingService.GetPlayerPosition (walk init — close enough)
            """{"x":9,"y":4.5}""",
            // 3. FindNearestEntity → not found
            """{"success":false,"error":"not_found","filter":"stone","radius":10}""",
            // 4. MiningService.StartMiningResource at patch center (10,5)
            """{"success":true,"entity":"stone","amount":20,"mining_time_per_unit":0.50,"x":10.0,"y":5.0,"status":"mining_started"}""",
            // 5. MiningService.GetMiningStatus — mined enough
            """{"is_mining":true,"depleted":false,"remaining":10,"mined":10,"entity":"stone"}""",
            // 6. MiningService.StopMining
            """{"success":true,"status":"mining_stopped"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.GatherResource("stone", count: 10);

        Assert.Contains("\"operation\":\"gather\"", result);
        Assert.Contains("\"resource\":\"stone\"", result);
        // Should use patch center coordinates since nearby entity wasn't found
        Assert.Contains("\"target_x\":10", result);
        Assert.Contains("\"target_y\":5", result);
    }

    [Fact]
    public async Task GatherResource_ResponseIncludesAllFields()
    {
        var rcon = new ScriptedRconClient([
            // 1. FindBestResourcePatch
            """{"success":true,"resource":"copper-ore","best_patch":{"center_x":15.0,"center_y":0.0,"count":30,"total_amount":7000,"distance":15.0},"total_entities":30,"total_patches":1,"alternatives":[]}""",
            // 2. PathfindingService.GetPlayerPosition (walk init — already near)
            """{"x":14,"y":0}""",
            // 3. FindNearestEntity
            """{"success":true,"entity":"copper-ore","type":"resource","x":14.5,"y":0.5,"distance":0.7,"total_found":5}""",
            // 4. MiningService.StartMiningResource
            """{"success":true,"entity":"copper-ore","amount":30,"mining_time_per_unit":0.50,"x":14.5,"y":0.5,"status":"mining_started"}""",
            // 5. MiningService.GetMiningStatus — mined enough
            """{"is_mining":true,"depleted":false,"remaining":20,"mined":10,"entity":"copper-ore"}""",
            // 6. MiningService.StopMining
            """{"success":true,"status":"mining_stopped"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.GatherResource("copper-ore", count: 10);

        Assert.Contains("\"operation\":\"gather\"", result);
        Assert.Contains("\"resource\":", result);
        Assert.Contains("\"requested\":", result);
        Assert.Contains("\"mined\":", result);
        Assert.Contains("\"status\":", result);
        Assert.Contains("\"walk_status\":", result);
        Assert.Contains("\"target_x\":", result);
        Assert.Contains("\"target_y\":", result);
        Assert.Contains("\"player_x\":", result);
        Assert.Contains("\"player_y\":", result);
    }

    // ══════════════════════════════════════════════════════════════════
    // RefuelEntity
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RefuelEntity_WalksAndInsertsFuel()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition → far from entity
            """{"x":0,"y":0}""",
            // 2. PathfindingService.GetPlayerPosition (walk init inside WalkToAsync)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":4,"total_waypoints":4,"x":19,"y":0}""",
            // 5. CleanupAsync
            """ok""",
            // 6. InsertItems (fuel into entity)
            """{"success":true,"entity":"stone-furnace","item":"coal","inserted":5,"requested":5}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.RefuelEntity(20, 0, fuelItem: "coal", count: 5);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"operation\":\"refuel\"", result);
        Assert.Contains("\"fuel\":\"coal\"", result);
        Assert.Contains("\"inserted\":5", result);
        Assert.Contains("\"status\":\"complete\"", result);
        Assert.Contains("\"entity\":\"stone-furnace\"", result);
    }

    [Fact]
    public async Task RefuelEntity_AlreadyNear_SkipsWalk()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition → already within reach
            """{"x":9.5,"y":0}""",
            // 2. InsertItems (fuel)
            """{"success":true,"entity":"burner-mining-drill","item":"coal","inserted":10,"requested":10}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.RefuelEntity(10, 0, fuelItem: "coal", count: 10);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"walk_status\":\"not_needed\"", result);
        Assert.Contains("\"inserted\":10", result);
        Assert.Contains("\"entity\":\"burner-mining-drill\"", result);
    }

    [Fact]
    public async Task RefuelEntity_WalkStuck_ReturnsWalkFailed()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition → far away
            """{"x":0,"y":0}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — stuck)
            """{"status":"stuck","waypoint":1,"total_waypoints":10,"x":0.5,"y":0}""",
            // 5. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.RefuelEntity(50, 0);

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"status\":\"walk_failed\"", result);
        Assert.Contains("\"walk_status\":\"stuck\"", result);
    }

    [Fact]
    public async Task RefuelEntity_InsertFails_ReturnsError()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition → already near
            """{"x":4.5,"y":0}""",
            // 2. InsertItems → fails
            """{"success":false,"error":"no_entity_at_position"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.RefuelEntity(5, 0, fuelItem: "coal", count: 5);

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"inserted\":0", result);
        Assert.Contains("no_entity_at_position", result);
    }

    [Fact]
    public async Task RefuelEntity_ResponseIncludesAllFields()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition → near entity
            """{"x":1,"y":1}""",
            // 2. InsertItems
            """{"success":true,"entity":"boiler","item":"wood","inserted":3,"requested":5}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.RefuelEntity(2, 2, fuelItem: "wood", count: 5);

        Assert.Contains("\"success\":", result);
        Assert.Contains("\"operation\":\"refuel\"", result);
        Assert.Contains("\"fuel\":", result);
        Assert.Contains("\"requested\":", result);
        Assert.Contains("\"inserted\":", result);
        Assert.Contains("\"status\":", result);
        Assert.Contains("\"walk_status\":", result);
        Assert.Contains("\"x\":", result);
        Assert.Contains("\"y\":", result);
        Assert.Contains("\"player_x\":", result);
        Assert.Contains("\"player_y\":", result);
    }

    // ══════════════════════════════════════════════════════════════════
    // Smelt
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Smelt_WithExplicitFurnace_WalksInsertsAndCollects()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 2. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 3. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":3,"total_waypoints":3,"x":14,"y":0}""",
            // 4. CleanupAsync
            """ok""",
            // 5. InsertItems ore into furnace_source
            """{"success":true,"entity":"stone-furnace","item":"iron-ore","inserted":10,"requested":10}""",
            // 6. InsertItems fuel
            """{"success":true,"entity":"stone-furnace","item":"coal","inserted":5,"requested":5}""",
            // 7. QueryFurnaceState (poll 1 — still smelting)
            """{"source_count":5,"result_count":3,"status":"working"}""",
            // 8. QueryFurnaceState (poll 2 — done, source empty and not working)
            """{"source_count":0,"result_count":10,"status":"idle"}""",
            // 9. RemoveItems (collect output)
            """{"success":true,"entity":"stone-furnace","item":"iron-plate","removed":10,"requested":1000}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.Smelt("iron-ore", "iron-plate", count: 10,
            furnaceX: 15, furnaceY: 0);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"operation\":\"smelt\"", result);
        Assert.Contains("\"ore\":\"iron-ore\"", result);
        Assert.Contains("\"output_item\":\"iron-plate\"", result);
        Assert.Contains("\"status\":\"complete\"", result);
        Assert.Contains("\"ore_inserted\":10", result);
        Assert.Contains("\"output_collected\":10", result);
    }

    [Fact]
    public async Task Smelt_FindsFurnaceFromBuildingMemory()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (FindFurnace → building memory search)
            """{"x":0,"y":0}""",
            // 2. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 3. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 4. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":5,"total_waypoints":5,"x":24,"y":0}""",
            // 5. CleanupAsync
            """ok""",
            // 6. InsertItems ore
            """{"success":true,"entity":"stone-furnace","item":"copper-ore","inserted":5,"requested":5}""",
            // 7. InsertItems fuel
            """{"success":true,"entity":"stone-furnace","item":"coal","inserted":3,"requested":3}""",
            // 8. QueryFurnaceState — done immediately (source empty, not working)
            """{"source_count":0,"result_count":5,"status":"idle"}""",
            // 9. RemoveItems (collect)
            """{"success":true,"entity":"stone-furnace","item":"copper-plate","removed":5,"requested":1000}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        await buildingMemory.TrackBuildingAsync("stone-furnace", 25, 0);
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.Smelt("copper-ore", "copper-plate", count: 5);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"furnace\":\"stone-furnace\"", result);
        Assert.Contains("\"output_collected\":5", result);
    }

    [Fact]
    public async Task Smelt_FindsFurnaceFromWorldSearch()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (building memory search — no match)
            """{"x":0,"y":0}""",
            // 2. FindNearestEntity("furnace") → found world entity
            """{"success":true,"entity":"steel-furnace","type":"furnace","x":10,"y":5,"distance":11.2,"total_found":1}""",
            // 3. PathfindingService.GetPlayerPosition (walk init)
            """{"x":0,"y":0}""",
            // 4. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 5. GetNavigationStatusAsync (poll — arrived)
            """{"status":"arrived","waypoint":3,"total_waypoints":3,"x":9,"y":4.5}""",
            // 6. CleanupAsync
            """ok""",
            // 7. InsertItems ore
            """{"success":true,"entity":"steel-furnace","item":"iron-ore","inserted":20,"requested":20}""",
            // 8. InsertItems fuel
            """{"success":true,"entity":"steel-furnace","item":"coal","inserted":10,"requested":10}""",
            // 9. QueryFurnaceState — done (source empty, not working)
            """{"source_count":0,"result_count":20,"status":"idle"}""",
            // 10. RemoveItems (collect)
            """{"success":true,"entity":"steel-furnace","item":"iron-plate","removed":20,"requested":1000}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.Smelt("iron-ore", "iron-plate", count: 20);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"furnace\":\"steel-furnace\"", result);
        Assert.Contains("\"output_collected\":20", result);
    }

    [Fact]
    public async Task Smelt_NoFurnaceFound_ReturnsError()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (building memory search)
            """{"x":0,"y":0}""",
            // 2. FindNearestEntity("furnace") → not found
            """{"success":false,"error":"not_found","filter":"furnace","radius":100}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.Smelt("iron-ore", "iron-plate");

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"error\":\"no_furnace\"", result);
        Assert.Contains("\"operation\":\"smelt\"", result);
    }

    [Fact]
    public async Task Smelt_WalkStuck_ReturnsWalkFailed()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (walk init for explicit furnace)
            """{"x":0,"y":0}""",
            // 2. RequestPathAsync
            """{"success":true,"request_id":1,"x":0,"y":0}""",
            // 3. GetNavigationStatusAsync (poll — stuck)
            """{"status":"stuck","waypoint":1,"total_waypoints":10,"x":0.5,"y":0}""",
            // 4. CleanupAsync
            """ok"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.Smelt("iron-ore", "iron-plate",
            furnaceX: 50, furnaceY: 0);

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"status\":\"walk_failed\"", result);
        Assert.Contains("\"walk_status\":\"stuck\"", result);
    }

    [Fact]
    public async Task Smelt_OreInsertFails_ReturnsError()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (walk init for explicit furnace — already near)
            """{"x":4,"y":0}""",
            // 2. InsertItems ore → fails
            """{"success":false,"error":"no_entity_at_position"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.Smelt("iron-ore", "iron-plate",
            furnaceX: 5, furnaceY: 0);

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"ore_inserted\":0", result);
    }

    [Fact]
    public async Task Smelt_CollectsZeroOutput_ReturnsTimeout()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (walk init for explicit furnace — already near)
            """{"x":4,"y":0}""",
            // 2. InsertItems ore
            """{"success":true,"entity":"stone-furnace","item":"iron-ore","inserted":5,"requested":5}""",
            // 3. InsertItems fuel
            """{"success":true,"entity":"stone-furnace","item":"coal","inserted":3,"requested":3}""",
            // 4. QueryFurnaceState — still working (will repeat until timeout)
            """{"source_count":5,"result_count":0,"status":"working"}""",
            // Eventually RemoveItems returns 0
            """{"success":false,"error":"no_items"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        // Use very short timeout so test doesn't take long
        var result = await tools.Smelt("iron-ore", "iron-plate",
            furnaceX: 5, furnaceY: 0, timeoutSeconds: 0.1);

        Assert.Contains("\"operation\":\"smelt\"", result);
        Assert.Contains("\"output_collected\":0", result);
        Assert.Contains("\"status\":\"timeout\"", result);
    }

    [Fact]
    public async Task Smelt_ResponseIncludesAllFields()
    {
        var rcon = new ScriptedRconClient([
            // 1. PathfindingService.GetPlayerPosition (walk init — already near)
            """{"x":4,"y":0}""",
            // 2. InsertItems ore
            """{"success":true,"entity":"stone-furnace","item":"iron-ore","inserted":10,"requested":10}""",
            // 3. InsertItems fuel
            """{"success":true,"entity":"stone-furnace","item":"coal","inserted":5,"requested":5}""",
            // 4. QueryFurnaceState — done (source empty, not working)
            """{"source_count":0,"result_count":10,"status":"idle"}""",
            // 5. RemoveItems (collect)
            """{"success":true,"entity":"stone-furnace","item":"iron-plate","removed":10,"requested":1000}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon) { PollInterval = TimeSpan.FromMilliseconds(10) };
        var mining = new MiningService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = CreateTools(factorio, pathfinding, mining, buildingMemory, queue);

        var result = await tools.Smelt("iron-ore", "iron-plate",
            furnaceX: 5, furnaceY: 0);

        Assert.Contains("\"success\":", result);
        Assert.Contains("\"operation\":\"smelt\"", result);
        Assert.Contains("\"ore\":", result);
        Assert.Contains("\"output_item\":", result);
        Assert.Contains("\"furnace\":", result);
        Assert.Contains("\"status\":", result);
        Assert.Contains("\"ore_inserted\":", result);
        Assert.Contains("\"fuel_inserted\":", result);
        Assert.Contains("\"output_collected\":", result);
        Assert.Contains("\"requested\":", result);
        Assert.Contains("\"walk_status\":", result);
        Assert.Contains("\"furnace_x\":", result);
        Assert.Contains("\"furnace_y\":", result);
        Assert.Contains("\"player_x\":", result);
        Assert.Contains("\"player_y\":", result);
    }
}
