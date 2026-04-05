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

    [Fact]
    public async Task TrackAndUntrack_FullLifecycle()
    {
        var service = CreateService();

        // Track new building with explicit direction
        var result = Parse(await service.TrackBuildingAsync("stone-furnace", 5, -3, "south"));
        Assert.Equal("tracked", result.GetProperty("status").GetString());
        Assert.Equal("stone-furnace", result.GetProperty("entity_name").GetString());
        Assert.Equal(5, result.GetProperty("x").GetDouble());
        Assert.Equal(-3, result.GetProperty("y").GetDouble());
        Assert.Equal("south", result.GetProperty("direction").GetString());
        Assert.Equal(1, result.GetProperty("total_buildings").GetInt32());

        // Default direction is "north"
        result = Parse(await service.TrackBuildingAsync("transport-belt", 0, 0));
        Assert.Equal("north", result.GetProperty("direction").GetString());

        // Track at same position replaces
        result = Parse(await service.TrackBuildingAsync("steel-furnace", 5, -3));
        Assert.Equal("tracked", result.GetProperty("status").GetString());
        Assert.Equal("steel-furnace", result.GetProperty("entity_name").GetString());
        Assert.Equal(2, result.GetProperty("total_buildings").GetInt32());

        // Track multiple buildings
        result = Parse(await service.TrackBuildingAsync("inserter", 10, 0));
        Assert.Equal(3, result.GetProperty("total_buildings").GetInt32());

        // Untrack existing building
        result = Parse(await service.UntrackBuildingAtAsync(0, 0));
        Assert.Equal("untracked", result.GetProperty("status").GetString());
        Assert.Equal("transport-belt", result.GetProperty("entity_name").GetString());
        Assert.Equal(2, result.GetProperty("total_buildings").GetInt32());

        // Untrack missing returns not_found
        result = Parse(await service.UntrackBuildingAtAsync(99, 99));
        Assert.Equal("not_found", result.GetProperty("status").GetString());

        // Tolerance matching: within 0.5 matches
        await service.TrackBuildingAsync("stone-furnace", 5.0, -3.0);
        result = Parse(await service.UntrackBuildingAtAsync(5.3, -3.2));
        Assert.Equal("untracked", result.GetProperty("status").GetString());

        // Beyond 0.5 tolerance does not match
        await service.TrackBuildingAsync("stone-furnace", 5.0, -3.0);
        result = Parse(await service.UntrackBuildingAtAsync(5.6, -3.0));
        Assert.Equal("not_found", result.GetProperty("status").GetString());

        // Label: set
        result = Parse(await service.UpdateBuildingLabelAsync(5, -3, "iron smelter #1"));
        Assert.Equal("updated", result.GetProperty("status").GetString());
        Assert.Equal("iron smelter #1", result.GetProperty("label").GetString());

        // Label: clear
        result = Parse(await service.UpdateBuildingLabelAsync(5, -3, null));
        Assert.Equal("updated", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("label").ValueKind);

        // Label: not_found for missing position
        result = Parse(await service.UpdateBuildingLabelAsync(99, 99, "label"));
        Assert.Equal("not_found", result.GetProperty("status").GetString());

        // Update direction
        await service.TrackBuildingAsync("transport-belt", 20, 0, "north");
        await service.UpdateBuildingDirectionAsync(20, 0, "east");
        var findResult = Parse(await service.FindBuildingsByTypeAsync("transport-belt"));
        Assert.Equal("east", findResult.GetProperty("buildings")[0].GetProperty("direction").GetString());

        // Update direction no-op for missing position (should not throw)
        await service.UpdateBuildingDirectionAsync(99, 99, "east");

        // Clear all
        result = Parse(await service.ClearAllBuildingsAsync());
        Assert.Equal("cleared", result.GetProperty("status").GetString());
        Assert.True(result.GetProperty("removed_count").GetInt32() > 0);

        var all = Parse(await service.GetAllBuildingsAsync());
        Assert.Equal(0, all.GetProperty("count").GetInt32());

        // Clear empty is fine
        result = Parse(await service.ClearAllBuildingsAsync());
        Assert.Equal("cleared", result.GetProperty("status").GetString());
        Assert.Equal(0, result.GetProperty("removed_count").GetInt32());
    }

    [Fact]
    public async Task SpatialQueries_GetBuildingsNear_And_FindByType()
    {
        var service = CreateService();
        await service.TrackBuildingAsync("stone-furnace", 0, 0);
        await service.TrackBuildingAsync("stone-furnace", 5, 0);
        await service.TrackBuildingAsync("stone-furnace", 100, 100);
        await service.TrackBuildingAsync("transport-belt", 3, 0);
        await service.TrackBuildingAsync("Stone-Furnace", 8, 0);
        await service.TrackBuildingAsync("stone-furnace", 15, 0, "east");
        await service.UpdateBuildingLabelAsync(15, 0, "iron smelter");

        // GetAllBuildings: returns all
        var all = Parse(await service.GetAllBuildingsAsync());
        Assert.Equal("ok", all.GetProperty("status").GetString());
        Assert.Equal(6, all.GetProperty("count").GetInt32());
        Assert.Equal(6, all.GetProperty("buildings").GetArrayLength());

        // GetBuildingsNear: only within radius
        var near = Parse(await service.GetBuildingsNearAsync(0, 0, 10));
        Assert.Equal(4, near.GetProperty("count").GetInt32());

        // GetBuildingsNear: sorted by distance
        var buildings = near.GetProperty("buildings");
        Assert.Equal(0.0, buildings[0].GetProperty("distance").GetDouble());

        // GetBuildingsNear: includes distance (3,4 triangle = 5)
        await service.TrackBuildingAsync("marker", 3, 4);
        var nearMarker = Parse(await service.GetBuildingsNearAsync(0, 0, 10));
        var found = false;
        for (int i = 0; i < nearMarker.GetProperty("buildings").GetArrayLength(); i++)
        {
            var b = nearMarker.GetProperty("buildings")[i];
            if (b.GetProperty("entity_name").GetString() == "marker")
            {
                Assert.Equal(5.0, b.GetProperty("distance").GetDouble());
                found = true;
            }
        }
        Assert.True(found);

        // FindBuildingsByType: matching
        var byType = Parse(await service.FindBuildingsByTypeAsync("stone-furnace"));
        Assert.True(byType.GetProperty("count").GetInt32() >= 4);
        Assert.Equal("stone-furnace", byType.GetProperty("entity_name").GetString());

        // FindBuildingsByType: case insensitive
        var byTypeCi = Parse(await service.FindBuildingsByTypeAsync("Stone-Furnace"));
        Assert.True(byTypeCi.GetProperty("count").GetInt32() >= 4);

        // FindBuildingsByType: no matches
        var noMatch = Parse(await service.FindBuildingsByTypeAsync("steel-furnace"));
        Assert.Equal(0, noMatch.GetProperty("count").GetInt32());

        // GetClosestBuildingOfType: returns closest
        var closest = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));
        Assert.Equal("ok", closest.GetProperty("status").GetString());
        Assert.Equal(0, closest.GetProperty("closest").GetProperty("x").GetDouble());
        Assert.Equal(0, closest.GetProperty("closest").GetProperty("y").GetDouble());
        Assert.True(closest.GetProperty("total_matches").GetInt32() >= 4);

        // GetClosestBuildingOfType: includes others
        var others = closest.GetProperty("others");
        Assert.True(others.GetArrayLength() >= 3);

        // GetClosestBuildingOfType: case insensitive
        var closestCi = Parse(await service.GetClosestBuildingOfTypeAsync("STONE-FURNACE", 0, 0));
        Assert.Equal("ok", closestCi.GetProperty("status").GetString());

        // GetClosestBuildingOfType: includes direction and label
        var closestLabeled = Parse(await service.GetClosestBuildingOfTypeAsync("stone-furnace", 14, 0));
        var closestB = closestLabeled.GetProperty("closest");
        Assert.Equal("east", closestB.GetProperty("direction").GetString());
        Assert.Equal("iron smelter", closestB.GetProperty("label").GetString());

        // GetClosestBuildingOfType: not_found for missing type
        var notFound = Parse(await service.GetClosestBuildingOfTypeAsync("nuclear-reactor", 0, 0));
        Assert.Equal("not_found", notFound.GetProperty("status").GetString());

        // FindClosestBuilding: by label
        var byLabel = await service.FindClosestBuildingAsync("iron smelter", 0, 0);
        Assert.NotNull(byLabel);
        Assert.Equal("stone-furnace", byLabel.EntityName);
        Assert.Equal(15, byLabel.X);

        // FindClosestBuilding: by entity name
        var byEntity = await service.FindClosestBuildingAsync("transport-belt", 0, 0);
        Assert.NotNull(byEntity);
        Assert.Equal("transport-belt", byEntity.EntityName);

        // FindClosestBuilding: label priority over entity name
        await service.TrackBuildingAsync("stone-furnace", 20, 0);
        await service.UpdateBuildingLabelAsync(20, 0, "iron output");
        await service.TrackBuildingAsync("iron-chest", 2, 0);
        var labelPriority = await service.FindClosestBuildingAsync("iron output", 0, 0);
        Assert.NotNull(labelPriority);
        Assert.Equal("stone-furnace", labelPriority.EntityName);
        Assert.Equal(20, labelPriority.X);

        // FindClosestBuilding: closest when multiple match
        var closestMultiple = await service.FindClosestBuildingAsync("stone-furnace", 0, 0);
        Assert.NotNull(closestMultiple);
        Assert.Equal(0, closestMultiple.X);

        // FindClosestBuilding: null when not found
        var nullResult = await service.FindClosestBuildingAsync("nuclear-reactor", 0, 0);
        Assert.Null(nullResult);

        // FindClosestBuilding: case insensitive label
        await service.TrackBuildingAsync("stone-furnace", 25, 0);
        await service.UpdateBuildingLabelAsync(25, 0, "Main Smelter");
        var ciLabel = await service.FindClosestBuildingAsync("main smelter", 24, 0);
        Assert.NotNull(ciLabel);
        Assert.Equal("Main Smelter", ciLabel.Label);

        // FindClosestBuilding: case insensitive entity name
        var ciEntity = await service.FindClosestBuildingAsync("TRANSPORT-BELT", 0, 0);
        Assert.NotNull(ciEntity);

        // FindClosestBuilding: null when empty
        var emptyService = CreateService();
        var emptyResult = await emptyService.FindClosestBuildingAsync("stone-furnace", 0, 0);
        Assert.Null(emptyResult);

        // GetClosestBuildingOfType: not_found when empty
        var emptyClosest = Parse(await emptyService.GetClosestBuildingOfTypeAsync("stone-furnace", 0, 0));
        Assert.Equal("not_found", emptyClosest.GetProperty("status").GetString());

        // GetAllBuildings: empty when none
        var emptyAll = Parse(await emptyService.GetAllBuildingsAsync());
        Assert.Equal("ok", emptyAll.GetProperty("status").GetString());
        Assert.Equal(0, emptyAll.GetProperty("count").GetInt32());
        Assert.Equal(0, emptyAll.GetProperty("buildings").GetArrayLength());
    }

    [Fact]
    public async Task GetBuildingSummary_ReturnsCorrectCounts()
    {
        // Empty summary
        var emptyService = CreateService();
        var emptyResult = Parse(await emptyService.GetBuildingSummaryAsync());
        Assert.Equal("ok", emptyResult.GetProperty("status").GetString());
        Assert.Equal(0, emptyResult.GetProperty("total_buildings").GetInt32());
        Assert.Equal(0, emptyResult.GetProperty("type_count").GetInt32());

        // Summary with multiple types
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

    [Fact]
    public async Task Persistence_SaveAndLoad()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json");

        var service1 = new BuildingMemoryService(filePath);
        await service1.TrackBuildingAsync("stone-furnace", 5, -3, "south");
        await service1.TrackBuildingAsync("transport-belt", 10, 0, "east");
        await service1.UpdateBuildingLabelAsync(5, -3, "main smelter");

        // New instance loads from same file
        var service2 = new BuildingMemoryService(filePath);
        var result = Parse(await service2.GetAllBuildingsAsync());
        Assert.Equal(2, result.GetProperty("count").GetInt32());

        // Label persisted
        var byType = Parse(await service2.FindBuildingsByTypeAsync("stone-furnace"));
        var building = byType.GetProperty("buildings")[0];
        Assert.Equal("main smelter", building.GetProperty("label").GetString());
    }

    [Fact]
    public async Task ValidateBuildings_RemovesInvalid()
    {
        // No rcon returns error
        var noRconService = CreateService();
        var noRconResult = Parse(await noRconService.ValidateBuildingMemoryAsync());
        Assert.Equal("error", noRconResult.GetProperty("status").GetString());
        Assert.Equal("no_rcon", noRconResult.GetProperty("error").GetString());

        // Empty memory returns ok without calling rcon
        var emptyRcon = new ScriptedRconClient([""]);
        var emptyService = CreateServiceWithRcon(emptyRcon);
        var emptyResult = Parse(await emptyService.ValidateBuildingMemoryAsync());
        Assert.Equal("ok", emptyResult.GetProperty("status").GetString());
        Assert.Equal(0, emptyResult.GetProperty("validated").GetInt32());
        Assert.Equal(0, emptyResult.GetProperty("removed").GetInt32());
        Assert.Equal(0, emptyResult.GetProperty("remaining").GetInt32());
        Assert.Empty(emptyRcon.AllCommands);

        // All exist: keeps all
        var allExistRcon = new ScriptedRconClient(["1,1,1"]);
        var allExistService = CreateServiceWithRcon(allExistRcon);
        await allExistService.TrackBuildingAsync("stone-furnace", 0, 0);
        await allExistService.TrackBuildingAsync("transport-belt", 5, 0);
        await allExistService.TrackBuildingAsync("inserter", 10, 0);
        var allExistResult = Parse(await allExistService.ValidateBuildingMemoryAsync());
        Assert.Equal("ok", allExistResult.GetProperty("status").GetString());
        Assert.Equal(3, allExistResult.GetProperty("validated").GetInt32());
        Assert.Equal(0, allExistResult.GetProperty("removed").GetInt32());
        Assert.Equal(3, allExistResult.GetProperty("remaining").GetInt32());

        // Mixed: removes missing, verify removed from memory
        var mixedRcon = new ScriptedRconClient(["1,0,1"]);
        var mixedService = CreateServiceWithRcon(mixedRcon);
        await mixedService.TrackBuildingAsync("stone-furnace", 0, 0);
        await mixedService.TrackBuildingAsync("transport-belt", 5, 0);
        await mixedService.TrackBuildingAsync("inserter", 10, 0);
        var mixedResult = Parse(await mixedService.ValidateBuildingMemoryAsync());
        Assert.Equal("ok", mixedResult.GetProperty("status").GetString());
        Assert.Equal(3, mixedResult.GetProperty("validated").GetInt32());
        Assert.Equal(1, mixedResult.GetProperty("removed").GetInt32());
        Assert.Equal(2, mixedResult.GetProperty("remaining").GetInt32());
        var remaining = Parse(await mixedService.GetAllBuildingsAsync());
        Assert.Equal(2, remaining.GetProperty("count").GetInt32());
        Assert.Equal("stone-furnace", remaining.GetProperty("buildings")[0].GetProperty("entity_name").GetString());
        Assert.Equal("inserter", remaining.GetProperty("buildings")[1].GetProperty("entity_name").GetString());

        // Empty rcon response returns error
        var emptyRespRcon = new ScriptedRconClient([""]);
        var emptyRespService = CreateServiceWithRcon(emptyRespRcon);
        await emptyRespService.TrackBuildingAsync("stone-furnace", 0, 0);
        var emptyRespResult = Parse(await emptyRespService.ValidateBuildingMemoryAsync());
        Assert.Equal("error", emptyRespResult.GetProperty("status").GetString());
        Assert.Equal("invalid_response", emptyRespResult.GetProperty("error").GetString());

        // Single rcon call sent
        var singleCallRcon = new ScriptedRconClient(["1,1"]);
        var singleCallService = CreateServiceWithRcon(singleCallRcon);
        await singleCallService.TrackBuildingAsync("stone-furnace", 0, 0);
        await singleCallService.TrackBuildingAsync("transport-belt", 5, 0);
        await singleCallService.ValidateBuildingMemoryAsync();
        Assert.Single(singleCallRcon.AllCommands);
        var luaCommand = singleCallRcon.AllCommands[0];
        Assert.Contains("stone-furnace", luaCommand);
        Assert.Contains("transport-belt", luaCommand);
        Assert.Contains("find_entities_filtered", luaCommand);

        // Persists after removal
        var persistPath = Path.Combine(Path.GetTempPath(), $"buildings-{Guid.NewGuid():N}.json");
        var persistRcon = new ScriptedRconClient(["1,0"]);
        var persistService1 = new BuildingMemoryService(persistPath, persistRcon);
        await persistService1.TrackBuildingAsync("stone-furnace", 0, 0);
        await persistService1.TrackBuildingAsync("transport-belt", 5, 0);
        await persistService1.ValidateBuildingMemoryAsync();
        var persistService2 = new BuildingMemoryService(persistPath);
        var persistResult = Parse(await persistService2.GetAllBuildingsAsync());
        Assert.Equal(1, persistResult.GetProperty("count").GetInt32());
        Assert.Equal("stone-furnace", persistResult.GetProperty("buildings")[0].GetProperty("entity_name").GetString());

        // BuildValidationLua: correct script
        var luaBuildings = new List<TrackedBuilding>
        {
            new() { EntityName = "stone-furnace", X = 5, Y = -3 },
            new() { EntityName = "transport-belt", X = 10.5, Y = 0 }
        };
        var lua = BuildingMemoryService.BuildValidationLua(luaBuildings);
        Assert.Contains("stone-furnace", lua);
        Assert.Contains("transport-belt", lua);
        Assert.Contains("x=5", lua);
        Assert.Contains("y=-3", lua);
        Assert.Contains("find_entities_filtered", lua);
        Assert.Contains("table.concat(results", lua);

        // BuildValidationLua: single building
        var singleLua = BuildingMemoryService.BuildValidationLua(
            [new() { EntityName = "inserter", X = 0, Y = 0 }]);
        Assert.Contains("inserter", singleLua);
        Assert.DoesNotContain(",{", singleLua);

        // ParseValidationResponse: all present
        var allPresent = BuildingMemoryService.ParseValidationResponse("1,1,1", 3);
        Assert.NotNull(allPresent);
        Assert.Equal(3, allPresent.Length);
        Assert.All(allPresent, r => Assert.True(r));

        // ParseValidationResponse: all missing
        var allMissing = BuildingMemoryService.ParseValidationResponse("0,0,0", 3);
        Assert.NotNull(allMissing);
        Assert.All(allMissing, r => Assert.False(r));

        // ParseValidationResponse: mixed
        var mixed = BuildingMemoryService.ParseValidationResponse("1,0,1,0", 4);
        Assert.NotNull(mixed);
        Assert.True(mixed[0]);
        Assert.False(mixed[1]);
        Assert.True(mixed[2]);
        Assert.False(mixed[3]);

        // ParseValidationResponse: empty
        Assert.Null(BuildingMemoryService.ParseValidationResponse("", 3));

        // ParseValidationResponse: whitespace handling
        var whitespace = BuildingMemoryService.ParseValidationResponse(" 1 , 0 , 1 ", 3);
        Assert.NotNull(whitespace);
        Assert.True(whitespace[0]);
        Assert.False(whitespace[1]);
        Assert.True(whitespace[2]);

        // ParseValidationResponse: shorter response
        var shorter = BuildingMemoryService.ParseValidationResponse("1", 3);
        Assert.NotNull(shorter);
        Assert.Equal(3, shorter.Length);
        Assert.True(shorter[0]);
        Assert.False(shorter[1]);
        Assert.False(shorter[2]);
    }

    [Theory]
    [InlineData("null_entity_track")]
    [InlineData("whitespace_entity_track")]
    [InlineData("zero_radius")]
    [InlineData("negative_radius")]
    [InlineData("null_entity_find_by_type")]
    [InlineData("null_search_find_closest")]
    public async Task Validation_ThrowsOnInvalidInput(string scenario)
    {
        var service = CreateService();

        switch (scenario)
        {
            case "null_entity_track":
                await Assert.ThrowsAnyAsync<ArgumentException>(
                    () => service.TrackBuildingAsync(null!, 0, 0));
                break;
            case "whitespace_entity_track":
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.TrackBuildingAsync("  ", 0, 0));
                break;
            case "zero_radius":
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => service.GetBuildingsNearAsync(0, 0, 0));
                break;
            case "negative_radius":
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => service.GetBuildingsNearAsync(0, 0, -5));
                break;
            case "null_entity_find_by_type":
                await Assert.ThrowsAnyAsync<ArgumentException>(
                    () => service.FindBuildingsByTypeAsync(null!));
                break;
            case "null_search_find_closest":
                await Assert.ThrowsAnyAsync<ArgumentException>(
                    () => service.FindClosestBuildingAsync(null!, 0, 0));
                break;
        }
    }
}
