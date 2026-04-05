using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class LogisticsServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly LogisticsService _service;

    public LogisticsServiceTests()
    {
        _service = new LogisticsService(_rcon);
    }

    // ── GetLogisticNetworkAsync ──────────────────────────────────────

    [Fact]
    public async Task GetLogisticNetworkAsync_SendsSilentCommand()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetLogisticNetworkAsync_FindsNetworkByPosition()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.Contains("find_logistic_network_by_position", _rcon.LastCommand!);
        Assert.Contains("player.force", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetLogisticNetworkAsync_ReturnsRobotCounts()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.Contains("all_logistic_robots", _rcon.LastCommand!);
        Assert.Contains("available_logistic_robots", _rcon.LastCommand!);
        Assert.Contains("all_construction_robots", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetLogisticNetworkAsync_ReturnsEntityCounts()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.Contains("providers", _rcon.LastCommand!);
        Assert.Contains("requesters", _rcon.LastCommand!);
        Assert.Contains("storages", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetLogisticNetworkAsync_ReturnsNetworkId()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.Contains("network_id", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetLogisticNetworkAsync_HandlesNoNetwork()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.Contains("no_network", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetLogisticNetworkAsync_OutputsJsonWithRconPrint()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"status\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetLogisticNetworkAsync_UsesConnectedPlayers()
    {
        await _service.GetLogisticNetworkAsync();

        Assert.Contains("game.connected_players[1]", _rcon.LastCommand!);
    }

    // ── GetNetworkContentsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetNetworkContentsAsync_SendsSilentCommand()
    {
        await _service.GetNetworkContentsAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetNetworkContentsAsync_GetsContents()
    {
        await _service.GetNetworkContentsAsync();

        Assert.Contains("get_contents", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetNetworkContentsAsync_OutputsItemList()
    {
        await _service.GetNetworkContentsAsync();

        Assert.Contains("item_count", _rcon.LastCommand!);
        Assert.Contains("items", _rcon.LastCommand!);
    }

    // ── GetRobotStatusAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetRobotStatusAsync_SendsSilentCommand()
    {
        await _service.GetRobotStatusAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetRobotStatusAsync_QueriesLogisticRobots()
    {
        await _service.GetRobotStatusAsync();

        Assert.Contains("logistic_robots", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRobotStatusAsync_QueriesConstructionRobots()
    {
        await _service.GetRobotStatusAsync();

        Assert.Contains("construction_robots", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRobotStatusAsync_TracksBusyAndIdle()
    {
        await _service.GetRobotStatusAsync();

        Assert.Contains("idle", _rcon.LastCommand!);
        Assert.Contains("busy", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRobotStatusAsync_OutputsJsonWithRconPrint()
    {
        await _service.GetRobotStatusAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"logistic_idle\"", _rcon.LastCommand!);
    }
}
