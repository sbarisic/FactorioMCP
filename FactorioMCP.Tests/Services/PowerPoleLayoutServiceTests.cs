using FactorioMCP.Models;
using FactorioMCP.Services;
using System.Text.Json;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class PowerPoleLayoutServiceTests
{
    private readonly PowerPoleLayoutService _service = new();

    [Fact]
    public void PlanPowerPoles_NoEntities_ReturnsError()
    {
        var entities = new List<PlacementInstruction>();

        var result = _service.PlanPowerPoles(entities);
        var json = JsonDocument.Parse(result);

        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("no_entities", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void PlanPowerPoles_OnlyPassiveEntities_ReturnsZeroPoles()
    {
        var entities = new List<PlacementInstruction>
        {
            new("transport-belt", 0, 0, "north"),
            new("wooden-chest", 5, 5, "north"),
            new("stone-furnace", 10, 10, "north"),
        };

        var result = _service.PlanPowerPoles(entities);
        var json = JsonDocument.Parse(result);

        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("pole_count").GetInt32());
    }

    [Fact]
    public void PlanPowerPoles_SingleAssembler_ReturnsOnePole()
    {
        var entities = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 5, 5, "north"),
        };

        var result = _service.PlanPowerPoles(entities);
        var json = JsonDocument.Parse(result);

        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.True(json.RootElement.GetProperty("pole_count").GetInt32() >= 1);
        Assert.Equal(0, json.RootElement.GetProperty("uncovered_count").GetInt32());

        var instructions = json.RootElement.GetProperty("instructions");
        Assert.True(instructions.GetArrayLength() >= 1);
        Assert.Equal("small-electric-pole", instructions[0].GetProperty("entity_name").GetString());
    }

    [Fact]
    public void PlanPowerPoles_SpreadEntities_CoverAll()
    {
        // Place entities far enough apart to need multiple poles
        var entities = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 0, 0, "north"),
            new("assembling-machine-1", 10, 0, "north"),
            new("assembling-machine-1", 20, 0, "north"),
        };

        var result = _service.PlanPowerPoles(entities);
        var json = JsonDocument.Parse(result);

        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.True(json.RootElement.GetProperty("pole_count").GetInt32() >= 2);
        Assert.Equal(0, json.RootElement.GetProperty("uncovered_count").GetInt32());
    }

    [Fact]
    public void PlanPowerPoles_UnknownPole_ReturnsError()
    {
        var entities = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 0, 0, "north"),
        };

        var result = _service.PlanPowerPoles(entities, "nonexistent-pole");
        var json = JsonDocument.Parse(result);

        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("unknown_pole", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void PlanPowerPoles_MediumPoles_LargerSpacing()
    {
        var entities = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 0, 0, "north"),
            new("assembling-machine-1", 8, 0, "north"),
        };

        var smallResult = _service.PlanPowerPoles(entities, "small-electric-pole");
        var mediumResult = _service.PlanPowerPoles(entities, "medium-electric-pole");

        var smallJson = JsonDocument.Parse(smallResult);
        var mediumJson = JsonDocument.Parse(mediumResult);

        int smallPoles = smallJson.RootElement.GetProperty("pole_count").GetInt32();
        int mediumPoles = mediumJson.RootElement.GetProperty("pole_count").GetInt32();

        // Medium poles have larger supply area, so should need same or fewer poles
        Assert.True(mediumPoles <= smallPoles,
            $"Medium poles ({mediumPoles}) should need <= small poles ({smallPoles})");
    }

    [Fact]
    public void PlanPowerPoles_WithExistingPole_AlignsGrid()
    {
        var entities = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 3, 3, "north"),
        };

        var withoutAlign = _service.PlanPowerPoles(entities, "small-electric-pole");
        var withAlign = _service.PlanPowerPoles(entities, "small-electric-pole", 0, 0);

        var withoutJson = JsonDocument.Parse(withoutAlign);
        var withJson = JsonDocument.Parse(withAlign);

        // Both should succeed and cover all entities
        Assert.True(withoutJson.RootElement.GetProperty("success").GetBoolean());
        Assert.True(withJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(0, withoutJson.RootElement.GetProperty("uncovered_count").GetInt32());
        Assert.Equal(0, withJson.RootElement.GetProperty("uncovered_count").GetInt32());
    }

    [Fact]
    public void PlanPowerPoles_MixedEntities_FiltersCorrectly()
    {
        var entities = new List<PlacementInstruction>
        {
            new("transport-belt", 0, 0, "north"),        // no power
            new("assembling-machine-1", 2, 0, "north"),  // needs power
            new("burner-inserter", 4, 0, "north"),       // no power (burner)
            new("inserter", 6, 0, "north"),              // needs power
            new("stone-furnace", 8, 0, "north"),         // no power (burner)
            new("electric-furnace", 10, 0, "north"),     // needs power
        };

        var result = _service.PlanPowerPoles(entities);
        var json = JsonDocument.Parse(result);

        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(3, json.RootElement.GetProperty("entities_needing_power").GetInt32());
    }

    [Fact]
    public void PlanPowerPoles_SubstationCoversLargeArea()
    {
        // With 18x18 supply area, substation covers a very large region
        var entities = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 0, 0, "north"),
            new("assembling-machine-1", 5, 5, "north"),
            new("assembling-machine-1", -5, -5, "north"),
        };

        var result = _service.PlanPowerPoles(entities, "substation");
        var json = JsonDocument.Parse(result);

        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        // Substation with 18x18 area should cover all 3 in a ~10x10 area with 1 pole
        Assert.Equal(1, json.RootElement.GetProperty("pole_count").GetInt32());
    }

    [Fact]
    public void PlanPowerPoles_AllInstructionsArePoles()
    {
        var entities = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 0, 0, "north"),
            new("assembling-machine-1", 10, 10, "north"),
        };

        var result = _service.PlanPowerPoles(entities);
        var json = JsonDocument.Parse(result);

        var instructions = json.RootElement.GetProperty("instructions");
        for (int i = 0; i < instructions.GetArrayLength(); i++)
        {
            Assert.Equal("small-electric-pole", instructions[i].GetProperty("entity_name").GetString());
            Assert.Equal("power_pole", instructions[i].GetProperty("role").GetString());
        }
    }
}
