using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Service for tracing how items flow through belts and inserters in the factory.
/// Maps the directed graph of entity-to-entity connections (belts, inserters, mining drills)
/// so the AI can understand logistics chains, debug clogged belts, and plan new routes.
/// </summary>
internal sealed class FlowService(RconClient rcon)
{
    /// <summary>
    /// Build an item-flow graph for all belts, inserters, and mining drills within a
    /// radius of the player. Returns a list of directed edges (from_entity → to_entity)
    /// with connection type and both endpoints' positions and names.
    /// Suitable for understanding which entities feed which others.
    /// </summary>
    public Task<string> GetFlowGraphAsync(double radius = 30, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            local edges = {}
            local seen_keys = {}
            local function add_edge(from_e, to_e, kind)
                if not from_e or not to_e then return end
                if not from_e.valid or not to_e.valid then return end
                local key = from_e.unit_number..":"..to_e.unit_number
                if seen_keys[key] then return end
                seen_keys[key] = true
                edges[#edges+1] = '{"type":"'..kind..'",'..
                    '"from_name":"'..esc(from_e.name)..'",'..
                    '"from_type":"'..esc(from_e.type)..'",'..
                    '"from_x":'..string.format("%.1f",from_e.position.x)..','..
                    '"from_y":'..string.format("%.1f",from_e.position.y)..','..
                    '"to_name":"'..esc(to_e.name)..'",'..
                    '"to_type":"'..esc(to_e.type)..'",'..
                    '"to_x":'..string.format("%.1f",to_e.position.x)..','..
                    '"to_y":'..string.format("%.1f",to_e.position.y)..'}'
            end
            -- Inserter edges: source -> inserter -> drop target
            local inserters = surface.find_entities_filtered{type="inserter", position=pos, radius={{radius}}}
            for _, ins in pairs(inserters) do
                if ins.valid then
                    local src_ent = nil
                    -- Primary: check pickup_target (precise, works when inserter is active)
                    local ok_p, src = pcall(function() return ins.pickup_target end)
                    if ok_p and src and src.valid then
                        src_ent = src
                    end
                    -- Fallback: find entity at pickup_position
                    if not src_ent then
                        local ok_pp, ppos = pcall(function() return ins.pickup_position end)
                        if ok_pp and ppos then
                            local candidates = surface.find_entities_filtered{position=ppos, radius=1.0, limit=5}
                            for _, c in pairs(candidates) do
                                if c.valid and c ~= ins and c.type ~= "resource" and c.type ~= "item-entity" then
                                    src_ent = c
                                    break
                                end
                            end
                        end
                    end
                    if src_ent then
                        add_edge(src_ent, ins, "inserter_pickup")
                    end
                    local ok_d, dpos = pcall(function() return ins.drop_position end)
                    if ok_d and dpos then
                        local targets = surface.find_entities_filtered{position=dpos, radius=1.5, limit=5}
                        for _, tgt in pairs(targets) do
                            if tgt.valid and tgt ~= ins then
                                add_edge(ins, tgt, "inserter_drop")
                                break
                            end
                        end
                    end
                end
            end
            -- Belt edges: input neighbours -> belt
            local belt_types = {"transport-belt","fast-transport-belt","express-transport-belt","turbo-transport-belt","underground-belt","splitter"}
            for _, bt in pairs(belt_types) do
                local belts = surface.find_entities_filtered{type=bt, position=pos, radius={{radius}}}
                for _, belt in pairs(belts) do
                    if belt.valid then
                        local ok_nb, nb = pcall(function() return belt.belt_neighbours end)
                        if ok_nb and nb then
                            for _, inp in pairs(nb.inputs or {}) do
                                if inp.valid then add_edge(inp, belt, "belt") end
                            end
                        end
                    end
                end
            end
            -- Mining drill edges: drill -> drop position entity
            local drills = surface.find_entities_filtered{type="mining-drill", position=pos, radius={{radius}}}
            for _, drill in pairs(drills) do
                if drill.valid then
                    local ok_d, dpos = pcall(function() return drill.drop_position end)
                    if ok_d and dpos then
                        local targets = surface.find_entities_filtered{position=dpos, radius=0.8, limit=3}
                        for _, tgt in pairs(targets) do
                            if tgt.valid and tgt ~= drill and tgt.type ~= "resource" then
                                add_edge(drill, tgt, "drill_output")
                                break
                            end
                        end
                    end
                end
            end
            rcon.print('{"status":"ok","radius":'..string.format("%.1f",{{radius}})..',"edge_count":'..#edges..',"edges":['..table.concat(edges, ",")..']}'  )
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Trace the downstream flow of items starting from a specific entity.
    /// Performs a breadth-first search from the entity at the given coordinates,
    /// following inserter drops and belt outputs, up to the specified depth.
    /// Returns the chain of nodes (entities) and directed edges the items pass through.
    /// </summary>
    public Task<string> TraceItemFlowAsync(double x, double y, int depth = 5, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local surface = game.connected_players[1].surface
            local pc = game.connected_players[1].character
            -- Find starting entity at coordinates
            local sx = {{x}}
            local sy = {{y}}
            local starts = surface.find_entities_filtered{position={sx, sy}, radius=0.6, limit=5}
            local start_ent = nil
            for _, e in pairs(starts) do
                if e.valid and e ~= pc and e.type ~= "resource" and e.type ~= "item-entity" then
                    start_ent = e
                    break
                end
            end
            if not start_ent then
                rcon.print('{"status":"error","error":"no_entity","x":'..string.format("%.1f",sx)..',"y":'..string.format("%.1f",sy)..'}')
                return
            end
            local max_depth = {{depth}}
            local visited = {}
            local nodes = {}
            local edges = {}
            -- BFS using parallel arrays (avoids nested table constructor braces)
            local bfs_entities = {}
            local bfs_depths = {}
            bfs_entities[1] = start_ent
            bfs_depths[1] = 0
            local belt_types_set = {}
            belt_types_set["transport-belt"] = true
            belt_types_set["fast-transport-belt"] = true
            belt_types_set["express-transport-belt"] = true
            belt_types_set["turbo-transport-belt"] = true
            local function is_simple_belt(e)
                return belt_types_set[e.type] ~= nil
            end
            local function entity_key(e)
                local uid = e.unit_number
                if uid then return uid end
                return e.name..":"..string.format("%.1f",e.position.x)..":"..string.format("%.1f",e.position.y)
            end
            -- Follow a belt chain to its end, returning the last belt and the tile count
            local function follow_belt_chain(first_belt)
                local cur = first_belt
                local count = 1
                local belt_visited = {}
                belt_visited[entity_key(cur)] = true
                while true do
                    local ok_nb, nb = pcall(function() return cur.belt_neighbours end)
                    if not ok_nb or not nb then break end
                    local outputs = nb.outputs or {}
                    if #outputs ~= 1 then break end
                    local nxt = outputs[1]
                    if not nxt.valid then break end
                    local nk = entity_key(nxt)
                    if belt_visited[nk] then break end
                    if not is_simple_belt(nxt) then break end
                    belt_visited[nk] = true
                    visited[nk] = true
                    cur = nxt
                    count = count + 1
                end
                return cur, count
            end
            local function get_inserter_targets(e)
                local out_ents = {}
                local out_kinds = {}
                local ekey = entity_key(e)
                local nearby = surface.find_entities_filtered{type="inserter", position=e.position, radius=3}
                for _, ins in pairs(nearby) do
                    if ins.valid then
                        local matches = false
                        -- Primary: check pickup_target (precise, works when inserter is active)
                        local ok_p, src = pcall(function() return ins.pickup_target end)
                        if ok_p and src and src.valid and entity_key(src) == ekey then
                            matches = true
                        end
                        -- Fallback: check pickup_position inside entity bounding box
                        -- (handles idle machines where pickup_target is nil)
                        if not matches then
                            local ok_pp, ppos = pcall(function() return ins.pickup_position end)
                            if ok_pp and ppos then
                                local ok_bb, bb = pcall(function() return e.bounding_box end)
                                if ok_bb and bb then
                                    local lt = bb.left_top
                                    local rb = bb.right_bottom
                                    if ppos.x >= lt.x - 0.1 and ppos.x <= rb.x + 0.1 and
                                       ppos.y >= lt.y - 0.1 and ppos.y <= rb.y + 0.1 then
                                        matches = true
                                    end
                                end
                            end
                        end
                        if matches then
                            local ok_d, dpos = pcall(function() return ins.drop_position end)
                            if ok_d and dpos then
                                local targets = surface.find_entities_filtered{position=dpos, radius=1.5, limit=5}
                                for _, tgt in pairs(targets) do
                                    if tgt.valid and tgt ~= ins then
                                        out_ents[#out_ents+1] = tgt
                                        out_kinds[#out_kinds+1] = "inserter"
                                        break
                                    end
                                end
                            end
                        end
                    end
                end
                return out_ents, out_kinds
            end
            local function get_outputs(e)
                local out_ents = {}
                local out_kinds = {}
                -- Belt outputs via belt_neighbours (handled by belt collapsing in BFS)
                if is_simple_belt(e) or e.type == "underground-belt" or e.type == "splitter" then
                    local ok_nb, nb = pcall(function() return e.belt_neighbours end)
                    if ok_nb and nb then
                        for _, out_e in pairs(nb.outputs or {}) do
                            if out_e.valid then
                                out_ents[#out_ents+1] = out_e
                                out_kinds[#out_kinds+1] = "belt"
                            end
                        end
                    end
                end
                -- Inserters that pick from this entity
                local ins_ents, ins_kinds = get_inserter_targets(e)
                for i = 1, #ins_ents do
                    out_ents[#out_ents+1] = ins_ents[i]
                    out_kinds[#out_kinds+1] = ins_kinds[i]
                end
                -- Underground belt pair: follow input to output via neighbours
                if e.type == "underground-belt" then
                    local ok_btg, btg = pcall(function() return e.belt_to_ground_type end)
                    if ok_btg and btg == "input" then
                        local ok_pair, paired = pcall(function() return e.neighbours end)
                        if ok_pair and paired and type(paired) ~= "number" then
                            local pent = nil
                            if paired.valid then
                                pent = paired
                            elseif type(paired) == "table" then
                                for _, p in pairs(paired) do
                                    if type(p) ~= "number" and p.valid then pent = p break end
                                end
                            end
                            if pent then
                                local pk = entity_key(pent)
                                local already = false
                                for j = 1, #out_ents do
                                    if entity_key(out_ents[j]) == pk then already = true break end
                                end
                                if not already then
                                    out_ents[#out_ents+1] = pent
                                    out_kinds[#out_kinds+1] = "underground_pair"
                                end
                            end
                        end
                    end
                end
                -- Mining drill drop output
                if e.type == "mining-drill" then
                    local ok_d, dpos = pcall(function() return e.drop_position end)
                    if ok_d and dpos then
                        local targets = surface.find_entities_filtered{position=dpos, radius=0.8, limit=3}
                        for _, tgt in pairs(targets) do
                            if tgt.valid and tgt ~= e and tgt.type ~= "resource" then
                                out_ents[#out_ents+1] = tgt
                                out_kinds[#out_kinds+1] = "drill_output"
                                break
                            end
                        end
                    end
                end
                return out_ents, out_kinds
            end
            local function node_json(e, d, extra)
                local s = '{"name":"'..esc(e.name)..'","type":"'..esc(e.type)..'","x":'..string.format("%.1f",e.position.x)..',"y":'..string.format("%.1f",e.position.y)..',"depth":'..d
                -- Add recipe info for machines (assemblers and furnaces)
                if e.type == "assembling-machine" or e.type == "furnace" then
                    local ok_r, recipe = pcall(function() return e.get_recipe() end)
                    if ok_r and recipe then
                        s = s..',"recipe":"'..esc(recipe.name)..'"'
                    end
                end
                if extra then s = s..extra end
                return s..'}'
            end
            local function edge_json(from_e, to_e, kind, extra)
                local s = '{"from_name":"'..esc(from_e.name)..'","from_x":'..string.format("%.1f",from_e.position.x)..',"from_y":'..string.format("%.1f",from_e.position.y)..',"to_name":"'..esc(to_e.name)..'","to_x":'..string.format("%.1f",to_e.position.x)..',"to_y":'..string.format("%.1f",to_e.position.y)..',"kind":"'..kind..'"'
                if extra then s = s..extra end
                return s..'}'
            end
            -- BFS loop
            local start_key = entity_key(start_ent)
            visited[start_key] = true
            nodes[#nodes+1] = node_json(start_ent, 0, nil)
            local head = 1
            while head <= #bfs_entities do
                local cur_ent = bfs_entities[head]
                local cur_depth = bfs_depths[head]
                head = head + 1
                if cur_depth < max_depth then
                    local out_ents, out_kinds = get_outputs(cur_ent)
                    for i = 1, #out_ents do
                        local out_e = out_ents[i]
                        local kind = out_kinds[i]
                        local key = entity_key(out_e)
                        -- Belt collapsing: follow simple belt chains into a single segment node
                        if kind == "belt" and is_simple_belt(out_e) and not visited[key] then
                            visited[key] = true
                            local last_belt, belt_len = follow_belt_chain(out_e)
                            -- The segment node represents the whole belt run
                            local seg_extra = ',"belt_length":'..belt_len..',"end_x":'..string.format("%.1f",last_belt.position.x)..',"end_y":'..string.format("%.1f",last_belt.position.y)
                            nodes[#nodes+1] = node_json(out_e, cur_depth, seg_extra)
                            edges[#edges+1] = edge_json(cur_ent, out_e, "belt_segment", ',"belt_length":'..belt_len)
                            -- Continue BFS from the end of the belt segment (same depth — belts are free)
                            bfs_entities[#bfs_entities+1] = last_belt
                            bfs_depths[#bfs_depths+1] = cur_depth
                        elseif kind == "underground_pair" and not visited[key] then
                            -- Underground belt traversal: free depth like belt segments
                            visited[key] = true
                            nodes[#nodes+1] = node_json(out_e, cur_depth, nil)
                            edges[#edges+1] = edge_json(cur_ent, out_e, "underground_pair", nil)
                            bfs_entities[#bfs_entities+1] = out_e
                            bfs_depths[#bfs_depths+1] = cur_depth
                        elseif not visited[key] then
                            visited[key] = true
                            local next_depth = cur_depth + 1
                            nodes[#nodes+1] = node_json(out_e, next_depth, nil)
                            edges[#edges+1] = edge_json(cur_ent, out_e, kind, nil)
                            bfs_entities[#bfs_entities+1] = out_e
                            bfs_depths[#bfs_depths+1] = next_depth
                        end
                    end
                end
            end
            rcon.print('{"status":"ok","start_name":"'..esc(start_ent.name)..'","start_x":'..string.format("%.1f",start_ent.position.x)..',"start_y":'..string.format("%.1f",start_ent.position.y)..',"node_count":'..#nodes..',"nodes":['..table.concat(nodes, ",")..'],"edges":['..table.concat(edges, ",")..']}'  )
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Preview what a transport belt placed at (x,y) facing the given direction would
    /// connect to. Shows input belts, output belt, nearby inserters, and whether
    /// placement is possible — the belt equivalent of PreviewInserterPlacement.
    /// </summary>
    public Task<string> PreviewBeltPlacementAsync(double x, double y, string direction, string beltType = "transport-belt", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        ArgumentException.ThrowIfNullOrWhiteSpace(beltType);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local surface = game.connected_players[1].surface
            local player = game.connected_players[1]
            local dir = defines.direction.{{direction}}
            local bx, by = {{x}}, {{y}}

            if not dir then
                rcon.print('{"success":false,"error":"invalid_direction","direction":"{{direction}}"}')
                return
            end

            -- Direction offsets for belt output (items flow in the facing direction)
            local offsets = {}
            offsets[defines.direction.north]     = {dx=0,  dy=-1}
            offsets[defines.direction.south]     = {dx=0,  dy=1}
            offsets[defines.direction.east]      = {dx=1,  dy=0}
            offsets[defines.direction.west]      = {dx=-1, dy=0}
            local off = offsets[dir]
            if not off then
                rcon.print('{"success":false,"error":"belt_requires_cardinal","direction":"{{direction}}"}')
                return
            end

            -- Output position (where items flow to)
            local out_x, out_y = bx + off.dx, by + off.dy
            -- Input positions (where items can flow from): behind and both sides
            local behind_x, behind_y = bx - off.dx, by - off.dy
            local left_x, left_y = bx - off.dy, by + off.dx
            local right_x, right_y = bx + off.dy, by - off.dx

            local function find_entities_at(px, py)
                local parts = {}
                local ents = surface.find_entities_filtered{position={px, py}, radius=0.6, limit=5}
                for _, e in pairs(ents) do
                    if e.name ~= "character" then
                        parts[#parts+1] = '{"name":"'..esc(e.name)..'","type":"'..esc(e.type)..'"}'
                    end
                end
                return parts
            end

            -- Check output side
            local out_parts = find_entities_at(out_x, out_y)
            -- Check input sides
            local behind_parts = find_entities_at(behind_x, behind_y)
            local left_parts = find_entities_at(left_x, left_y)
            local right_parts = find_entities_at(right_x, right_y)

            -- Find inserters that would interact with this belt position
            local inserter_parts = {}
            local nearby_inserters = surface.find_entities_filtered{type="inserter", position={bx, by}, radius=2}
            for _, ins in pairs(nearby_inserters) do
                if ins.valid then
                    local ok_p, pp = pcall(function() return ins.pickup_position end)
                    local ok_d, dp = pcall(function() return ins.drop_position end)
                    local role = nil
                    if ok_p and pp and math.abs(pp.x - bx) < 0.6 and math.abs(pp.y - by) < 0.6 then
                        role = "picks_from_belt"
                    elseif ok_d and dp and math.abs(dp.x - bx) < 0.6 and math.abs(dp.y - by) < 0.6 then
                        role = "drops_onto_belt"
                    end
                    if role then
                        inserter_parts[#inserter_parts+1] = '{"name":"'..esc(ins.name)..'","x":'..string.format("%.1f",ins.position.x)..',"y":'..string.format("%.1f",ins.position.y)..',"role":"'..role..'"}'
                    end
                end
            end

            -- Check if belt can be placed
            local belt_name = "{{beltType}}"
            local can_place = surface.can_place_entity{name=belt_name, position={bx, by}, force=player.force, direction=dir}

            -- Check existing entity at belt position
            local existing_parts = find_entities_at(bx, by)

            rcon.print('{"success":true'..
                ',"belt_position":{"x":'..bx..',"y":'..by..'}'..
                ',"direction":"{{direction}}"'..
                ',"belt_type":"'..belt_name..'"'..
                ',"output":{"x":'..out_x..',"y":'..out_y..',"entities":['..table.concat(out_parts, ",")..']}'..
                ',"input_behind":{"x":'..behind_x..',"y":'..behind_y..',"entities":['..table.concat(behind_parts, ",")..']}'..
                ',"input_left":{"x":'..left_x..',"y":'..left_y..',"entities":['..table.concat(left_parts, ",")..']}'..
                ',"input_right":{"x":'..right_x..',"y":'..right_y..',"entities":['..table.concat(right_parts, ",")..']}'..
                ',"inserters":['..table.concat(inserter_parts, ",")..']'..
                ',"existing_at_position":['..table.concat(existing_parts, ",")..']'..
                ',"can_place":'..tostring(can_place)..
                '}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get a compact summary of item flow connections in the area.
    /// Shows inserter-mediated connections between machines and drill outputs.
    /// Filters out belt-to-belt inserter transfers to keep output compact.
    /// Designed for inclusion in factory status snapshots.
    /// </summary>
    public Task<string> GetFlowSummaryAsync(double radius = 30, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            local edges = {}
            local belt_types = {}
            belt_types["transport-belt"] = true
            belt_types["fast-transport-belt"] = true
            belt_types["express-transport-belt"] = true
            belt_types["turbo-transport-belt"] = true
            belt_types["underground-belt"] = true
            belt_types["splitter"] = true
            local inserters = surface.find_entities_filtered{type="inserter", position=pos, radius={{radius}}}
            for _, ins in pairs(inserters) do
                if ins.valid then
                    local src, dst
                    -- Primary: check pickup_target
                    local ok_p, pickup = pcall(function() return ins.pickup_target end)
                    if ok_p and pickup and pickup.valid then src = pickup end
                    -- Fallback: find entity at pickup_position
                    if not src then
                        local ok_pp, ppos = pcall(function() return ins.pickup_position end)
                        if ok_pp and ppos then
                            local candidates = surface.find_entities_filtered{position=ppos, radius=1.0, limit=5}
                            for _, c in pairs(candidates) do
                                if c.valid and c ~= ins and c.type ~= "resource" and c.type ~= "item-entity" then
                                    src = c
                                    break
                                end
                            end
                        end
                    end
                    local ok_d, dpos = pcall(function() return ins.drop_position end)
                    if ok_d and dpos then
                        local targets = surface.find_entities_filtered{position=dpos, radius=1.5, limit=5}
                        for _, tgt in pairs(targets) do
                            if tgt.valid and tgt ~= ins then dst = tgt break end
                        end
                    end
                    if src and dst then
                        local src_belt = belt_types[src.type] ~= nil
                        local dst_belt = belt_types[dst.type] ~= nil
                        if not (src_belt and dst_belt) then
                            edges[#edges+1] = '{"from":"'..esc(src.name)..'",'..
                                '"from_x":'..string.format("%.1f",src.position.x)..','..
                                '"from_y":'..string.format("%.1f",src.position.y)..','..
                                '"to":"'..esc(dst.name)..'",'..
                                '"to_x":'..string.format("%.1f",dst.position.x)..','..
                                '"to_y":'..string.format("%.1f",dst.position.y)..'}'
                        end
                    end
                end
            end
            local drills = surface.find_entities_filtered{type="mining-drill", position=pos, radius={{radius}}}
            for _, drill in pairs(drills) do
                if drill.valid then
                    local ok, dt = pcall(function() return drill.drop_target end)
                    if ok and dt and dt.valid then
                        edges[#edges+1] = '{"from":"'..esc(drill.name)..'",'..
                            '"from_x":'..string.format("%.1f",drill.position.x)..','..
                            '"from_y":'..string.format("%.1f",drill.position.y)..','..
                            '"to":"'..esc(dt.name)..'",'..
                            '"to_x":'..string.format("%.1f",dt.position.x)..','..
                            '"to_y":'..string.format("%.1f",dt.position.y)..'}'
                    end
                end
            end
            rcon.print('['..table.concat(edges, ",")..']')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
