using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task UtilityCommands_GenerateCorrectLua()
    {
        // CountItemInWorldAsync
        await _service.CountItemInWorldAsync("iron-plate");
        var countCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", countCmd);
        Assert.Contains("iron-plate", countCmd);
        Assert.Contains("find_entities_filtered", countCmd);
        Assert.Contains("get_item_count", countCmd);
        Assert.Contains("defines.inventory.chest", countCmd);
        Assert.Contains("defines.inventory.furnace_source", countCmd);
        Assert.Contains("\"total\":", countCmd);
    }

    [Fact]
    public async Task InventoryUtilityCommands_GenerateCorrectLua()
    {
        // GetInventorySummaryAsync
        await _service.GetInventorySummaryAsync();
        var summaryCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", summaryCmd);
        Assert.Contains("get_main_inventory", summaryCmd);
        Assert.Contains("\"items\":{", summaryCmd);
        Assert.Contains("\"total_slots\":", summaryCmd);
        Assert.Contains("\"free_slots\":", summaryCmd);

        // CheckEnsureItemAsync
        await _service.CheckEnsureItemAsync("iron-plate", 5);
        var ensureCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", ensureCmd);
        Assert.Contains("get_item_count", ensureCmd);
        Assert.Contains("\"iron-plate\"", ensureCmd);
        Assert.Contains("\"satisfied\":true", ensureCmd);
        Assert.Contains("local need = 5", ensureCmd);
    }
}
