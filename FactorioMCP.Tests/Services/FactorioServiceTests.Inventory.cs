using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task InventoryCommands_GenerateCorrectLua()
    {
        // GetInventoryAsync
        await _service.GetInventoryAsync();
        var invCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", invCmd);
        Assert.Contains("get_main_inventory", invCmd);
        Assert.Contains("\"items\":[", invCmd);
        Assert.Contains("\"name\":\"", invCmd);
        Assert.Contains("\"count\":", invCmd);

        // CraftAsync
        await _service.CraftAsync("iron-gear-wheel", 5);
        var craftCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", craftCmd);
        Assert.Contains("begin_crafting", craftCmd);
        Assert.Contains("count=5", craftCmd);
        Assert.Contains("recipe=\"iron-gear-wheel\"", craftCmd);
        Assert.Contains("pcall", craftCmd);

        // GetCraftingQueueAsync
        await _service.GetCraftingQueueAsync();
        var queueCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", queueCmd);
        Assert.Contains("crafting_queue", queueCmd);
        Assert.Contains("\"queue\":", queueCmd);
    }

    [Fact]
    public async Task CraftAsync_Validation()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CraftAsync(null!, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CraftAsync("", 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CraftAsync("iron-plate", 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CraftAsync("iron-plate", -1));
    }
}
