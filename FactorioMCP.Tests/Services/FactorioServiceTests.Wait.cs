using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
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

    // ── WaitForItemCount ─────────────────────────────────────────────

    [Fact]
    public async Task WaitForItemCountAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.WaitForItemCountAsync(null!, 10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForItemCountAsync_ThrowsOnEmptyItemName()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.WaitForItemCountAsync("", 10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForItemCountAsync_ThrowsOnZeroTargetCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForItemCountAsync("iron-plate", 0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForItemCountAsync_ThrowsOnZeroPollInterval()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForItemCountAsync("iron-plate", 10, TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForItemCountAsync_ThrowsOnZeroTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForItemCountAsync("iron-plate", 10, TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForItemCountAsync_ReturnsSatisfiedWhenCountMet()
    {
        var scripted = new ScriptedRconClient([
            "{\"item\":\"iron-plate\",\"count\":50}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForItemCountAsync(
            "iron-plate", 50,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"satisfied\"", result);
        Assert.Contains("\"item\":\"iron-plate\"", result);
        Assert.Contains("\"count\":50", result);
    }

    [Fact]
    public async Task WaitForItemCountAsync_ReturnsSatisfiedWhenCountExceeded()
    {
        var scripted = new ScriptedRconClient([
            "{\"item\":\"iron-plate\",\"count\":10}",
            "{\"item\":\"iron-plate\",\"count\":75}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForItemCountAsync(
            "iron-plate", 50,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"satisfied\"", result);
    }

    [Fact]
    public async Task WaitForItemCountAsync_ReturnsTimeoutWhenCountNotMet()
    {
        var scripted = new ScriptedRconClient([
            "{\"item\":\"iron-plate\",\"count\":5}",
            "{\"item\":\"iron-plate\",\"count\":10}",
            "{\"item\":\"iron-plate\",\"count\":15}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForItemCountAsync(
            "iron-plate", 50,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
        Assert.Contains("\"target\":50", result);
    }

    [Fact]
    public async Task WaitForItemCountAsync_SendsLuaWithItemName()
    {
        var scripted = new ScriptedRconClient([
            "{\"item\":\"electronic-circuit\",\"count\":100}"
        ]);
        var service = new FactorioService(scripted);

        await service.WaitForItemCountAsync(
            "electronic-circuit", 10,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("electronic-circuit", scripted.AllCommands[0]);
        Assert.Contains("get_item_count", scripted.AllCommands[0]);
    }

    // ── WaitForEntityStatus ──────────────────────────────────────────

    [Fact]
    public async Task WaitForEntityStatusAsync_ThrowsOnNullTargetStatus()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.WaitForEntityStatusAsync(0, 0, null!, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_ThrowsOnEmptyTargetStatus()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.WaitForEntityStatusAsync(0, 0, "", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_ThrowsOnZeroPollInterval()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForEntityStatusAsync(0, 0, "working", TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_ThrowsOnZeroTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForEntityStatusAsync(0, 0, "working", TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_ReturnsSatisfiedWhenStatusMatches()
    {
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"stone-furnace\",\"status\":\"working\"}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityStatusAsync(
            5, 10, "working",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"satisfied\"", result);
        Assert.Contains("\"entity_status\":\"working\"", result);
        Assert.Contains("\"entity\":\"stone-furnace\"", result);
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_ReturnsSatisfiedCaseInsensitive()
    {
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"stone-furnace\",\"status\":\"Working\"}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityStatusAsync(
            5, 10, "working",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"satisfied\"", result);
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_ReturnsErrorWhenNoEntity()
    {
        var scripted = new ScriptedRconClient([
            "{\"error\":\"no_entity\",\"x\":5,\"y\":10}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityStatusAsync(
            5, 10, "working",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"error\"", result);
        Assert.Contains("\"error\":\"no_entity\"", result);
    }

    [Fact]
    public async Task WaitForEntityStatusAsync_ReturnsTimeoutWhenStatusNeverMatches()
    {
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"stone-furnace\",\"status\":\"no_fuel\"}",
            "{\"entity\":\"stone-furnace\",\"status\":\"no_fuel\"}",
            "{\"entity\":\"stone-furnace\",\"status\":\"no_fuel\"}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityStatusAsync(
            5, 10, "working",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
        Assert.Contains("\"entity_status\":\"no_fuel\"", result);
        Assert.Contains("\"target_status\":\"working\"", result);
    }

    // ── WaitForEntityInventory ───────────────────────────────────────

    [Fact]
    public async Task WaitForEntityInventoryAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.WaitForEntityInventoryAsync(0, 0, null!, 10, "chest",
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ThrowsOnZeroTargetCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForEntityInventoryAsync(0, 0, "iron-plate", 0, "chest",
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ThrowsOnNullInventoryType()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.WaitForEntityInventoryAsync(0, 0, "iron-plate", 10, null!,
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ThrowsOnZeroPollInterval()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForEntityInventoryAsync(0, 0, "iron-plate", 10, "chest",
                TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ThrowsOnZeroTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.WaitForEntityInventoryAsync(0, 0, "iron-plate", 10, "chest",
                TimeSpan.FromSeconds(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ReturnsSatisfiedWhenCountMet()
    {
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"stone-furnace\",\"item\":\"iron-plate\",\"count\":20,\"inventory_type\":\"furnace_result\"}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityInventoryAsync(
            5, 10, "iron-plate", 10, "furnace_result",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"satisfied\"", result);
        Assert.Contains("\"entity\":\"stone-furnace\"", result);
        Assert.Contains("\"count\":20", result);
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ReturnsErrorWhenNoEntity()
    {
        var scripted = new ScriptedRconClient([
            "{\"error\":\"no_entity\",\"x\":5,\"y\":10}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityInventoryAsync(
            5, 10, "iron-plate", 10, "chest",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"error\"", result);
        Assert.Contains("\"error\":\"no_entity\"", result);
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ReturnsErrorWhenInvalidInventoryType()
    {
        var scripted = new ScriptedRconClient([
            "{\"error\":\"invalid_inventory_type\",\"inventory_type\":\"bogus\"}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityInventoryAsync(
            5, 10, "iron-plate", 10, "bogus",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"error\"", result);
        Assert.Contains("\"error\":\"invalid_inventory_type\"", result);
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ReturnsErrorWhenNoInventory()
    {
        var scripted = new ScriptedRconClient([
            "{\"error\":\"no_inventory\",\"entity\":\"transport-belt\",\"inventory_type\":\"chest\"}"
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityInventoryAsync(
            5, 10, "iron-plate", 10, "chest",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"error\"", result);
        Assert.Contains("\"error\":\"no_inventory\"", result);
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_ReturnsTimeoutWhenCountNotMet()
    {
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"stone-furnace\",\"item\":\"iron-plate\",\"count\":2,\"inventory_type\":\"furnace_result\"}",
            "{\"entity\":\"stone-furnace\",\"item\":\"iron-plate\",\"count\":4,\"inventory_type\":\"furnace_result\"}",
            "{\"entity\":\"stone-furnace\",\"item\":\"iron-plate\",\"count\":6,\"inventory_type\":\"furnace_result\"}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityInventoryAsync(
            5, 10, "iron-plate", 50, "furnace_result",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));

        Assert.Contains("\"status\":\"timeout\"", result);
        Assert.Contains("\"target\":50", result);
    }

    [Fact]
    public async Task WaitForEntityInventoryAsync_PollsUntilCountSatisfied()
    {
        var scripted = new ScriptedRconClient([
            "{\"entity\":\"iron-chest\",\"item\":\"copper-plate\",\"count\":3,\"inventory_type\":\"chest\"}",
            "{\"entity\":\"iron-chest\",\"item\":\"copper-plate\",\"count\":7,\"inventory_type\":\"chest\"}",
            "{\"entity\":\"iron-chest\",\"item\":\"copper-plate\",\"count\":12,\"inventory_type\":\"chest\"}",
        ]);
        var service = new FactorioService(scripted);

        var result = await service.WaitForEntityInventoryAsync(
            0, 0, "copper-plate", 10, "chest",
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"satisfied\"", result);
        Assert.Contains("\"count\":12", result);
        // Should have polled 3 times (first two below target, third satisfies)
        Assert.Equal(3, scripted.AllCommands.Count);
    }
}
