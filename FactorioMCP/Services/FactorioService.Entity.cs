using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
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
            local placed = surface.create_entity{name=name, position=pos, force=player.force, player=player, direction=dir}
            if not placed then
                player.insert{name=name, count=1}
                rcon.print('{"success":false,"error":"placement_failed","entity":"'..name..'","x":'..pos[1]..',"y":'..pos[2]..'}')
                return
            end
            rcon.print('{"success":true,"entity":"'..name..'","x":'..pos[1]..',"y":'..pos[2]..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Mine/remove a non-resource entity (building) at the specified position.
    /// Mined items go to the player's inventory. Uses player.mine_entity() which handles
    /// inventory transfer and raises proper game events.
    /// Prioritizes non-resource entities when multiple entities overlap at the same position.
    /// If only resource entities exist at the position, returns an error directing the AI
    /// to use MineResource instead (realistic tick-based mining).
    /// Validates proximity before mining.
    /// </summary>
    public Task<string> MineEntityAtAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
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
            sort_entities(entities)
            local e = entities[1]
            local name = esc(e.name)
            if e.type == "resource" then
                -- Resource entities must be mined with MineResource for realistic timing
                rcon.print('{"success":false,"error":"use_mine_resource","entity":"'..name..'"'..
                    ',"amount":'..(e.amount or 0)..
                    ',"message":"Resource entities must be mined with the MineResource tool for realistic mining duration"}')
                return
            end
            local mined = player.mine_entity(e, true)
            if mined then
                rcon.print('{"success":true,"entity":"'..name..'"}')
            else
                rcon.print('{"success":false,"error":"mine_failed","entity":"'..name..'"}')
            end
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Rotate a non-resource entity at the specified position.
    /// Uses the built-in LuaEntity.rotate() which handles all entity-type constraints
    /// and raises proper game events. Validates proximity before interacting.
    /// </summary>
    public Task<string> RotateEntityAsync(double x, double y, bool reverse = false, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
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
            sort_entities(entities)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local dir_names = {}
            for k, v in pairs(defines.direction) do dir_names[v] = k end
            local prev_dir = dir_names[e.direction] or "unknown"
            local rotated = e.rotate({reverse={{(reverse ? "true" : "false")}}, by_player=player})
            if not rotated then
                rcon.print('{"success":false,"error":"rotation_failed","entity":"'..esc(e.name)..'","direction":"'..prev_dir..'"}')
                return
            end
            local new_dir = dir_names[e.direction] or "unknown"
            rcon.print('{"success":true,"entity":"'..esc(e.name)..'","previous_direction":"'..prev_dir..'","new_direction":"'..new_dir..'","x":'..e.position.x..',"y":'..e.position.y..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
    /// <summary>
    /// Insert items from the player's inventory into a nearby entity's inventory.
    /// Supports specifying the target inventory slot (fuel, input, output, etc.).
    /// Validates proximity before interacting.
    /// </summary>
    public Task<string> InsertItemsAsync(double x, double y, string itemName, int count, string inventoryType = "fuel", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryType);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
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
            sort_entities(entities)
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
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..esc(e.name)..'","inventory_type":"{{inventoryType}}"}')
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
            rcon.print('{"success":true,"entity":"'..esc(e.name)..'","item":"{{itemName}}","inserted":'..inserted..',"requested":'..({{count}})..'}')  
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
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
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
            sort_entities(entities)
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
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..esc(e.name)..'","inventory_type":"{{inventoryType}}"}')
                return
            end
            local item_count = inv.get_item_count("{{itemName}}")
            if item_count == 0 then
                rcon.print('{"success":false,"error":"no_items","entity":"'..esc(e.name)..'","item":"{{itemName}}","available":0}')
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
            rcon.print('{"success":true,"entity":"'..esc(e.name)..'","item":"{{itemName}}","removed":'..removed..',"transferred":'..inserted..',"requested":'..({{count}})..',"inventory_full":'..tostring(inv_full)..'}')  
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
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
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
            sort_entities(entities)
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local surface = player.surface
            local result = '{"success":true,"entity":"'..esc(e.name)..'","type":"'..esc(e.type)..'","position":{"x":'..e.position.x..',"y":'..e.position.y..'}'
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
                if e.max_health then
                    result = result..',"max_health":'..e.max_health
                end
            end
            -- Recipe (assembling machines)
            local ok_recipe, recipe = pcall(function() return e.get_recipe() end)
            if ok_recipe and recipe then
                result = result..',"recipe":"'..esc(recipe.name)..'"'
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
                        items[#items+1] = '{"name":"'..esc(item_stack.name)..'","count":'..item_stack.count..'}'
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
            if e.type == "mining-drill" and e.mining_target then
                result = result..',"mining_target":"'..esc(e.mining_target.name)..'"'
            end
            -- Direction (for all directional entities)
            local dir_names = {}
            for k, v in pairs(defines.direction) do dir_names[v] = k end
            local dir_name = dir_names[e.direction]
            if dir_name then
                result = result..',"direction":"'..dir_name..'"'
            end
            -- Inserter pickup/drop info
            if e.type == "inserter" then
                local offsets = {
                    [defines.direction.north]     = {dx=0,  dy=-1},
                    [defines.direction.south]     = {dx=0,  dy=1},
                    [defines.direction.east]      = {dx=1,  dy=0},
                    [defines.direction.west]      = {dx=-1, dy=0},
                    [defines.direction.northeast] = {dx=1,  dy=-1},
                    [defines.direction.northwest] = {dx=-1, dy=-1},
                    [defines.direction.southeast] = {dx=1,  dy=1},
                    [defines.direction.southwest] = {dx=-1, dy=1}
                }
                local off = offsets[e.direction]
                if off then
                    -- In Factorio, direction = PICKUP side (arm extends in facing direction)
                    -- Drop is on the OPPOSITE side
                    local pickup_x, pickup_y = e.position.x + off.dx, e.position.y + off.dy
                    local drop_x, drop_y = e.position.x - off.dx, e.position.y - off.dy
                    local drop_ents = surface.find_entities_filtered{position={drop_x, drop_y}, radius=0.5}
                    local drop_parts = {}
                    for _, de in pairs(drop_ents) do
                        if de.name ~= "character" and de ~= e then
                            drop_parts[#drop_parts+1] = '"'..esc(de.name)..'"'
                        end
                    end
                    local pickup_ents = surface.find_entities_filtered{position={pickup_x, pickup_y}, radius=0.5}
                    local pickup_parts = {}
                    for _, pe in pairs(pickup_ents) do
                        if pe.name ~= "character" and pe ~= e then
                            pickup_parts[#pickup_parts+1] = '"'..esc(pe.name)..'"'
                        end
                    end
                    result = result..',"inserter_info":{"drop":{"x":'..drop_x..',"y":'..drop_y..',"entities":['..table.concat(drop_parts, ",")..']}'
                    result = result..',"pickup":{"x":'..pickup_x..',"y":'..pickup_y..',"entities":['..table.concat(pickup_parts, ",")..']}'
                    result = result..'}'
                end
            end
            result = result..'}'
            rcon.print(result)
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
            {{LuaJsonEscape}}
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            if #entities == 0 then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local e = entities[1]
            local inv = e.get_inventory(defines.inventory.{{inventoryType}})
            if not inv then
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..esc(e.name)..'","inventory_type":"{{inventoryType}}"}')
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
                        transferred[#transferred+1] = '{"item":"'..esc(name)..'","count":'..inserted..'}'
                        total = total + inserted
                    end
                    if inserted < cnt then
                        inv_full = true
                        break
                    end
                end
            end
            rcon.print('{"success":true,"entity":"'..esc(e.name)..'","transferred":['..table.concat(transferred, ",")..'],"total_items":'..total..',"inventory_full":'..tostring(inv_full)..'}')  
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
            {{LuaJsonEscape}}
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            if #entities == 0 then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local e = entities[1]
            local inv = e.get_inventory(defines.inventory.{{inventoryType}})
            if not inv then
                rcon.print('{"success":false,"error":"no_inventory","entity":"'..esc(e.name)..'","inventory_type":"{{inventoryType}}"}')
                return
            end
            local contents = inv.get_contents()
            local parts = {}
            for _, item in pairs(contents) do
                parts[#parts+1] = '{"name":"'..esc(item.name)..'","count":'..item.count..'}'
            end
            rcon.print('{"success":true,"entity":"'..esc(e.name)..'","inventory_type":"{{inventoryType}}","items":['..table.concat(parts, ",")..'],"slots":'..#inv..',"empty_slots":'..inv.count_empty_stacks()..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Preview what an inserter placed at the given position and direction would pick up from
    /// and drop to. Reports the calculated pickup/drop tile positions and any entities found there.
    /// Does NOT actually place anything — purely informational for planning inserter layouts.
    /// </summary>
    public Task<string> PreviewInserterPlacementAsync(
        double x,
        double y,
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            local dir = defines.direction.{{direction}}
            local ix, iy = {{x}}, {{y}}

            -- Direction offsets: the direction the inserter faces is the PICKUP direction
            -- (the arm extends in the facing direction to grab items)
            local offsets = {
                [defines.direction.north]     = {dx=0,  dy=-1},
                [defines.direction.south]     = {dx=0,  dy=1},
                [defines.direction.east]      = {dx=1,  dy=0},
                [defines.direction.west]      = {dx=-1, dy=0},
                [defines.direction.northeast] = {dx=1,  dy=-1},
                [defines.direction.northwest] = {dx=-1, dy=-1},
                [defines.direction.southeast] = {dx=1,  dy=1},
                [defines.direction.southwest] = {dx=-1, dy=1}
            }
            local off = offsets[dir]
            if not off then
                rcon.print('{"success":false,"error":"invalid_direction","direction":"{{direction}}"}')
                return
            end

            -- Pickup position is in the facing direction, drop is opposite
            local pickup_x, pickup_y = ix + off.dx, iy + off.dy
            local drop_x, drop_y = ix - off.dx, iy - off.dy

            local surface = game.connected_players[1].surface

            -- Find entities at drop position
            local drop_entities = surface.find_entities_filtered{position={drop_x, drop_y}, radius=0.5}
            local drop_parts = {}
            for _, e in pairs(drop_entities) do
                if e.name ~= "character" then
                    drop_parts[#drop_parts+1] = '{"name":"'..esc(e.name)..'","type":"'..esc(e.type)..'"}'
                end
            end

            -- Find entities at pickup position
            local pickup_entities = surface.find_entities_filtered{position={pickup_x, pickup_y}, radius=0.5}
            local pickup_parts = {}
            for _, e in pairs(pickup_entities) do
                if e.name ~= "character" then
                    pickup_parts[#pickup_parts+1] = '{"name":"'..esc(e.name)..'","type":"'..esc(e.type)..'"}'
                end
            end

            -- Check if an inserter can actually be placed here
            local can_place = surface.can_place_entity{name="burner-inserter", position={ix, iy}, force=game.connected_players[1].force, direction=dir}

            rcon.print('{"success":true'..
                ',"inserter_position":{"x":'..ix..',"y":'..iy..'}'..
                ',"direction":"{{direction}}"'..
                ',"pickup":{"x":'..pickup_x..',"y":'..pickup_y..',"entities":['..table.concat(pickup_parts, ",")..']}'..
                ',"drop":{"x":'..drop_x..',"y":'..drop_y..',"entities":['..table.concat(drop_parts, ",")..']}'..
                ',"can_place":'..tostring(can_place)..
                '}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Place an inserter adjacent to a target entity on a specified side with a specified flow direction.
    /// Calculates the correct inserter position and facing based on the target entity's bounding box.
    /// For inbound: inserter drops INTO the target. For outbound: inserter picks FROM the target.
    /// </summary>
    public Task<string> PlaceInserterAsync(
        string inserterName,
        double targetX,
        double targetY,
        string side,
        bool inbound,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inserterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(side);

        // inbound = inserter drops into target (faces toward target)
        // outbound = inserter picks from target (faces away from target)
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
            local player = game.connected_players[1]
            local surface = player.surface
            local target_entities = surface.find_entities_filtered{position={{{targetX}}, {{targetY}}}, radius=1}
            sort_entities(target_entities)
            local target = nil
            for _, e in pairs(target_entities) do
                if e.type ~= "resource" then target = e break end
            end
            if not target then
                rcon.print('{"success":false,"error":"no_target_entity","x":{{targetX}},"y":{{targetY}}}')
                return
            end

            -- Get target entity bounding box to calculate edge positions
            local bb = target.bounding_box
            local cx, cy = target.position.x, target.position.y

            -- Calculate inserter position based on side (just outside the entity bounding box)
            local side = "{{side}}"
            local inserter_x, inserter_y
            if side == "north" then
                inserter_x = math.floor(cx) + 0.5
                inserter_y = math.floor(bb.left_top.y) - 0.5
            elseif side == "south" then
                inserter_x = math.floor(cx) + 0.5
                inserter_y = math.floor(bb.right_bottom.y) + 0.5
            elseif side == "east" then
                inserter_x = math.floor(bb.right_bottom.x) + 0.5
                inserter_y = math.floor(cy) + 0.5
            elseif side == "west" then
                inserter_x = math.floor(bb.left_top.x) - 0.5
                inserter_y = math.floor(cy) + 0.5
            else
                rcon.print('{"success":false,"error":"invalid_side","side":"'..side..'","valid_sides":"north, south, east, west"}')
                return
            end

            -- Snap to tile center
            inserter_x = math.floor(inserter_x) + 0.5
            inserter_y = math.floor(inserter_y) + 0.5

            -- Calculate inserter direction
            -- In Factorio, direction = PICKUP side (arm extends toward facing direction)
            -- inbound = drop INTO target = face AWAY from target (pick from outside, drop into target)
            -- outbound = pick FROM target = face TOWARD target (pick from target, drop outside)
            local inbound = {{(inbound ? "true" : "false")}}
            local dir
            if side == "north" then
                dir = inbound and defines.direction.north or defines.direction.south
            elseif side == "south" then
                dir = inbound and defines.direction.south or defines.direction.north
            elseif side == "east" then
                dir = inbound and defines.direction.east or defines.direction.west
            elseif side == "west" then
                dir = inbound and defines.direction.west or defines.direction.east
            end

            -- Check distance
            local player_pos = player.position
            local dx = inserter_x - player_pos.x
            local dy = inserter_y - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            if distance > player.build_distance then
                rcon.print('{"success":false,"error":"out_of_range","distance":'..string.format("%.1f", distance)..',"limit":'..player.build_distance..'}')
                return
            end

            -- Check placement validity
            if not surface.can_place_entity{name="{{inserterName}}", position={inserter_x, inserter_y}, force=player.force, direction=dir} then
                rcon.print('{"success":false,"error":"invalid_position","inserter":"{{inserterName}}","x":'..inserter_x..',"y":'..inserter_y..'}')
                return
            end

            -- Check inventory
            if player.get_item_count("{{inserterName}}") < 1 then
                rcon.print('{"success":false,"error":"missing_item","item":"{{inserterName}}"}')
                return
            end

            -- Place the inserter
            player.remove_item{name="{{inserterName}}", count=1}
            surface.create_entity{name="{{inserterName}}", position={inserter_x, inserter_y}, force=player.force, player=player, direction=dir}

            local dir_names = {}
            for k, v in pairs(defines.direction) do dir_names[v] = k end
            local dir_name = dir_names[dir] or "unknown"

            rcon.print('{"success":true,"inserter":"{{inserterName}}","x":'..inserter_x..',"y":'..inserter_y..
                ',"direction":"'..dir_name..'"'..
                ',"target":"'..esc(target.name)..'"'..
                ',"side":"'..side..'"'..
                ',"flow":"'..(inbound and "inbound" or "outbound")..'"'..
                '}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Automatically place an inserter between two adjacent entities.
    /// Detects the gap, determines correct position and orientation so items flow from source to destination.
    /// </summary>
    public Task<string> InsertBetweenAsync(
        string inserterName,
        double sourceX,
        double sourceY,
        double destX,
        double destY,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inserterName);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
            local player = game.connected_players[1]
            local surface = player.surface

            -- Find source entity
            local src_entities = surface.find_entities_filtered{position={{{sourceX}}, {{sourceY}}}, radius=1}
            sort_entities(src_entities)
            local source = nil
            for _, e in pairs(src_entities) do
                if e.type ~= "resource" then source = e break end
            end
            if not source then
                rcon.print('{"success":false,"error":"no_source_entity","x":{{sourceX}},"y":{{sourceY}}}')
                return
            end

            -- Find destination entity
            local dst_entities = surface.find_entities_filtered{position={{{destX}}, {{destY}}}, radius=1}
            sort_entities(dst_entities)
            local dest = nil
            for _, e in pairs(dst_entities) do
                if e.type ~= "resource" then dest = e break end
            end
            if not dest then
                rcon.print('{"success":false,"error":"no_dest_entity","x":{{destX}},"y":{{destY}}}')
                return
            end

            -- Calculate midpoint between entities for inserter placement
            local sx, sy = source.position.x, source.position.y
            local dx, dy = dest.position.x, dest.position.y
            local mid_x = (sx + dx) / 2
            local mid_y = (sy + dy) / 2

            -- Snap to tile center
            local inserter_x = math.floor(mid_x) + 0.5
            local inserter_y = math.floor(mid_y) + 0.5

            -- Determine direction: inserter faces toward source (pickup side)
            -- In Factorio, direction = where the arm reaches to PICK UP items
            local diff_x = sx - dx
            local diff_y = sy - dy
            local dir
            if math.abs(diff_x) > math.abs(diff_y) then
                dir = diff_x > 0 and defines.direction.east or defines.direction.west
            else
                dir = diff_y > 0 and defines.direction.south or defines.direction.north
            end

            -- Check distance
            local player_pos = player.position
            local pdx = inserter_x - player_pos.x
            local pdy = inserter_y - player_pos.y
            local distance = math.sqrt(pdx*pdx + pdy*pdy)
            if distance > player.build_distance then
                rcon.print('{"success":false,"error":"out_of_range","distance":'..string.format("%.1f", distance)..',"limit":'..player.build_distance..'}')
                return
            end

            -- Check placement validity
            if not surface.can_place_entity{name="{{inserterName}}", position={inserter_x, inserter_y}, force=player.force, direction=dir} then
                rcon.print('{"success":false,"error":"invalid_position","inserter":"{{inserterName}}","x":'..inserter_x..',"y":'..inserter_y..
                    ',"message":"Cannot place inserter at midpoint. Entities may not be adjacent or the position is blocked."}')
                return
            end

            -- Check inventory
            if player.get_item_count("{{inserterName}}") < 1 then
                rcon.print('{"success":false,"error":"missing_item","item":"{{inserterName}}"}')
                return
            end

            -- Place the inserter
            player.remove_item{name="{{inserterName}}", count=1}
            surface.create_entity{name="{{inserterName}}", position={inserter_x, inserter_y}, force=player.force, player=player, direction=dir}

            local dir_names = {}
            for k, v in pairs(defines.direction) do dir_names[v] = k end
            local dir_name = dir_names[dir] or "unknown"

            rcon.print('{"success":true,"inserter":"{{inserterName}}","x":'..inserter_x..',"y":'..inserter_y..
                ',"direction":"'..dir_name..'"'..
                ',"source":"'..esc(source.name)..'","source_x":'..sx..',"source_y":'..sy..
                ',"dest":"'..esc(dest.name)..'","dest_x":'..dx..',"dest_y":'..dy..
                '}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
