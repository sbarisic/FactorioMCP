using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── ScanResources ────────────────────────────────────────────────

    [Fact]
    public async Task ScanResourcesAsync_SendsCorrectLuaCommand()
    {
        await _service.ScanResourcesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("find_entities_filtered", _rcon.LastCommand);
        Assert.Contains("type=\"resource\"", _rcon.LastCommand);
    }

    [Fact]
    public async Task ScanResourcesAsync_UsesDefaultRadius()
    {
        await _service.ScanResourcesAsync();

        Assert.Contains("radius=50", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_UsesCustomRadius()
    {
        await _service.ScanResourcesAsync(100);

        Assert.Contains("radius=100", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_OutputsJsonResourcesSummary()
    {
        await _service.ScanResourcesAsync();

        Assert.Contains("\"scan_radius\":", _rcon.LastCommand!);
        Assert.Contains("\"resources\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_IncludesResourceDetails()
    {
        await _service.ScanResourcesAsync();

        Assert.Contains("\"name\":\"", _rcon.LastCommand!);
        Assert.Contains("\"patches\":", _rcon.LastCommand!);
        Assert.Contains("\"total_amount\":", _rcon.LastCommand!);
        Assert.Contains("\"center_x\":", _rcon.LastCommand!);
        Assert.Contains("\"center_y\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanResourcesAsync(0));
    }

    [Fact]
    public async Task ScanResourcesAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanResourcesAsync(-5));
    }

    // ── ScanTiles ────────────────────────────────────────────────────

    [Fact]
    public async Task ScanTilesAsync_SendsCorrectLuaCommand()
    {
        await _service.ScanTilesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("find_tiles_filtered", _rcon.LastCommand);
    }

    [Fact]
    public async Task ScanTilesAsync_UsesDefaultRadius()
    {
        await _service.ScanTilesAsync();

        Assert.Contains("\"scan_radius\":16", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_UsesCustomRadius()
    {
        await _service.ScanTilesAsync(32);

        Assert.Contains("\"scan_radius\":32", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_OutputsJsonTilesSummary()
    {
        await _service.ScanTilesAsync();

        Assert.Contains("\"tiles\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_ThrowsOnZeroRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanTilesAsync(0));
    }

    [Fact]
    public async Task ScanTilesAsync_ThrowsOnNegativeRadius()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.ScanTilesAsync(-5));
    }

    // ── Remote Area Scanning ─────────────────────────────────────────

    [Fact]
    public async Task GetNearbyEntitiesAsync_UsesPlayerPositionByDefault()
    {
        await _service.GetNearbyEntitiesAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetNearbyEntitiesAsync_UsesCustomCenterWhenProvided()
    {
        await _service.GetNearbyEntitiesAsync(20, centerX: 100.5, centerY: -50.0);

        Assert.Contains("100.5", _rcon.LastCommand!);
        Assert.Contains("-50", _rcon.LastCommand!);
        Assert.DoesNotContain("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_UsesPlayerPositionByDefault()
    {
        await _service.ScanResourcesAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanResourcesAsync_UsesCustomCenterWhenProvided()
    {
        await _service.ScanResourcesAsync(100, centerX: 200.0, centerY: 300.0);

        Assert.Contains("200", _rcon.LastCommand!);
        Assert.Contains("300", _rcon.LastCommand!);
        Assert.DoesNotContain("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_UsesPlayerPositionByDefault()
    {
        await _service.ScanTilesAsync();

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task ScanTilesAsync_UsesCustomCenterWhenProvided()
    {
        await _service.ScanTilesAsync(32, centerX: -10.5, centerY: 42.0);

        Assert.Contains("-10.5", _rcon.LastCommand!);
        Assert.Contains("42", _rcon.LastCommand!);
        Assert.DoesNotContain("player.position", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetNearbyEntitiesAsync_IgnoresPartialCenter()
    {
        await _service.GetNearbyEntitiesAsync(10, centerX: 50.0);

        Assert.Contains("player.position", _rcon.LastCommand!);
    }

    // ── InsertItems ──────────────────────────────────────────────────

    [Fact]
    public async Task InsertItemsAsync_SendsCorrectItemAndCount()
    {
        await _service.InsertItemsAsync(5.0, 3.0, "coal", 10, "fuel");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("coal", _rcon.LastCommand);
        Assert.Contains("10", _rcon.LastCommand);
    }

    [Fact]
    public async Task InsertItemsAsync_ChecksProximity()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 1);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_PrioritizesNonResourceEntities()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 1);

        Assert.Contains("table.sort", _rcon.LastCommand!);
        Assert.Contains("\"resource\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_MapsInventoryTypes()
    {
        await _service.InsertItemsAsync(0, 0, "iron-ore", 5, "furnace_source");

        Assert.Contains("defines.inventory.fuel", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.furnace_source", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.furnace_result", _rcon.LastCommand!);
        Assert.Contains("defines.inventory.chest", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_ChecksPlayerInventory()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 1);

        Assert.Contains("get_item_count", _rcon.LastCommand!);
        Assert.Contains("no_items", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_InsertsIntoEntityInventory()
    {
        await _service.InsertItemsAsync(0, 0, "coal", 5);

        Assert.Contains("inv.insert", _rcon.LastCommand!);
        Assert.Contains("remove_item", _rcon.LastCommand!);
        Assert.Contains("\"inserted\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InsertItemsAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.InsertItemsAsync(0, 0, null!, 1));
    }

    [Fact]
    public async Task InsertItemsAsync_ThrowsOnZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.InsertItemsAsync(0, 0, "coal", 0));
    }

    // ── RemoveItems ──────────────────────────────────────────────────

    [Fact]
    public async Task RemoveItemsAsync_SendsCorrectItemAndCount()
    {
        await _service.RemoveItemsAsync(5.0, 3.0, "iron-plate", 20, "furnace_result");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("iron-plate", _rcon.LastCommand);
        Assert.Contains("20", _rcon.LastCommand);
    }

    [Fact]
    public async Task RemoveItemsAsync_ChecksProximity()
    {
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 1);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RemoveItemsAsync_ChecksEntityInventoryContents()
    {
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 1);

        Assert.Contains("get_item_count", _rcon.LastCommand!);
        Assert.Contains("no_items", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RemoveItemsAsync_RemovesFromEntityAndInsertsToPlayer()
    {
        await _service.RemoveItemsAsync(0, 0, "iron-plate", 5);

        Assert.Contains("inv.remove", _rcon.LastCommand!);
        Assert.Contains("player.insert", _rcon.LastCommand!);
        Assert.Contains("\"removed\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task RemoveItemsAsync_ThrowsOnNullItemName()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.RemoveItemsAsync(0, 0, null!, 1));
    }

    [Fact]
    public async Task RemoveItemsAsync_ThrowsOnZeroCount()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.RemoveItemsAsync(0, 0, "iron-plate", 0));
    }

    // ── InspectEntity ────────────────────────────────────────────────

    [Fact]
    public async Task InspectEntityAsync_SendsCorrectPosition()
    {
        await _service.InspectEntityAsync(7.5, -3.0);

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("7.5", _rcon.LastCommand);
        Assert.Contains("-3", _rcon.LastCommand);
    }

    [Fact]
    public async Task InspectEntityAsync_ChecksProximity()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("reach_distance", _rcon.LastCommand!);
        Assert.Contains("out_of_range", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_PrioritizesNonResourceEntities()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("table.sort", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsEntityStatus()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("e.status", _rcon.LastCommand!);
        Assert.Contains("defines.entity_status", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsInventoryContents()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("get_inventory", _rcon.LastCommand!);
        Assert.Contains("get_contents", _rcon.LastCommand!);
        Assert.Contains("\"inventories\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsBurnerInfo()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("e.burner", _rcon.LastCommand!);
        Assert.Contains("remaining_burning_fuel", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsRecipe()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("get_recipe", _rcon.LastCommand!);
        Assert.Contains("\"recipe\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsHealth()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("e.health", _rcon.LastCommand!);
        Assert.Contains("\"health\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InspectEntityAsync_ReadsMiningTarget()
    {
        await _service.InspectEntityAsync(0, 0);

        Assert.Contains("mining_target", _rcon.LastCommand!);
    }

    // ── InitializeChatListener ────────────────────────────────────────

    [Fact]
    public async Task InitializeChatListenerAsync_RegistersEventHandler()
    {
        await _service.InitializeChatListenerAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("on_console_chat", _rcon.LastCommand);
        Assert.Contains("storage.chat_log", _rcon.LastCommand);
    }

    [Fact]
    public async Task InitializeChatListenerAsync_PreservesExistingMessages()
    {
        await _service.InitializeChatListenerAsync();

        Assert.Contains("storage.chat_log = storage.chat_log or {}", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InitializeChatListenerAsync_CapturesPlayerName()
    {
        await _service.InitializeChatListenerAsync();

        Assert.Contains("game.get_player", _rcon.LastCommand!);
        Assert.Contains("player_name", _rcon.LastCommand!);
    }

    [Fact]
    public async Task InitializeChatListenerAsync_OutputsJsonStatus()
    {
        await _service.InitializeChatListenerAsync();

        Assert.Contains("\"status\":\"initialized\"", _rcon.LastCommand!);
        Assert.Contains("\"existing_messages\":", _rcon.LastCommand!);
    }

    // ── GetChatMessages ───────────────────────────────────────────────

    [Fact]
    public async Task GetChatMessagesAsync_QueriesChatLog()
    {
        await _service.GetChatMessagesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("storage.chat_log", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetChatMessagesAsync_DefaultSinceTickIsZero()
    {
        await _service.GetChatMessagesAsync();

        Assert.Contains("since_tick = 0", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_FiltersBySinceTick()
    {
        await _service.GetChatMessagesAsync(12345);

        Assert.Contains("since_tick = 12345", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_OutputsJsonWithMessages()
    {
        await _service.GetChatMessagesAsync();

        Assert.Contains("\"messages\":[", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
        Assert.Contains("\"latest_tick\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_IncludesJsonEscaping()
    {
        await _service.GetChatMessagesAsync();

        Assert.Contains("json_escape", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetChatMessagesAsync_ThrowsOnNegativeSinceTick()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetChatMessagesAsync(-1));
    }

    // ── SendChatMessage ───────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessageAsync_SendsMessageViaGamePrint()
    {
        await _service.SendChatMessageAsync("Hello world");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("game.print", _rcon.LastCommand);
        Assert.Contains("Hello world", _rcon.LastCommand);
    }

    [Fact]
    public async Task SendChatMessageAsync_TagsWithAiPrefix()
    {
        await _service.SendChatMessageAsync("test message");

        Assert.Contains("[AI]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_EscapesQuotes()
    {
        await _service.SendChatMessageAsync("He said \"hello\"");

        Assert.Contains("\\\"hello\\\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_EscapesBackslashes()
    {
        await _service.SendChatMessageAsync("path\\to\\file");

        Assert.Contains("\\\\", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_EscapesNewlines()
    {
        await _service.SendChatMessageAsync("line1\nline2");

        Assert.Contains("\\n", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_OutputsJsonStatus()
    {
        await _service.SendChatMessageAsync("test");

        Assert.Contains("\"status\":\"sent\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task SendChatMessageAsync_ThrowsOnNullMessage()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SendChatMessageAsync(null!));
    }

    [Fact]
    public async Task SendChatMessageAsync_ThrowsOnWhitespaceMessage()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SendChatMessageAsync("   "));
    }
}
