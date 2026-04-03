namespace FactorioMCP.Models;

/// <summary>
/// A building placed by the AI that is tracked in memory for spatial queries and factory awareness.
/// Position coordinates match Factorio map coordinates.
/// </summary>
internal sealed class TrackedBuilding
{
    public required string EntityName { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public string Direction { get; set; } = "north";
    public string? Label { get; set; }
    public DateTime PlacedAt { get; init; } = DateTime.UtcNow;
}
