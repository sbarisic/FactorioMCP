using FactorioMCP.Models;
using FactorioMCP.Rcon;
using FactorioMCP.Services;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace FactorioMCP.Tests.Integration;

/// <summary>
/// Live integration tests for new production planning and blueprint tools.
/// Requires Factorio running with: --rcon-port 27015 --rcon-password mypassword
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProductionToolsLiveTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly RconClient _rcon = new();
    private RecipeRateCalculatorService _rateCalc = null!;
    private ProductionPlannerService _planner = null!;
    private BlueprintCodecService _codec = null!;
    private LayoutSynthesisService _layout = null!;
    private BlueprintService _blueprints = null!;

    public ProductionToolsLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");
        _rateCalc = new RecipeRateCalculatorService(_rcon);
        _planner = new ProductionPlannerService(_rcon);
        _codec = new BlueprintCodecService();
        _layout = new LayoutSynthesisService();
        _blueprints = new BlueprintService(_rcon);
        _output.WriteLine("✅ RCON connected");
    }

    public async Task DisposeAsync()
    {
        await _rcon.DisposeAsync();
    }

    [Fact]
    public async Task RecipeRateCalculator_IronGearWheel_CalculatesCorrectly()
    {
        var result = await _rateCalc.CalculateProductionRateAsync("iron-gear-wheel", 1.0);
        _output.WriteLine(result);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("machines_needed").GetInt32() > 0);
        Assert.Contains("assembling-machine", doc.RootElement.GetProperty("machine").GetString());
    }

    [Fact]
    public async Task RecipeRateCalculator_IronPlate_DetectsFurnace()
    {
        var result = await _rateCalc.CalculateProductionRateAsync("iron-plate", 1.0);
        _output.WriteLine(result);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("furnace", doc.RootElement.GetProperty("machine").GetString()!);
    }

    [Fact]
    public async Task RecipeRateCalculator_WithMachineOverride_UsesSpecifiedMachine()
    {
        var result = await _rateCalc.CalculateProductionRateAsync("iron-gear-wheel", 2.0, "assembling-machine-2");
        _output.WriteLine(result);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("assembling-machine-2", doc.RootElement.GetProperty("machine").GetString());
    }

    [Fact]
    public void LayoutSynthesis_SmelterLine_GeneratesValidLayout()
    {
        var result = _layout.PlanSmelterLine(0, 0, 4, "stone-furnace", "burner-inserter", "transport-belt");
        _output.WriteLine(result);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("instruction_count").GetInt32() > 0);
    }

    [Fact]
    public void BlueprintCodec_ExportSmelterLine_ProducesValidBlueprint()
    {
        // Generate a smelter line
        var layoutResult = _layout.PlanSmelterLine(0, 0, 3, "stone-furnace", "burner-inserter", "transport-belt");
        using var layoutDoc = JsonDocument.Parse(layoutResult);

        // Extract instructions
        var instructions = new List<PlacementInstruction>();
        foreach (var inst in layoutDoc.RootElement.GetProperty("instructions").EnumerateArray())
        {
            instructions.Add(new PlacementInstruction(
                inst.GetProperty("entity_name").GetString()!,
                inst.GetProperty("x").GetDouble(),
                inst.GetProperty("y").GetDouble(),
                inst.GetProperty("direction").GetString() ?? "north",
                inst.TryGetProperty("role", out var r) ? r.GetString() : null
            ));
        }

        // Export as blueprint
        var exportResult = _codec.ExportAsBlueprint(instructions, "Test Smelter Line");
        _output.WriteLine(exportResult);
        using var exportDoc = JsonDocument.Parse(exportResult);
        Assert.True(exportDoc.RootElement.GetProperty("success").GetBoolean());

        var bpString = exportDoc.RootElement.GetProperty("blueprint_string").GetString()!;
        Assert.StartsWith("0", bpString);

        // Round-trip decode
        var decodeResult = _codec.DecodeBlueprintString(bpString);
        _output.WriteLine(decodeResult);
        using var decodeDoc = JsonDocument.Parse(decodeResult);
        Assert.True(decodeDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Test Smelter Line", decodeDoc.RootElement.GetProperty("label").GetString());
        Assert.Equal(instructions.Count, decodeDoc.RootElement.GetProperty("entity_count").GetInt32());
    }

    [Fact]
    public async Task BlueprintCodec_PlaceExportedBlueprint_InGame()
    {
        // Generate a small smelter line
        var layoutResult = _layout.PlanSmelterLine(0, 0, 2, "stone-furnace", "burner-inserter", "transport-belt");
        using var layoutDoc = JsonDocument.Parse(layoutResult);

        var instructions = new List<PlacementInstruction>();
        foreach (var inst in layoutDoc.RootElement.GetProperty("instructions").EnumerateArray())
        {
            instructions.Add(new PlacementInstruction(
                inst.GetProperty("entity_name").GetString()!,
                inst.GetProperty("x").GetDouble(),
                inst.GetProperty("y").GetDouble(),
                inst.GetProperty("direction").GetString() ?? "north",
                inst.TryGetProperty("role", out var r) ? r.GetString() : null
            ));
        }

        // Export as blueprint string
        var exportResult = _codec.ExportAsBlueprint(instructions, "Integration Test");
        using var exportDoc = JsonDocument.Parse(exportResult);
        var bpString = exportDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        // Get player position
        var posResult = await _rcon.ExecuteLuaAsync(
            "local p = game.connected_players[1].position; rcon.print(string.format('{\"x\":%.1f,\"y\":%.1f}', p.x, p.y))");
        using var posDoc = JsonDocument.Parse(posResult);
        var px = posDoc.RootElement.GetProperty("x").GetDouble();
        var py = posDoc.RootElement.GetProperty("y").GetDouble();
        _output.WriteLine($"Player at ({px}, {py})");

        // Place the blueprint 15 tiles east of player as ghosts
        var placeResult = await _blueprints.PlaceBlueprintStringAsync(bpString, px + 15, py);
        _output.WriteLine($"Place result: {placeResult}");
        using var placeDoc = JsonDocument.Parse(placeResult);
        Assert.True(placeDoc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task ProductionPlanner_IronGearWheel_ReturnsMultiStage()
    {
        var result = await _planner.PlanProductionAsync("iron-gear-wheel", 1.0);
        _output.WriteLine(result);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

        var stages = doc.RootElement.GetProperty("stages");
        Assert.True(stages.GetArrayLength() >= 2); // At least iron-plate + iron-gear-wheel

        // Verify each stage has expected fields
        foreach (var stage in stages.EnumerateArray())
        {
            Assert.True(stage.TryGetProperty("recipe", out _));
            Assert.True(stage.TryGetProperty("machine_type", out _));
            Assert.True(stage.TryGetProperty("machine_count", out _));
            Assert.True(stage.TryGetProperty("belt_tier", out _));
        }
    }

    [Fact]
    public async Task ProductionPlanner_ElectronicCircuit_ShowsComplexChain()
    {
        var result = await _planner.PlanProductionAsync("electronic-circuit", 0.5);
        _output.WriteLine(result);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

        var stages = doc.RootElement.GetProperty("stages");
        // Electronic circuits need copper-cable + iron-plate at minimum
        Assert.True(stages.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task FullWorkflow_PlanAndExportAsBlueprint()
    {
        // 1. Calculate rate for iron plates
        var rateResult = await _rateCalc.CalculateProductionRateAsync("iron-plate", 2.0);
        _output.WriteLine($"Rate calc: {rateResult}");
        using var rateDoc = JsonDocument.Parse(rateResult);
        Assert.True(rateDoc.RootElement.GetProperty("success").GetBoolean());
        var machinesNeeded = rateDoc.RootElement.GetProperty("machines_needed").GetInt32();
        _output.WriteLine($"Need {machinesNeeded} furnaces for 2/s iron plates");

        // 2. Generate smelter line with that many furnaces
        var layoutResult = _layout.PlanSmelterLine(0, 0, machinesNeeded);
        _output.WriteLine($"Layout: {layoutResult}");
        using var layoutDoc = JsonDocument.Parse(layoutResult);

        var instructions = new List<PlacementInstruction>();
        foreach (var inst in layoutDoc.RootElement.GetProperty("instructions").EnumerateArray())
        {
            instructions.Add(new PlacementInstruction(
                inst.GetProperty("entity_name").GetString()!,
                inst.GetProperty("x").GetDouble(),
                inst.GetProperty("y").GetDouble(),
                inst.GetProperty("direction").GetString() ?? "north",
                inst.TryGetProperty("role", out var r) ? r.GetString() : null
            ));
        }

        // 3. Export as blueprint
        var exportResult = _codec.ExportAsBlueprint(instructions, $"{machinesNeeded}x Iron Smelter");
        _output.WriteLine($"Export: {exportResult}");
        using var exportDoc = JsonDocument.Parse(exportResult);
        Assert.True(exportDoc.RootElement.GetProperty("success").GetBoolean());

        // 4. Verify blueprint is decodable
        var bpString = exportDoc.RootElement.GetProperty("blueprint_string").GetString()!;
        var decodeResult = _codec.DecodeBlueprintString(bpString);
        using var decodeDoc = JsonDocument.Parse(decodeResult);
        Assert.True(decodeDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal($"{machinesNeeded}x Iron Smelter", decodeDoc.RootElement.GetProperty("label").GetString());
        _output.WriteLine($"Blueprint has {decodeDoc.RootElement.GetProperty("entity_count")} entities, ready for import!");
    }
}
