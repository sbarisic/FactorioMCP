using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class RecipeRateCalculatorServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly RecipeRateCalculatorService _service;

    public RecipeRateCalculatorServiceTests()
    {
        _service = new RecipeRateCalculatorService(_rcon);
    }

    // ── AllCommands_UseSilentCommandPrefix ─────────────────────────────

    [Fact]
    public async Task AllCommands_UseSilentCommandPrefix()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 1.0);

        Assert.Single(_rcon.AllCommands);
        Assert.All(_rcon.AllCommands, cmd => Assert.StartsWith("/silent-command ", cmd));
    }

    // ── CalculateProductionRateAsync ─────────────────────────────────

    [Fact]
    public async Task CalculateProductionRateAsync_SendsSilentCommand()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 1.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_LooksUpRecipe()
    {
        await _service.CalculateProductionRateAsync("electronic-circuit", 2.0);

        Assert.Contains("force.recipes[\"electronic-circuit\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_IncludesTargetRate()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 3.5);

        Assert.Contains("3.5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_WithMachineType_LooksUpPrototype()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 1.0, "assembling-machine-2");

        Assert.Contains("assembling-machine-2", _rcon.LastCommand!);
        Assert.Contains("prototypes.entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_WithoutMachineType_UsesDefaults()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 1.0);

        // Should contain the defaults table for auto-detection
        Assert.Contains("assembling-machine-1", _rcon.LastCommand!);
        Assert.Contains("stone-furnace", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_CalculatesMachinesNeeded()
    {
        await _service.CalculateProductionRateAsync("iron-plate", 1.0);

        Assert.Contains("machines_needed", _rcon.LastCommand!);
        Assert.Contains("math.ceil", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_CalculatesInputRates()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 1.0);

        Assert.Contains("inputs_per_second", _rcon.LastCommand!);
        Assert.Contains("per_second_total", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_CalculatesOutputRates()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 1.0);

        Assert.Contains("outputs_per_second", _rcon.LastCommand!);
        Assert.Contains("per_craft", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_IncludesEscFunction()
    {
        await _service.CalculateProductionRateAsync("iron-gear-wheel", 1.0);

        Assert.Contains("local function esc(s)", _rcon.LastCommand!);
    }

    [Fact]
    public async Task CalculateProductionRateAsync_EscapesRecipeName()
    {
        await _service.CalculateProductionRateAsync("recipe-with-\"quotes\"", 1.0);

        Assert.Contains("recipe-with-\\\"quotes\\\"", _rcon.LastCommand!);
    }

    // ── Argument validation ─────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateProductionRateAsync_ThrowsOnInvalidRecipe(string recipe)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CalculateProductionRateAsync(recipe, 1.0));
    }

    [Fact]
    public async Task CalculateProductionRateAsync_ThrowsOnNullRecipe()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CalculateProductionRateAsync(null!, 1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public async Task CalculateProductionRateAsync_ThrowsOnInvalidRate(double rate)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CalculateProductionRateAsync("iron-gear-wheel", rate));
    }
}
