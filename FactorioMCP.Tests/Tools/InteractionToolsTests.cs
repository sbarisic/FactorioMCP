using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class InteractionToolsTests
{
    // ── PickupItems ─────────────────────────────────────────────────

    [Fact]
    public async Task PickupItems_DelegatesToService()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new InteractionTools(factorio, queue);

        await tools.PickupItems(radius: 15);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
        Assert.Contains("item-entity", rcon.LastCommand);
        Assert.Contains("15", rcon.LastCommand);
    }

    [Fact]
    public async Task PickupItems_DefaultRadius_Uses10()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new InteractionTools(factorio, queue);

        await tools.PickupItems();

        Assert.NotNull(rcon.LastCommand);
        Assert.Contains("10", rcon.LastCommand);
    }

    [Fact]
    public async Task PickupItems_ReturnsPickedUpItems()
    {
        var json = """{"success":true,"picked_up":5,"ground_items_found":2,"items":[{"name":"iron-plate","count":3},{"name":"copper-plate","count":2}]}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new InteractionTools(factorio, queue);

        var result = await tools.PickupItems();

        Assert.Contains("\"picked_up\":5", result);
        Assert.Contains("\"iron-plate\"", result);
        Assert.Contains("\"copper-plate\"", result);
    }

    [Fact]
    public async Task PickupItems_ZeroRadius_Throws()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new InteractionTools(factorio, queue);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tools.PickupItems(radius: 0));
    }
}
