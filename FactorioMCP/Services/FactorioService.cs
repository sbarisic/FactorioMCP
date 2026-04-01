using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// High-level service for controlling a Factorio game instance via RCON Lua commands.
/// All operations execute Lua scripts through the /c console command and return
/// JSON-formatted output from rcon.print() for reliable AI parsing.
/// </summary>
internal sealed class FactorioService(RconClient rcon)
{
    /// <summary>
    /// Start walking in a direction. Registers an on_tick handler with stuck detection
    /// so the player keeps walking every game tick until <see cref="StopWalkingAsync"/> is called.
    /// When the player is blocked by an entity, the handler automatically tries perpendicular
    /// directions to navigate around the obstacle, then resumes the original direction.
    /// Valid directions: north, south, east, west, northeast, northwest, southeast, southwest.
    /// </summary>
    public Task<string> WalkAsync(string direction, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local target_dir = defines.direction.{{direction}}
            player.walking_state = {walking = true, direction = target_dir}

            -- Perpendicular directions for obstacle avoidance
            local perp = {
                [defines.direction.north]     = {defines.direction.east,      defines.direction.west},
                [defines.direction.south]     = {defines.direction.west,      defines.direction.east},
                [defines.direction.east]      = {defines.direction.south,     defines.direction.north},
                [defines.direction.west]      = {defines.direction.north,     defines.direction.south},
                [defines.direction.northeast] = {defines.direction.southeast, defines.direction.northwest},
                [defines.direction.northwest] = {defines.direction.northeast, defines.direction.southwest},
                [defines.direction.southeast] = {defines.direction.northeast, defines.direction.southwest},
                [defines.direction.southwest] = {defines.direction.northwest, defines.direction.southeast}
            }

            -- State for stuck detection
            global.walk_state = {
                target_dir = target_dir,
                prev_x = player.position.x,
                prev_y = player.position.y,
                stuck_ticks = 0,
                detour_dir = nil,
                detour_ticks = 0,
                detour_side = 1
            }

            script.on_event(defines.events.on_tick, function()
                local p = game.connected_players[1]
                if not (p and p.valid) then return end
                local ws = global.walk_state
                if not ws then return end

                local pos = p.position
                local dx = pos.x - ws.prev_x
                local dy = pos.y - ws.prev_y
                local moved = (dx*dx + dy*dy) > 0.0001

                if ws.detour_dir then
                    -- Currently detouring around an obstacle
                    if moved then
                        ws.detour_ticks = ws.detour_ticks + 1
                    end
                    if ws.detour_ticks >= 15 then
                        -- Detoured enough, try original direction again
                        ws.detour_dir = nil
                        ws.detour_ticks = 0
                        ws.stuck_ticks = 0
                    end
                    p.walking_state = {walking = true, direction = ws.detour_dir or ws.target_dir}
                else
                    -- Walking in target direction
                    if moved then
                        ws.stuck_ticks = 0
                    else
                        ws.stuck_ticks = ws.stuck_ticks + 1
                    end

                    if ws.stuck_ticks >= 10 then
                        -- Stuck! Pick a perpendicular direction to detour
                        local sides = perp[ws.target_dir]
                        if sides then
                            ws.detour_dir = sides[ws.detour_side]
                            ws.detour_ticks = 0
                            ws.detour_side = (ws.detour_side % 2) + 1
                        end
                        ws.stuck_ticks = 0
                        p.walking_state = {walking = true, direction = ws.detour_dir or ws.target_dir}
                    else
                        p.walking_state = {walking = true, direction = ws.target_dir}
                    end
                end

                ws.prev_x = pos.x
                ws.prev_y = pos.y
            end)
            local p = player.position
            rcon.print('{"status":"walking","direction":"{{direction}}","x":'..p.x..',"y":'..p.y..'}')
            """);
        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Stop the player from walking, de-register the on_tick walking handler,
    /// and clean up the walk state used for stuck detection.
    /// </summary>
    public Task<string> StopWalkingAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local player = game.connected_players[1]
            script.on_event(defines.events.on_tick, nil)
            global.walk_state = nil
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
    /// Get the contents of the player's main inventory as a JSON array of items.
    /// </summary>
    public Task<string> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
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
                parts[#parts+1] = '{"name":"'..name..'","count":'..count..'}'
            end
            rcon.print('{"items":['..table.concat(parts, ",")..']}')
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
            local crafted = player.begin_crafting{count={{count}}, recipe="{{recipe}}"}
            rcon.print('{"status":"crafting","recipe":"{{recipe}}","requested":{{count}},"queued":'..crafted..'}')
            """);
        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get the player's current crafting queue contents.
    /// </summary>
    public Task<string> GetCraftingQueueAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local queue = game.connected_players[1].crafting_queue
            if queue then
                local parts = {}
                for _, item in pairs(queue) do
                    parts[#parts+1] = '{"recipe":"'..item.recipe..'","count":'..item.count..'}'
                end
                rcon.print('{"queue":['..table.concat(parts, ",")..']}')
            else
                rcon.print('{"queue":[]}')
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
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = {{{x}}, {{y}}}
            local name = "{{entityName}}"
            local dir = defines.direction.{{direction}}
            local player_pos = player.position
            local dx = pos[1] - player_pos.x
            local dy = pos[2] - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            if distance > player.build_distance then
                rcon.print('{"success":false,"error":"out_of_range","distance":'..string.format("%.1f", distance)..',"limit":'..player.build_distance..'}')
                return
            end
            if not surface.can_place_entity{name=name, position=pos, force=player.force, direction=dir} then
                rcon.print('{"success":false,"error":"invalid_position","entity":"'..name..'","x":'..pos[1]..',"y":'..pos[2]..'}')
                return
            end
            if player.get_item_count(name) < 1 then
                rcon.print('{"success":false,"error":"missing_item","entity":"'..name..'"}')
                return
            end
            player.remove_item{name=name, count=1}
            surface.create_entity{name=name, position=pos, force=player.force, player=player, direction=dir}
            rcon.print('{"success":true,"entity":"'..name..'","x":'..pos[1]..',"y":'..pos[2]..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Mine/remove an entity at the specified position. Mined items go to the player's inventory.
    /// Uses player.mine_entity() which handles inventory transfer and raises proper game events.
    /// Validates proximity before mining.
    /// </summary>
    public Task<string> MineEntityAtAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local player_pos = player.position
            local dx = {{x}} - player_pos.x
            local dy = {{y}} - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            if distance > player.reach_distance then
                rcon.print('{"success":false,"error":"out_of_range","distance":'..string.format("%.1f", distance)..',"limit":'..player.reach_distance..'}')
                return
            end
            local entities = player.surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            if #entities > 0 then
                local e = entities[1]
                local name = e.name
                local mined = player.mine_entity(e, true)
                if mined then
                    rcon.print('{"success":true,"entity":"'..name..'"}')
                else
                    rcon.print('{"success":false,"error":"mine_failed","entity":"'..name..'"}')
                end
            else
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
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
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{
                position=player.position, radius={{radius}}
            }
            local parts = {}
            for _, e in pairs(entities) do
                parts[#parts+1] = '{"name":"'..e.name..'","x":'..e.position.x..',"y":'..e.position.y..'}'
            end
            rcon.print('{"entities":['..table.concat(parts, ",")..']}')
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
            local player = game.connected_players[1]
            local player_pos = player.position
            local dx = {{x}} - player_pos.x
            local dy = {{y}} - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            local build_ok = distance <= player.build_distance
            local reach_ok = distance <= player.reach_distance
            rcon.print('{"distance":'..string.format("%.1f", distance)..',"build_in_range":'..tostring(build_ok)..',"build_limit":'..player.build_distance..',"reach_in_range":'..tostring(reach_ok)..',"reach_limit":'..player.reach_distance..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get the current research status and progress for the player's force.
    /// </summary>
    public Task<string> GetResearchStatusAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local tech = game.connected_players[1].force.current_research
            if tech then
                rcon.print('{"researching":true,"technology":"'..tech.name..'","progress":'..string.format("%.3f", tech.research_progress)..'}')
            else
                rcon.print('{"researching":false}')
            end
            """, cancellationToken);
    }

    /// <summary>
    /// Poll the crafting queue until it is empty or the timeout expires.
    /// Returns the final queue state as JSON.
    /// </summary>
    public async Task<string> WaitForCraftingAsync(
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await GetCraftingQueueAsync(cancellationToken);
            if (result.Contains("\"queue\":[]"))
                return """{"status":"complete","queue":[]}""";
            await Task.Delay(pollInterval, cancellationToken);
        }

        var finalQueue = await GetCraftingQueueAsync(cancellationToken);
        return $$"""{"status":"timeout","remaining":{{finalQueue}}}""";
    }

    /// <summary>
    /// Poll the player position until it is within the given tolerance of the target
    /// coordinates, or the timeout expires. Returns the final position as JSON.
    /// </summary>
    public async Task<string> WaitForPositionAsync(
        double targetX,
        double targetY,
        double tolerance,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tolerance, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lua = string.Create(CultureInfo.InvariantCulture, $$"""
                local p = game.connected_players[1].position
                local dx = p.x - {{targetX}}
                local dy = p.y - {{targetY}}
                local dist = math.sqrt(dx*dx + dy*dy)
                rcon.print('{"x":'..p.x..',"y":'..p.y..',"distance":'..string.format("%.2f", dist)..'}')
                """);
            var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);
            if (result.Contains("\"distance\":"))
            {
                var distStr = result.Split("\"distance\":")[1].Split([',', '}'])[0];
                if (double.TryParse(distStr, CultureInfo.InvariantCulture, out var dist) && dist <= tolerance)
                    return $$"""{"status":"arrived","tolerance":{{string.Format(CultureInfo.InvariantCulture, "{0}", tolerance)}},"position":{{result}}}""";
            }
            await Task.Delay(pollInterval, cancellationToken);
        }

        var finalPos = await GetPlayerPositionAsync(cancellationToken);
        return $$"""{"status":"timeout","target_x":{{string.Format(CultureInfo.InvariantCulture, "{0}", targetX)}},"target_y":{{string.Format(CultureInfo.InvariantCulture, "{0}", targetY)}},"position":{{finalPos}}}""";
    }

    /// <summary>
    /// Get the current game tick count.
    /// </summary>
    public Task<string> GetGameTickAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            rcon.print('{"tick":'..game.tick..'}')
            """, cancellationToken);
    }

    /// <summary>
    /// Wait for a specified number of game ticks to elapse.
    /// Polls the game tick and waits until the target tick count has passed.
    /// </summary>
    public async Task<string> WaitForTicksAsync(
        int ticks,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var startResult = await GetGameTickAsync(cancellationToken);
        var startTickStr = startResult.Split("\"tick\":")[1].Split('}')[0];
        if (!long.TryParse(startTickStr, CultureInfo.InvariantCulture, out var startTick))
            return """{"status":"error","error":"failed_to_read_tick"}""";

        var targetTick = startTick + ticks;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(pollInterval, cancellationToken);

            var tickResult = await GetGameTickAsync(cancellationToken);
            var currentTickStr = tickResult.Split("\"tick\":")[1].Split('}')[0];
            if (long.TryParse(currentTickStr, CultureInfo.InvariantCulture, out var currentTick) && currentTick >= targetTick)
                return string.Create(CultureInfo.InvariantCulture, $$$"""{"status":"complete","start_tick":{{{startTick}}},"end_tick":{{{currentTick}}},"elapsed":{{{currentTick - startTick}}}}""");
        }

        var finalResult = await GetGameTickAsync(cancellationToken);
        var finalTickStr = finalResult.Split("\"tick\":")[1].Split('}')[0];
        long.TryParse(finalTickStr, CultureInfo.InvariantCulture, out var finalTick);
        return string.Create(CultureInfo.InvariantCulture, $$$"""{"status":"timeout","start_tick":{{{startTick}}},"current_tick":{{{finalTick}}},"target_tick":{{{targetTick}}}}""");
    }

    /// <summary>
    /// Execute arbitrary Lua code on the Factorio instance.
    /// </summary>
    public Task<string> ExecuteRawLuaAsync(string luaCode, CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync(luaCode, cancellationToken);
    }
}
