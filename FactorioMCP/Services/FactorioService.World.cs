using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
    /// <summary>
    /// Get a list of entities within the specified radius.
    /// When centerX/centerY are provided, scans around those coordinates instead of the player.
    /// </summary>
    public Task<string> GetNearbyEntitiesAsync(double radius = 10, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{{centerX.Value},{centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local center = {{posExpr}}
            local entities = player.surface.find_entities_filtered{
                position=center, radius={{radius}}
            }
            local dir_names = {}
            for k, v in pairs(defines.direction) do dir_names[v] = k end
            local parts = {}
            for _, e in pairs(entities) do
                local entry = '{"name":"'..e.name..'","x":'..e.position.x..',"y":'..e.position.y
                local dn = dir_names[e.direction]
                if dn then
                    entry = entry..',"direction":"'..dn..'"'
                end
                entry = entry..'}'
                parts[#parts+1] = entry
            end
            rcon.print('{"entities":['..table.concat(parts, ",")..']}') 
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Check the distance from the player to a target position and report whether
    /// it is within build and reach range.
    /// </summary>
    public Task<string> CheckDistanceAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local player_pos = player.position
            local dx = {{x}} - player_pos.x
            local dy = {{y}} - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            local build_ok = distance <= player.build_distance
            local reach_ok = distance <= player.reach_distance
            rcon.print('{"distance":'..string.format("%.1f", distance)..',"build_in_range":'..tostring(build_ok)..',"build_limit":'..player.build_distance..',"reach_in_range":'..tostring(reach_ok)..',"reach_limit":'..player.reach_distance..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Scan for resource patches (ores, oil, etc.) within a radius.
    /// When centerX/centerY are provided, scans around those coordinates instead of the player.
    /// Returns each resource entity's name, position, and remaining amount.
    /// </summary>
    public Task<string> ScanResourcesAsync(double radius = 50, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{{centerX.Value},{centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local center = {{posExpr}}
            local resources = player.surface.find_entities_filtered{
                position=center, radius={{radius}}, type="resource"
            }
            local summary = {}
            for _, r in pairs(resources) do
                local key = r.name
                if not summary[key] then
                    summary[key] = {name=r.name, count=0, total_amount=0, min_x=r.position.x, min_y=r.position.y, max_x=r.position.x, max_y=r.position.y}
                end
                local s = summary[key]
                s.count = s.count + 1
                s.total_amount = s.total_amount + r.amount
                if r.position.x < s.min_x then s.min_x = r.position.x end
                if r.position.y < s.min_y then s.min_y = r.position.y end
                if r.position.x > s.max_x then s.max_x = r.position.x end
                if r.position.y > s.max_y then s.max_y = r.position.y end
            end
            local parts = {}
            for _, s in pairs(summary) do
                parts[#parts+1] = '{"name":"'..s.name..'","patches":'..s.count..',"total_amount":'..s.total_amount..',"center_x":'..string.format("%.1f",(s.min_x+s.max_x)/2)..',"center_y":'..string.format("%.1f",(s.min_y+s.max_y)/2)..'}'
            end
            rcon.print('{"scan_radius":{{radius}},"resources":['..table.concat(parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Scan tiles to get terrain type information.
    /// When centerX/centerY are provided, scans around those coordinates instead of the player.
    /// Returns a summary of tile types found within the specified radius.
    /// </summary>
    public Task<string> ScanTilesAsync(double radius = 16, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{x={centerX.Value},y={centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local pos = {{posExpr}}
            local r = {{radius}}
            local tiles = player.surface.find_tiles_filtered{
                area={{"{"}}{pos.x-r, pos.y-r}, {pos.x+r, pos.y+r}{{"}"}}
            }
            local summary = {}
            for _, t in pairs(tiles) do
                local name = t.name
                if not summary[name] then
                    summary[name] = 0
                end
                summary[name] = summary[name] + 1
            end
            local parts = {}
            for name, count in pairs(summary) do
                parts[#parts+1] = '{"name":"'..name..'","count":'..count..'}'
            end
            rcon.print('{"scan_radius":{{radius}},"tiles":['..table.concat(parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get a comprehensive factory status snapshot in a single RCON call.
    /// Returns player position, inventory summary, crafting queue, research status,
    /// nearby resource summary, and electric network summary. Building memory and
    /// goal status are C#-side and must be queried separately.
    /// </summary>
    public Task<string> GetFactoryStatusAsync(
        double resourceScanRadius = 50,
        double entityScanRadius = 20,
        double electricPoleRadius = 50,
        CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position

            -- Position
            local pos_json = '"position":{"x":'..string.format("%.1f", pos.x)..',"y":'..string.format("%.1f", pos.y)..'}'

            -- Inventory summary
            local inv = player.get_main_inventory()
            local items = {}
            for i = 1, #inv do
                local stack = inv[i]
                if stack.valid_for_read then
                    items[stack.name] = (items[stack.name] or 0) + stack.count
                end
            end
            local inv_parts = {}
            for name, count in pairs(items) do
                inv_parts[#inv_parts+1] = '{"name":"'..name..'","count":'..count..'}'
            end
            local inv_json = '"inventory":{"items":['..table.concat(inv_parts, ",")..'],"total_slots":'..#inv..',"free_slots":'..inv.count_empty_stacks()..'}'

            -- Crafting queue
            local queue = player.crafting_queue
            local queue_parts = {}
            if queue then
                for _, item in pairs(queue) do
                    queue_parts[#queue_parts+1] = '{"recipe":"'..item.recipe..'","count":'..item.count..'}'
                end
            end
            local craft_json = '"crafting_queue":['..table.concat(queue_parts, ",")..']'

            -- Research
            local tech = player.force.current_research
            local research_json
            if tech then
                research_json = '"research":{"active":true,"technology":"'..tech.name..'","progress":'..string.format("%.3f", tech.research_progress)..'}'
            else
                research_json = '"research":{"active":false}'
            end

            -- Nearby resources (compact summary)
            local resources = surface.find_entities_filtered{position=pos, radius={{resourceScanRadius}}, type="resource"}
            local res_summary = {}
            for _, r in pairs(resources) do
                local key = r.name
                if not res_summary[key] then
                    res_summary[key] = {name=r.name, count=0, total_amount=0, cx=0, cy=0}
                end
                local s = res_summary[key]
                s.count = s.count + 1
                s.total_amount = s.total_amount + r.amount
                s.cx = s.cx + r.position.x
                s.cy = s.cy + r.position.y
            end
            local res_parts = {}
            for _, s in pairs(res_summary) do
                res_parts[#res_parts+1] = '{"name":"'..s.name..'","patches":'..s.count..',"total_amount":'..s.total_amount..',"center_x":'..string.format("%.1f", s.cx/s.count)..',"center_y":'..string.format("%.1f", s.cy/s.count)..'}'
            end
            local resources_json = '"nearby_resources":{"scan_radius":{{resourceScanRadius}},"resources":['..table.concat(res_parts, ",")..']}'

            -- Nearby entities summary (non-resource, grouped by type)
            local entities = surface.find_entities_filtered{position=pos, radius={{entityScanRadius}}}
            local ent_summary = {}
            for _, e in pairs(entities) do
                if e.type ~= "resource" and e.name ~= "character" then
                    ent_summary[e.name] = (ent_summary[e.name] or 0) + 1
                end
            end
            local ent_parts = {}
            for name, count in pairs(ent_summary) do
                ent_parts[#ent_parts+1] = '{"name":"'..name..'","count":'..count..'}'
            end
            local entities_json = '"nearby_entities":{"scan_radius":{{entityScanRadius}},"types":['..table.concat(ent_parts, ",")..']}'

            -- Electric network (simplified)
            local power_json
            local poles = surface.find_entities_filtered{type="electric-pole", position=pos, radius={{electricPoleRadius}}}
            if #poles > 0 then
                table.sort(poles, function(a, b)
                    local da = (a.position.x - pos.x)^2 + (a.position.y - pos.y)^2
                    local db = (b.position.x - pos.x)^2 + (b.position.y - pos.y)^2
                    return da < db
                end)
                local pole = poles[1]
                local stats = pole.electric_network_statistics
                local prec = defines.flow_precision_index.five_seconds
                local total_prod = 0
                for name, _ in pairs(stats.output_counts) do
                    total_prod = total_prod + stats.get_flow_count{name=name, category="output", precision_index=prec}
                end
                local total_cons = 0
                for name, _ in pairs(stats.input_counts) do
                    total_cons = total_cons + stats.get_flow_count{name=name, category="input", precision_index=prec}
                end
                local satisfaction = 100.0
                if total_cons > 0 then satisfaction = math.min(100, (total_prod / total_cons) * 100) end
                power_json = '"power":{"available":true,"production_watts":'..string.format("%.1f", total_prod * 60)..',"consumption_watts":'..string.format("%.1f", total_cons * 60)..',"satisfaction_percent":'..string.format("%.1f", satisfaction)..'}'
            else
                power_json = '"power":{"available":false}'
            end

            rcon.print('{'..pos_json..','..inv_json..','..craft_json..','..research_json..','..resources_json..','..entities_json..','..power_json..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
