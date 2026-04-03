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
}
