using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── GetReachableEntities ─────────────────────────────────────────

    [Fact]
    public async Task GetReachableEntitiesAsync_SendsSilentCommand()
    {
        await _service.GetReachableEntitiesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_UsesReachDistance()
    {
        await _service.GetReachableEntitiesAsync();

        Assert.Contains("reach_distance", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_UsesPlayerPosition()
    {
        await _service.GetReachableEntitiesAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_FiltersEntitiesByFindEntitiesFiltered()
    {
        await _service.GetReachableEntitiesAsync();

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_WithTypeFilter_IncludesNameFilter()
    {
        await _service.GetReachableEntitiesAsync(type: "stone-furnace");

        Assert.Contains("\"stone-furnace\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_WithMaxDistance_UsesCustomRadius()
    {
        await _service.GetReachableEntitiesAsync(maxDistance: 25.0);

        Assert.Contains("25", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_OutputsJsonWithEntities()
    {
        await _service.GetReachableEntitiesAsync();

        Assert.Contains("\"entities\":[", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_ExcludesPlayerCharacter()
    {
        await _service.GetReachableEntitiesAsync();

        Assert.Contains("player.character", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_ThrowsOnZeroMaxDistance()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetReachableEntitiesAsync(maxDistance: 0));
    }

    [Fact]
    public async Task GetReachableEntitiesAsync_ThrowsOnNegativeMaxDistance()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetReachableEntitiesAsync(maxDistance: -5));
    }

    // ── CountItemInWorld ─────────────────────────────────────────────

    [Fact]
    public async Task CountItemInWorldAsync_SendsSilentCommand()
    {
        await _service.CountItemInWorldAsync("iron-plate");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task CountItemInWorldAsync_IncludesItemName()
    {
        await _service.CountItemInWorldAsync("iron-plate");

        Assert.Contains("iron-plate", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_SearchesNearbyEntities()
    {
        await _service.CountItemInWorldAsync("coal");

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_ChecksPlayerInventory()
    {
        await _service.CountItemInWorldAsync("iron-plate");

        Assert.Contains("get_item_count", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_ChecksChestInventory()
    {
        await _service.CountItemInWorldAsync("iron-plate");

        Assert.Contains("defines.inventory.chest", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_ChecksFurnaceInventories()
    {
        await _service.CountItemInWorldAsync("iron-plate");

        Assert.Contains("defines.inventory.furnace_source", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.furnace_result", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_ChecksAssemblerInventories()
    {
        await _service.CountItemInWorldAsync("iron-plate");

        Assert.Contains("defines.inventory.assembling_machine_input", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.assembling_machine_output", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_UsesCustomRadius()
    {
        await _service.CountItemInWorldAsync("iron-plate", radius: 100);

        Assert.Contains("100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_OutputsJsonWithBreakdown()
    {
        await _service.CountItemInWorldAsync("iron-plate");

        Assert.Contains("\"total\":", _rcon.LastCommand!);
        Assert.Contains("\"player_count\":", _rcon.LastCommand!);
        Assert.Contains("\"container_count\":", _rcon.LastCommand!);
        Assert.Contains("\"containers\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CountItemInWorldAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CountItemInWorldAsync(null!));
    }

    [Fact]
    public async Task CountItemInWorldAsync_ThrowsOnEmptyItemName()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CountItemInWorldAsync(""));
    }

    [Fact]
    public async Task CountItemInWorldAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CountItemInWorldAsync("iron-plate", radius: 0));
    }

    // ── EstimateTravelTime ───────────────────────────────────────────

    [Fact]
    public async Task EstimateTravelTimeAsync_SendsSilentCommand()
    {
        await _service.EstimateTravelTimeAsync(100, 200);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task EstimateTravelTimeAsync_IncludesTargetCoordinates()
    {
        await _service.EstimateTravelTimeAsync(100, 200);

        Assert.Contains("100", _rcon.LastCommand!);
        Assert.Contains("200", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EstimateTravelTimeAsync_UsesPlayerPosition()
    {
        await _service.EstimateTravelTimeAsync(50, 50);

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EstimateTravelTimeAsync_UsesCharacterRunningSpeed()
    {
        await _service.EstimateTravelTimeAsync(50, 50);

        Assert.Contains("character_running_speed", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EstimateTravelTimeAsync_CalculatesDistance()
    {
        await _service.EstimateTravelTimeAsync(50, 50);

        Assert.Contains("math.sqrt", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EstimateTravelTimeAsync_OutputsJsonWithEstimate()
    {
        await _service.EstimateTravelTimeAsync(50, 50);

        Assert.Contains("\"distance\":", _rcon.LastCommand!);
        Assert.Contains("\"estimated_seconds\":", _rcon.LastCommand!);
        Assert.Contains("\"player_x\":", _rcon.LastCommand!);
        Assert.Contains("\"player_y\":", _rcon.LastCommand!);
        Assert.Contains("\"target_x\":", _rcon.LastCommand!);
        Assert.Contains("\"target_y\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EstimateTravelTimeAsync_FormatsDecimalsWithInvariantCulture()
    {
        await _service.EstimateTravelTimeAsync(1.5, -3.75);

        Assert.Contains("1.5", _rcon.LastCommand!);
        Assert.Contains("-3.75", _rcon.LastCommand!);
    }
}
