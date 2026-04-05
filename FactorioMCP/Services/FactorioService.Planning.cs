using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
    /// <summary>
    /// Plan a craft by recursively expanding the recipe tree for an item.
    /// Returns the full dependency tree showing all intermediates and raw materials needed,
    /// with counts. Uses the Factorio recipe data to avoid LLM hallucinations.
    /// </summary>
    public Task<string> PlanCraftAsync(string itemName, int count = 1, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var escapedItemName = itemName.Replace("\\", "\\\\").Replace("\"", "\\\"");

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            {{LuaJsonEscape}}
            local player = game.connected_players[1]
            local force = player.force
            local target_item = "{{escapedItemName}}"
            local target_count = {{count}}

            -- Build a lookup of enabled recipes by product name
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

            -- Recursive function to build the craft tree
            local function plan(item, need, depth)
                if depth > 10 then
                    return '{"item":"'..esc(item)..'","count":'..need..',"type":"max_depth"}'
                end
                local recipe = recipe_for[item]
                if not recipe then
                    return '{"item":"'..esc(item)..'","count":'..need..',"type":"raw"}'
                end
                -- Find how many this recipe produces
                local produced = 1
                for _, prod in pairs(recipe.products) do
                    if prod.name == item then
                        produced = prod.amount or 1
                        break
                    end
                end
                local batches = math.ceil(need / produced)
                local ingredient_parts = {}
                for _, ing in pairs(recipe.ingredients) do
                    local ing_need = ing.amount * batches
                    ingredient_parts[#ingredient_parts+1] = plan(ing.name, ing_need, depth + 1)
                end
                return '{"item":"'..esc(item)..'","count":'..need..',"type":"craft","recipe":"'..esc(recipe.name)..'","batches":'..batches..',"produces_per_batch":'..produced..',"ingredients":['..table.concat(ingredient_parts, ",")..']}'
            end

            -- Collect raw totals
            local raw_totals = {}
            local function collect_raws(item, need, depth)
                if depth > 10 then return end
                local recipe = recipe_for[item]
                if not recipe then
                    raw_totals[item] = (raw_totals[item] or 0) + need
                    return
                end
                local produced = 1
                for _, prod in pairs(recipe.products) do
                    if prod.name == item then
                        produced = prod.amount or 1
                        break
                    end
                end
                local batches = math.ceil(need / produced)
                for _, ing in pairs(recipe.ingredients) do
                    collect_raws(ing.name, ing.amount * batches, depth + 1)
                end
            end

            local tree = plan(target_item, target_count, 0)
            collect_raws(target_item, target_count, 0)

            local raw_parts = {}
            for name, amt in pairs(raw_totals) do
                raw_parts[#raw_parts+1] = '{"item":"'..esc(name)..'","count":'..amt..'}'
            end

            -- Check player inventory
            local player_has = player.get_item_count(target_item)

            rcon.print('{"success":true,"item":"'..esc(target_item)..'","requested":'..target_count..',"player_has":'..player_has..',"recipe_tree":'..tree..',"raw_materials":['..table.concat(raw_parts, ",")..']}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
