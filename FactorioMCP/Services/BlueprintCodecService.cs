using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactorioMCP.Models;

namespace FactorioMCP.Services;

/// <summary>
/// Pure C# service for decoding and encoding Factorio blueprint strings.
/// Blueprint format: version char ('0') + base64(zlib-deflate(JSON)).
/// Supports blueprints, blueprint books, deconstruction planners, and upgrade planners.
/// </summary>
internal sealed class BlueprintCodecService
{
    private static readonly Dictionary<string, int> DirectionToNumber = new(StringComparer.OrdinalIgnoreCase)
    {
        ["north"] = 0,
        ["northeast"] = 2,
        ["east"] = 4,
        ["southeast"] = 6,
        ["south"] = 8,
        ["southwest"] = 10,
        ["west"] = 12,
        ["northwest"] = 14,
    };

    private static readonly Dictionary<int, string> NumberToDirection = new()
    {
        [0] = "north",
        [2] = "northeast",
        [4] = "east",
        [6] = "southeast",
        [8] = "south",
        [10] = "southwest",
        [12] = "west",
        [14] = "northwest",
    };

    /// <summary>
    /// Decode a Factorio blueprint string into its JSON representation.
    /// Returns a human-readable JSON summary with entity counts and details.
    /// </summary>
    public string DecodeBlueprintString(string blueprintString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintString);

        if (blueprintString[0] != '0')
            return JsonSerializer.Serialize(new { success = false, error = "invalid_version", message = $"Expected version byte '0', got '{blueprintString[0]}'" });

        try
        {
            var base64 = blueprintString.AsSpan(1);
            var compressed = Convert.FromBase64String(base64.ToString());
            var decompressed = ZlibDecompress(compressed);
            var json = Encoding.UTF8.GetString(decompressed);
            var doc = JsonDocument.Parse(json);

            // Build a rich summary
            return BuildDecodeSummary(doc, json);
        }
        catch (FormatException)
        {
            return JsonSerializer.Serialize(new { success = false, error = "invalid_base64", message = "Blueprint string contains invalid base64 data" });
        }
        catch (InvalidDataException)
        {
            return JsonSerializer.Serialize(new { success = false, error = "invalid_compression", message = "Blueprint string contains invalid compressed data" });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = "invalid_json", message = $"Decompressed data is not valid JSON: {ex.Message}" });
        }
    }

    /// <summary>
    /// Encode a JSON object into a Factorio blueprint string.
    /// Accepts the full blueprint JSON (with "blueprint" or "blueprint_book" root key).
    /// </summary>
    public string EncodeBlueprintString(string blueprintJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintJson);

        try
        {
            // Validate it's valid JSON first
            using var doc = JsonDocument.Parse(blueprintJson);
            var root = doc.RootElement;

            // Must have a blueprint or blueprint_book root
            if (!root.TryGetProperty("blueprint", out _) &&
                !root.TryGetProperty("blueprint_book", out _) &&
                !root.TryGetProperty("deconstruction_planner", out _) &&
                !root.TryGetProperty("upgrade_planner", out _))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "invalid_structure",
                    message = "JSON must have a 'blueprint', 'blueprint_book', 'deconstruction_planner', or 'upgrade_planner' root key"
                });
            }

            var jsonBytes = Encoding.UTF8.GetBytes(blueprintJson);
            var compressed = ZlibCompress(jsonBytes);
            var base64 = Convert.ToBase64String(compressed);
            var blueprintString = "0" + base64;

            return JsonSerializer.Serialize(new
            {
                success = true,
                blueprint_string = blueprintString,
                json_size = blueprintJson.Length,
                compressed_size = compressed.Length,
                final_size = blueprintString.Length
            });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = "invalid_json", message = ex.Message });
        }
    }

    /// <summary>
    /// Convert PlacementInstruction[] to a Factorio blueprint string.
    /// </summary>
    public string ExportAsBlueprint(IReadOnlyList<PlacementInstruction> instructions, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(instructions);

        if (instructions.Count == 0)
            return JsonSerializer.Serialize(new { success = false, error = "empty_instructions", message = "No placement instructions to export" });

        var entities = new JsonArray();
        for (int i = 0; i < instructions.Count; i++)
        {
            var inst = instructions[i];
            var entity = new JsonObject
            {
                ["entity_number"] = i + 1,
                ["name"] = inst.EntityName,
                ["position"] = new JsonObject
                {
                    ["x"] = inst.X,
                    ["y"] = inst.Y
                }
            };

            if (DirectionToNumber.TryGetValue(inst.Direction, out var dirNum) && dirNum != 0)
            {
                entity["direction"] = dirNum;
            }

            if (inst.Recipe is not null)
            {
                entity["recipe"] = inst.Recipe;
            }

            entities.Add(entity);
        }

        var blueprint = new JsonObject
        {
            ["item"] = "blueprint",
            ["entities"] = entities,
            ["version"] = 562949958402048 // Factorio 2.0 version
        };

        if (label is not null)
            blueprint["label"] = label;

        var root = new JsonObject
        {
            ["blueprint"] = blueprint
        };

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var compressed = ZlibCompress(jsonBytes);
        var base64 = Convert.ToBase64String(compressed);
        var blueprintString = "0" + base64;

        return JsonSerializer.Serialize(new
        {
            success = true,
            blueprint_string = blueprintString,
            entity_count = instructions.Count,
            final_size = blueprintString.Length
        });
    }

    private static string BuildDecodeSummary(JsonDocument doc, string rawJson)
    {
        var root = doc.RootElement;

        if (root.TryGetProperty("blueprint_book", out var book))
            return BuildBookSummary(book, rawJson);

        if (root.TryGetProperty("blueprint", out var bp))
            return BuildSingleBlueprintSummary(bp, rawJson);

        if (root.TryGetProperty("deconstruction_planner", out _))
            return JsonSerializer.Serialize(new { success = true, type = "deconstruction_planner", raw_json = rawJson });

        if (root.TryGetProperty("upgrade_planner", out _))
            return JsonSerializer.Serialize(new { success = true, type = "upgrade_planner", raw_json = rawJson });

        return JsonSerializer.Serialize(new { success = true, type = "unknown", raw_json = rawJson });
    }

    private static string BuildSingleBlueprintSummary(JsonElement bp, string rawJson)
    {
        var label = bp.TryGetProperty("label", out var l) ? l.GetString() : null;

        var entities = bp.TryGetProperty("entities", out var ents) ? ents : default;
        int entityCount = entities.ValueKind == JsonValueKind.Array ? entities.GetArrayLength() : 0;

        // Count entity types
        var entityCounts = new Dictionary<string, int>();
        if (entities.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in entities.EnumerateArray())
            {
                var name = entity.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
                entityCounts[name] = entityCounts.GetValueOrDefault(name) + 1;
            }
        }

        // Build entity list with directions resolved
        var entityList = new List<object>();
        if (entities.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in entities.EnumerateArray())
            {
                var name = entity.TryGetProperty("name", out var n) ? n.GetString() : "unknown";
                double x = 0, y = 0;
                if (entity.TryGetProperty("position", out var pos))
                {
                    x = pos.TryGetProperty("x", out var px) ? px.GetDouble() : 0;
                    y = pos.TryGetProperty("y", out var py) ? py.GetDouble() : 0;
                }
                var dirNum = entity.TryGetProperty("direction", out var d) ? d.GetInt32() : 0;
                NumberToDirection.TryGetValue(dirNum, out var dirName);
                dirName ??= "north";

                string? recipe = entity.TryGetProperty("recipe", out var r) ? r.GetString() : null;

                var entry = new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["x"] = x,
                    ["y"] = y,
                    ["direction"] = dirName
                };
                if (recipe != null)
                    entry["recipe"] = recipe;

                entityList.Add(entry);
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            type = "blueprint",
            label,
            entity_count = entityCount,
            entity_summary = entityCounts,
            entities = entityList,
            raw_json = rawJson
        }, new JsonSerializerOptions { WriteIndented = false });
    }

    private static string BuildBookSummary(JsonElement book, string rawJson)
    {
        var label = book.TryGetProperty("label", out var l) ? l.GetString() : null;
        int blueprintCount = 0;
        var blueprintLabels = new List<string?>();

        if (book.TryGetProperty("blueprints", out var bps) && bps.ValueKind == JsonValueKind.Array)
        {
            blueprintCount = bps.GetArrayLength();
            foreach (var entry in bps.EnumerateArray())
            {
                if (entry.TryGetProperty("blueprint", out var bp))
                {
                    var bpLabel = bp.TryGetProperty("label", out var bl) ? bl.GetString() : null;
                    blueprintLabels.Add(bpLabel);
                }
                else if (entry.TryGetProperty("blueprint_book", out _))
                {
                    blueprintLabels.Add("[nested book]");
                }
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            type = "blueprint_book",
            label,
            blueprint_count = blueprintCount,
            blueprint_labels = blueprintLabels,
            raw_json = rawJson
        }, new JsonSerializerOptions { WriteIndented = false });
    }

    private static byte[] ZlibDecompress(byte[] data)
    {
        // Factorio uses zlib format (RFC 1950): 2-byte header + deflate data + 4-byte checksum
        // Skip the 2-byte zlib header, decompress the deflate stream
        if (data.Length < 2)
            throw new InvalidDataException("Data too short for zlib format");

        using var input = new MemoryStream(data, 2, data.Length - 2);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        // Write zlib format: 2-byte header (CMF=0x78, FLG=0x01 for default compression)
        // + deflate data + 4-byte Adler-32 checksum
        using var output = new MemoryStream();

        // Zlib header: CM=8 (deflate), CINFO=7 (32K window) → CMF=0x78
        // FLG: FCHECK so that (CMF*256 + FLG) % 31 == 0 → 0x01
        output.WriteByte(0x78);
        output.WriteByte(0x01);

        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        // Adler-32 checksum (big-endian)
        var adler = ComputeAdler32(data);
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);

        return output.ToArray();
    }

    private static uint ComputeAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        const uint mod = 65521;
        foreach (var byte_ in data)
        {
            a = (a + byte_) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }
}
