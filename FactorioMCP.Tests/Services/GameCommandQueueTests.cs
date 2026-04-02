using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class GameCommandQueueTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsOperationResult()
    {
        using var queue = new GameCommandQueue();

        var result = await queue.ExecuteAsync("test", _ => Task.FromResult("hello"));

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ExecuteAsync_SerializesOperations()
    {
        using var queue = new GameCommandQueue();
        var running = false;
        var overlapDetected = false;

        async Task<string> Operation(CancellationToken ct)
        {
            if (running)
                overlapDetected = true;
            running = true;
            await Task.Delay(50, ct);
            running = false;
            return "done";
        }

        var task1 = queue.ExecuteAsync("op1", Operation);
        var task2 = queue.ExecuteAsync("op2", Operation);

        await Task.WhenAll(task1, task2);

        Assert.False(overlapDetected, "Operations should not overlap — the queue must serialize them.");
        Assert.Equal("done", await task1);
        Assert.Equal("done", await task2);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellation_WhenWaitingForGate()
    {
        using var queue = new GameCommandQueue();
        using var cts = new CancellationTokenSource();
        var blockingStarted = new TaskCompletionSource();

        // Hold the gate with a long-running operation
        var blockingTask = queue.ExecuteAsync("blocking", async ct =>
        {
            blockingStarted.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return "blocked";
        });

        await blockingStarted.Task;

        // Cancel the token before the second operation can acquire the gate
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => queue.ExecuteAsync("cancelled", _ => Task.FromResult("should not run"), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesOperationException()
    {
        using var queue = new GameCommandQueue();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.ExecuteAsync<string>("failing", _ => throw new InvalidOperationException("test error")));
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesGateAfterException()
    {
        using var queue = new GameCommandQueue();

        // First operation throws
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.ExecuteAsync<string>("failing", _ => throw new InvalidOperationException("test error")));

        // Second operation should still execute
        var result = await queue.ExecuteAsync("recovery", _ => Task.FromResult("recovered"));
        Assert.Equal("recovered", result);
    }
}
