using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── Walk ──────────────────────────────────────────────────────────

    [Fact]
    public async Task WalkAsync_SendsCorrectDirection()
    {
        await _service.WalkAsync("north");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("defines.direction.north", _rcon.LastCommand);
        Assert.Contains("walking = true", _rcon.LastCommand);
    }

    [Theory]
    [InlineData("south")]
    [InlineData("east")]
    [InlineData("west")]
    [InlineData("northeast")]
    [InlineData("northwest")]
    [InlineData("southeast")]
    [InlineData("southwest")]
    public async Task WalkAsync_SupportsAllDirections(string direction)
    {
        await _service.WalkAsync(direction);

        Assert.Contains($"defines.direction.{direction}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task WalkAsync_OutputsJsonWithDirectionAndPosition()
    {
        await _service.WalkAsync("east");

        Assert.Contains("\"status\":\"walking\"", _rcon.LastCommand!);
        Assert.Contains("\"direction\":\"east\"", _rcon.LastCommand!);
        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task WalkAsync_ThrowsOnNullDirection()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.WalkAsync(null!));
    }

    [Fact]
    public async Task WalkAsync_ThrowsOnWhitespaceDirection()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.WalkAsync("  "));
    }

    [Fact]
    public async Task WalkAsync_SetsWalkingStateWithDirection()
    {
        await _service.WalkAsync("south");

        Assert.NotNull(_rcon.LastCommand);
        Assert.Contains("walking_state", _rcon.LastCommand);
        Assert.Contains("defines.direction.south", _rcon.LastCommand);
    }

    [Fact]
    public async Task WalkAsync_InstallsOnTickHandler()
    {
        await _service.WalkAsync("north");

        Assert.NotNull(_rcon.LastCommand);
        Assert.Contains("script.on_event(defines.events.on_tick", _rcon.LastCommand);
        Assert.Contains("storage.walk_state", _rcon.LastCommand);
    }

    [Fact]
    public async Task StopWalkingAsync_ClearsWalkState()
    {
        await _service.StopWalkingAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.Contains("storage.walk_state = nil", _rcon.LastCommand);
    }

    [Fact]
    public async Task StopWalkingAsync_RemovesOnTickHandler()
    {
        await _service.StopWalkingAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.Contains("script.on_event(defines.events.on_tick, nil)", _rcon.LastCommand);
    }

    // ── StopWalking ──────────────────────────────────────────────────

    [Fact]
    public async Task StopWalkingAsync_SetsWalkingFalse()
    {
        await _service.StopWalkingAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("walking = false", _rcon.LastCommand);
    }

    [Fact]
    public async Task StopWalkingAsync_OutputsJsonWithStoppedStatus()
    {
        await _service.StopWalkingAsync();

        Assert.Contains("\"status\":\"stopped\"", _rcon.LastCommand!);
        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }

    // ── GetPlayerPosition ────────────────────────────────────────────

    [Fact]
    public async Task GetPlayerPositionAsync_QueriesPosition()
    {
        await _service.GetPlayerPositionAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("game.connected_players[1].position", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetPlayerPositionAsync_OutputsJsonXY()
    {
        await _service.GetPlayerPositionAsync();

        Assert.Contains("\"x\":", _rcon.LastCommand!);
        Assert.Contains("\"y\":", _rcon.LastCommand!);
    }
}
