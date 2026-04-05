using System.Text.Json;
using System.Text.Json.Nodes;

namespace FactorioMCP.Services;

/// <summary>
/// Pure C# service for analyzing decoded blueprints offline — no RCON required.
/// Builds flow graphs from entity positions/directions, identifies inserter connections,
/// traces belt chains, and reports layout issues.
/// Works entirely from the blueprint JSON data.
/// </summary>
internal sealed class BlueprintAnalysisService(BlueprintCodecService codec)
{
    // Entity size in tiles (width, height) for bounding-box hit-testing.
    // Positions in blueprints are always the center of the entity.
    private static readonly Dictionary<string, (int W, int H)> EntitySizes = new(StringComparer.OrdinalIgnoreCase)
    {
        // 1x1
        ["transport-belt"] = (1, 1),
        ["fast-transport-belt"] = (1, 1),
        ["express-transport-belt"] = (1, 1),
        ["turbo-transport-belt"] = (1, 1),
        ["inserter"] = (1, 1),
        ["burner-inserter"] = (1, 1),
        ["fast-inserter"] = (1, 1),
        ["bulk-inserter"] = (1, 1),
        ["long-handed-inserter"] = (1, 1),
        ["stack-inserter"] = (1, 1),
        ["small-electric-pole"] = (1, 1),
        ["pipe"] = (1, 1),
        ["pipe-to-ground"] = (1, 1),
        ["small-lamp"] = (1, 1),
        ["wooden-chest"] = (1, 1),
        ["iron-chest"] = (1, 1),
        ["steel-chest"] = (1, 1),
        ["splitter"] = (2, 1),
        ["fast-splitter"] = (2, 1),
        ["express-splitter"] = (2, 1),
        ["turbo-splitter"] = (2, 1),
        ["underground-belt"] = (1, 1),
        ["fast-underground-belt"] = (1, 1),
        ["express-underground-belt"] = (1, 1),
        ["turbo-underground-belt"] = (1, 1),
        // 2x2
        ["stone-furnace"] = (2, 2),
        ["steel-furnace"] = (2, 2),
        ["medium-electric-pole"] = (1, 1),
        ["pump"] = (1, 2),
        ["gun-turret"] = (2, 2),
        ["boiler"] = (2, 3),
        // 3x3
        ["assembling-machine-1"] = (3, 3),
        ["assembling-machine-2"] = (3, 3),
        ["assembling-machine-3"] = (3, 3),
        ["electric-furnace"] = (3, 3),
        ["chemical-plant"] = (3, 3),
        ["lab"] = (3, 3),
        ["radar"] = (3, 3),
        ["electric-mining-drill"] = (3, 3),
        ["big-electric-pole"] = (2, 2),
        ["substation"] = (2, 2),
        // 5x5
        ["oil-refinery"] = (5, 5),
        ["centrifuge"] = (3, 3),
        ["foundry"] = (4, 4),
        ["biochamber"] = (4, 4),
        ["electromagnetic-plant"] = (4, 4),
        ["cryogenic-plant"] = (4, 4),
    };

    // Inserter reach distances: how far behind they pick up and how far ahead they drop.
    private static readonly HashSet<string> LongInserters = new(StringComparer.OrdinalIgnoreCase)
    {
        "long-handed-inserter"
    };

    private static readonly HashSet<string> BeltTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "transport-belt", "fast-transport-belt", "express-transport-belt", "turbo-transport-belt"
    };

    private static readonly HashSet<string> UndergroundBeltTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "underground-belt", "fast-underground-belt", "express-underground-belt", "turbo-underground-belt"
    };

    private static readonly HashSet<string> SplitterTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "splitter", "fast-splitter", "express-splitter", "turbo-splitter"
    };

    private static readonly HashSet<string> InserterTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "inserter", "burner-inserter", "fast-inserter", "bulk-inserter",
        "long-handed-inserter", "stack-inserter"
    };

    private static readonly Dictionary<int, string> NumberToDirection = new()
    {
        [0] = "north",
        [1] = "northeast",
        [2] = "east",
        [3] = "southeast",
        [4] = "south",
        [5] = "southwest",
        [6] = "west",
        [7] = "northwest",
    };

    /// <summary>
    /// Analyze a blueprint string: decode it, build flow graph, identify issues.
    /// Returns a structured analysis with entity summary, dimensions, flow edges, and issues.
    /// </summary>
    public string AnalyzeBlueprint(string blueprintString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintString);

        // Decode
        var decoded = codec.DecodeBlueprintString(blueprintString);
        using var decodedDoc = JsonDocument.Parse(decoded);
        var decodedRoot = decodedDoc.RootElement;

        if (decodedRoot.TryGetProperty("success", out var s) && !s.GetBoolean())
            return decoded; // Pass through decode errors

        if (decodedRoot.TryGetProperty("type", out var t) && t.GetString() != "blueprint")
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "not_a_blueprint",
                message = $"Flow analysis only works on single blueprints, got '{t.GetString()}'"
            });

        // Parse entities from the raw JSON
        if (!decodedRoot.TryGetProperty("raw_json", out var rawJsonEl))
            return JsonSerializer.Serialize(new { success = false, error = "no_raw_json", message = "Decoded blueprint missing raw_json" });

        var rawJson = rawJsonEl.GetString()!;
        using var bpDoc = JsonDocument.Parse(rawJson);
        var bpRoot = bpDoc.RootElement;

        if (!bpRoot.TryGetProperty("blueprint", out var bp))
            return JsonSerializer.Serialize(new { success = false, error = "no_blueprint_key", message = "Missing 'blueprint' key in raw JSON" });

        var entities = ParseEntities(bp);
        if (entities.Count == 0)
            return JsonSerializer.Serialize(new
            {
                success = true,
                entity_count = 0,
                message = "Blueprint contains no entities"
            });

        var label = bp.TryGetProperty("label", out var lbl) ? lbl.GetString() : null;

        // Build entity summary
        var entitySummary = new Dictionary<string, int>();
        foreach (var e in entities)
            entitySummary[e.Name] = entitySummary.GetValueOrDefault(e.Name) + 1;

        // Compute dimensions
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var e in entities)
        {
            var (w, h) = GetEntitySize(e.Name);
            double halfW = w / 2.0, halfH = h / 2.0;
            if (e.X - halfW < minX) minX = e.X - halfW;
            if (e.Y - halfH < minY) minY = e.Y - halfH;
            if (e.X + halfW > maxX) maxX = e.X + halfW;
            if (e.Y + halfH > maxY) maxY = e.Y + halfH;
        }

        // Build flow graph
        var edges = BuildFlowGraph(entities);

        // Detect issues
        var issues = DetectIssues(entities, edges);

        // Categorize entities
        var categories = CategorizeEntities(entities);

        // Build belt chain summary
        var beltChains = TraceBeltChains(entities, edges);

        return JsonSerializer.Serialize(new
        {
            success = true,
            label,
            entity_count = entities.Count,
            entity_summary = entitySummary,
            categories,
            dimensions = new
            {
                min_x = Math.Round(minX, 1),
                min_y = Math.Round(minY, 1),
                max_x = Math.Round(maxX, 1),
                max_y = Math.Round(maxY, 1),
                width = Math.Round(maxX - minX, 1),
                height = Math.Round(maxY - minY, 1)
            },
            flow_graph = new
            {
                edge_count = edges.Count,
                edges = edges.Select(e => new
                {
                    from_entity = e.FromNum,
                    from_name = e.FromName,
                    to_entity = e.ToNum,
                    to_name = e.ToName,
                    type = e.Type
                })
            },
            belt_chains = beltChains,
            issue_count = issues.Count,
            issues
        }, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Trace flow from a specific entity in a blueprint.
    /// Performs BFS from the given entity number, following belt connections and inserter drops,
    /// and returns the downstream chain. Belt segments are collapsed into single entries.
    /// </summary>
    public string TraceBlueprintFlow(string blueprintString, int startEntityNumber, int maxDepth = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintString);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);

        var decoded = codec.DecodeBlueprintString(blueprintString);
        using var decodedDoc = JsonDocument.Parse(decoded);
        var decodedRoot = decodedDoc.RootElement;

        if (decodedRoot.TryGetProperty("success", out var s) && !s.GetBoolean())
            return decoded;

        if (decodedRoot.TryGetProperty("type", out var t) && t.GetString() != "blueprint")
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "not_a_blueprint",
                message = $"Flow tracing only works on single blueprints, got '{t.GetString()}'"
            });

        if (!decodedRoot.TryGetProperty("raw_json", out var rawJsonEl))
            return JsonSerializer.Serialize(new { success = false, error = "no_raw_json" });

        using var bpDoc = JsonDocument.Parse(rawJsonEl.GetString()!);
        if (!bpDoc.RootElement.TryGetProperty("blueprint", out var bp))
            return JsonSerializer.Serialize(new { success = false, error = "no_blueprint_key" });

        var entities = ParseEntities(bp);
        var startEntity = entities.FirstOrDefault(e => e.Number == startEntityNumber);
        if (startEntity is null)
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "entity_not_found",
                message = $"No entity with number {startEntityNumber} in blueprint. Valid range: 1-{entities.Count}"
            });

        var edges = BuildFlowGraph(entities);

        // BFS from start entity
        var adjacency = new Dictionary<int, List<FlowEdge>>();
        foreach (var e in edges)
        {
            if (!adjacency.ContainsKey(e.FromNum))
                adjacency[e.FromNum] = [];
            adjacency[e.FromNum].Add(e);
        }

        var visited = new HashSet<int> { startEntityNumber };
        var queue = new Queue<(int entityNum, int depth)>();
        queue.Enqueue((startEntityNumber, 0));

        var traceNodes = new List<object>();
        var traceEdges = new List<object>();

        // Add start node
        traceNodes.Add(new
        {
            entity_number = startEntity.Number,
            name = startEntity.Name,
            x = startEntity.X,
            y = startEntity.Y,
            direction = startEntity.DirectionName,
            recipe = startEntity.Recipe,
            depth = 0
        });

        while (queue.Count > 0)
        {
            var (currentNum, currentDepth) = queue.Dequeue();
            if (currentDepth >= maxDepth) continue;

            if (!adjacency.TryGetValue(currentNum, out var outEdges)) continue;

            foreach (var edge in outEdges)
            {
                traceEdges.Add(new
                {
                    from_entity = edge.FromNum,
                    from_name = edge.FromName,
                    to_entity = edge.ToNum,
                    to_name = edge.ToName,
                    type = edge.Type
                });

                if (visited.Add(edge.ToNum))
                {
                    var targetEntity = entities.First(e => e.Number == edge.ToNum);
                    // Belt-to-belt edges don't cost depth
                    int nextDepth = IsBeltEntity(edge.FromName) && IsBeltEntity(edge.ToName)
                        ? currentDepth
                        : currentDepth + 1;

                    traceNodes.Add(new
                    {
                        entity_number = targetEntity.Number,
                        name = targetEntity.Name,
                        x = targetEntity.X,
                        y = targetEntity.Y,
                        direction = targetEntity.DirectionName,
                        recipe = targetEntity.Recipe,
                        depth = nextDepth
                    });

                    queue.Enqueue((edge.ToNum, nextDepth));
                }
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            start_entity = startEntityNumber,
            start_name = startEntity.Name,
            node_count = traceNodes.Count,
            edge_count = traceEdges.Count,
            nodes = traceNodes,
            edges = traceEdges
        }, new JsonSerializerOptions { WriteIndented = false });
    }

    // ── Internal types ──────────────────────────────────────────────────

    internal sealed record BpEntity(
        int Number,
        string Name,
        double X,
        double Y,
        int Direction,
        string DirectionName,
        string? Recipe);

    internal sealed record FlowEdge(
        int FromNum,
        string FromName,
        int ToNum,
        string ToName,
        string Type);

    // ── Entity parsing ──────────────────────────────────────────────────

    internal static List<BpEntity> ParseEntities(JsonElement blueprint)
    {
        var result = new List<BpEntity>();
        if (!blueprint.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var ent in entities.EnumerateArray())
        {
            int number = ent.TryGetProperty("entity_number", out var en) ? en.GetInt32() : 0;
            string name = ent.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
            double x = 0, y = 0;
            if (ent.TryGetProperty("position", out var pos))
            {
                x = pos.TryGetProperty("x", out var px) ? px.GetDouble() : 0;
                y = pos.TryGetProperty("y", out var py) ? py.GetDouble() : 0;
            }
            int dir = ent.TryGetProperty("direction", out var d) ? d.GetInt32() : 0;
            NumberToDirection.TryGetValue(dir, out var dirName);
            dirName ??= "north";
            string? recipe = ent.TryGetProperty("recipe", out var r) ? r.GetString() : null;

            result.Add(new BpEntity(number, name, x, y, dir, dirName, recipe));
        }
        return result;
    }

    // ── Flow graph construction ─────────────────────────────────────────

    internal List<FlowEdge> BuildFlowGraph(List<BpEntity> entities)
    {
        var edges = new List<FlowEdge>();

        // Build spatial index: map grid positions to entities that occupy those tiles
        var positionIndex = BuildPositionIndex(entities);

        foreach (var entity in entities)
        {
            if (InserterTypes.Contains(entity.Name))
            {
                AddInserterEdges(entity, entities, positionIndex, edges);
            }
            else if (IsBeltEntity(entity.Name))
            {
                AddBeltEdges(entity, entities, positionIndex, edges);
            }
        }

        return edges;
    }

    private static void AddInserterEdges(
        BpEntity inserter,
        List<BpEntity> allEntities,
        Dictionary<(int, int), List<BpEntity>> positionIndex,
        List<FlowEdge> edges)
    {
        int reach = LongInserters.Contains(inserter.Name) ? 2 : 1;
        var (dx, dy) = DirectionOffset(inserter.Direction);

        // Inserter faces the DROP direction: pickup is behind, drop is in front
        double pickupX = inserter.X - dx * reach;
        double pickupY = inserter.Y - dy * reach;
        double dropX = inserter.X + dx * reach;
        double dropY = inserter.Y + dy * reach;

        var pickupTarget = FindEntityAt(pickupX, pickupY, positionIndex, inserter.Number);
        var dropTarget = FindEntityAt(dropX, dropY, positionIndex, inserter.Number);

        if (pickupTarget is not null)
            edges.Add(new FlowEdge(pickupTarget.Number, pickupTarget.Name, inserter.Number, inserter.Name, "inserter_pickup"));

        if (dropTarget is not null)
            edges.Add(new FlowEdge(inserter.Number, inserter.Name, dropTarget.Number, dropTarget.Name, "inserter_drop"));
    }

    private static void AddBeltEdges(
        BpEntity belt,
        List<BpEntity> allEntities,
        Dictionary<(int, int), List<BpEntity>> positionIndex,
        List<FlowEdge> edges)
    {
        // Belt outputs items in its facing direction
        var (dx, dy) = DirectionOffset(belt.Direction);
        double outputX = belt.X + dx;
        double outputY = belt.Y + dy;

        var outputTarget = FindEntityAt(outputX, outputY, positionIndex, belt.Number);
        if (outputTarget is not null && IsBeltOrTransportEntity(outputTarget.Name))
        {
            edges.Add(new FlowEdge(belt.Number, belt.Name, outputTarget.Number, outputTarget.Name, "belt"));
        }
    }

    // ── Spatial index ───────────────────────────────────────────────────

    private static Dictionary<(int, int), List<BpEntity>> BuildPositionIndex(List<BpEntity> entities)
    {
        var index = new Dictionary<(int, int), List<BpEntity>>();
        foreach (var e in entities)
        {
            var (w, h) = GetEntitySize(e.Name);
            // For entities whose size depends on direction (e.g. splitters), swap w/h
            if (e.Direction is 0 or 4) // north/south — splitters are 2-wide horizontal by default
            {
                // default orientation
            }
            else if (e.Direction is 2 or 6) // east/west — swap for splitters
            {
                if (SplitterTypes.Contains(e.Name))
                    (w, h) = (h, w);
            }

            // Register all tiles this entity occupies
            int startTileX = (int)Math.Floor(e.X - w / 2.0 + 0.01);
            int startTileY = (int)Math.Floor(e.Y - h / 2.0 + 0.01);
            for (int tx = startTileX; tx < startTileX + w; tx++)
            {
                for (int ty = startTileY; ty < startTileY + h; ty++)
                {
                    var key = (tx, ty);
                    if (!index.ContainsKey(key))
                        index[key] = [];
                    index[key].Add(e);
                }
            }
        }
        return index;
    }

    private static BpEntity? FindEntityAt(double x, double y, Dictionary<(int, int), List<BpEntity>> positionIndex, int excludeNumber)
    {
        int tileX = (int)Math.Floor(x);
        int tileY = (int)Math.Floor(y);

        if (positionIndex.TryGetValue((tileX, tileY), out var candidates))
        {
            foreach (var c in candidates)
            {
                if (c.Number != excludeNumber)
                    return c;
            }
        }
        return null;
    }

    // ── Issue detection ─────────────────────────────────────────────────

    private static List<object> DetectIssues(List<BpEntity> entities, List<FlowEdge> edges)
    {
        var issues = new List<object>();

        // Build sets of entities that participate in edges
        var hasPickup = new HashSet<int>();
        var hasDrop = new HashSet<int>();
        var hasAnyConnection = new HashSet<int>();

        foreach (var edge in edges)
        {
            hasAnyConnection.Add(edge.FromNum);
            hasAnyConnection.Add(edge.ToNum);

            if (edge.Type == "inserter_pickup")
                hasPickup.Add(edge.ToNum); // The inserter is the "to" in pickup
            if (edge.Type == "inserter_drop")
                hasDrop.Add(edge.FromNum); // The inserter is the "from" in drop
        }

        foreach (var entity in entities)
        {
            if (InserterTypes.Contains(entity.Name))
            {
                bool hasP = hasPickup.Contains(entity.Number);
                bool hasD = hasDrop.Contains(entity.Number);

                if (!hasP && !hasD)
                {
                    issues.Add(new
                    {
                        entity_number = entity.Number,
                        name = entity.Name,
                        x = entity.X,
                        y = entity.Y,
                        issue = "orphaned_inserter",
                        message = "Inserter has no pickup source and no drop target in this blueprint"
                    });
                }
                else if (!hasP)
                {
                    issues.Add(new
                    {
                        entity_number = entity.Number,
                        name = entity.Name,
                        x = entity.X,
                        y = entity.Y,
                        issue = "no_pickup_target",
                        message = "Inserter has no pickup source in this blueprint"
                    });
                }
                else if (!hasD)
                {
                    issues.Add(new
                    {
                        entity_number = entity.Number,
                        name = entity.Name,
                        x = entity.X,
                        y = entity.Y,
                        issue = "no_drop_target",
                        message = "Inserter has no drop target in this blueprint"
                    });
                }
            }

            // Dead-end belts: belt that outputs to nothing (and isn't at the edge of the blueprint)
            if (IsBeltEntity(entity.Name))
            {
                bool outputsToSomething = edges.Any(e => e.FromNum == entity.Number && e.Type == "belt");
                bool receivesFromSomething = edges.Any(e => e.ToNum == entity.Number);
                if (!outputsToSomething && receivesFromSomething)
                {
                    // This belt receives items but has nowhere to send them — possible dead end
                    issues.Add(new
                    {
                        entity_number = entity.Number,
                        name = entity.Name,
                        x = entity.X,
                        y = entity.Y,
                        issue = "dead_end_belt",
                        message = "Belt receives items but has no belt output — items may back up here"
                    });
                }
            }
        }

        return issues;
    }

    // ── Entity categorization ───────────────────────────────────────────

    private static Dictionary<string, List<object>> CategorizeEntities(List<BpEntity> entities)
    {
        var categories = new Dictionary<string, List<object>>();

        foreach (var e in entities)
        {
            string category;
            if (InserterTypes.Contains(e.Name)) category = "inserters";
            else if (IsBeltEntity(e.Name)) category = "belts";
            else if (IsMachine(e.Name)) category = "machines";
            else if (IsPowerEntity(e.Name)) category = "power";
            else if (IsLogisticsEntity(e.Name)) category = "logistics";
            else category = "other";

            if (!categories.ContainsKey(category))
                categories[category] = [];

            var entry = new Dictionary<string, object?>
            {
                ["entity_number"] = e.Number,
                ["name"] = e.Name,
                ["x"] = e.X,
                ["y"] = e.Y,
                ["direction"] = e.DirectionName
            };
            if (e.Recipe is not null)
                entry["recipe"] = e.Recipe;

            categories[category].Add(entry);
        }

        return categories;
    }

    // ── Belt chain tracing ──────────────────────────────────────────────

    private static List<object> TraceBeltChains(List<BpEntity> entities, List<FlowEdge> edges)
    {
        // Find contiguous belt chains and report them as single entries
        var beltEdges = edges.Where(e => e.Type == "belt").ToList();
        var beltEntityNums = new HashSet<int>(
            entities.Where(e => IsBeltEntity(e.Name)).Select(e => e.Number));

        // Build adjacency for belt-only edges
        var beltNext = new Dictionary<int, int>();
        var beltPrev = new Dictionary<int, int>();
        foreach (var e in beltEdges)
        {
            if (beltEntityNums.Contains(e.FromNum) && beltEntityNums.Contains(e.ToNum))
            {
                beltNext[e.FromNum] = e.ToNum;
                beltPrev[e.ToNum] = e.FromNum;
            }
        }

        // Find chain starts (belts with no predecessor)
        var chainStarts = beltEntityNums.Where(n => !beltPrev.ContainsKey(n)).ToList();
        var visited = new HashSet<int>();
        var chains = new List<object>();

        var entityLookup = entities.ToDictionary(e => e.Number);

        foreach (var start in chainStarts)
        {
            if (visited.Contains(start)) continue;

            var chain = new List<int>();
            int current = start;
            while (true)
            {
                if (!visited.Add(current)) break;
                chain.Add(current);
                if (!beltNext.TryGetValue(current, out var next)) break;
                current = next;
            }

            if (chain.Count > 0)
            {
                var startEntity = entityLookup[chain[0]];
                var endEntity = entityLookup[chain[^1]];
                chains.Add(new
                {
                    length = chain.Count,
                    belt_type = startEntity.Name,
                    start_x = startEntity.X,
                    start_y = startEntity.Y,
                    end_x = endEntity.X,
                    end_y = endEntity.Y,
                    direction = startEntity.DirectionName
                });
            }
        }

        return chains;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    internal static (int W, int H) GetEntitySize(string name)
    {
        if (EntitySizes.TryGetValue(name, out var size))
            return size;
        // Default: assume 1x1 for unknown entities
        return (1, 1);
    }

    private static (int dx, int dy) DirectionOffset(int direction) => direction switch
    {
        0 => (0, -1),  // north
        1 => (1, -1),  // northeast
        2 => (1, 0),   // east
        3 => (1, 1),   // southeast
        4 => (0, 1),   // south
        5 => (-1, 1),  // southwest
        6 => (-1, 0),  // west
        7 => (-1, -1), // northwest
        _ => (0, 0)
    };

    private static bool IsBeltEntity(string name) =>
        BeltTypes.Contains(name) || UndergroundBeltTypes.Contains(name) || SplitterTypes.Contains(name);

    private static bool IsBeltOrTransportEntity(string name) =>
        IsBeltEntity(name) || InserterTypes.Contains(name);

    private static bool IsMachine(string name) => name switch
    {
        "stone-furnace" or "steel-furnace" or "electric-furnace" => true,
        "assembling-machine-1" or "assembling-machine-2" or "assembling-machine-3" => true,
        "chemical-plant" or "oil-refinery" or "centrifuge" => true,
        "foundry" or "biochamber" or "electromagnetic-plant" or "cryogenic-plant" => true,
        "electric-mining-drill" or "lab" => true,
        _ => false
    };

    private static bool IsPowerEntity(string name) => name switch
    {
        "small-electric-pole" or "medium-electric-pole" or "big-electric-pole" or "substation" => true,
        "solar-panel" or "accumulator" or "boiler" or "steam-engine" or "steam-turbine" => true,
        _ => false
    };

    private static bool IsLogisticsEntity(string name) => name switch
    {
        "wooden-chest" or "iron-chest" or "steel-chest" => true,
        "logistic-chest-passive-provider" or "logistic-chest-active-provider" => true,
        "logistic-chest-storage" or "logistic-chest-requester" or "logistic-chest-buffer" => true,
        _ => false
    };
}
