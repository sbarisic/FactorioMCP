using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class CombatServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly CombatService _service;

    public CombatServiceTests()
    {
        _service = new CombatService(_rcon);
    }

    // ── ScanEnemiesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ScanEnemiesAsync_SendsSilentCommand()
    {
        await _service.ScanEnemiesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task ScanEnemiesAsync_FindsEnemyUnits()
    {
        await _service.ScanEnemiesAsync();

        Assert.Contains("find_enemy_units", _rcon.LastCommand!);
        Assert.Contains("player.force", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanEnemiesAsync_FindsSpawnersAndWorms()
    {
        await _service.ScanEnemiesAsync();

        Assert.Contains("unit-spawner", _rcon.LastCommand!);
        Assert.Contains("turret", _rcon.LastCommand!);
        Assert.Contains("enemy", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanEnemiesAsync_FindsNearestEnemy()
    {
        await _service.ScanEnemiesAsync();

        Assert.Contains("find_nearest_enemy", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanEnemiesAsync_UsesDefaultRadius()
    {
        await _service.ScanEnemiesAsync();

        Assert.Contains("100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanEnemiesAsync_UsesCustomRadius()
    {
        await _service.ScanEnemiesAsync(radius: 200);

        Assert.Contains("200", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanEnemiesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanEnemiesAsync(radius: 0));
    }

    [Fact]
    public async Task ScanEnemiesAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanEnemiesAsync(radius: -10));
    }

    [Fact]
    public async Task ScanEnemiesAsync_OutputsJsonWithRconPrint()
    {
        await _service.ScanEnemiesAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"status\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanEnemiesAsync_UsesConnectedPlayers()
    {
        await _service.ScanEnemiesAsync();

        Assert.Contains("game.connected_players[1]", _rcon.LastCommand!);
    }

    // ── GetDefensesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetDefensesAsync_SendsSilentCommand()
    {
        await _service.GetDefensesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetDefensesAsync_FindsTurrets()
    {
        await _service.GetDefensesAsync();

        Assert.Contains("ammo-turret", _rcon.LastCommand!);
        Assert.Contains("fluid-turret", _rcon.LastCommand!);
        Assert.Contains("electric-turret", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetDefensesAsync_ReadsAmmoCount()
    {
        await _service.GetDefensesAsync();

        Assert.Contains("turret_ammo", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetDefensesAsync_ReadsKillCount()
    {
        await _service.GetDefensesAsync();

        Assert.Contains("kills", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetDefensesAsync_ReadsShootingTarget()
    {
        await _service.GetDefensesAsync();

        Assert.Contains("shooting_target", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetDefensesAsync_FiltersToPlayerForce()
    {
        await _service.GetDefensesAsync();

        Assert.Contains("force=player.force", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetDefensesAsync_UsesDefaultRadius()
    {
        await _service.GetDefensesAsync();

        Assert.Contains("radius=80", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetDefensesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetDefensesAsync(radius: 0));
    }

    [Fact]
    public async Task GetDefensesAsync_OutputsJsonWithRconPrint()
    {
        await _service.GetDefensesAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"turret_count\"", _rcon.LastCommand!);
    }
}
