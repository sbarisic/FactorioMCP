using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── GetEntityPrototypeAsync ───────────────────────────────────────

    [Fact]
    public async Task GetEntityPrototypeAsync_SendsSilentCommand()
    {
        await _service.GetEntityPrototypeAsync("stone-furnace");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_QueriesPrototype()
    {
        await _service.GetEntityPrototypeAsync("stone-furnace");

        Assert.Contains("prototypes.entity", _rcon.LastCommand!);
        Assert.Contains("stone-furnace", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_ReturnsTileDimensions()
    {
        await _service.GetEntityPrototypeAsync("stone-furnace");

        Assert.Contains("tile_width", _rcon.LastCommand!);
        Assert.Contains("tile_height", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_ReturnsMaxHealth()
    {
        await _service.GetEntityPrototypeAsync("stone-furnace");

        Assert.Contains("max_health", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_ReturnsType()
    {
        await _service.GetEntityPrototypeAsync("stone-furnace");

        Assert.Contains("\"type\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_IncludesOptionalCraftingSpeed()
    {
        await _service.GetEntityPrototypeAsync("assembling-machine-1");

        Assert.Contains("crafting_speed", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_IncludesOptionalMiningSpeed()
    {
        await _service.GetEntityPrototypeAsync("electric-mining-drill");

        Assert.Contains("mining_speed", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_IncludesCollisionBox()
    {
        await _service.GetEntityPrototypeAsync("stone-furnace");

        Assert.Contains("collision_box", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_HandlesUnknownEntity()
    {
        await _service.GetEntityPrototypeAsync("nonexistent-entity");

        Assert.Contains("unknown_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_EscapesEntityName()
    {
        await _service.GetEntityPrototypeAsync("stone-furnace");

        Assert.Contains("esc", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_ThrowsOnNullName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetEntityPrototypeAsync(null!));
    }

    [Fact]
    public async Task GetEntityPrototypeAsync_ThrowsOnEmptyName()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetEntityPrototypeAsync(""));
    }

    // ── GetAreaOccupancyAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetAreaOccupancyAsync_SendsSilentCommand()
    {
        await _service.GetAreaOccupancyAsync(0, 0, 5, 5);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_UsesAreaSearch()
    {
        await _service.GetAreaOccupancyAsync(0, 0, 5, 5);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("area", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_ReturnsTileGrid()
    {
        await _service.GetAreaOccupancyAsync(0, 0, 3, 3);

        Assert.Contains("\"tiles\"", _rcon.LastCommand!);
        Assert.Contains("\"blocked\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_ReportsAreaDimensions()
    {
        await _service.GetAreaOccupancyAsync(0, 0, 5, 5);

        Assert.Contains("\"width\"", _rcon.LastCommand!);
        Assert.Contains("\"height\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_ChecksAreaLimit()
    {
        await _service.GetAreaOccupancyAsync(0, 0, 5, 5);

        Assert.Contains("10000", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_SwapsCoordinatesIfReversed()
    {
        await _service.GetAreaOccupancyAsync(5, 5, 0, 0);

        // Should still work — coordinates are swapped in Lua
        Assert.Contains("if x1 > x2", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_ChecksWaterTiles()
    {
        await _service.GetAreaOccupancyAsync(0, 0, 5, 5);

        Assert.Contains("water", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_ExcludesResources()
    {
        await _service.GetAreaOccupancyAsync(0, 0, 5, 5);

        Assert.Contains("\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAreaOccupancyAsync_UsesFormattedCoordinates()
    {
        await _service.GetAreaOccupancyAsync(1.5, 2.5, 6.5, 7.5);

        Assert.NotNull(_rcon.LastCommand);
        Assert.Contains("1.5", _rcon.LastCommand!);
    }
}
