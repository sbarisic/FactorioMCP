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
    // ── Shared on_tick handler ──────────────────────────────────────
    // In Factorio 2, walking_state and mining_state do NOT persist across
    // ticks — they must be re-applied every tick. Both walking and mining
    // share a single on_tick handler that reads from storage.walk_state
    // and storage.mine_state to continuously apply the appropriate states.
    // The handler is installed when either system starts and removed only
    // when both systems are stopped.

    /// <summary>
    /// Lua snippet that installs (or reinstalls) the shared on_tick handler.
    /// The handler checks both <c>storage.walk_state</c> and <c>storage.mine_state</c>
    /// each tick, applying <c>walking_state</c> and <c>mining_state</c> respectively.
    /// For mining, <c>update_selected_entity</c> must be called every tick before setting
    /// <c>mining_state</c> — Factorio 2 requires the entity to be selected for mining to work.
    /// Safe to call multiple times — simply replaces the existing handler.
    /// </summary>
    internal const string InstallOnTickHandler = """
        script.on_event(defines.events.on_tick, function()
            local p = game.connected_players[1]
            if not p then return end
            if storage.walk_state then
                p.walking_state = {walking = true, direction = storage.walk_state.direction}
            end
            if storage.mine_state then
                p.update_selected_entity(storage.mine_state.position)
                p.mining_state = {mining = true, position = storage.mine_state.position}
            end
        end)
        """;

    /// <summary>
    /// Lua snippet that removes the on_tick handler only if neither walking
    /// nor mining is active. Call this after clearing a storage state.
    /// </summary>
    internal const string RemoveOnTickIfIdle = """
        if not storage.walk_state and not storage.mine_state then
            script.on_event(defines.events.on_tick, nil)
        end
        """;

    /// <summary>
    /// Start walking in a direction using a shared on_tick handler that continuously
    /// applies <c>walking_state</c> every tick. In Factorio 2, walking_state does NOT
    /// persist — it must be set every tick to keep the player moving. The direction is
    /// stored in <c>storage.walk_state</c> so the C# polling loop can change direction
    /// by calling WalkAsync again with a new direction (just updates storage, reinstalls
    /// the shared handler to pick up both walk and mine states).
    /// Valid directions: north, south, east, west, northeast, northwest, southeast, southwest.
    /// </summary>
    public Task<string> WalkAsync(string direction, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local dir = defines.direction.{{direction}}
            storage.walk_state = {direction = dir}
            {{InstallOnTickHandler}}
            player.walking_state = {walking = true, direction = dir}
            local p = player.position
            rcon.print('{"status":"walking","direction":"{{direction}}","x":'..p.x..',"y":'..p.y..'}')
            """);
        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Stop the player from walking. Clears walk state and removes the on_tick handler
    /// only if mining is also stopped.
    /// </summary>
    public Task<string> StopWalkingAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            local player = game.connected_players[1]
            storage.walk_state = nil
            player.walking_state = {walking = false, direction = defines.direction.north}
            {{RemoveOnTickIfIdle}}
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
