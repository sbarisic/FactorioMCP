using FactorioMCP.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for AI vision — annotated screenshots with entity overlays and structured map legends.
/// Returns both an image (for vision models) and a text legend (for text-only models).
/// </summary>
[McpServerToolType]
internal sealed class VisionTools(VisionService vision, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Take an annotated screenshot of the game world. The screenshot includes entity bounding " +
        "boxes (color-coded by type), numbered labels, and inserter direction arrows. Returns both " +
        "the image and a structured 'Map Legend' listing every visible entity with position, direction, " +
        "and status. Use this to visually inspect the factory layout, identify bottlenecks, verify " +
        "placement, and plan builds. Defaults to a view centered on the player.")]
    public async Task<CallToolResult> TakeScreenshot(
        [Description("Optional X coordinate to center the screenshot on (omit to use player position)")]
        double? centerX = null,
        [Description("Optional Y coordinate to center the screenshot on (omit to use player position)")]
        double? centerY = null,
        [Description("Map zoom level (default 1.0). Higher values = more zoomed in, less area visible")]
        double zoom = 1.0,
        [Description("Screenshot width in pixels (default 1920)")]
        int width = 1920,
        [Description("Screenshot height in pixels (default 1080)")]
        int height = 1080,
        CancellationToken cancellationToken = default)
    {
        // Execute the Lua script: draw overlays, take screenshot, collect legend
        var mapLegend = await queue.ExecuteAsync(
            nameof(TakeScreenshot),
            ct => vision.TakeScreenshotAsync(centerX, centerY, zoom, width, height, ct),
            cancellationToken);

        // Read the screenshot file from disk
        var imageBytes = await vision.ReadScreenshotFileAsync(cancellationToken);

        var content = new List<ContentBlock>();

        if (imageBytes is { Length: > 0 })
        {
            content.Add(ImageContentBlock.FromBytes(imageBytes, "image/png"));
        }

        // Always include the text legend (works for both vision and text-only models)
        content.Add(new TextContentBlock
        {
            Text = $"## Map Legend\n\n{mapLegend}"
        });

        return new CallToolResult
        {
            Content = content
        };
    }
}
