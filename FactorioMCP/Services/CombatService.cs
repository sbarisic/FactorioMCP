using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Service for inspecting combat-related state: nearby enemies, spawners, and player defences
/// (turrets). All queries run on the player's current surface via RCON Lua commands.
/// </summary>
internal sealed class CombatService(RconClient rcon)
{
    /// <summary>
    /// Scan for enemy units and spawners within a radius of the player.
    /// Returns unit counts by type, individual unit positions, and spawner positions.
    /// Useful for situational awareness and deciding whether to expand or defend.
    /// </summary>
    public Task<string> ScanEnemiesAsync(double radius = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            -- Enemy units (biters/spitters)
            local units = surface.find_enemy_units(pos, {{radius}}, player.force)
            local unit_parts = {}
            for _, u in pairs(units) do
                if u.valid then
                    unit_parts[#unit_parts+1] = '{"name":"'..esc(u.name)..'","x":'..string.format("%.1f",u.position.x)..',"y":'..string.format("%.1f",u.position.y)..'}'
                end
            end
            -- Enemy spawners and worms
            local spawners = surface.find_entities_filtered{force="enemy", type="unit-spawner", position=pos, radius={{radius}}}
            local spawner_parts = {}
            for _, s in pairs(spawners) do
                if s.valid then
                    spawner_parts[#spawner_parts+1] = '{"name":"'..esc(s.name)..'","x":'..string.format("%.1f",s.position.x)..',"y":'..string.format("%.1f",s.position.y)..'}'
                end
            end
            local worms = surface.find_entities_filtered{force="enemy", type="turret", position=pos, radius={{radius}}}
            local worm_parts = {}
            for _, w in pairs(worms) do
                if w.valid then
                    worm_parts[#worm_parts+1] = '{"name":"'..esc(w.name)..'","x":'..string.format("%.1f",w.position.x)..',"y":'..string.format("%.1f",w.position.y)..'}'
                end
            end
            -- Nearest military enemy
            local nearest = surface.find_nearest_enemy{position=pos, max_distance={{radius}}, force=player.force}
            local nearest_json = "null"
            if nearest and nearest.valid then
                nearest_json = '{"name":"'..esc(nearest.name)..'","x":'..string.format("%.1f",nearest.position.x)..',"y":'..string.format("%.1f",nearest.position.y)..'}'
            end
            rcon.print('{"status":"ok","radius":'..string.format("%.1f", {{radius}})..',"unit_count":'..#unit_parts..',"spawner_count":'..#spawner_parts..',"worm_count":'..#worm_parts..',"units":['..table.concat(unit_parts, ",")..'],"spawners":['..table.concat(spawner_parts, ",")..'],"worms":['..table.concat(worm_parts, ",")..'],"nearest_enemy":'..nearest_json..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Find all player-owned turrets within a radius and report their status:
    /// name, position, ammo count, kill count, and current shooting target.
    /// Useful for auditing defence coverage and restocking ammo.
    /// </summary>
    public Task<string> GetDefensesAsync(double radius = 80, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = player.position
            local turrets = surface.find_entities_filtered{type="ammo-turret", position=pos, radius={{radius}}, force=player.force}
            local fluid_turrets = surface.find_entities_filtered{type="fluid-turret", position=pos, radius={{radius}}, force=player.force}
            local electric_turrets = surface.find_entities_filtered{type="electric-turret", position=pos, radius={{radius}}, force=player.force}
            local parts = {}
            local all = {}
            for _, t in pairs(turrets) do all[#all+1] = t end
            for _, t in pairs(fluid_turrets) do all[#all+1] = t end
            for _, t in pairs(electric_turrets) do all[#all+1] = t end
            for _, t in pairs(all) do
                if t.valid then
                    local ammo = t.get_inventory(defines.inventory.turret_ammo)
                    local ammo_count = 0
                    if ammo and ammo.valid then
                        for i = 1, #ammo do
                            local stack = ammo[i]
                            if stack.valid_for_read then
                                ammo_count = ammo_count + stack.count
                            end
                        end
                    end
                    local kills = 0
                    local ok_k, k = pcall(function() return t.kills end)
                    if ok_k and k then kills = k end
                    local target_json = "null"
                    local ok_t, tgt = pcall(function() return t.shooting_target end)
                    if ok_t and tgt and tgt.valid then
                        target_json = '{"name":"'..esc(tgt.name)..'","x":'..string.format("%.1f",tgt.position.x)..',"y":'..string.format("%.1f",tgt.position.y)..'}'
                    end
                    parts[#parts+1] = '{"name":"'..esc(t.name)..'","type":"'..esc(t.type)..'","x":'..string.format("%.1f",t.position.x)..',"y":'..string.format("%.1f",t.position.y)..',"ammo_count":'..ammo_count..',"kills":'..kills..',"shooting_target":'..target_json..'}'
                end
            end
            rcon.print('{"status":"ok","radius":'..string.format("%.1f", {{radius}})..',"turret_count":'..#parts..',"turrets":['..table.concat(parts, ",")..']}'  )
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
