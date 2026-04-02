using System.Text.Json;

namespace FactorioMCP.Services;

/// <summary>
/// Pure computation service for planning transport belt routes between positions.
/// Calculates belt tile positions and directions for straight and L-shaped paths.
/// No RCON calls — all logic is C#-side geometry.
/// </summary>
internal sealed class BeltPlannerService
{
    private static readonly string[] ValidDirections =
        ["north", "south", "east", "west"];

    /// <summary>
    /// Plan a belt route from a start position to an end position.
    /// Items flow from start → end. Supports straight lines and L-shaped routes
    /// (one 90° turn). Returns an ordered list of belt tile positions with their
    /// facing directions so the AI can place them via <c>PlaceEntity</c>.
    /// </summary>
    /// <param name="startX">X of the first belt tile (where items enter the belt line)</param>
    /// <param name="startY">Y of the first belt tile</param>
    /// <param name="endX">X of the last belt tile (where items exit the belt line)</param>
    /// <param name="endY">Y of the last belt tile</param>
    /// <param name="turnPreference">For L-shaped routes: "horizontal_first" (go horizontal then vertical) or "vertical_first" (go vertical then horizontal). Ignored for straight lines. Defaults to "horizontal_first".</param>
    public string PlanRoute(double startX, double startY, double endX, double endY, string turnPreference = "horizontal_first")
    {
        int sx = (int)Math.Round(startX);
        int sy = (int)Math.Round(startY);
        int ex = (int)Math.Round(endX);
        int ey = (int)Math.Round(endY);

        if (sx == ex && sy == ey)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "same_position",
                message = "Start and end positions are the same tile"
            });
        }

        var steps = new List<BeltStep>();

        bool isStraightHorizontal = sy == ey;
        bool isStraightVertical = sx == ex;

        if (isStraightHorizontal)
        {
            // Straight horizontal line
            string dir = ex > sx ? "east" : "west";
            int step = ex > sx ? 1 : -1;
            for (int x = sx; x != ex + step; x += step)
            {
                steps.Add(new BeltStep(x, sy, dir));
            }
        }
        else if (isStraightVertical)
        {
            // Straight vertical line
            string dir = ey > sy ? "south" : "north";
            int step = ey > sy ? 1 : -1;
            for (int y = sy; y != ey + step; y += step)
            {
                steps.Add(new BeltStep(sx, y, dir));
            }
        }
        else
        {
            // L-shaped route with one turn
            bool horizontalFirst = !string.Equals(turnPreference, "vertical_first", StringComparison.OrdinalIgnoreCase);

            if (horizontalFirst)
            {
                // Horizontal leg first, then vertical
                string hDir = ex > sx ? "east" : "west";
                string vDir = ey > sy ? "south" : "north";
                int hStep = ex > sx ? 1 : -1;
                int vStep = ey > sy ? 1 : -1;

                // Horizontal segment (excluding the corner tile)
                for (int x = sx; x != ex; x += hStep)
                {
                    steps.Add(new BeltStep(x, sy, hDir));
                }

                // Corner tile faces the vertical direction (turns the belt)
                steps.Add(new BeltStep(ex, sy, vDir));

                // Vertical segment (after the corner)
                for (int y = sy + vStep; y != ey + vStep; y += vStep)
                {
                    steps.Add(new BeltStep(ex, y, vDir));
                }
            }
            else
            {
                // Vertical leg first, then horizontal
                string vDir = ey > sy ? "south" : "north";
                string hDir = ex > sx ? "east" : "west";
                int vStep = ey > sy ? 1 : -1;
                int hStep = ex > sx ? 1 : -1;

                // Vertical segment (excluding the corner tile)
                for (int y = sy; y != ey; y += vStep)
                {
                    steps.Add(new BeltStep(sx, y, vDir));
                }

                // Corner tile faces the horizontal direction (turns the belt)
                steps.Add(new BeltStep(sx, ey, hDir));

                // Horizontal segment (after the corner)
                for (int x = sx + hStep; x != ex + hStep; x += hStep)
                {
                    steps.Add(new BeltStep(x, ey, hDir));
                }
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            route_type = isStraightHorizontal || isStraightVertical ? "straight" : "L-shaped",
            turn_preference = isStraightHorizontal || isStraightVertical ? (string?)null : turnPreference,
            belt_count = steps.Count,
            steps
        });
    }

    /// <summary>
    /// Returns a description of belt direction mechanics.
    /// </summary>
    public static string GetBeltDirectionHelp()
    {
        return JsonSerializer.Serialize(new
        {
            directions = new[]
            {
                new { direction = "north", items_flow = "upward (decreasing Y)", from = "south side", to = "north side" },
                new { direction = "south", items_flow = "downward (increasing Y)", from = "north side", to = "south side" },
                new { direction = "east", items_flow = "rightward (increasing X)", from = "west side", to = "east side" },
                new { direction = "west", items_flow = "leftward (decreasing X)", from = "east side", to = "west side" }
            },
            tips = new[]
            {
                "Belt direction = the direction items FLOW (the way the arrows point)",
                "Place belts from source to destination — each tile faces the flow direction",
                "At corners/turns, the belt at the turn point faces the NEW direction",
                "Belts have two lanes (left and right side) — items stay on their lane",
                "Use PlanBeltRoute to calculate exact positions and directions for a belt path",
                "Underground belts skip tiles — place entry facing flow direction, exit facing same direction"
            }
        });
    }

    private record BeltStep(int x, int y, string direction);
}
