using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tool for executing arbitrary Lua code on the Factorio instance via RCON.
/// Intended for advanced operations not covered by specific tools.
/// </summary>
[McpServerToolType]
internal sealed class LuaTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Execute arbitrary Lua code on the Factorio game instance via RCON. " +
        "Use this for advanced operations not covered by other tools. " +
        "The code runs as /silent-command and has full access to the Factorio Lua API. " +
        "Use rcon.print() to return data. The player is accessed via game.connected_players[1]. " +
        "WARNING: This executes code directly on the game server with no sandboxing. " +
        "Incorrect Lua code can corrupt game state, crash the server, or cause data loss. " +
        "Prefer specific tools (PlaceEntity, Craft, etc.) when available.")]
    public Task<string> ExecuteLua(
        [Description("Lua code to execute. Use rcon.print() to return results as JSON. " +
            "Access the player with game.connected_players[1].")]
        string luaCode,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ExecuteLua), ct => factorio.ExecuteRawLuaAsync(luaCode, ct), cancellationToken);
    }
}
