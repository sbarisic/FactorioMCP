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
                    local ok_p, src = pcall(function() return ins.pickup_target end)
                    if ok_p and src and src.valid then
                        add_edge(src, ins, "inserter_pickup")
                    end
                    local ok_d, dpos = pcall(function() return ins.drop_position end)
                    if ok_d and dpos then
                        local targets = surface.find_entities_filtered{position=dpos, radius=0.5, limit=3}
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
            local function entity_key(e)
                local uid = e.unit_number
                if uid then return uid end
                return e.name..":"..string.format("%.1f",e.position.x)..":"..string.format("%.1f",e.position.y)
            end
            local function get_outputs(e)
                local out_ents = {}
                local out_kinds = {}
                -- Belt outputs via belt_neighbours
                if e.type == "transport-belt" or e.type == "fast-transport-belt" or
                   e.type == "express-transport-belt" or e.type == "turbo-transport-belt" or
                   e.type == "underground-belt" or e.type == "splitter" then
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
                local nearby = surface.find_entities_filtered{type="inserter", position=e.position, radius=3}
                for _, ins in pairs(nearby) do
                    if ins.valid then
                        local ok_p, src = pcall(function() return ins.pickup_target end)
                        if ok_p and src and src.valid and entity_key(src) == entity_key(e) then
                            local ok_d, dpos = pcall(function() return ins.drop_position end)
                            if ok_d and dpos then
                                local targets = surface.find_entities_filtered{position=dpos, radius=0.5, limit=3}
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
            -- BFS loop
            local start_key = entity_key(start_ent)
            visited[start_key] = true
            nodes[#nodes+1] = '{"name":"'..esc(start_ent.name)..'","type":"'..esc(start_ent.type)..'","x":'..string.format("%.1f",start_ent.position.x)..',"y":'..string.format("%.1f",start_ent.position.y)..',"depth":0}'
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
                        edges[#edges+1] = '{"from_name":"'..esc(cur_ent.name)..'","from_x":'..string.format("%.1f",cur_ent.position.x)..',"from_y":'..string.format("%.1f",cur_ent.position.y)..',"to_name":"'..esc(out_e.name)..'","to_x":'..string.format("%.1f",out_e.position.x)..',"to_y":'..string.format("%.1f",out_e.position.y)..',"kind":"'..kind..'"}'
                        if not visited[key] then
                            visited[key] = true
                            local next_depth = cur_depth + 1
                            nodes[#nodes+1] = '{"name":"'..esc(out_e.name)..'","type":"'..esc(out_e.type)..'","x":'..string.format("%.1f",out_e.position.x)..',"y":'..string.format("%.1f",out_e.position.y)..',"depth":'..next_depth..'}'
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
}
