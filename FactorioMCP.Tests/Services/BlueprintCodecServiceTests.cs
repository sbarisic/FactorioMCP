using FactorioMCP.Models;
using FactorioMCP.Services;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class BlueprintCodecServiceTests
{
    private readonly BlueprintCodecService _service = new();

    // Helper: build a valid blueprint string from JSON
    private static string MakeBlueprintString(string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        using var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x01);
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(jsonBytes, 0, jsonBytes.Length);

        // Adler-32
        uint a = 1, b = 0;
        foreach (var byte_ in jsonBytes)
        {
            a = (a + byte_) % 65521;
            b = (b + a) % 65521;
        }
        var adler = (b << 16) | a;
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);

        return "0" + Convert.ToBase64String(output.ToArray());
    }

    #region DecodeBlueprintString

    [Fact]
    public void Decode_ValidBlueprint_ReturnsEntities()
    {
        var json = """{"blueprint":{"item":"blueprint","entities":[{"entity_number":1,"name":"transport-belt","position":{"x":0.5,"y":0.5},"direction":2}],"version":562949954076672}}""";
        var bpString = MakeBlueprintString(json);

        var result = _service.DecodeBlueprintString(bpString);
        using var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("blueprint", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("entity_count").GetInt32());

        var entities = doc.RootElement.GetProperty("entities");
        var first = entities[0];
        Assert.Equal("transport-belt", first.GetProperty("name").GetString());
        Assert.Equal("east", first.GetProperty("direction").GetString());
    }

    [Fact]
    public void Decode_BlueprintWithLabel_ReturnsLabel()
    {
        var json = """{"blueprint":{"item":"blueprint","label":"My Smelter","entities":[],"version":562949954076672}}""";
        var bpString = MakeBlueprintString(json);

        var result = _service.DecodeBlueprintString(bpString);
        using var doc = JsonDocument.Parse(result);

        Assert.Equal("My Smelter", doc.RootElement.GetProperty("label").GetString());
    }

    [Fact]
    public void Decode_BlueprintBook_ReturnsBookSummary()
    {
        var json = """{"blueprint_book":{"item":"blueprint-book","blueprints":[{"blueprint":{"item":"blueprint","label":"Sub BP","entities":[],"version":562949954076672},"index":0}],"active_index":0,"version":562949954076672}}""";
        var bpString = MakeBlueprintString(json);

        var result = _service.DecodeBlueprintString(bpString);
        using var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("blueprint_book", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("blueprint_count").GetInt32());
    }

    [Fact]
    public void Decode_InvalidVersionByte_ReturnsError()
    {
        var result = _service.DecodeBlueprintString("1invaliddata");
        using var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("invalid_version", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Decode_InvalidBase64_ReturnsError()
    {
        var result = _service.DecodeBlueprintString("0!!!not-base64!!!");
        using var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("invalid_base64", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Decode_NullOrWhitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.DecodeBlueprintString(""));
        Assert.Throws<ArgumentException>(() => _service.DecodeBlueprintString("   "));
        Assert.Throws<ArgumentNullException>(() => _service.DecodeBlueprintString(null!));
    }

    [Fact]
    public void Decode_EntitySummary_CountsByType()
    {
        var json = """{"blueprint":{"item":"blueprint","entities":[{"entity_number":1,"name":"transport-belt","position":{"x":0,"y":0}},{"entity_number":2,"name":"transport-belt","position":{"x":1,"y":0}},{"entity_number":3,"name":"inserter","position":{"x":2,"y":0}}],"version":562949954076672}}""";
        var bpString = MakeBlueprintString(json);

        var result = _service.DecodeBlueprintString(bpString);
        using var doc = JsonDocument.Parse(result);

        var summary = doc.RootElement.GetProperty("entity_summary");
        Assert.Equal(2, summary.GetProperty("transport-belt").GetInt32());
        Assert.Equal(1, summary.GetProperty("inserter").GetInt32());
    }

    [Fact]
    public void Decode_DirectionMapping_AllDirections()
    {
        var entities = new List<string>();
        for (int dir = 0; dir < 8; dir++)
            entities.Add($"{{\"entity_number\":{dir + 1},\"name\":\"transport-belt\",\"position\":{{\"x\":{dir},\"y\":0}},\"direction\":{dir}}}");

        var json = $"{{\"blueprint\":{{\"item\":\"blueprint\",\"entities\":[{string.Join(",", entities)}],\"version\":562949954076672}}}}";
        var bpString = MakeBlueprintString(json);

        var result = _service.DecodeBlueprintString(bpString);
        using var doc = JsonDocument.Parse(result);

        var ents = doc.RootElement.GetProperty("entities");
        var expectedDirs = new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
        for (int i = 0; i < 8; i++)
            Assert.Equal(expectedDirs[i], ents[i].GetProperty("direction").GetString());
    }

    [Fact]
    public void Decode_RecipeIncluded_WhenPresent()
    {
        var json = """{"blueprint":{"item":"blueprint","entities":[{"entity_number":1,"name":"assembling-machine-1","position":{"x":0,"y":0},"recipe":"iron-gear-wheel"}],"version":562949954076672}}""";
        var bpString = MakeBlueprintString(json);

        var result = _service.DecodeBlueprintString(bpString);
        using var doc = JsonDocument.Parse(result);

        var entity = doc.RootElement.GetProperty("entities")[0];
        Assert.Equal("iron-gear-wheel", entity.GetProperty("recipe").GetString());
    }

    #endregion

    #region EncodeBlueprintString

    [Fact]
    public void Encode_ValidBlueprint_ReturnsString()
    {
        var json = """{"blueprint":{"item":"blueprint","entities":[{"entity_number":1,"name":"transport-belt","position":{"x":0.5,"y":0.5},"direction":2}],"version":562949954076672}}""";

        var result = _service.EncodeBlueprintString(json);
        using var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var bpString = doc.RootElement.GetProperty("blueprint_string").GetString()!;
        Assert.StartsWith("0", bpString);
    }

    [Fact]
    public void Encode_InvalidJson_ReturnsError()
    {
        var result = _service.EncodeBlueprintString("{not valid json");
        using var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("invalid_json", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Encode_MissingRootKey_ReturnsError()
    {
        var result = _service.EncodeBlueprintString("""{"something_else":{}}""");
        using var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("invalid_structure", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Encode_NullOrWhitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.EncodeBlueprintString(""));
        Assert.Throws<ArgumentNullException>(() => _service.EncodeBlueprintString(null!));
    }

    [Fact]
    public void RoundTrip_EncodeAndDecode_PreservesEntities()
    {
        var json = """{"blueprint":{"item":"blueprint","entities":[{"entity_number":1,"name":"stone-furnace","position":{"x":1,"y":1}},{"entity_number":2,"name":"inserter","position":{"x":0,"y":1},"direction":6}],"version":562949954076672}}""";

        // Encode
        var encodeResult = _service.EncodeBlueprintString(json);
        using var encDoc = JsonDocument.Parse(encodeResult);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        // Decode
        var decodeResult = _service.DecodeBlueprintString(bpString);
        using var decDoc = JsonDocument.Parse(decodeResult);

        Assert.True(decDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(2, decDoc.RootElement.GetProperty("entity_count").GetInt32());

        var entities = decDoc.RootElement.GetProperty("entities");
        Assert.Equal("stone-furnace", entities[0].GetProperty("name").GetString());
        Assert.Equal("inserter", entities[1].GetProperty("name").GetString());
        Assert.Equal("west", entities[1].GetProperty("direction").GetString());
    }

    #endregion

    #region ExportAsBlueprint

    [Fact]
    public void Export_SingleEntity_ReturnsBlueprintString()
    {
        var instructions = new List<PlacementInstruction>
        {
            new("stone-furnace", 5, 5, "north", "furnace")
        };

        var result = _service.ExportAsBlueprint(instructions);
        using var doc = JsonDocument.Parse(result);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, doc.RootElement.GetProperty("entity_count").GetInt32());
        var bpString = doc.RootElement.GetProperty("blueprint_string").GetString()!;
        Assert.StartsWith("0", bpString);
    }

    [Fact]
    public void Export_WithLabel_IncludesLabelInBlueprint()
    {
        var instructions = new List<PlacementInstruction>
        {
            new("transport-belt", 0, 0, "south", "input_belt")
        };

        var result = _service.ExportAsBlueprint(instructions, "My Line");
        using var encDoc = JsonDocument.Parse(result);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        // Decode to verify label
        var decoded = _service.DecodeBlueprintString(bpString);
        using var decDoc = JsonDocument.Parse(decoded);
        Assert.Equal("My Line", decDoc.RootElement.GetProperty("label").GetString());
    }

    [Fact]
    public void Export_MultipleEntities_PreservesPositionsAndDirections()
    {
        var instructions = new List<PlacementInstruction>
        {
            new("transport-belt", 0, 0, "south", "input_belt"),
            new("inserter", 1, 0, "west", "inbound_inserter"),
            new("stone-furnace", 2, 0, "north", "furnace"),
            new("inserter", 3, 0, "west", "outbound_inserter"),
            new("transport-belt", 4, 0, "south", "output_belt")
        };

        var result = _service.ExportAsBlueprint(instructions);
        using var encDoc = JsonDocument.Parse(result);
        Assert.Equal(5, encDoc.RootElement.GetProperty("entity_count").GetInt32());

        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;
        var decoded = _service.DecodeBlueprintString(bpString);
        using var decDoc = JsonDocument.Parse(decoded);

        var entities = decDoc.RootElement.GetProperty("entities");
        Assert.Equal(5, entities.GetArrayLength());
        Assert.Equal("transport-belt", entities[0].GetProperty("name").GetString());
        Assert.Equal("south", entities[0].GetProperty("direction").GetString());
        Assert.Equal("inserter", entities[1].GetProperty("name").GetString());
        Assert.Equal("west", entities[1].GetProperty("direction").GetString());
        Assert.Equal("stone-furnace", entities[2].GetProperty("name").GetString());
        Assert.Equal("north", entities[2].GetProperty("direction").GetString());
    }

    [Fact]
    public void Export_WithRecipe_IncludesRecipeInBlueprint()
    {
        var instructions = new List<PlacementInstruction>
        {
            new("assembling-machine-1", 5, 5, "north", "machine", "iron-gear-wheel")
        };

        var result = _service.ExportAsBlueprint(instructions);
        using var encDoc = JsonDocument.Parse(result);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var decoded = _service.DecodeBlueprintString(bpString);
        using var decDoc = JsonDocument.Parse(decoded);
        var rawJson = decDoc.RootElement.GetProperty("raw_json").GetString()!;
        using var rawDoc = JsonDocument.Parse(rawJson);
        var entity = rawDoc.RootElement.GetProperty("blueprint").GetProperty("entities")[0];
        Assert.Equal("iron-gear-wheel", entity.GetProperty("recipe").GetString());
    }

    [Fact]
    public void Export_EmptyList_ReturnsError()
    {
        var result = _service.ExportAsBlueprint(new List<PlacementInstruction>());
        using var doc = JsonDocument.Parse(result);

        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("empty_instructions", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Export_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _service.ExportAsBlueprint(null!));
    }

    [Fact]
    public void Export_NorthDirection_OmitsDirectionField()
    {
        // North (direction=0) should be omitted from the blueprint JSON to match Factorio convention
        var instructions = new List<PlacementInstruction>
        {
            new("stone-furnace", 0, 0, "north", "furnace")
        };

        var result = _service.ExportAsBlueprint(instructions);
        using var encDoc = JsonDocument.Parse(result);
        var bpString = encDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        var decoded = _service.DecodeBlueprintString(bpString);
        using var decDoc = JsonDocument.Parse(decoded);
        var rawJson = decDoc.RootElement.GetProperty("raw_json").GetString()!;
        using var rawDoc = JsonDocument.Parse(rawJson);
        var entity = rawDoc.RootElement.GetProperty("blueprint").GetProperty("entities")[0];

        // direction=0 (north) should not be present in the raw JSON
        Assert.False(entity.TryGetProperty("direction", out _));
    }

    #endregion

    #region Full Round-Trip with PlanSmelterLine output format

    [Fact]
    public void RoundTrip_SmelterLineFormat_WorksEndToEnd()
    {
        // Simulate what PlanSmelterLine outputs in its instructions array
        var instructions = new List<PlacementInstruction>
        {
            new("transport-belt", -2, 0, "south", "input_belt"),
            new("burner-inserter", -1, 0, "west", "inbound_inserter"),
            new("stone-furnace", 0, 0, "north", "furnace"),
            new("burner-inserter", 1, 0, "west", "outbound_inserter"),
            new("transport-belt", 2, 0, "south", "output_belt"),
            new("transport-belt", -2, 3, "south", "input_belt"),
            new("burner-inserter", -1, 3, "west", "inbound_inserter"),
            new("stone-furnace", 0, 3, "north", "furnace"),
            new("burner-inserter", 1, 3, "west", "outbound_inserter"),
            new("transport-belt", 2, 3, "south", "output_belt"),
        };

        // Export to blueprint string
        var exportResult = _service.ExportAsBlueprint(instructions, "2x Stone Furnace Line");
        using var exportDoc = JsonDocument.Parse(exportResult);
        Assert.True(exportDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(10, exportDoc.RootElement.GetProperty("entity_count").GetInt32());
        var bpString = exportDoc.RootElement.GetProperty("blueprint_string").GetString()!;

        // Decode back
        var decodeResult = _service.DecodeBlueprintString(bpString);
        using var decodeDoc = JsonDocument.Parse(decodeResult);
        Assert.True(decodeDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("2x Stone Furnace Line", decodeDoc.RootElement.GetProperty("label").GetString());
        Assert.Equal(10, decodeDoc.RootElement.GetProperty("entity_count").GetInt32());

        var summary = decodeDoc.RootElement.GetProperty("entity_summary");
        Assert.Equal(4, summary.GetProperty("transport-belt").GetInt32());
        Assert.Equal(4, summary.GetProperty("burner-inserter").GetInt32());
        Assert.Equal(2, summary.GetProperty("stone-furnace").GetInt32());
    }

    #endregion
}
