using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
    /// <summary>
    /// Find entities that have no power or are experiencing low power within a given radius.
    /// Returns a list of entities with their status, type, and position.
    /// </summary>
    public Task<string> FindUnpoweredEntitiesAsync(double radius = 50, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{
                position=player.position, radius={{radius}}
            }
            local status_names = {}
            for k, v in pairs(defines.entity_status) do status_names[v] = k end
            local parts = {}
            for _, e in pairs(entities) do
                if e.status == defines.entity_status.no_power
                    or e.status == defines.entity_status.low_power then
                    local status_name = status_names[e.status] or tostring(e.status)
                    parts[#parts+1] = '{"name":"'..esc(e.name)..'","type":"'..esc(e.type)..'","status":"'..status_name..'","x":'..string.format("%.1f", e.position.x)..',"y":'..string.format("%.1f", e.position.y)..'}'
                end
            end
            rcon.print('{"count":'..#parts..',"entities":['..table.concat(parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Find machines that are idle (not working) within a given radius.
    /// Returns machines grouped by idle reason (no fuel, no ingredients, no power, etc.).
    /// </summary>
    public Task<string> FindIdleMachinesAsync(double radius = 50, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{
                position=player.position, radius={{radius}}
            }
            local status_names = {}
            for k, v in pairs(defines.entity_status) do status_names[v] = k end
            local working_statuses = {
                [defines.entity_status.working] = true,
                [defines.entity_status.normal] = true
            }
            local parts = {}
            for _, e in pairs(entities) do
                if e.status ~= nil and not working_statuses[e.status]
                    and e.type ~= "resource" and e.name ~= "character"
                    and e.type ~= "item-entity" and e.type ~= "transport-belt"
                    and e.type ~= "underground-belt" and e.type ~= "splitter"
                    and e.type ~= "electric-pole" and e.type ~= "pipe"
                    and e.type ~= "pipe-to-ground" and e.type ~= "wall"
                    and e.type ~= "gate" and e.type ~= "straight-rail"
                    and e.type ~= "curved-rail" then
                    local status_name = status_names[e.status] or tostring(e.status)
                    parts[#parts+1] = '{"name":"'..esc(e.name)..'","type":"'..esc(e.type)..'","status":"'..status_name..'","x":'..string.format("%.1f", e.position.x)..',"y":'..string.format("%.1f", e.position.y)..'}'
                end
            end
            rcon.print('{"count":'..#parts..',"entities":['..table.concat(parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Check which input items a furnace or assembler at a given position is missing.
    /// For furnaces, checks fuel and source slots. For assemblers, checks input slots against recipe ingredients.
    /// </summary>
    public Task<string> FindMissingInputsAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            {{LuaEntitySort}}
            local player = game.connected_players[1]
            local entities = player.surface.find_entities_filtered{position={{{x}},{{y}}}, radius=1}
            sort_entities(entities, {{x}}, {{y}})
            local e = nil
            for _, ent in pairs(entities) do
                if ent.type ~= "resource" then e = ent break end
            end
            if not e then
                rcon.print('{"success":false,"error":"no_entity","x":{{x}},"y":{{y}}}')
                return
            end
            local status_names = {}
            for k, v in pairs(defines.entity_status) do status_names[v] = k end
            local status_name = e.status and status_names[e.status] or "unknown"
            local missing = {}
            -- Check fuel
            local fuel_inv = e.get_inventory(defines.inventory.fuel)
            if fuel_inv and #fuel_inv > 0 then
                local has_fuel = false
                for i = 1, #fuel_inv do
                    local s = fuel_inv[i]
                    if s.valid_for_read then has_fuel = true break end
                end
                if not has_fuel then
                    missing[#missing+1] = '{"slot":"fuel","issue":"empty"}'
                end
            end
            -- Check furnace source
            local src_inv = e.get_inventory(defines.inventory.furnace_source)
            if src_inv and #src_inv > 0 then
                local has_source = false
                for i = 1, #src_inv do
                    local s = src_inv[i]
                    if s.valid_for_read then has_source = true break end
                end
                if not has_source then
                    missing[#missing+1] = '{"slot":"furnace_source","issue":"empty"}'
                end
            end
            -- Check assembler inputs against recipe
            local recipe = e.get_recipe()
            if recipe then
                local input_inv = e.get_inventory(defines.inventory.assembling_machine_input)
                if input_inv then
                    for _, ingredient in pairs(recipe.ingredients) do
                        local have = input_inv.get_item_count(ingredient.name)
                        if have < ingredient.amount then
                            missing[#missing+1] = '{"slot":"assembling_machine_input","item":"'..esc(ingredient.name)..'","have":'..have..',"need":'..ingredient.amount..',"issue":"insufficient"}'
                        end
                    end
                end
            end
            -- Check output full
            local out_inv = e.get_inventory(defines.inventory.furnace_result) or e.get_inventory(defines.inventory.assembling_machine_output)
            if out_inv then
                local all_full = true
                for i = 1, #out_inv do
                    local s = out_inv[i]
                    if not s.valid_for_read or s.count < s.prototype.stack_size then
                        all_full = false
                        break
                    end
                end
                if all_full and #out_inv > 0 then
                    missing[#missing+1] = '{"slot":"output","issue":"full"}'
                end
            end
            rcon.print('{"success":true,"entity":"'..esc(e.name)..'","type":"'..esc(e.type)..'","status":"'..status_name..'","x":'..string.format("%.1f", e.position.x)..',"y":'..string.format("%.1f", e.position.y)..',"missing":['..table.concat(missing, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
