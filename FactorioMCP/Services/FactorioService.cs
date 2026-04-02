using System.Globalization;
using System.Text;
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
            storage.walk_state = {
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
                local ws = storage.walk_state
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
            storage.walk_state = nil
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
    /// Includes total slot count and free slot count for capacity awareness.
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
    /// Uses player.mine_entity() for non-resource entities which handles inventory transfer
    /// and raises proper game events. For resource entities (ores), manually harvests the
    /// resource amount and inserts products into inventory since player.mine_entity() fails
    /// on resources in Factorio 2.
    /// Prioritizes non-resource entities when multiple entities overlap at the same position.
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
            if #entities == 0 then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            -- Sort: non-resource entities first so we don't accidentally mine ore under a drill
            table.sort(entities, function(a, b)
                local a_res = a.type == "resource" and 1 or 0
                local b_res = b.type == "resource" and 1 or 0
                return a_res < b_res
            end)
            local e = entities[1]
            local name = e.name
            if e.type == "resource" then
                -- Resource entities cannot be mined with player.mine_entity() in Factorio 2.
                -- Manually harvest: get resource amount, insert products, then destroy.
                local amount = e.amount or 1
                local products = e.prototype.mineable_properties.products
                local total_expected = 0
                local total_inserted = 0
                if products then
                    for _, prod in pairs(products) do
                        local qty = prod.amount or math.floor((prod.amount_min + prod.amount_max) / 2)
                        qty = math.ceil(qty * amount)
                        if qty > 0 then
                            total_expected = total_expected + qty
                            local inserted = player.insert{name=prod.name, count=qty}
                            total_inserted = total_inserted + inserted
                        end
                    end
                end
                if total_inserted == 0 and total_expected > 0 then
                    rcon.print('{"success":false,"error":"inventory_full","entity":"'..name..'"}')
                    return
                end
                e.destroy()
                local inv_full = total_inserted < total_expected
                rcon.print('{"success":true,"entity":"'..name..'","amount":'..amount..',"inventory_full":'..tostring(inv_full)..'}')  
            else
                local mined = player.mine_entity(e, true)
                if mined then
                    rcon.print('{"success":true,"entity":"'..name..'"}')
                else
                    rcon.print('{"success":false,"error":"mine_failed","entity":"'..name..'"}')
                end
            end
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get a list of entities within the specified radius.
    /// When centerX/centerY are provided, scans around those coordinates instead of the player.
    /// </summary>
    public Task<string> GetNearbyEntitiesAsync(double radius = 10, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{{centerX.Value},{centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local center = {{posExpr}}
            local entities = player.surface.find_entities_filtered{
                position=center, radius={{radius}}
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
    /// Get technologies available for research — not yet researched, enabled,
    /// and with all prerequisites already researched. Returns each technology's
    /// name, unit cost, and ingredient requirements.
    /// </summary>
    public Task<string> GetAvailableTechnologiesAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local force = game.connected_players[1].force
            local parts = {}
            for name, tech in pairs(force.technologies) do
                if not tech.researched and tech.enabled then
                    local prereqs_met = true
                    for _, prereq in pairs(tech.prerequisites) do
                        if not prereq.researched then
                            prereqs_met = false
                            break
                        end
                    end
                    if prereqs_met then
                        local ings = {}
                        for _, ing in pairs(tech.research_unit_ingredients) do
                            ings[#ings+1] = '{"name":"'..ing.name..'","count":'..ing.amount..'}'
                        end
                        parts[#parts+1] = '{"name":"'..name..'","cost":'..tech.research_unit_count..',"ingredients":['..table.concat(ings, ",")..']}'    
                    end
                end
            end
            rcon.print('{"technologies":['..table.concat(parts, ",")..'],"count":'..#parts..'}')
            """, cancellationToken);
    }

    /// <summary>
    /// Start researching a technology by adding it to the research queue.
    /// If no research is in progress, it begins immediately.
    /// Validates that the technology exists and is not already researched.
    /// </summary>
    public Task<string> StartResearchAsync(string technology, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(technology);

        var lua = $$"""
            local force = game.connected_players[1].force
            local tech = force.technologies["{{technology}}"]
            if not tech then
                rcon.print('{"success":false,"error":"unknown_technology","technology":"{{technology}}"}')
                return
            end
            if tech.researched then
                rcon.print('{"success":false,"error":"already_researched","technology":"{{technology}}"}')
                return
            end
            local ok, err = pcall(function() force.add_research(tech) end)
            if ok then
                local ings = {}
                for _, ing in pairs(tech.research_unit_ingredients) do
                    ings[#ings+1] = '{"name":"'..ing.name..'","count":'..ing.amount..'}'
                end
                rcon.print('{"success":true,"technology":"'..tech.name..'","cost":'..tech.research_unit_count..',"ingredients":['..table.concat(ings, ",")..']}')
            else
                rcon.print('{"success":false,"error":"research_failed","technology":"{{technology}}","detail":"'..tostring(err)..'"}')
            end
            """;

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    // ── Recipe & Technology Queries ──────────────────────────────────

    /// <summary>
    /// Get details about a specific recipe — ingredients, products, crafting time, and category.
    /// </summary>
    public Task<string> GetRecipeDetailsAsync(string recipe, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe);

        var lua = $$"""
            local recipe = game.connected_players[1].force.recipes["{{recipe}}"]
            if not recipe then
                rcon.print('{"success":false,"error":"unknown_recipe","recipe":"{{recipe}}"}')
                return
            end
            local ings = {}
            for _, i in pairs(recipe.ingredients) do
                ings[#ings+1] = '{"type":"'..i.type..'","name":"'..i.name..'","amount":'..i.amount..'}'
            end
            local prods = {}
            for _, p in pairs(recipe.products) do
                local amt = p.amount or ((p.amount_min + p.amount_max) / 2)
                local prob = p.probability or 1
                prods[#prods+1] = '{"type":"'..p.type..'","name":"'..p.name..'","amount":'..amt..',"probability":'..prob..'}'
            end
            rcon.print('{"success":true,"name":"'..recipe.name..'","enabled":'..tostring(recipe.enabled)..',"energy":'..recipe.energy..',"category":"'..recipe.category..'","ingredients":['..table.concat(ings, ",")..'],"products":['..table.concat(prods, ",")..']}')
            """;

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get all recipes currently available (enabled/unlocked) for the player's force.
    /// Returns each recipe's name, category, and crafting time.
    /// </summary>
    public Task<string> GetAvailableRecipesAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local force = game.connected_players[1].force
            local parts = {}
            for name, recipe in pairs(force.recipes) do
                if recipe.enabled then
                    parts[#parts+1] = '{"name":"'..name..'","category":"'..recipe.category..'","energy":'..recipe.energy..'}'
                end
            end
            rcon.print('{"recipes":['..table.concat(parts, ",")..'],"count":'..#parts..'}')
            """, cancellationToken);
    }

    /// <summary>
    /// Get details about a specific technology — prerequisites, effects (recipe unlocks),
    /// research cost, and ingredients.
    /// </summary>
    public Task<string> GetTechnologyDetailsAsync(string technology, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(technology);

        var lua = $$"""
            local tech = game.connected_players[1].force.technologies["{{technology}}"]
            if not tech then
                rcon.print('{"success":false,"error":"unknown_technology","technology":"{{technology}}"}')
                return
            end
            local prereqs = {}
            for name, _ in pairs(tech.prerequisites) do
                prereqs[#prereqs+1] = '"'..name..'"'
            end
            local effects = {}
            for _, e in pairs(tech.effects) do
                if e.type == "unlock-recipe" then
                    effects[#effects+1] = '{"type":"unlock-recipe","recipe":"'..e.recipe..'"}'
                else
                    effects[#effects+1] = '{"type":"'..e.type..'"}'
                end
            end
            local ings = {}
            for _, ing in pairs(tech.research_unit_ingredients) do
                ings[#ings+1] = '{"name":"'..ing.name..'","count":'..ing.amount..'}'
            end
            rcon.print('{"success":true,"name":"'..tech.name..'","researched":'..tostring(tech.researched)..',"enabled":'..tostring(tech.enabled)..',"cost":'..tech.research_unit_count..',"prerequisites":['..table.concat(prereqs, ",")..'],"effects":['..table.concat(effects, ",")..'],"ingredients":['..table.concat(ings, ",")..']}')
            """;

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Check whether a recipe can be crafted with the player's current inventory.
    /// Reports the maximum craftable count and per-ingredient breakdown showing
    /// how many are needed, available, and missing.
    /// Uses <c>LuaControl.get_craftable_count()</c> for accurate results that
    /// account for intermediate crafting.
    /// </summary>
    public Task<string> CheckCraftFeasibilityAsync(string recipe, int count = 1, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local recipe = player.force.recipes["{{recipe}}"]
            if not recipe then
                rcon.print('{"success":false,"error":"unknown_recipe","recipe":"{{recipe}}"}')
                return
            end
            if not recipe.enabled then
                rcon.print('{"success":false,"error":"recipe_not_unlocked","recipe":"{{recipe}}"}')
                return
            end
            local count = {{count}}
            local craftable = player.get_craftable_count(recipe)
            local can_craft = craftable >= count
            local ings = {}
            for _, i in pairs(recipe.ingredients) do
                local needed = i.amount * count
                local available = 0
                if i.type == "item" then
                    available = player.get_item_count(i.name)
                end
                local missing = math.max(0, needed - available)
                ings[#ings+1] = '{"name":"'..i.name..'","type":"'..i.type..'","needed":'..needed..',"available":'..available..',"missing":'..missing..'}'
            end
            rcon.print('{"success":true,"recipe":"'..recipe.name..'","count":'..count..',"can_craft":'..tostring(can_craft)..',"craftable_count":'..craftable..',"ingredients":['..table.concat(ings, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
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

    /// <summary>
    /// Scan for resource patches (ores, oil, etc.) within a radius.
    /// When centerX/centerY are provided, scans around those coordinates instead of the player.
    /// Returns each resource entity's name, position, and remaining amount.
    /// </summary>
    public Task<string> ScanResourcesAsync(double radius = 50, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{{centerX.Value},{centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local center = {{posExpr}}
            local resources = player.surface.find_entities_filtered{
                position=center, radius={{radius}}, type="resource"
            }
            local summary = {}
            for _, r in pairs(resources) do
                local key = r.name
                if not summary[key] then
                    summary[key] = {name=r.name, count=0, total_amount=0, min_x=r.position.x, min_y=r.position.y, max_x=r.position.x, max_y=r.position.y}
                end
                local s = summary[key]
                s.count = s.count + 1
                s.total_amount = s.total_amount + r.amount
                if r.position.x < s.min_x then s.min_x = r.position.x end
                if r.position.y < s.min_y then s.min_y = r.position.y end
                if r.position.x > s.max_x then s.max_x = r.position.x end
                if r.position.y > s.max_y then s.max_y = r.position.y end
            end
            local parts = {}
            for _, s in pairs(summary) do
                parts[#parts+1] = '{"name":"'..s.name..'","patches":'..s.count..',"total_amount":'..s.total_amount..',"center_x":'..string.format("%.1f",(s.min_x+s.max_x)/2)..',"center_y":'..string.format("%.1f",(s.min_y+s.max_y)/2)..'}'
            end
            rcon.print('{"scan_radius":{{radius}},"resources":['..table.concat(parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Scan tiles to get terrain type information.
    /// When centerX/centerY are provided, scans around those coordinates instead of the player.
    /// Returns a summary of tile types found within the specified radius.
    /// </summary>
    public Task<string> ScanTilesAsync(double radius = 16, double? centerX = null, double? centerY = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{x={centerX.Value},y={centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local pos = {{posExpr}}
            local r = {{radius}}
            local tiles = player.surface.find_tiles_filtered{
                area={{"{"}}{pos.x-r, pos.y-r}, {pos.x+r, pos.y+r}{{"}"}}
            }
            local summary = {}
            for _, t in pairs(tiles) do
                local name = t.name
                if not summary[name] then
                    summary[name] = 0
                end
                summary[name] = summary[name] + 1
            end
            local parts = {}
            for name, count in pairs(summary) do
                parts[#parts+1] = '{"name":"'..name..'","count":'..count..'}'
            end
            rcon.print('{"scan_radius":{{radius}},"tiles":['..table.concat(parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Insert items from the player's inventory into an entity's inventory at the specified position.
    /// Supports specifying the target inventory slot (fuel, input, output, etc.).
    /// Validates proximity before interacting.
    /// </summary>
    public Task<string> InsertItemsAsync(double x, double y, string itemName, int count, string inventoryType = "fuel", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryType);

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
            -- Prioritize non-resource entities
            table.sort(entities, function(a, b)
                local a_res = a.type == "resource" and 1 or 0
                local b_res = b.type == "resource" and 1 or 0
                return a_res < b_res
            end)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            -- Map inventory type string to defines
            local inv_map = {
                fuel = defines.inventory.fuel,
                furnace_source = defines.inventory.furnace_source,
                furnace_result = defines.inventory.furnace_result,
                chest = defines.inventory.chest,
                assembling_machine_input = defines.inventory.assembling_machine_input,
                assembling_machine_output = defines.inventory.assembling_machine_output
            }
            local inv_type = inv_map["{{inventoryType}}"]
            if not inv_type then
                rcon.print('{"success":false,"error":"invalid_inventory_type","inventory_type":"{{inventoryType}}"}')
                return
            end
            local inv = e.get_inventory(inv_type)
            if not inv then
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..e.name..'","inventory_type":"{{inventoryType}}"}')
                return
            end
            local available = player.get_item_count("{{itemName}}")
            if available == 0 then
                rcon.print('{"success":false,"error":"no_items","item":"{{itemName}}","available":0}')
                return
            end
            local to_insert = math.min({{count}}, available)
            local inserted = inv.insert{name="{{itemName}}", count=to_insert}
            if inserted > 0 then
                player.remove_item{name="{{itemName}}", count=inserted}
            end
            rcon.print('{"success":true,"entity":"'..e.name..'","item":"{{itemName}}","inserted":'..inserted..',"requested":'..{{count}}..'}')  
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Remove items from an entity's inventory at the specified position into the player's inventory.
    /// Supports specifying the source inventory slot (fuel, input, output, etc.).
    /// Validates proximity before interacting.
    /// </summary>
    public Task<string> RemoveItemsAsync(double x, double y, string itemName, int count, string inventoryType = "furnace_result", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryType);

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
            table.sort(entities, function(a, b)
                local a_res = a.type == "resource" and 1 or 0
                local b_res = b.type == "resource" and 1 or 0
                return a_res < b_res
            end)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local inv_map = {
                fuel = defines.inventory.fuel,
                furnace_source = defines.inventory.furnace_source,
                furnace_result = defines.inventory.furnace_result,
                chest = defines.inventory.chest,
                assembling_machine_input = defines.inventory.assembling_machine_input,
                assembling_machine_output = defines.inventory.assembling_machine_output
            }
            local inv_type = inv_map["{{inventoryType}}"]
            if not inv_type then
                rcon.print('{"success":false,"error":"invalid_inventory_type","inventory_type":"{{inventoryType}}"}')
                return
            end
            local inv = e.get_inventory(inv_type)
            if not inv then
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..e.name..'","inventory_type":"{{inventoryType}}"}')
                return
            end
            local item_count = inv.get_item_count("{{itemName}}")
            if item_count == 0 then
                rcon.print('{"success":false,"error":"no_items","entity":"'..e.name..'","item":"{{itemName}}","available":0}')
                return
            end
            local to_remove = math.min({{count}}, item_count)
            local removed = inv.remove{name="{{itemName}}", count=to_remove}
            local inserted = 0
            if removed > 0 then
                inserted = player.insert{name="{{itemName}}", count=removed}
                if inserted < removed then
                    inv.insert{name="{{itemName}}", count=removed - inserted}
                end
            end
            local inv_full = inserted < removed
            rcon.print('{"success":true,"entity":"'..e.name..'","item":"{{itemName}}","removed":'..inserted..',"requested":'..{{count}}..',"inventory_full":'..tostring(inv_full)..'}')  
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Inspect an entity at the specified position, returning its status, inventory contents,
    /// fuel level, recipe, and other relevant information.
    /// Validates proximity before interacting.
    /// </summary>
    public Task<string> InspectEntityAsync(double x, double y, CancellationToken cancellationToken = default)
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
            table.sort(entities, function(a, b)
                local a_res = a.type == "resource" and 1 or 0
                local b_res = b.type == "resource" and 1 or 0
                return a_res < b_res
            end)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local result = '{"success":true,"entity":"'..e.name..'","type":"'..e.type..'","position":{"x":'..e.position.x..',"y":'..e.position.y..'}'
            -- Status
            if e.status then
                local status_names = {}
                for k, v in pairs(defines.entity_status) do status_names[v] = k end
                local status_name = status_names[e.status] or tostring(e.status)
                result = result..',"status":"'..status_name..'"'
            end
            -- Health
            if e.health then
                result = result..',"health":'..e.health
                if e.prototype and e.prototype.max_health then
                    result = result..',"max_health":'..e.prototype.max_health
                end
            end
            -- Recipe (assembling machines)
            local ok_recipe, recipe = pcall(function() return e.get_recipe() end)
            if ok_recipe and recipe then
                result = result..',"recipe":"'..recipe.name..'"'
            end
            -- Inventories
            local inv_names = {"fuel", "furnace_source", "furnace_result", "chest", "assembling_machine_input", "assembling_machine_output"}
            local inv_defines = {defines.inventory.fuel, defines.inventory.furnace_source, defines.inventory.furnace_result, defines.inventory.chest, defines.inventory.assembling_machine_input, defines.inventory.assembling_machine_output}
            local inv_parts = {}
            for i, inv_name in pairs(inv_names) do
                local inv = e.get_inventory(inv_defines[i])
                if inv then
                    local contents = inv.get_contents()
                    local items = {}
                    for _, item_stack in pairs(contents) do
                        items[#items+1] = '{"name":"'..item_stack.name..'","count":'..item_stack.count..'}'
                    end
                    inv_parts[#inv_parts+1] = '"'..inv_name..'":[' ..table.concat(items, ",")..']'
                end
            end
            if #inv_parts > 0 then
                result = result..',"inventories":{'..table.concat(inv_parts, ",")..'}'
            end
            -- Burner (fuel remaining)
            if e.burner then
                result = result..',"burner":{"remaining_burning_fuel":'..string.format("%.1f", e.burner.remaining_burning_fuel)..',"heat":'..string.format("%.1f", e.burner.heat)..',"heat_capacity":'..string.format("%.1f", e.burner.heat_capacity)..'}'
            end
            -- Mining target (for mining drills)
            if e.mining_target then
                result = result..',"mining_target":"'..e.mining_target.name..'"'
            end
            result = result..'}'
            rcon.print(result)
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

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

    /// <summary>
    /// Transfer all items from an entity's inventory to the player's inventory.
    /// Finds the entity at the given position and moves everything from the
    /// specified inventory type into the player's main inventory.
    /// </summary>
    public Task<string> TransferAllItemsAsync(
        double x,
        double y,
        string inventoryType = "chest",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryType);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            if #entities == 0 then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local e = entities[1]
            local inv = e.get_inventory(defines.inventory.{{inventoryType}})
            if not inv then
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..e.name..'","inventory_type":"{{inventoryType}}"}')
                return
            end
            local transferred = {}
            local total = 0
            local inv_full = false
            for i = 1, #inv do
                local stack = inv[i]
                if stack.valid_for_read then
                    local name = stack.name
                    local cnt = stack.count
                    local inserted = player.insert{name=name, count=cnt}
                    if inserted > 0 then
                        stack.count = cnt - inserted
                        transferred[#transferred+1] = '{"item":"'..name..'","count":'..inserted..'}'
                        total = total + inserted
                    end
                    if inserted < cnt then
                        inv_full = true
                        break
                    end
                end
            end
            rcon.print('{"success":true,"entity":"'..e.name..'","transferred":['..table.concat(transferred, ",")..'],"total_items":'..total..',"inventory_full":'..tostring(inv_full)..'}')  
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Get the contents of a specific entity's inventory at a position.
    /// Returns all items and counts in the specified inventory slot.
    /// </summary>
    public Task<string> GetEntityInventoryAsync(
        double x,
        double y,
        string inventoryType = "chest",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryType);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            if #entities == 0 then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local e = entities[1]
            local inv = e.get_inventory(defines.inventory.{{inventoryType}})
            if not inv then
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..e.name..'","inventory_type":"{{inventoryType}}"}')
                return
            end
            local contents = inv.get_contents()
            local parts = {}
            for _, item in pairs(contents) do
                parts[#parts+1] = '{"name":"'..item.name..'","count":'..item.count..'}'
            end
            rcon.print('{"success":true,"entity":"'..e.name..'","inventory_type":"{{inventoryType}}","items":['..table.concat(parts, ",")..'],"slots":'..#inv..',"empty_slots":'..inv.count_empty_stacks()..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
