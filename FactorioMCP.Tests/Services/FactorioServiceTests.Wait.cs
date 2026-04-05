using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task WaitForCraftingAsync_CompletesWhenQueueEmpty()
    {
        // ScriptedRconClient returns a response with an empty queue.
        // WaitForCraftingAsync calls GetCraftingQueueAsync which calls rcon.ExecuteLuaAsync.
        // The service parses the response for "queue":[] to determine completion.
        var scripted = new ScriptedRconClient(["{\"queue\":[]}"]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForCraftingAsync(
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"complete\"", result);
        Assert.Contains("\"queue\":[]", result);
    }

    [Fact]
    public async Task WaitForCraftingAsync_TimesOutWhenQueueNeverEmpties()
    {
        // The queue always has items, so the service should poll until timeout.
        // The last response is repeated (ScriptedRconClient behavior) for the final poll.
        var scripted = new ScriptedRconClient([
            "{\"queue\":[{\"recipe\":\"iron-gear-wheel\",\"count\":5}]}",
            "{\"queue\":[{\"recipe\":\"iron-gear-wheel\",\"count\":3}]}",
            "{\"queue\":[{\"recipe\":\"iron-gear-wheel\",\"count\":1}]}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForCraftingAsync(
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_CompletesWhenStatusMatches()
    {
        // WaitForEntityStatusAsync polls entity status. When the response status
        // matches the target (case-insensitive), it returns "satisfied".
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"stone-furnace\",\"status\":\"working\"}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityStatusAsync(
            5, 10, "working",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"satisfied\"", result);
        Assert.Contains("\"entity\":\"stone-furnace\"", result);
        Assert.Contains("\"entity_status\":\"working\"", result);
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_TimesOutWhenStatusNeverMatches()
    {
        // Entity always returns "no_fuel" but we're waiting for "working".
        // The last response is repeated for the final poll after timeout.
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"stone-furnace\",\"status\":\"no_fuel\"}",
            "{\"entity\":\"stone-furnace\",\"status\":\"no_fuel\"}",
            "{\"entity\":\"stone-furnace\",\"status\":\"no_fuel\"}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityStatusAsync(
            5, 10, "working",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
        Assert.Contains("\"entity_status\":\"no_fuel\"", result);
        Assert.Contains("\"target_status\":\"working\"", result);
    }
}
