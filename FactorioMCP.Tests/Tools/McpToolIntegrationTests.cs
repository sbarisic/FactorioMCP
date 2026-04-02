using FactorioMCP.Rcon;
using FactorioMCP.Resources;
using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class McpToolIntegrationTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly FactorioService _factorio;
    private readonly EnergyService _energy;
    private readonly BlueprintService _blueprints;
    private readonly GoalPlannerService _goals;
    private readonly BuildingMemoryService _buildingMemory;
    private readonly GameCommandQueue _queue = new();

    public McpToolIntegrationTests()
    {
        _factorio = new FactorioService(_rcon);
        _energy = new EnergyService(_rcon);
        _blueprints = new BlueprintService(_rcon);
        _goals = new GoalPlannerService(
            Path.Combine(Path.GetTempPath(), $"goals-mcp-{Guid.NewGuid():N}.json"));
        _buildingMemory = new BuildingMemoryService(
            Path.Combine(Path.GetTempPath(), $"buildings-mcp-{Guid.NewGuid():N}.json"));
    }

    // ── DI Resolution ─────────────────────────────────────────────────
    // Verify all tool classes can be resolved from the same service registrations
    // used in Program.cs, ensuring no missing DI wiring.

    private static ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<RconClient, CapturingRconClient>();
        services.AddSingleton<FactorioService>();
        services.AddSingleton<EnergyService>();
        services.AddSingleton<BlueprintService>();
        services.AddSingleton<GameCommandQueue>();

        var tempPath = Path.Combine(Path.GetTempPath(), $"goals-di-{Guid.NewGuid():N}.json");
        services.AddSingleton(new GoalPlannerService(tempPath));

        var buildingsPath = Path.Combine(Path.GetTempPath(), $"buildings-di-{Guid.NewGuid():N}.json");
        services.AddSingleton(new BuildingMemoryService(buildingsPath));

        return services.BuildServiceProvider();
    }

    [Fact]
    public void MovementTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<MovementTools>(provider));
    }

    [Fact]
    public void InventoryTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<InventoryTools>(provider));
    }

    [Fact]
    public void EntityTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<EntityTools>(provider));
    }

    [Fact]
    public void WorldTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<WorldTools>(provider));
    }

    [Fact]
    public void ResearchTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<ResearchTools>(provider));
    }

    [Fact]
    public void LuaTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<LuaTools>(provider));
    }

    [Fact]
    public void RecipeTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<RecipeTools>(provider));
    }

    [Fact]
    public void InteractionTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<InteractionTools>(provider));
    }

    [Fact]
    public void WaitTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<WaitTools>(provider));
    }

    [Fact]
    public void ChatTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<ChatTools>(provider));
    }

    [Fact]
    public void GoalTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<GoalTools>(provider));
    }

    [Fact]
    public void EnergyTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<EnergyTools>(provider));
    }

    [Fact]
    public void BlueprintTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<BlueprintTools>(provider));
    }

    // ── MovementTools Delegation ──────────────────────────────────────

    [Fact]
    public async Task MovementTools_GetPlayerPosition_DelegatesToFactorioService()
    {
        var tools = new MovementTools(_factorio, _queue);

        await tools.GetPlayerPosition();

        Assert.Contains("position", _rcon.LastCommand!);
        Assert.Contains("game.connected_players[1]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task MovementTools_StopWalking_DelegatesToFactorioService()
    {
        var tools = new MovementTools(_factorio, _queue);

        var result = await tools.StopWalking();

        Assert.Contains("walking", _rcon.LastCommand!);
        Assert.Equal("Stopped walking.", result);
    }

    // ── InventoryTools Delegation ─────────────────────────────────────

    [Fact]
    public async Task InventoryTools_GetInventory_DelegatesToFactorioService()
    {
        var tools = new InventoryTools(_factorio, _queue);

        await tools.GetInventory();

        Assert.Contains("get_main_inventory", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InventoryTools_Craft_PassesRecipeAndCount()
    {
        var tools = new InventoryTools(_factorio, _queue);

        await tools.Craft("iron-gear-wheel", 5);

        Assert.Contains("iron-gear-wheel", _rcon.LastCommand!);
        Assert.Contains("5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InventoryTools_GetCraftingQueue_DelegatesToFactorioService()
    {
        var tools = new InventoryTools(_factorio, _queue);

        await tools.GetCraftingQueue();

        Assert.Contains("crafting_queue", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InventoryTools_DropItems_DelegatesToFactorioService()
    {
        var tools = new InventoryTools(_factorio, _queue);

        await tools.DropItems("iron-plate", 10);

        Assert.Contains("spill_item_stack", _rcon.LastCommand!);
        Assert.Contains("iron-plate", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InventoryTools_TransferAllItems_DelegatesToFactorioService()
    {
        var tools = new InventoryTools(_factorio, _queue);

        await tools.TransferAllItems(5, 10, "furnace_result");

        Assert.Contains("get_inventory", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.furnace_result", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InventoryTools_GetEntityInventory_DelegatesToFactorioService()
    {
        var tools = new InventoryTools(_factorio, _queue);

        await tools.GetEntityInventory(5, 10, "fuel");

        Assert.Contains("get_contents", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.fuel", _rcon.LastCommand!);
    }

    // ── EntityTools Delegation ────────────────────────────────────────

    [Fact]
    public async Task EntityTools_PlaceEntity_PassesAllParameters()
    {
        var tools = new EntityTools(_factorio, _buildingMemory, _queue);

        await tools.PlaceEntity("stone-furnace", 10.5, -3.2, "south");

        Assert.Contains("stone-furnace", _rcon.LastCommand!);
        Assert.Contains("10.5", _rcon.LastCommand!);
        Assert.Contains("-3.2", _rcon.LastCommand!);
        Assert.Contains("south", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EntityTools_MineEntity_PassesCoordinates()
    {
        var tools = new EntityTools(_factorio, _buildingMemory, _queue);

        await tools.MineEntity(5, -2);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    // ── WorldTools Delegation ─────────────────────────────────────────

    [Fact]
    public async Task WorldTools_GetNearbyEntities_PassesRadius()
    {
        var tools = new WorldTools(_factorio, _queue);

        await tools.GetNearbyEntities(25);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("25", _rcon.LastCommand!);
    }

    [Fact]
    public async Task WorldTools_CheckDistance_PassesCoordinates()
    {
        var tools = new WorldTools(_factorio, _queue);

        await tools.CheckDistance(100, -50);

        Assert.Contains("100", _rcon.LastCommand!);
        Assert.Contains("-50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task WorldTools_ScanResources_PassesRadius()
    {
        var tools = new WorldTools(_factorio, _queue);

        await tools.ScanResources(100);

        Assert.Contains("resource", _rcon.LastCommand!);
        Assert.Contains("100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task WorldTools_ScanTiles_PassesRadius()
    {
        var tools = new WorldTools(_factorio, _queue);

        await tools.ScanTiles(32);

        Assert.Contains("find_tiles_filtered", _rcon.LastCommand!);
        Assert.Contains("32", _rcon.LastCommand!);
    }

    // ── InteractionTools Delegation ───────────────────────────────────

    [Fact]
    public async Task InteractionTools_InsertItems_PassesAllParameters()
    {
        var tools = new InteractionTools(_factorio, _queue);

        await tools.InsertItems(5, -2, "coal", 10, "fuel");

        Assert.Contains("coal", _rcon.LastCommand!);
        Assert.Contains("10", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InteractionTools_RemoveItems_PassesAllParameters()
    {
        var tools = new InteractionTools(_factorio, _queue);

        await tools.RemoveItems(5, -2, "iron-plate", 20, "furnace_result");

        Assert.Contains("iron-plate", _rcon.LastCommand!);
        Assert.Contains("20", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InteractionTools_InspectEntity_PassesCoordinates()
    {
        var tools = new InteractionTools(_factorio, _queue);

        await tools.InspectEntity(10, 20);

        Assert.Contains("10", _rcon.LastCommand!);
        Assert.Contains("20", _rcon.LastCommand!);
    }

    // ── ChatTools Delegation ──────────────────────────────────────────

    [Fact]
    public async Task ChatTools_InitializeChatListener_DelegatesToFactorioService()
    {
        var tools = new ChatTools(_factorio, _queue);

        await tools.InitializeChatListener();

        Assert.Contains("on_console_chat", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ChatTools_GetChatMessages_PassesSinceTick()
    {
        var tools = new ChatTools(_factorio, _queue);

        await tools.GetChatMessages(sinceTick: 500);

        Assert.Contains("chat_log", _rcon.LastCommand!);
        Assert.Contains("500", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ChatTools_SendChatMessage_PassesMessage()
    {
        var tools = new ChatTools(_factorio, _queue);

        await tools.SendChatMessage("Hello from AI");

        Assert.Contains("Hello from AI", _rcon.LastCommand!);
        Assert.Contains("[AI]", _rcon.LastCommand!);
    }

    // ── GoalTools Delegation ──────────────────────────────────────────

    [Fact]
    public async Task GoalTools_SetGoal_DelegatesToGoalPlannerService()
    {
        var tools = new GoalTools(_goals);

        var result = await tools.SetGoal("Build iron smelting");

        Assert.Contains("created", result);
        Assert.Contains("Build iron smelting", result);
    }

    [Fact]
    public async Task GoalTools_SetGoal_PassesSteps()
    {
        var tools = new GoalTools(_goals);

        var result = await tools.SetGoal("Smelt iron", ["Mine ore", "Build furnace"]);

        Assert.Contains("step_count", result);
    }

    [Fact]
    public async Task GoalTools_GetActiveGoal_DelegatesToGoalPlannerService()
    {
        var tools = new GoalTools(_goals);
        await tools.SetGoal("Test goal");

        var result = await tools.GetActiveGoal();

        Assert.Contains("Test goal", result);
    }

    [Fact]
    public async Task GoalTools_GetAllGoals_DelegatesToGoalPlannerService()
    {
        var tools = new GoalTools(_goals);
        await tools.SetGoal("Goal A");

        var result = await tools.GetAllGoals();

        Assert.Contains("Goal A", result);
    }

    // ── EnergyTools Delegation ────────────────────────────────────────

    [Fact]
    public async Task EnergyTools_GetElectricNetwork_PassesRadius()
    {
        var tools = new EnergyTools(_energy, _queue);

        await tools.GetElectricNetwork(radius: 75);

        Assert.Contains("electric-pole", _rcon.LastCommand!);
        Assert.Contains("75", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EnergyTools_GetElectricNetwork_UsesDefaultRadius()
    {
        var tools = new EnergyTools(_energy, _queue);

        await tools.GetElectricNetwork();

        Assert.Contains("radius=50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task EnergyTools_InspectEntityPower_PassesCoordinates()
    {
        var tools = new EnergyTools(_energy, _queue);

        await tools.InspectEntityPower(12.5, -7.3);

        Assert.Contains("12.5", _rcon.LastCommand!);
        Assert.Contains("-7.3", _rcon.LastCommand!);
        Assert.Contains("is_connected_to_electric_network", _rcon.LastCommand!);
    }

    // ── ResearchTools Delegation ───────────────────────────────────────

    [Fact]
    public async Task ResearchTools_GetResearchStatus_DelegatesToFactorioService()
    {
        var tools = new ResearchTools(_factorio, _queue);

        await tools.GetResearchStatus();

        Assert.Contains("current_research", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ResearchTools_GetAvailableTechnologies_DelegatesToFactorioService()
    {
        var tools = new ResearchTools(_factorio, _queue);

        await tools.GetAvailableTechnologies();

        Assert.Contains("force.technologies", _rcon.LastCommand!);
        Assert.Contains("research_unit_count", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ResearchTools_StartResearch_PassesTechnology()
    {
        var tools = new ResearchTools(_factorio, _queue);

        await tools.StartResearch("automation");

        Assert.Contains("automation", _rcon.LastCommand!);
        Assert.Contains("add_research", _rcon.LastCommand!);
    }

    // ── LuaTools Delegation ───────────────────────────────────────────

    [Fact]
    public async Task LuaTools_ExecuteLua_DelegatesToFactorioService()
    {
        var tools = new LuaTools(_factorio, _queue);

        await tools.ExecuteLua("rcon.print('hello')");

        Assert.Equal("/silent-command rcon.print('hello')", _rcon.LastCommand!);
    }

    // ── RecipeTools Delegation ────────────────────────────────────────

    [Fact]
    public async Task RecipeTools_GetRecipeDetails_PassesRecipeName()
    {
        var tools = new RecipeTools(_factorio, _queue);

        await tools.GetRecipeDetails("iron-gear-wheel");

        Assert.Contains("iron-gear-wheel", _rcon.LastCommand!);
        Assert.Contains("force.recipes", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RecipeTools_GetAvailableRecipes_DelegatesToFactorioService()
    {
        var tools = new RecipeTools(_factorio, _queue);

        await tools.GetAvailableRecipes();

        Assert.Contains("force.recipes", _rcon.LastCommand!);
        Assert.Contains("recipe.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RecipeTools_GetTechnologyDetails_PassesTechnologyName()
    {
        var tools = new RecipeTools(_factorio, _queue);

        await tools.GetTechnologyDetails("automation");

        Assert.Contains("automation", _rcon.LastCommand!);
        Assert.Contains("force.technologies", _rcon.LastCommand!);
        Assert.Contains("tech.effects", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RecipeTools_CheckCraftFeasibility_PassesRecipeAndCount()
    {
        var tools = new RecipeTools(_factorio, _queue);

        await tools.CheckCraftFeasibility("transport-belt", 10);

        Assert.Contains("transport-belt", _rcon.LastCommand!);
        Assert.Contains("get_craftable_count", _rcon.LastCommand!);
        Assert.Contains("10", _rcon.LastCommand!);
    }

    // ── BuildingTools DI Resolution ───────────────────────────────────

    [Fact]
    public void BuildingTools_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<BuildingTools>(provider));
    }

    // ── BuildingTools Delegation ──────────────────────────────────────

    [Fact]
    public async Task BuildingTools_GetAllBuildings_DelegatesToBuildingMemoryService()
    {
        var tools = new BuildingTools(_buildingMemory);

        var result = await tools.GetAllBuildings();

        Assert.Contains("ok", result);
        Assert.Contains("count", result);
    }

    [Fact]
    public async Task BuildingTools_GetBuildingsNear_PassesParameters()
    {
        var tools = new BuildingTools(_buildingMemory);

        var result = await tools.GetBuildingsNear(10, -5, 30);

        Assert.Contains("ok", result);
        Assert.Contains("10", result);
        Assert.Contains("-5", result);
    }

    [Fact]
    public async Task BuildingTools_FindBuildingsByType_PassesEntityName()
    {
        var tools = new BuildingTools(_buildingMemory);

        var result = await tools.FindBuildingsByType("stone-furnace");

        Assert.Contains("ok", result);
        Assert.Contains("stone-furnace", result);
    }

    [Fact]
    public async Task BuildingTools_GetBuildingSummary_DelegatesToBuildingMemoryService()
    {
        var tools = new BuildingTools(_buildingMemory);

        var result = await tools.GetBuildingSummary();

        Assert.Contains("ok", result);
        Assert.Contains("total_buildings", result);
    }

    // ── BlueprintTools Delegation ─────────────────────────────────────

    [Fact]
    public async Task BlueprintTools_PlaceGhostEntity_DelegatesToBlueprintService()
    {
        var tools = new BlueprintTools(_blueprints, _queue);

        await tools.PlaceGhostEntity("stone-furnace", 10, 20);

        Assert.Contains("entity-ghost", _rcon.LastCommand!);
        Assert.Contains("stone-furnace", _rcon.LastCommand!);
    }

    [Fact]
    public async Task BlueprintTools_PlaceBlueprintString_DelegatesToBlueprintService()
    {
        var tools = new BlueprintTools(_blueprints, _queue);

        await tools.PlaceBlueprintString("0eNqFake", 10, 20);

        Assert.Contains("import_stack", _rcon.LastCommand!);
        Assert.Contains("build_from_cursor", _rcon.LastCommand!);
    }

    [Fact]
    public async Task BlueprintTools_GetGhostEntities_DelegatesToBlueprintService()
    {
        var tools = new BlueprintTools(_blueprints, _queue);

        await tools.GetGhostEntities();

        Assert.Contains("entity-ghost", _rcon.LastCommand!);
        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
    }

    [Fact]
    public async Task BlueprintTools_CreateBlueprintFromArea_DelegatesToBlueprintService()
    {
        var tools = new BlueprintTools(_blueprints, _queue);

        await tools.CreateBlueprintFromArea(-10, -10, 10, 10);

        Assert.Contains("create_blueprint", _rcon.LastCommand!);
        Assert.Contains("export_stack", _rcon.LastCommand!);
    }

    [Fact]
    public async Task BlueprintTools_RevokeGhostEntity_DelegatesToBlueprintService()
    {
        var tools = new BlueprintTools(_blueprints, _queue);

        await tools.RevokeGhostEntity(10, 20);

        Assert.Contains("entity-ghost", _rcon.LastCommand!);
        Assert.Contains("destroy", _rcon.LastCommand!);
    }

    // ── EntityTools Auto-Tracking ─────────────────────────────────────

    [Fact]
    public async Task EntityTools_PlaceEntity_DoesNotTrackOnFailure()
    {
        // CapturingRconClient returns empty string (not a success JSON),
        // so building should not be tracked
        var tools = new EntityTools(_factorio, _buildingMemory, _queue);

        await tools.PlaceEntity("stone-furnace", 5, 5);

        var summary = await _buildingMemory.GetBuildingSummaryAsync();
        Assert.Contains("\"total_buildings\":0", summary);
    }

    // ── Resource DI Resolution ────────────────────────────────────────

    [Fact]
    public void GameStateResources_ResolvesFromDI()
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance<GameStateResources>(provider));
    }

    // ── Resource Delegation ───────────────────────────────────────────

    [Fact]
    public async Task GameStateResources_GetPlayerPosition_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetPlayerPosition();
        Assert.Contains("game.connected_players", _rcon.LastCommand);
        Assert.Contains("position", _rcon.LastCommand);
    }

    [Fact]
    public async Task GameStateResources_GetPlayerInventory_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetPlayerInventory();
        Assert.Contains("get_main_inventory", _rcon.LastCommand);
    }

    [Fact]
    public async Task GameStateResources_GetCraftingQueue_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetCraftingQueue();
        Assert.Contains("crafting_queue", _rcon.LastCommand);
    }

    [Fact]
    public async Task GameStateResources_GetResearchStatus_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetResearchStatus();
        Assert.Contains("current_research", _rcon.LastCommand);
    }

    [Fact]
    public async Task GameStateResources_GetAvailableRecipes_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetAvailableRecipes();
        Assert.Contains("recipes", _rcon.LastCommand);
    }

    [Fact]
    public async Task GameStateResources_GetElectricNetwork_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetElectricNetwork();
        Assert.Contains("electric", _rcon.LastCommand);
    }

    [Fact]
    public async Task GameStateResources_GetGameTick_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetGameTick();
        Assert.Contains("game.tick", _rcon.LastCommand);
    }

    [Fact]
    public async Task GameStateResources_GetAvailableTechnologies_DelegatesToService()
    {
        var resources = new GameStateResources(_factorio, _energy);
        await resources.GetAvailableTechnologies();
        Assert.Contains("technologies", _rcon.LastCommand);
    }
}
