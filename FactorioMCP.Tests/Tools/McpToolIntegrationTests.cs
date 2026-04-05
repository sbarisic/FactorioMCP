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
    private readonly GoalPlannerService _goals;
    private readonly BuildingMemoryService _buildingMemory;
    private readonly MiningService _mining;
    private readonly GameCommandQueue _queue = new();

    public McpToolIntegrationTests()
    {
        _factorio = new FactorioService(_rcon);
        _mining = new MiningService(_rcon);
        _goals = new GoalPlannerService(
            Path.Combine(Path.GetTempPath(), $"goals-mcp-{Guid.NewGuid():N}.json"));
        _buildingMemory = new BuildingMemoryService(
            Path.Combine(Path.GetTempPath(), $"buildings-mcp-{Guid.NewGuid():N}.json"));
    }

    // ── DI Resolution ─────────────────────────────────────────────────
    // Verify all tool/resource classes can be resolved from the same service
    // registrations used in Program.cs, ensuring no missing DI wiring.

    public static TheoryData<Type> AllToolTypes => new()
    {
        typeof(MovementTools),
        typeof(InventoryTools),
        typeof(EntityTools),
        typeof(WorldTools),
        typeof(ResearchTools),
        typeof(LuaTools),
        typeof(RecipeTools),
        typeof(InteractionTools),
        typeof(WaitTools),
        typeof(ChatTools),
        typeof(GoalTools),
        typeof(EnergyTools),
        typeof(BlueprintTools),
        typeof(NavigationTools),
        typeof(BuildingTools),
        typeof(StatusTools),
        typeof(PerceptionTools),
        typeof(BeltTools),
        typeof(BatchTools),
        typeof(FactoryTools),
        typeof(TaskTools),
        typeof(VisionTools),
        typeof(UtilityTools),
        typeof(TargetTools),
        typeof(GameStateResources),
    };

    private static ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<RconClient, CapturingRconClient>();
        services.AddSingleton<FactorioService>();
        services.AddSingleton<PathfindingService>();
        services.AddSingleton<EnergyService>();
        services.AddSingleton<BlueprintService>();
        services.AddSingleton<MiningService>();
        services.AddSingleton<GameCommandQueue>();
        services.AddSingleton<BeltPlannerService>();
        services.AddSingleton<VisionService>();

        var tempPath = Path.Combine(Path.GetTempPath(), $"goals-di-{Guid.NewGuid():N}.json");
        services.AddSingleton(new GoalPlannerService(tempPath));

        var buildingsPath = Path.Combine(Path.GetTempPath(), $"buildings-di-{Guid.NewGuid():N}.json");
        services.AddSingleton(new BuildingMemoryService(buildingsPath));

        return services.BuildServiceProvider();
    }

    [Theory]
    [MemberData(nameof(AllToolTypes))]
    public void AllToolsAndResources_ResolveFromDI(Type toolType)
    {
        using var provider = BuildTestServiceProvider();
        Assert.NotNull(ActivatorUtilities.CreateInstance(provider, toolType));
    }

    // ── StatusTools – Factory Status ─────────────────────────────────

    [Fact]
    public async Task StatusTools_GetFactoryStatus_ReturnsComprehensiveStatus()
    {
        await _goals.SetGoalAsync("Test factory goal", ["Step 1", "Step 2"]);
        var tools = new StatusTools(_factorio, _buildingMemory, _goals, _queue);

        var result = await tools.GetFactoryStatus(
            resourceScanRadius: 100,
            entityScanRadius: 30,
            electricPoleRadius: 75);

        // Lua script was sent to RCON
        Assert.Contains("get_main_inventory", _rcon.LastCommand!);
        Assert.Contains("crafting_queue", _rcon.LastCommand!);
        Assert.Contains("current_research", _rcon.LastCommand!);

        // Custom radii were forwarded
        Assert.Contains("100", _rcon.LastCommand!);
        Assert.Contains("30", _rcon.LastCommand!);
        Assert.Contains("75", _rcon.LastCommand!);

        // Result merges game state with C#-side state
        Assert.Contains("\"building_summary\":", result);
        Assert.Contains("total_buildings", result);
        Assert.Contains("\"active_goal\":", result);
        Assert.Contains("Test factory goal", result);
    }

    // ── EntityTools – Failure Tracking ───────────────────────────────

    [Fact]
    public async Task EntityTools_PlaceEntity_DoesNotTrackOnFailure()
    {
        // CapturingRconClient returns empty string (not a success JSON),
        // so building should not be tracked
        var tools = new EntityTools(_factorio, _mining, _buildingMemory, _queue);

        await tools.PlaceEntity("stone-furnace", 5, 5);

        var summary = await _buildingMemory.GetBuildingSummaryAsync();
        Assert.Contains("\"total_buildings\":0", summary);
    }
}
