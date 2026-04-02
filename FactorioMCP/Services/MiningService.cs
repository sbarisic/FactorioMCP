using System.Globalization;
using System.Text.Json;
using FactorioMCP.Rcon;

namespace FactorioMCP.Services;

/// <summary>
/// Service for realistic resource mining via RCON Lua commands.
/// Uses <c>player.mining_state</c> to initiate tick-based mining instead of
/// instant extraction, respecting the "no cheating" design principle.
/// Buildings are still mined instantly via <c>player.mine_entity()</c> in
/// <see cref="FactorioService.MineEntityAtAsync"/>.
/// </summary>
internal sealed class MiningService(RconClient rcon)
{
    /// <summary>
    /// Start mining a resource entity at the given position using <c>player.mining_state</c>.
    /// Validates proximity, selects the entity via <c>update_selected_entity</c>, and sets
    /// mining state to begin tick-based extraction. Returns entity info, current amount,
    /// and estimated mining time per unit.
    /// </summary>
    public Task<string> StartMiningResourceAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local player_pos = player.position
            local tx = {{x}}
            local ty = {{y}}
            local dx = tx - player_pos.x
            local dy = ty - player_pos.y
            local distance = math.sqrt(dx*dx + dy*dy)
            if distance > player.reach_distance then
                rcon.print('{"success":false,"error":"out_of_range","distance":'..string.format("%.1f", distance)..',"limit":'..player.reach_distance..'}')
                return
            end
            local pos = {tx, ty}
            local entities = player.surface.find_entities_filtered{position=pos, radius=1, type="resource"}
            if #entities == 0 then
                rcon.print('{"success":false,"error":"no_resource","x":'..tx..',"y":'..ty..'}')
                return
            end
            local e = entities[1]
            local name = e.name
            local amount = e.amount or 0
            local mining_time = e.prototype.mineable_properties.mining_time or 1.0
            -- Select the entity and start mining
            player.update_selected_entity(pos)
            player.mining_state = {mining = true, position = pos}
            rcon.print('{"success":true,"entity":"'..name..'"'..
                ',"amount":'..amount..
                ',"mining_time_per_unit":'..string.format("%.2f", mining_time)..
                ',"x":'..string.format("%.1f", e.position.x)..
                ',"y":'..string.format("%.1f", e.position.y)..
                ',"status":"mining_started"}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Check the current mining status: whether the player is still mining,
    /// the remaining resource amount at the position, and how many units have been mined.
    /// </summary>
    public Task<string> GetMiningStatusAsync(double x, double y, int initialAmount, CancellationToken cancellationToken = default)
    {
        var lua = string.Create(CultureInfo.InvariantCulture, $$"""
            local player = game.connected_players[1]
            local is_mining = player.mining_state.mining
            local tx = {{x}}
            local ty = {{y}}
            local entities = player.surface.find_entities_filtered{position={tx, ty}, radius=1, type="resource"}
            if #entities == 0 then
                -- Resource entity is gone (fully depleted)
                rcon.print('{"is_mining":'..tostring(is_mining)..
                    ',"depleted":true'..
                    ',"remaining":0'..
                    ',"mined":'..{{initialAmount}}..'}')
                return
            end
            local e = entities[1]
            local remaining = e.amount or 0
            local mined = {{initialAmount}} - remaining
            rcon.print('{"is_mining":'..tostring(is_mining)..
                ',"depleted":false'..
                ',"remaining":'..remaining..
                ',"mined":'..mined..
                ',"entity":"'..e.name..'"}')
            """);

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Stop mining by setting <c>mining_state</c> to not mining.
    /// </summary>
    public Task<string> StopMiningAsync(CancellationToken cancellationToken = default)
    {
        const string lua = """
            local player = game.connected_players[1]
            player.mining_state = {mining = false}
            rcon.print('{"success":true,"status":"mining_stopped"}')
            """;

        return rcon.ExecuteLuaAsync(lua, cancellationToken);
    }

    /// <summary>
    /// Mine a specified number of resource units from a resource entity using realistic
    /// tick-based mining. Starts mining via <c>mining_state</c>, polls until the desired
    /// count is extracted or timeout. Returns the actual number mined.
    /// </summary>
    public async Task<string> MineResourceAsync(
        double x,
        double y,
        int count,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        // Step 1: Start mining and get initial state
        var startResult = await StartMiningResourceAsync(x, y, cancellationToken);

        using var startDoc = JsonDocument.Parse(startResult);
        var startRoot = startDoc.RootElement;

        if (!startRoot.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
        {
            return startResult; // Return the error as-is
        }

        var entityName = startRoot.GetProperty("entity").GetString()!;
        var initialAmount = startRoot.GetProperty("amount").GetInt32();
        var targetMined = Math.Min(count, initialAmount); // Can't mine more than available

        // Step 2: Poll until enough units are mined or timeout
        var deadline = DateTime.UtcNow + timeout;
        var totalMined = 0;
        var remaining = initialAmount;
        var depleted = false;

        while (DateTime.UtcNow < deadline && totalMined < targetMined)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(pollInterval, cancellationToken);

            var statusResult = await GetMiningStatusAsync(x, y, initialAmount, cancellationToken);
            using var statusDoc = JsonDocument.Parse(statusResult);
            var statusRoot = statusDoc.RootElement;

            totalMined = statusRoot.GetProperty("mined").GetInt32();
            depleted = statusRoot.GetProperty("depleted").GetBoolean();

            if (!depleted)
            {
                remaining = statusRoot.GetProperty("remaining").GetInt32();
            }
            else
            {
                remaining = 0;
                break;
            }

            // Check if player stopped mining (moved out of range, etc.)
            var isMining = statusRoot.GetProperty("is_mining").GetBoolean();
            if (!isMining && totalMined < targetMined && !depleted)
            {
                // Try to restart mining
                var restartResult = await StartMiningResourceAsync(x, y, cancellationToken);
                using var restartDoc = JsonDocument.Parse(restartResult);
                if (!restartDoc.RootElement.TryGetProperty("success", out var restartSuccess) || !restartSuccess.GetBoolean())
                {
                    break; // Can't restart, return what we have
                }
            }
        }

        // Step 3: Stop mining
        await StopMiningAsync(cancellationToken);

        // Step 4: Return results
        var reachedTarget = totalMined >= targetMined;
        var status = depleted ? "depleted" : reachedTarget ? "complete" : "timeout";

        return string.Create(CultureInfo.InvariantCulture,
            $$"""{"success":true,"status":"{{status}}","entity":"{{entityName}}","mined":{{totalMined}},"requested":{{count}},"remaining":{{remaining}},"depleted":{{depleted.ToString().ToLowerInvariant()}}}""");
    }
}
