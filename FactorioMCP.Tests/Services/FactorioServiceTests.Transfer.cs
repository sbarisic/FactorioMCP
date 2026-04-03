using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── DropItemsAsync ───────────────────────────────────────────

    [Fact]
    public async Task DropItemsAsync_SendsSilentCommand()
    {
        await _service.DropItemsAsync("iron-plate", 10);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task DropItemsAsync_ChecksItemCount()
    {
        await _service.DropItemsAsync("iron-plate", 10);

        Assert.Contains("get_item_count", _rcon.LastCommand!);
    }

    [Fact]
    public async Task DropItemsAsync_RemovesItemsBeforeSpilling()
    {
        await _service.DropItemsAsync("iron-plate", 10);

        Assert.Contains("remove_item", _rcon.LastCommand!);
    }

    [Fact]
    public async Task DropItemsAsync_UsesSpillItemStack()
    {
        await _service.DropItemsAsync("iron-plate", 10);

        Assert.Contains("spill_item_stack", _rcon.LastCommand!);
    }

    [Fact]
    public async Task DropItemsAsync_ReportsNoItemsError()
    {
        await _service.DropItemsAsync("iron-plate", 10);

        Assert.Contains("no_items", _rcon.LastCommand!);
    }

    [Fact]
    public async Task DropItemsAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.DropItemsAsync(null!, 10));
    }

    [Fact]
    public async Task DropItemsAsync_ThrowsOnZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.DropItemsAsync("iron-plate", 0));
    }

    [Fact]
    public async Task DropItemsAsync_ThrowsOnNegativeCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.DropItemsAsync("iron-plate", -5));
    }

    [Fact]
    public async Task DropItemsAsync_FormatsCountWithInvariantCulture()
    {
        await _service.DropItemsAsync("iron-plate", 50);

        Assert.Contains("50", _rcon.LastCommand!);
    }

    // ── TransferAllItemsAsync ────────────────────────────────────

    [Fact]
    public async Task TransferAllItemsAsync_SendsSilentCommand()
    {
        await _service.TransferAllItemsAsync(10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task TransferAllItemsAsync_FindsEntityAtPosition()
    {
        await _service.TransferAllItemsAsync(10, 20);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TransferAllItemsAsync_GetsInventory()
    {
        await _service.TransferAllItemsAsync(10, 20);

        Assert.Contains("get_inventory", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TransferAllItemsAsync_DefaultsToChest()
    {
        await _service.TransferAllItemsAsync(10, 20);

        Assert.Contains("defines.inventory.chest", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TransferAllItemsAsync_UsesCustomInventoryType()
    {
        await _service.TransferAllItemsAsync(10, 20, "furnace_result");

        Assert.Contains("defines.inventory.furnace_result", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TransferAllItemsAsync_InsertsIntoPlayer()
    {
        await _service.TransferAllItemsAsync(10, 20);

        Assert.Contains("player.insert", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TransferAllItemsAsync_ReportsNoEntityError()
    {
        await _service.TransferAllItemsAsync(10, 20);

        Assert.Contains("no_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TransferAllItemsAsync_ReportsTransferredItems()
    {
        await _service.TransferAllItemsAsync(10, 20);

        Assert.Contains("\"transferred\"", _rcon.LastCommand!);
        Assert.Contains("\"total_items\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TransferAllItemsAsync_ThrowsOnNullInventoryType()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.TransferAllItemsAsync(10, 20, null!));
    }

    // ── GetEntityInventoryAsync ──────────────────────────────────

    [Fact]
    public async Task GetEntityInventoryAsync_SendsSilentCommand()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_FindsEntityAtPosition()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_GetsInventory()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.Contains("get_inventory", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_DefaultsToChest()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.Contains("defines.inventory.chest", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_UsesCustomInventoryType()
    {
        await _service.GetEntityInventoryAsync(10, 20, "fuel");

        Assert.Contains("defines.inventory.fuel", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_GetsContents()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.Contains("get_contents", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_ReportsSlotInfo()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.Contains("\"slots\"", _rcon.LastCommand!);
        Assert.Contains("\"empty_slots\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_ReportsNoEntityError()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.Contains("no_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_ReportsNoInventoryError()
    {
        await _service.GetEntityInventoryAsync(10, 20);

        Assert.Contains("no_inventory", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetEntityInventoryAsync_ThrowsOnNullInventoryType()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetEntityInventoryAsync(10, 20, null!));
    }

    // ── CheckCraftFeasibility ────────────────────────────────────

    [Fact]
    public async Task CheckCraftFeasibilityAsync_SendsSilentCommand()
    {
        await _service.CheckCraftFeasibilityAsync("iron-gear-wheel");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_LooksUpRecipeByName()
    {
        await _service.CheckCraftFeasibilityAsync("electronic-circuit");

        Assert.Contains("force.recipes[\"electronic-circuit\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_ChecksRecipeEnabled()
    {
        await _service.CheckCraftFeasibilityAsync("iron-gear-wheel");

        Assert.Contains("recipe.enabled", _rcon.LastCommand!);
        Assert.Contains("recipe_not_unlocked", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_UsesGetCraftableCount()
    {
        await _service.CheckCraftFeasibilityAsync("iron-gear-wheel");

        Assert.Contains("get_craftable_count", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_ChecksIngredientAvailability()
    {
        await _service.CheckCraftFeasibilityAsync("iron-gear-wheel");

        Assert.Contains("get_item_count", _rcon.LastCommand!);
        Assert.Contains("\"needed\":", _rcon.LastCommand!);
        Assert.Contains("\"available\":", _rcon.LastCommand!);
        Assert.Contains("\"missing\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_OutputsCanCraftAndCraftableCount()
    {
        await _service.CheckCraftFeasibilityAsync("iron-gear-wheel");

        Assert.Contains("\"can_craft\":", _rcon.LastCommand!);
        Assert.Contains("\"craftable_count\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_DefaultCountIsOne()
    {
        await _service.CheckCraftFeasibilityAsync("iron-gear-wheel");

        Assert.Contains("local count = 1", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_FormatsCountWithInvariantCulture()
    {
        await _service.CheckCraftFeasibilityAsync("iron-gear-wheel", 50);

        Assert.Contains("local count = 50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_ReportsUnknownRecipeError()
    {
        await _service.CheckCraftFeasibilityAsync("not-a-recipe");

        Assert.Contains("\"error\":\"unknown_recipe\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_ThrowsOnNullRecipe()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CheckCraftFeasibilityAsync(null!));
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_ThrowsOnWhitespaceRecipe()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CheckCraftFeasibilityAsync("  "));
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_ThrowsOnZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CheckCraftFeasibilityAsync("iron-gear-wheel", 0));
    }

    [Fact]
    public async Task CheckCraftFeasibilityAsync_ThrowsOnNegativeCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CheckCraftFeasibilityAsync("iron-gear-wheel", -1));
    }

    // ── GetFactoryStatus ──────────────────────────────────────────────

    [Fact]
    public async Task GetFactoryStatusAsync_SendsComprehensiveLuaScript()
    {
        await _service.GetFactoryStatusAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IncludesPositionQuery()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
        Assert.Contains("\"position\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IncludesInventoryQuery()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("get_main_inventory", _rcon.LastCommand!);
        Assert.Contains("\"inventory\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IncludesCraftingQueueQuery()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("crafting_queue", _rcon.LastCommand!);
        Assert.Contains("\"crafting_queue\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IncludesResearchQuery()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("current_research", _rcon.LastCommand!);
        Assert.Contains("\"research\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IncludesResourceScan()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("type=\"resource\"", _rcon.LastCommand!);
        Assert.Contains("\"nearby_resources\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IncludesEntityScan()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("\"nearby_entities\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IncludesPowerQuery()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("electric-pole", _rcon.LastCommand!);
        Assert.Contains("\"power\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_UsesDefaultRadii()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("radius=50", _rcon.LastCommand!);
        Assert.Contains("radius=20", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_PassesCustomRadii()
    {
        await _service.GetFactoryStatusAsync(
            resourceScanRadius: 100,
            entityScanRadius: 30,
            electricPoleRadius: 75);

        Assert.Contains("100", _rcon.LastCommand!);
        Assert.Contains("30", _rcon.LastCommand!);
        Assert.Contains("75", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_IsSingleRconCall()
    {
        _rcon.AllCommands.Clear();

        await _service.GetFactoryStatusAsync();

        Assert.Single(_rcon.AllCommands);
    }

    [Fact]
    public async Task GetFactoryStatusAsync_OutputsAllSectionsInSingleJsonObject()
    {
        await _service.GetFactoryStatusAsync();

        Assert.Contains("rcon.print('{'", _rcon.LastCommand!);
        Assert.Contains("pos_json", _rcon.LastCommand!);
        Assert.Contains("inv_json", _rcon.LastCommand!);
        Assert.Contains("craft_json", _rcon.LastCommand!);
        Assert.Contains("research_json", _rcon.LastCommand!);
        Assert.Contains("resources_json", _rcon.LastCommand!);
        Assert.Contains("entities_json", _rcon.LastCommand!);
        Assert.Contains("power_json", _rcon.LastCommand!);
    }

    // ── PreviewInserterPlacement ───────────────────────────────────────

    [Fact]
    public async Task PreviewInserterPlacementAsync_SendsLuaScript()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "north");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_IncludesPosition()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "north");

        Assert.Contains("5", _rcon.LastCommand!);
        Assert.Contains("3", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_IncludesDirection()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "south");

        Assert.Contains("defines.direction.south", _rcon.LastCommand!);
        Assert.Contains("\"south\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_CalculatesPickupAndDropPositions()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "north");

        Assert.Contains("drop_x, drop_y", _rcon.LastCommand!);
        Assert.Contains("pickup_x, pickup_y", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_ScansForEntitiesAtBothPositions()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "north");

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("pickup", _rcon.LastCommand!);
        Assert.Contains("drop", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_ChecksCanPlace()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "north");

        Assert.Contains("can_place_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_OutputsJsonWithAllSections()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "east");

        Assert.Contains("\"inserter_position\":", _rcon.LastCommand!);
        Assert.Contains("\"pickup\":", _rcon.LastCommand!);
        Assert.Contains("\"drop\":", _rcon.LastCommand!);
        Assert.Contains("\"can_place\":", _rcon.LastCommand!);
    }

    [Theory]
    [InlineData("north")]
    [InlineData("south")]
    [InlineData("east")]
    [InlineData("west")]
    public async Task PreviewInserterPlacementAsync_SupportsCardinalDirections(string direction)
    {
        await _service.PreviewInserterPlacementAsync(0, 0, direction);

        Assert.Contains($"defines.direction.{direction}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_IsSingleRconCall()
    {
        _rcon.AllCommands.Clear();

        await _service.PreviewInserterPlacementAsync(5, 3, "north");

        Assert.Single(_rcon.AllCommands);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_ThrowsOnNullDirection()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.PreviewInserterPlacementAsync(0, 0, null!));
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_ThrowsOnWhitespaceDirection()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.PreviewInserterPlacementAsync(0, 0, "  "));
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_UsesDirectionOffsetTable()
    {
        await _service.PreviewInserterPlacementAsync(10, 20, "north");

        Assert.Contains("offsets", _rcon.LastCommand!);
        Assert.Contains("dx", _rcon.LastCommand!);
        Assert.Contains("dy", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PreviewInserterPlacementAsync_FiltersOutPlayerCharacter()
    {
        await _service.PreviewInserterPlacementAsync(5, 3, "north");

        Assert.Contains("\"character\"", _rcon.LastCommand!);
    }
}
