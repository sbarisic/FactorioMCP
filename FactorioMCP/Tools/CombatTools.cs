using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for combat situational awareness: scanning enemies and inspecting player defences.
/// All queries run on the player's current surface.
/// </summary>
[McpServerToolType]
internal sealed class CombatTools(CombatService combat, GameCommandQueue queue)
{
    [McpServerTool, Description(
        "Scan for enemy units (biters, spitters), spawners (biter/spitter nests), and worms " +
        "within a radius of the player. Returns individual positions grouped by category " +
        "and the nearest military enemy. Use this to assess danger before expanding, " +
        "or to plan a counter-attack route.")]
    public Task<string> ScanEnemies(
        [Description("Search radius in tiles. Default 100.")]
        double radius = 100,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(ScanEnemies), ct => combat.ScanEnemiesAsync(radius, ct), cancellationToken);
    }

    [McpServerTool, Description(
        "Find all player-owned turrets within a radius and report their status. " +
        "Returns turret type, position, current ammo count, total kills, and shooting target. " +
        "Use this to audit defence coverage, identify turrets that need restocking, " +
        "and check whether any turret is actively engaged.")]
    public Task<string> GetDefenses(
        [Description("Search radius in tiles around the player. Default 80.")]
        double radius = 80,
        CancellationToken cancellationToken = default)
    {
        return queue.ExecuteAsync(nameof(GetDefenses), ct => combat.GetDefensesAsync(radius, ct), cancellationToken);
    }
}
