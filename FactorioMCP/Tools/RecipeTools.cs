using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for querying recipes and technology details.
/// Helps the AI plan crafting chains and research priorities.
/// </summary>
[McpServerToolType]
internal sealed class RecipeTools(FactorioService factorio)
{
    [McpServerTool, Description(
        "Get details about a specific recipe — ingredients, products, crafting time, and category. " +
        "Use this to understand what materials are needed to craft an item and what it produces.")]
    public Task<string> GetRecipeDetails(
        [Description("Recipe name to look up (e.g. 'iron-gear-wheel', 'electronic-circuit', 'transport-belt')")]
        string recipe,
        CancellationToken cancellationToken = default)
    {
        return factorio.GetRecipeDetailsAsync(recipe, cancellationToken);
    }

    [McpServerTool, Description(
        "Get all recipes currently available (unlocked) for crafting. " +
        "Returns each recipe's name, crafting category, and crafting time. " +
        "Use this to see what the player can currently craft.")]
    public Task<string> GetAvailableRecipes(CancellationToken cancellationToken = default)
    {
        return factorio.GetAvailableRecipesAsync(cancellationToken);
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
        return factorio.GetTechnologyDetailsAsync(technology, cancellationToken);
    }
}
