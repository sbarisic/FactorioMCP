using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

/// <summary>
/// Test double for RconClient that captures the command string passed to ExecuteAsync
/// instead of sending it over TCP. Returns an empty string as the response.
/// </summary>
internal sealed class CapturingRconClient : RconClient
{
    public string? LastCommand { get; private set; }
    public List<string> AllCommands { get; } = [];

    public override Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        AllCommands.Add(command);
        return Task.FromResult(string.Empty);
    }
}

public class FactorioServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly FactorioService _service;

    public FactorioServiceTests()
    {
        _service = new FactorioService(_rcon);
    }

    // ── Walk ──────────────────────────────────────────────────────────

    [Fact]
    public async Task WalkAsync_SendsCorrectDirection()
    {
        await _service.WalkAsync("north");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/c", _rcon.LastCommand);
        Assert.Contains("defines.direction.north", _rcon.LastCommand);
        Assert.Contains("walking = true", _rcon.LastCommand);
    }

    [Theory]
    [InlineData("south")]
    [InlineData("east")]
    [InlineData("west")]
    [InlineData("northeast")]
    [InlineData("northwest")]
    [InlineData("southeast")]
    [InlineData("southwest")]
    public async Task WalkAsync_SupportsAllDirections(string direction)
    {
        await _service.WalkAsync(direction);

        Assert.Contains($"defines.direction.{direction}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task WalkAsync_OutputsJsonWithDirectionAndPosition()
    {
        await _service.WalkAsync("east");

        Assert.Contains("\"status\":\"walking\"", _rcon.LastCommand!);
        Assert.Contains("\"direction\":\"east\"", _rcon.LastCommand!);
        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task WalkAsync_ThrowsOnNullDirection()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.WalkAsync(null!));
    }

    [Fact]
    public async Task WalkAsync_ThrowsOnWhitespaceDirection()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.WalkAsync("  "));
    }

    // ── StopWalking ──────────────────────────────────────────────────

    [Fact]
    public async Task StopWalkingAsync_SetsWalkingFalse()
    {
        await _service.StopWalkingAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/c", _rcon.LastCommand);
        Assert.Contains("walking = false", _rcon.LastCommand);
    }

    [Fact]
    public async Task StopWalkingAsync_OutputsJsonWithStoppedStatus()
    {
        await _service.StopWalkingAsync();

        Assert.Contains("\"status\":\"stopped\"", _rcon.LastCommand!);
        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }

    // ── GetPlayerPosition ────────────────────────────────────────────

    [Fact]
    public async Task GetPlayerPositionAsync_QueriesPosition()
    {
        await _service.GetPlayerPositionAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/c", _rcon.LastCommand);
        Assert.Contains("game.player.position", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetPlayerPositionAsync_OutputsJsonXY()
    {
        await _service.GetPlayerPositionAsync();

        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }

    // ── GetInventory ─────────────────────────────────────────────────

    [Fact]
    public async Task GetInventoryAsync_QueriesMainInventory()
    {
        await _service.GetInventoryAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/c", _rcon.LastCommand);
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
        Assert.StartsWith("/c", _rcon.LastCommand);
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
        Assert.StartsWith("/c", _rcon.LastCommand);
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

    // ── PlaceEntity ──────────────────────────────────────────────────

    [Fact]
    public async Task PlaceEntityAsync_SendsCorrectEntityAndPosition()
    {
        await _service.PlaceEntityAsync("stone-furnace", 5.0, -2.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/c", _rcon.LastCommand);
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
        Assert.Contains("force=game.player.force", _rcon.LastCommand!);
        Assert.Contains("player=game.player", _rcon.LastCommand!);
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
        Assert.StartsWith("/c", _rcon.LastCommand);
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
    public async Task MineEntityAtAsync_MinesIntoMainInventory()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains(".mine{", _rcon.LastCommand!);
        Assert.Contains("get_main_inventory", _rcon.LastCommand!);
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

    // ── GetNearbyEntities ────────────────────────────────────────────

    [Fact]
    public async Task GetNearbyEntitiesAsync_DefaultRadius()
    {
        await _service.GetNearbyEntitiesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/c", _rcon.LastCommand);
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
        Assert.StartsWith("/c", _rcon.LastCommand);
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

    // ── GetResearchStatus ────────────────────────────────────────────

    [Fact]
    public async Task GetResearchStatusAsync_QueriesCurrentResearch()
    {
        await _service.GetResearchStatusAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/c", _rcon.LastCommand);
        Assert.Contains("current_research", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetResearchStatusAsync_OutputsJsonWhenResearching()
    {
        await _service.GetResearchStatusAsync();

        Assert.Contains("\"researching\":true", _rcon.LastCommand!);
        Assert.Contains("\"technology\":\"", _rcon.LastCommand!);
        Assert.Contains("\"progress\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetResearchStatusAsync_OutputsJsonWhenNoResearch()
    {
        await _service.GetResearchStatusAsync();

        Assert.Contains("\"researching\":false", _rcon.LastCommand!);
    }

    // ── ExecuteRawLua ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteRawLuaAsync_PassesThroughLuaCode()
    {
        await _service.ExecuteRawLuaAsync("rcon.print('hello')");

        Assert.NotNull(_rcon.LastCommand);
        Assert.Equal("/c rcon.print('hello')", _rcon.LastCommand);
    }

    // ── Cross-cutting: all commands use /c prefix ────────────────────

    [Fact]
    public async Task AllCommands_UseSlashCPrefix()
    {
        await _service.WalkAsync("north");
        await _service.StopWalkingAsync();
        await _service.GetPlayerPositionAsync();
        await _service.GetInventoryAsync();
        await _service.CraftAsync("iron-plate", 1);
        await _service.GetCraftingQueueAsync();
        await _service.PlaceEntityAsync("stone-furnace", 0, 0);
        await _service.MineEntityAtAsync(0, 0);
        await _service.GetNearbyEntitiesAsync();
        await _service.CheckDistanceAsync(0, 0);
        await _service.GetResearchStatusAsync();
        await _service.ExecuteRawLuaAsync("rcon.print('test')");

        Assert.Equal(12, _rcon.AllCommands.Count);
        Assert.All(_rcon.AllCommands, cmd => Assert.StartsWith("/c ", cmd));
    }
}
