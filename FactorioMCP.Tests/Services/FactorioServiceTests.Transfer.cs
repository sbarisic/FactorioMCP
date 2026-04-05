using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task DropItemsAsync_GeneratesCorrectLua()
    {
        await _service.DropItemsAsync("iron-plate", 10);
        var cmd = _rcon.LastCommand!;

        Assert.Contains("iron-plate", cmd);
        Assert.Contains("10", cmd);
        Assert.Contains("spill_item_stack", cmd);
        Assert.Contains("get_item_count", cmd);
        Assert.Contains("remove_item", cmd);
    }

    [Fact]
    public async Task TransferAllItemsAsync_GeneratesCorrectLua()
    {
        await _service.TransferAllItemsAsync(10, 20);
        var defaultCmd = _rcon.LastCommand!;

        Assert.Contains("find_entities_filtered", defaultCmd);
        Assert.Contains("get_inventory", defaultCmd);
        Assert.Contains("defines.inventory.chest", defaultCmd);
        Assert.Contains("player.insert", defaultCmd);

        // Verify custom inventory type
        await _service.TransferAllItemsAsync(10, 20, "furnace_result");
        Assert.Contains("defines.inventory.furnace_result", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_GeneratesCorrectLua()
    {
        await _service.GetEntityInventoryAsync(10, 20);
        var defaultCmd = _rcon.LastCommand!;

        Assert.Contains("find_entities_filtered", defaultCmd);
        Assert.Contains("get_contents", defaultCmd);
        Assert.Contains("defines.inventory.chest", defaultCmd);
        Assert.Contains("get_inventory", defaultCmd);

        // Verify custom inventory type mapping
        await _service.GetEntityInventoryAsync(10, 20, "fuel");
        Assert.Contains("defines.inventory.fuel", _rcon.LastCommand!);
    }
}
