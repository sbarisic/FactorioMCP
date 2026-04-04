using System.Globalization;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Service for blueprint and ghost entity operations via RCON Lua commands.
/// Supports placing ghost entities for planned construction, building blueprint strings,
/// scanning for existing ghosts, capturing areas as blueprints, and revoking ghosts.
/// </summary>
internal sealed class BlueprintService(RconClient rcon)
{
    /// <summary>
    /// Place a ghost entity at the specified position. Ghost entities are transparent
    /// construction plans that bots or players can fill in later. Does not require
    /// the item in inventory. Uses <c>create_entity{name="entity-ghost"}</c>.
    /// </summary>
    public Task<string> PlaceGhostEntityAsync(
        string entityName,
        double x,
        double y,
        string direction = "north",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = {{{x}}, {{y}}}
            local inner = "{{entityName}}"
            local dir = defines.direction.{{direction}}
            if not surface.can_place_entity{name="entity-ghost", position=pos, force=player.force, direction=dir, inner_name=inner} then
                rcon.print('{"success":false,"error":"invalid_position","entity":"'..inner..'","x":'..pos[1]..',"y":'..pos[2]..'}')
                return
            end
            local ghost = surface.create_entity{name="entity-ghost", inner_name=inner, position=pos, force=player.force, direction=dir}
            if ghost then
                rcon.print('{"success":true,"entity":"'..inner..'","x":'..string.format("%.1f", ghost.position.x)..',"y":'..string.format("%.1f", ghost.position.y)..',"ghost_name":"'..ghost.ghost_name..'"}')
            else
                rcon.print('{"success":false,"error":"create_failed","entity":"'..inner..'","x":'..pos[1]..',"y":'..pos[2]..'}')
            end
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Build a blueprint string at the given position. Imports the blueprint string into
    /// the player's cursor stack, builds it, then clears the cursor.
    /// Uses <c>import_stack</c> + <c>build_from_cursor</c> for RCON-compatible placement.
    /// </summary>
    public Task<string> PlaceBlueprintStringAsync(
        string blueprintString,
        double x,
        double y,
        string direction = "north",
        string buildMode = "normal",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintString);

        // Escape the blueprint string for Lua - it may contain special chars
        var escapedBlueprint = blueprintString.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local pos = {{{x}}, {{y}}}
            local dir = defines.direction.{{direction}}
            local build_mode = defines.build_mode.{{buildMode}}
            player.clear_cursor()
            local stack = player.cursor_stack
            if not stack then
                rcon.print('{"success":false,"error":"no_cursor_stack"}')
                return
            end
            stack.set_stack("blueprint")
            local import_result = stack.import_stack('{{escapedBlueprint}}')
            if import_result == 1 then
                player.clear_cursor()
                rcon.print('{"success":false,"error":"invalid_blueprint_string"}')
                return
            end
            player.build_from_cursor{position=pos, direction=dir, build_mode=build_mode}
            player.clear_cursor()
            rcon.print('{"success":true,"x":'..pos[1]..',"y":'..pos[2]..',"import_warnings":'..import_result..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Scan for ghost entities near a position. Returns all entity-ghost and tile-ghost
    /// entities within a radius. Uses <c>find_entities_filtered{type="entity-ghost"}</c>.
    /// </summary>
    public Task<string> GetGhostEntitiesAsync(
        double radius = 50,
        double? centerX = null,
        double? centerY = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        var posExpr = centerX.HasValue && centerY.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{{x={centerX.Value},y={centerY.Value}}}")
            : "player.position";

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local center = {{posExpr}}
            local ghosts = surface.find_entities_filtered{type="entity-ghost", position=center, radius={{radius}}}
            local parts = {}
            for _, g in ipairs(ghosts) do
                parts[#parts+1] = '{"ghost_name":"'..g.ghost_name..'","x":'..string.format("%.1f", g.position.x)..',"y":'..string.format("%.1f", g.position.y)..',"direction":"'..(g.direction or 0)..'"}'
            end
            rcon.print('{"ghosts":['..table.concat(parts, ",")..'],"count":'..#ghosts..',"center_x":'..string.format("%.1f", center.x)..',"center_y":'..string.format("%.1f", center.y)..',"radius":'..({{radius}})..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Capture entities in a rectangular area as a blueprint string.
    /// Creates a temporary blueprint in the player's cursor, captures entities,
    /// exports the string, then clears the cursor.
    /// </summary>
    public Task<string> CreateBlueprintFromAreaAsync(
        double x1,
        double y1,
        double x2,
        double y2,
        bool includeTiles = false,
        CancellationToken cancellationToken = default)
    {
        var tilesFlag = includeTiles ? "true" : "false";
        var areaExpr = string.Create(CultureInfo.InvariantCulture, $"{{{{{x1}, {y1}}}, {{{x2}, {y2}}}}}");

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local area = {{areaExpr}}
            player.clear_cursor()
            local stack = player.cursor_stack
            if not stack then
                rcon.print('{"success":false,"error":"no_cursor_stack"}')
                return
            end
            stack.set_stack("blueprint")
            local mapping = stack.create_blueprint{surface=surface, force=player.force, area=area, always_include_tiles={{tilesFlag}}}
            if not stack.is_blueprint_setup() then
                player.clear_cursor()
                rcon.print('{"success":false,"error":"no_entities_in_area","x1":{{x1}},"y1":{{y1}},"x2":{{x2}},"y2":{{y2}}}')
                return
            end
            local bp_string = stack.export_stack()
            local entity_count = stack.get_blueprint_entity_count()
            player.clear_cursor()
            rcon.print('{"success":true,"blueprint_string":"'..bp_string..'","entity_count":'..entity_count..',"x1":{{x1}},"y1":{{y1}},"x2":{{x2}},"y2":{{y2}}}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Remove/revoke ghost entities at a specific position. Finds and destroys
    /// entity-ghost entities at the given coordinates.
    /// </summary>
    public Task<string> RevokeGhostEntityAsync(
        double x,
        double y,
        double radius = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local surface = player.surface
            local ghosts = surface.find_entities_filtered{type="entity-ghost", position={{{x}}, {{y}}}, radius={{radius}}}
            if #ghosts == 0 then
                rcon.print('{"success":false,"error":"no_ghosts","x":{{x}},"y":{{y}},"radius":{{radius}}}')
                return
            end
            local removed = {}
            for _, g in ipairs(ghosts) do
                removed[#removed+1] = '{"ghost_name":"'..g.ghost_name..'","x":'..string.format("%.1f", g.position.x)..',"y":'..string.format("%.1f", g.position.y)..'}'
                g.destroy()
            end
            rcon.print('{"success":true,"removed":['..table.concat(removed, ",")..'],"count":'..#removed..'}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }
}
