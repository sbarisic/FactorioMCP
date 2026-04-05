using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public partial class FactorioServiceTests
{
    [Fact]
    public async Task ResearchCommands_GenerateCorrectLua()
    {
        // GetResearchStatusAsync
        await _service.GetResearchStatusAsync();
        var statusCmd = _rcon.LastCommand!;
        Assert.Contains("current_research", statusCmd);
        Assert.Contains("\"researching\":true", statusCmd);
        Assert.Contains("\"progress\":", statusCmd);

        // GetAvailableTechnologiesAsync
        await _service.GetAvailableTechnologiesAsync();
        var availCmd = _rcon.LastCommand!;
        Assert.Contains("force.technologies", availCmd);
        Assert.Contains("not tech.researched", availCmd);
        Assert.Contains("tech.enabled", availCmd);
        Assert.Contains("tech.prerequisites", availCmd);
        Assert.Contains("research_unit_count", availCmd);

        // StartResearchAsync
        await _service.StartResearchAsync("logistics");
        var startCmd = _rcon.LastCommand!;
        Assert.Contains("force.technologies[\"logistics\"]", startCmd);
        Assert.Contains("force.add_research(tech)", startCmd);
        Assert.Contains("pcall", startCmd);
        Assert.Contains("\"error\":\"unknown_technology\"", startCmd);
        Assert.Contains("\"error\":\"already_researched\"", startCmd);
    }

    [Fact]
    public async Task RecipeCommands_GenerateCorrectLua()
    {
        // GetRecipeDetailsAsync
        await _service.GetRecipeDetailsAsync("electronic-circuit");
        var detailCmd = _rcon.LastCommand!;
        Assert.Contains("force.recipes[\"electronic-circuit\"]", detailCmd);
        Assert.Contains("recipe.ingredients", detailCmd);
        Assert.Contains("recipe.products", detailCmd);
        Assert.Contains("recipe.energy", detailCmd);
        Assert.Contains("recipe.category", detailCmd);

        // GetAvailableRecipesAsync
        await _service.GetAvailableRecipesAsync();
        var availCmd = _rcon.LastCommand!;
        Assert.Contains("force.recipes", availCmd);
        Assert.Contains("recipe.enabled", availCmd);
        Assert.Contains("\"recipes\":[", availCmd);

        // GetTechnologyDetailsAsync
        await _service.GetTechnologyDetailsAsync("automation");
        var techCmd = _rcon.LastCommand!;
        Assert.Contains("force.technologies[\"automation\"]", techCmd);
        Assert.Contains("tech.prerequisites", techCmd);
        Assert.Contains("tech.prototype.effects", techCmd);
        Assert.Contains("research_unit_count", techCmd);
        Assert.Contains("tech.researched", techCmd);
    }

    [Theory]
    [InlineData("StartResearch_NullTech", null, "")]
    [InlineData("StartResearch_WhitespaceTech", "   ", "")]
    [InlineData("GetRecipeDetails_NullRecipe", "", null)]
    [InlineData("GetRecipeDetails_WhitespaceRecipe", "", "   ")]
    public async Task ResearchValidation_ThrowsOnInvalidInput(
        string scenario, string? techName, string? recipeName)
    {
        if (scenario.StartsWith("StartResearch"))
        {
            if (techName == null)
                await Assert.ThrowsAsync<ArgumentNullException>(() => _service.StartResearchAsync(null!));
            else
                await Assert.ThrowsAsync<ArgumentException>(() => _service.StartResearchAsync(techName));
        }
        else
        {
            if (recipeName == null)
                await Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetRecipeDetailsAsync(null!));
            else
                await Assert.ThrowsAsync<ArgumentException>(() => _service.GetRecipeDetailsAsync(recipeName));
        }
    }
}
