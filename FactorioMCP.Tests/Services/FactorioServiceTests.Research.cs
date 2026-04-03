using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    // ── GetResearchStatus ────────────────────────────────────────────

    [Fact]
    public async Task GetResearchStatusAsync_QueriesCurrentResearch()
    {
        await _service.GetResearchStatusAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
        Assert.Contains("current_research", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetResearchStatusAsync_OutputsJsonWhenResearching()
    {
        await _service.GetResearchStatusAsync();

        Assert.Contains("\"researching\":true", _rcon.LastCommand!);
        Assert.Contains("\"technology\":\"", _rcon.LastCommand!);
        Assert.Contains("\"progress\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetResearchStatusAsync_OutputsJsonWhenNoResearch()
    {
        await _service.GetResearchStatusAsync();

        Assert.Contains("\"researching\":false", _rcon.LastCommand!);
    }

    // ── GetAvailableTechnologies ─────────────────────────────────────

    [Fact]
    public async Task GetAvailableTechnologiesAsync_SendsSilentCommand()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_UsesForce()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("game.connected_players[1].force", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_IteratesTechnologies()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("force.technologies", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_ChecksPrerequisites()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("tech.prerequisites", _rcon.LastCommand!);
        Assert.Contains("prereq.researched", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_FiltersResearchedAndDisabled()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("not tech.researched", _rcon.LastCommand!);
        Assert.Contains("tech.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_IncludesCostAndIngredients()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("research_unit_count", _rcon.LastCommand!);
        Assert.Contains("research_unit_ingredients", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableTechnologiesAsync_OutputsJsonWithTechnologiesArray()
    {
        await _service.GetAvailableTechnologiesAsync();

        Assert.Contains("\"technologies\":[", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
    }

    // ── StartResearch ────────────────────────────────────────────────

    [Fact]
    public async Task StartResearchAsync_SendsSilentCommand()
    {
        await _service.StartResearchAsync("automation");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task StartResearchAsync_LookUpTechnologyByName()
    {
        await _service.StartResearchAsync("logistics");

        Assert.Contains("force.technologies[\"logistics\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_ValidatesTechnologyExists()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("\"error\":\"unknown_technology\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_ChecksAlreadyResearched()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("tech.researched", _rcon.LastCommand!);
        Assert.Contains("\"error\":\"already_researched\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_UsesAddResearch()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("force.add_research(tech)", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_UsesPcallForSafety()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("pcall", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_OutputsJsonSuccessResponse()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("\"success\":true", _rcon.LastCommand!);
        Assert.Contains("\"technology\":\"", _rcon.LastCommand!);
        Assert.Contains("\"cost\":", _rcon.LastCommand!);
        Assert.Contains("\"ingredients\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_OutputsJsonErrorOnFailure()
    {
        await _service.StartResearchAsync("automation");

        Assert.Contains("\"success\":false", _rcon.LastCommand!);
        Assert.Contains("\"error\":\"research_failed\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task StartResearchAsync_ThrowsOnNullTechnology()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.StartResearchAsync(null!));
    }

    [Fact]
    public async Task StartResearchAsync_ThrowsOnWhitespaceTechnology()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.StartResearchAsync("   "));
    }

    // ── GetRecipeDetails ─────────────────────────────────────────────

    [Fact]
    public async Task GetRecipeDetailsAsync_SendsSilentCommand()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_LooksUpRecipeByName()
    {
        await _service.GetRecipeDetailsAsync("electronic-circuit");

        Assert.Contains("force.recipes[\"electronic-circuit\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_ValidatesRecipeExists()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("\"error\":\"unknown_recipe\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_IncludesIngredientsAndProducts()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("recipe.ingredients", _rcon.LastCommand!);
        Assert.Contains("recipe.products", _rcon.LastCommand!);
        Assert.Contains("\"ingredients\":[", _rcon.LastCommand!);
        Assert.Contains("\"products\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_IncludesEnergyAndCategory()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("recipe.energy", _rcon.LastCommand!);
        Assert.Contains("recipe.category", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_IncludesEnabledStatus()
    {
        await _service.GetRecipeDetailsAsync("iron-gear-wheel");

        Assert.Contains("recipe.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_ThrowsOnNullRecipe()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetRecipeDetailsAsync(null!));
    }

    [Fact]
    public async Task GetRecipeDetailsAsync_ThrowsOnWhitespaceRecipe()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetRecipeDetailsAsync("   "));
    }

    // ── GetAvailableRecipes ──────────────────────────────────────────

    [Fact]
    public async Task GetAvailableRecipesAsync_SendsSilentCommand()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_FiltersEnabledRecipes()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("recipe.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_IteratesForceRecipes()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("force.recipes", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_OutputsJsonWithRecipesArray()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("\"recipes\":[", _rcon.LastCommand!);
        Assert.Contains("\"count\":", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetAvailableRecipesAsync_IncludesCategoryAndEnergy()
    {
        await _service.GetAvailableRecipesAsync();

        Assert.Contains("\"category\":\"", _rcon.LastCommand!);
        Assert.Contains("\"energy\":", _rcon.LastCommand!);
    }

    // ── GetTechnologyDetails ─────────────────────────────────────────

    [Fact]
    public async Task GetTechnologyDetailsAsync_SendsSilentCommand()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.NotNull(_rcon.LastCommand);
        Assert.StartsWith("/silent-command", _rcon.LastCommand);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_LooksUpTechnologyByName()
    {
        await _service.GetTechnologyDetailsAsync("logistics");

        Assert.Contains("force.technologies[\"logistics\"]", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_ValidatesTechnologyExists()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("\"error\":\"unknown_technology\"", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesPrerequisites()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("tech.prerequisites", _rcon.LastCommand!);
        Assert.Contains("\"prerequisites\":[", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesEffectsWithRecipeUnlocks()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("tech.effects", _rcon.LastCommand!);
        Assert.Contains("\"effects\":[", _rcon.LastCommand!);
        Assert.Contains("unlock-recipe", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesCostAndIngredients()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("research_unit_count", _rcon.LastCommand!);
        Assert.Contains("research_unit_ingredients", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_IncludesResearchedAndEnabledStatus()
    {
        await _service.GetTechnologyDetailsAsync("automation");

        Assert.Contains("tech.researched", _rcon.LastCommand!);
        Assert.Contains("tech.enabled", _rcon.LastCommand!);
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_ThrowsOnNullTechnology()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetTechnologyDetailsAsync(null!));
    }

    [Fact]
    public async Task GetTechnologyDetailsAsync_ThrowsOnWhitespaceTechnology()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetTechnologyDetailsAsync("   "));
    }
}
