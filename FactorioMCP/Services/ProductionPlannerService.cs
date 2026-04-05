using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Orchestrates production planning by expanding recipe trees, calculating machine counts
/// at target rates, and locating resource patches — all in a single RCON call.
/// Returns a structured ProductionPlan JSON without placing anything.
/// </summary>
internal sealed class ProductionPlannerService(RconClient rcon)
{
    /// <summary>
    /// Plan a full production chain for a target item at a target rate.
    /// Expands the recipe tree, calculates machine counts per stage,
    /// identifies belt tiers needed, and locates resource patches for raw inputs.
    /// </summary>
    public Task<string> PlanProductionAsync(
        string targetItem,
        double targetRatePerSecond,
        string? machineOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetItem);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetRatePerSecond, 0);

        var escapedItem = targetItem.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var machineClause = machineOverride != null
            ? $"local machine_override = \"{machineOverride.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""
            : "local machine_override = nil";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local force = player.force
            local surface = player.surface
            local ppos = player.position
            local target_item = "{{escapedItem}}"
            local target_rate = {{targetRatePerSecond}}
            {{machineClause}}

            local recipe_for = {}
            for name, recipe in pairs(force.recipes) do
                if recipe.enabled then
                    for _, product in pairs(recipe.products) do
                        if not recipe_for[product.name] then
                            recipe_for[product.name] = recipe
                        end
                    end
                end
            end

            local raw_items = {}
            raw_items["iron-ore"] = true
            raw_items["copper-ore"] = true
            raw_items["stone"] = true
            raw_items["coal"] = true
            raw_items["wood"] = true
            raw_items["crude-oil"] = true
            raw_items["water"] = true
            raw_items["uranium-ore"] = true

            local machine_defs = {}
            machine_defs["crafting"] = "assembling-machine-1"
            machine_defs["advanced-crafting"] = "assembling-machine-2"
            machine_defs["crafting-with-fluid"] = "assembling-machine-2"
            machine_defs["smelting"] = "stone-furnace"
            machine_defs["chemistry"] = "chemical-plant"
            machine_defs["oil-processing"] = "oil-refinery"
            machine_defs["metallurgy"] = "foundry"
            machine_defs["recycling"] = "recycler"

            local function belt_for_rate(rate)
                if rate <= 15 then return "transport-belt"
                elseif rate <= 30 then return "fast-transport-belt"
                elseif rate <= 45 then return "express-transport-belt"
                else return "turbo-transport-belt" end
            end

            local function get_machine(cat)
                if machine_override then return machine_override end
                local m = machine_defs[cat]
                if m then return m end
                return "assembling-machine-1"
            end

            local function get_speed(machine_name)
                local proto = prototypes.entity[machine_name]
                if proto and proto.crafting_speed then return proto.crafting_speed end
                return 1.0
            end

            local stages = {}
            local raw_needs = {}
            local visited = {}

            local function expand(item_name, needed_rate)
                if visited[item_name] then return end
                if raw_items[item_name] then
                    raw_needs[item_name] = (raw_needs[item_name] or 0) + needed_rate
                    return
                end
                local recipe = recipe_for[item_name]
                if not recipe then
                    raw_needs[item_name] = (raw_needs[item_name] or 0) + needed_rate
                    return
                end
                visited[item_name] = true
                local energy = recipe.energy
                local cat = recipe.category
                local machine_name = get_machine(cat)
                local speed = get_speed(machine_name)
                local eff_time = energy / speed
                local crafts_per_sec = 1.0 / eff_time

                local output_per_craft = 1
                for _, p in pairs(recipe.products) do
                    if p.name == item_name then
                        local amt = p.amount or ((p.amount_min + p.amount_max) / 2)
                        local prob = p.probability or 1
                        output_per_craft = amt * prob
                        break
                    end
                end

                local rate_per_machine = output_per_craft * crafts_per_sec
                local machines = math.max(1, math.ceil(needed_rate / rate_per_machine))
                local actual_rate = machines * rate_per_machine

                local inputs_json = {}
                for _, ing in pairs(recipe.ingredients) do
                    local ing_rate = ing.amount * crafts_per_sec * machines
                    inputs_json[#inputs_json+1] = '{"name":"'..esc(ing.name)..'","per_second":'..string.format("%.4f", ing_rate)..'}'
                    expand(ing.name, ing_rate)
                end

                local belt = belt_for_rate(actual_rate)
                stages[#stages+1] = '{"output_item":"'..esc(item_name)..'","recipe":"'..esc(recipe.name)..'","machine_type":"'..esc(machine_name)..'","crafting_speed":'..string.format("%.2f", speed)..',"machine_count":'..machines..',"output_rate":'..string.format("%.4f", actual_rate)..',"belt_tier":"'..belt..'","inputs":['..table.concat(inputs_json, ",")..']}'
            end

            expand(target_item, target_rate)

            local resource_json = {}
            for item, rate in pairs(raw_needs) do
                local belt = belt_for_rate(rate)
                local patch_info = "null"
                local ore_name = item
                if item == "iron-plate" then ore_name = "iron-ore" end
                if item == "copper-plate" then ore_name = "copper-ore" end
                local resources = surface.find_entities_filtered{name=ore_name, position=ppos, radius=200, limit=1}
                if #resources > 0 then
                    local res = resources[1]
                    patch_info = '{"resource":"'..esc(ore_name)..'","x":'..string.format("%.1f", res.position.x)..',"y":'..string.format("%.1f", res.position.y)..'}'
                end
                resource_json[#resource_json+1] = '{"item":"'..esc(item)..'","per_second":'..string.format("%.4f", rate)..',"belt_tier":"'..belt..'","nearest_patch":'..patch_info..'}'
            end

            local fmt = string.format
            rcon.print('{"success":true,"target_item":"'..esc(target_item)..'","target_rate":'..fmt("%.4f", target_rate)..',"stage_count":'..#stages..',"stages":['..table.concat(stages, ",")..'],"raw_materials":['..table.concat(resource_json, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
