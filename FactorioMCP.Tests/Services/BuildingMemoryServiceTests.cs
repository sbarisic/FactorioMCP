using System.Text.Json;
using FactorioMCP.Models;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class BuildingMemoryServiceTests
{
    private static BuildingMemoryService CreateService() =>
        new(Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json"));

    private static BuildingMemoryService CreateServiceWithRcon(ScriptedRconClient rcon) =>
        new(Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json"), rcon);

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

    // ── FindClosestBuilding ─────────────────────────────────────────

    [Fact]
    public async Task FindClosestBuildingAsync_FindsByLabelContains()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 10, 0);
        await service.UpdateBuildingLabelAsync(10, 0, "main iron smelter");

        var result = await service.FindClosestBuildingAsync("iron smelter", 0, 0);

        Assert.NotNull(result);
        Assert.Equal("stone-furnace", result.EntityName);
        Assert.Equal(10, result.X);
    }

    [Fact]
    public async Task FindClosestBuildingAsync_FindsByEntityName()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("wooden-chest", 5, 5);

        var result = await service.FindClosestBuildingAsync("wooden-chest", 0, 0);

        Assert.NotNull(result);
        Assert.Equal("wooden-chest", result.EntityName);
    }

    [Fact]
    public async Task FindClosestBuildingAsync_LabelTakesPriorityOverEntityName()
    {
        var service = CreateService();
        // Building A: has matching label, farther away
        await service.TrackBuildingAsync("stone-furnace", 20, 0);
        await service.UpdateBuildingLabelAsync(20, 0, "iron output");
        // Building B: entity name "iron-chest", closer
        await service.TrackBuildingAsync("iron-chest", 5, 0);

        // Search for "iron" — should match label "iron output" first, not entity name "iron-chest"
        var result = await service.FindClosestBuildingAsync("iron output", 0, 0);

        Assert.NotNull(result);
        Assert.Equal("stone-furnace", result.EntityName);
        Assert.Equal(20, result.X);
    }

    [Fact]
    public async Task FindClosestBuildingAsync_ReturnsClosestWhenMultipleMatch()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 30, 0);
        await service.TrackBuildingAsync("stone-furnace", 10, 0);
        await service.TrackBuildingAsync("stone-furnace", 50, 0);

        var result = await service.FindClosestBuildingAsync("stone-furnace", 0, 0);

        Assert.NotNull(result);
        Assert.Equal(10, result.X);
    }

    [Fact]
    public async Task FindClosestBuildingAsync_ReturnsNullWhenNotFound()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("transport-belt", 5, 0);

        var result = await service.FindClosestBuildingAsync("stone-furnace", 0, 0);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindClosestBuildingAsync_CaseInsensitiveLabel()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 5, 0);
        await service.UpdateBuildingLabelAsync(5, 0, "Main Smelter");

        var result = await service.FindClosestBuildingAsync("main smelter", 0, 0);

        Assert.NotNull(result);
        Assert.Equal("Main Smelter", result.Label);
    }

    [Fact]
    public async Task FindClosestBuildingAsync_CaseInsensitiveEntityName()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("Stone-Furnace", 5, 0);

        var result = await service.FindClosestBuildingAsync("stone-furnace", 0, 0);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindClosestBuildingAsync_ThrowsOnNullSearchTerm()
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.FindClosestBuildingAsync(null!, 0, 0));
    }

    [Fact]
    public async Task FindClosestBuildingAsync_ReturnsNullWhenEmpty()
    {
        var service = CreateService();

        var result = await service.FindClosestBuildingAsync("stone-furnace", 0, 0);

        Assert.Null(result);
    }

    // ── ValidateBuildingMemory ───────────────────────────────────────

    [Fact]
    public async Task ValidateBuildingMemoryAsync_ReturnsErrorWhenNoRcon()
    {
        var service = CreateService(); // No RCON client

        var result = Parse(await service.ValidateBuildingMemoryAsync());

        Assert.Equal("error", result.GetProperty("status").GetString());
        Assert.Equal("no_rcon", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidateBuildingMemoryAsync_ReturnsOkForEmptyMemory()
    {
        var rcon = new ScriptedRconClient([""]); // Won't be called
        var service = CreateServiceWithRcon(rcon);

        var result = Parse(await service.ValidateBuildingMemoryAsync());

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(0, result.GetProperty("validated").GetInt32());
        Assert.Equal(0, result.GetProperty("removed").GetInt32());
        Assert.Equal(0, result.GetProperty("remaining").GetInt32());
        Assert.Empty(rcon.AllCommands); // No RCON call made
    }

    [Fact]
    public async Task ValidateBuildingMemoryAsync_KeepsAllWhenAllExist()
    {
        var rcon = new ScriptedRconClient(["1,1,1"]);
        var service = CreateServiceWithRcon(rcon);
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("transport-belt", 5, 0);
        await service.TrackBuildingAsync("inserter", 10, 0);

        var result = Parse(await service.ValidateBuildingMemoryAsync());

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(3, result.GetProperty("validated").GetInt32());
        Assert.Equal(0, result.GetProperty("removed").GetInt32());
        Assert.Equal(3, result.GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task ValidateBuildingMemoryAsync_RemovesMissingBuildings()
    {
        // First building exists, second missing, third exists
        var rcon = new ScriptedRconClient(["1,0,1"]);
        var service = CreateServiceWithRcon(rcon);
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("transport-belt", 5, 0); // This one is "missing"
        await service.TrackBuildingAsync("inserter", 10, 0);

        var result = Parse(await service.ValidateBuildingMemoryAsync());

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(3, result.GetProperty("validated").GetInt32());
        Assert.Equal(1, result.GetProperty("removed").GetInt32());
        Assert.Equal(2, result.GetProperty("remaining").GetInt32());

        // Verify the missing building was actually removed from memory
        var all = Parse(await service.GetAllBuildingsAsync());
        Assert.Equal(2, all.GetProperty("count").GetInt32());
        var buildings = all.GetProperty("buildings");
        Assert.Equal("stone-furnace", buildings[0].GetProperty("entity_name").GetString());
        Assert.Equal("inserter", buildings[1].GetProperty("entity_name").GetString());
    }

    [Fact]
    public async Task ValidateBuildingMemoryAsync_RemovesAllWhenAllMissing()
    {
        var rcon = new ScriptedRconClient(["0,0"]);
        var service = CreateServiceWithRcon(rcon);
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("transport-belt", 5, 0);

        var result = Parse(await service.ValidateBuildingMemoryAsync());

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("validated").GetInt32());
        Assert.Equal(2, result.GetProperty("removed").GetInt32());
        Assert.Equal(0, result.GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task ValidateBuildingMemoryAsync_HandlesEmptyRconResponse()
    {
        // Empty response = treat all as missing
        var rcon = new ScriptedRconClient([""]);
        var service = CreateServiceWithRcon(rcon);
        await service.TrackBuildingAsync("stone-furnace", 0, 0);

        var result = Parse(await service.ValidateBuildingMemoryAsync());

        Assert.Equal(1, result.GetProperty("removed").GetInt32());
        Assert.Equal(0, result.GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task ValidateBuildingMemoryAsync_SendsSingleRconCall()
    {
        var rcon = new ScriptedRconClient(["1,1"]);
        var service = CreateServiceWithRcon(rcon);
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("transport-belt", 5, 0);

        await service.ValidateBuildingMemoryAsync();

        // TrackBuildingAsync doesn't call RCON, so only the validation call should be present
        Assert.Single(rcon.AllCommands);
        // Verify the Lua script contains both building positions
        var luaCommand = rcon.AllCommands[0];
        Assert.Contains("stone-furnace", luaCommand);
        Assert.Contains("transport-belt", luaCommand);
        Assert.Contains("find_entities_filtered", luaCommand);
    }

    [Fact]
    public async Task ValidateBuildingMemoryAsync_PersistsAfterRemoval()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json");

        var rcon = new ScriptedRconClient(["1,0"]); // Second building missing
        var service1 = new BuildingMemoryService(filePath, rcon);
        await service1.TrackBuildingAsync("stone-furnace", 0, 0);
        await service1.TrackBuildingAsync("transport-belt", 5, 0);
        await service1.ValidateBuildingMemoryAsync();

        // New instance should only have the surviving building
        var service2 = new BuildingMemoryService(filePath);
        var result = Parse(await service2.GetAllBuildingsAsync());
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.Equal("stone-furnace",
            result.GetProperty("buildings")[0].GetProperty("entity_name").GetString());
    }

    // ── BuildValidationLua ──────────────────────────────────────────

    [Fact]
    public void BuildValidationLua_GeneratesCorrectScript()
    {
        var buildings = new List<TrackedBuilding>
        {
            new() { EntityName = "stone-furnace", X = 5, Y = -3 },
            new() { EntityName = "transport-belt", X = 10.5, Y = 0 }
        };

        var lua = BuildingMemoryService.BuildValidationLua(buildings);

        Assert.Contains("stone-furnace", lua);
        Assert.Contains("transport-belt", lua);
        Assert.Contains("x=5", lua);
        Assert.Contains("y=-3", lua);
        Assert.Contains("find_entities_filtered", lua);
        Assert.Contains("table.concat(results", lua);
    }

    [Fact]
    public void BuildValidationLua_HandlesSingleBuilding()
    {
        var buildings = new List<TrackedBuilding>
        {
            new() { EntityName = "inserter", X = 0, Y = 0 }
        };

        var lua = BuildingMemoryService.BuildValidationLua(buildings);

        Assert.Contains("inserter", lua);
        Assert.DoesNotContain(",{", lua); // Only one entry, no comma separator between entries
    }

    // ── ParseValidationResponse ─────────────────────────────────────

    [Fact]
    public void ParseValidationResponse_ParsesAllPresent()
    {
        var results = BuildingMemoryService.ParseValidationResponse("1,1,1", 3);

        Assert.Equal(3, results.Length);
        Assert.All(results, r => Assert.True(r));
    }

    [Fact]
    public void ParseValidationResponse_ParsesAllMissing()
    {
        var results = BuildingMemoryService.ParseValidationResponse("0,0,0", 3);

        Assert.Equal(3, results.Length);
        Assert.All(results, r => Assert.False(r));
    }

    [Fact]
    public void ParseValidationResponse_ParsesMixed()
    {
        var results = BuildingMemoryService.ParseValidationResponse("1,0,1,0", 4);

        Assert.True(results[0]);
        Assert.False(results[1]);
        Assert.True(results[2]);
        Assert.False(results[3]);
    }

    [Fact]
    public void ParseValidationResponse_HandlesEmptyResponse()
    {
        var results = BuildingMemoryService.ParseValidationResponse("", 3);

        Assert.Equal(3, results.Length);
        Assert.All(results, r => Assert.False(r));
    }

    [Fact]
    public void ParseValidationResponse_HandlesWhitespaceInResponse()
    {
        var results = BuildingMemoryService.ParseValidationResponse(" 1 , 0 , 1 ", 3);

        Assert.True(results[0]);
        Assert.False(results[1]);
        Assert.True(results[2]);
    }

    [Fact]
    public void ParseValidationResponse_HandlesShorterResponse()
    {
        // If RCON returns fewer values than expected, missing values are false
        var results = BuildingMemoryService.ParseValidationResponse("1", 3);

        Assert.Equal(3, results.Length);
        Assert.True(results[0]);
        Assert.False(results[1]);
        Assert.False(results[2]);
    }
}
