using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Service for inspecting electric networks and entity power status via RCON Lua commands.
/// Uses electric pole network statistics (LuaFlowStatistics) for network-wide production,
/// consumption, and satisfaction data, and entity properties for per-entity power details.
/// </summary>
internal sealed class EnergyService(RconClient rcon)
{
    /// <summary>
    /// Find the nearest electric pole to the player within a radius and report its network
    /// statistics: production and consumption rates by entity type, satisfaction percentage,
    /// and accumulator charge levels. Uses <c>electric_network_statistics.get_flow_count</c>
    /// with 5-second precision for current power rates (watts).
    /// </summary>
    public Task<string> GetElectricNetworkAsync(double radius = 50, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            local poles = surface.find_entities_filtered{type="electric-pole", position=pos, radius={{radius}}}
            if #poles == 0 then
                rcon.print('{"status":"no_poles_found","player_x":'..string.format("%.1f", pos.x)..',"player_y":'..string.format("%.1f", pos.y)..',"radius":'..string.format("%.1f", {{radius}})..'}')
                return
            end
            table.sort(poles, function(a, b)
                local da = (a.position.x - pos.x)^2 + (a.position.y - pos.y)^2
                local db = (b.position.x - pos.x)^2 + (b.position.y - pos.y)^2
                return da < db
            end)
            local pole = poles[1]
            local stats = pole.electric_network_statistics
            local net_id = pole.electric_network_id
            local prec = defines.flow_precision_index.five_seconds
            -- Production (output in flow stats = production for electric networks)
            local prod_parts = {}
            local total_prod = 0
            for name, _ in pairs(stats.output_counts) do
                local rate = stats.get_flow_count{name=name, category="output", precision_index=prec}
                if rate > 0 then
                    prod_parts[#prod_parts+1] = '{"name":"'..name..'","watts":'..string.format("%.1f", rate * 60)..'}'
                    total_prod = total_prod + rate
                end
            end
            -- Consumption (input in flow stats = consumption for electric networks)
            local cons_parts = {}
            local total_cons = 0
            for name, _ in pairs(stats.input_counts) do
                local rate = stats.get_flow_count{name=name, category="input", precision_index=prec}
                if rate > 0 then
                    cons_parts[#cons_parts+1] = '{"name":"'..name..'","watts":'..string.format("%.1f", rate * 60)..'}'
                    total_cons = total_cons + rate
                end
            end
            -- Accumulators on this network
            local total_charge = 0
            local total_capacity = 0
            local accum_count = 0
            local accums = surface.find_entities_filtered{type="accumulator", position=pos, radius={{radius}}}
            for _, acc in pairs(accums) do
                if acc.electric_network_id == net_id then
                    total_charge = total_charge + acc.energy
                    total_capacity = total_capacity + (acc.electric_buffer_size or 0)
                    accum_count = accum_count + 1
                end
            end
            local accum_pct = 0
            if total_capacity > 0 then
                accum_pct = (total_charge / total_capacity) * 100
            end
            -- Satisfaction
            local satisfaction = 100.0
            if total_cons > 0 then
                satisfaction = math.min(100, (total_prod / total_cons) * 100)
            end
            rcon.print('{"status":"ok"'..
                ',"network_id":'..(net_id or 0)..
                ',"pole":"'..pole.name..'"'..
                ',"pole_x":'..string.format("%.1f", pole.position.x)..
                ',"pole_y":'..string.format("%.1f", pole.position.y)..
                ',"total_production_watts":'..string.format("%.1f", total_prod * 60)..
                ',"total_consumption_watts":'..string.format("%.1f", total_cons * 60)..
                ',"satisfaction_percent":'..string.format("%.1f", satisfaction)..
                ',"accumulator_count":'..accum_count..
                ',"accumulator_charge_joules":'..string.format("%.1f", total_charge)..
                ',"accumulator_capacity_joules":'..string.format("%.1f", total_capacity)..
                ',"accumulator_charge_percent":'..string.format("%.1f", accum_pct)..
                ',"producers":['..table.concat(prod_parts, ",")..']'..
                ',"consumers":['..table.concat(cons_parts, ",")..']'..
                '}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Inspect the power status of an entity at the given coordinates. Reports electric
    /// network connection, energy stored, buffer size, drain, and generation rate.
    /// Useful for diagnosing power issues at the entity level.
    /// </summary>
    public Task<string> InspectEntityPowerAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local surface = game.connected_players[1].surface
            local entities = surface.find_entities_filtered{position={{{x}}, {{y}}}, radius=0.5, limit=5}
            table.sort(entities, function(a, b)
                if a.type == "resource" and b.type ~= "resource" then return false end
                if a.type ~= "resource" and b.type == "resource" then return true end
                return false
            end)
            local e = entities[1]
            if not e then
                rcon.print('{"status":"error","error":"no_entity","x":'..string.format("%.1f", {{x}})..',"y":'..string.format("%.1f", {{y}})..'}')
                return
            end
            local result = '{"status":"ok"'..
                ',"name":"'..e.name..'"'..
                ',"type":"'..e.type..'"'..
                ',"x":'..string.format("%.1f", e.position.x)..
                ',"y":'..string.format("%.1f", e.position.y)
            local connected = e.is_connected_to_electric_network()
            result = result..',"connected_to_network":'..tostring(connected)
            local net_id = e.electric_network_id
            if net_id then
                result = result..',"network_id":'..net_id
            end
            result = result..',"energy_joules":'..string.format("%.1f", e.energy)
            local buf = e.electric_buffer_size
            if buf then
                result = result..',"buffer_size_joules":'..string.format("%.1f", buf)
                if buf > 0 then
                    result = result..',"charge_percent":'..string.format("%.1f", (e.energy / buf) * 100)
                end
            end
            local drain = e.electric_drain
            if drain then
                result = result..',"drain_watts":'..string.format("%.1f", drain * 60)
            end
            local ok, gen = pcall(function() return e.energy_generated_last_tick end)
            if ok and gen then
                result = result..',"generation_watts":'..string.format("%.1f", gen * 60)
            end
            result = result..'}'
            rcon.print(result)
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
