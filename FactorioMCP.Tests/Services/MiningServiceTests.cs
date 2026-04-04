using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

/// <summary>
/// Test double for RconClient that returns scripted responses in order.
/// Used for testing polling/multi-call service methods like MineResourceAsync.
/// </summary>
internal sealed class ScriptedMiningRconClient : RconClient
{
    private readonly Queue<string> _responses = new();
    public List<string> AllCommands { get; } = [];

    public void EnqueueResponse(string response) => _responses.Enqueue(response);

    public override Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        AllCommands.Add(command);
        var response = _responses.Count > 0 ? _responses.Dequeue() : string.Empty;
        return Task.FromResult(response);
    }
}

public class MiningServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly MiningService _service;

    public MiningServiceTests()
    {
        _service = new MiningService(_rcon);
    }

    // ── StartMiningResourceAsync ─────────────────────────────────────

    [Fact]
    public async Task StartMiningResource_SendsCorrectLuaScript()
    {
        await _service.StartMiningResourceAsync(10.5, -3.2);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("10.5", _rcon.LastCommand);
        Assert.Contains("-3.2", _rcon.LastCommand);
    }

    [Fact]
    public async Task StartMiningResource_ChecksReachDistance()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_FiltersResourceEntities()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Contains("type=\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_SetsUpdateSelectedEntity()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Contains("update_selected_entity", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_SetsMiningState()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Contains("mining_state", _rcon.LastCommand!);
        Assert.Contains("mining = true", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_StoresMineStateInStorage()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Contains("storage.mine_state", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_InstallsOnTickHandler()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Contains("script.on_event(defines.events.on_tick", _rcon.LastCommand!);
        Assert.Contains("storage.mine_state", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_OnTickHandlerOnlyChecksMineState()
    {
        await _service.StartMiningResourceAsync(5, 5);

        // The mining on_tick handler only handles mining (not walking - that's separate now)
        Assert.Contains("storage.mine_state", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_OnTickHandlerSelectsEntityBeforeMining()
    {
        await _service.StartMiningResourceAsync(5, 5);

        // update_selected_entity must be called before mining_state in the on_tick handler
        // so the player has a selected entity to mine
        Assert.Contains("update_selected_entity", _rcon.LastCommand!);
        int selectIdx = _rcon.LastCommand!.IndexOf("update_selected_entity(storage.mine_state.position)");
        int miningIdx = _rcon.LastCommand.IndexOf("p.mining_state = {mining = true");
        Assert.True(selectIdx < miningIdx, "update_selected_entity must come before mining_state in on_tick handler");
    }

    [Fact]
    public async Task StartMiningResource_IncludesMiningTimeInOutput()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Contains("mining_time_per_unit", _rcon.LastCommand!);
        Assert.Contains("mineable_properties.mining_time", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartMiningResource_SendsSingleRconCall()
    {
        await _service.StartMiningResourceAsync(5, 5);

        Assert.Single(_rcon.AllCommands);
    }

    // ── GetMiningStatusAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetMiningStatus_ChecksMiningState()
    {
        await _service.GetMiningStatusAsync(5, 5, 100);

        Assert.Contains("mining_state.mining", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetMiningStatus_FiltersResourceEntities()
    {
        await _service.GetMiningStatusAsync(5, 5, 100);

        Assert.Contains("type=\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetMiningStatus_CalculatesMinedFromInitialAmount()
    {
        await _service.GetMiningStatusAsync(5, 5, 100);

        Assert.Contains("100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetMiningStatus_PassesCoordinates()
    {
        await _service.GetMiningStatusAsync(7.5, -2.5, 50);

        Assert.Contains("7.5", _rcon.LastCommand!);
        Assert.Contains("-2.5", _rcon.LastCommand!);
    }

    // ── StopMiningAsync ──────────────────────────────────────────────

    [Fact]
    public async Task StopMining_SetsMiningStateFalse()
    {
        await _service.StopMiningAsync();

        Assert.Contains("mining = false", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StopMining_ClearsMineStateInStorage()
    {
        await _service.StopMiningAsync();

        Assert.Contains("storage.mine_state = nil", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StopMining_KeepsOnTickHandlerInstalled()
    {
        await _service.StopMiningAsync();

        // on_tick handler should NOT be removed — it's shared with PathfindingService
        Assert.Contains("storage.mine_state = nil", _rcon.LastCommand!);
        Assert.DoesNotContain("script.on_event(defines.events.on_tick, nil)", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StopMining_SendsSingleRconCall()
    {
        await _service.StopMiningAsync();

        Assert.Single(_rcon.AllCommands);
    }

    // ── MineResourceAsync ────────────────────────────────────────────

    [Fact]
    public async Task MineResource_RejectsZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.MineResourceAsync(5, 5, 0, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task MineResource_RejectsNegativeCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.MineResourceAsync(5, 5, -1, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task MineResource_RejectsZeroPollInterval()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.MineResourceAsync(5, 5, 1, TimeSpan.Zero, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task MineResource_RejectsZeroTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.MineResourceAsync(5, 5, 1, TimeSpan.FromSeconds(0.1), TimeSpan.Zero));
    }

    [Fact]
    public async Task MineResource_ReturnsStartError_WhenOutOfRange()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        scriptedRcon.EnqueueResponse("""{"success":false,"error":"out_of_range","distance":12.3,"limit":6}""");

        var result = await service.MineResourceAsync(5, 5, 1,
            TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(1));

        Assert.Contains("out_of_range", result);
    }

    [Fact]
    public async Task MineResource_ReturnsStartError_WhenNoResource()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        scriptedRcon.EnqueueResponse("""{"success":false,"error":"no_resource","x":5,"y":5}""");

        var result = await service.MineResourceAsync(5, 5, 1,
            TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(1));

        Assert.Contains("no_resource", result);
    }

    [Fact]
    public async Task MineResource_CompletesWhenDesiredCountReached()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        // Start mining response
        scriptedRcon.EnqueueResponse("""{"success":true,"entity":"iron-ore","amount":100,"mining_time_per_unit":1.0,"x":5.0,"y":5.0,"status":"mining_started"}""");
        // First poll: 3 units mined
        scriptedRcon.EnqueueResponse("""{"is_mining":true,"depleted":false,"remaining":97,"mined":3,"entity":"iron-ore"}""");
        // Second poll: 5 units mined (target reached)
        scriptedRcon.EnqueueResponse("""{"is_mining":true,"depleted":false,"remaining":95,"mined":5,"entity":"iron-ore"}""");
        // Stop mining response
        scriptedRcon.EnqueueResponse("""{"success":true,"status":"mining_stopped"}""");

        var result = await service.MineResourceAsync(5, 5, 5,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"complete\"", result);
        Assert.Contains("\"mined\":5", result);
        Assert.Contains("\"remaining\":95", result);
    }

    [Fact]
    public async Task MineResource_ReportsDepletedWhenResourceGone()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        // Start mining
        scriptedRcon.EnqueueResponse("""{"success":true,"entity":"iron-ore","amount":3,"mining_time_per_unit":1.0,"x":5.0,"y":5.0,"status":"mining_started"}""");
        // Poll: depleted
        scriptedRcon.EnqueueResponse("""{"is_mining":false,"depleted":true,"remaining":0,"mined":3}""");
        // Stop mining
        scriptedRcon.EnqueueResponse("""{"success":true,"status":"mining_stopped"}""");

        var result = await service.MineResourceAsync(5, 5, 10,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"depleted\"", result);
        Assert.Contains("\"mined\":3", result);
        Assert.Contains("\"depleted\":true", result);
    }

    [Fact]
    public async Task MineResource_CapsRequestedToAvailable()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        // Start: only 2 available but requesting 10
        scriptedRcon.EnqueueResponse("""{"success":true,"entity":"coal","amount":2,"mining_time_per_unit":1.0,"x":5.0,"y":5.0,"status":"mining_started"}""");
        // Poll: all mined
        scriptedRcon.EnqueueResponse("""{"is_mining":false,"depleted":true,"remaining":0,"mined":2}""");
        // Stop
        scriptedRcon.EnqueueResponse("""{"success":true,"status":"mining_stopped"}""");

        var result = await service.MineResourceAsync(5, 5, 10,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"mined\":2", result);
        Assert.Contains("\"requested\":10", result);
    }

    [Fact]
    public async Task MineResource_StopsMiningAfterCompletion()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        scriptedRcon.EnqueueResponse("""{"success":true,"entity":"iron-ore","amount":100,"mining_time_per_unit":1.0,"x":5.0,"y":5.0,"status":"mining_started"}""");
        scriptedRcon.EnqueueResponse("""{"is_mining":true,"depleted":false,"remaining":99,"mined":1,"entity":"iron-ore"}""");
        scriptedRcon.EnqueueResponse("""{"success":true,"status":"mining_stopped"}""");

        await service.MineResourceAsync(5, 5, 1,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        // Last command should be the stop mining command
        var lastCmd = scriptedRcon.AllCommands[^1];
        Assert.Contains("mining = false", lastCmd);
    }

    [Fact]
    public async Task MineResource_RestartsIfMiningStops()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        // Start
        scriptedRcon.EnqueueResponse("""{"success":true,"entity":"iron-ore","amount":100,"mining_time_per_unit":1.0,"x":5.0,"y":5.0,"status":"mining_started"}""");
        // Poll: mining stopped but not enough mined
        scriptedRcon.EnqueueResponse("""{"is_mining":false,"depleted":false,"remaining":99,"mined":1,"entity":"iron-ore"}""");
        // Restart response
        scriptedRcon.EnqueueResponse("""{"success":true,"entity":"iron-ore","amount":99,"mining_time_per_unit":1.0,"x":5.0,"y":5.0,"status":"mining_started"}""");
        // Poll: target reached
        scriptedRcon.EnqueueResponse("""{"is_mining":true,"depleted":false,"remaining":95,"mined":5,"entity":"iron-ore"}""");
        // Stop
        scriptedRcon.EnqueueResponse("""{"success":true,"status":"mining_stopped"}""");

        var result = await service.MineResourceAsync(5, 5, 5,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"status\":\"complete\"", result);
    }

    [Fact]
    public async Task MineResource_IncludesEntityNameInResult()
    {
        var scriptedRcon = new ScriptedMiningRconClient();
        var service = new MiningService(scriptedRcon);

        scriptedRcon.EnqueueResponse("""{"success":true,"entity":"copper-ore","amount":50,"mining_time_per_unit":1.0,"x":5.0,"y":5.0,"status":"mining_started"}""");
        scriptedRcon.EnqueueResponse("""{"is_mining":true,"depleted":false,"remaining":49,"mined":1,"entity":"copper-ore"}""");
        scriptedRcon.EnqueueResponse("""{"success":true,"status":"mining_stopped"}""");

        var result = await service.MineResourceAsync(5, 5, 1,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(5));

        Assert.Contains("\"entity\":\"copper-ore\"", result);
    }
}
