using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for reading and sending in-game chat messages.
/// Uses an on_console_chat event handler registered via RCON to capture messages
/// in storage.chat_log, and game.print() to send messages to all players.
/// </summary>
[McpServerToolType]
internal sealed class ChatTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Initialize the chat message listener. Registers an event handler that captures " +
        "in-game chat messages from players. This is called automatically on startup, but " +
        "can be called again to re-initialize if the listener was lost (e.g. after a game reload). " +
        "Safe to call multiple times.")]
    public Task<string> InitializeChatListener(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(InitializeChatListener), factorio.InitializeChatListenerAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Get chat messages from the in-game chat log. Returns messages sent by players " +
        "(not commands). Use the 'sinceTick' parameter to only get new messages since " +
        "the last poll — pass the 'latest_tick' value from the previous response. " +
        "The chat listener must be initialized first (done automatically on startup).")]
    public Task<string> GetChatMessages(
        [Description("Only return messages after this game tick. Pass 0 to get all messages, " +
            "or pass the 'latest_tick' from a previous GetChatMessages response to get only new ones.")]
        long sinceTick = 0,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetChatMessages), ct => factorio.GetChatMessagesAsync(sinceTick, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Send a chat message visible to all connected players. " +
        "The message is automatically tagged with '[AI]' to distinguish it from player messages.")]
    public Task<string> SendChatMessage(
        [Description("The message text to send to the in-game chat")]
        string message,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(SendChatMessage), ct => factorio.SendChatMessageAsync(message, ct), cancellationToken);
    }
}
