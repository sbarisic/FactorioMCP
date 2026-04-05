using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class BatchToolsTests
{
    private static BuildingMemoryService CreateBuildingMemory() =>
        new(Path.Combine(Path.GetTempPath(), $"buildings-batch-{Guid.NewGuid():N}.json"));

    private static BatchTools CreateTools(CapturingRconClient? rcon = null)
    {
        rcon ??= new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        return new BatchTools(factorio, pathfinding, buildingMemory, queue);
    }

    // ── MineEntityMultiple ──────────────────────────────────────────

    [Fact]
    public async Task MineEntityMultiple_ParsesTargets_DelegatesToService()
    {
        var rcon = new ScriptedRconClient([
            """{"success":true,"entity":"stone-furnace"}""",
            """{"success":true,"entity":"iron-chest"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = new BatchTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MineEntityMultiple("""[{"x":1,"y":2},{"x":3,"y":4}]""");

        Assert.Contains("\"succeeded\":2", result);
        Assert.Contains("\"status\":\"complete\"", result);
        Assert.Contains("\"success\":true", result);
    }

    [Fact]
    public async Task MineEntityMultiple_FailsFastOnError()
    {
        var rcon = new ScriptedRconClient([
            """{"success":true,"entity":"stone-furnace"}""",
            """{"success":false,"error":"no_entity"}""",
            """{"success":true,"entity":"should-not-reach"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = new BatchTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.MineEntityMultiple("""[{"x":1,"y":2},{"x":3,"y":4},{"x":5,"y":6}]""");

        Assert.Contains("\"succeeded\":1", result);
        Assert.Contains("\"failed\":1", result);
        Assert.Contains("\"status\":\"failed\"", result);
        // Should have only 2 results (stopped before 3rd)
        Assert.Equal(2, rcon.AllCommands.Count);
    }

    [Fact]
    public async Task MineEntityMultiple_EmptyArray_Throws()
    {
        var tools = CreateTools();

        await Assert.ThrowsAsync<ArgumentException>(() => tools.MineEntityMultiple("[]"));
    }

    // ── InspectEntityMultiple ───────────────────────────────────────

    [Fact]
    public async Task InspectEntityMultiple_AllSucceed_ReturnsComplete()
    {
        var rcon = new ScriptedRconClient([
            """{"success":true,"entity":"stone-furnace","type":"furnace"}""",
            """{"success":true,"entity":"iron-chest","type":"container"}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = new BatchTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.InspectEntityMultiple("""[{"x":1,"y":2},{"x":3,"y":4}]""");

        Assert.Contains("\"succeeded\":2", result);
        Assert.Contains("\"status\":\"complete\"", result);
    }

    // ── InsertItemsMultiple ─────────────────────────────────────────

    [Fact]
    public async Task InsertItemsMultiple_ParsesItemFields()
    {
        var rcon = new ScriptedRconClient([
            """{"success":true,"entity":"stone-furnace","item":"coal","inserted":5,"requested":5}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = new BatchTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.InsertItemsMultiple("""[{"x":1,"y":2,"item":"coal","count":5}]""");

        Assert.Contains("\"succeeded\":1", result);
        Assert.Contains("\"status\":\"complete\"", result);
    }

    [Fact]
    public async Task InsertItemsMultiple_UsesCustomInventoryType()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = new BatchTools(factorio, pathfinding, buildingMemory, queue);

        // This will call InsertItemsAsync which will generate a Lua command
        // We just verify it doesn't throw during parsing
        try
        {
            await tools.InsertItemsMultiple("""[{"x":1,"y":2,"item":"iron-ore","count":10,"inventoryType":"furnace_source"}]""");
        }
        catch { /* Capturing client returns empty - expected */ }

        Assert.Contains("furnace_source", rcon.LastCommand!);
    }

    // ── RefuelEntityMultiple ────────────────────────────────────────

    [Fact]
    public async Task RefuelEntityMultiple_ParsesTargetsWithDefaults()
    {
        // position response + insert response for one target
        var rcon = new ScriptedRconClient([
            """{"x":1,"y":2}""",
            """{"success":true,"entity":"stone-furnace","item":"coal","inserted":5,"requested":5}"""
        ]);
        var factorio = new FactorioService(rcon);
        var pathfinding = new PathfindingService(rcon);
        var buildingMemory = CreateBuildingMemory();
        var queue = new GameCommandQueue();
        var tools = new BatchTools(factorio, pathfinding, buildingMemory, queue);

        var result = await tools.RefuelEntityMultiple("""[{"x":1,"y":2}]""");

        Assert.Contains("\"succeeded\":1", result);
        Assert.Contains("\"status\":\"complete\"", result);
    }

    [Fact]
    public async Task RefuelEntityMultiple_EmptyArray_Throws()
    {
        var tools = CreateTools();

        await Assert.ThrowsAsync<ArgumentException>(() => tools.RefuelEntityMultiple("[]"));
    }
}
