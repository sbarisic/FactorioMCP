using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task ScanResourcesAsync_GeneratesCorrectLua()
    {
        await _service.ScanResourcesAsync();
        var defaultCmd = _rcon.LastCommand!;

        Assert.Contains("find_entities_filtered", defaultCmd);
        Assert.Contains("type=\"resource\"", defaultCmd);
        Assert.Contains("radius=50", defaultCmd);
        Assert.Contains("player.position", defaultCmd);

        await _service.ScanResourcesAsync(100, centerX: 200.0, centerY: 300.0);
        var customCmd = _rcon.LastCommand!;

        Assert.Contains("radius=100", customCmd);
        Assert.Contains("200", customCmd);
        Assert.Contains("300", customCmd);
        Assert.DoesNotContain("player.position", customCmd);
    }

    [Fact]
    public async Task ScanTilesAsync_GeneratesCorrectLua()
    {
        await _service.ScanTilesAsync();
        var defaultCmd = _rcon.LastCommand!;

        Assert.Contains("find_tiles_filtered", defaultCmd);
        Assert.Contains("\"scan_radius\":16", defaultCmd);
        Assert.Contains("player.position", defaultCmd);

        await _service.ScanTilesAsync(32, centerX: -10.5, centerY: 42.0);
        var customCmd = _rcon.LastCommand!;

        Assert.Contains("\"scan_radius\":32", customCmd);
        Assert.Contains("-10.5", customCmd);
        Assert.DoesNotContain("player.position", customCmd);
    }

    [Fact]
    public async Task GetNearbyEntitiesAsync_GeneratesCorrectLua()
    {
        await _service.GetNearbyEntitiesAsync();
        var defaultCmd = _rcon.LastCommand!;

        Assert.Contains("find_entities_filtered", defaultCmd);
        Assert.Contains("player.position", defaultCmd);

        await _service.GetNearbyEntitiesAsync(20, centerX: 100.5, centerY: -50.0);
        var customCmd = _rcon.LastCommand!;

        Assert.Contains("100.5", customCmd);
        Assert.Contains("-50", customCmd);
        Assert.DoesNotContain("player.position", customCmd);

        // Partial center (only X) should fall back to player position
        await _service.GetNearbyEntitiesAsync(10, centerX: 50.0);
        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertAndRemoveItemsAsync_GenerateCorrectLua()
    {
        // Insert: verify item name, count, and inventory type mapping
        await _service.InsertItemsAsync(5.0, 3.0, "coal", 10, "fuel");
        var insertCmd = _rcon.LastCommand!;

        Assert.Contains("coal", insertCmd);
        Assert.Contains("10", insertCmd);
        Assert.Contains("inv.insert", insertCmd);
        Assert.Contains("defines.inventory.fuel", insertCmd);
        Assert.Contains("defines.inventory.chest", insertCmd);

        // Remove: verify item name, count, and transfer to player
        await _service.RemoveItemsAsync(5.0, 3.0, "iron-plate", 20, "furnace_result");
        var removeCmd = _rcon.LastCommand!;

        Assert.Contains("iron-plate", removeCmd);
        Assert.Contains("20", removeCmd);
        Assert.Contains("inv.remove", removeCmd);
        Assert.Contains("player.insert", removeCmd);
        Assert.Contains("defines.inventory.furnace_result", removeCmd);
    }
}
