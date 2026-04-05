using System.Text.Json;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class LayoutSynthesisServiceTests
{
    private readonly LayoutSynthesisService _service = new();

    private static JsonElement Parse(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }

    // ── Basic layout generation ────────────────────────────────────

    [Fact]
    public void PlanSmelterLine_ReturnsSuccess()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 2));

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(2, result.GetProperty("furnace_count").GetInt32());
    }

    [Fact]
    public void PlanSmelterLine_SingleFurnace_GeneratesCorrectEntities()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 1));
        var instructions = result.GetProperty("instructions");

        // Should have: input belt, inbound inserter, furnace, outbound inserter, output belt = 5
        Assert.Equal(5, instructions.GetArrayLength());

        // Verify roles
        var roles = new List<string>();
        for (int i = 0; i < instructions.GetArrayLength(); i++)
            roles.Add(instructions[i].GetProperty("role").GetString()!);

        Assert.Contains("input_belt", roles);
        Assert.Contains("inbound_inserter", roles);
        Assert.Contains("furnace", roles);
        Assert.Contains("outbound_inserter", roles);
        Assert.Contains("output_belt", roles);
    }

    [Fact]
    public void PlanSmelterLine_UsesCorrectEntityNames()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 1, "steel-furnace", "inserter", "fast-transport-belt"));
        var instructions = result.GetProperty("instructions");

        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            var inst = instructions[i];
            var role = inst.GetProperty("role").GetString();
            var name = inst.GetProperty("entity_name").GetString();

            if (role == "furnace") Assert.Equal("steel-furnace", name);
            else if (role!.Contains("inserter")) Assert.Equal("inserter", name);
            else if (role.Contains("belt")) Assert.Equal("fast-transport-belt", name);
        }
    }

    [Fact]
    public void PlanSmelterLine_MultipleFurnaces_StacksVertically()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 3));
        var instructions = result.GetProperty("instructions");

        // Collect furnace Y positions
        var furnaceYPositions = new List<double>();
        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            var inst = instructions[i];
            if (inst.GetProperty("role").GetString() == "furnace")
                furnaceYPositions.Add(inst.GetProperty("y").GetDouble());
        }

        Assert.Equal(3, furnaceYPositions.Count);
        // Each stone-furnace is 2x2, with 1-tile gap between = 3 tiles apart
        Assert.True(furnaceYPositions[1] > furnaceYPositions[0]);
        Assert.True(furnaceYPositions[2] > furnaceYPositions[1]);
    }

    [Fact]
    public void PlanSmelterLine_InputBeltIsWestOfFurnace()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 1));
        var instructions = result.GetProperty("instructions");

        double furnaceX = 0, beltX = 0;
        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            var inst = instructions[i];
            var role = inst.GetProperty("role").GetString();
            if (role == "furnace") furnaceX = inst.GetProperty("x").GetDouble();
            if (role == "input_belt") beltX = inst.GetProperty("x").GetDouble();
        }

        Assert.True(beltX < furnaceX, "Input belt should be west (left) of furnace");
    }

    [Fact]
    public void PlanSmelterLine_OutputBeltIsEastOfFurnace()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 1));
        var instructions = result.GetProperty("instructions");

        double furnaceX = 0, beltX = 0;
        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            var inst = instructions[i];
            var role = inst.GetProperty("role").GetString();
            if (role == "furnace") furnaceX = inst.GetProperty("x").GetDouble();
            if (role == "output_belt") beltX = inst.GetProperty("x").GetDouble();
        }

        Assert.True(beltX > furnaceX, "Output belt should be east (right) of furnace");
    }

    [Fact]
    public void PlanSmelterLine_ReturnsInstructionCount()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 2));

        var count = result.GetProperty("instruction_count").GetInt32();
        var actualCount = result.GetProperty("instructions").GetArrayLength();
        Assert.Equal(count, actualCount);
    }

    [Fact]
    public void PlanSmelterLine_IncludesMetadata()
    {
        var result = Parse(_service.PlanSmelterLine(5, 10, 3, "steel-furnace", "inserter", "fast-transport-belt"));

        Assert.Equal("steel-furnace", result.GetProperty("furnace_name").GetString());
        Assert.Equal("inserter", result.GetProperty("inserter_name").GetString());
        Assert.Equal("fast-transport-belt", result.GetProperty("belt_name").GetString());
        Assert.Equal(5, result.GetProperty("origin_x").GetDouble());
        Assert.Equal(10, result.GetProperty("origin_y").GetDouble());
    }

    [Fact]
    public void PlanSmelterLine_BurnerInserters_NoPowerPoles()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 8, inserterName: "burner-inserter"));
        var instructions = result.GetProperty("instructions");

        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            var role = instructions[i].GetProperty("role").GetString();
            Assert.NotEqual("power_pole", role);
        }
    }

    [Fact]
    public void PlanSmelterLine_ElectricInserters_AddsPowerPoles()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 8, inserterName: "inserter"));
        var instructions = result.GetProperty("instructions");

        bool hasPole = false;
        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            if (instructions[i].GetProperty("role").GetString() == "power_pole")
            {
                hasPole = true;
                Assert.Equal("small-electric-pole", instructions[i].GetProperty("entity_name").GetString());
            }
        }
        Assert.True(hasPole);
    }

    // ── Argument validation ────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PlanSmelterLine_ThrowsOnInvalidFurnaceCount(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.PlanSmelterLine(0, 0, count));
    }

    [Fact]
    public void PlanSmelterLine_AllInstructionsHaveDirections()
    {
        var result = Parse(_service.PlanSmelterLine(0, 0, 3));
        var instructions = result.GetProperty("instructions");

        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            var dir = instructions[i].GetProperty("direction").GetString();
            Assert.NotNull(dir);
            Assert.NotEmpty(dir);
        }
    }
}
