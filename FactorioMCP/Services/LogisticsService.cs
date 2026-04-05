using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Service for querying the logistic robot network: network statistics, robot status,
/// and item inventory. Uses <c>LuaLogisticNetwork</c> accessed via <c>LuaForce.logistic_networks</c>
/// and <c>LuaSurface.find_logistic_network_by_position</c>.
/// </summary>
internal sealed class LogisticsService(RconClient rcon)
{
    /// <summary>
    /// Find the logistic network that covers the player's position and return
    /// high-level statistics: robot counts, roboport count, and totals for
    /// providers, requesters, and storages.
    /// </summary>
    public Task<string> GetLogisticNetworkAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            local net = surface.find_logistic_network_by_position(pos, player.force)
            if not net then
                rcon.print('{"status":"no_network","player_x":'..string.format("%.1f",pos.x)..',"player_y":'..string.format("%.1f",pos.y)..'}')
                return
            end
            local custom_name = net.custom_name or ""
            rcon.print('{"status":"ok"'..
                ',"network_id":'..(net.network_id or 0)..
                ',"name":"'..esc(custom_name)..'"'..
                ',"all_logistic_robots":'..net.all_logistic_robots..
                ',"available_logistic_robots":'..net.available_logistic_robots..
                ',"all_construction_robots":'..net.all_construction_robots..
                ',"available_construction_robots":'..net.available_construction_robots..
                ',"robot_limit":'..net.robot_limit..
                ',"provider_count":'..#net.providers..
                ',"requester_count":'..#net.requesters..
                ',"storage_count":'..#net.storages..
                ',"cell_count":'..#net.cells..
                '}'  )
            """, cancellationToken);
    }

    /// <summary>
    /// Get the full item inventory of the logistic network at the player's position.
    /// Returns a list of all items in provider and storage chests.
    /// Useful for knowing what resources are available for robot delivery.
    /// </summary>
    public Task<string> GetNetworkContentsAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            local net = surface.find_logistic_network_by_position(pos, player.force)
            if not net then
                rcon.print('{"status":"no_network","player_x":'..string.format("%.1f",pos.x)..',"player_y":'..string.format("%.1f",pos.y)..'}')
                return
            end
            local contents = net.get_contents()
            local parts = {}
            for _, item in pairs(contents) do
                parts[#parts+1] = '{"name":"'..esc(item.name)..'","count":'..item.count..'}'
            end
            rcon.print('{"status":"ok","network_id":'..(net.network_id or 0)..',"item_count":'..#parts..',"items":['..table.concat(parts, ",")..']}'  )
            """, cancellationToken);
    }

    /// <summary>
    /// Get a detailed breakdown of active logistic robots in the network at the player's
    /// position: how many are idle, how many are on delivery jobs, and the names of
    /// entities being serviced (up to 20). Useful for diagnosing delivery bottlenecks.
    /// </summary>
    public Task<string> GetRobotStatusAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            local net = surface.find_logistic_network_by_position(pos, player.force)
            if not net then
                rcon.print('{"status":"no_network","player_x":'..string.format("%.1f",pos.x)..',"player_y":'..string.format("%.1f",pos.y)..'}')
                return
            end
            local robots = net.logistic_robots
            local idle = 0
            local busy = 0
            local sample = {}
            for _, robot in pairs(robots) do
                if robot.valid then
                    -- LuaEntity for a logistic robot has 'speed' and 'energy'
                    if robot.speed and robot.speed > 0 then
                        busy = busy + 1
                        if #sample < 20 then
                            sample[#sample+1] = '{"name":"'..esc(robot.name)..'","x":'..string.format("%.1f",robot.position.x)..',"y":'..string.format("%.1f",robot.position.y)..'}'
                        end
                    else
                        idle = idle + 1
                    end
                end
            end
            local construction = net.construction_robots
            local c_idle = 0
            local c_busy = 0
            for _, robot in pairs(construction) do
                if robot.valid then
                    if robot.speed and robot.speed > 0 then c_busy = c_busy + 1
                    else c_idle = c_idle + 1 end
                end
            end
            rcon.print('{"status":"ok","network_id":'..(net.network_id or 0)..',"logistic_idle":'..idle..',"logistic_busy":'..busy..',"construction_idle":'..c_idle..',"construction_busy":'..c_busy..',"busy_robots_sample":['..table.concat(sample, ",")..']}'  )
            """, cancellationToken);
    }
}
