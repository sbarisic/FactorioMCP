using FactorioMCP.Rcon;
using Xunit;
using Xunit.Abstractions;

namespace FactorioMCP.Tests.Integration;

/// <summary>
/// RCON protocol integration tests that connect to a running Factorio instance.
/// Tests authentication, command execution, error handling, and reconnection behavior.
///
/// Requires Factorio running with: --rcon-port 27015 --rcon-password mypassword
/// Use: dotnet test --filter "Category!=Integration" to exclude from CI.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RconIntegrationTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly RconClient _rcon = new();

    public RconIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask DisposeAsync()
    {
        await _rcon.DisposeAsync();
    }

    // ── Authentication ──────────────────────────────────────────────

    [Fact]
    public async Task ConnectAndAuthenticate_Succeeds()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");
        _output.WriteLine("✅ Authentication succeeded");
    }

    [Fact]
    public async Task ConnectAndAuthenticate_WrongPassword_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "wrongpassword"));
        _output.WriteLine("✅ Wrong password correctly rejected");
    }

    [Fact]
    public async Task ConnectAndAuthenticate_WrongHost_Throws()
    {
        // Connection to non-existent host should throw a network exception
        await Assert.ThrowsAnyAsync<Exception>(
            () => _rcon.ConnectAndAuthenticateAsync("192.0.2.1", 27015, "mypassword",
                new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token));
        _output.WriteLine("✅ Wrong host correctly rejected");
    }

    // ── Command Execution ───────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SimpleCommand_ReturnsResult()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        var result = await _rcon.ExecuteAsync("/silent-command rcon.print('hello')");
        Assert.Equal("hello", result.Trim());
        _output.WriteLine($"✅ Simple command returned: [{result}]");
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutput_ReturnsValidJson()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        var result = await _rcon.ExecuteAsync(
            "/silent-command rcon.print('{\"key\":\"value\",\"num\":42}')");
        Assert.Contains("\"key\"", result);
        Assert.Contains("\"value\"", result);
        _output.WriteLine($"✅ JSON command returned: [{result}]");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyOutput_ReturnsEmpty()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        // A command with no rcon.print returns empty
        var result = await _rcon.ExecuteAsync("/silent-command local x = 1 + 1");
        Assert.Equal("", result);
        _output.WriteLine($"✅ Empty output command returned: [{result}]");
    }

    [Fact]
    public async Task ExecuteAsync_LuaError_ReturnsErrorText()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        // Intentional Lua error — Factorio returns error text via RCON
        var result = await _rcon.ExecuteAsync("/silent-command error('test error')");
        Assert.NotEmpty(result);
        _output.WriteLine($"✅ Lua error returned: [{result}]");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleCommandsSequentially_AllSucceed()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        for (int i = 0; i < 10; i++)
        {
            var result = await _rcon.ExecuteAsync($"/silent-command rcon.print('{i}')");
            Assert.Equal(i.ToString(), result.Trim());
        }
        _output.WriteLine("✅ 10 sequential commands all succeeded");
    }

    [Fact]
    public async Task ExecuteAsync_LargeOutput_ReturnsFullResult()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        // Generate a moderately large output
        var result = await _rcon.ExecuteAsync(
            "/silent-command local t = {} for i=1,100 do t[#t+1] = 'item_'..i end rcon.print(table.concat(t, ','))");
        Assert.Contains("item_1", result);
        Assert.Contains("item_100", result);
        _output.WriteLine($"✅ Large output returned ({result.Length} chars)");
    }

    // ── Game State Queries ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_GameVersion_ReturnsVersion()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        var result = await _rcon.ExecuteAsync(
            "/silent-command rcon.print(script.active_mods['base'])");
        Assert.NotEmpty(result);
        Assert.Contains(".", result); // Version string like "2.0.76"
        _output.WriteLine($"✅ Game version: {result}");
    }

    [Fact]
    public async Task ExecuteAsync_ConnectedPlayers_ReturnsAtLeastOne()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        var result = await _rcon.ExecuteAsync(
            "/silent-command local c = 0 for _ in pairs(game.connected_players) do c = c + 1 end rcon.print(c)");
        var count = int.Parse(result);
        Assert.True(count >= 1, "Expected at least 1 connected player");
        _output.WriteLine($"✅ Connected players: {count}");
    }

    // ── Connection Resilience ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AfterDisconnect_Reconnects()
    {
        await _rcon.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        // First command works
        var result1 = await _rcon.ExecuteAsync("/silent-command rcon.print('before')");
        Assert.Equal("before", result1.Trim());

        // Close and reconnect
        await _rcon.DisposeAsync();
        var rcon2 = new RconClient();
        await rcon2.ConnectAndAuthenticateAsync("127.0.0.1", 27015, "mypassword");

        var result2 = await rcon2.ExecuteAsync("/silent-command rcon.print('after')");
        Assert.Equal("after", result2.Trim());
        await rcon2.DisposeAsync();

        _output.WriteLine("✅ Reconnection after disconnect succeeded");
    }
}
