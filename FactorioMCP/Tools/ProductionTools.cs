using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using FactorioMCP.Models;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for factory layout planning and production design.
/// Generates placement instructions without actually building anything.
/// </summary>
[McpServerToolType]
internal sealed class ProductionTools(LayoutSynthesisService layout, PowerPoleLayoutService powerPoles, ProductionPlannerService planner, BlueprintCodecService codec, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Plan a standard smelter line layout. Generates a PlacementInstruction[] for a row of " +
        "furnaces with input belt, inbound inserters, furnaces, outbound inserters, and output belt. " +
        "Does NOT place anything — returns the blueprint as JSON. " +
        "Use the instructions with PlaceGhostBatch to create the layout as ghosts. " +
        "Default pattern: input belt (west) → inserter → furnace → inserter → output belt (east), stacked south.")]
    public Task<string> PlanSmelterLine(
        [Description("X coordinate of the first furnace center")]
        double originX,
        [Description("Y coordinate of the first furnace center")]
        double originY,
        [Description("Number of furnaces in the line")]
        int furnaceCount,
        [Description("Furnace entity name (default 'stone-furnace'). Use 'steel-furnace' or 'electric-furnace' for upgrades.")]
        string furnaceName = "stone-furnace",
        [Description("Inserter entity name (default 'burner-inserter'). Use 'inserter' or 'fast-inserter' for electric setups.")]
        string inserterName = "burner-inserter",
        [Description("Belt entity name (default 'transport-belt'). Use 'fast-transport-belt' or 'express-transport-belt' for higher throughput.")]
        string beltName = "transport-belt",
        [Description("Stacking direction: 'south' (default, furnaces stack downward), 'north', 'east', 'west'")]
        string direction = "south",
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlanSmelterLine),
            _ => Task.FromResult(layout.PlanSmelterLine(originX, originY, furnaceCount, furnaceName, inserterName, beltName, direction)),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Plan a complete production chain from raw resources to a target item at a target rate. " +
        "Expands the full recipe tree, calculates machine counts per stage, determines belt tiers, " +
        "and locates nearby resource patches for raw inputs. " +
        "Does NOT build anything — returns a structured plan. " +
        "Use this to design a factory before placing any entities.")]
    public Task<string> PlanProduction(
        [Description("Target item to produce (e.g. 'iron-gear-wheel', 'electronic-circuit', 'automation-science-pack')")]
        string targetItem,
        [Description("Target production rate in items per second (e.g. 1.0)")]
        double targetRatePerSecond,
        [Description("Optional machine override — forces all stages to use this machine. " +
                     "If omitted, auto-selects based on recipe category (assembling-machine for crafting, stone-furnace for smelting, etc.)")]
        string? machineOverride = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlanProduction),
            ct => planner.PlanProductionAsync(targetItem, targetRatePerSecond, machineOverride, ct),
            cancellationToken);
    }

    [McpServerTool, Description(
        "Export placement instructions as a Factorio blueprint string. " +
        "Takes a JSON array of placement instructions (from PlanSmelterLine or manual layout) " +
        "and converts them into a blueprint string that can be imported into the game or shared. " +
        "Use this to turn planned layouts into importable blueprints.")]
    public Task<string> ExportLayoutAsBlueprint(
        [Description("JSON array of placement instructions: [{\"entity_name\":\"stone-furnace\",\"x\":0,\"y\":0,\"direction\":\"north\"}, ...]. " +
                     "Compatible with PlanSmelterLine output's 'instructions' array.")]
        string instructionsJson,
        [Description("Optional label for the blueprint")]
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ExportLayoutAsBlueprint),
            _ =>
            {
                try
                {
                    var instructions = JsonSerializer.Deserialize<List<PlacementInstruction>>(instructionsJson,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                    if (instructions is null || instructions.Count == 0)
                        return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "empty_instructions", message = "No placement instructions provided" }));

                    return Task.FromResult(codec.ExportAsBlueprint(instructions, label));
                }
                catch (JsonException ex)
                {
                    return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "invalid_json", message = ex.Message }));
                }
            },
            cancellationToken);
    }

    [McpServerTool, Description(
        "Compute power pole placements to cover all entities in a layout. " +
        "Given a PlacementInstruction[] (from PlanSmelterLine or manual layout), " +
        "calculates the minimum set of power poles needed to supply electricity to all " +
        "entities that require it. Automatically filters out passive entities (belts, chests, burner entities). " +
        "Returns PlacementInstruction[] for poles only. Use with PlaceGhostBatch to plan pole placement.")]
    public Task<string> PlanPowerPoles(
        [Description("JSON array of placement instructions: [{\"entity_name\":\"assembling-machine-1\",\"x\":0,\"y\":0}, ...]. " +
                     "Compatible with PlanSmelterLine output's 'instructions' array.")]
        string instructionsJson,
        [Description("Pole type to use: 'small-electric-pole' (default, 5×5 area), " +
                     "'medium-electric-pole' (7×7), 'big-electric-pole' (4×4, long reach), 'substation' (18×18)")]
        string poleName = "small-electric-pole",
        [Description("Optional X coordinate of an existing pole to align the grid to (ensures connectivity)")]
        double? existingPoleX = null,
        [Description("Optional Y coordinate of an existing pole to align the grid to")]
        double? existingPoleY = null,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(PlanPowerPoles),
            _ =>
            {
                try
                {
                    var instructions = JsonSerializer.Deserialize<List<PlacementInstruction>>(instructionsJson,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                    if (instructions is null || instructions.Count == 0)
                        return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "empty_instructions", message = "No placement instructions provided" }));

                    return Task.FromResult(powerPoles.PlanPowerPoles(instructions, poleName, existingPoleX, existingPoleY));
                }
                catch (JsonException ex)
                {
                    return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "invalid_json", message = ex.Message }));
                }
            },
            cancellationToken);
    }
}
