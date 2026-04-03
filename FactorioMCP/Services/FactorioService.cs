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
    /// Start walking in a direction using an on_tick handler that continuously applies
    /// <c>walking_state</c> every tick. In Factorio 2, walking_state does NOT persist —
    /// it must be set every tick to keep the player moving. The direction is stored in
    /// <c>storage.walk_state</c> so the C# polling loop can change direction by calling
    /// WalkAsync again with a new direction (which just updates storage, no handler reinstall).
    /// Valid directions: north, south, east, west, northeast, northwest, southeast, southwest.
    /// </summary>
    public Task<string> WalkAsync(string direction, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local dir = defines.direction.{{direction}}
            if storage.walk_state then
                storage.walk_state.direction = dir
            else
                storage.walk_state = {direction = dir}
                script.on_event(defines.events.on_tick, function()
                    if storage.walk_state then
                        local p = game.connected_players[1]
                        if p then p.walking_state = {walking = true, direction = storage.walk_state.direction} end
                    end
                end)
            end
            player.walking_state = {walking = true, direction = dir}
            local p = player.position
            rcon.print('{"status":"walking","direction":"{{direction}}","x":'..p.x..',"y":'..p.y..'}')
            """);
        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Stop the player from walking and clean up walk state and on_tick handler.
    /// </summary>
    public Task<string> StopWalkingAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local player = game.connected_players[1]
            storage.walk_state = nil
            script.on_event(defines.events.on_tick, nil)
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
