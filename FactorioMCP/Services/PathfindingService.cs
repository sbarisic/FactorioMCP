using System.Globalization;
using System.Text;
using System.Text.Json;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Pathfinding service using Factorio's A* pathfinder with C#-driven waypoint following.
/// 
/// Design: All navigation logic runs in C#, with Lua only used for:
///   1. Requesting paths (async via request_path)
///   2. Receiving path results (minimal event handler)
///   3. Continuous walking via on_tick handler (Factorio 2 resets walking_state each tick)
/// 
/// In Factorio 2, walking_state only persists for a single tick, so a Lua on_tick handler
/// must re-apply the walking direction every tick. C# controls the direction by writing
/// to storage.walk_dir, and the on_tick handler reads it to keep the player moving.
/// </summary>
internal sealed class PathfindingService(RconClient rcon)
{
    /// <summary>
    /// Poll interval for position checks during walking. Can be overridden in tests.
    /// </summary>
    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    private bool _pathHandlerInstalled;
    private int? _lastDirection;

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
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 0.5));

        // Check if already at target
        var (px, py) = await GetPositionAsync(cancellationToken);
        var dist = Distance(px, py, targetX, targetY);

        if (dist <= tolerance)
            return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);

        // Ensure path handler is installed (once per session)
        await EnsurePathHandlerInstalledAsync(cancellationToken);

        // Request path from Factorio's A* pathfinder
        var requestId = await RequestPathAsync(targetX, targetY, tolerance, cancellationToken);
        if (requestId < 0)
            return FormatResult("no_character", px, py, targetX, targetY, dist, tolerance);

        // Wait for path computation (usually 1-2 game ticks)
        List<(double x, double y)>? waypoints = null;
        for (int i = 0; i < 20 && waypoints is null; i++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            var (status, path) = await GetPathResultAsync(requestId, cancellationToken);

            if (status == "ok" && path is not null)
            {
                waypoints = path;
                break;
            }
            if (status == "no_path")
                return FormatResult("no_path", px, py, targetX, targetY, dist, tolerance);
            if (status == "busy")
            {
                // Pathfinder overloaded — old request already cleaned up by
                // GetPathResultAsync (non-ok entries are nil'd), just retry
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                requestId = await RequestPathAsync(targetX, targetY, tolerance, cancellationToken);
                if (requestId < 0)
                    return FormatResult("no_character", px, py, targetX, targetY, dist, tolerance);
            }
        }

        if (waypoints is null || waypoints.Count == 0)
            return FormatResult("no_path", px, py, targetX, targetY, dist, tolerance);

        // Draw debug visualization
        await DrawPathAsync(waypoints, tolerance, cancellationToken);

        // Follow waypoints from C#
        return await FollowWaypointsAsync(waypoints, targetX, targetY, tolerance, deadline, cancellationToken);
    }

    /// <summary>
    /// Stop the player from walking and clear navigation state.
    /// </summary>
    public async Task<string> StopAsync(CancellationToken cancellationToken = default)
    {
        await StopWalkingAsync(cancellationToken);
        var (x, y) = await GetPositionAsync(cancellationToken);
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"status":"stopped","x":{{x}},"y":{{y}}}""");
    }

    /// <summary>
    /// Get the player's current position.
    /// </summary>
    public Task<string> GetPlayerPositionAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            local p = game.connected_players[1].position
            rcon.print('{"x":'..p.x..',"y":'..p.y..'}')
            """, cancellationToken);
    }

    // ── Waypoint Following (Segment-Vector Projection Model) ──────────

    private async Task<string> FollowWaypointsAsync(
        List<(double x, double y)> waypoints,
        double targetX, double targetY,
        double tolerance,
        DateTime deadline,
        CancellationToken cancellationToken)
    {
        // Segment index: player walks from waypoints[segIndex] toward waypoints[segIndex+1].
        // When projection along the segment passes the endpoint, advance to next segment.
        int segIndex = 0;
        double bestProgress = 0; // cumulative distance along path (for stuck detection)
        int stuckCount = 0;
        const int maxStuckCount = 60; // ~3 seconds at 50ms poll

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (px, py) = await GetPositionAsync(cancellationToken);

                // Check if we've reached the final destination
                var dist = Distance(px, py, targetX, targetY);
                if (dist <= tolerance)
                {
                    await StopWalkingAsync(cancellationToken);
                    return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);
                }

                // Advance segments using projection
                segIndex = AdvanceSegment(waypoints, segIndex, px, py);

                // Past last segment — arrived (close enough to final waypoint)
                if (segIndex >= waypoints.Count - 1)
                {
                    await StopWalkingAsync(cancellationToken);
                    return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);
                }

                // Stuck detection: measure progress along the path, not toward the goal.
                // Progress = sum of completed segment lengths + projection on current segment.
                var progress = GetPathProgress(waypoints, segIndex, px, py);
                if (progress > bestProgress + 0.1)
                {
                    bestProgress = progress;
                    stuckCount = 0;
                }
                else
                {
                    stuckCount++;
                }

                if (stuckCount > maxStuckCount)
                {
                    await StopWalkingAsync(cancellationToken);
                    return FormatResult("stuck", px, py, targetX, targetY, dist, tolerance);
                }

                // Steer toward the endpoint of the current segment
                var (wpx, wpy) = waypoints[segIndex + 1];
                var direction = CalculateDirection(px, py, wpx, wpy);
                if (_lastDirection != direction)
                    await SetWalkingDirectionAsync(direction, cancellationToken);

                await Task.Delay(PollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            try { await StopWalkingAsync(CancellationToken.None); } catch { }
            throw;
        }

        // Timeout
        await StopWalkingAsync(cancellationToken);
        var (finalX, finalY) = await GetPositionAsync(cancellationToken);
        var finalDist = Distance(finalX, finalY, targetX, targetY);
        return FormatResult("timeout", finalX, finalY, targetX, targetY, finalDist, tolerance);
    }

    /// <summary>
    /// Advance the segment index by projecting the player position onto the current segment vector.
    /// When the projection parameter t >= 1.0 (player has passed the segment endpoint),
    /// advance to the next segment. Only advances one segment at a time to maintain path fidelity
    /// and avoid skipping obstacle-avoidance waypoints.
    /// </summary>
    internal static int AdvanceSegment(
        List<(double x, double y)> waypoints, int segIndex,
        double playerX, double playerY)
    {
        while (segIndex < waypoints.Count - 1)
        {
            var t = ProjectOntoSegment(waypoints, segIndex, playerX, playerY);

            // Player hasn't passed the segment endpoint yet — stay on this segment
            if (t < 1.0)
                break;

            segIndex++;
        }

        return segIndex;
    }

    /// <summary>
    /// Calculate cumulative progress along the path in tiles.
    /// Sum of completed segment lengths + clamped projection on current segment.
    /// Used for stuck detection: if this value stops increasing, the player is stuck.
    /// </summary>
    internal static double GetPathProgress(
        List<(double x, double y)> waypoints, int segIndex,
        double playerX, double playerY)
    {
        double progress = 0;

        // Sum completed segments
        for (int i = 0; i < segIndex && i < waypoints.Count - 1; i++)
        {
            var (ax, ay) = waypoints[i];
            var (bx, by) = waypoints[i + 1];
            progress += Distance(ax, ay, bx, by);
        }

        // Add projection on current segment (clamped to [0, segLen])
        if (segIndex < waypoints.Count - 1)
        {
            var (sx, sy) = waypoints[segIndex];
            var (ex, ey) = waypoints[segIndex + 1];
            var segLen = Distance(sx, sy, ex, ey);

            if (segLen > 0.001)
            {
                var t = ProjectOntoSegment(waypoints, segIndex, playerX, playerY);
                progress += Math.Clamp(t, 0, 1) * segLen;
            }
        }

        return progress;
    }

    /// <summary>
    /// Project the player position onto a path segment, returning the parameter t.
    /// t=0 means at segment start, t=1 means at segment end, t>1 means past the endpoint.
    /// Uses the standard vector projection formula: t = dot(player-start, end-start) / |end-start|².
    /// </summary>
    internal static double ProjectOntoSegment(
        List<(double x, double y)> waypoints, int segIndex,
        double playerX, double playerY)
    {
        var (sx, sy) = waypoints[segIndex];
        var (ex, ey) = waypoints[segIndex + 1];
        var segX = ex - sx;
        var segY = ey - sy;
        var segLenSq = segX * segX + segY * segY;

        if (segLenSq < 0.001)
            return 1.0; // degenerate segment, treat as passed

        var toPlayerX = playerX - sx;
        var toPlayerY = playerY - sy;
        return (segX * toPlayerX + segY * toPlayerY) / segLenSq;
    }

    /// <summary>
    /// Calculate Factorio 2 direction (0-15, even values for 8 cardinals) using
    /// component-based selection to avoid oscillation at sector boundaries.
    /// Factorio 2 uses 16 directions: 0=N 2=NE 4=E 6=SE 8=S 10=SW 12=W 14=NW
    /// (odd values are intermediate directions like NNE=1, ENE=3, etc.)
    /// </summary>
    internal static int CalculateDirection(double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var adx = Math.Abs(dx);
        var ady = Math.Abs(dy);

        const double threshold = 2.414; // tan(67.5°) - use cardinal if one axis dominates

        if (ady > adx * threshold)
            return dy < 0 ? 0 : 8; // North or South

        if (adx > ady * threshold)
            return dx > 0 ? 4 : 12; // East or West

        // Diagonal
        if (dx > 0)
            return dy < 0 ? 2 : 6; // NE or SE

        return dy < 0 ? 14 : 10; // NW or SW
    }

    // ── Lua Commands ────────────────────────────────────────────────

    /// <summary>
    /// Install the path result handler and shared on_tick handler once per session.
    /// The on_tick handler continuously re-applies walking_state because Factorio 2
    /// resets it every tick — a single set only moves the player for one tick.
    /// </summary>
    private async Task EnsurePathHandlerInstalledAsync(CancellationToken cancellationToken)
    {
        if (_pathHandlerInstalled) return;

        await rcon.ExecuteLuaAsync($$"""
            storage.nav_results = storage.nav_results or {}
            storage.walk_dir = nil
            script.on_event(defines.events.on_script_path_request_finished, function(event)
                if event.path and #event.path > 0 then
                    storage.nav_results[event.id] = {status = "ok", path = event.path, tick = game.tick}
                elseif event.try_again_later then
                    storage.nav_results[event.id] = {status = "busy", tick = game.tick}
                else
                    storage.nav_results[event.id] = {status = "no_path", tick = game.tick}
                end
            end)
            {{FactorioService.LuaOnTickHandler}}
            rcon.print('ok')
            """, cancellationToken);

        _pathHandlerInstalled = true;
    }

    /// <summary>
    /// Request a path from Factorio's A* pathfinder.
    /// </summary>
    private async Task<int> RequestPathAsync(
        double targetX, double targetY, double tolerance,
        CancellationToken cancellationToken)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local p = game.connected_players[1]
            if not p.character or not p.character.valid then
                rcon.print('-1')
                return
            end
            local id = p.surface.request_path{
                bounding_box = p.character.prototype.collision_box,
                collision_mask = p.character.prototype.collision_mask,
                start = p.position,
                goal = {x={{targetX}}, y={{targetY}}},
                force = p.force,
                radius = {{tolerance}},
                entity_to_ignore = p.character,
                pathfind_flags = {prefer_straight_paths = true, no_break = true}
            }
            rcon.print(tostring(id))
            """);

        var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);
        return int.TryParse(result.Trim(), out var id) ? id : -1;
    }

    /// <summary>
    /// Get the result of a path request.
    /// </summary>
    private async Task<(string status, List<(double x, double y)>? path)> GetPathResultAsync(
        int requestId, CancellationToken cancellationToken)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            if storage.nav_results then
                local now = game.tick
                for id, r in pairs(storage.nav_results) do
                    if now - (r.tick or 0) > 600 then
                        storage.nav_results[id] = nil
                    end
                end
            end
            local r = storage.nav_results and storage.nav_results[{{requestId}}]
            if not r then
                rcon.print('{"status":"waiting"}')
                return
            end
            if r.status ~= "ok" then
                storage.nav_results[{{requestId}}] = nil
                rcon.print('{"status":"'..r.status..'"}')
                return
            end
            local pts = {}
            for i, wp in ipairs(r.path) do
                pts[i] = '{"x":'..wp.position.x..',"y":'..wp.position.y..'}'
            end
            storage.nav_results[{{requestId}}] = nil
            rcon.print('{"status":"ok","path":['..table.concat(pts, ',')..']}')
            """);

        var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        var status = root.GetProperty("status").GetString() ?? "unknown";

        if (status != "ok" || !root.TryGetProperty("path", out var pathProp))
            return (status, null);

        var waypoints = new List<(double, double)>(pathProp.GetArrayLength());
        foreach (var wp in pathProp.EnumerateArray())
            waypoints.Add((wp.GetProperty("x").GetDouble(), wp.GetProperty("y").GetDouble()));

        return (status, waypoints);
    }

    private async Task<(double x, double y)> GetPositionAsync(CancellationToken cancellationToken)
    {
        var json = await GetPlayerPositionAsync(cancellationToken);
        return ParsePosition(json);
    }

    private async Task StopWalkingAsync(CancellationToken cancellationToken)
    {
        _lastDirection = null;
        await rcon.ExecuteLuaAsync("""
            storage.walk_dir = nil
            game.connected_players[1].walking_state = {walking = false, direction = defines.direction.north}
            rcon.print('ok')
            """, cancellationToken);
    }

    private async Task SetWalkingDirectionAsync(int direction, CancellationToken cancellationToken)
    {
        _lastDirection = direction;
        await rcon.ExecuteLuaAsync(string.Create(CultureInfo.InvariantCulture, $$"""
            storage.walk_dir = {{direction}}
            rcon.print('ok')
            """), cancellationToken);
    }

    /// <summary>
    /// Draw debug path visualization on the game map.
    /// Draws lines from the player's current position through all waypoints,
    /// plus a circle at the final waypoint showing the arrival tolerance.
    /// </summary>
    private Task DrawPathAsync(List<(double x, double y)> waypoints, double tolerance, CancellationToken cancellationToken)
    {
        if (waypoints.Count == 0)
            return Task.CompletedTask;

        var lua = new StringBuilder();
        lua.Append("local p = game.connected_players[1]\n");
        lua.Append("local s = p.surface\n");

        // Draw first segment from the player's actual position to the first waypoint
        var (wx0, wy0) = waypoints[0];
        lua.AppendFormat(CultureInfo.InvariantCulture,
            "rendering.draw_line{{color={{0,1,0.3,0.7}},width=2,from=p.position,to={{{0},{1}}},surface=s,time_to_live=1800,draw_on_ground=true}}\n",
            wx0, wy0);

        // Draw remaining segments between waypoints
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            var (x1, y1) = waypoints[i];
            var (x2, y2) = waypoints[i + 1];
            lua.AppendFormat(CultureInfo.InvariantCulture,
                "rendering.draw_line{{color={{0,1,0.3,0.7}},width=2,from={{{0},{1}}},to={{{2},{3}}},surface=s,time_to_live=1800,draw_on_ground=true}}\n",
                x1, y1, x2, y2);
        }

        var (lastX, lastY) = waypoints[^1];
        lua.AppendFormat(CultureInfo.InvariantCulture,
            "rendering.draw_circle{{color={{0,1,0,0.5}},radius={0},filled=false,width=2,target={{{1},{2}}},surface=s,time_to_live=1800,draw_on_ground=true}}\n",
            tolerance, lastX, lastY);
        lua.Append("rcon.print('ok')");

        return rcon.ExecuteLuaAsync(lua.ToString(), cancellationToken);
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
