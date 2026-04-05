using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class BlueprintServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly BlueprintService _service;

    public BlueprintServiceTests()
    {
        _service = new BlueprintService(_rcon);
    }

    // ── PlaceGhostEntityAsync ─────────────────────────────────────────

    [Fact]
    public async Task PlaceGhostEntityAsync_SendsSilentCommand()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_CreatesEntityGhost()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 10, 20);

        Assert.Contains("entity-ghost", _rcon.LastCommand!);
        Assert.Contains("create_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_UsesInnerName()
    {
        await _service.PlaceGhostEntityAsync("assembling-machine-1", 10, 20);

        Assert.Contains("inner_name=inner", _rcon.LastCommand!);
        Assert.Contains("\"assembling-machine-1\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_ChecksCanPlaceEntity()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 10, 20);

        Assert.Contains("can_place_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_UsesDirection()
    {
        await _service.PlaceGhostEntityAsync("transport-belt", 5, 10, "east");

        Assert.Contains("defines.direction.east", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_DefaultsToNorth()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 10, 20);

        Assert.Contains("defines.direction.north", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_OutputsJsonWithRconPrint()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 10, 20);

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"success\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_ThrowsOnNullEntityName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.PlaceGhostEntityAsync(null!, 10, 20));
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_ThrowsOnEmptyEntityName()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.PlaceGhostEntityAsync("", 10, 20));
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_FormatsCoordinatesWithInvariantCulture()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 10.5, 20.5);

        Assert.Contains("10.5", _rcon.LastCommand!);
        Assert.Contains("20.5", _rcon.LastCommand!);
        Assert.DoesNotContain("10,5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostEntityAsync_ReportsGhostName()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 10, 20);

        Assert.Contains("ghost_name", _rcon.LastCommand!);
    }

    // ── PlaceBlueprintStringAsync ─────────────────────────────────────

    [Fact]
    public async Task PlaceBlueprintStringAsync_SendsSilentCommand()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_ImportsBlueprintString()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20);

        Assert.Contains("import_stack", _rcon.LastCommand!);
        Assert.Contains("0eNqFake", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_ClearsCursorBeforeAndAfter()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20);

        // Should clear cursor at start and after building
        var cmd = _rcon.LastCommand!;
        var firstClear = cmd.IndexOf("clear_cursor()");
        var lastClear = cmd.LastIndexOf("clear_cursor()");
        Assert.True(firstClear >= 0);
        Assert.True(lastClear > firstClear, "Should clear cursor at least twice (before import and after build)");
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_SetsCursorToBlueprint()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20);

        Assert.Contains("set_stack(\"blueprint\")", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_BuildsFromCursor()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20);

        Assert.Contains("build_from_cursor", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_UsesDirection()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20, direction: "east");

        Assert.Contains("defines.direction.east", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_UsesBuildMode()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20, buildMode: "forced");

        Assert.Contains("defines.build_mode.forced", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_DefaultsBuildModeToNormal()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20);

        Assert.Contains("defines.build_mode.normal", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_ChecksImportResult()
    {
        await _service.PlaceBlueprintStringAsync("0eNqFake", 10, 20);

        Assert.Contains("import_result", _rcon.LastCommand!);
        Assert.Contains("invalid_blueprint_string", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_ThrowsOnNullBlueprintString()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.PlaceBlueprintStringAsync(null!, 10, 20));
    }

    [Fact]
    public async Task PlaceBlueprintStringAsync_EscapesBackslashesInBlueprintString()
    {
        await _service.PlaceBlueprintStringAsync("test\\data", 10, 20);

        Assert.Contains("test\\\\data", _rcon.LastCommand!);
    }

    // ── GetGhostEntitiesAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetGhostEntitiesAsync_SendsSilentCommand()
    {
        await _service.GetGhostEntitiesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_FiltersEntityGhosts()
    {
        await _service.GetGhostEntitiesAsync();

        Assert.Contains("type=\"entity-ghost\"", _rcon.LastCommand!);
        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_UsesPlayerPositionByDefault()
    {
        await _service.GetGhostEntitiesAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_UsesCustomCenter()
    {
        await _service.GetGhostEntitiesAsync(centerX: 100, centerY: 200);

        Assert.Contains("{x=100,y=200}", _rcon.LastCommand!);
        Assert.DoesNotContain("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_FallsBackToPlayerPositionWithPartialCenter()
    {
        await _service.GetGhostEntitiesAsync(centerX: 100);

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_UsesDefaultRadius()
    {
        await _service.GetGhostEntitiesAsync();

        Assert.Contains("radius=50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_UsesCustomRadius()
    {
        await _service.GetGhostEntitiesAsync(radius: 100);

        Assert.Contains("radius=100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_ReturnsGhostNameAndPosition()
    {
        await _service.GetGhostEntitiesAsync();

        Assert.Contains("ghost_name", _rcon.LastCommand!);
        Assert.Contains("position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_MapsDirectionToHumanReadableName()
    {
        await _service.GetGhostEntitiesAsync();

        Assert.Contains("dir_names", _rcon.LastCommand!);
        Assert.Contains("defines.direction", _rcon.LastCommand!);
        Assert.DoesNotContain("g.direction or 0", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_OutputsJsonWithCount()
    {
        await _service.GetGhostEntitiesAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"count\"", _rcon.LastCommand!);
        Assert.Contains("\"ghosts\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetGhostEntitiesAsync(radius: 0));
    }

    [Fact]
    public async Task GetGhostEntitiesAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetGhostEntitiesAsync(radius: -5));
    }

    // ── CreateBlueprintFromAreaAsync ───────────────────────────────────

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_SendsSilentCommand()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_CreatesBlueprintInCursor()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.Contains("set_stack(\"blueprint\")", _rcon.LastCommand!);
        Assert.Contains("create_blueprint", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_ExportsStack()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.Contains("export_stack", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_ChecksBlueprintSetup()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.Contains("is_blueprint_setup", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_IncludesTilesWhenRequested()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10, includeTiles: true);

        Assert.Contains("always_include_tiles=true", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_ExcludesTilesByDefault()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.Contains("always_include_tiles=false", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_UsesAreaCoordinates()
    {
        await _service.CreateBlueprintFromAreaAsync(-15, -20, 25, 30);

        Assert.Contains("-15", _rcon.LastCommand!);
        Assert.Contains("-20", _rcon.LastCommand!);
        Assert.Contains("25", _rcon.LastCommand!);
        Assert.Contains("30", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_GetsEntityCount()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.Contains("get_blueprint_entity_count", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_ClearsCursor()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.Contains("clear_cursor", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CreateBlueprintFromAreaAsync_ReportsNoEntitiesError()
    {
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);

        Assert.Contains("no_entities_in_area", _rcon.LastCommand!);
    }

    // ── RevokeGhostEntityAsync ────────────────────────────────────────

    [Fact]
    public async Task RevokeGhostEntityAsync_SendsSilentCommand()
    {
        await _service.RevokeGhostEntityAsync(10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_FindsEntityGhosts()
    {
        await _service.RevokeGhostEntityAsync(10, 20);

        Assert.Contains("type=\"entity-ghost\"", _rcon.LastCommand!);
        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_DestroysGhosts()
    {
        await _service.RevokeGhostEntityAsync(10, 20);

        Assert.Contains("destroy()", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_UsesDefaultRadius()
    {
        await _service.RevokeGhostEntityAsync(10, 20);

        Assert.Contains("radius=1", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_UsesCustomRadius()
    {
        await _service.RevokeGhostEntityAsync(10, 20, radius: 5);

        Assert.Contains("radius=5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_ReportsNoGhostsError()
    {
        await _service.RevokeGhostEntityAsync(10, 20);

        Assert.Contains("no_ghosts", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_ReportsRemovedGhosts()
    {
        await _service.RevokeGhostEntityAsync(10, 20);

        Assert.Contains("\"removed\"", _rcon.LastCommand!);
        Assert.Contains("\"count\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RevokeGhostEntityAsync(10, 20, radius: 0));
    }

    [Fact]
    public async Task RevokeGhostEntityAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.RevokeGhostEntityAsync(10, 20, radius: -1));
    }

    // ── PlaceGhostBatchAsync ──────────────────────────────────────────

    [Fact]
    public async Task PlaceGhostBatchAsync_SendsSilentCommand()
    {
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":0,\"y\":0}]");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_UsesJsonToTable()
    {
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":0,\"y\":0}]");

        Assert.Contains("helpers.json_to_table", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_CreatesEntityGhosts()
    {
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":5,\"y\":10}]");

        Assert.Contains("entity-ghost", _rcon.LastCommand!);
        Assert.Contains("create_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_ChecksCanPlaceEntity()
    {
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":0,\"y\":0}]");

        Assert.Contains("can_place_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_BuildsDirectionMap()
    {
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":0,\"y\":0}]");

        Assert.Contains("defines.direction", _rcon.LastCommand!);
        Assert.Contains("dir_map", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_ReportsPlacedAndSkippedCounts()
    {
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":0,\"y\":0}]");

        Assert.Contains("\"placed\"", _rcon.LastCommand!);
        Assert.Contains("\"skipped\"", _rcon.LastCommand!);
        Assert.Contains("\"total\"", _rcon.LastCommand!);
        Assert.Contains("\"errors\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_EscapesSingleQuotes()
    {
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":0,\"y\":0}]");

        // Input JSON is escaped and embedded in Lua single-quoted string
        Assert.Contains("pcall(helpers.json_to_table", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_ThrowsOnNullInput()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.PlaceGhostBatchAsync(null!));
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_ThrowsOnEmptyInput()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.PlaceGhostBatchAsync(""));
    }

    [Fact]
    public async Task PlaceGhostBatchAsync_ThrowsOnWhitespaceInput()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.PlaceGhostBatchAsync("   "));
    }

    // ── AllCommands_UseSilentCommandPrefix ─────────────────────────────

    [Fact]
    public async Task AllCommands_UseSilentCommandPrefix()
    {
        await _service.PlaceGhostEntityAsync("stone-furnace", 0, 0);
        await _service.PlaceGhostBatchAsync("[{\"name\":\"stone-furnace\",\"x\":0,\"y\":0}]");
        await _service.PlaceBlueprintStringAsync("0eNqFake", 0, 0);
        await _service.GetGhostEntitiesAsync();
        await _service.CreateBlueprintFromAreaAsync(-10, -10, 10, 10);
        await _service.RevokeGhostEntityAsync(0, 0);
        await _service.ValidateGhostPlacementsAsync();

        Assert.Equal(7, _rcon.AllCommands.Count);
        Assert.All(_rcon.AllCommands, cmd => Assert.StartsWith("/silent-command", cmd));
    }

    // ── ValidateGhostPlacementsAsync ──────────────────────────────────

    [Fact]
    public async Task ValidateGhostPlacementsAsync_SendsSilentCommand()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_FindsEntityGhosts()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("type=\"entity-ghost\"", _rcon.LastCommand!);
        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_UsesPlayerPositionByDefault()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_UsesCustomCenter()
    {
        await _service.ValidateGhostPlacementsAsync(centerX: 100, centerY: 200);

        Assert.Contains("{x=100,y=200}", _rcon.LastCommand!);
        Assert.DoesNotContain("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_FallsBackToPlayerPositionWithPartialCenter()
    {
        await _service.ValidateGhostPlacementsAsync(centerX: 100);

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_UsesDefaultRadius()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("radius=50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_UsesCustomRadius()
    {
        await _service.ValidateGhostPlacementsAsync(radius: 100);

        Assert.Contains("radius=100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_ChecksCanPlaceEntity()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("can_place_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_ChecksPlacementBlocked()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("placement_blocked", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_ChecksInserterType()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("ghost_prototype", _rcon.LastCommand!);
        Assert.Contains("\"inserter\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_ChecksPickupPosition()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("pickup_position", _rcon.LastCommand!);
        Assert.Contains("no_pickup_target", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_ChecksDropPosition()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("drop_position", _rcon.LastCommand!);
        Assert.Contains("no_drop_target", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_OutputsJsonWithTotalAndValid()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"total_ghosts\"", _rcon.LastCommand!);
        Assert.Contains("\"valid\"", _rcon.LastCommand!);
        Assert.Contains("\"issues\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_UsesEscFunction()
    {
        await _service.ValidateGhostPlacementsAsync();

        Assert.Contains("esc(", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ValidateGhostPlacementsAsync(radius: 0));
    }

    [Fact]
    public async Task ValidateGhostPlacementsAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ValidateGhostPlacementsAsync(radius: -5));
    }
}
