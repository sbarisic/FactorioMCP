using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// High-level service for controlling a Factorio game instance via RCON Lua commands.
/// All operations execute Lua scripts through the /c console command and return
/// JSON-formatted output from rcon.print() for reliable AI parsing.
/// Split into partial class files by domain — see FactorioService.*.cs.
/// </summary>
internal sealed partial class FactorioService(RconClient rcon)
{
    /// <summary>
    /// Start walking in a direction. Registers an on_tick handler with stuck detection
    /// so the player keeps walking every game tick until <see cref="StopWalkingAsync"/> is called.
    /// When the player is blocked by an entity, the handler automatically tries perpendicular
    /// directions to navigate around the obstacle, then resumes the original direction.
    /// Valid directions: north, south, east, west, northeast, northwest, southeast, southwest.
    /// </summary>
    public Task<string> WalkAsync(string direction, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local target_dir = defines.direction.{{direction}}
            player.walking_state = {walking = true, direction = target_dir}

            -- Perpendicular directions for obstacle avoidance
            local perp = {
                [defines.direction.north]     = {defines.direction.east,      defines.direction.west},
                [defines.direction.south]     = {defines.direction.west,      defines.direction.east},
                [defines.direction.east]      = {defines.direction.south,     defines.direction.north},
                [defines.direction.west]      = {defines.direction.north,     defines.direction.south},
                [defines.direction.northeast] = {defines.direction.southeast, defines.direction.northwest},
                [defines.direction.northwest] = {defines.direction.northeast, defines.direction.southwest},
                [defines.direction.southeast] = {defines.direction.northeast, defines.direction.southwest},
                [defines.direction.southwest] = {defines.direction.northwest, defines.direction.southeast}
            }

            -- State for stuck detection
            storage.walk_state = {
                target_dir = target_dir,
                prev_x = player.position.x,
                prev_y = player.position.y,
                stuck_ticks = 0,
                detour_dir = nil,
                detour_ticks = 0,
                detour_side = 1
            }

            script.on_event(defines.events.on_tick, function()
                local p = game.connected_players[1]
                if not (p and p.valid) then return end
                local ws = storage.walk_state
                if not ws then return end

                local pos = p.position
                local dx = pos.x - ws.prev_x
                local dy = pos.y - ws.prev_y
                local moved = (dx*dx + dy*dy) > 0.0001

                if ws.detour_dir then
                    -- Currently detouring around an obstacle
                    if moved then
                        ws.detour_ticks = ws.detour_ticks + 1
                    end
                    if ws.detour_ticks >= 15 then
                        -- Detoured enough, try original direction again
                        ws.detour_dir = nil
                        ws.detour_ticks = 0
                        ws.stuck_ticks = 0
                    end
                    p.walking_state = {walking = true, direction = ws.detour_dir or ws.target_dir}
                else
                    -- Walking in target direction
                    if moved then
                        ws.stuck_ticks = 0
                    else
                        ws.stuck_ticks = ws.stuck_ticks + 1
                    end

                    if ws.stuck_ticks >= 10 then
                        -- Stuck! Pick a perpendicular direction to detour
                        local sides = perp[ws.target_dir]
                        if sides then
                            ws.detour_dir = sides[ws.detour_side]
                            ws.detour_ticks = 0
                            ws.detour_side = (ws.detour_side % 2) + 1
                        end
                        ws.stuck_ticks = 0
                        p.walking_state = {walking = true, direction = ws.detour_dir or ws.target_dir}
                    else
                        p.walking_state = {walking = true, direction = ws.target_dir}
                    end
                end

                ws.prev_x = pos.x
                ws.prev_y = pos.y
            end)
            local p = player.position
            rcon.print('{"status":"walking","direction":"{{direction}}","x":'..p.x..',"y":'..p.y..'}')
            """);
        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Stop the player from walking, de-register the on_tick walking handler,
    /// and clean up the walk state used for stuck detection.
    /// </summary>
    public Task<string> StopWalkingAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local player = game.connected_players[1]
            script.on_event(defines.events.on_tick, nil)
            storage.walk_state = nil
            player.walking_state = {walking = false, direction = defines.direction.north}
            local p = player.position
            rcon.print('{"status":"stopped","x":'..p.x..',"y":'..p.y..'}')
            """,
            cancellationToken);
    }

    /// <summary>
    /// Get the player's current map position.
    /// </summary>
    public Task<string> GetPlayerPositionAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local p = game.connected_players[1].position
            rcon.print('{"x":'..p.x..',"y":'..p.y..'}')
            """,
            cancellationToken);
    }

    /// <summary>
    /// Execute arbitrary Lua code on the Factorio instance.
    /// </summary>
    public Task<string> ExecuteRawLuaAsync(string luaCode, CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync(luaCode, cancellationToken);
    }
}
