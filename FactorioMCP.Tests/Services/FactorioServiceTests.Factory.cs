using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task FactoryCommands_GenerateCorrectLua()
    {
        // PlaceEntitySmartAsync
        await _service.PlaceEntitySmartAsync("stone-furnace", 10, 20);
        var placeCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", placeCmd);
        Assert.Contains("can_place_entity", placeCmd);
        Assert.Contains("stone-furnace", placeCmd);
        Assert.Contains("10", placeCmd);
        Assert.Contains("20", placeCmd);

        // FindUnpoweredEntitiesAsync
        await _service.FindUnpoweredEntitiesAsync(30);
        var unpoweredCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", unpoweredCmd);
        Assert.Contains("no_power", unpoweredCmd);
        Assert.Contains("30", unpoweredCmd);

        // FindIdleMachinesAsync
        await _service.FindIdleMachinesAsync(25);
        var idleCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", idleCmd);
        Assert.Contains("25", idleCmd);

        // FindMissingInputsAsync
        await _service.FindMissingInputsAsync(5, 10);
        var missingCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", missingCmd);
        Assert.Contains("5", missingCmd);
        Assert.Contains("10", missingCmd);
        Assert.Contains("furnace_source", missingCmd);

        // PickupItemsAsync
        await _service.PickupItemsAsync(15);
        var pickupCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", pickupCmd);
        Assert.Contains("item-entity", pickupCmd);
        Assert.Contains("15", pickupCmd);

        // PlanCraftAsync
        await _service.PlanCraftAsync("iron-gear-wheel", 10);
        var planCmd = _rcon.LastCommand!;
        Assert.StartsWith("/silent-command", planCmd);
        Assert.Contains("iron-gear-wheel", planCmd);
        Assert.Contains("recipe_for", planCmd);
    }

    [Theory]
    [InlineData("PlaceEntitySmart_EmptyName", "", 0, 0, 5)]
    [InlineData("PlaceEntitySmart_ZeroRadius", "stone-furnace", 0, 0, 0)]
    [InlineData("PlanCraft_EmptyName", "", 0, 0, -1)]
    [InlineData("PickupItems_ZeroRadius", "pickup", 0, 0, -2)]
    public async Task FactoryValidation_ThrowsOnInvalidInput(string scenario, string name, double x, double y, int extra)
    {
        switch (scenario)
        {
            case "PlaceEntitySmart_EmptyName":
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    _service.PlaceEntitySmartAsync(name, x, y));
                break;
            case "PlaceEntitySmart_ZeroRadius":
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    _service.PlaceEntitySmartAsync(name, x, y, searchRadius: extra));
                break;
            case "PlanCraft_EmptyName":
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    _service.PlanCraftAsync(name));
                break;
            case "PickupItems_ZeroRadius":
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    _service.PickupItemsAsync(0));
                break;
        }
    }
}
