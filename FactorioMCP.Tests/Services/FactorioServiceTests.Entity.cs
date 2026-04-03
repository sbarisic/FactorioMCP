using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── PlaceEntity ──────────────────────────────────────────────────

    [Fact]
    public async Task PlaceEntityAsync_SendsCorrectEntityAndPosition()
    {
        await _service.PlaceEntityAsync("stone-furnace", 5.0, -2.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("\"stone-furnace\"", _rcon.LastCommand);
        Assert.Contains("5", _rcon.LastCommand);
        Assert.Contains("-2", _rcon.LastCommand);
    }

    [Fact]
    public async Task PlaceEntityAsync_DefaultsToNorthDirection()
    {
        await _service.PlaceEntityAsync("transport-belt", 1.0, 2.0);

        Assert.Contains("defines.direction.north", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_UsesSpecifiedDirection()
    {
        await _service.PlaceEntityAsync("transport-belt", 1.0, 2.0, "east");

        Assert.Contains("defines.direction.east", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_ChecksProximity()
    {
        await _service.PlaceEntityAsync("stone-furnace", 0, 0);

        Assert.Contains("build_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_ChecksCanPlace()
    {
        await _service.PlaceEntityAsync("stone-furnace", 0, 0);

        Assert.Contains("can_place_entity", _rcon.LastCommand!);
        Assert.Contains("invalid_position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_ChecksInventory()
    {
        await _service.PlaceEntityAsync("stone-furnace", 0, 0);

        Assert.Contains("get_item_count", _rcon.LastCommand!);
        Assert.Contains("missing_item", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_OutputsJsonSuccessResponse()
    {
        await _service.PlaceEntityAsync("stone-furnace", 5.0, -2.0);

        Assert.Contains("\"success\":true", _rcon.LastCommand!);
        Assert.Contains("\"entity\":\"", _rcon.LastCommand!);
        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_CreatesEntityWithForceAndPlayer()
    {
        await _service.PlaceEntityAsync("assembling-machine-1", 3.0, 4.0);

        Assert.Contains("create_entity", _rcon.LastCommand!);
        Assert.Contains("force=player.force", _rcon.LastCommand!);
        Assert.Contains("player=player", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_RemovesItemFromInventory()
    {
        await _service.PlaceEntityAsync("stone-furnace", 0, 0);

        Assert.Contains("remove_item", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_FormatsDecimalPositionsWithInvariantCulture()
    {
        await _service.PlaceEntityAsync("stone-furnace", 1.5, -3.75);

        // Verify decimal points are dots not commas (InvariantCulture)
        Assert.Contains("1.5", _rcon.LastCommand!);
        Assert.Contains("-3.75", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceEntityAsync_ThrowsOnNullEntityName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.PlaceEntityAsync(null!, 0, 0));
    }

    [Fact]
    public async Task PlaceEntityAsync_ThrowsOnNullDirection()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.PlaceEntityAsync("stone-furnace", 0, 0, null!));
    }

    // ── MineEntity ───────────────────────────────────────────────────

    [Fact]
    public async Task MineEntityAtAsync_SendsCorrectPosition()
    {
        await _service.MineEntityAtAsync(5.0, -2.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("5", _rcon.LastCommand);
        Assert.Contains("-2", _rcon.LastCommand);
    }

    [Fact]
    public async Task MineEntityAtAsync_ChecksProximity()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_FindsEntitiesAtPosition()
    {
        await _service.MineEntityAtAsync(3.0, 4.0);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("radius=1", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_UsesPlayerMineEntity()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("mine_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_OutputsJsonSuccessResponse()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("\"success\":true", _rcon.LastCommand!);
        Assert.Contains("\"entity\":\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_OutputsJsonNoEntityError()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("\"success\":false", _rcon.LastCommand!);
        Assert.Contains("\"error\":\"no_entity\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_FormatsDecimalsWithInvariantCulture()
    {
        await _service.MineEntityAtAsync(1.5, -3.75);

        Assert.Contains("1.5", _rcon.LastCommand!);
        Assert.Contains("-3.75", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_PrioritizesNonResourceEntities()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("table.sort", _rcon.LastCommand!);
        Assert.Contains("\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_RedirectsResourceEntitiesToMineResource()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("e.type == \"resource\"", _rcon.LastCommand!);
        Assert.Contains("use_mine_resource", _rcon.LastCommand!);
        Assert.DoesNotContain("e.destroy()", _rcon.LastCommand!);
        Assert.DoesNotContain("player.insert", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_ResourceRedirectIncludesEntityInfo()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("e.amount", _rcon.LastCommand!);
        Assert.Contains("MineResource", _rcon.LastCommand!);
    }

    // ── GetNearbyEntities ────────────────────────────────────────────

    [Fact]
    public async Task GetNearbyEntitiesAsync_DefaultRadius()
    {
        await _service.GetNearbyEntitiesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("find_entities_filtered", _rcon.LastCommand);
        Assert.Contains("radius=10", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetNearbyEntitiesAsync_CustomRadius()
    {
        await _service.GetNearbyEntitiesAsync(25.5);

        Assert.Contains("radius=25.5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetNearbyEntitiesAsync_OutputsJsonEntitiesArray()
    {
        await _service.GetNearbyEntitiesAsync();

        Assert.Contains("\"entities\":[", _rcon.LastCommand!);
        Assert.Contains("\"name\":\"", _rcon.LastCommand!);
        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetNearbyEntitiesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetNearbyEntitiesAsync(0));
    }

    [Fact]
    public async Task GetNearbyEntitiesAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetNearbyEntitiesAsync(-5));
    }

    // ── CheckDistance ────────────────────────────────────────────────

    [Fact]
    public async Task CheckDistanceAsync_SendsCorrectCoordinates()
    {
        await _service.CheckDistanceAsync(10.0, -5.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("10", _rcon.LastCommand);
        Assert.Contains("-5", _rcon.LastCommand);
    }

    [Fact]
    public async Task CheckDistanceAsync_CalculatesDistance()
    {
        await _service.CheckDistanceAsync(3.0, 4.0);

        Assert.Contains("math.sqrt", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckDistanceAsync_ChecksBuildAndReachRange()
    {
        await _service.CheckDistanceAsync(0, 0);

        Assert.Contains("build_distance", _rcon.LastCommand!);
        Assert.Contains("reach_distance", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckDistanceAsync_OutputsJsonWithRangeStatus()
    {
        await _service.CheckDistanceAsync(0, 0);

        Assert.Contains("\"distance\":", _rcon.LastCommand!);
        Assert.Contains("\"build_in_range\":", _rcon.LastCommand!);
        Assert.Contains("\"build_limit\":", _rcon.LastCommand!);
        Assert.Contains("\"reach_in_range\":", _rcon.LastCommand!);
        Assert.Contains("\"reach_limit\":", _rcon.LastCommand!);
    }

    // ── RotateEntity ────────────────────────────────────────────────

    [Fact]
    public async Task RotateEntityAsync_SendsCorrectPosition()
    {
        await _service.RotateEntityAsync(5.0, -2.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("5", _rcon.LastCommand);
        Assert.Contains("-2", _rcon.LastCommand);
    }

    [Fact]
    public async Task RotateEntityAsync_ChecksProximity()
    {
        await _service.RotateEntityAsync(0, 0);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RotateEntityAsync_UsesEntityRotate()
    {
        await _service.RotateEntityAsync(0, 0);

        Assert.Contains("e.rotate(", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RotateEntityAsync_DefaultsToForwardRotation()
    {
        await _service.RotateEntityAsync(0, 0);

        Assert.Contains("reverse=false", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RotateEntityAsync_SupportsReverseRotation()
    {
        await _service.RotateEntityAsync(0, 0, reverse: true);

        Assert.Contains("reverse=true", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RotateEntityAsync_OutputsDirectionInfo()
    {
        await _service.RotateEntityAsync(0, 0);

        Assert.Contains("\"previous_direction\":", _rcon.LastCommand!);
        Assert.Contains("\"new_direction\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RotateEntityAsync_PrioritizesNonResourceEntities()
    {
        await _service.RotateEntityAsync(0, 0);

        Assert.Contains("table.sort", _rcon.LastCommand!);
        Assert.Contains("\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RotateEntityAsync_FormatsDecimalsWithInvariantCulture()
    {
        await _service.RotateEntityAsync(1.5, -3.75);

        Assert.Contains("1.5", _rcon.LastCommand!);
        Assert.Contains("-3.75", _rcon.LastCommand!);
    }

    // ── FindNearestEntity ────────────────────────────────────────────

    [Fact]
    public async Task FindNearestEntityAsync_SendsCorrectEntityType()
    {
        await _service.FindNearestEntityAsync("stone-furnace");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("\"stone-furnace\"", _rcon.LastCommand);
    }

    [Fact]
    public async Task FindNearestEntityAsync_SearchesByNameFirst()
    {
        await _service.FindNearestEntityAsync("stone-furnace");

        Assert.Contains("find_entities_filtered{name=filter", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindNearestEntityAsync_FallsBackToType()
    {
        await _service.FindNearestEntityAsync("resource");

        Assert.Contains("find_entities_filtered{type=filter", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindNearestEntityAsync_DefaultRadius()
    {
        await _service.FindNearestEntityAsync("iron-ore");

        Assert.Contains("radius=100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindNearestEntityAsync_CustomRadius()
    {
        await _service.FindNearestEntityAsync("iron-ore", 50);

        Assert.Contains("radius=50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindNearestEntityAsync_OutputsDistance()
    {
        await _service.FindNearestEntityAsync("stone-furnace");

        Assert.Contains("\"distance\":", _rcon.LastCommand!);
        Assert.Contains("\"total_found\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindNearestEntityAsync_OutputsNotFoundError()
    {
        await _service.FindNearestEntityAsync("stone-furnace");

        Assert.Contains("\"not_found\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindNearestEntityAsync_ThrowsOnNullType()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.FindNearestEntityAsync(null!));
    }

    [Fact]
    public async Task FindNearestEntityAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.FindNearestEntityAsync("iron-ore", 0));
    }

    // ── FindBestResourcePatch ────────────────────────────────────────

    [Fact]
    public async Task FindBestResourcePatchAsync_SendsCorrectResourceName()
    {
        await _service.FindBestResourcePatchAsync("iron-ore");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("\"iron-ore\"", _rcon.LastCommand);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_FiltersResourceType()
    {
        await _service.FindBestResourcePatchAsync("iron-ore");

        Assert.Contains("type=\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_DefaultRadius()
    {
        await _service.FindBestResourcePatchAsync("iron-ore");

        Assert.Contains("radius=200", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_CustomRadius()
    {
        await _service.FindBestResourcePatchAsync("iron-ore", 500);

        Assert.Contains("radius=500", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_GroupsIntoClusters()
    {
        await _service.FindBestResourcePatchAsync("iron-ore");

        Assert.Contains("cell_size", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_OutputsBestPatch()
    {
        await _service.FindBestResourcePatchAsync("iron-ore");

        Assert.Contains("\"best_patch\":", _rcon.LastCommand!);
        Assert.Contains("\"center_x\":", _rcon.LastCommand!);
        Assert.Contains("\"center_y\":", _rcon.LastCommand!);
        Assert.Contains("\"total_amount\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_OutputsAlternatives()
    {
        await _service.FindBestResourcePatchAsync("iron-ore");

        Assert.Contains("\"alternatives\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_OutputsNotFoundError()
    {
        await _service.FindBestResourcePatchAsync("iron-ore");

        Assert.Contains("\"not_found\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_ThrowsOnNullResourceName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.FindBestResourcePatchAsync(null!));
    }

    [Fact]
    public async Task FindBestResourcePatchAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.FindBestResourcePatchAsync("iron-ore", 0));
    }
}
