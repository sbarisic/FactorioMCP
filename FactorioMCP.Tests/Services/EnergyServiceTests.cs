using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class EnergyServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly EnergyService _service;

    public EnergyServiceTests()
    {
        _service = new EnergyService(_rcon);
    }

    // ── GetElectricNetworkAsync ───────────────────────────────────────

    [Fact]
    public async Task GetElectricNetworkAsync_SendsSilentCommand()
    {
        await _service.GetElectricNetworkAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_FindsElectricPoles()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("electric-pole", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_UsesPlayerPosition()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("game.connected_players[1]", _rcon.LastCommand!);
        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_UsesDefaultRadius()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("radius=50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_UsesCustomRadius()
    {
        await _service.GetElectricNetworkAsync(radius: 100);

        Assert.Contains("radius=100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_UsesFlowStatistics()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("electric_network_statistics", _rcon.LastCommand!);
        Assert.Contains("get_flow_count", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_UsesFiveSecondPrecision()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("defines.flow_precision_index.five_seconds", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_QueriesProductionAndConsumption()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("output_counts", _rcon.LastCommand!);
        Assert.Contains("input_counts", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_ScansAccumulators()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("type=\"accumulator\"", _rcon.LastCommand!);
        Assert.Contains("electric_buffer_size", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_CalculatesSatisfaction()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("satisfaction", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_SortsPolesByDistance()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("table.sort(poles", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_OutputsJsonWithRconPrint()
    {
        await _service.GetElectricNetworkAsync();

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"status\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetElectricNetworkAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetElectricNetworkAsync(radius: 0));
    }

    [Fact]
    public async Task GetElectricNetworkAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetElectricNetworkAsync(radius: -10));
    }

    [Fact]
    public async Task GetElectricNetworkAsync_FormatsRadiusWithInvariantCulture()
    {
        await _service.GetElectricNetworkAsync(radius: 25.5);

        // Should use "25.5" not "25,5" (locale-dependent)
        Assert.Contains("radius=25.5", _rcon.LastCommand!);
    }

    // ── InspectEntityPowerAsync ───────────────────────────────────────

    [Fact]
    public async Task InspectEntityPowerAsync_SendsSilentCommand()
    {
        await _service.InspectEntityPowerAsync(10, 20);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_FindsEntitiesAtPosition()
    {
        await _service.InspectEntityPowerAsync(10, 20);

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("position={10, 20}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_PrioritizesNonResourceEntities()
    {
        await _service.InspectEntityPowerAsync(5, -3);

        Assert.Contains("sort_entities(entities", _rcon.LastCommand!);
        Assert.Contains("resource", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_ChecksElectricConnection()
    {
        await _service.InspectEntityPowerAsync(0, 0);

        Assert.Contains("is_connected_to_electric_network", _rcon.LastCommand!);
        Assert.Contains("electric_network_id", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_ReadsEnergyProperties()
    {
        await _service.InspectEntityPowerAsync(0, 0);

        Assert.Contains("e.energy", _rcon.LastCommand!);
        Assert.Contains("electric_buffer_size", _rcon.LastCommand!);
        Assert.Contains("electric_drain", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_ReadsGenerationWithPcall()
    {
        await _service.InspectEntityPowerAsync(0, 0);

        Assert.Contains("pcall", _rcon.LastCommand!);
        Assert.Contains("energy_generated_last_tick", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_OutputsJsonWithRconPrint()
    {
        await _service.InspectEntityPowerAsync(0, 0);

        Assert.Contains("rcon.print(", _rcon.LastCommand!);
        Assert.Contains("\"status\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_FormatsCoordinatesWithInvariantCulture()
    {
        await _service.InspectEntityPowerAsync(12.5, -3.7);

        Assert.Contains("12.5", _rcon.LastCommand!);
        Assert.Contains("-3.7", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_HandlesNegativeCoordinates()
    {
        await _service.InspectEntityPowerAsync(-50, -100);

        Assert.Contains("-50", _rcon.LastCommand!);
        Assert.Contains("-100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityPowerAsync_UsesConnectedPlayers()
    {
        await _service.InspectEntityPowerAsync(0, 0);

        Assert.Contains("game.connected_players[1]", _rcon.LastCommand!);
    }
}
