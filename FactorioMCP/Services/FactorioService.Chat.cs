using System.Globalization;
using System.Text;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
    // ── Chat ─────────────────────────────────────────────────────────

    /// <summary>
    /// Register an <c>on_console_chat</c> event handler that stores incoming chat messages
    /// in <c>storage.chat_log</c>. Idempotent — safe to call multiple times; the handler
    /// is replaced and existing messages are preserved.
    /// </summary>
    public Task<string> InitializeChatListenerAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            storage.chat_log = storage.chat_log or {}
            script.on_event(defines.events.on_console_chat, function(e)
                local player_name = "server"
                if e.player_index then
                    local p = game.get_player(e.player_index)
                    if p then player_name = p.name end
                end
                table.insert(storage.chat_log, {
                    tick = e.tick,
                    player_name = player_name,
                    message = e.message
                })
            end)
            rcon.print('{"status":"initialized","existing_messages":'..#storage.chat_log..'}')
            """, cancellationToken);
    }

    /// <summary>
    /// Read chat messages from the stored log. Optionally filters to messages
    /// after a given game tick so the caller can poll for new messages only.
    /// </summary>
    public Task<string> GetChatMessagesAsync(long sinceTick = 0, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sinceTick);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local log = storage.chat_log or {}
            local since_tick = {{sinceTick}}
            local json_escape = function(s)
                return s:gsub('\\', '\\\\'):gsub('"', '\\"'):gsub('\n', '\\n'):gsub('\r', '\\r')
            end
            local parts = {}
            local latest_tick = since_tick
            for _, msg in pairs(log) do
                if msg.tick > since_tick then
                    parts[#parts+1] = '{"tick":'..msg.tick..',"player":"'..json_escape(msg.player_name)..'","message":"'..json_escape(msg.message)..'"}'
                    if msg.tick > latest_tick then latest_tick = msg.tick end
                end
            end
            rcon.print('{"messages":['..table.concat(parts, ",")..'],"count":'..#parts..',"latest_tick":'..latest_tick..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Send a chat message visible to all connected players via <c>game.print()</c>.
    /// The message is tagged with "[AI]" to distinguish it from player messages.
    /// </summary>
    public Task<string> SendChatMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var escaped = EscapeLuaString(message);
        var lua = $$"""
            game.print("[AI] {{escaped}}")
            rcon.print('{"status":"sent"}')
            """;

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Escape a string for safe embedding in a Lua double-quoted string literal.
    /// </summary>
    private static string EscapeLuaString(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\0': break; // strip null bytes
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
