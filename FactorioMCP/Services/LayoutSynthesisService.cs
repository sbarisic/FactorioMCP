using System.Text.Json;
using FactorioMCP.Models;

namespace FactorioMCP.Services;

/// <summary>
/// Pure C# geometry service for synthesizing standard factory layout patterns.
/// Generates PlacementInstruction arrays without placing anything in the game.
/// </summary>
internal sealed class LayoutSynthesisService
{
    /// <summary>
    /// Plan a standard smelter line layout. Pattern:
    /// input belt (west) → inbound inserter → furnace → outbound inserter → output belt (east).
    /// Furnaces stacked vertically, power poles every 7 tiles.
    /// </summary>
    public string PlanSmelterLine(
        double originX,
        double originY,
        int furnaceCount,
        string furnaceName = "stone-furnace",
        string inserterName = "burner-inserter",
        string beltName = "transport-belt",
        string direction = "south")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(furnaceCount);

        // Furnace dimensions (stone-furnace and steel-furnace are 2x2, electric-furnace is 3x3)
        bool isElectric = furnaceName.Contains("electric", StringComparison.OrdinalIgnoreCase);
        int furnaceSize = isElectric ? 3 : 2;

        // Layout direction: furnaces stack along this axis
        bool vertical = direction is "south" or "north";
        int sign = direction is "south" or "east" ? 1 : -1;

        var instructions = new List<PlacementInstruction>();

        for (int i = 0; i < furnaceCount; i++)
        {
            // Furnace center offset along the stacking axis
            double stackOffset = i * (furnaceSize + 1) * sign;

            double fx, fy;
            if (vertical)
            {
                fx = originX;
                fy = originY + stackOffset;
            }
            else
            {
                fx = originX + stackOffset;
                fy = originY;
            }

            // Furnace position (center of 2x2 is at +0.5, +0.5 from top-left)
            double furnaceCenterX = fx;
            double furnaceCenterY = fy;

            if (vertical)
            {
                // Input belt is west of furnace, output belt is east
                double inputBeltX = furnaceCenterX - furnaceSize;
                double outputBeltX = furnaceCenterX + furnaceSize;
                double inboundInserterX = furnaceCenterX - (furnaceSize / 2.0 + 0.5);
                double outboundInserterX = furnaceCenterX + (furnaceSize / 2.0 + 0.5);

                // For 2x2 furnaces, furnace takes 2 tiles vertically
                // Place inserters and belts at the furnace center Y
                double rowY = furnaceCenterY;

                // Input belt — flows south (toward the furnace row)
                instructions.Add(new PlacementInstruction(beltName, inputBeltX, rowY, "south", "input_belt"));

                // Inbound inserter — picks from west (input belt), drops east (into furnace)
                instructions.Add(new PlacementInstruction(inserterName, inboundInserterX, rowY, "west", "inbound_inserter"));

                // Furnace
                instructions.Add(new PlacementInstruction(furnaceName, furnaceCenterX, furnaceCenterY, "north", "furnace"));

                // Outbound inserter — picks from west (furnace), drops east (onto output belt)
                instructions.Add(new PlacementInstruction(inserterName, outboundInserterX, rowY, "west", "outbound_inserter"));

                // Output belt — flows south
                instructions.Add(new PlacementInstruction(beltName, outputBeltX, rowY, "south", "output_belt"));
            }
            else
            {
                // Input belt is north of furnace, output belt is south
                double inputBeltY = furnaceCenterY - furnaceSize;
                double outputBeltY = furnaceCenterY + furnaceSize;
                double inboundInserterY = furnaceCenterY - (furnaceSize / 2.0 + 0.5);
                double outboundInserterY = furnaceCenterY + (furnaceSize / 2.0 + 0.5);

                double colX = furnaceCenterX;

                instructions.Add(new PlacementInstruction(beltName, colX, inputBeltY, "east", "input_belt"));
                instructions.Add(new PlacementInstruction(inserterName, colX, inboundInserterY, "north", "inbound_inserter"));
                instructions.Add(new PlacementInstruction(furnaceName, furnaceCenterX, furnaceCenterY, "north", "furnace"));
                instructions.Add(new PlacementInstruction(inserterName, colX, outboundInserterY, "north", "outbound_inserter"));
                instructions.Add(new PlacementInstruction(beltName, colX, outputBeltY, "east", "output_belt"));
            }

            // Power pole every 7 furnaces (or at the first furnace)
            if (i % 7 == 0 && !inserterName.StartsWith("burner", StringComparison.OrdinalIgnoreCase))
            {
                double poleX, poleY;
                if (vertical)
                {
                    // Place pole east of the output belt
                    poleX = furnaceCenterX + furnaceSize + 1;
                    poleY = furnaceCenterY;
                }
                else
                {
                    poleX = furnaceCenterX;
                    poleY = furnaceCenterY + furnaceSize + 1;
                }
                instructions.Add(new PlacementInstruction("small-electric-pole", poleX, poleY, "north", "power_pole"));
            }
        }

        // Fill in belt tiles between furnace rows to make continuous lines
        if (furnaceCount > 1 && vertical)
        {
            for (int i = 0; i < furnaceCount - 1; i++)
            {
                double stackOffset = i * (furnaceSize + 1) * sign;
                double nextOffset = (i + 1) * (furnaceSize + 1) * sign;
                double inputBeltX = originX - furnaceSize;
                double outputBeltX = originX + furnaceSize;

                // Fill belt gap between furnace rows
                double startY = originY + stackOffset + sign;
                double endY = originY + nextOffset;

                double y = startY;
                while ((sign > 0 && y < endY) || (sign < 0 && y > endY))
                {
                    instructions.Add(new PlacementInstruction(beltName, inputBeltX, y, "south", "input_belt"));
                    instructions.Add(new PlacementInstruction(beltName, outputBeltX, y, "south", "output_belt"));
                    y += sign;
                }
            }
        }

        var result = new
        {
            success = true,
            furnace_count = furnaceCount,
            furnace_name = furnaceName,
            inserter_name = inserterName,
            belt_name = beltName,
            direction,
            origin_x = originX,
            origin_y = originY,
            instruction_count = instructions.Count,
            instructions
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
    }
}
