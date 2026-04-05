using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class ProductionPlannerServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly ProductionPlannerService _service;

    public ProductionPlannerServiceTests()
    {
        _service = new ProductionPlannerService(_rcon);
    }

    // ── AllCommands_UseSilentCommandPrefix ─────────────────────────────

    [Fact]
    public async Task AllCommands_UseSilentCommandPrefix()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0);

        Assert.Single(_rcon.AllCommands);
        Assert.All(_rcon.AllCommands, cmd => Assert.StartsWith("/silent-command ", cmd));
    }

    // ── PlanProductionAsync ─────────────────────────────────────────

    [Fact]
    public async Task PlanProductionAsync_SendsSilentCommand()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task PlanProductionAsync_ExpandsRecipeTree()
    {
        await _service.PlanProductionAsync("electronic-circuit", 2.0);

        Assert.Contains("recipe_for", _rcon.LastCommand!);
        Assert.Contains("expand", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_IncludesTargetItemAndRate()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 3.5);

        Assert.Contains("iron-gear-wheel", _rcon.LastCommand!);
        Assert.Contains("3.5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_CalculatesMachineCountsPerStage()
    {
        await _service.PlanProductionAsync("iron-plate", 1.0);

        Assert.Contains("machine_count", _rcon.LastCommand!);
        Assert.Contains("math.ceil", _rcon.LastCommand!);
        Assert.Contains("crafting_speed", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_DeterminesBeltTiers()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0);

        Assert.Contains("belt_tier", _rcon.LastCommand!);
        Assert.Contains("transport-belt", _rcon.LastCommand!);
        Assert.Contains("fast-transport-belt", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_LocatesResourcePatches()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("nearest_patch", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_IncludesRawMaterials()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0);

        Assert.Contains("raw_materials", _rcon.LastCommand!);
        Assert.Contains("iron-ore", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_WithMachineOverride()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0, "assembling-machine-3");

        Assert.Contains("assembling-machine-3", _rcon.LastCommand!);
        Assert.Contains("machine_override", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_WithoutMachineOverride_UsesDefaults()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0);

        Assert.Contains("machine_defs", _rcon.LastCommand!);
        Assert.Contains("stone-furnace", _rcon.LastCommand!);
        Assert.Contains("assembling-machine-1", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_IncludesEscFunction()
    {
        await _service.PlanProductionAsync("iron-gear-wheel", 1.0);

        Assert.Contains("local function esc(s)", _rcon.LastCommand!);
    }

    [Fact]
    public async Task PlanProductionAsync_EscapesTargetItem()
    {
        await _service.PlanProductionAsync("item-with-\"quotes\"", 1.0);

        Assert.Contains("item-with-\\\"quotes\\\"", _rcon.LastCommand!);
    }

    // ── Argument validation ─────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlanProductionAsync_ThrowsOnInvalidItem(string item)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.PlanProductionAsync(item, 1.0));
    }

    [Fact]
    public async Task PlanProductionAsync_ThrowsOnNullItem()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.PlanProductionAsync(null!, 1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public async Task PlanProductionAsync_ThrowsOnInvalidRate(double rate)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.PlanProductionAsync("iron-gear-wheel", rate));
    }
}
