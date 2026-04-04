using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
    /// <summary>
    /// Poll the crafting queue until it is empty or the timeout expires.
    /// Returns the final queue state as JSON.
    /// </summary>
    public async Task<string> WaitForCraftingAsync(
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await GetCraftingQueueAsync(cancellationToken);
            if (TryParseJsonArray(result, "queue", out var queue) && queue == "[]")
                return """{"status":"complete","queue":[]}""";
            await Task.Delay(pollInterval, cancellationToken);
        }

        var finalQueue = await GetCraftingQueueAsync(cancellationToken);
        return $$"""{"status":"timeout","remaining":{{finalQueue}}}""";
    }

    /// <summary>
    /// Poll the player position until it is within the given tolerance of the target
    /// coordinates, or the timeout expires. Returns the final position as JSON.
    /// </summary>
    public async Task<string> WaitForPositionAsync(
        double targetX,
        double targetY,
        double tolerance,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tolerance, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lua = string.Create(CultureInfo.InvariantCulture, $$"""
                local p = game.connected_players[1].position
                local dx = p.x - {{targetX}}
                local dy = p.y - {{targetY}}
                local dist = math.sqrt(dx*dx + dy*dy)
                rcon.print('{"x":'..p.x..',"y":'..p.y..',"distance":'..string.format("%.2f", dist)..'}')
                """);
            var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);
            if (TryParseJsonDouble(result, "distance", out var dist) && dist <= tolerance)
                return $$"""{"status":"arrived","tolerance":{{string.Format(CultureInfo.InvariantCulture, "{0}", tolerance)}},"position":{{result}}}""";
            await Task.Delay(pollInterval, cancellationToken);
        }

        var finalPos = await GetPlayerPositionAsync(cancellationToken);
        return $$"""{"status":"timeout","target_x":{{string.Format(CultureInfo.InvariantCulture, "{0}", targetX)}},"target_y":{{string.Format(CultureInfo.InvariantCulture, "{0}", targetY)}},"position":{{finalPos}}}""";
    }

    /// <summary>
    /// Get the current game tick count.
    /// </summary>
    public Task<string> GetGameTickAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            rcon.print('{"tick":'..game.tick..'}')
            """, cancellationToken);
    }

    /// <summary>
    /// Wait for a specified number of game ticks to elapse.
    /// Polls the game tick and waits until the target tick count has passed.
    /// </summary>
    public async Task<string> WaitForTicksAsync(
        int ticks,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var startResult = await GetGameTickAsync(cancellationToken);
        if (!TryParseJsonLong(startResult, "tick", out var startTick))
            return """{"status":"error","error":"failed_to_read_tick"}""";

        var targetTick = startTick + ticks;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(pollInterval, cancellationToken);

            var tickResult = await GetGameTickAsync(cancellationToken);
            if (TryParseJsonLong(tickResult, "tick", out var currentTick) && currentTick >= targetTick)
                return string.Create(CultureInfo.InvariantCulture, $$$"""{"status":"complete","start_tick":{{{startTick}}},"end_tick":{{{currentTick}}},"elapsed":{{{currentTick - startTick}}}}""");
        }

        var finalResult = await GetGameTickAsync(cancellationToken);
        TryParseJsonLong(finalResult, "tick", out var finalTick);
        return string.Create(CultureInfo.InvariantCulture, $$$"""{"status":"timeout","start_tick":{{{startTick}}},"current_tick":{{{finalTick}}},"target_tick":{{{targetTick}}}}""");
    }

    /// <summary>
    /// Poll the player's inventory until it contains at least <paramref name="targetCount"/>
    /// of the specified item, or the timeout expires.
    /// </summary>
    public async Task<string> WaitForItemCountAsync(
        string itemName,
        int targetCount,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetCount);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local count = game.connected_players[1].get_item_count("{{itemName}}")
            rcon.print('{"item":"{{itemName}}","count":'..count..'}')
            """);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);
            if (TryParseJsonInt(result, "count", out var count) && count >= targetCount)
                return string.Create(CultureInfo.InvariantCulture,
                    $$"""{"status":"satisfied","item":"{{itemName}}","count":{{count}},"target":{{targetCount}}}""");
            await Task.Delay(pollInterval, cancellationToken);
        }

        var final = await rcon.ExecuteLuaAsync(lua, cancellationToken);
        TryParseJsonInt(final, "count", out var finalCount);
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"status":"timeout","item":"{{itemName}}","count":{{finalCount}},"target":{{targetCount}}}""");
    }

    /// <summary>
    /// Poll an entity's status at the specified position until it matches
    /// <paramref name="targetStatus"/>, or the timeout expires.
    /// Status names follow <c>defines.entity_status</c> (e.g. "working", "no_fuel",
    /// "item_ingredient_shortage", "no_power").
    /// </summary>
    public async Task<string> WaitForEntityStatusAsync(
        double x,
        double y,
        string targetStatus,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetStatus);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
            local entities = game.connected_players[1].surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            sort_entities(entities)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local status_name = "unknown"
            if e.status then
                local status_names = {}
                for k, v in pairs(defines.entity_status) do status_names[v] = k end
                status_name = status_names[e.status] or "unknown"
            end
            rcon.print('{"entity":"'..esc(e.name)..'","status":"'..status_name..'"}')
            """);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);

            if (result.Contains("\"error\":\"no_entity\""))
                return string.Create(CultureInfo.InvariantCulture,
                    $$"""{"status":"error","error":"no_entity","x":{{x}},"y":{{y}}}""");

            if (TryParseJsonString(result, "status", out var currentStatus)
                && string.Equals(currentStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
            {
                TryParseJsonString(result, "entity", out var entityName);
                return string.Create(CultureInfo.InvariantCulture,
                    $$"""{"status":"satisfied","entity":"{{entityName}}","entity_status":"{{currentStatus}}","target_status":"{{targetStatus}}","x":{{x}},"y":{{y}}}""");
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        // Timeout — report final state
        var final = await rcon.ExecuteLuaAsync(lua, cancellationToken);
        TryParseJsonString(final, "entity", out var finalEntity);
        TryParseJsonString(final, "status", out var finalStatus);
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"status":"timeout","entity":"{{finalEntity}}","entity_status":"{{finalStatus}}","target_status":"{{targetStatus}}","x":{{x}},"y":{{y}}}""");
    }

    /// <summary>
    /// Poll an entity's inventory at the specified position until it contains at least
    /// <paramref name="targetCount"/> of the specified item, or the timeout expires.
    /// </summary>
    public async Task<string> WaitForEntityInventoryAsync(
        double x,
        double y,
        string itemName,
        int targetCount,
        string inventoryType,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryType);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
            local entities = game.connected_players[1].surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            sort_entities(entities)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local inv_map = {
                fuel = defines.inventory.fuel,
                furnace_source = defines.inventory.furnace_source,
                furnace_result = defines.inventory.furnace_result,
                chest = defines.inventory.chest,
                assembling_machine_input = defines.inventory.assembling_machine_input,
                assembling_machine_output = defines.inventory.assembling_machine_output
            }
            local inv_type = inv_map["{{inventoryType}}"]
            if not inv_type then
                rcon.print('{"error":"invalid_inventory_type","inventory_type":"{{inventoryType}}"}')
                return
            end
            local inv = e.get_inventory(inv_type)
            if not inv then
                rcon.print('{"error":"no_inventory","entity":"'..esc(e.name)..'","inventory_type":"{{inventoryType}}"}')
                return
            end
            local count = inv.get_item_count("{{itemName}}")
            rcon.print('{"entity":"'..esc(e.name)..'","item":"{{itemName}}","count":'..count..',"inventory_type":"{{inventoryType}}"}')
            """);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);

            if (result.Contains("\"error\":"))
            {
                // Persistent errors (no entity, invalid inventory) — fail immediately
                if (result.Contains("no_entity"))
                    return string.Create(CultureInfo.InvariantCulture,
                        $$"""{"status":"error","error":"no_entity","x":{{x}},"y":{{y}}}""");
                if (result.Contains("invalid_inventory_type"))
                    return string.Create(CultureInfo.InvariantCulture,
                        $$"""{"status":"error","error":"invalid_inventory_type","inventory_type":"{{inventoryType}}"}""");
                if (result.Contains("no_inventory"))
                    return string.Create(CultureInfo.InvariantCulture,
                        $$"""{"status":"error","error":"no_inventory","inventory_type":"{{inventoryType}}","x":{{x}},"y":{{y}}}""");
            }

            if (TryParseJsonInt(result, "count", out var count) && count >= targetCount)
            {
                TryParseJsonString(result, "entity", out var entityName);
                return string.Create(CultureInfo.InvariantCulture,
                    $$"""{"status":"satisfied","entity":"{{entityName}}","item":"{{itemName}}","count":{{count}},"target":{{targetCount}},"inventory_type":"{{inventoryType}}","x":{{x}},"y":{{y}}}""");
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        // Timeout — report final state
        var final = await rcon.ExecuteLuaAsync(lua, cancellationToken);
        TryParseJsonString(final, "entity", out var finalEntity);
        TryParseJsonInt(final, "count", out var finalCount);
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"status":"timeout","entity":"{{finalEntity}}","item":"{{itemName}}","count":{{finalCount}},"target":{{targetCount}},"inventory_type":"{{inventoryType}}","x":{{x}},"y":{{y}}}""");
    }

    // ── JSON helpers for simple value extraction ────────────────────

    private static bool TryParseJsonInt(string json, string key, out int value)
    {
        value = 0;
        var marker = $"\"{key}\":";
        var idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        var start = idx + marker.Length;
        var end = json.IndexOfAny([',', '}'], start);
        if (end < 0) return false;
        return int.TryParse(json.AsSpan(start, end - start).Trim(), CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseJsonLong(string json, string key, out long value)
    {
        value = 0;
        var marker = $"\"{key}\":";
        var idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        var start = idx + marker.Length;
        var end = json.IndexOfAny([',', '}'], start);
        if (end < 0) return false;
        return long.TryParse(json.AsSpan(start, end - start).Trim(), CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseJsonDouble(string json, string key, out double value)
    {
        value = 0;
        var marker = $"\"{key}\":";
        var idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        var start = idx + marker.Length;
        var end = json.IndexOfAny([',', '}'], start);
        if (end < 0) return false;
        return double.TryParse(json.AsSpan(start, end - start).Trim(), CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseJsonString(string json, string key, out string? value)
    {
        value = null;
        var marker = $"\"{key}\":\"";
        var idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        var start = idx + marker.Length;
        // Find closing quote, skipping escaped quotes
        var pos = start;
        while (pos < json.Length)
        {
            var ch = json[pos];
            if (ch == '"')
                break;
            if (ch == '\\' && pos + 1 < json.Length)
            {
                pos += 2; // skip escaped character
                continue;
            }
            pos++;
        }
        if (pos >= json.Length) return false;
        value = json[start..pos].Replace("\\\"", "\"").Replace("\\\\", "\\");
        return true;
    }

    private static bool TryParseJsonArray(string json, string key, out string value)
    {
        value = "";
        var marker = $"\"{key}\":";
        var idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        var start = idx + marker.Length;
        // Skip whitespace
        while (start < json.Length && json[start] == ' ') start++;
        if (start >= json.Length || json[start] != '[') return false;
        // Find matching closing bracket
        var depth = 0;
        var pos = start;
        while (pos < json.Length)
        {
            if (json[pos] == '[') depth++;
            else if (json[pos] == ']') { depth--; if (depth == 0) break; }
            pos++;
        }
        if (depth != 0) return false;
        value = json[start..(pos + 1)];
        return true;
    }
}
