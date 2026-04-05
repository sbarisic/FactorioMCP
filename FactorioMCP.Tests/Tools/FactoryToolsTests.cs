using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class FactoryToolsTests
{
    // ── FindUnpoweredEntities ───────────────────────────────────────

    [Fact]
    public async Task FindUnpoweredEntities_DelegatesToService()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new FactoryTools(factorio, queue);

        await tools.FindUnpoweredEntities(radius: 30);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
        Assert.Contains("no_power", rcon.LastCommand);
        Assert.Contains("low_power", rcon.LastCommand);
        Assert.Contains("30", rcon.LastCommand);
    }

    [Fact]
    public async Task FindUnpoweredEntities_ReturnsEntities()
    {
        var json = """{"count":1,"entities":[{"name":"assembling-machine-1","type":"assembling-machine","status":"no_power","x":10.0,"y":-5.0}]}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new FactoryTools(factorio, queue);

        var result = await tools.FindUnpoweredEntities();

        Assert.Contains("\"count\":1", result);
        Assert.Contains("\"no_power\"", result);
    }

    [Fact]
    public async Task FindUnpoweredEntities_ZeroRadius_Throws()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new FactoryTools(factorio, queue);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tools.FindUnpoweredEntities(radius: 0));
    }

    // ── FindIdleMachines ────────────────────────────────────────────

    [Fact]
    public async Task FindIdleMachines_DelegatesToService()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new FactoryTools(factorio, queue);

        await tools.FindIdleMachines(radius: 25);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
        Assert.Contains("25", rcon.LastCommand);
    }

    [Fact]
    public async Task FindIdleMachines_ReturnsIdleMachines()
    {
        var json = """{"count":2,"entities":[{"name":"stone-furnace","type":"furnace","status":"no_fuel","x":1.0,"y":2.0},{"name":"stone-furnace","type":"furnace","status":"no_ingredients","x":3.0,"y":4.0}]}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new FactoryTools(factorio, queue);

        var result = await tools.FindIdleMachines();

        Assert.Contains("\"count\":2", result);
        Assert.Contains("\"no_fuel\"", result);
        Assert.Contains("\"no_ingredients\"", result);
    }

    // ── FindMissingInputs ───────────────────────────────────────────

    [Fact]
    public async Task FindMissingInputs_DelegatesToService()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new FactoryTools(factorio, queue);

        await tools.FindMissingInputs(x: 5, y: 10);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
        Assert.Contains("5", rcon.LastCommand);
        Assert.Contains("10", rcon.LastCommand);
    }

    [Fact]
    public async Task FindMissingInputs_ReturnsMissingItems()
    {
        var json = """{"success":true,"entity":"stone-furnace","type":"furnace","status":"no_fuel","x":5.0,"y":10.0,"missing":[{"slot":"fuel","issue":"empty"}]}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new FactoryTools(factorio, queue);

        var result = await tools.FindMissingInputs(5, 10);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"fuel\"", result);
        Assert.Contains("\"empty\"", result);
    }

    // ── DI Resolution ───────────────────────────────────────────────

    [Fact]
    public void FactoryTools_CanBeConstructed()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();

        var tools = new FactoryTools(factorio, queue);

        Assert.NotNull(tools);
    }
}
