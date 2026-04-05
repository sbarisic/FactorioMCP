using System.Text.Json;
using FactorioMCP.Rcon;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class BlueprintAnalysisServiceTests
{
    private readonly BlueprintCodecService _codec = new();
    private readonly CapturingRconClient _rcon = new();
    private readonly BlueprintAnalysisService _service;

    // User-provided blueprint string (may be corrupt from copy-paste — tests should be resilient)
    private const string UserBlueprint = "0eNqlldtuhCAURf/lPONELjrqrzRNo85pQ6JoAJuZGP69ONO0TUNTCI/c1oKcDewwTBuuWioL3Q5yXJSB7mkHI99UPx19qp8ROsDrqtGYwupemXXRthhwsuAISHXBK3TUkcAyYxeFxeumVT/ij9nMPRNAZaWV+DDeG7cXtc0Dao8j/5gJrIvxyxd16DyyEOxUEbhB154qb7pIjeNjvDn29kvA0gX8nCLgGSegNMYgcgxljKH6MkplUFvfF2DSP5kiwKxjmLxJYp5JOHCBzZafWBfANBmZiCtZm2OIKhktc1LBoxQ5l5OyKAVLSx6PSQnladGLg4rk7PFQ9miVE424utU5ikDd/BsuLc6e9/2NEHhHbe4Tqpq1om2rRpSsFI1zH5/dHgc=";

    // Known-good synthetic blueprint: 2 furnaces with inserters and belts
    private static string GoodBlueprint
    {
        get
        {
            var codec = new BlueprintCodecService();
            var json = @"{""blueprint"":{""item"":""blueprint"",""label"":""Test Smelter"",""entities"":[
                {""entity_number"":1,""name"":""transport-belt"",""position"":{""x"":-1.5,""y"":0.5},""direction"":8},
                {""entity_number"":2,""name"":""transport-belt"",""position"":{""x"":-1.5,""y"":1.5},""direction"":8},
                {""entity_number"":3,""name"":""burner-inserter"",""position"":{""x"":-0.5,""y"":0.5},""direction"":4},
                {""entity_number"":4,""name"":""burner-inserter"",""position"":{""x"":-0.5,""y"":1.5},""direction"":4},
                {""entity_number"":5,""name"":""stone-furnace"",""position"":{""x"":1,""y"":1}},
                {""entity_number"":6,""name"":""burner-inserter"",""position"":{""x"":2.5,""y"":0.5},""direction"":4},
                {""entity_number"":7,""name"":""burner-inserter"",""position"":{""x"":2.5,""y"":1.5},""direction"":4},
                {""entity_number"":8,""name"":""transport-belt"",""position"":{""x"":3.5,""y"":0.5},""direction"":8},
                {""entity_number"":9,""name"":""transport-belt"",""position"":{""x"":3.5,""y"":1.5},""direction"":8}
            ],""version"":562949954076672}}";
            var result = codec.EncodeBlueprintString(json);
            using var doc = JsonDocument.Parse(result);
            return doc.RootElement.GetProperty("blueprint_string").GetString()!;
        }
    }

    public BlueprintAnalysisServiceTests()
    {
        _service = new BlueprintAnalysisService(_codec, _rcon);
    }

    // ── AllCommands_UseSilentCommandPrefix ─────────────────────────────

    [Fact]
    public async Task AllCommands_UseSilentCommandPrefix()
    {
        var capturingRcon = new CapturingRconClient();
        var service = new BlueprintAnalysisService(_codec, capturingRcon);
        var bp = MakeSmelterBlueprintWithRecipe();

        await service.AnalyzeBlueprintProductionAsync(bp);

        Assert.Single(capturingRcon.AllCommands);
        Assert.All(capturingRcon.AllCommands, cmd => Assert.StartsWith("/silent-command ", cmd));
    }

    [Fact]
    public void AnalyzeBlueprint_GoodBlueprint_ReturnsSuccess()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsEntityCount()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        Assert.Equal(9, doc.RootElement.GetProperty("entity_count").GetInt32());
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsEntitySummary()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        var summary = doc.RootElement.GetProperty("entity_summary");
        Assert.Equal(4, summary.GetProperty("transport-belt").GetInt32());
        Assert.Equal(4, summary.GetProperty("burner-inserter").GetInt32());
        Assert.Equal(1, summary.GetProperty("stone-furnace").GetInt32());
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsDimensions()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        var dims = doc.RootElement.GetProperty("dimensions");
        Assert.True(dims.GetProperty("width").GetDouble() > 0);
        Assert.True(dims.GetProperty("height").GetDouble() > 0);
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsFlowGraph()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        var graph = doc.RootElement.GetProperty("flow_graph");
        Assert.True(graph.GetProperty("edge_count").GetInt32() > 0);
    }

    [Fact]
    public void AnalyzeBlueprint_HasInserterEdges()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        var edges = doc.RootElement.GetProperty("flow_graph").GetProperty("edges");
        var edgeTypes = edges.EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .Distinct()
            .ToList();
        Assert.Contains("inserter_pickup", edgeTypes);
        Assert.Contains("inserter_drop", edgeTypes);
    }

    [Fact]
    public void AnalyzeBlueprint_HasBeltEdges()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        var edges = doc.RootElement.GetProperty("flow_graph").GetProperty("edges");
        var edgeTypes = edges.EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .Distinct()
            .ToList();
        Assert.Contains("belt", edgeTypes);
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsBeltChains()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        var chains = doc.RootElement.GetProperty("belt_chains");
        Assert.True(chains.GetArrayLength() > 0);
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsCategories()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        var categories = doc.RootElement.GetProperty("categories");
        Assert.True(categories.TryGetProperty("machines", out _));
        Assert.True(categories.TryGetProperty("inserters", out _));
        Assert.True(categories.TryGetProperty("belts", out _));
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsIssuesArray()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("issues").ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public void AnalyzeBlueprint_ReturnsLabel()
    {
        var result = _service.AnalyzeBlueprint(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        Assert.Equal("Test Smelter", doc.RootElement.GetProperty("label").GetString());
    }

    [Fact]
    public void AnalyzeBlueprint_InvalidString_ReturnsError()
    {
        var result = _service.AnalyzeBlueprint("1InvalidBlueprint");
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void AnalyzeBlueprint_ThrowsOnNullOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => _service.AnalyzeBlueprint(""));
        Assert.Throws<ArgumentException>(() => _service.AnalyzeBlueprint("  "));
        Assert.Throws<ArgumentNullException>(() => _service.AnalyzeBlueprint(null!));
    }

    [Fact]
    public void AnalyzeBlueprint_UserBlueprint_ReturnsValidJson()
    {
        // The user blueprint may be corrupt; but should always return valid JSON
        var result = _service.AnalyzeBlueprint(UserBlueprint);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("success", out _));
    }

    // ── TraceBlueprintFlow tests ─────────────────────────────────────

    [Fact]
    public void TraceBlueprintFlow_ReturnsSuccess()
    {
        var result = _service.TraceBlueprintFlow(GoodBlueprint, 1);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void TraceBlueprintFlow_FromBelt_ReachesDownstream()
    {
        var result = _service.TraceBlueprintFlow(GoodBlueprint, 1);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("node_count").GetInt32() > 1);
        Assert.True(doc.RootElement.GetProperty("edge_count").GetInt32() >= 1);
    }

    [Fact]
    public void TraceBlueprintFlow_InvalidEntity_ReturnsError()
    {
        var result = _service.TraceBlueprintFlow(GoodBlueprint, 99999);
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("entity_not_found", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void TraceBlueprintFlow_ThrowsOnInvalidDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.TraceBlueprintFlow(GoodBlueprint, 1, 0));
    }

    // ── Flow graph building tests (unit) ────────────────────────────

    [Fact]
    public void BuildFlowGraph_SimpleBeltChain_CreatesEdges()
    {
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "transport-belt", 0.5, 0.5, 4, "east", null),
            new(2, "transport-belt", 1.5, 0.5, 4, "east", null),
            new(3, "transport-belt", 2.5, 0.5, 4, "east", null),
        };

        var edges = _service.BuildFlowGraph(entities);

        Assert.Equal(2, edges.Count);
        Assert.Contains(edges, e => e.FromNum == 1 && e.ToNum == 2 && e.Type == "belt");
        Assert.Contains(edges, e => e.FromNum == 2 && e.ToNum == 3 && e.Type == "belt");
    }

    [Fact]
    public void BuildFlowGraph_BeltsGoingSouth_CreatesEdges()
    {
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "transport-belt", 0.5, 0.5, 8, "south", null),
            new(2, "transport-belt", 0.5, 1.5, 8, "south", null),
        };

        var edges = _service.BuildFlowGraph(entities);
        Assert.Single(edges);
        Assert.Contains(edges, e => e.FromNum == 1 && e.ToNum == 2 && e.Type == "belt");
    }

    [Fact]
    public void BuildFlowGraph_InserterBetweenChestAndBelt_CreatesPickupAndDrop()
    {
        // Inserter facing east picks up from east (belt) and drops to west (chest)
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "iron-chest", 0.5, 0.5, 0, "north", null),
            new(2, "inserter", 1.5, 0.5, 4, "east", null),
            new(3, "transport-belt", 2.5, 0.5, 4, "east", null),
        };

        var edges = _service.BuildFlowGraph(entities);

        Assert.Contains(edges, e => e.FromNum == 3 && e.ToNum == 2 && e.Type == "inserter_pickup");
        Assert.Contains(edges, e => e.FromNum == 2 && e.ToNum == 1 && e.Type == "inserter_drop");
    }

    [Fact]
    public void BuildFlowGraph_LongInserter_ReachesTwoTiles()
    {
        // Long inserter facing east picks up from 2 tiles east, drops 2 tiles west
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "iron-chest", 0.5, 0.5, 0, "north", null),
            new(2, "long-handed-inserter", 2.5, 0.5, 4, "east", null),
            new(3, "iron-chest", 4.5, 0.5, 0, "north", null),
        };

        var edges = _service.BuildFlowGraph(entities);

        Assert.Contains(edges, e => e.FromNum == 3 && e.ToNum == 2 && e.Type == "inserter_pickup");
        Assert.Contains(edges, e => e.FromNum == 2 && e.ToNum == 1 && e.Type == "inserter_drop");
    }

    [Fact]
    public void BuildFlowGraph_InserterFromFurnace_HitsLargeEntity()
    {
        // Inserter at (2.5,0.5) facing west picks up from (1.5,0.5) → furnace tile
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "stone-furnace", 1, 1, 0, "north", null),
            new(2, "inserter", 2.5, 0.5, 12, "west", null),
        };

        var edges = _service.BuildFlowGraph(entities);

        Assert.Contains(edges, e => e.FromNum == 1 && e.ToNum == 2 && e.Type == "inserter_pickup");
    }

    [Fact]
    public void BuildFlowGraph_NoEntities_ReturnsEmptyEdges()
    {
        var edges = _service.BuildFlowGraph([]);
        Assert.Empty(edges);
    }

    // ── Entity size lookup tests ────────────────────────────────────

    [Theory]
    [InlineData("transport-belt", 1, 1)]
    [InlineData("inserter", 1, 1)]
    [InlineData("stone-furnace", 2, 2)]
    [InlineData("assembling-machine-1", 3, 3)]
    [InlineData("oil-refinery", 5, 5)]
    [InlineData("unknown-entity-xyz", 1, 1)]
    public void GetEntitySize_ReturnsCorrectSize(string name, int expectedW, int expectedH)
    {
        var (w, h) = BlueprintAnalysisService.GetEntitySize(name);
        Assert.Equal(expectedW, w);
        Assert.Equal(expectedH, h);
    }

    // ── Synthetic blueprint issue detection ─────────────────────────

    [Fact]
    public void AnalyzeBlueprint_OrphanedInserter_ReportsIssue()
    {
        var json = @"{""blueprint"":{""item"":""blueprint"",""entities"":[
            {""entity_number"":1,""name"":""inserter"",""position"":{""x"":10.5,""y"":10.5},""direction"":4}
        ],""version"":562949954076672}}";
        var encodeResult = _codec.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encodeResult);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var result = _service.AnalyzeBlueprint(bpString);
        using var doc = JsonDocument.Parse(result);
        var issues = doc.RootElement.GetProperty("issues");
        Assert.True(issues.GetArrayLength() > 0);
        Assert.Contains("orphaned_inserter",
            issues.EnumerateArray().Select(i => i.GetProperty("issue").GetString()));
    }

    [Fact]
    public void AnalyzeBlueprint_DeadEndBelt_ReportsIssue()
    {
        var json = @"{""blueprint"":{""item"":""blueprint"",""entities"":[
            {""entity_number"":1,""name"":""transport-belt"",""position"":{""x"":0.5,""y"":0.5},""direction"":4},
            {""entity_number"":2,""name"":""transport-belt"",""position"":{""x"":1.5,""y"":0.5},""direction"":4}
        ],""version"":562949954076672}}";
        var encodeResult = _codec.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encodeResult);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var result = _service.AnalyzeBlueprint(bpString);
        using var doc = JsonDocument.Parse(result);
        var issues = doc.RootElement.GetProperty("issues");
        Assert.Contains("dead_end_belt",
            issues.EnumerateArray().Select(i => i.GetProperty("issue").GetString()));
    }

    [Fact]
    public void AnalyzeBlueprint_NotABlueprint_ReturnsError()
    {
        var json = @"{""blueprint_book"":{""item"":""blueprint-book"",""blueprints"":[],""version"":562949954076672}}";
        var encodeResult = _codec.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encodeResult);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var result = _service.AnalyzeBlueprint(bpString);
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("not_a_blueprint", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void AnalyzeBlueprint_EmptyBlueprint_ReportsZeroEntities()
    {
        var json = @"{""blueprint"":{""item"":""blueprint"",""entities"":[],""version"":562949954076672}}";
        var encodeResult = _codec.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encodeResult);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var result = _service.AnalyzeBlueprint(bpString);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("entity_count").GetInt32());
    }

    // ── AnalyzeBlueprintProductionAsync tests ──────────────────────────

    // Helper to make a smelter blueprint with recipe set on the furnace
    private static string MakeSmelterBlueprintWithRecipe()
    {
        var codec = new BlueprintCodecService();
        var json = @"{""blueprint"":{""item"":""blueprint"",""label"":""Smelter Line"",""entities"":[
            {""entity_number"":1,""name"":""transport-belt"",""position"":{""x"":-1.5,""y"":0.5},""direction"":8},
            {""entity_number"":2,""name"":""inserter"",""position"":{""x"":-0.5,""y"":0.5},""direction"":4},
            {""entity_number"":3,""name"":""stone-furnace"",""position"":{""x"":1,""y"":1},""recipe"":""iron-plate""},
            {""entity_number"":4,""name"":""inserter"",""position"":{""x"":2.5,""y"":0.5},""direction"":4},
            {""entity_number"":5,""name"":""transport-belt"",""position"":{""x"":3.5,""y"":0.5},""direction"":8}
        ],""version"":562949954076672}}";
        var result = codec.EncodeBlueprintString(json);
        using var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("blueprint_string").GetString()!;
    }

    private static string MakeRconRecipeResponse()
    {
        return @"{""recipes"":[{""name"":""iron-plate"",""energy"":3.2000,""ings"":[{""n"":""iron-ore"",""a"":1}],""prods"":[{""n"":""iron-plate"",""a"":1.0000}]}],""machines"":[{""name"":""stone-furnace"",""speed"":1.0000}]}";
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_ReturnsSuccess()
    {
        var scriptedRcon = new ScriptedRconClient([MakeRconRecipeResponse()]);
        var service = new BlueprintAnalysisService(_codec, scriptedRcon);
        var bp = MakeSmelterBlueprintWithRecipe();

        var result = await service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_ReportsRecipeGroups()
    {
        var scriptedRcon = new ScriptedRconClient([MakeRconRecipeResponse()]);
        var service = new BlueprintAnalysisService(_codec, scriptedRcon);
        var bp = MakeSmelterBlueprintWithRecipe();

        var result = await service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("machines_with_recipes").GetInt32());
        var groups = root.GetProperty("recipe_groups");
        Assert.Equal(1, groups.GetArrayLength());

        var group = groups[0];
        Assert.Equal("iron-plate", group.GetProperty("recipe").GetString());
        Assert.Equal("stone-furnace", group.GetProperty("machine_type").GetString());
        Assert.Equal(1, group.GetProperty("machine_count").GetInt32());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_CalculatesItemBalance()
    {
        var scriptedRcon = new ScriptedRconClient([MakeRconRecipeResponse()]);
        var service = new BlueprintAnalysisService(_codec, scriptedRcon);
        var bp = MakeSmelterBlueprintWithRecipe();

        var result = await service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);
        var balance = doc.RootElement.GetProperty("item_balance");

        // iron-ore is consumed but not produced → deficit
        Assert.True(balance.TryGetProperty("iron-ore", out var oreBalance));
        Assert.Equal("deficit", oreBalance.GetProperty("status").GetString());

        // iron-plate is produced but not consumed → surplus
        Assert.True(balance.TryGetProperty("iron-plate", out var plateBalance));
        Assert.Equal("surplus", plateBalance.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_ReportsBeltAnalysis()
    {
        var scriptedRcon = new ScriptedRconClient([MakeRconRecipeResponse()]);
        var service = new BlueprintAnalysisService(_codec, scriptedRcon);
        var bp = MakeSmelterBlueprintWithRecipe();

        var result = await service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);
        var beltAnalysis = doc.RootElement.GetProperty("belt_analysis");
        Assert.True(beltAnalysis.GetArrayLength() > 0);
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_DetectsInserterBottleneck()
    {
        // Create blueprint with a fast machine but slow inserter → bottleneck
        // Inserter faces west: picks from belt (west), drops into AM3 (east)
        var codec = new BlueprintCodecService();
        var json = @"{""blueprint"":{""item"":""blueprint"",""entities"":[
            {""entity_number"":1,""name"":""transport-belt"",""position"":{""x"":-1.5,""y"":0.5},""direction"":8},
            {""entity_number"":2,""name"":""burner-inserter"",""position"":{""x"":-0.5,""y"":0.5},""direction"":12},
            {""entity_number"":3,""name"":""assembling-machine-3"",""position"":{""x"":2,""y"":1},""recipe"":""iron-gear-wheel""}
        ],""version"":562949954076672}}";
        var encResult = codec.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encResult);
        var bp = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        // Machine is very fast (speed=1.25) with short recipe time (0.5s) → needs 2.5 items/s
        // Burner inserter can only do 0.6 items/s → bottleneck
        var rconResponse = @"{""recipes"":[{""name"":""iron-gear-wheel"",""energy"":0.5000,""ings"":[{""n"":""iron-plate"",""a"":2}],""prods"":[{""n"":""iron-gear-wheel"",""a"":1.0000}]}],""machines"":[{""name"":""assembling-machine-3"",""speed"":1.2500}]}";
        var scriptedRcon = new ScriptedRconClient([rconResponse]);
        var service = new BlueprintAnalysisService(codec, scriptedRcon);

        var result = await service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);
        var bottlenecks = doc.RootElement.GetProperty("inserter_bottlenecks");
        Assert.True(bottlenecks.GetArrayLength() > 0);
        Assert.Contains("inserter_bottleneck", bottlenecks[0].GetProperty("issue").GetString());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_EmptyBlueprint_ReportsNoEntities()
    {
        var json = @"{""blueprint"":{""item"":""blueprint"",""entities"":[],""version"":562949954076672}}";
        var encResult = _codec.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encResult);
        var bp = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var result = await _service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("entity_count").GetInt32());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_NoRecipeMachines_NoRecipeGroups()
    {
        // Blueprint with only belts — no machines with recipes
        var result = await _service.AnalyzeBlueprintProductionAsync(GoodBlueprint);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        // Furnace without recipe set → not counted
        Assert.Equal(0, doc.RootElement.GetProperty("machines_with_recipes").GetInt32());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_MultipleRecipes_GroupedCorrectly()
    {
        var codec = new BlueprintCodecService();
        var json = @"{""blueprint"":{""item"":""blueprint"",""entities"":[
            {""entity_number"":1,""name"":""stone-furnace"",""position"":{""x"":1,""y"":1},""recipe"":""iron-plate""},
            {""entity_number"":2,""name"":""stone-furnace"",""position"":{""x"":4,""y"":1},""recipe"":""iron-plate""},
            {""entity_number"":3,""name"":""stone-furnace"",""position"":{""x"":7,""y"":1},""recipe"":""copper-plate""}
        ],""version"":562949954076672}}";
        var encResult = codec.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encResult);
        var bp = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var rconResponse = @"{""recipes"":[{""name"":""iron-plate"",""energy"":3.2000,""ings"":[{""n"":""iron-ore"",""a"":1}],""prods"":[{""n"":""iron-plate"",""a"":1.0000}]},{""name"":""copper-plate"",""energy"":3.2000,""ings"":[{""n"":""copper-ore"",""a"":1}],""prods"":[{""n"":""copper-plate"",""a"":1.0000}]}],""machines"":[{""name"":""stone-furnace"",""speed"":1.0000}]}";
        var scriptedRcon = new ScriptedRconClient([rconResponse]);
        var service = new BlueprintAnalysisService(codec, scriptedRcon);

        var result = await service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);

        var groups = doc.RootElement.GetProperty("recipe_groups");
        Assert.Equal(2, groups.GetArrayLength());

        // iron-plate group should have 2 machines
        var ironGroup = groups.EnumerateArray()
            .First(g => g.GetProperty("recipe").GetString() == "iron-plate");
        Assert.Equal(2, ironGroup.GetProperty("machine_count").GetInt32());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_InvalidString_ReturnsError()
    {
        var result = await _service.AnalyzeBlueprintProductionAsync("invalid");
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task AnalyzeBlueprintProduction_RecommendsBeltTier()
    {
        var scriptedRcon = new ScriptedRconClient([MakeRconRecipeResponse()]);
        var service = new BlueprintAnalysisService(_codec, scriptedRcon);
        var bp = MakeSmelterBlueprintWithRecipe();

        var result = await service.AnalyzeBlueprintProductionAsync(bp);
        using var doc = JsonDocument.Parse(result);
        var beltAnalysis = doc.RootElement.GetProperty("belt_analysis");

        // Find the recommendation entry
        var recommendation = beltAnalysis.EnumerateArray()
            .FirstOrDefault(b => b.GetProperty("belt_type").GetString() == "recommendation");
        Assert.NotEqual(default, recommendation);
        Assert.True(recommendation.TryGetProperty("recommended_belt", out _));
    }
}
