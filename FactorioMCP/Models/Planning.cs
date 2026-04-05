using System.Text.Json.Serialization;

namespace FactorioMCP.Models;

/// <summary>
/// A single entity to be placed as part of a build plan.
/// Returned by layout synthesis tools (e.g. PlanSmelterLine) and consumed
/// by build execution tools (e.g. PlaceGhostBatch).
/// </summary>
internal sealed record PlacementInstruction(
    [property: JsonPropertyName("entity_name")] string EntityName,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("direction")] string Direction = "north",
    [property: JsonPropertyName("role")] string? Role = null,
    [property: JsonPropertyName("recipe")] string? Recipe = null);

/// <summary>
/// A complete production plan with stages from raw resources to final product.
/// Returned by the production planner tool.
/// </summary>
internal sealed record ProductionPlan(
    [property: JsonPropertyName("target_item")] string TargetItem,
    [property: JsonPropertyName("target_rate")] double TargetRate,
    [property: JsonPropertyName("stages")] IReadOnlyList<ProductionStage> Stages);

/// <summary>
/// A single stage in a production plan (e.g. "smelt iron-ore → iron-plate").
/// </summary>
internal sealed record ProductionStage(
    [property: JsonPropertyName("input_item")] string InputItem,
    [property: JsonPropertyName("output_item")] string OutputItem,
    [property: JsonPropertyName("recipe")] string Recipe,
    [property: JsonPropertyName("machine_type")] string MachineType,
    [property: JsonPropertyName("machine_count")] int MachineCount,
    [property: JsonPropertyName("input_rate")] double InputRate,
    [property: JsonPropertyName("output_rate")] double OutputRate,
    [property: JsonPropertyName("belt_tier")] string BeltTier = "transport-belt");
