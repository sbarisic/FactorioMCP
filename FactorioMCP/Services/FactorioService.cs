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
    /// Lua helper function that escapes a string for safe embedding in JSON.
    /// Prepend this to any Lua script that concatenates entity names/types into JSON strings.
    /// Usage: <c>..esc(e.name)..</c> instead of <c>..e.name..</c>
    /// </summary>
    internal const string LuaJsonEscape = """local function esc(s) return s:gsub('\\', '\\\\'):gsub('"', '\\"') end""";

    /// <summary>
    /// Lua helper that sorts entity list so non-resource entities come first,
    /// nearest to the query position, and filters out the player character entity.
    /// Prevents accidentally selecting ore under a drill/furnace or selecting the
    /// player's own character.
    /// Usage: <c>sort_entities(entities, qx, qy)</c> — mutates in place. qx/qy optional.
    /// </summary>
    internal const string LuaEntitySort = """
        local function sort_entities(t, qx, qy)
            local pc = game.connected_players[1].character
            local j = 1
            for i = 1, #t do
                if t[i] ~= pc then t[j] = t[i] j = j + 1 end
            end
            for i = j, #t do t[i] = nil end
            table.sort(t, function(a, b)
                local a_res = a.type == "resource" and 1 or 0
                local b_res = b.type == "resource" and 1 or 0
                if a_res ~= b_res then return a_res < b_res end
                if qx and qy then
                    local da = (a.position.x - qx)*(a.position.x - qx) + (a.position.y - qy)*(a.position.y - qy)
                    local db = (b.position.x - qx)*(b.position.x - qx) + (b.position.y - qy)*(b.position.y - qy)
                    return da < db
                end
                return false
            end)
        end
        """;

    /// <summary>
    /// Lua on_tick handler that continuously re-applies walking_state and mining_state
    /// every tick. Required in Factorio 2 where both states reset after each tick.
    /// Reads from <c>storage.walk_dir</c> (set by PathfindingService) and
    /// <c>storage.mine_state</c> (set by MiningService).
    /// </summary>
    internal const string LuaOnTickHandler = """
        script.on_event(defines.events.on_tick, function()
            local p = game.connected_players[1]
            if not p then return end
            local d = storage.walk_dir
            if d ~= nil and p.character and p.character.valid then
                p.walking_state = {walking = true, direction = d}
            end
            if storage.mine_state then
                p.update_selected_entity(storage.mine_state.position)
                p.mining_state = {mining = true, position = storage.mine_state.position}
            end
        end)
        """;

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
