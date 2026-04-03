using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
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
}
