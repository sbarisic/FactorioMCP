using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Resources;

/// <summary>
/// MCP Resources exposing read-only Factorio game state.
/// These are passive context the AI can read without making tool calls.
/// </summary>
[McpServerResourceType]
internal sealed class GameStateResources(FactorioService factorioService, EnergyService energyService)
{
    [McpServerResource(UriTemplate = "factorio://player/position", Name = "Player Position", MimeType = "application/json")]
    [Description("Current player map coordinates")]
    public async Task<string> GetPlayerPosition() =>
        await factorioService.GetPlayerPositionAsync();

    [McpServerResource(UriTemplate = "factorio://player/inventory", Name = "Player Inventory", MimeType = "application/json")]
    [Description("All items and counts in the player's main inventory")]
    public async Task<string> GetPlayerInventory() =>
        await factorioService.GetInventoryAsync();

    [McpServerResource(UriTemplate = "factorio://player/crafting-queue", Name = "Crafting Queue", MimeType = "application/json")]
    [Description("Current crafting queue contents and progress")]
    public async Task<string> GetCraftingQueue() =>
        await factorioService.GetCraftingQueueAsync();

    [McpServerResource(UriTemplate = "factorio://research/status", Name = "Research Status", MimeType = "application/json")]
    [Description("Current research technology and progress percentage")]
    public async Task<string> GetResearchStatus() =>
        await factorioService.GetResearchStatusAsync();

    [McpServerResource(UriTemplate = "factorio://research/available", Name = "Available Technologies", MimeType = "application/json")]
    [Description("Technologies available for research with prerequisites met")]
    public async Task<string> GetAvailableTechnologies() =>
        await factorioService.GetAvailableTechnologiesAsync();

    [McpServerResource(UriTemplate = "factorio://recipes/available", Name = "Available Recipes", MimeType = "application/json")]
    [Description("All currently unlocked recipes with category and crafting energy")]
    public async Task<string> GetAvailableRecipes() =>
        await factorioService.GetAvailableRecipesAsync();

    [McpServerResource(UriTemplate = "factorio://energy/network", Name = "Electric Network", MimeType = "application/json")]
    [Description("Electric network statistics: production, consumption, satisfaction, and accumulator charge levels")]
    public async Task<string> GetElectricNetwork() =>
        await energyService.GetElectricNetworkAsync();

    [McpServerResource(UriTemplate = "factorio://game/tick", Name = "Game Tick", MimeType = "application/json")]
    [Description("Current game tick number for timing and coordination")]
    public async Task<string> GetGameTick() =>
        await factorioService.GetGameTickAsync();
}
