using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for semantic area perception — structured area summaries,
/// directional queries, and buildable space detection.
/// </summary>
[McpServerToolType]
internal sealed class PerceptionTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get a structured overview of a circular area: resources (types, amounts, centers), " +
        "machines (grouped by type with working/idle counts), threats (enemies), and free space estimate. " +
        "Defaults to scanning around the player. Provide centerX/centerY to scan a remote area. " +
        "Use this to understand what is around you before making decisions.")]
    public Task<string> SummarizeArea(
        [Description("Search radius in tiles (default 50)")]
        double radius = 50,
        [Description("Optional X coordinate to center the scan on (omit to use player position)")]
        double? centerX = null,
        [Description("Optional Y coordinate to center the scan on (omit to use player position)")]
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(SummarizeArea),
            ct => factorio.SummarizeAreaAsync(radius, centerX, centerY, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Look in a compass direction from the player and report what entities are along that line. " +
        "Returns entities sorted by distance (closest first) with type, position, and direction. " +
        "Think of it as 'what am I looking at if I face north/east/etc'. " +
        "Useful for understanding what is ahead before walking or building.")]
    public Task<string> WhatAmILookingAt(
        [Description("Compass direction to look: north, south, east, west, northeast, northwest, southeast, southwest")]
        string direction,
        [Description("How far to look in tiles (default 30)")]
        double range = 30,
        [Description("Width of the look cone in tiles (default 3)")]
        double width = 3,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(WhatAmILookingAt),
            ct => factorio.LookInDirectionAsync(direction, range, width, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Find a flat, empty rectangular area suitable for building. " +
        "Searches outward from the player (or given center) for a region free of structures and water. " +
        "Resource entities (ores) are ignored — only buildings and water block placement. " +
        "Returns the top-left corner, center, and distance of the closest suitable area. " +
        "Use this before laying out a factory section to find where to build.")]
    public Task<string> FindBuildableArea(
        [Description("Required width in tiles")]
        int width,
        [Description("Required height in tiles")]
        int height,
        [Description("Maximum search distance from center (default 50)")]
        double searchRadius = 50,
        [Description("Optional X coordinate to search from (omit to use player position)")]
        double? centerX = null,
        [Description("Optional Y coordinate to search from (omit to use player position)")]
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(FindBuildableArea),
            ct => factorio.FindBuildableAreaAsync(width, height, searchRadius, centerX, centerY, ct),
            cancellationToken);
    }
}
