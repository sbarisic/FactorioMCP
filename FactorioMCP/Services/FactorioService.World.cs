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
            local force = player.force
            local tech = force.current_research
            local research_json
            if tech then
                research_json = '"research":{"active":true,"technology":"'..tech.name..'","progress":'..string.format("%.3f", force.research_progress)..'}'
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

    /// <summary>
    /// Find the nearest entity of the specified type or name within a search radius.
    /// Returns position, distance, and entity details. Searches by entity name first,
    /// then falls back to entity type if no match is found by name.
    /// </summary>
    public Task<string> FindNearestEntityAsync(string entityType, double radius = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local pos = player.position
            local surface = player.surface
            local filter = "{{entityType}}"
            local entities = surface.find_entities_filtered{name=filter, position=pos, radius={{radius}}}
            if #entities == 0 then
                entities = surface.find_entities_filtered{type=filter, position=pos, radius={{radius}}}
            end
            if #entities == 0 then
                rcon.print('{"success":false,"error":"not_found","filter":"'..filter..'","radius":{{radius}}}')
                return
            end
            local best = nil
            local best_dist = math.huge
            for _, e in pairs(entities) do
                local dx = e.position.x - pos.x
                local dy = e.position.y - pos.y
                local d = math.sqrt(dx*dx + dy*dy)
                if d < best_dist then
                    best = e
                    best_dist = d
                end
            end
            local dir_names = {}
            for k, v in pairs(defines.direction) do dir_names[v] = k end
            local result = '{"success":true,"entity":"'..best.name..'","type":"'..best.type..'","x":'..best.position.x..',"y":'..best.position.y..',"distance":'..string.format("%.1f", best_dist)
            local dn = dir_names[best.direction]
            if dn then result = result..',"direction":"'..dn..'"' end
            if best.type == "resource" then result = result..',"amount":'..best.amount end
            result = result..',"total_found":'..#entities..'}'
            rcon.print(result)
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Find the best resource patch of the specified resource type within a search radius.
    /// "Best" is determined by a heuristic: closest patches with high amounts are preferred.
    /// Returns the patch center, total amount, entity count, distance, and nearby alternative patches.
    /// </summary>
    public Task<string> FindBestResourcePatchAsync(string resourceName, double radius = 200, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local pos = player.position
            local surface = player.surface
            local resources = surface.find_entities_filtered{name="{{resourceName}}", position=pos, radius={{radius}}, type="resource"}
            if #resources == 0 then
                rcon.print('{"success":false,"error":"not_found","resource":"{{resourceName}}","radius":{{radius}}}')
                return
            end
            -- Cluster resources into patches using a grid (8-tile cells)
            local cell_size = 8
            local cells = {}
            for _, r in pairs(resources) do
                local cx = math.floor(r.position.x / cell_size)
                local cy = math.floor(r.position.y / cell_size)
                local key = cx..","..cy
                if not cells[key] then
                    cells[key] = {count=0, total_amount=0, sum_x=0, sum_y=0}
                end
                local c = cells[key]
                c.count = c.count + 1
                c.total_amount = c.total_amount + r.amount
                c.sum_x = c.sum_x + r.position.x
                c.sum_y = c.sum_y + r.position.y
            end
            -- Build patch list with center and distance
            local patches = {}
            for _, c in pairs(cells) do
                local cx = c.sum_x / c.count
                local cy = c.sum_y / c.count
                local dx = cx - pos.x
                local dy = cy - pos.y
                local dist = math.sqrt(dx*dx + dy*dy)
                patches[#patches+1] = {center_x=cx, center_y=cy, count=c.count, total_amount=c.total_amount, distance=dist}
            end
            -- Sort by heuristic: score = total_amount / (distance + 10) — prefer close, rich patches
            table.sort(patches, function(a, b)
                return (a.total_amount / (a.distance + 10)) > (b.total_amount / (b.distance + 10))
            end)
            local best = patches[1]
            local alt_parts = {}
            for i = 2, math.min(#patches, 4) do
                local p = patches[i]
                alt_parts[#alt_parts+1] = '{"center_x":'..string.format("%.1f", p.center_x)..',"center_y":'..string.format("%.1f", p.center_y)..',"count":'..p.count..',"total_amount":'..p.total_amount..',"distance":'..string.format("%.1f", p.distance)..'}'
            end
            rcon.print('{"success":true,"resource":"{{resourceName}}","best_patch":{"center_x":'..string.format("%.1f", best.center_x)..',"center_y":'..string.format("%.1f", best.center_y)..',"count":'..best.count..',"total_amount":'..best.total_amount..',"distance":'..string.format("%.1f", best.distance)..'},"total_entities":'..#resources..',"total_patches":'..#patches..',"alternatives":['..table.concat(alt_parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Summarize an area around the player or a given center, returning a structured
    /// overview of resources, machines (grouped by type with status), threats
    /// (enemies), and free-space estimate.
    /// </summary>
    public Task<string> SummarizeAreaAsync(double radius = 50, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{{centerX.Value},{centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local center = {{posExpr}}

            -- Resources
            local res = surface.find_entities_filtered{position=center, radius={{radius}}, type="resource"}
            local res_sum = {}
            for _, r in pairs(res) do
                local key = r.name
                if not res_sum[key] then
                    res_sum[key] = {name=r.name, count=0, total_amount=0, sx=0, sy=0}
                end
                local s = res_sum[key]
                s.count = s.count + 1
                s.total_amount = s.total_amount + r.amount
                s.sx = s.sx + r.position.x
                s.sy = s.sy + r.position.y
            end
            local res_parts = {}
            for _, s in pairs(res_sum) do
                res_parts[#res_parts+1] = '{"name":"'..s.name..'","count":'..s.count..',"total_amount":'..s.total_amount..',"center_x":'..string.format("%.1f", s.sx/s.count)..',"center_y":'..string.format("%.1f", s.sy/s.count)..'}'
            end

            -- Machines / structures (non-resource, non-character entities)
            local all = surface.find_entities_filtered{position=center, radius={{radius}}}
            local machines = {}
            local entity_count = 0
            for _, e in pairs(all) do
                if e.type ~= "resource" and e.name ~= "character" then
                    entity_count = entity_count + 1
                    local key = e.name
                    if not machines[key] then
                        machines[key] = {name=e.name, type=e.type, count=0, working=0, idle=0}
                    end
                    local m = machines[key]
                    m.count = m.count + 1
                    local st = e.status
                    if st == defines.entity_status.working or st == defines.entity_status.normal then
                        m.working = m.working + 1
                    elseif st ~= nil then
                        m.idle = m.idle + 1
                    end
                end
            end
            local mach_parts = {}
            for _, m in pairs(machines) do
                mach_parts[#mach_parts+1] = '{"name":"'..m.name..'","type":"'..m.type..'","count":'..m.count..',"working":'..m.working..',"idle":'..m.idle..'}'
            end

            -- Threats (enemies)
            local enemies = surface.find_entities_filtered{position=center, radius={{radius}}, force="enemy"}
            local threat_sum = {}
            for _, e in pairs(enemies) do
                threat_sum[e.name] = (threat_sum[e.name] or 0) + 1
            end
            local threat_parts = {}
            for name, count in pairs(threat_sum) do
                threat_parts[#threat_parts+1] = '{"name":"'..name..'","count":'..count..'}'
            end

            -- Free space estimate: total tiles minus occupied tiles
            local r = {{radius}}
            local total_tiles = math.floor((2*r)*(2*r))
            local occupied = entity_count + #res
            local free_pct = 100 * (1 - occupied / total_tiles)
            if free_pct < 0 then free_pct = 0 end

            local free_json = '"free_space":{"total_tiles":'..total_tiles..',"occupied":'..occupied..',"free_percent":'..string.format("%.1f", free_pct)..'}'

            rcon.print('{"center_x":'..string.format("%.1f", center[1] or center.x or 0)..',"center_y":'..string.format("%.1f", center[2] or center.y or 0)..',"radius":{{radius}},'
                ..'"resources":['..table.concat(res_parts, ",")..'],'
                ..'"machines":['..table.concat(mach_parts, ",")..'],'
                ..'"threats":['..table.concat(threat_parts, ",")..'],'
                ..free_json..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Look in a compass direction from the player and report what entities
    /// are along that line within the specified range. Uses a narrow cone
    /// (width parameter) to capture entities along the look direction.
    /// </summary>
    public Task<string> LookInDirectionAsync(string direction, double range = 30, double width = 3, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        // Calculate direction vector for the scan rectangle
        // Factorio coords: +X = east, +Y = south
        var (dx, dy) = direction.ToLowerInvariant() switch
        {
            "north" => (0.0, -1.0),
            "south" => (0.0, 1.0),
            "east" => (1.0, 0.0),
            "west" => (-1.0, 0.0),
            "northeast" => (0.707, -0.707),
            "northwest" => (-0.707, -0.707),
            "southeast" => (0.707, 0.707),
            "southwest" => (-0.707, 0.707),
            _ => throw new ArgumentException($"Invalid direction: {direction}. Use north/south/east/west/northeast/northwest/southeast/southwest.")
        };

        // perpendicular vector
        var (px, py) = (-dy, dx);
        var halfWidth = width / 2.0;

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local pos = player.position
            local surface = player.surface
            local dx, dy = {{dx}}, {{dy}}
            local px, py = {{px}}, {{py}}
            local range = {{range}}
            local hw = {{halfWidth}}

            -- Build a bounding box for the directional scan area
            local x1 = math.min(pos.x, pos.x + dx*range) - math.abs(px)*hw - 1
            local y1 = math.min(pos.y, pos.y + dy*range) - math.abs(py)*hw - 1
            local x2 = math.max(pos.x, pos.x + dx*range) + math.abs(px)*hw + 1
            local y2 = math.max(pos.y, pos.y + dy*range) + math.abs(py)*hw + 1

            local entities = surface.find_entities_filtered{area={{"{"}}{x1, y1}, {x2, y2}{{"}"}} }

            -- Filter to entities within the narrow cone
            local results = {}
            local dir_names = {}
            for k, v in pairs(defines.direction) do dir_names[v] = k end

            for _, e in pairs(entities) do
                if e.name ~= "character" then
                    local ex = e.position.x - pos.x
                    local ey = e.position.y - pos.y
                    -- Project onto direction vector
                    local proj = ex*dx + ey*dy
                    -- Perpendicular distance
                    local perp = math.abs(ex*px + ey*py)
                    if proj > 0 and proj <= range and perp <= hw then
                        local dist = math.sqrt(ex*ex + ey*ey)
                        local entry = '{"name":"'..e.name..'","type":"'..e.type..'","x":'..string.format("%.1f", e.position.x)..',"y":'..string.format("%.1f", e.position.y)..',"distance":'..string.format("%.1f", dist)
                        local dn = dir_names[e.direction]
                        if dn then entry = entry..',"direction":"'..dn..'"' end
                        if e.type == "resource" then entry = entry..',"amount":'..e.amount end
                        entry = entry..'}'
                        results[#results+1] = {dist=dist, json=entry}
                    end
                end
            end

            -- Sort by distance (closest first)
            table.sort(results, function(a, b) return a.dist < b.dist end)

            local parts = {}
            for _, r in pairs(results) do
                parts[#parts+1] = r.json
            end

            rcon.print('{"direction":"{{direction}}","range":{{range}},"width":{{width}},"entities":['..table.concat(parts, ",")..'],"total_found":'..#parts..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Find a buildable rectangular area (width × height) near the player or a given center.
    /// Scans outward in a spiral-like pattern checking for tile regions free of entities
    /// and non-water tiles. Returns the first suitable area found.
    /// </summary>
    public Task<string> FindBuildableAreaAsync(int width, int height, double searchRadius = 50, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(searchRadius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{{centerX.Value},{centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local center = {{posExpr}}
            local cx = center[1] or center.x
            local cy = center[2] or center.y
            local w = {{width}}
            local h = {{height}}
            local search_r = {{searchRadius}}

            -- Scan in expanding rings from center, step by 2 tiles
            local best = nil
            local step = 2
            for dist = 0, search_r, step do
                for ox = -dist, dist, step do
                    for oy = -dist, dist, step do
                        -- Only check cells on the current ring (skip inner)
                        if math.abs(ox) == dist or math.abs(oy) == dist or dist == 0 then
                            local ax = math.floor(cx + ox)
                            local ay = math.floor(cy + oy)
                            -- Check for water tiles
                            local tiles = surface.find_tiles_filtered{
                                area={{"{"}}{ax, ay}, {ax+w, ay+h}{{"}"}},
                                name={"water", "deepwater", "water-green", "water-mud", "water-shallow"}
                            }
                            if #tiles == 0 then
                                -- Check for blocking entities (non-resource)
                                local blocking = surface.find_entities_filtered{
                                    area={{"{"}}{ax, ay}, {ax+w, ay+h}{{"}"}},
                                    type={"resource", "character"},
                                    invert=true
                                }
                                if #blocking == 0 then
                                    local d = math.sqrt((ax + w/2 - cx)^2 + (ay + h/2 - cy)^2)
                                    best = {x=ax, y=ay, distance=d}
                                    break
                                end
                            end
                        end
                    end
                    if best then break end
                end
                if best then break end
            end

            if best then
                rcon.print('{"success":true,"x":'..best.x..',"y":'..best.y..',"width":'..w..',"height":'..h..',"center_x":'..string.format("%.1f", best.x + w/2)..',"center_y":'..string.format("%.1f", best.y + h/2)..',"distance":'..string.format("%.1f", best.distance)..'}')
            else
                rcon.print('{"success":false,"error":"no_area_found","width":'..w..',"height":'..h..',"search_radius":'..search_r..'}')
            end
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
