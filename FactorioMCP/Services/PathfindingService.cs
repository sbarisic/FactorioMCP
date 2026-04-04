using System.Globalization;
using System.Text.Json;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Centralized pathfinding service that uses Factorio's built-in A* pathfinder
/// (<c>surface.request_path</c>) to navigate the player around obstacles.
/// Replaces the old "walk straight, detect stuck, try perpendicular" approach
/// with proper collision-aware pathfinding and waypoint following.
/// 
/// Flow:
///   1. C# calls <see cref="RequestPathAsync"/> → Lua <c>request_path</c> (async)
///   2. Lua <c>on_script_path_request_finished</c> stores the path in <c>storage.nav_path</c>
///   3. Lua <c>on_tick</c> handler walks toward each waypoint, advancing when close enough
///   4. C# polls <see cref="GetNavigationStatusAsync"/> until arrived/stuck/timeout
///   5. Debug lines are drawn on the map for the path
/// </summary>
internal sealed class PathfindingService(RconClient rcon)
{
    /// <summary>
    /// Default poll interval for walking. Can be overridden in tests.
    /// </summary>
    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(0.5);

    // ── Lua Constants ───────────────────────────────────────────────

    /// <summary>
    /// Installs the pathfinding event handlers:
    /// 1. <c>on_script_path_request_finished</c> — receives computed path, stores in
    ///    <c>storage.nav_path</c>, draws debug lines, starts walking
    /// 2. <c>on_tick</c> — follows waypoints: walks toward current waypoint,
    ///    advances index when close, also handles mining state
    /// </summary>
    internal const string InstallPathfindingHandlers = """
        script.on_event(defines.events.on_script_path_request_finished, function(event)
            if event.id ~= storage.nav_request_id then return end
            if not event.path or #event.path == 0 then
                storage.nav_path = nil
                storage.nav_status = "no_path"
                return
            end
            if event.try_again_later then
                storage.nav_path = nil
                storage.nav_status = "busy"
                return
            end
            storage.nav_path = event.path
            storage.nav_index = 1
            storage.nav_status = "walking"
            storage.nav_stuck_ticks = 0
            storage.nav_prev_pos = nil
            -- Draw debug path lines
            local p = game.connected_players[1]
            if p then
                local s = p.surface
                for i = 1, #event.path - 1 do
                    rendering.draw_line{
                        color = {r=0, g=1, b=0.3, a=0.7},
                        width = 2,
                        from = event.path[i].position,
                        to = event.path[i+1].position,
                        surface = s,
                        time_to_live = 1800,
                        draw_on_ground = true
                    }
                end
                -- Draw target circle
                local last = event.path[#event.path].position
                rendering.draw_circle{
                    color = {r=0, g=1, b=0, a=0.5},
                    radius = storage.nav_tolerance or 1.5,
                    filled = false,
                    width = 2,
                    target = last,
                    surface = s,
                    time_to_live = 1800,
                    draw_on_ground = true
                }
            end
        end)
        """;

    /// <summary>
    /// On_tick handler that follows waypoints from <c>storage.nav_path</c> and
    /// also continues mining if <c>storage.mine_state</c> is set.
    /// Uses <c>math.atan2</c> for precise direction (not limited to 8 directions)
    /// for smooth curved path following.
    /// </summary>
    internal const string InstallOnTickHandler = """
        script.on_event(defines.events.on_tick, function()
            local p = game.connected_players[1]
            if not p then return end
            -- Mining (re-apply every tick, Factorio 2 requirement)
            if storage.mine_state then
                p.update_selected_entity(storage.mine_state.position)
                p.mining_state = {mining = true, position = storage.mine_state.position}
            end
            -- Path following
            if not storage.nav_path or not storage.nav_index then return end
            local path = storage.nav_path
            local idx = storage.nav_index
            if idx > #path then
                storage.nav_status = "arrived"
                storage.nav_path = nil
                storage.nav_index = nil
                storage.walk_state = nil
                p.walking_state = {walking = false}
                storage.nav_stuck_ticks = 0
                return
            end
            local target = path[idx].position
            local pos = p.position
            local dx = target.x - pos.x
            local dy = target.y - pos.y
            local dist_sq = dx*dx + dy*dy
            -- Check if close enough to current waypoint
            local wp_tolerance = 0.5
            -- Use larger tolerance for final waypoint
            if idx == #path then
                wp_tolerance = storage.nav_tolerance or 1.5
            end
            if dist_sq < wp_tolerance * wp_tolerance then
                storage.nav_index = idx + 1
                if idx + 1 > #path then
                    storage.nav_status = "arrived"
                    storage.nav_path = nil
                    storage.nav_index = nil
                    storage.walk_state = nil
                    p.walking_state = {walking = false}
                    storage.nav_stuck_ticks = 0
                    return
                end
                target = path[idx + 1].position
                dx = target.x - pos.x
                dy = target.y - pos.y
            end
            -- Stuck detection: if position hasn't changed in 120 ticks (2 sec)
            if storage.nav_prev_pos then
                local pdx = pos.x - storage.nav_prev_pos.x
                local pdy = pos.y - storage.nav_prev_pos.y
                if pdx*pdx + pdy*pdy < 0.01 then
                    storage.nav_stuck_ticks = (storage.nav_stuck_ticks or 0) + 1
                    if storage.nav_stuck_ticks > 120 then
                        storage.nav_status = "stuck"
                        storage.nav_path = nil
                        storage.nav_index = nil
                        storage.walk_state = nil
                        p.walking_state = {walking = false}
                        return
                    end
                else
                    storage.nav_stuck_ticks = 0
                end
            end
            storage.nav_prev_pos = {x = pos.x, y = pos.y}
            -- Walk toward current waypoint using 8-direction mapping
            local dir = math.atan2(dx, -dy) * (4 / math.pi) % 8
            dir = math.floor(dir + 0.5) % 8
            storage.walk_state = {direction = dir}
            p.walking_state = {walking = true, direction = dir}
        end)
        """;

    /// <summary>
    /// Removes the on_tick handler only if neither pathfinding nor mining is active.
    /// </summary>
    internal const string RemoveOnTickIfIdle = """
        if not storage.nav_path and not storage.mine_state and not storage.walk_state then
            script.on_event(defines.events.on_tick, nil)
        end
        """;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Walk to a target position using Factorio's A* pathfinder.
    /// Returns a structured JSON result with status (arrived/stuck/timeout/no_path),
    /// final position, and distance.
    /// </summary>
    public async Task<string> WalkToAsync(
        double targetX, double targetY,
        double tolerance,
        double timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        var pollInterval = PollInterval;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 0.5));

        // Check if already at target
        var posJson = await GetPlayerPositionAsync(cancellationToken);
        var (px, py) = ParsePosition(posJson);
        var dist = Distance(px, py, targetX, targetY);

        if (dist <= tolerance)
            return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);

        // Request path from Factorio's pathfinder
        var requestResult = await RequestPathAsync(targetX, targetY, tolerance, cancellationToken);
        using var reqDoc = JsonDocument.Parse(requestResult);
        var reqRoot = reqDoc.RootElement;

        if (!reqRoot.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
        {
            var error = reqRoot.TryGetProperty("error", out var errProp)
                ? errProp.GetString() ?? "request_failed"
                : "request_failed";
            return FormatResult(error, px, py, targetX, targetY, dist, tolerance);
        }

        // Poll until the path is computed and walking completes
        try
        {
            // Wait for path computation (usually 1-2 ticks)
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var statusJson = await GetNavigationStatusAsync(cancellationToken);
                using var statusDoc = JsonDocument.Parse(statusJson);
                var statusRoot = statusDoc.RootElement;

                var status = statusRoot.GetProperty("status").GetString() ?? "unknown";
                px = statusRoot.GetProperty("x").GetDouble();
                py = statusRoot.GetProperty("y").GetDouble();
                dist = Distance(px, py, targetX, targetY);

                switch (status)
                {
                    case "arrived":
                        await CleanupAsync(cancellationToken);
                        return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);

                    case "stuck":
                        await CleanupAsync(cancellationToken);
                        return FormatResult("stuck", px, py, targetX, targetY, dist, tolerance);

                    case "no_path":
                        await CleanupAsync(cancellationToken);
                        return FormatResult("no_path", px, py, targetX, targetY, dist, tolerance);

                    case "busy":
                        // Pathfinder overloaded, retry once after a delay
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                        await RequestPathAsync(targetX, targetY, tolerance, cancellationToken);
                        break;

                    case "walking":
                    case "waiting":
                        // Still in progress — check if close enough despite status
                        if (dist <= tolerance)
                        {
                            await StopAsync(cancellationToken);
                            return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);
                        }
                        break;
                }

                await Task.Delay(pollInterval, cancellationToken);
            }
        }
        catch
        {
            try { await StopAsync(CancellationToken.None); } catch { }
            throw;
        }

        // Timeout
        await StopAsync(cancellationToken);
        posJson = await GetPlayerPositionAsync(cancellationToken);
        (px, py) = ParsePosition(posJson);
        dist = Distance(px, py, targetX, targetY);
        return FormatResult("timeout", px, py, targetX, targetY, dist, tolerance);
    }

    /// <summary>
    /// Stop all navigation — clears path, stops walking, removes handlers if idle.
    /// </summary>
    public Task<string> StopAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            local player = game.connected_players[1]
            storage.nav_path = nil
            storage.nav_index = nil
            storage.nav_status = nil
            storage.nav_request_id = nil
            storage.nav_tolerance = nil
            storage.nav_stuck_ticks = 0
            storage.nav_prev_pos = nil
            storage.walk_state = nil
            player.walking_state = {walking = false, direction = defines.direction.north}
            {{RemoveOnTickIfIdle}}
            local p = player.position
            rcon.print('{"status":"stopped","x":'..p.x..',"y":'..p.y..'}')
            """,
            cancellationToken);
    }

    /// <summary>
    /// Get the player's current position.
    /// </summary>
    public Task<string> GetPlayerPositionAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local p = game.connected_players[1].position
            rcon.print('{"x":'..p.x..',"y":'..p.y..'}')
            """,
            cancellationToken);
    }

    // ── Internal Methods ────────────────────────────────────────────

    /// <summary>
    /// Request a path from Factorio's A* pathfinder. The result arrives asynchronously
    /// via <c>on_script_path_request_finished</c>.
    /// </summary>
    internal Task<string> RequestPathAsync(
        double targetX, double targetY, double tolerance,
        CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local p = game.connected_players[1]
            if not p.character or not p.character.valid then
                rcon.print('{"success":false,"error":"no_character"}')
                return
            end
            storage.nav_status = "waiting"
            storage.nav_tolerance = {{tolerance}}
            {{InstallPathfindingHandlers}}
            {{InstallOnTickHandler}}
            local id = p.surface.request_path{
                bounding_box = p.character.prototype.collision_box,
                collision_mask = p.character.prototype.collision_mask,
                start = p.position,
                goal = {x={{targetX}}, y={{targetY}}},
                force = p.force,
                radius = {{tolerance}},
                pathfind_flags = {
                    prefer_straight_paths = true,
                    no_break = true
                }
            }
            storage.nav_request_id = id
            local pos = p.position
            rcon.print('{"success":true,"request_id":'..id..',"x":'..pos.x..',"y":'..pos.y..'}')
            """);
        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Query current navigation status: waiting for path, walking, arrived, stuck, or no_path.
    /// </summary>
    internal Task<string> GetNavigationStatusAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local p = game.connected_players[1]
            local pos = p.position
            local status = storage.nav_status or "idle"
            local idx = storage.nav_index or 0
            local total = 0
            if storage.nav_path then total = #storage.nav_path end
            rcon.print('{"status":"'..status..'"'..
                ',"waypoint":'..idx..
                ',"total_waypoints":'..total..
                ',"x":'..pos.x..
                ',"y":'..pos.y..'}')
            """,
            cancellationToken);
    }

    /// <summary>
    /// Clean up navigation state without stopping walking (for terminal states).
    /// </summary>
    private Task<string> CleanupAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync($$"""
            storage.nav_path = nil
            storage.nav_index = nil
            storage.nav_request_id = nil
            storage.nav_tolerance = nil
            storage.nav_stuck_ticks = 0
            storage.nav_prev_pos = nil
            storage.walk_state = nil
            local player = game.connected_players[1]
            player.walking_state = {walking = false, direction = defines.direction.north}
            {{RemoveOnTickIfIdle}}
            rcon.print('ok')
            """,
            cancellationToken);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    internal static (double x, double y) ParsePosition(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble());
    }

    internal static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    internal static string FormatResult(
        string status, double x, double y,
        double targetX, double targetY, double distance, double tolerance)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"status":"{{status}}","x":{{x}},"y":{{y}},"target_x":{{targetX}},"target_y":{{targetY}},"distance":{{string.Format(CultureInfo.InvariantCulture, "{0:F2}", distance)}},"tolerance":{{tolerance}}}""");
    }
}
