using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class VisionServiceTests
{
    private readonly CapturingRconClient _rcon = new();
    private readonly VisionService _service;

    public VisionServiceTests()
    {
        _service = new VisionService(_rcon);
    }

    // ── TakeScreenshotAsync — Command structure ─────────────────────

    [Fact]
    public async Task TakeScreenshotAsync_SendsSilentCommand()
    {
        await _service.TakeScreenshotAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task TakeScreenshotAsync_UsesPlayerPositionByDefault()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_UsesCustomCenter()
    {
        await _service.TakeScreenshotAsync(centerX: 10.5, centerY: -20.3);

        Assert.Contains("{10.5,-20.3}", _rcon.LastCommand!);
        Assert.DoesNotContain("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_RequiresBothCenterCoordinates()
    {
        // Only centerX provided — should use player position
        await _service.TakeScreenshotAsync(centerX: 5.0);

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_IncludesResolution()
    {
        await _service.TakeScreenshotAsync(width: 1280, height: 720);

        Assert.Contains("resolution={x=1280, y=720}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_IncludesDefaultResolution()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("resolution={x=1920, y=1080}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_IncludesZoom()
    {
        await _service.TakeScreenshotAsync(zoom: 0.5);

        Assert.Contains("zoom=zoom", _rcon.LastCommand!);
        Assert.Contains("local zoom = 0.5", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_IncludesDefaultZoom()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("local zoom = 1", _rcon.LastCommand!);
    }

    // ── TakeScreenshotAsync — Screenshot options ────────────────────

    [Fact]
    public async Task TakeScreenshotAsync_CallsTakeScreenshot()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("game.take_screenshot", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_EnablesAltMode()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("show_entity_info=true", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_DisablesGui()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("show_gui=false", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_SetsScreenshotPath()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("ai-player/vision.png", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_WaitsForScreenshotFinish()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("set_wait_for_screenshots_to_finish", _rcon.LastCommand!);
    }

    // ── TakeScreenshotAsync — Entity scanning ───────────────────────

    [Fact]
    public async Task TakeScreenshotAsync_ScansEntitiesInVisibleArea()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("find_entities_filtered", _rcon.LastCommand!);
        Assert.Contains("area=", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_FiltersOutClutter()
    {
        await _service.TakeScreenshotAsync();

        // Should skip resources, trees, cliffs, fish, character
        Assert.Contains("resource", _rcon.LastCommand!);
        Assert.Contains("tree", _rcon.LastCommand!);
        Assert.Contains("simple-entity", _rcon.LastCommand!);
        Assert.Contains("cliff", _rcon.LastCommand!);
        Assert.Contains("character", _rcon.LastCommand!);
    }

    // ── TakeScreenshotAsync — Rendering overlays ────────────────────

    [Fact]
    public async Task TakeScreenshotAsync_DrawsBoundingBoxes()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("rendering.draw_rectangle", _rcon.LastCommand!);
        Assert.Contains("bounding_box", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_DrawsLabels()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("rendering.draw_text", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_DrawsInserterArrows()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("rendering.draw_line", _rcon.LastCommand!);
        Assert.Contains("pickup_position", _rcon.LastCommand!);
        Assert.Contains("drop_position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_OverlaysHaveTimeToLive()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("time_to_live=120", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_CleansUpRenderObjects()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("obj.destroy()", _rcon.LastCommand!);
    }

    // ── TakeScreenshotAsync — Map legend output ─────────────────────

    [Fact]
    public async Task TakeScreenshotAsync_OutputsMapLegend()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("entity_count", _rcon.LastCommand!);
        Assert.Contains("center_x", _rcon.LastCommand!);
        Assert.Contains("center_y", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_LegendIncludesInsertPositions()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("pickup_x", _rcon.LastCommand!);
        Assert.Contains("pickup_y", _rcon.LastCommand!);
        Assert.Contains("drop_x", _rcon.LastCommand!);
        Assert.Contains("drop_y", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_LegendIncludesRecipe()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("get_recipe", _rcon.LastCommand!);
    }

    [Fact]
    public async Task TakeScreenshotAsync_LegendIncludesStatus()
    {
        await _service.TakeScreenshotAsync();

        Assert.Contains("e.status", _rcon.LastCommand!);
        Assert.Contains("entity_status", _rcon.LastCommand!);
    }

    // ── TakeScreenshotAsync — Validation ────────────────────────────

    [Fact]
    public async Task TakeScreenshotAsync_ThrowsOnInvalidZoom()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.TakeScreenshotAsync(zoom: 0));
    }

    [Fact]
    public async Task TakeScreenshotAsync_ThrowsOnNegativeZoom()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.TakeScreenshotAsync(zoom: -1));
    }

    [Fact]
    public async Task TakeScreenshotAsync_ThrowsOnZeroWidth()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.TakeScreenshotAsync(width: 0));
    }

    [Fact]
    public async Task TakeScreenshotAsync_ThrowsOnZeroHeight()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.TakeScreenshotAsync(height: 0));
    }

    // ── BuildScreenshotLua — Static method tests ────────────────────

    [Fact]
    public void BuildScreenshotLua_PlayerCenter_UsesPlayerPosition()
    {
        var lua = VisionService.BuildScreenshotLua(null, null, 1.0, 1920, 1080);

        Assert.Contains("player.position", lua);
    }

    [Fact]
    public void BuildScreenshotLua_CustomCenter_UsesCoordinates()
    {
        var lua = VisionService.BuildScreenshotLua(5.5, -3.2, 1.0, 1920, 1080);

        Assert.Contains("{5.5,-3.2}", lua);
        Assert.DoesNotContain("player.position", lua);
    }

    [Fact]
    public void BuildScreenshotLua_CalculatesScanRadius()
    {
        var lua = VisionService.BuildScreenshotLua(null, null, 1.0, 1920, 1080);

        Assert.Contains("scan_radius", lua);
        Assert.Contains("math.max(tiles_w, tiles_h) / 2 + 2", lua);
    }

    [Fact]
    public void BuildScreenshotLua_DrawsColorCodedOverlays()
    {
        var lua = VisionService.BuildScreenshotLua(null, null, 1.0, 1920, 1080);

        // Should have color definitions for different entity types
        Assert.Contains("inserter", lua);
        Assert.Contains("belt", lua);
        Assert.Contains("assembler", lua);
        Assert.Contains("furnace", lua);
        Assert.Contains("miner", lua);
        Assert.Contains("chest", lua);
    }

    // ── ReadScreenshotFileAsync ─────────────────────────────────────

    [Fact]
    public async Task ReadScreenshotFileAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        _service.ScriptOutputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var result = await _service.ReadScreenshotFileAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadScreenshotFileAsync_ReturnsBytes_WhenFileExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "ai-player");
        Directory.CreateDirectory(tempDir);

        try
        {
            var testData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "vision.png"), testData);

            _service.ScriptOutputDir = Path.GetDirectoryName(tempDir)!;

            var result = await _service.ReadScreenshotFileAsync();

            Assert.NotNull(result);
            Assert.Equal(testData, result);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempDir)!, true);
        }
    }
}
