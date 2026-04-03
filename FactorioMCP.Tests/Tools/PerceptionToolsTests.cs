using FactorioMCP.Services;
using FactorioMCP.Tests.Services;
using FactorioMCP.Tools;
using Xunit;

namespace FactorioMCP.Tests.Tools;

public class PerceptionToolsTests
{
    // ── SummarizeArea — Delegates to service ────────────────────────

    [Fact]
    public async Task SummarizeArea_DelegatesToService()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await tools.SummarizeArea(radius: 30);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
        Assert.Contains("find_entities_filtered", rcon.LastCommand);
        Assert.Contains("resource", rcon.LastCommand);
        Assert.Contains("30", rcon.LastCommand);
    }

    [Fact]
    public async Task SummarizeArea_WithCenter_UsesProvidedCoordinates()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await tools.SummarizeArea(radius: 25, centerX: 100, centerY: -50);

        Assert.Contains("100", rcon.LastCommand!);
        Assert.Contains("-50", rcon.LastCommand!);
    }

    [Fact]
    public async Task SummarizeArea_WithoutCenter_UsesPlayerPosition()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await tools.SummarizeArea(radius: 20);

        Assert.Contains("player.position", rcon.LastCommand!);
    }

    [Fact]
    public async Task SummarizeArea_ReturnsStructuredJson()
    {
        var json = """{"center_x":0.0,"center_y":0.0,"radius":50,"resources":[{"name":"iron-ore","count":120,"total_amount":50000,"center_x":10.5,"center_y":-3.2}],"machines":[{"name":"stone-furnace","type":"furnace","count":4,"working":3,"idle":1}],"threats":[],"free_space":{"total_tiles":10000,"occupied":124,"free_percent":98.8}}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        var result = await tools.SummarizeArea();

        Assert.Contains("\"resources\":", result);
        Assert.Contains("\"machines\":", result);
        Assert.Contains("\"threats\":", result);
        Assert.Contains("\"free_space\":", result);
        Assert.Contains("\"iron-ore\"", result);
        Assert.Contains("\"stone-furnace\"", result);
    }

    // ── WhatAmILookingAt — Delegates to service ─────────────────────

    [Fact]
    public async Task WhatAmILookingAt_DelegatesToService()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await tools.WhatAmILookingAt("north", range: 20);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
        Assert.Contains("find_entities_filtered", rcon.LastCommand);
    }

    [Fact]
    public async Task WhatAmILookingAt_ReturnsEntitiesSortedByDistance()
    {
        var json = """{"direction":"north","range":30,"width":3,"entities":[{"name":"stone-furnace","type":"furnace","x":0.0,"y":-5.0,"distance":5.0},{"name":"iron-chest","type":"container","x":1.0,"y":-15.0,"distance":15.0}],"total_found":2}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        var result = await tools.WhatAmILookingAt("north");

        Assert.Contains("\"direction\":\"north\"", result);
        Assert.Contains("\"total_found\":2", result);
        Assert.Contains("\"stone-furnace\"", result);
        Assert.Contains("\"iron-chest\"", result);
    }

    [Fact]
    public async Task WhatAmILookingAt_EmptyDirection_ReturnsEmptyList()
    {
        var json = """{"direction":"east","range":30,"width":3,"entities":[],"total_found":0}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        var result = await tools.WhatAmILookingAt("east");

        Assert.Contains("\"total_found\":0", result);
        Assert.Contains("\"entities\":[]", result);
    }

    [Theory]
    [InlineData("north")]
    [InlineData("south")]
    [InlineData("east")]
    [InlineData("west")]
    [InlineData("northeast")]
    [InlineData("northwest")]
    [InlineData("southeast")]
    [InlineData("southwest")]
    public async Task WhatAmILookingAt_AllDirections_ProduceValidLua(string direction)
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await tools.WhatAmILookingAt(direction);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
    }

    [Fact]
    public async Task WhatAmILookingAt_InvalidDirection_Throws()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await Assert.ThrowsAsync<ArgumentException>(() => tools.WhatAmILookingAt("up"));
    }

    // ── FindBuildableArea — Delegates to service ────────────────────

    [Fact]
    public async Task FindBuildableArea_DelegatesToService()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await tools.FindBuildableArea(width: 10, height: 8);

        Assert.NotNull(rcon.LastCommand);
        Assert.StartsWith("/silent-command", rcon.LastCommand);
        Assert.Contains("find_tiles_filtered", rcon.LastCommand);
        Assert.Contains("find_entities_filtered", rcon.LastCommand);
        Assert.Contains("10", rcon.LastCommand);
        Assert.Contains("8", rcon.LastCommand);
    }

    [Fact]
    public async Task FindBuildableArea_WithCenter_UsesProvidedCoordinates()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await tools.FindBuildableArea(width: 5, height: 5, centerX: 50, centerY: -30);

        Assert.Contains("50", rcon.LastCommand!);
        Assert.Contains("-30", rcon.LastCommand!);
    }

    [Fact]
    public async Task FindBuildableArea_AreaFound_ReturnsPosition()
    {
        var json = """{"success":true,"x":10,"y":-5,"width":8,"height":6,"center_x":14.0,"center_y":-2.0,"distance":15.2}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        var result = await tools.FindBuildableArea(8, 6);

        Assert.Contains("\"success\":true", result);
        Assert.Contains("\"x\":10", result);
        Assert.Contains("\"y\":-5", result);
        Assert.Contains("\"width\":8", result);
        Assert.Contains("\"height\":6", result);
        Assert.Contains("\"center_x\":14.0", result);
    }

    [Fact]
    public async Task FindBuildableArea_NoAreaFound_ReturnsError()
    {
        var json = """{"success":false,"error":"no_area_found","width":100,"height":100,"search_radius":10}""";
        var rcon = new ScriptedRconClient([json]);
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        var result = await tools.FindBuildableArea(100, 100, searchRadius: 10);

        Assert.Contains("\"success\":false", result);
        Assert.Contains("\"error\":\"no_area_found\"", result);
    }

    // ── Service argument validation ─────────────────────────────────

    [Fact]
    public async Task SummarizeArea_ZeroRadius_Throws()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tools.SummarizeArea(radius: 0));
    }

    [Fact]
    public async Task FindBuildableArea_ZeroWidth_Throws()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tools.FindBuildableArea(0, 5));
    }

    [Fact]
    public async Task FindBuildableArea_ZeroHeight_Throws()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tools.FindBuildableArea(5, 0));
    }

    [Fact]
    public async Task WhatAmILookingAt_NullDirection_Throws()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();
        var tools = new PerceptionTools(factorio, queue);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => tools.WhatAmILookingAt(null!));
    }

    // ── DI Resolution ───────────────────────────────────────────────

    [Fact]
    public void PerceptionTools_CanBeConstructed()
    {
        var rcon = new CapturingRconClient();
        var factorio = new FactorioService(rcon);
        var queue = new GameCommandQueue();

        var tools = new PerceptionTools(factorio, queue);

        Assert.NotNull(tools);
    }
}
