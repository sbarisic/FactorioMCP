using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for planning and understanding transport belt placement.
/// Provides route computation and direction guidance for belt networks.
/// </summary>
[McpServerToolType]
internal sealed class BeltTools(BeltPlannerService planner)
{
    [McpServerTool, Description(
        "Plan a transport belt route between two positions. Returns an ordered list of belt tile positions " +
        "and directions that you can place one-by-one with PlaceEntity. Supports straight lines and L-shaped " +
        "routes (one 90° turn). Items flow from start to end position. " +
        "BELT MECHANICS: Belt direction = the direction items FLOW (the way arrows point). A north-facing belt " +
        "moves items upward (decreasing Y). A east-facing belt moves items rightward (increasing X). " +
        "At corners, the belt at the turn point faces the NEW direction after the turn. " +
        "USAGE: Call this tool to get the route plan, then place each belt tile using PlaceEntity with " +
        "'transport-belt' (or 'fast-transport-belt', 'express-transport-belt') and the direction from each step. " +
        "Walk close to the belt tiles before placing — you must be within build distance of each tile.")]
    public string PlanBeltRoute(
        [Description("X coordinate of the first belt tile (where items enter the belt line)")]
        double startX,
        [Description("Y coordinate of the first belt tile")]
        double startY,
        [Description("X coordinate of the last belt tile (where items exit the belt line)")]
        double endX,
        [Description("Y coordinate of the last belt tile")]
        double endY,
        [Description("For L-shaped routes only: 'horizontal_first' (go horizontal then turn vertical) or " +
                     "'vertical_first' (go vertical then turn horizontal). Ignored for straight lines. " +
                     "Choose based on which path avoids obstacles or connects better to existing belts.")]
        string turnPreference = "horizontal_first")
    {
        return planner.PlanRoute(startX, startY, endX, endY, turnPreference);
    }

    [McpServerTool, Description(
        "Get a reference guide explaining transport belt direction mechanics and placement tips. " +
        "Call this if you're unsure how belt directions work or need a reminder before placing belts.")]
    public static string GetBeltDirectionHelp()
    {
        return BeltPlannerService.GetBeltDirectionHelp();
    }
}
