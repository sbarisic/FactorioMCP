using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── GetInventory ─────────────────────────────────────────────────

    [Fact]
    public async Task GetInventoryAsync_QueriesMainInventory()
    {
        await _service.GetInventoryAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("get_main_inventory", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetInventoryAsync_OutputsJsonItemsArray()
    {
        await _service.GetInventoryAsync();

        Assert.Contains("\"items\":[", _rcon.LastCommand!);
        Assert.Contains("\"name\":\"", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
    }

    // ── Craft ────────────────────────────────────────────────────────

    [Fact]
    public async Task CraftAsync_SendsCorrectRecipeAndCount()
    {
        await _service.CraftAsync("iron-gear-wheel", 5);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("begin_crafting", _rcon.LastCommand);
        Assert.Contains("count=5", _rcon.LastCommand);
        Assert.Contains("recipe=\"iron-gear-wheel\"", _rcon.LastCommand);
    }

    [Fact]
    public async Task CraftAsync_OutputsJsonWithRecipeAndCounts()
    {
        await _service.CraftAsync("electronic-circuit", 10);

        Assert.Contains("\"status\":\"crafting\"", _rcon.LastCommand!);
        Assert.Contains("\"recipe\":\"electronic-circuit\"", _rcon.LastCommand!);
        Assert.Contains("\"requested\":10", _rcon.LastCommand!);
        Assert.Contains("\"queued\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CraftAsync_ReturnsNoMaterialsWhenQueuedIsZero()
    {
        await _service.CraftAsync("iron-gear-wheel", 5);

        Assert.Contains("\"status\":\"no_materials\"", _rcon.LastCommand!);
        Assert.Contains("\"queued\":0", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CraftAsync_ReturnsErrorOnInvalidRecipe()
    {
        await _service.CraftAsync("not-a-real-recipe", 1);

        Assert.Contains("pcall", _rcon.LastCommand!);
        Assert.Contains("\"status\":\"error\"", _rcon.LastCommand!);
        Assert.Contains("\"error\":\"unknown_recipe\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CraftAsync_ThrowsOnNullRecipe()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CraftAsync(null!, 1));
    }

    [Fact]
    public async Task CraftAsync_ThrowsOnZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CraftAsync("iron-plate", 0));
    }

    [Fact]
    public async Task CraftAsync_ThrowsOnNegativeCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CraftAsync("iron-plate", -1));
    }

    // ── GetCraftingQueue ─────────────────────────────────────────────

    [Fact]
    public async Task GetCraftingQueueAsync_QueriesCraftingQueue()
    {
        await _service.GetCraftingQueueAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("crafting_queue", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetCraftingQueueAsync_OutputsJsonQueueArray()
    {
        await _service.GetCraftingQueueAsync();

        Assert.Contains("\"queue\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetCraftingQueueAsync_HandlesEmptyQueue()
    {
        await _service.GetCraftingQueueAsync();

        // When queue is nil, should still output valid JSON
        Assert.Contains("\"queue\":[]", _rcon.LastCommand!);
    }
}
