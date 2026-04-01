using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// High-level service for controlling a Factorio game instance via RCON Lua commands.
/// All operations execute Lua scripts through the /c console command and return
/// the text output from rcon.print().
/// </summary>
internal sealed class FactorioService(RconClient rcon)
{
    /// <summary>
    /// Start walking in a direction. The player will keep walking until stopped.
    /// Valid directions: north, south, east, west, northeast, northwest, southeast, southwest.
    /// </summary>
    public Task<string> WalkAsync(string direction, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        return rcon.ExecuteLuaAsync(
            $"game.player.walking_state = {{walking = true, direction = defines.direction.{direction}}}",
            cancellationToken);
    }

    /// <summary>
    /// Stop the player from walking.
    /// </summary>
    public Task<string> StopWalkingAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync(
            "game.player.walking_state = {walking = false, direction = defines.direction.north}",
            cancellationToken);
    }

    /// <summary>
    /// Get the player's current map position.
    /// </summary>
    public Task<string> GetPlayerPositionAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync(
            "rcon.print(serpent.line(game.player.position))",
            cancellationToken);
    }

    /// <summary>
    /// Get the contents of the player's main inventory as a list of item names and counts.
    /// </summary>
    public Task<string> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local inv = game.player.get_main_inventory()
            local items = {}
            for i = 1, #inv do
                local stack = inv[i]
                if stack.valid_for_read then
                    items[stack.name] = (items[stack.name] or 0) + stack.count
                end
            end
            local result = ""
            for name, count in pairs(items) do
                result = result .. name .. ": " .. count .. "\n"
            end
            rcon.print(result)
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
        return rcon.ExecuteLuaAsync(
            $"local crafted = game.player.begin_crafting{{count={count}, recipe=\"{recipe}\"}} rcon.print(\"Queued \" .. crafted .. \" {recipe}\")",
            cancellationToken);
    }

    /// <summary>
    /// Get the player's current crafting queue contents.
    /// </summary>
    public Task<string> GetCraftingQueueAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local queue = game.player.crafting_queue
            if queue then
                local result = ""
                for _, item in pairs(queue) do
                    result = result .. item.recipe .. " x" .. item.count .. "\n"
                end
                rcon.print(result)
            else
                rcon.print("No items in crafting queue")
            end
            """, cancellationToken);
    }

    /// <summary>
    /// Place an entity from the player's inventory at the specified position.
    /// Validates proximity, inventory contents, and position validity before placing.
    /// </summary>
    public Task<string> PlaceEntityAsync(
        string entityName,
        double x,
        double y,
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local surface = game.player.surface
            local pos = {{{x}}, {{y}}}
            local name = "{{entityName}}"
            local dir = defines.direction.{{direction}}
            local player_pos = game.player.position
            local dx = pos[1] - player_pos.x
            local dy = pos[2] - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            if distance > game.player.build_distance then
                rcon.print("Out of range: " .. string.format("%.1f", distance) .. " tiles away (build distance: " .. game.player.build_distance .. ")")
                return
            end
            if not surface.can_place_entity{name=name, position=pos, force=game.player.force, direction=dir} then
                rcon.print("Cannot place " .. name .. " at " .. serpent.line(pos))
                return
            end
            if game.player.get_item_count(name) < 1 then
                rcon.print("No " .. name .. " in inventory")
                return
            end
            game.player.remove_item{name=name, count=1}
            surface.create_entity{name=name, position=pos, force=game.player.force, player=game.player, direction=dir}
            rcon.print("Placed " .. name .. " at " .. serpent.line(pos))
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Mine/remove an entity at the specified position. Mined items go to the player's inventory.
    /// Validates proximity before mining.
    /// </summary>
    public Task<string> MineEntityAtAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player_pos = game.player.position
            local dx = {{x}} - player_pos.x
            local dy = {{y}} - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            if distance > game.player.reach_distance then
                rcon.print("Out of range: " .. string.format("%.1f", distance) .. " tiles away (reach distance: " .. game.player.reach_distance .. ")")
                return
            end
            local entities = game.player.surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            if #entities > 0 then
                local e = entities[1]
                local name = e.name
                e.mine{inventory=game.player.get_main_inventory()}
                rcon.print("Mined " .. name)
            else
                rcon.print("No entity found at position")
            end
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get a list of entities near the player within the specified radius.
    /// </summary>
    public Task<string> GetNearbyEntitiesAsync(double radius = 10, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local entities = game.player.surface.find_entities_filtered{
                position=game.player.position, radius={{radius}}
            }
            local result = ""
            for _, e in pairs(entities) do
                result = result .. e.name .. " at " .. serpent.line(e.position) .. "\n"
            end
            rcon.print(result)
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Check the distance from the player to a target position and report whether
    /// it is within build and reach range.
    /// </summary>
    public Task<string> CheckDistanceAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player_pos = game.player.position
            local dx = {{x}} - player_pos.x
            local dy = {{y}} - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            local build_ok = distance <= game.player.build_distance
            local reach_ok = distance <= game.player.reach_distance
            rcon.print("Distance: " .. string.format("%.1f", distance) .. " tiles"
                .. " | Build: " .. (build_ok and "in range" or "OUT OF RANGE") .. " (" .. game.player.build_distance .. ")"
                .. " | Reach: " .. (reach_ok and "in range" or "OUT OF RANGE") .. " (" .. game.player.reach_distance .. ")")
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get the current research status and progress for the player's force.
    /// </summary>
    public Task<string> GetResearchStatusAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local tech = game.player.force.current_research
            if tech then
                rcon.print("Researching: " .. tech.name .. " (" ..
                    string.format("%.1f", tech.research_progress * 100) .. "%)")
            else
                rcon.print("No active research")
            end
            """, cancellationToken);
    }

    /// <summary>
    /// Execute arbitrary Lua code on the Factorio instance.
    /// </summary>
    public Task<string> ExecuteRawLuaAsync(string luaCode, CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync(luaCode, cancellationToken);
    }
}
