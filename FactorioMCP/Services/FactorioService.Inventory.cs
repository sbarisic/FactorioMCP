using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
    /// <summary>
    /// Get the contents of the player's main inventory as a JSON array of items.
    /// Includes total slot count and free slot count for capacity awareness.
    /// </summary>
    public Task<string> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            {{LuaJsonEscape}}
            local player = game.connected_players[1]
            local inv = player.get_main_inventory()
            local items = {}
            for i = 1, #inv do
                local stack = inv[i]
                if stack.valid_for_read then
                    items[stack.name] = (items[stack.name] or 0) + stack.count
                end
            end
            local parts = {}
            for name, count in pairs(items) do
                parts[#parts+1] = '{"name":"'..esc(name)..'","count":'..count..'}'
            end
            rcon.print('{"items":['..table.concat(parts, ",")..'],"total_slots":'..#inv..',"free_slots":'..inv.count_empty_stacks()..'}')
            """, cancellationToken);
    }

    /// <summary>
    /// Begin crafting items. Uses the real crafting queue so the player must wait for completion.
    /// Returns the number of items that were actually queued for crafting.
    /// </summary>
    public Task<string> CraftAsync(string recipe, int count, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local ok, result = pcall(function() return player.begin_crafting{count={{count}}, recipe="{{recipe}}"} end)
            if not ok then
                rcon.print('{"status":"error","error":"unknown_recipe","recipe":"{{recipe}}"}')
            elseif result == 0 then
                rcon.print('{"status":"no_materials","recipe":"{{recipe}}","requested":{{count}},"queued":0}')
            else
                rcon.print('{"status":"crafting","recipe":"{{recipe}}","requested":{{count}},"queued":'..result..'}')
            end
            """);
        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get the player's current crafting queue contents.
    /// </summary>
    public Task<string> GetCraftingQueueAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            {{LuaJsonEscape}}
            local queue = game.connected_players[1].crafting_queue
            if queue then
                local parts = {}
                for _, item in pairs(queue) do
                    parts[#parts+1] = '{"recipe":"'..esc(item.recipe)..'","count":'..item.count..'}'
                end
                rcon.print('{"queue":['..table.concat(parts, ",")..']}')
            else
                rcon.print('{"queue":[]}')
            end
            """, cancellationToken);
    }

    /// <summary>
    /// Drop items from the player's inventory onto the ground at the player's position.
    /// Uses <c>surface.spill_item_stack</c> to scatter items near the player.
    /// Removes items from inventory first, then spills them.
    /// </summary>
    public Task<string> DropItemsAsync(
        string itemName,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local name = "{{itemName}}"
            local available = player.get_item_count(name)
            local want = {{count}}
            if available == 0 then
                rcon.print('{"success":false,"error":"no_items","item":"'..name..'"}')
                return
            end
            local to_drop = math.min(want, available)
            player.remove_item{name=name, count=to_drop}
            local drop_stack = {name=name, count=to_drop}
            player.surface.spill_item_stack{position=player.position, stack=drop_stack}
            rcon.print('{"success":true,"item":"'..name..'","dropped":'..to_drop..',"remaining":'..player.get_item_count(name)..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
