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
    /// 
    /// Key features:
    /// - Progress-based waypoint advancement (close-enough + passed-by dot-product test)
    /// - Look-ahead targeting for smoother movement on curves
    /// - Direction dead-zone: at sector boundaries, biases toward the direction component
    ///   that has the larger delta, preventing oscillation from atan2 instability
    /// - Stuck detection: marks stuck if no distance progress toward final goal over 120 ticks
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
            local pos = p.position
            if idx > #path then
                storage.nav_status = "arrived"
                storage.nav_path = nil
                storage.nav_index = nil
                storage.walk_state = nil
                p.walking_state = {walking = false}
                storage.nav_stuck_ticks = 0
                return
            end
            -- Progress-based waypoint advancement:
            -- Skip waypoints we have passed or are very close to.
            local advanced = true
            while advanced and idx <= #path do
                advanced = false
                local wp = path[idx].position
                local dx = wp.x - pos.x
                local dy = wp.y - pos.y
                local dist_sq = dx*dx + dy*dy
                -- Tolerance: 1.5 for intermediate waypoints, user-specified for final
                local tol = 1.5
                if idx == #path then
                    tol = storage.nav_tolerance or 1.5
                end
                -- Close enough: advance
                if dist_sq < tol * tol then
                    idx = idx + 1
                    storage.nav_index = idx
                    advanced = true
                -- Passed-by test for non-final waypoints:
                -- If the next waypoint exists, check if we've passed the current one
                -- by testing if the vector from current wp to player points roughly
                -- toward the next wp (dot product > 0).
                elseif idx < #path then
                    local nwp = path[idx + 1].position
                    local wnx = nwp.x - wp.x
                    local wny = nwp.y - wp.y
                    local wpx = pos.x - wp.x
                    local wpy = pos.y - wp.y
                    local dot = wnx * wpx + wny * wpy
                    if dot > 0 then
                        local seg_len_sq = wnx*wnx + wny*wny
                        if seg_len_sq > 0.01 then
                            local cross = math.abs(wnx * wpy - wny * wpx)
                            local perp_dist = cross / math.sqrt(seg_len_sq)
                            if perp_dist < 3.0 then
                                idx = idx + 1
                                storage.nav_index = idx
                                advanced = true
                            end
                        end
                    end
                end
            end
            -- Check if we finished all waypoints
            if idx > #path then
                storage.nav_status = "arrived"
                storage.nav_path = nil
                storage.nav_index = nil
                storage.walk_state = nil
                p.walking_state = {walking = false}
                storage.nav_stuck_ticks = 0
                return
            end
            -- Look-ahead: target a waypoint a few steps ahead for smoother movement
            local look_ahead = idx
            local max_look = math.min(idx + 4, #path)
            for la = idx + 1, max_look do
                local lawp = path[la].position
                local ladx = lawp.x - pos.x
                local lady = lawp.y - pos.y
                local la_dist_sq = ladx*ladx + lady*lady
                if la_dist_sq < 36 then
                    look_ahead = la
                end
            end
            local target = path[look_ahead].position
            local dx = target.x - pos.x
            local dy = target.y - pos.y
            -- Stuck detection: track distance to FINAL goal position.
            -- If no progress toward the goal over 120 ticks (2 sec), mark stuck.
            local final_wp = path[#path].position
            local goal_dx = final_wp.x - pos.x
            local goal_dy = final_wp.y - pos.y
            local goal_dist_sq = goal_dx*goal_dx + goal_dy*goal_dy
            if not storage.nav_best_goal_dist_sq then
                storage.nav_best_goal_dist_sq = goal_dist_sq
                storage.nav_stuck_ticks = 0
            end
            if goal_dist_sq < storage.nav_best_goal_dist_sq - 0.1 then
                storage.nav_best_goal_dist_sq = goal_dist_sq
                storage.nav_stuck_ticks = 0
            else
                storage.nav_stuck_ticks = (storage.nav_stuck_ticks or 0) + 1
                if storage.nav_stuck_ticks > 120 then
                    storage.nav_status = "stuck"
                    storage.nav_path = nil
                    storage.nav_index = nil
                    storage.walk_state = nil
                    p.walking_state = {walking = false}
                    return
                end
            end
            -- Direction selection with dead-zone to prevent oscillation.
            -- The 8 directions map to sectors of 45 deg each. When the ideal angle
            -- falls near a sector boundary (within ~5 deg), atan2 noise from
            -- sub-tile position changes causes the direction to flip every tick.
            --
            -- Fix: use component-based direction selection. Pick the axis-aligned
            -- or diagonal direction whose movement vector best matches (dx, dy).
            -- Ties are broken by preferring the cardinal direction (N/S/E/W) which
            -- guarantees progress along the dominant axis without zigzag.
            --
            -- Direction vectors (defines.direction):
            -- 0=N(0,-1) 1=NE(1,-1) 2=E(1,0) 3=SE(1,1) 4=S(0,1) 5=SW(-1,1) 6=W(-1,0) 7=NW(-1,-1)
            local adx = math.abs(dx)
            local ady = math.abs(dy)
            local dir
            -- If one component dominates heavily (>2.4x the other), use cardinal direction
            -- This avoids diagonal oscillation when movement is mostly along one axis
            if ady > adx * 2.414 then
                -- Mostly vertical: use N or S
                dir = dy < 0 and 0 or 4
            elseif adx > ady * 2.414 then
                -- Mostly horizontal: use E or W
                dir = dx > 0 and 2 or 6
            else
                -- Diagonal movement: pick the appropriate diagonal
                if dx > 0 then
                    dir = dy < 0 and 1 or 3
                else
                    dir = dy < 0 and 7 or 5
                end
            end
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
            storage.nav_cur_dir = nil
            storage.nav_best_goal_dist_sq = nil
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
                entity_to_ignore = p.character,
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
            storage.nav_cur_dir = nil
            storage.nav_best_goal_dist_sq = nil
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
