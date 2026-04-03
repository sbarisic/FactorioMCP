using System.Globalization;

namespace FactorioMCP.Services;

internal sealed partial class FactorioService
{
    /// <summary>
    /// Poll the crafting queue until it is empty or the timeout expires.
    /// Returns the final queue state as JSON.
    /// </summary>
    public async Task<string> WaitForCraftingAsync(
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await GetCraftingQueueAsync(cancellationToken);
            if (result.Contains("\"queue\":[]"))
                return """{"status":"complete","queue":[]}""";
            await Task.Delay(pollInterval, cancellationToken);
        }

        var finalQueue = await GetCraftingQueueAsync(cancellationToken);
        return $$"""{"status":"timeout","remaining":{{finalQueue}}}""";
    }

    /// <summary>
    /// Poll the player position until it is within the given tolerance of the target
    /// coordinates, or the timeout expires. Returns the final position as JSON.
    /// </summary>
    public async Task<string> WaitForPositionAsync(
        double targetX,
        double targetY,
        double tolerance,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tolerance, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lua = string.Create(CultureInfo.InvariantCulture, $$"""
                local p = game.connected_players[1].position
                local dx = p.x - {{targetX}}
                local dy = p.y - {{targetY}}
                local dist = math.sqrt(dx*dx + dy*dy)
                rcon.print('{"x":'..p.x..',"y":'..p.y..',"distance":'..string.format("%.2f", dist)..'}')
                """);
            var result = await rcon.ExecuteLuaAsync(lua, cancellationToken);
            if (result.Contains("\"distance\":"))
            {
                var distStr = result.Split("\"distance\":")[1].Split([',', '}'])[0];
                if (double.TryParse(distStr, CultureInfo.InvariantCulture, out var dist) && dist <= tolerance)
                    return $$"""{"status":"arrived","tolerance":{{string.Format(CultureInfo.InvariantCulture, "{0}", tolerance)}},"position":{{result}}}""";
            }
            await Task.Delay(pollInterval, cancellationToken);
        }

        var finalPos = await GetPlayerPositionAsync(cancellationToken);
        return $$"""{"status":"timeout","target_x":{{string.Format(CultureInfo.InvariantCulture, "{0}", targetX)}},"target_y":{{string.Format(CultureInfo.InvariantCulture, "{0}", targetY)}},"position":{{finalPos}}}""";
    }

    /// <summary>
    /// Get the current game tick count.
    /// </summary>
    public Task<string> GetGameTickAsync(CancellationToken cancellationToken = default)
    {
        return rcon.ExecuteLuaAsync("""
            rcon.print('{"tick":'..game.tick..'}')
            """, cancellationToken);
    }

    /// <summary>
    /// Wait for a specified number of game ticks to elapse.
    /// Polls the game tick and waits until the target tick count has passed.
    /// </summary>
    public async Task<string> WaitForTicksAsync(
        int ticks,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var startResult = await GetGameTickAsync(cancellationToken);
        var startTickStr = startResult.Split("\"tick\":")[1].Split('}')[0];
        if (!long.TryParse(startTickStr, CultureInfo.InvariantCulture, out var startTick))
            return """{"status":"error","error":"failed_to_read_tick"}""";

        var targetTick = startTick + ticks;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(pollInterval, cancellationToken);

            var tickResult = await GetGameTickAsync(cancellationToken);
            var currentTickStr = tickResult.Split("\"tick\":")[1].Split('}')[0];
            if (long.TryParse(currentTickStr, CultureInfo.InvariantCulture, out var currentTick) && currentTick >= targetTick)
                return string.Create(CultureInfo.InvariantCulture, $$$"""{"status":"complete","start_tick":{{{startTick}}},"end_tick":{{{currentTick}}},"elapsed":{{{currentTick - startTick}}}}""");
        }

        var finalResult = await GetGameTickAsync(cancellationToken);
        var finalTickStr = finalResult.Split("\"tick\":")[1].Split('}')[0];
        long.TryParse(finalTickStr, CultureInfo.InvariantCulture, out var finalTick);
        return string.Create(CultureInfo.InvariantCulture, $$$"""{"status":"timeout","start_tick":{{{startTick}}},"current_tick":{{{finalTick}}},"target_tick":{{{targetTick}}}}""");
    }
}
