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
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
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

    [Fact]
    public async Task WalkAsync_IncludesStuckDetectionLogic()
    {
        await _service.WalkAsync("south");

        Assert.NotNull(_rcon.LastCommand);
        Assert.Contains("stuck_ticks", _rcon.LastCommand);
        Assert.Contains("detour_dir", _rcon.LastCommand);
        Assert.Contains("storage.walk_state", _rcon.LastCommand);
    }

    [Fact]
    public async Task StopWalkingAsync_ClearsWalkState()
    {
        await _service.StopWalkingAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.Contains("storage.walk_state = nil", _rcon.LastCommand);
    }

    // ── StopWalking ──────────────────────────────────────────────────

    [Fact]
    public async Task StopWalkingAsync_SetsWalkingFalse()
    {
        await _service.StopWalkingAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
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
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("game.connected_players[1].position", _rcon.LastCommand);
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
    public async Task MineEntityAtAsync_HandlesResourceEntitiesWithDestroy()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("e.type == \"resource\"", _rcon.LastCommand!);
        Assert.Contains("e.destroy()", _rcon.LastCommand!);
        Assert.Contains("player.insert", _rcon.LastCommand!);
        Assert.Contains("mineable_properties", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MineEntityAtAsync_ReturnsAmountForResourceEntities()
    {
        await _service.MineEntityAtAsync(0, 0);

        Assert.Contains("\"amount\":", _rcon.LastCommand!);
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

    // ── GetResearchStatus ────────────────────────────────────────────

    [Fact]
    public async Task GetResearchStatusAsync_QueriesCurrentResearch()
    {
        await _service.GetResearchStatusAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
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

    // ── GetAvailableTechnologies ─────────────────────────────────────

    [Fact]
    public async Task GetAvailableTechnologiesAsync_SendsSilentCommand()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_UsesForce()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("game.connected_players[1].force", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_IteratesTechnologies()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("force.technologies", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_ChecksPrerequisites()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("tech.prerequisites", _rcon.LastCommand!);
        Assert.Contains("prereq.researched", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_FiltersResearchedAndDisabled()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("not tech.researched", _rcon.LastCommand!);
        Assert.Contains("tech.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_IncludesCostAndIngredients()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("research_unit_count", _rcon.LastCommand!);
        Assert.Contains("research_unit_ingredients", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_OutputsJsonWithTechnologiesArray()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("\"technologies\":[", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
    }

    // ── StartResearch ────────────────────────────────────────────────

    [Fact]
    public async Task StartResearchAsync_SendsSilentCommand()
    {
        await _service.StartResearchAsync("automation");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task StartResearchAsync_LookUpTechnologyByName()
    {
        await _service.StartResearchAsync("logistics");

        Assert.Contains("force.technologies[\"logistics\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_ValidatesTechnologyExists()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("\"error\":\"unknown_technology\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_ChecksAlreadyResearched()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("tech.researched", _rcon.LastCommand!);
        Assert.Contains("\"error\":\"already_researched\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_UsesAddResearch()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("force.add_research(tech)", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_UsesPcallForSafety()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("pcall", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_OutputsJsonSuccessResponse()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("\"success\":true", _rcon.LastCommand!);
        Assert.Contains("\"technology\":\"", _rcon.LastCommand!);
        Assert.Contains("\"cost\":", _rcon.LastCommand!);
        Assert.Contains("\"ingredients\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_OutputsJsonErrorOnFailure()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("\"success\":false", _rcon.LastCommand!);
        Assert.Contains("\"error\":\"research_failed\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_ThrowsOnNullTechnology()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.StartResearchAsync(null!));
    }

    [Fact]
    public async Task StartResearchAsync_ThrowsOnWhitespaceTechnology()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.StartResearchAsync("   "));
    }

    // ── GetRecipeDetails ─────────────────────────────────────────────

    [Fact]
    public async Task GetRecipeDetailsAsync_SendsSilentCommand()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_LooksUpRecipeByName()
    {
        await _service.GetRecipeDetailsAsync("electronic-circuit");

        Assert.Contains("force.recipes[\"electronic-circuit\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_ValidatesRecipeExists()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("\"error\":\"unknown_recipe\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_IncludesIngredientsAndProducts()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("recipe.ingredients", _rcon.LastCommand!);
        Assert.Contains("recipe.products", _rcon.LastCommand!);
        Assert.Contains("\"ingredients\":[", _rcon.LastCommand!);
        Assert.Contains("\"products\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_IncludesEnergyAndCategory()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("recipe.energy", _rcon.LastCommand!);
        Assert.Contains("recipe.category", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_IncludesEnabledStatus()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("recipe.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_ThrowsOnNullRecipe()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetRecipeDetailsAsync(null!));
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_ThrowsOnWhitespaceRecipe()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetRecipeDetailsAsync("   "));
    }

    // ── GetAvailableRecipes ──────────────────────────────────────────

    [Fact]
    public async Task GetAvailableRecipesAsync_SendsSilentCommand()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_FiltersEnabledRecipes()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("recipe.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_IteratesForceRecipes()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("force.recipes", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_OutputsJsonWithRecipesArray()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("\"recipes\":[", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_IncludesCategoryAndEnergy()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("\"category\":\"", _rcon.LastCommand!);
        Assert.Contains("\"energy\":", _rcon.LastCommand!);
    }

    // ── GetTechnologyDetails ─────────────────────────────────────────

    [Fact]
    public async Task GetTechnologyDetailsAsync_SendsSilentCommand()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_LooksUpTechnologyByName()
    {
        await _service.GetTechnologyDetailsAsync("logistics");

        Assert.Contains("force.technologies[\"logistics\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_ValidatesTechnologyExists()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("\"error\":\"unknown_technology\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesPrerequisites()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("tech.prerequisites", _rcon.LastCommand!);
        Assert.Contains("\"prerequisites\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesEffectsWithRecipeUnlocks()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("tech.effects", _rcon.LastCommand!);
        Assert.Contains("\"effects\":[", _rcon.LastCommand!);
        Assert.Contains("unlock-recipe", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesCostAndIngredients()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("research_unit_count", _rcon.LastCommand!);
        Assert.Contains("research_unit_ingredients", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesResearchedAndEnabledStatus()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("tech.researched", _rcon.LastCommand!);
        Assert.Contains("tech.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_ThrowsOnNullTechnology()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetTechnologyDetailsAsync(null!));
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_ThrowsOnWhitespaceTechnology()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetTechnologyDetailsAsync("   "));
    }

    // ── ExecuteRawLua ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteRawLuaAsync_PassesThroughLuaCode()
    {
        await _service.ExecuteRawLuaAsync("rcon.print('hello')");

        Assert.NotNull(_rcon.LastCommand);
        Assert.Equal("/silent-command rcon.print('hello')", _rcon.LastCommand);
    }

    // ── Cross-cutting: all commands use /silent-command prefix ────────────────────

    [Fact]
    public async Task AllCommands_UseSilentCommandPrefix()
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
        await _service.GetAvailableTechnologiesAsync();
        await _service.StartResearchAsync("automation");
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");
        await _service.GetAvailableRecipesAsync();
        await _service.GetTechnologyDetailsAsync("automation");
        await _service.ExecuteRawLuaAsync("rcon.print('test')");
        await _service.GetGameTickAsync();
        await _service.ScanResourcesAsync();
        await _service.ScanTilesAsync();
        await _service.InsertItemsAsync(0, 0, "coal", 5);
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 10);
        await _service.InspectEntityAsync(0, 0);
        await _service.InitializeChatListenerAsync();
        await _service.GetChatMessagesAsync();
        await _service.SendChatMessageAsync("hello");

        Assert.Equal(26, _rcon.AllCommands.Count);
        Assert.All(_rcon.AllCommands, cmd => Assert.StartsWith("/silent-command ", cmd));
    }

    // ── GetGameTick ──────────────────────────────────────────────────

    [Fact]
    public async Task GetGameTickAsync_QueriesGameTick()
    {
        await _service.GetGameTickAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("game.tick", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetGameTickAsync_OutputsJsonWithTick()
    {
        await _service.GetGameTickAsync();

        Assert.Contains("\"tick\":", _rcon.LastCommand!);
    }

    // ── WaitForCrafting ──────────────────────────────────────────────

    [Fact]
    public async Task WaitForCraftingAsync_ThrowsOnZeroPollInterval()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForCraftingAsync(TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForCraftingAsync_ThrowsOnZeroTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForCraftingAsync(TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForCraftingAsync_ReturnsCompleteWhenQueueEmpty()
    {
        // CapturingRconClient returns empty string, which does not contain "queue":[]
        // but the crafting queue Lua script outputs that pattern. We need a scripted client.
        var scripted = new ScriptedRconClient(["\"queue\":[]"]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForCraftingAsync(
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"complete\"", result);
    }

    [Fact]
    public async Task WaitForCraftingAsync_ReturnsTimeoutWhenQueueNeverEmpties()
    {
        var scripted = new ScriptedRconClient([
            "\"queue\":[{\"recipe\":\"iron-gear-wheel\",\"count\":5}]",
            "\"queue\":[{\"recipe\":\"iron-gear-wheel\",\"count\":3}]",
            "\"queue\":[{\"recipe\":\"iron-gear-wheel\",\"count\":1}]",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForCraftingAsync(
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
    }

    // ── WaitForPosition ──────────────────────────────────────────────

    [Fact]
    public async Task WaitForPositionAsync_ThrowsOnZeroTolerance()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForPositionAsync(0, 0, 0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForPositionAsync_ThrowsOnNegativeTolerance()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForPositionAsync(0, 0, -1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForPositionAsync_ThrowsOnZeroPollInterval()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForPositionAsync(0, 0, 2, TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForPositionAsync_ThrowsOnZeroTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForPositionAsync(0, 0, 2, TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForPositionAsync_ReturnsArrivedWhenWithinTolerance()
    {
        // Simulate player at (9.5, -1.0), target (10, -1), tolerance 2 → distance ~0.5
        var scripted = new ScriptedRconClient(["{\"x\":9.5,\"y\":-1,\"distance\":0.50}"]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForPositionAsync(
            10, -1, 2.0,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"arrived\"", result);
    }

    [Fact]
    public async Task WaitForPositionAsync_ReturnsTimeoutWhenNotReached()
    {
        // Simulate player stuck far away
        var scripted = new ScriptedRconClient([
            "{\"x\":0,\"y\":0,\"distance\":14.14}",
            "{\"x\":1,\"y\":1,\"distance\":12.73}",
            "{\"x\":2,\"y\":2,\"distance\":11.31}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForPositionAsync(
            10, 10, 2.0,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
    }

    [Fact]
    public async Task WaitForPositionAsync_SendsLuaWithTargetCoordinates()
    {
        var scripted = new ScriptedRconClient(["{\"x\":5,\"y\":5,\"distance\":0.50}"]);
        var service = new FactorioService(scripted);

        await service.WaitForPositionAsync(
            10.5, -3.25, 2.0,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("10.5", scripted.AllCommands[0]);
        Assert.Contains("-3.25", scripted.AllCommands[0]);
    }

    // ── WaitForTicks ─────────────────────────────────────────────────

    [Fact]
    public async Task WaitForTicksAsync_ThrowsOnZeroTicks()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForTicksAsync(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForTicksAsync_ThrowsOnNegativeTicks()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForTicksAsync(-1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForTicksAsync_ThrowsOnZeroPollInterval()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForTicksAsync(60, TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForTicksAsync_ThrowsOnZeroTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForTicksAsync(60, TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForTicksAsync_ReturnsCompleteWhenTicksElapse()
    {
        // Start at tick 100, wait 60 ticks, next poll returns tick 160
        var scripted = new ScriptedRconClient([
            "{\"tick\":100}",
            "{\"tick\":160}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForTicksAsync(
            60,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"complete\"", result);
        Assert.Contains("\"start_tick\":100", result);
    }

    [Fact]
    public async Task WaitForTicksAsync_ReturnsTimeoutWhenTicksDontElapse()
    {
        // Start at tick 100, wait 600 ticks, but game barely advances
        var scripted = new ScriptedRconClient([
            "{\"tick\":100}",
            "{\"tick\":105}",
            "{\"tick\":110}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForTicksAsync(
            600,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
        Assert.Contains("\"target_tick\":700", result);
    }

    [Fact]
    public async Task WaitForTicksAsync_QueriesGameTick()
    {
        var scripted = new ScriptedRconClient([
            "{\"tick\":100}",
            "{\"tick\":200}",
        ]);
        var service = new FactorioService(scripted);

        await service.WaitForTicksAsync(
            60,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.All(scripted.AllCommands, cmd => Assert.Contains("game.tick", cmd));
    }

    // ── ScanResources ────────────────────────────────────────────────

    [Fact]
    public async Task ScanResourcesAsync_SendsCorrectLuaCommand()
    {
        await _service.ScanResourcesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("find_entities_filtered", _rcon.LastCommand);
        Assert.Contains("type=\"resource\"", _rcon.LastCommand);
    }

    [Fact]
    public async Task ScanResourcesAsync_UsesDefaultRadius()
    {
        await _service.ScanResourcesAsync();

        Assert.Contains("radius=50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_UsesCustomRadius()
    {
        await _service.ScanResourcesAsync(100);

        Assert.Contains("radius=100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_OutputsJsonResourcesSummary()
    {
        await _service.ScanResourcesAsync();

        Assert.Contains("\"scan_radius\":", _rcon.LastCommand!);
        Assert.Contains("\"resources\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_IncludesResourceDetails()
    {
        await _service.ScanResourcesAsync();

        Assert.Contains("\"name\":\"", _rcon.LastCommand!);
        Assert.Contains("\"patches\":", _rcon.LastCommand!);
        Assert.Contains("\"total_amount\":", _rcon.LastCommand!);
        Assert.Contains("\"center_x\":", _rcon.LastCommand!);
        Assert.Contains("\"center_y\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanResourcesAsync(0));
    }

    [Fact]
    public async Task ScanResourcesAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanResourcesAsync(-5));
    }

    // ── ScanTiles ────────────────────────────────────────────────────

    [Fact]
    public async Task ScanTilesAsync_SendsCorrectLuaCommand()
    {
        await _service.ScanTilesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("find_tiles_filtered", _rcon.LastCommand);
    }

    [Fact]
    public async Task ScanTilesAsync_UsesDefaultRadius()
    {
        await _service.ScanTilesAsync();

        // The radius appears in the scan_radius output field
        Assert.Contains("\"scan_radius\":16", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_UsesCustomRadius()
    {
        await _service.ScanTilesAsync(32);

        Assert.Contains("\"scan_radius\":32", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_OutputsJsonTilesSummary()
    {
        await _service.ScanTilesAsync();

        Assert.Contains("\"tiles\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanTilesAsync(0));
    }

    [Fact]
    public async Task ScanTilesAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanTilesAsync(-5));
    }

    // ── InsertItems ──────────────────────────────────────────────────

    [Fact]
    public async Task InsertItemsAsync_SendsCorrectItemAndCount()
    {
        await _service.InsertItemsAsync(5.0, 3.0, "coal", 10, "fuel");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("coal", _rcon.LastCommand);
        Assert.Contains("10", _rcon.LastCommand);
    }

    [Fact]
    public async Task InsertItemsAsync_ChecksProximity()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 1);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_PrioritizesNonResourceEntities()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 1);

        Assert.Contains("table.sort", _rcon.LastCommand!);
        Assert.Contains("\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_MapsInventoryTypes()
    {
        await _service.InsertItemsAsync(0, 0, "iron-ore", 5, "furnace_source");

        Assert.Contains("defines.inventory.fuel", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.furnace_source", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.furnace_result", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.chest", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_ChecksPlayerInventory()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 1);

        Assert.Contains("get_item_count", _rcon.LastCommand!);
        Assert.Contains("no_items", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_InsertsIntoEntityInventory()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 5);

        Assert.Contains("inv.insert", _rcon.LastCommand!);
        Assert.Contains("remove_item", _rcon.LastCommand!);
        Assert.Contains("\"inserted\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.InsertItemsAsync(0, 0, null!, 1));
    }

    [Fact]
    public async Task InsertItemsAsync_ThrowsOnZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.InsertItemsAsync(0, 0, "coal", 0));
    }

    // ── RemoveItems ──────────────────────────────────────────────────

    [Fact]
    public async Task RemoveItemsAsync_SendsCorrectItemAndCount()
    {
        await _service.RemoveItemsAsync(5.0, 3.0, "iron-plate", 20, "furnace_result");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("iron-plate", _rcon.LastCommand);
        Assert.Contains("20", _rcon.LastCommand);
    }

    [Fact]
    public async Task RemoveItemsAsync_ChecksProximity()
    {
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 1);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RemoveItemsAsync_ChecksEntityInventoryContents()
    {
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 1);

        Assert.Contains("get_item_count", _rcon.LastCommand!);
        Assert.Contains("no_items", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RemoveItemsAsync_RemovesFromEntityAndInsertsToPlayer()
    {
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 5);

        Assert.Contains("inv.remove", _rcon.LastCommand!);
        Assert.Contains("player.insert", _rcon.LastCommand!);
        Assert.Contains("\"removed\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RemoveItemsAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.RemoveItemsAsync(0, 0, null!, 1));
    }

    [Fact]
    public async Task RemoveItemsAsync_ThrowsOnZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.RemoveItemsAsync(0, 0, "iron-plate", 0));
    }

    // ── InspectEntity ────────────────────────────────────────────────

    [Fact]
    public async Task InspectEntityAsync_SendsCorrectPosition()
    {
        await _service.InspectEntityAsync(7.5, -3.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("7.5", _rcon.LastCommand);
        Assert.Contains("-3", _rcon.LastCommand);
    }

    [Fact]
    public async Task InspectEntityAsync_ChecksProximity()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_PrioritizesNonResourceEntities()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("table.sort", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsEntityStatus()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("e.status", _rcon.LastCommand!);
        Assert.Contains("defines.entity_status", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsInventoryContents()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("get_inventory", _rcon.LastCommand!);
        Assert.Contains("get_contents", _rcon.LastCommand!);
        Assert.Contains("\"inventories\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsBurnerInfo()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("e.burner", _rcon.LastCommand!);
        Assert.Contains("remaining_burning_fuel", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsRecipe()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("get_recipe", _rcon.LastCommand!);
        Assert.Contains("\"recipe\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsHealth()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("e.health", _rcon.LastCommand!);
        Assert.Contains("\"health\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsMiningTarget()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("mining_target", _rcon.LastCommand!);
    }

    // ── InitializeChatListener ────────────────────────────────────────

    [Fact]
    public async Task InitializeChatListenerAsync_RegistersEventHandler()
    {
        await _service.InitializeChatListenerAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("on_console_chat", _rcon.LastCommand);
        Assert.Contains("storage.chat_log", _rcon.LastCommand);
    }

    [Fact]
    public async Task InitializeChatListenerAsync_PreservesExistingMessages()
    {
        await _service.InitializeChatListenerAsync();

        Assert.Contains("storage.chat_log = storage.chat_log or {}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InitializeChatListenerAsync_CapturesPlayerName()
    {
        await _service.InitializeChatListenerAsync();

        Assert.Contains("game.get_player", _rcon.LastCommand!);
        Assert.Contains("player_name", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InitializeChatListenerAsync_OutputsJsonStatus()
    {
        await _service.InitializeChatListenerAsync();

        Assert.Contains("\"status\":\"initialized\"", _rcon.LastCommand!);
        Assert.Contains("\"existing_messages\":", _rcon.LastCommand!);
    }

    // ── GetChatMessages ───────────────────────────────────────────────

    [Fact]
    public async Task GetChatMessagesAsync_QueriesChatLog()
    {
        await _service.GetChatMessagesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("storage.chat_log", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetChatMessagesAsync_DefaultSinceTickIsZero()
    {
        await _service.GetChatMessagesAsync();

        Assert.Contains("since_tick = 0", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_FiltersBySinceTick()
    {
        await _service.GetChatMessagesAsync(12345);

        Assert.Contains("since_tick = 12345", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_OutputsJsonWithMessages()
    {
        await _service.GetChatMessagesAsync();

        Assert.Contains("\"messages\":[", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
        Assert.Contains("\"latest_tick\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_IncludesJsonEscaping()
    {
        await _service.GetChatMessagesAsync();

        Assert.Contains("json_escape", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_ThrowsOnNegativeSinceTick()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetChatMessagesAsync(-1));
    }

    // ── SendChatMessage ───────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessageAsync_SendsMessageViaGamePrint()
    {
        await _service.SendChatMessageAsync("Hello world");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("game.print", _rcon.LastCommand);
        Assert.Contains("Hello world", _rcon.LastCommand);
    }

    [Fact]
    public async Task SendChatMessageAsync_TagsWithAiPrefix()
    {
        await _service.SendChatMessageAsync("test message");

        Assert.Contains("[AI]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_EscapesQuotes()
    {
        await _service.SendChatMessageAsync("He said \"hello\"");

        Assert.Contains("\\\"hello\\\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_EscapesBackslashes()
    {
        await _service.SendChatMessageAsync("path\\to\\file");

        Assert.Contains("\\\\", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_EscapesNewlines()
    {
        await _service.SendChatMessageAsync("line1\nline2");

        Assert.Contains("\\n", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_OutputsJsonStatus()
    {
        await _service.SendChatMessageAsync("test");

        Assert.Contains("\"status\":\"sent\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_ThrowsOnNullMessage()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SendChatMessageAsync(null!));
    }

    [Fact]
    public async Task SendChatMessageAsync_ThrowsOnWhitespaceMessage()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SendChatMessageAsync("   "));
    }
}

/// <summary>
/// Test double for RconClient that returns pre-configured responses in sequence.
/// When responses are exhausted, repeats the last one.
/// </summary>
internal sealed class ScriptedRconClient : RconClient
{
    private readonly string[] _responses;
    private int _index;
    public List<string> AllCommands { get; } = [];

    public ScriptedRconClient(string[] responses)
    {
        _responses = responses;
    }

    public override Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        AllCommands.Add(command);
        var response = _responses[Math.Min(_index, _responses.Length - 1)];
        _index++;
        return Task.FromResult(response);
    }
}
