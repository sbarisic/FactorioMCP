using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for querying recipes and technology details.
/// Helps the AI plan crafting chains and research priorities.
/// </summary>
[McpServerToolType]
internal sealed class RecipeTools(FactorioService factorio, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Get details about a specific recipe — ingredients, products, crafting time, and category. " +
        "Use this to understand what materials are needed to craft an item and what it produces.")]
    public Task<string> GetRecipeDetails(
        [Description("Recipe name to look up (e.g. 'iron-gear-wheel', 'electronic-circuit', 'transport-belt')")]
        string recipe,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetRecipeDetails), ct => factorio.GetRecipeDetailsAsync(recipe, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Get all recipes currently available (unlocked) for crafting. " +
        "Returns each recipe's name, crafting category, and crafting time. " +
        "Use this to see what the player can currently craft.")]
    public Task<string> GetAvailableRecipes(CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetAvailableRecipes), factorio.GetAvailableRecipesAsync, cancellationToken);
    }

    [McpServerTool, Description(
        "Get details about a specific technology — prerequisites, effects (recipe unlocks), " +
        "research cost, and required science packs. Use this to understand what a technology " +
        "unlocks and what is needed to research it.")]
    public Task<string> GetTechnologyDetails(
        [Description("Technology name to look up (e.g. 'automation', 'logistics', 'steel-processing')")]
        string technology,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetTechnologyDetails), ct => factorio.GetTechnologyDetailsAsync(technology, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Check whether a recipe can be crafted with the player's current inventory. " +
        "Returns whether crafting is possible, the maximum craftable count, and a per-ingredient " +
        "breakdown showing how many are needed, available, and missing. " +
        "Use this before crafting to verify you have the materials, or to plan resource gathering.")]
    public Task<string> CheckCraftFeasibility(
        [Description("Recipe name to check (e.g. 'iron-gear-wheel', 'electronic-circuit', 'transport-belt')")]
        string recipe,
        [Description("Number of items you want to craft (default 1)")]
        int count = 1,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(CheckCraftFeasibility), ct => factorio.CheckCraftFeasibilityAsync(recipe, count, ct), cancellationToken);
    }
}
