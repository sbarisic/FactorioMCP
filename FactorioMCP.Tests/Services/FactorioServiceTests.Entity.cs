using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task PlaceEntityAsync_GeneratesCorrectLua()
    {
        await _service.PlaceEntityAsync("stone-furnace", 1.5, -3.75, "east");

        var cmd = _rcon.LastCommand!;
        Assert.Contains("\"stone-furnace\"", cmd);
        Assert.Contains("1.5", cmd);
        Assert.Contains("-3.75", cmd);
        Assert.Contains("defines.direction.east", cmd);
        Assert.Contains("force=player.force", cmd);
        Assert.Contains("create_entity", cmd);
        Assert.Contains("can_place_entity", cmd);
        Assert.Contains("get_item_count", cmd);
    }

    [Fact]
    public async Task MineEntityAtAsync_GeneratesCorrectLua()
    {
        await _service.MineEntityAtAsync(2.5, -4.25);

        var cmd = _rcon.LastCommand!;
        Assert.Contains("find_entities_filtered", cmd);
        Assert.Contains("2.5", cmd);
        Assert.Contains("-4.25", cmd);
        Assert.Contains("mine_entity", cmd);
        Assert.Contains("reach_distance", cmd);
        Assert.Contains("e.type == \"resource\"", cmd);
    }

    [Fact]
    public async Task InspectEntityAsync_GeneratesCorrectLua()
    {
        await _service.InspectEntityAsync(3.0, -1.0);

        var cmd = _rcon.LastCommand!;
        Assert.Contains("3", cmd);
        Assert.Contains("-1", cmd);
        Assert.Contains("find_entities_filtered", cmd);
        Assert.Contains("health", cmd);
        Assert.Contains("get_recipe", cmd);
        Assert.Contains("remaining_burning_fuel", cmd);
        Assert.Contains("status", cmd);
    }

    [Fact]
    public async Task FindNearestEntityAsync_And_FindBestResourcePatchAsync_GenerateCorrectLua()
    {
        await _service.FindNearestEntityAsync("stone-furnace", 50);
        var findCmd = _rcon.LastCommand!;
        Assert.Contains("\"stone-furnace\"", findCmd);
        Assert.Contains("radius=50", findCmd);
        Assert.Contains("find_entities_filtered{name=filter", findCmd);
        Assert.Contains("find_entities_filtered{type=filter", findCmd);
        Assert.Contains("\"distance\":", findCmd);

        await _service.FindBestResourcePatchAsync("iron-ore", 500);
        var patchCmd = _rcon.LastCommand!;
        Assert.Contains("\"iron-ore\"", patchCmd);
        Assert.Contains("radius=500", patchCmd);
        Assert.Contains("type=\"resource\"", patchCmd);
        Assert.Contains("cell_size", patchCmd);
        Assert.Contains("\"best_patch\":", patchCmd);
    }

    [Theory]
    [InlineData("PlaceEntity_NullName")]
    [InlineData("FindNearest_NullType")]
    [InlineData("FindNearest_ZeroRadius")]
    public async Task EntityValidation_ThrowsOnInvalidInput(string scenario)
    {
        switch (scenario)
        {
            case "PlaceEntity_NullName":
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.PlaceEntityAsync(null!, 0, 0));
                break;
            case "FindNearest_NullType":
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _service.FindNearestEntityAsync(null!));
                break;
            case "FindNearest_ZeroRadius":
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => _service.FindNearestEntityAsync("iron-ore", 0));
                break;
        }
    }
}
