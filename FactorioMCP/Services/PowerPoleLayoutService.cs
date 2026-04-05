using System.Text.Json;
using FactorioMCP.Models;

namespace FactorioMCP.Services;

/// <summary>
/// Pure C# geometry service for computing power pole placements.
/// Given entity positions that need electric power, computes minimum pole
/// placements at correct spacing intervals to cover all entities.
/// </summary>
internal sealed class PowerPoleLayoutService
{
    // Pole specifications: (supply_area_side, wire_reach)
    // supply_area_side is the side length of the square coverage area centered on the pole.
    // wire_reach is the maximum distance between two poles that can still connect.
    private static readonly Dictionary<string, (int SupplyAreaSide, double WireReach, int TileSize)> PoleSpecs = new()
    {
        ["small-electric-pole"]  = (5,  7.5, 1),
        ["medium-electric-pole"] = (7,  9.0, 1),
        ["big-electric-pole"]    = (4, 30.0, 2),
        ["substation"]           = (18, 18.0, 2),
    };

    /// <summary>
    /// Compute power pole placements to cover all given entity positions.
    /// Uses a greedy grid-based approach: places poles on a grid aligned to the
    /// supply area, then removes any poles that don't cover at least one entity.
    /// </summary>
    public string PlanPowerPoles(
        IReadOnlyList<PlacementInstruction> entities,
        string poleName = "small-electric-pole",
        double? existingPoleX = null,
        double? existingPoleY = null)
    {
        if (!PoleSpecs.TryGetValue(poleName, out var spec))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "unknown_pole",
                message = $"Unknown pole type '{poleName}'. Valid: {string.Join(", ", PoleSpecs.Keys)}"
            });
        }

        if (entities.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "no_entities",
                message = "No entities provided"
            });
        }

        // Filter to entities that need power (exclude belts, pipes, etc.)
        var needsPower = entities.Where(e => EntityNeedsPower(e.EntityName)).ToList();
        if (needsPower.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                success = true,
                pole_count = 0,
                message = "No entities require electric power (all are belts, pipes, or passive entities)",
                instructions = Array.Empty<PlacementInstruction>()
            });
        }

        // Supply radius: half the supply area side, minus 0.5 for tile center alignment
        double supplyRadius = spec.SupplyAreaSide / 2.0;

        // Grid spacing: place poles so their supply areas tile perfectly.
        // Max spacing for full coverage = supply_area_side (areas just touch).
        // Constrain by wire reach so poles stay connected.
        int gridSpacing = Math.Min(spec.SupplyAreaSide, (int)Math.Floor(spec.WireReach));

        // Compute bounding box of entities needing power
        double minX = needsPower.Min(e => e.X);
        double maxX = needsPower.Max(e => e.X);
        double minY = needsPower.Min(e => e.Y);
        double maxY = needsPower.Max(e => e.Y);

        // Determine grid origin — align to existing pole if provided, else to bounding box
        double gridOriginX, gridOriginY;
        if (existingPoleX.HasValue && existingPoleY.HasValue)
        {
            gridOriginX = existingPoleX.Value;
            gridOriginY = existingPoleY.Value;
        }
        else
        {
            // Start grid at bounding box center, snapped to integer
            gridOriginX = Math.Round((minX + maxX) / 2.0);
            gridOriginY = Math.Round((minY + maxY) / 2.0);
        }

        // Generate candidate pole positions on the grid covering the bounding box
        var candidates = new List<(double X, double Y)>();

        // Extend grid beyond bounding box by supplyRadius to catch edge entities
        double extMinX = minX - supplyRadius;
        double extMaxX = maxX + supplyRadius;
        double extMinY = minY - supplyRadius;
        double extMaxY = maxY + supplyRadius;

        // Calculate grid start indices
        int startI = (int)Math.Floor((extMinX - gridOriginX) / gridSpacing);
        int endI = (int)Math.Ceiling((extMaxX - gridOriginX) / gridSpacing);
        int startJ = (int)Math.Floor((extMinY - gridOriginY) / gridSpacing);
        int endJ = (int)Math.Ceiling((extMaxY - gridOriginY) / gridSpacing);

        for (int i = startI; i <= endI; i++)
        {
            for (int j = startJ; j <= endJ; j++)
            {
                double px = gridOriginX + i * gridSpacing;
                double py = gridOriginY + j * gridSpacing;
                candidates.Add((px, py));
            }
        }

        // Filter: keep only poles that cover at least one entity
        var poles = new List<(double X, double Y)>();
        foreach (var (px, py) in candidates)
        {
            bool coversAny = needsPower.Any(e =>
                Math.Abs(e.X - px) < supplyRadius &&
                Math.Abs(e.Y - py) < supplyRadius);

            if (coversAny)
                poles.Add((px, py));
        }

        // Verify all entities are covered
        var uncovered = new List<PlacementInstruction>();
        foreach (var entity in needsPower)
        {
            bool covered = poles.Any(p =>
                Math.Abs(entity.X - p.X) < supplyRadius &&
                Math.Abs(entity.Y - p.Y) < supplyRadius);

            // Also check existing pole coverage
            if (!covered && existingPoleX.HasValue && existingPoleY.HasValue)
            {
                covered = Math.Abs(entity.X - existingPoleX.Value) < supplyRadius &&
                          Math.Abs(entity.Y - existingPoleY.Value) < supplyRadius;
            }

            if (!covered)
                uncovered.Add(entity);
        }

        // Build placement instructions
        var instructions = poles
            .Select(p => new PlacementInstruction(poleName, p.X, p.Y, "north", "power_pole"))
            .ToList();

        var result = new
        {
            success = true,
            pole_name = poleName,
            pole_count = instructions.Count,
            entities_needing_power = needsPower.Count,
            uncovered_count = uncovered.Count,
            grid_spacing = gridSpacing,
            supply_radius = supplyRadius,
            wire_reach = spec.WireReach,
            instructions,
            uncovered_entities = uncovered.Count > 0
                ? uncovered.Select(e => new { e.EntityName, e.X, e.Y }).ToArray()
                : null
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// Determines if an entity type typically requires electric power.
    /// Passive entities (belts, pipes, walls, etc.) and burner entities don't need power poles.
    /// </summary>
    private static bool EntityNeedsPower(string entityName)
    {
        // Entities that DON'T need electricity
        if (entityName.Contains("transport-belt", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("splitter", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("underground-belt", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("pipe", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("wall", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("gate", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("stone-furnace", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("steel-furnace", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("burner", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("wooden-chest", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("iron-chest", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("steel-chest", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("electric-pole", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("substation", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("lamp", StringComparison.OrdinalIgnoreCase)) return false;
        if (entityName.Contains("radar", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("assembling-machine", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("electric-furnace", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("inserter", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("lab", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("mining-drill", StringComparison.OrdinalIgnoreCase))
            return !entityName.Contains("burner", StringComparison.OrdinalIgnoreCase);
        if (entityName.Contains("pump", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("beacon", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("roboport", StringComparison.OrdinalIgnoreCase)) return true;
        if (entityName.Contains("accumulator", StringComparison.OrdinalIgnoreCase)) return false;

        // Default: assume it needs power (safer)
        return true;
    }
}
