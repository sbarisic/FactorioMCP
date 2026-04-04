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
    /// Lua helper that sorts entity list so non-resource entities come first.
    /// Prevents accidentally selecting ore under a drill/furnace.
    /// Usage: <c>sort_entities(entities)</c> — mutates in place.
    /// </summary>
    internal const string LuaEntitySort = """
        local function sort_entities(t)
            table.sort(t, function(a, b)
                local a_res = a.type == "resource" and 1 or 0
                local b_res = b.type == "resource" and 1 or 0
                return a_res < b_res
            end)
        end
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
