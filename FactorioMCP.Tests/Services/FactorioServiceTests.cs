using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

/// <summary>
/// Test double for RconClient that captures the command string passed to ExecuteAsync
/// instead of sending it over TCP. Returns an empty string as the response.
/// </summary>
internal sealed class CapturingRconClient : RconClient
{
    public string? LastCommand { get; private set; }
    public List<string> AllCommands { get; } = [];

    public override Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        AllCommands.Add(command);
        return Task.FromResult(string.Empty);
    }
}

/// <summary>
/// Test double for RconClient that returns pre-configured responses in sequence.
/// When responses are exhausted, repeats the last one.
/// </summary>
internal sealed class ScriptedRconClient : RconClient
{
    private readonly string[] _responses;
    private int _index;
    public List<string> AllCommands { get; } = [];

    public ScriptedRconClient(string[] responses)
    {
        _responses = responses;
    }

    public override Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        AllCommands.Add(command);
        var response = _responses[Math.Min(_index, _responses.Length - 1)];
        _index++;
        return Task.FromResult(response);
    }
}

public partial class FactorioServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly FactorioService _service;

    public FactorioServiceTests()
    {
        _service = new FactorioService(_rcon);
    }

    // ── ExecuteRawLua ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteRawLuaAsync_PassesThroughLuaCode()
    {
        await _service.ExecuteRawLuaAsync("rcon.print('hello')");

        Assert.NotNull(_rcon.LastCommand);
        Assert.Equal("/silent-command rcon.print('hello')", _rcon.LastCommand);
    }

    // ── Cross-cutting: all commands use /silent-command prefix ────────────────────

    [Fact]
    public async Task AllCommands_UseSilentCommandPrefix()
    {
        await _service.GetPlayerPositionAsync();
        await _service.GetInventoryAsync();
        await _service.CraftAsync("iron-plate", 1);
        await _service.GetCraftingQueueAsync();
        await _service.PlaceEntityAsync("stone-furnace", 0, 0);
        await _service.MineEntityAtAsync(0, 0);
        await _service.GetNearbyEntitiesAsync();
        await _service.CheckDistanceAsync(0, 0);
        await _service.GetResearchStatusAsync();
        await _service.GetAvailableTechnologiesAsync();
        await _service.StartResearchAsync("automation");
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");
        await _service.GetAvailableRecipesAsync();
        await _service.GetTechnologyDetailsAsync("automation");
        await _service.ExecuteRawLuaAsync("rcon.print('test')");
        await _service.GetGameTickAsync();
        await _service.ScanResourcesAsync();
        await _service.ScanTilesAsync();
        await _service.InsertItemsAsync(0, 0, "coal", 5);
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 10);
        await _service.InspectEntityAsync(0, 0);
        await _service.InitializeChatListenerAsync();
        await _service.GetChatMessagesAsync();
        await _service.SendChatMessageAsync("hello");
        await _service.DropItemsAsync("iron-plate", 10);
        await _service.TransferAllItemsAsync(0, 0);
        await _service.GetEntityInventoryAsync(0, 0);
        await _service.FindNearestEntityAsync("stone-furnace");
        await _service.FindBestResourcePatchAsync("iron-ore");
        await _service.RotateEntityAsync(0, 0);

        Assert.Equal(30, _rcon.AllCommands.Count);
        Assert.All(_rcon.AllCommands, cmd => Assert.StartsWith("/silent-command ", cmd));
    }
}
