using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Calculates production rates and machine counts for Factorio recipes.
/// Fetches recipe and machine prototype data via RCON, then computes
/// how many machines are needed to achieve a target items-per-second rate.
/// </summary>
internal sealed class RecipeRateCalculatorService(RconClient rcon)
{
    /// <summary>
    /// Calculate machine count and throughput for a recipe at a target production rate.
    /// Returns machines_needed, items_per_second_actual, inputs_per_second[], outputs_per_second[].
    /// </summary>
    public Task<string> CalculateProductionRateAsync(
        string recipe,
        double targetItemsPerSecond,
        string? machineType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetItemsPerSecond, 0);

        var escapedRecipe = recipe.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var machineClause = machineType != null
            ? $"local machine_override = \"{machineType.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""
            : "local machine_override = nil";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{FactorioService.LuaJsonEscape}}
            local player = game.connected_players[1]
            local force = player.force
            local r = force.recipes["{{escapedRecipe}}"]
            if not r then
                rcon.print('{"success":false,"error":"unknown_recipe","recipe":"{{escapedRecipe}}"}')
                return
            end
            local energy = r.energy
            local cat = r.category
            {{machineClause}}
            local machine_name = machine_override
            local crafting_speed = 1.0
            if machine_name then
                local proto = prototypes.entity[machine_name]
                if not proto then
                    rcon.print('{"success":false,"error":"unknown_machine","machine":"'..esc(machine_name)..'"}')
                    return
                end
                if not proto.get_crafting_speed then
                    rcon.print('{"success":false,"error":"not_a_crafting_machine","machine":"'..esc(machine_name)..'"}')
                    return
                end
                crafting_speed = proto.get_crafting_speed()
            else
                local defs = {}
                defs["crafting"] = "assembling-machine-1"
                defs["advanced-crafting"] = "assembling-machine-2"
                defs["crafting-with-fluid"] = "assembling-machine-2"
                defs["smelting"] = "stone-furnace"
                defs["chemistry"] = "chemical-plant"
                defs["oil-processing"] = "oil-refinery"
                defs["rocket-building"] = "rocket-silo"
                defs["metallurgy"] = "foundry"
                defs["organic"] = "biochamber"
                defs["electromagnetics"] = "electromagnetic-plant"
                defs["cryogenics"] = "cryogenic-plant"
                defs["recycling"] = "recycler"
                machine_name = defs[cat]
                if machine_name then
                    local mp = prototypes.entity[machine_name]
                    if mp and mp.get_crafting_speed then crafting_speed = mp.get_crafting_speed() end
                else
                    for _, proto in pairs(prototypes.get_entity_filtered{ {filter="crafting-machine"} }) do
                        if proto.crafting_categories and proto.crafting_categories[cat] then
                            machine_name = proto.name
                            if proto.get_crafting_speed then crafting_speed = proto.get_crafting_speed() end
                            break
                        end
                    end
                    if not machine_name then
                        rcon.print('{"success":false,"error":"no_machine_for_category","category":"'..esc(cat)..'"}')
                        return
                    end
                end
            end
            local eff_time = energy / crafting_speed
            local crafts_per_sec = 1.0 / eff_time
            local target_rate = {{targetItemsPerSecond}}
            local primary_per_craft = 0
            for _, p in pairs(r.products) do
                local amt = p.amount or ((p.amount_min + p.amount_max) / 2)
                local prob = p.probability or 1
                local per_craft = amt * prob
                if per_craft > primary_per_craft then primary_per_craft = per_craft end
            end
            if primary_per_craft <= 0 then primary_per_craft = 1 end
            local rate_per_machine = primary_per_craft * crafts_per_sec
            local machines_needed = math.max(1, math.ceil(target_rate / rate_per_machine))
            local actual_rate = machines_needed * rate_per_machine
            local inputs = {}
            for _, i in pairs(r.ingredients) do
                local total = i.amount * crafts_per_sec * machines_needed
                inputs[#inputs+1] = '{"name":"'..esc(i.name)..'","per_craft":'..i.amount..',"per_second_total":'..string.format("%.4f", total)..'}'
            end
            local outputs = {}
            for _, p in pairs(r.products) do
                local amt = p.amount or ((p.amount_min + p.amount_max) / 2)
                local prob = p.probability or 1
                local total = amt * prob * crafts_per_sec * machines_needed
                outputs[#outputs+1] = '{"name":"'..esc(p.name)..'","per_craft":'..string.format("%.4f", amt * prob)..',"per_second_total":'..string.format("%.4f", total)..'}'
            end
            local fmt = string.format
            rcon.print('{"success":true,"recipe":"'..esc(r.name)..'","machine":"'..esc(machine_name)..'","crafting_speed":'..fmt("%.2f", crafting_speed)..',"base_crafting_time":'..fmt("%.2f", energy)..',"effective_crafting_time":'..fmt("%.4f", eff_time)..',"target_items_per_second":'..fmt("%.4f", target_rate)..',"machines_needed":'..machines_needed..',"items_per_second_actual":'..fmt("%.4f", actual_rate)..',"inputs_per_second":['..table.concat(inputs, ",")..'],"outputs_per_second":['..table.concat(outputs, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
