using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── FindBuildableArea Ore Patch Avoidance ────────────────────────

    [Fact]
    public async Task FindBuildableAreaAsync_Default_ExcludesOrePatches()
    {
        await _service.FindBuildableAreaAsync(5, 5);

        Assert.NotNull(_rcon.LastCommand);
        // Default: only character excluded from blocking (ore patches count as blocking)
        Assert.Contains("\"character\"", _rcon.LastCommand);
        Assert.Contains("invert=true", _rcon.LastCommand);
        // Should NOT have "resource" in the filter (ore patches block)
        Assert.DoesNotContain("\"resource\"", _rcon.LastCommand);
    }

    [Fact]
    public async Task FindBuildableAreaAsync_AllowOrePatches_ExcludesResourceFromBlocking()
    {
        await _service.FindBuildableAreaAsync(5, 5, allowOrePatches: true);

        Assert.NotNull(_rcon.LastCommand);
        // When allowOrePatches=true, both resource and character excluded from blocking
        Assert.Contains("\"resource\"", _rcon.LastCommand);
        Assert.Contains("\"character\"", _rcon.LastCommand);
        Assert.Contains("invert=true", _rcon.LastCommand);
    }

    // ── PlaceEntitySmart ────────────────────────────────────────────

    [Fact]
    public async Task PlaceEntitySmartAsync_GeneratesValidLua()
    {
        await _service.PlaceEntitySmartAsync("stone-furnace", 10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("can_place_entity", _rcon.LastCommand);
        Assert.Contains("stone-furnace", _rcon.LastCommand);
        Assert.Contains("10", _rcon.LastCommand);
        Assert.Contains("20", _rcon.LastCommand);
    }

    [Fact]
    public async Task PlaceEntitySmartAsync_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.PlaceEntitySmartAsync("", 0, 0));
    }

    [Fact]
    public async Task PlaceEntitySmartAsync_ZeroRadius_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.PlaceEntitySmartAsync("stone-furnace", 0, 0, searchRadius: 0));
    }

    // ── PickupItems ─────────────────────────────────────────────────

    [Fact]
    public async Task PickupItemsAsync_GeneratesValidLua()
    {
        await _service.PickupItemsAsync(15);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("item-entity", _rcon.LastCommand);
        Assert.Contains("15", _rcon.LastCommand);
    }

    [Fact]
    public async Task PickupItemsAsync_ZeroRadius_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.PickupItemsAsync(0));
    }

    // ── Factory Analysis ────────────────────────────────────────────

    [Fact]
    public async Task FindUnpoweredEntitiesAsync_GeneratesValidLua()
    {
        await _service.FindUnpoweredEntitiesAsync(30);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("no_power", _rcon.LastCommand);
        Assert.Contains("30", _rcon.LastCommand);
    }

    [Fact]
    public async Task FindIdleMachinesAsync_GeneratesValidLua()
    {
        await _service.FindIdleMachinesAsync(25);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("25", _rcon.LastCommand);
    }

    [Fact]
    public async Task FindMissingInputsAsync_GeneratesValidLua()
    {
        await _service.FindMissingInputsAsync(5, 10);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("5", _rcon.LastCommand);
        Assert.Contains("10", _rcon.LastCommand);
        Assert.Contains("furnace_source", _rcon.LastCommand);
    }

    // ── PlanCraft ───────────────────────────────────────────────────

    [Fact]
    public async Task PlanCraftAsync_GeneratesValidLua()
    {
        await _service.PlanCraftAsync("iron-gear-wheel", 10);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("iron-gear-wheel", _rcon.LastCommand);
        Assert.Contains("recipe_for", _rcon.LastCommand);
    }

    [Fact]
    public async Task PlanCraftAsync_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.PlanCraftAsync(""));
    }

    [Fact]
    public async Task PlanCraftAsync_ZeroCount_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.PlanCraftAsync("iron-plate", 0));
    }
}
