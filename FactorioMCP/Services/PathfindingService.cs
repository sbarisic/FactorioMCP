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
///   3. Setting walking direction (simple command)
/// 
/// This approach is simpler and more robust than Lua-based on_tick handlers because:
///   - All state lives in C# (easier to debug, test, cancel)
///   - No complex Lua event handler registration/cleanup per request
///   - Predictable behavior with clear control flow
/// </summary>
internal sealed class PathfindingService(RconClient rcon)
{
    /// <summary>
    /// Poll interval for position checks during walking. Can be overridden in tests.
    /// </summary>
    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(150);

    private bool _pathHandlerInstalled;

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
                // Pathfinder overloaded, wait and retry
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                requestId = await RequestPathAsync(targetX, targetY, tolerance, cancellationToken);
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

    // ── Waypoint Following ──────────────────────────────────────────

    private async Task<string> FollowWaypointsAsync(
        List<(double x, double y)> waypoints,
        double targetX, double targetY,
        double tolerance,
        DateTime deadline,
        CancellationToken cancellationToken)
    {
        int waypointIndex = 0;
        double bestDistToGoal = double.MaxValue;
        int stuckCount = 0;
        const int maxStuckCount = 40; // ~6 seconds at 150ms poll

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (px, py) = await GetPositionAsync(cancellationToken);

                // Advance through waypoints we've reached or passed
                waypointIndex = AdvanceWaypoints(waypoints, waypointIndex, px, py, tolerance);

                // Check if we've reached the destination
                var dist = Distance(px, py, targetX, targetY);
                if (waypointIndex >= waypoints.Count || dist <= tolerance)
                {
                    await StopWalkingAsync(cancellationToken);
                    return FormatResult("arrived", px, py, targetX, targetY, dist, tolerance);
                }

                // Stuck detection: no progress toward goal
                if (dist < bestDistToGoal - 0.1)
                {
                    bestDistToGoal = dist;
                    stuckCount = 0;
                }
                else if (++stuckCount > maxStuckCount)
                {
                    await StopWalkingAsync(cancellationToken);
                    return FormatResult("stuck", px, py, targetX, targetY, dist, tolerance);
                }

                // Look-ahead: target a waypoint ahead for smoother curves
                var targetWpIndex = GetLookAheadIndex(waypoints, waypointIndex, px, py);
                var (wpx, wpy) = waypoints[targetWpIndex];

                // Set walking direction
                var direction = CalculateDirection(px, py, wpx, wpy);
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
    /// Advance waypoint index past waypoints that have been reached or passed.
    /// </summary>
    private static int AdvanceWaypoints(
        List<(double x, double y)> waypoints, int currentIndex,
        double playerX, double playerY, double finalTolerance)
    {
        while (currentIndex < waypoints.Count)
        {
            var (wpx, wpy) = waypoints[currentIndex];
            var wpDist = Distance(playerX, playerY, wpx, wpy);
            var wpTolerance = currentIndex == waypoints.Count - 1 ? finalTolerance : 1.5;

            // Close enough to waypoint
            if (wpDist <= wpTolerance)
            {
                currentIndex++;
                continue;
            }

            // Passed-by test for intermediate waypoints using dot product
            if (currentIndex < waypoints.Count - 1)
            {
                var (nwpx, nwpy) = waypoints[currentIndex + 1];
                var segX = nwpx - wpx;
                var segY = nwpy - wpy;
                var toPlayerX = playerX - wpx;
                var toPlayerY = playerY - wpy;
                var dot = segX * toPlayerX + segY * toPlayerY;

                if (dot > 0)
                {
                    var segLenSq = segX * segX + segY * segY;
                    if (segLenSq > 0.01)
                    {
                        var cross = Math.Abs(segX * toPlayerY - segY * toPlayerX);
                        var perpDist = cross / Math.Sqrt(segLenSq);
                        if (perpDist < 3.0)
                        {
                            currentIndex++;
                            continue;
                        }
                    }
                }
            }

            break;
        }

        return currentIndex;
    }

    /// <summary>
    /// Get look-ahead waypoint index for smoother movement on curves.
    /// </summary>
    private static int GetLookAheadIndex(
        List<(double x, double y)> waypoints, int currentIndex,
        double playerX, double playerY)
    {
        var lookAhead = currentIndex;
        var maxLook = Math.Min(currentIndex + 5, waypoints.Count);

        for (int i = currentIndex + 1; i < maxLook; i++)
        {
            var (wpx, wpy) = waypoints[i];
            if (Distance(playerX, playerY, wpx, wpy) < 6.0)
                lookAhead = i;
        }

        return lookAhead;
    }

    /// <summary>
    /// Calculate Factorio direction (0-7) using component-based selection
    /// to avoid oscillation at sector boundaries.
    /// Direction mapping: 0=N 1=NE 2=E 3=SE 4=S 5=SW 6=W 7=NW
    /// </summary>
    private static int CalculateDirection(double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var adx = Math.Abs(dx);
        var ady = Math.Abs(dy);

        const double threshold = 2.414; // tan(67.5°) - use cardinal if one axis dominates

        if (ady > adx * threshold)
            return dy < 0 ? 0 : 4; // North or South

        if (adx > ady * threshold)
            return dx > 0 ? 2 : 6; // East or West

        // Diagonal
        if (dx > 0)
            return dy < 0 ? 1 : 3; // NE or SE

        return dy < 0 ? 7 : 5; // NW or SW
    }

    // ── Lua Commands ────────────────────────────────────────────────

    /// <summary>
    /// Install the path result handler once per session.
    /// </summary>
    private async Task EnsurePathHandlerInstalledAsync(CancellationToken cancellationToken)
    {
        if (_pathHandlerInstalled) return;

        await rcon.ExecuteLuaAsync("""
            storage.nav_results = storage.nav_results or {}
            script.on_event(defines.events.on_script_path_request_finished, function(event)
                if event.path and #event.path > 0 then
                    storage.nav_results[event.id] = {status = "ok", path = event.path}
                elseif event.try_again_later then
                    storage.nav_results[event.id] = {status = "busy"}
                else
                    storage.nav_results[event.id] = {status = "no_path"}
                end
            end)
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

    private Task StopWalkingAsync(CancellationToken cancellationToken)
    {
        return rcon.ExecuteLuaAsync("""
            game.connected_players[1].walking_state = {walking = false, direction = defines.direction.north}
            rcon.print('ok')
            """, cancellationToken);
    }

    private Task SetWalkingDirectionAsync(int direction, CancellationToken cancellationToken)
    {
        return rcon.ExecuteLuaAsync(string.Create(CultureInfo.InvariantCulture, $$"""
            game.connected_players[1].walking_state = {walking = true, direction = {{direction}}}
            rcon.print('ok')
            """), cancellationToken);
    }

    /// <summary>
    /// Draw debug path visualization on the game map.
    /// </summary>
    private Task DrawPathAsync(List<(double x, double y)> waypoints, double tolerance, CancellationToken cancellationToken)
    {
        if (waypoints.Count < 2)
            return Task.CompletedTask;

        var lua = new StringBuilder();
        lua.Append("local s = game.connected_players[1].surface\n");

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            var (x1, y1) = waypoints[i];
            var (x2, y2) = waypoints[i + 1];
            lua.AppendFormat(CultureInfo.InvariantCulture,
                "rendering.draw_line{{color={{r=0,g=1,b=0.3,a=0.7}},width=2,from={{x={0},y={1}}},to={{x={2},y={3}}},surface=s,time_to_live=1800,draw_on_ground=true}}\n",
                x1, y1, x2, y2);
        }

        var (lastX, lastY) = waypoints[^1];
        lua.AppendFormat(CultureInfo.InvariantCulture,
            "rendering.draw_circle{{color={{r=0,g=1,b=0,a=0.5}},radius={0},filled=false,width=2,target={{x={1},y={2}}},surface=s,time_to_live=1800,draw_on_ground=true}}\n",
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
