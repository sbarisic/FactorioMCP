using System.Text.Json;
using System.Text.Json.Serialization;
using FactorioMCP.Models;
using Microsoft.Extensions.Configuration;

namespace FactorioMCP.Services;

/// <summary>
/// Tracks buildings placed by the AI with JSON file persistence.
/// Provides spatial queries for factory awareness and planning.
/// State persists across server restarts.
/// </summary>
internal sealed class BuildingMemoryService
{
    /// <summary>
    /// Position tolerance for matching building coordinates (accounts for floating-point imprecision).
    /// </summary>
    private const double PositionTolerance = 0.5;

    private static readonly JsonSerializerOptions s_responseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private static readonly JsonSerializerOptions s_fileOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<TrackedBuilding>? _buildings;

    public BuildingMemoryService(IConfiguration configuration)
    {
        _filePath = configuration["FACTORIO_BUILDINGS_FILE"] ?? "buildings.json";
    }

    internal BuildingMemoryService(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Records a newly placed building in memory.
    /// </summary>
    public async Task<string> TrackBuildingAsync(
        string entityName,
        double x,
        double y,
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);

            // Remove any existing building at the same position (replacement)
            buildings.RemoveAll(b => IsAtPosition(b, x, y));

            var building = new TrackedBuilding
            {
                EntityName = entityName,
                X = x,
                Y = y,
                Direction = direction
            };

            buildings.Add(building);
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "tracked",
                building.EntityName,
                building.X,
                building.Y,
                building.Direction,
                TotalBuildings = buildings.Count
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes a building from memory at the specified position.
    /// </summary>
    public async Task<string> UntrackBuildingAtAsync(
        double x,
        double y,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var removed = buildings.FindIndex(b => IsAtPosition(b, x, y));

            if (removed < 0)
            {
                return Respond(new
                {
                    Status = "not_found",
                    X = x,
                    Y = y
                });
            }

            var building = buildings[removed];
            buildings.RemoveAt(removed);
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "untracked",
                building.EntityName,
                building.X,
                building.Y,
                TotalBuildings = buildings.Count
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns all tracked buildings.
    /// </summary>
    public async Task<string> GetAllBuildingsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);

            return Respond(new
            {
                Status = "ok",
                Count = buildings.Count,
                Buildings = buildings.Select(FormatBuilding)
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns buildings within a radius of the specified position.
    /// </summary>
    public async Task<string> GetBuildingsNearAsync(
        double x,
        double y,
        double radius = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var nearby = buildings
                .Where(b => DistanceTo(b, x, y) <= radius)
                .OrderBy(b => DistanceTo(b, x, y))
                .ToList();

            return Respond(new
            {
                Status = "ok",
                CenterX = x,
                CenterY = y,
                Radius = radius,
                Count = nearby.Count,
                Buildings = nearby.Select(b => new
                {
                    b.EntityName,
                    b.X,
                    b.Y,
                    b.Direction,
                    b.Label,
                    Distance = Math.Round(DistanceTo(b, x, y), 1)
                })
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns all buildings of a specific entity type.
    /// </summary>
    public async Task<string> FindBuildingsByTypeAsync(
        string entityName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var matches = buildings
                .Where(b => string.Equals(b.EntityName, entityName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Respond(new
            {
                Status = "ok",
                EntityName = entityName,
                Count = matches.Count,
                Buildings = matches.Select(FormatBuilding)
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns a summary of all building types and their counts.
    /// </summary>
    public async Task<string> GetBuildingSummaryAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var groups = buildings
                .GroupBy(b => b.EntityName)
                .Select(g => new { EntityName = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            return Respond(new
            {
                Status = "ok",
                TotalBuildings = buildings.Count,
                TypeCount = groups.Count,
                Types = groups
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sets or updates a label on the building at the specified position.
    /// </summary>
    public async Task<string> UpdateBuildingLabelAsync(
        double x,
        double y,
        string? label,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var building = buildings.Find(b => IsAtPosition(b, x, y));

            if (building is null)
            {
                return Respond(new
                {
                    Status = "not_found",
                    X = x,
                    Y = y
                });
            }

            building.Label = label;
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "updated",
                building.EntityName,
                building.X,
                building.Y,
                building.Label
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the closest building of the specified type to the given position.
    /// </summary>
    public async Task<string> GetClosestBuildingOfTypeAsync(
        string entityName,
        double playerX,
        double playerY,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var matches = buildings
                .Where(b => string.Equals(b.EntityName, entityName, StringComparison.OrdinalIgnoreCase))
                .Select(b => new
                {
                    b.EntityName,
                    b.X,
                    b.Y,
                    b.Direction,
                    b.Label,
                    Distance = Math.Round(DistanceTo(b, playerX, playerY), 1)
                })
                .OrderBy(b => b.Distance)
                .ToList();

            if (matches.Count == 0)
            {
                return Respond(new
                {
                    Status = "not_found",
                    EntityName = entityName,
                    Message = $"No tracked buildings of type '{entityName}' found"
                });
            }

            var closest = matches[0];

            return Respond(new
            {
                Status = "ok",
                Closest = closest,
                TotalMatches = matches.Count,
                Others = matches.Count > 1 ? matches.Skip(1).Take(3) : Enumerable.Empty<object>()
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Updates the direction of the tracked building at the specified position.
    /// </summary>
    public async Task UpdateBuildingDirectionAsync(
        double x,
        double y,
        string newDirection,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var building = buildings.Find(b => IsAtPosition(b, x, y));

            if (building is not null)
            {
                building.Direction = newDirection;
                await PersistAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes all tracked buildings from memory.
    /// </summary>
    public async Task<string> ClearAllBuildingsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var buildings = await EnsureLoadedAsync(cancellationToken);
            var count = buildings.Count;

            buildings.Clear();
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "cleared",
                RemovedCount = count
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static bool IsAtPosition(TrackedBuilding building, double x, double y)
    {
        return Math.Abs(building.X - x) < PositionTolerance
            && Math.Abs(building.Y - y) < PositionTolerance;
    }

    private static double DistanceTo(TrackedBuilding building, double x, double y)
    {
        var dx = building.X - x;
        var dy = building.Y - y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static object FormatBuilding(TrackedBuilding b)
    {
        return new
        {
            b.EntityName,
            b.X,
            b.Y,
            b.Direction,
            b.Label,
            b.PlacedAt
        };
    }

    private static string Respond(object data) =>
        JsonSerializer.Serialize(data, s_responseOptions);

    // ── Persistence ─────────────────────────────────────────────────

    private async Task<List<TrackedBuilding>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_buildings is not null)
            return _buildings;

        if (File.Exists(_filePath))
        {
            await using var stream = File.OpenRead(_filePath);
            _buildings = await JsonSerializer.DeserializeAsync<List<TrackedBuilding>>(stream, s_fileOptions, cancellationToken) ?? [];
        }
        else
        {
            _buildings = [];
        }

        return _buildings;
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        if (_buildings is null) return;

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, _buildings, s_fileOptions, cancellationToken);
    }
}
