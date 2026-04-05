using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class TrainServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly TrainService _service;

    public TrainServiceTests()
    {
        _service = new TrainService(_rcon);
    }

    // ── GetTrainsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetTrainsAsync_SendsSilentCommand()
    {
        await _service.GetTrainsAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetTrainsAsync_UsesTrainManager()
    {
        await _service.GetTrainsAsync();

        Assert.Contains("game.train_manager", _rcon.LastCommand!);
        Assert.Contains("get_trains", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTrainsAsync_FiltersToPlayerSurface()
    {
        await _service.GetTrainsAsync();

        Assert.Contains("surface", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTrainsAsync_ReportsTrainState()
    {
        await _service.GetTrainsAsync();

        Assert.Contains("train.state", _rcon.LastCommand!);
        Assert.Contains("defines.train_state", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTrainsAsync_ReportsManualMode()
    {
        await _service.GetTrainsAsync();

        Assert.Contains("manual_mode", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTrainsAsync_ReportsSpeed()
    {
        await _service.GetTrainsAsync();

        Assert.Contains("train.speed", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTrainsAsync_OutputsJsonWithRconPrint()
    {
        await _service.GetTrainsAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"train_count\"", _rcon.LastCommand!);
    }

    // ── GetTrainStopsAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetTrainStopsAsync_SendsSilentCommand()
    {
        await _service.GetTrainStopsAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetTrainStopsAsync_UsesTrainManager()
    {
        await _service.GetTrainStopsAsync();

        Assert.Contains("game.train_manager", _rcon.LastCommand!);
        Assert.Contains("get_train_stops", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTrainStopsAsync_ReportsStoppedTrain()
    {
        await _service.GetTrainStopsAsync();

        Assert.Contains("get_stopped_train", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTrainStopsAsync_OutputsJsonWithRconPrint()
    {
        await _service.GetTrainStopsAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"stop_count\"", _rcon.LastCommand!);
    }

    // ── InspectTrainAsync ──────────────────────────────────────────

    [Fact]
    public async Task InspectTrainAsync_SendsSilentCommand()
    {
        await _service.InspectTrainAsync(1);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task InspectTrainAsync_LooksUpById()
    {
        await _service.InspectTrainAsync(42);

        Assert.Contains("get_train_by_id", _rcon.LastCommand!);
        Assert.Contains("42", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectTrainAsync_ReadsSchedule()
    {
        await _service.InspectTrainAsync(1);

        Assert.Contains("get_schedule", _rcon.LastCommand!);
        Assert.Contains("get_record", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectTrainAsync_ReadsCargo()
    {
        await _service.InspectTrainAsync(1);

        Assert.Contains("get_contents", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectTrainAsync_OutputsJsonWithRconPrint()
    {
        await _service.InspectTrainAsync(1);

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"schedule\"", _rcon.LastCommand!);
    }

    // ── SetTrainModeAsync ──────────────────────────────────────────

    [Fact]
    public async Task SetTrainModeAsync_SendsSilentCommand()
    {
        await _service.SetTrainModeAsync(1, true);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task SetTrainModeAsync_SetsManualModeTrue()
    {
        await _service.SetTrainModeAsync(7, true);

        Assert.Contains("7", _rcon.LastCommand!);
        Assert.Contains("manual_mode = true", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SetTrainModeAsync_SetsManualModeFalse()
    {
        await _service.SetTrainModeAsync(3, false);

        Assert.Contains("manual_mode = false", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SetTrainModeAsync_OutputsJsonWithRconPrint()
    {
        await _service.SetTrainModeAsync(1, false);

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"manual_mode\"", _rcon.LastCommand!);
    }
}
