using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Service for querying and controlling trains and train stations via RCON Lua commands.
/// Uses <c>game.train_manager</c> for network-wide queries and individual LuaTrain
/// properties for per-train details.
/// </summary>
internal sealed class TrainService(RconClient rcon)
{
    /// <summary>
    /// List all trains on the player's current surface with their state, position,
    /// locomotive types, and cargo wagon count. Useful for getting an overview of
    /// the rail network.
    /// </summary>
    public Task<string> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local all_trains = game.train_manager.get_trains{surface=surface}
            local parts = {}
            for _, train in pairs(all_trains) do
                if train.valid then
                    -- Position from front stock
                    local px, py = 0, 0
                    local fs = train.front_stock
                    if fs and fs.valid then
                        px = fs.position.x
                        py = fs.position.y
                    end
                    -- State name
                    local state_name = "unknown"
                    for name, val in pairs(defines.train_state) do
                        if val == train.state then state_name = name break end
                    end
                    -- Station name if stopped
                    local station_name = "null"
                    local st = train.station
                    if st and st.valid then
                        station_name = '"'..esc(st.backer_name or st.name)..'"'
                    end
                    -- Locomotive count (front + back)
                    local loco_count = 0
                    local ok_l, locos = pcall(function() return train.locomotives end)
                    if ok_l and locos then
                        loco_count = (#(locos.front_movers or {})) + (#(locos.back_movers or {}))
                    end
                    parts[#parts+1] = '{"id":'..train.id..',"state":"'..esc(state_name)..'","manual_mode":'..tostring(train.manual_mode)..',"x":'..string.format("%.1f",px)..',"y":'..string.format("%.1f",py)..',"has_path":'..tostring(train.has_path)..',"speed":'..string.format("%.2f",train.speed)..',"locomotive_count":'..loco_count..',"cargo_wagon_count":'..#train.cargo_wagons..',"station":'..station_name..'}'
                end
            end
            rcon.print('{"status":"ok","train_count":'..#parts..',"trains":['..table.concat(parts, ",")..']}'  )
            """, cancellationToken);
    }

    /// <summary>
    /// List all train stops on the player's current surface with their name, position,
    /// and whether a train is currently stopped there.
    /// </summary>
    public Task<string> GetTrainStopsAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local stops = game.train_manager.get_train_stops{surface=surface}
            local parts = {}
            for _, stop in pairs(stops) do
                if stop.valid then
                    local stopped_train_id = "null"
                    local ok_t, st = pcall(function() return stop.get_stopped_train() end)
                    if ok_t and st and st.valid then
                        stopped_train_id = tostring(st.id)
                    end
                    local name = stop.backer_name or stop.name
                    parts[#parts+1] = '{"name":"'..esc(name)..'","x":'..string.format("%.1f",stop.position.x)..',"y":'..string.format("%.1f",stop.position.y)..',"stopped_train_id":'..stopped_train_id..'}'
                end
            end
            rcon.print('{"status":"ok","stop_count":'..#parts..',"stops":['..table.concat(parts, ",")..']}'  )
            """, cancellationToken);
    }

    /// <summary>
    /// Inspect a single train by its numeric ID. Returns full details including
    /// schedule station list, current cargo contents, and speed.
    /// </summary>
    public Task<string> InspectTrainAsync(uint trainId, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local train = game.train_manager.get_train_by_id({{trainId}})
            if not train or not train.valid then
                rcon.print('{"status":"error","error":"train_not_found","id":{{trainId}}}')
                return
            end
            -- Schedule records
            local sched_parts = {}
            local ok_s, sched = pcall(function() return train.get_schedule() end)
            if ok_s and sched and sched.valid then
                local rec_count = sched.get_record_count() or 0
                for i = 1, rec_count do
                    local ok_r, rec = pcall(function() return sched.get_record({index=i}) end)
                    if ok_r and rec then
                        local rname = rec.station or ""
                        sched_parts[#sched_parts+1] = '{"index":'..i..',"station":"'..esc(rname)..'"}'
                    end
                end
            end
            -- Cargo
            local cargo_parts = {}
            local ok_c, contents = pcall(function() return train.get_contents() end)
            if ok_c and contents then
                for _, item in pairs(contents) do
                    cargo_parts[#cargo_parts+1] = '{"name":"'..esc(item.name)..'","count":'..item.count..'}'
                end
            end
            -- State name
            local state_name = "unknown"
            for name, val in pairs(defines.train_state) do
                if val == train.state then state_name = name break end
            end
            rcon.print('{"status":"ok","id":'..train.id..',"state":"'..esc(state_name)..'","manual_mode":'..tostring(train.manual_mode)..',"has_path":'..tostring(train.has_path)..',"speed":'..string.format("%.2f",train.speed)..',"schedule":['..table.concat(sched_parts, ",")..'],"cargo":['..table.concat(cargo_parts, ",")..']}'  )
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Switch a train between manual mode (player/script controlled) and
    /// automatic mode (schedule-driven). Returns the train's new mode.
    /// </summary>
    public Task<string> SetTrainModeAsync(uint trainId, bool manual, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local train = game.train_manager.get_train_by_id({{trainId}})
            if not train or not train.valid then
                rcon.print('{"status":"error","error":"train_not_found","id":{{trainId}}}')
                return
            end
            train.manual_mode = {{(manual ? "true" : "false")}}
            rcon.print('{"status":"ok","id":'..train.id..',"manual_mode":'..tostring(train.manual_mode)..'}'  )
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
