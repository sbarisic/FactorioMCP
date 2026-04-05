using System.Text.Json;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class BlueprintAnalysisServiceTests
{
    private readonly BlueprintCodecService _codec = new();
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
                {""entity_number"":1,""name"":""transport-belt"",""position"":{""x"":-1.5,""y"":0.5},""direction"":4},
                {""entity_number"":2,""name"":""transport-belt"",""position"":{""x"":-1.5,""y"":1.5},""direction"":4},
                {""entity_number"":3,""name"":""burner-inserter"",""position"":{""x"":-0.5,""y"":0.5},""direction"":2},
                {""entity_number"":4,""name"":""burner-inserter"",""position"":{""x"":-0.5,""y"":1.5},""direction"":2},
                {""entity_number"":5,""name"":""stone-furnace"",""position"":{""x"":1,""y"":1}},
                {""entity_number"":6,""name"":""burner-inserter"",""position"":{""x"":2.5,""y"":0.5},""direction"":2},
                {""entity_number"":7,""name"":""burner-inserter"",""position"":{""x"":2.5,""y"":1.5},""direction"":2},
                {""entity_number"":8,""name"":""transport-belt"",""position"":{""x"":3.5,""y"":0.5},""direction"":4},
                {""entity_number"":9,""name"":""transport-belt"",""position"":{""x"":3.5,""y"":1.5},""direction"":4}
            ],""version"":562949954076672}}";
            var result = codec.EncodeBlueprintString(json);
            using var doc = JsonDocument.Parse(result);
            return doc.RootElement.GetProperty("blueprint_string").GetString()!;
        }
    }

    public BlueprintAnalysisServiceTests()
    {
        _service = new BlueprintAnalysisService(_codec);
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
            new(1, "transport-belt", 0.5, 0.5, 2, "east", null),
            new(2, "transport-belt", 1.5, 0.5, 2, "east", null),
            new(3, "transport-belt", 2.5, 0.5, 2, "east", null),
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
            new(1, "transport-belt", 0.5, 0.5, 4, "south", null),
            new(2, "transport-belt", 0.5, 1.5, 4, "south", null),
        };

        var edges = _service.BuildFlowGraph(entities);
        Assert.Single(edges);
        Assert.Contains(edges, e => e.FromNum == 1 && e.ToNum == 2 && e.Type == "belt");
    }

    [Fact]
    public void BuildFlowGraph_InserterBetweenChestAndBelt_CreatesPickupAndDrop()
    {
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "iron-chest", 0.5, 0.5, 0, "north", null),
            new(2, "inserter", 1.5, 0.5, 2, "east", null),
            new(3, "transport-belt", 2.5, 0.5, 2, "east", null),
        };

        var edges = _service.BuildFlowGraph(entities);

        Assert.Contains(edges, e => e.FromNum == 1 && e.ToNum == 2 && e.Type == "inserter_pickup");
        Assert.Contains(edges, e => e.FromNum == 2 && e.ToNum == 3 && e.Type == "inserter_drop");
    }

    [Fact]
    public void BuildFlowGraph_LongInserter_ReachesTwoTiles()
    {
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "iron-chest", 0.5, 0.5, 0, "north", null),
            new(2, "long-handed-inserter", 2.5, 0.5, 2, "east", null),
            new(3, "iron-chest", 4.5, 0.5, 0, "north", null),
        };

        var edges = _service.BuildFlowGraph(entities);

        Assert.Contains(edges, e => e.FromNum == 1 && e.ToNum == 2 && e.Type == "inserter_pickup");
        Assert.Contains(edges, e => e.FromNum == 2 && e.ToNum == 3 && e.Type == "inserter_drop");
    }

    [Fact]
    public void BuildFlowGraph_InserterIntoFurnace_HitsLargeEntity()
    {
        // Furnace centered at (1,1) occupies tiles (0,0),(1,0),(0,1),(1,1)
        // Inserter at (2.5,0.5) facing west drops at (1.5,0.5) → tile (1,0) which is furnace
        var entities = new List<BlueprintAnalysisService.BpEntity>
        {
            new(1, "stone-furnace", 1, 1, 0, "north", null),
            new(2, "inserter", 2.5, 0.5, 6, "west", null),
        };

        var edges = _service.BuildFlowGraph(entities);

        Assert.Contains(edges, e => e.FromNum == 2 && e.ToNum == 1 && e.Type == "inserter_drop");
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
            {""entity_number"":1,""name"":""inserter"",""position"":{""x"":10.5,""y"":10.5},""direction"":2}
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
            {""entity_number"":1,""name"":""transport-belt"",""position"":{""x"":0.5,""y"":0.5},""direction"":2},
            {""entity_number"":2,""name"":""transport-belt"",""position"":{""x"":1.5,""y"":0.5},""direction"":2}
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
}
