using System.Text.Json;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class BuildingMemoryServiceTests
{
    private static BuildingMemoryService CreateService() =>
        new(Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json"));

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ── TrackBuilding ────────────────────────────────────────────────

    [Fact]
    public async Task TrackBuildingAsync_TracksNewBuilding()
    {
        var service = CreateService();

        var result = Parse(await service.TrackBuildingAsync("stone-furnace", 5, -3, "south"));

        Assert.Equal("tracked", result.GetProperty("status").GetString());
        Assert.Equal("stone-furnace", result.GetProperty("entity_name").GetString());
        Assert.Equal(5, result.GetProperty("x").GetDouble());
        Assert.Equal(-3, result.GetProperty("y").GetDouble());
        Assert.Equal("south", result.GetProperty("direction").GetString());
        Assert.Equal(1, result.GetProperty("total_buildings").GetInt32());
    }

    [Fact]
    public async Task TrackBuildingAsync_DefaultsDirectionToNorth()
    {
        var service = CreateService();

        var result = Parse(await service.TrackBuildingAsync("transport-belt", 0, 0));

        Assert.Equal("north", result.GetProperty("direction").GetString());
    }

    [Fact]
    public async Task TrackBuildingAsync_ReplacesExistingAtSamePosition()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5, 5);

        var result = Parse(await service.TrackBuildingAsync("steel-furnace", 5, 5));

        Assert.Equal("tracked", result.GetProperty("status").GetString());
        Assert.Equal("steel-furnace", result.GetProperty("entity_name").GetString());
        Assert.Equal(1, result.GetProperty("total_buildings").GetInt32());
    }

    [Fact]
    public async Task TrackBuildingAsync_TracksMultipleBuildings()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("stone-furnace", 3, 0);

        var result = Parse(await service.TrackBuildingAsync("transport-belt", 6, 0));

        Assert.Equal(3, result.GetProperty("total_buildings").GetInt32());
    }

    [Fact]
    public async Task TrackBuildingAsync_ThrowsOnNullEntityName()
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.TrackBuildingAsync(null!, 0, 0));
    }

    [Fact]
    public async Task TrackBuildingAsync_ThrowsOnWhitespaceEntityName()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.TrackBuildingAsync("  ", 0, 0));
    }

    // ── UntrackBuildingAt ────────────────────────────────────────────

    [Fact]
    public async Task UntrackBuildingAtAsync_RemovesExistingBuilding()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5, -3);

        var result = Parse(await service.UntrackBuildingAtAsync(5, -3));

        Assert.Equal("untracked", result.GetProperty("status").GetString());
        Assert.Equal("stone-furnace", result.GetProperty("entity_name").GetString());
        Assert.Equal(0, result.GetProperty("total_buildings").GetInt32());
    }

    [Fact]
    public async Task UntrackBuildingAtAsync_ReturnsNotFoundForMissingPosition()
    {
        var service = CreateService();

        var result = Parse(await service.UntrackBuildingAtAsync(99, 99));

        Assert.Equal("not_found", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UntrackBuildingAtAsync_MatchesWithinTolerance()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5.0, -3.0);

        // Within 0.5 tolerance
        var result = Parse(await service.UntrackBuildingAtAsync(5.3, -3.2));

        Assert.Equal("untracked", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UntrackBuildingAtAsync_DoesNotMatchBeyondTolerance()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5.0, -3.0);

        // Beyond 0.5 tolerance
        var result = Parse(await service.UntrackBuildingAtAsync(5.6, -3.0));

        Assert.Equal("not_found", result.GetProperty("status").GetString());
    }

    // ── GetAllBuildings ──────────────────────────────────────────────

    [Fact]
    public async Task GetAllBuildingsAsync_ReturnsEmptyWhenNoneTracked()
    {
        var service = CreateService();

        var result = Parse(await service.GetAllBuildingsAsync());

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(0, result.GetProperty("count").GetInt32());
        Assert.Equal(0, result.GetProperty("buildings").GetArrayLength());
    }

    [Fact]
    public async Task GetAllBuildingsAsync_ReturnsAllTrackedBuildings()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0, "south");
        await service.TrackBuildingAsync("transport-belt", 3, 0, "east");

        var result = Parse(await service.GetAllBuildingsAsync());

        Assert.Equal(2, result.GetProperty("count").GetInt32());
        var buildings = result.GetProperty("buildings");
        Assert.Equal(2, buildings.GetArrayLength());
    }

    // ── GetBuildingsNear ─────────────────────────────────────────────

    [Fact]
    public async Task GetBuildingsNearAsync_ReturnsOnlyBuildingsWithinRadius()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("stone-furnace", 5, 0);
        await service.TrackBuildingAsync("stone-furnace", 100, 100);

        var result = Parse(await service.GetBuildingsNearAsync(0, 0, 10));

        Assert.Equal(2, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetBuildingsNearAsync_SortsByDistance()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("far-entity", 8, 0);
        await service.TrackBuildingAsync("close-entity", 2, 0);

        var result = Parse(await service.GetBuildingsNearAsync(0, 0, 20));

        var buildings = result.GetProperty("buildings");
        Assert.Equal("close-entity", buildings[0].GetProperty("entity_name").GetString());
        Assert.Equal("far-entity", buildings[1].GetProperty("entity_name").GetString());
    }

    [Fact]
    public async Task GetBuildingsNearAsync_IncludesDistanceInResponse()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 3, 4); // distance = 5

        var result = Parse(await service.GetBuildingsNearAsync(0, 0, 10));

        var building = result.GetProperty("buildings")[0];
        Assert.Equal(5.0, building.GetProperty("distance").GetDouble());
    }

    [Fact]
    public async Task GetBuildingsNearAsync_ThrowsOnInvalidRadius()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetBuildingsNearAsync(0, 0, 0));
    }

    [Fact]
    public async Task GetBuildingsNearAsync_ThrowsOnNegativeRadius()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetBuildingsNearAsync(0, 0, -5));
    }

    // ── FindBuildingsByType ──────────────────────────────────────────

    [Fact]
    public async Task FindBuildingsByTypeAsync_ReturnsMatchingBuildings()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("transport-belt", 3, 0);
        await service.TrackBuildingAsync("stone-furnace", 6, 0);

        var result = Parse(await service.FindBuildingsByTypeAsync("stone-furnace"));

        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.Equal("stone-furnace", result.GetProperty("entity_name").GetString());
    }

    [Fact]
    public async Task FindBuildingsByTypeAsync_IsCaseInsensitive()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);

        var result = Parse(await service.FindBuildingsByTypeAsync("Stone-Furnace"));

        Assert.Equal(1, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task FindBuildingsByTypeAsync_ReturnsEmptyForNoMatches()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);

        var result = Parse(await service.FindBuildingsByTypeAsync("steel-furnace"));

        Assert.Equal(0, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task FindBuildingsByTypeAsync_ThrowsOnNullEntityName()
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.FindBuildingsByTypeAsync(null!));
    }

    // ── GetBuildingSummary ───────────────────────────────────────────

    [Fact]
    public async Task GetBuildingSummaryAsync_ReturnsEmptyForNoBuildings()
    {
        var service = CreateService();

        var result = Parse(await service.GetBuildingSummaryAsync());

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(0, result.GetProperty("total_buildings").GetInt32());
        Assert.Equal(0, result.GetProperty("type_count").GetInt32());
    }

    [Fact]
    public async Task GetBuildingSummaryAsync_GroupsByType()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("stone-furnace", 3, 0);
        await service.TrackBuildingAsync("transport-belt", 6, 0);

        var result = Parse(await service.GetBuildingSummaryAsync());

        Assert.Equal(3, result.GetProperty("total_buildings").GetInt32());
        Assert.Equal(2, result.GetProperty("type_count").GetInt32());

        var types = result.GetProperty("types");
        // Ordered by count descending
        Assert.Equal("stone-furnace", types[0].GetProperty("entity_name").GetString());
        Assert.Equal(2, types[0].GetProperty("count").GetInt32());
        Assert.Equal("transport-belt", types[1].GetProperty("entity_name").GetString());
        Assert.Equal(1, types[1].GetProperty("count").GetInt32());
    }

    // ── UpdateBuildingLabel ──────────────────────────────────────────

    [Fact]
    public async Task UpdateBuildingLabelAsync_SetsLabel()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5, -3);

        var result = Parse(await service.UpdateBuildingLabelAsync(5, -3, "iron smelter #1"));

        Assert.Equal("updated", result.GetProperty("status").GetString());
        Assert.Equal("iron smelter #1", result.GetProperty("label").GetString());
    }

    [Fact]
    public async Task UpdateBuildingLabelAsync_ClearsLabel()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5, -3);
        await service.UpdateBuildingLabelAsync(5, -3, "old label");

        var result = Parse(await service.UpdateBuildingLabelAsync(5, -3, null));

        Assert.Equal("updated", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("label").ValueKind);
    }

    [Fact]
    public async Task UpdateBuildingLabelAsync_ReturnsNotFoundForMissingPosition()
    {
        var service = CreateService();

        var result = Parse(await service.UpdateBuildingLabelAsync(99, 99, "label"));

        Assert.Equal("not_found", result.GetProperty("status").GetString());
    }

    // ── ClearAllBuildings ───────────────────────────────────────────

    [Fact]
    public async Task ClearAllBuildingsAsync_RemovesAllBuildings()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("transport-belt", 3, 0);

        var result = Parse(await service.ClearAllBuildingsAsync());

        Assert.Equal("cleared", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("removed_count").GetInt32());

        // Verify empty
        var all = Parse(await service.GetAllBuildingsAsync());
        Assert.Equal(0, all.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ClearAllBuildingsAsync_HandlesEmptyMemory()
    {
        var service = CreateService();

        var result = Parse(await service.ClearAllBuildingsAsync());

        Assert.Equal("cleared", result.GetProperty("status").GetString());
        Assert.Equal(0, result.GetProperty("removed_count").GetInt32());
    }

    // ── Persistence ─────────────────────────────────────────────────

    [Fact]
    public async Task Buildings_PersistAcrossServiceInstances()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json");

        var service1 = new BuildingMemoryService(filePath);
        await service1.TrackBuildingAsync("stone-furnace", 5, -3, "south");
        await service1.TrackBuildingAsync("transport-belt", 10, 0, "east");

        // New instance loads from same file
        var service2 = new BuildingMemoryService(filePath);
        var result = Parse(await service2.GetAllBuildingsAsync());

        Assert.Equal(2, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Buildings_PersistsLabelChanges()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json");

        var service1 = new BuildingMemoryService(filePath);
        await service1.TrackBuildingAsync("stone-furnace", 5, -3);
        await service1.UpdateBuildingLabelAsync(5, -3, "main smelter");

        var service2 = new BuildingMemoryService(filePath);
        var result = Parse(await service2.FindBuildingsByTypeAsync("stone-furnace"));

        var building = result.GetProperty("buildings")[0];
        Assert.Equal("main smelter", building.GetProperty("label").GetString());
    }

    // ── GetClosestBuildingOfType ─────────────────────────────────────

    [Fact]
    public async Task GetClosestBuildingOfTypeAsync_ReturnsClosest()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 10, 0);
        await service.TrackBuildingAsync("stone-furnace", 30, 0);
        await service.TrackBuildingAsync("stone-furnace", 50, 0);

        var result = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));

        Assert.Equal("ok", result.GetProperty("status").GetString());
        var closest = result.GetProperty("closest");
        Assert.Equal(10, closest.GetProperty("x").GetDouble());
        Assert.Equal(0, closest.GetProperty("y").GetDouble());
        Assert.Equal(10.0, closest.GetProperty("distance").GetDouble());
        Assert.Equal(3, result.GetProperty("total_matches").GetInt32());
    }

    [Fact]
    public async Task GetClosestBuildingOfTypeAsync_ReturnsNotFoundForMissingType()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("transport-belt", 5, 5);

        var result = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));

        Assert.Equal("not_found", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetClosestBuildingOfTypeAsync_ReturnsNotFoundWhenEmpty()
    {
        var service = CreateService();

        var result = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));

        Assert.Equal("not_found", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetClosestBuildingOfTypeAsync_IncludesOthers()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5, 0);
        await service.TrackBuildingAsync("stone-furnace", 15, 0);

        var result = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("total_matches").GetInt32());
        var others = result.GetProperty("others");
        Assert.Equal(1, others.GetArrayLength());
        Assert.Equal(15, others[0].GetProperty("x").GetDouble());
    }

    [Fact]
    public async Task GetClosestBuildingOfTypeAsync_CaseInsensitiveMatch()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("Stone-Furnace", 10, 0);

        var result = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));

        Assert.Equal("ok", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetClosestBuildingOfTypeAsync_IncludesDirectionAndLabel()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5, 0, "east");
        await service.UpdateBuildingLabelAsync(5, 0, "iron smelter");

        var result = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));

        var closest = result.GetProperty("closest");
        Assert.Equal("east", closest.GetProperty("direction").GetString());
        Assert.Equal("iron smelter", closest.GetProperty("label").GetString());
    }

    // ── UpdateBuildingDirection ──────────────────────────────────────

    [Fact]
    public async Task UpdateBuildingDirectionAsync_UpdatesDirection()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("transport-belt", 5, 0, "north");

        await service.UpdateBuildingDirectionAsync(5, 0, "east");

        var result = Parse(await service.FindBuildingsByTypeAsync("transport-belt"));
        var building = result.GetProperty("buildings")[0];
        Assert.Equal("east", building.GetProperty("direction").GetString());
    }

    [Fact]
    public async Task UpdateBuildingDirectionAsync_NoOpForMissingPosition()
    {
        var service = CreateService();

        // Should not throw
        await service.UpdateBuildingDirectionAsync(99, 99, "east");
    }
}
