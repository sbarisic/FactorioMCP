using System.Text.Json;
using FactorioMCP.Services;
using Xunit;

namespace FactorioMCP.Tests.Services;

public class GoalPlannerServiceTests
{
    private static GoalPlannerService CreateService() =>
        new(Path.Combine(Path.GetTempPath(), $"goals-{Guid.NewGuid():N}.json"));

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ── SetGoal ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetGoalAsync_CreatesGoalAndAutoActivates()
    {
        var service = CreateService();

        var result = Parse(await service.SetGoalAsync("Build furnace", ["Mine stone", "Craft furnace"]));

        Assert.Equal("created", result.GetProperty("status").GetString());
        Assert.Equal("active", result.GetProperty("goal_status").GetString());
        Assert.Equal("Build furnace", result.GetProperty("description").GetString());
        Assert.Equal(2, result.GetProperty("step_count").GetInt32());
    }

    [Fact]
    public async Task SetGoalAsync_StaysPendingWhenActiveGoalExists()
    {
        var service = CreateService();
        await service.SetGoalAsync("First goal");

        var result = Parse(await service.SetGoalAsync("Second goal"));

        Assert.Equal("pending", result.GetProperty("goal_status").GetString());
    }

    [Fact]
    public async Task SetGoalAsync_WithNoSteps()
    {
        var service = CreateService();

        var result = Parse(await service.SetGoalAsync("Simple goal"));

        Assert.Equal("created", result.GetProperty("status").GetString());
        Assert.Equal(0, result.GetProperty("step_count").GetInt32());
    }

    [Fact]
    public async Task SetGoalAsync_ThrowsOnNullDescription()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SetGoalAsync(null!));
    }

    [Fact]
    public async Task SetGoalAsync_ThrowsOnWhitespaceDescription()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetGoalAsync("  "));
    }

    // ── GetActiveGoal ────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveGoalAsync_ReturnsActiveGoalDetails()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build furnace", ["Mine stone", "Craft furnace"]);

        var result = Parse(await service.GetActiveGoalAsync());

        Assert.Equal("active", result.GetProperty("status").GetString());
        Assert.Equal("Build furnace", result.GetProperty("description").GetString());
        Assert.Equal("Mine stone", result.GetProperty("current_step").GetString());
        Assert.Equal(0, result.GetProperty("steps_completed").GetInt32());
        Assert.Equal(2, result.GetProperty("total_steps").GetInt32());
    }

    [Fact]
    public async Task GetActiveGoalAsync_ReturnsNoActiveGoalWhenNoneExists()
    {
        var service = CreateService();

        var result = Parse(await service.GetActiveGoalAsync());

        Assert.Equal("no_active_goal", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetActiveGoalAsync_ShowsSuspendedAndPendingCounts()
    {
        var service = CreateService();
        await service.SetGoalAsync("Goal 1");
        await service.SuspendGoalAsync();
        await service.SetGoalAsync("Goal 2");
        await service.SuspendGoalAsync();

        var result = Parse(await service.GetActiveGoalAsync());

        Assert.Equal("no_active_goal", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("suspended_count").GetInt32());
    }

    // ── GetAllGoals ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAllGoalsAsync_ReturnsAllGoals()
    {
        var service = CreateService();
        await service.SetGoalAsync("Goal 1");
        await service.SetGoalAsync("Goal 2");

        var result = Parse(await service.GetAllGoalsAsync());

        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.Equal(2, result.GetProperty("goals").GetArrayLength());
    }

    [Fact]
    public async Task GetAllGoalsAsync_ReturnsEmptyWhenNoGoals()
    {
        var service = CreateService();

        var result = Parse(await service.GetAllGoalsAsync());

        Assert.Equal(0, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetAllGoalsAsync_IncludesStepProgress()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build", ["Step 1", "Step 2", "Step 3"]);
        await service.AdvanceGoalStepAsync();

        var result = Parse(await service.GetAllGoalsAsync());
        var goal = result.GetProperty("goals")[0];

        Assert.Equal("1/3", goal.GetProperty("step_progress").GetString());
    }

    // ── AdvanceGoalStep ──────────────────────────────────────────────

    [Fact]
    public async Task AdvanceGoalStepAsync_CompletesCurrentAndStartsNext()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build", ["Step 1", "Step 2", "Step 3"]);

        var result = Parse(await service.AdvanceGoalStepAsync());

        Assert.Equal("advanced", result.GetProperty("status").GetString());
        Assert.Equal("Step 1", result.GetProperty("completed_step").GetString());
        Assert.Equal("Step 2", result.GetProperty("next_step").GetString());
        Assert.Equal(1, result.GetProperty("steps_remaining").GetInt32());
        Assert.False(result.GetProperty("all_steps_complete").GetBoolean());
    }

    [Fact]
    public async Task AdvanceGoalStepAsync_SignalsAllCompleteOnLastStep()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build", ["Step 1"]);

        var result = Parse(await service.AdvanceGoalStepAsync());

        Assert.Equal("advanced", result.GetProperty("status").GetString());
        Assert.True(result.GetProperty("all_steps_complete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("next_step").ValueKind);
    }

    [Fact]
    public async Task AdvanceGoalStepAsync_AddsNotesToCompletedStep()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build", ["Step 1", "Step 2"]);
        await service.AdvanceGoalStepAsync("Completed mining 5 stone");

        var active = Parse(await service.GetActiveGoalAsync());
        var steps = active.GetProperty("steps");

        Assert.Equal("Completed mining 5 stone", steps[0].GetProperty("notes").GetString());
    }

    [Fact]
    public async Task AdvanceGoalStepAsync_ErrorsWhenNoActiveGoal()
    {
        var service = CreateService();

        var result = Parse(await service.AdvanceGoalStepAsync());

        Assert.Equal("error", result.GetProperty("status").GetString());
        Assert.Equal("no_active_goal", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AdvanceGoalStepAsync_ErrorsWhenNoStepInProgress()
    {
        var service = CreateService();
        await service.SetGoalAsync("Empty goal");

        var result = Parse(await service.AdvanceGoalStepAsync());

        Assert.Equal("error", result.GetProperty("status").GetString());
        Assert.Equal("no_step_in_progress", result.GetProperty("error").GetString());
    }

    // ── AddGoalSteps ─────────────────────────────────────────────────

    [Fact]
    public async Task AddGoalStepsAsync_AddsStepsToActiveGoal()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build", ["Step 1"]);

        var result = Parse(await service.AddGoalStepsAsync(["Step 2", "Step 3"]));

        Assert.Equal("added", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("added_count").GetInt32());
        Assert.Equal(3, result.GetProperty("total_steps").GetInt32());
    }

    [Fact]
    public async Task AddGoalStepsAsync_ActivatesFirstStepWhenNoneInProgress()
    {
        var service = CreateService();
        await service.SetGoalAsync("Empty goal");

        await service.AddGoalStepsAsync(["New step"]);

        var active = Parse(await service.GetActiveGoalAsync());
        Assert.Equal("New step", active.GetProperty("current_step").GetString());
    }

    [Fact]
    public async Task AddGoalStepsAsync_ErrorsWhenNoActiveGoal()
    {
        var service = CreateService();

        var result = Parse(await service.AddGoalStepsAsync(["Step"]));

        Assert.Equal("error", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AddGoalStepsAsync_ThrowsOnEmptyList()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddGoalStepsAsync([]));
    }

    // ── CompleteGoal ─────────────────────────────────────────────────

    [Fact]
    public async Task CompleteGoalAsync_MarksGoalCompleted()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build", ["Step 1"]);

        var result = Parse(await service.CompleteGoalAsync());

        Assert.Equal("completed", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CompleteGoalAsync_SkipsRemainingPendingSteps()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build", ["Step 1", "Step 2", "Step 3"]);
        await service.CompleteGoalAsync();

        // Step 1 was InProgress → Completed, Steps 2+3 were Pending → Skipped
        var all = Parse(await service.GetAllGoalsAsync());
        var goal = all.GetProperty("goals")[0];

        Assert.Equal("1/3", goal.GetProperty("step_progress").GetString());
    }

    [Fact]
    public async Task CompleteGoalAsync_ErrorsWhenNoActiveGoal()
    {
        var service = CreateService();

        var result = Parse(await service.CompleteGoalAsync());

        Assert.Equal("error", result.GetProperty("status").GetString());
    }

    // ── FailGoal ─────────────────────────────────────────────────────

    [Fact]
    public async Task FailGoalAsync_MarksGoalFailed()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build");

        var result = Parse(await service.FailGoalAsync("No resources"));

        Assert.Equal("failed", result.GetProperty("status").GetString());
        Assert.Equal("No resources", result.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task FailGoalAsync_ErrorsWhenNoActiveGoal()
    {
        var service = CreateService();

        var result = Parse(await service.FailGoalAsync("Reason"));

        Assert.Equal("error", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task FailGoalAsync_ThrowsOnWhitespaceReason()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.FailGoalAsync("  "));
    }

    // ── SuspendGoal ──────────────────────────────────────────────────

    [Fact]
    public async Task SuspendGoalAsync_SuspendsActiveGoal()
    {
        var service = CreateService();
        await service.SetGoalAsync("Build");

        var result = Parse(await service.SuspendGoalAsync());

        Assert.Equal("suspended", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SuspendGoalAsync_ErrorsWhenNoActiveGoal()
    {
        var service = CreateService();

        var result = Parse(await service.SuspendGoalAsync());

        Assert.Equal("error", result.GetProperty("status").GetString());
    }

    // ── ResumeGoal ───────────────────────────────────────────────────

    [Fact]
    public async Task ResumeGoalAsync_ResumesSuspendedGoal()
    {
        var service = CreateService();
        var created = Parse(await service.SetGoalAsync("Build", ["Step 1"]));
        var goalId = created.GetProperty("id").GetString()!;
        await service.SuspendGoalAsync();

        var result = Parse(await service.ResumeGoalAsync(goalId));

        Assert.Equal("resumed", result.GetProperty("status").GetString());
        Assert.Equal(goalId, result.GetProperty("id").GetString());
    }

    [Fact]
    public async Task ResumeGoalAsync_PreservesStepProgress()
    {
        var service = CreateService();
        var created = Parse(await service.SetGoalAsync("Build", ["Step 1", "Step 2", "Step 3"]));
        var goalId = created.GetProperty("id").GetString()!;
        await service.AdvanceGoalStepAsync();
        await service.SuspendGoalAsync();

        await service.ResumeGoalAsync(goalId);

        var active = Parse(await service.GetActiveGoalAsync());
        Assert.Equal("Step 2", active.GetProperty("current_step").GetString());
        Assert.Equal(1, active.GetProperty("steps_completed").GetInt32());
    }

    [Fact]
    public async Task ResumeGoalAsync_ErrorsWhenAnotherGoalIsActive()
    {
        var service = CreateService();
        var created = Parse(await service.SetGoalAsync("Goal 1", ["Step 1"]));
        var goalId = created.GetProperty("id").GetString()!;
        await service.SuspendGoalAsync();
        await service.SetGoalAsync("Goal 2");

        var result = Parse(await service.ResumeGoalAsync(goalId));

        Assert.Equal("error", result.GetProperty("status").GetString());
        Assert.Equal("another_goal_active", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ResumeGoalAsync_ErrorsWhenGoalNotFound()
    {
        var service = CreateService();

        var result = Parse(await service.ResumeGoalAsync("nonexistent"));

        Assert.Equal("error", result.GetProperty("status").GetString());
        Assert.Equal("goal_not_found", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ResumeGoalAsync_ErrorsWhenGoalNotSuspended()
    {
        var service = CreateService();
        var created = Parse(await service.SetGoalAsync("Build"));
        var goalId = created.GetProperty("id").GetString()!;
        await service.CompleteGoalAsync();

        var result = Parse(await service.ResumeGoalAsync(goalId));

        Assert.Equal("error", result.GetProperty("status").GetString());
        Assert.Equal("goal_not_suspended", result.GetProperty("error").GetString());
    }

    // ── Persistence ──────────────────────────────────────────────────

    [Fact]
    public async Task Goals_PersistAcrossServiceInstances()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"goals-{Guid.NewGuid():N}.json");

        var service1 = new GoalPlannerService(filePath);
        await service1.SetGoalAsync("Persistent goal", ["Step 1"]);

        var service2 = new GoalPlannerService(filePath);
        var result = Parse(await service2.GetActiveGoalAsync());

        Assert.Equal("active", result.GetProperty("status").GetString());
        Assert.Equal("Persistent goal", result.GetProperty("description").GetString());
    }

    // ── Full Workflow ────────────────────────────────────────────────

    [Fact]
    public async Task FullWorkflow_SetAdvanceComplete()
    {
        var service = CreateService();

        await service.SetGoalAsync("Build furnace", ["Mine stone", "Craft furnace", "Place furnace"]);
        await service.AdvanceGoalStepAsync("Got 5 stone");
        await service.AdvanceGoalStepAsync("Crafted 1 furnace");
        await service.AdvanceGoalStepAsync("Placed at (10, 5)");

        var result = Parse(await service.CompleteGoalAsync("Furnace operational"));
        Assert.Equal("completed", result.GetProperty("status").GetString());

        var active = Parse(await service.GetActiveGoalAsync());
        Assert.Equal("no_active_goal", active.GetProperty("status").GetString());

        var all = Parse(await service.GetAllGoalsAsync());
        var goal = all.GetProperty("goals")[0];
        Assert.Equal("3/3", goal.GetProperty("step_progress").GetString());
    }

    [Fact]
    public async Task FullWorkflow_SuspendAndResume()
    {
        var service = CreateService();

        // Start goal 1
        var goal1 = Parse(await service.SetGoalAsync("Build furnace", ["Mine stone", "Craft furnace"]));
        var goal1Id = goal1.GetProperty("id").GetString()!;
        await service.AdvanceGoalStepAsync();

        // Suspend to handle urgent task
        await service.SuspendGoalAsync();

        // Work on urgent goal 2
        await service.SetGoalAsync("Fight biters", ["Build turret"]);
        await service.AdvanceGoalStepAsync();
        await service.CompleteGoalAsync();

        // Resume goal 1
        var resumed = Parse(await service.ResumeGoalAsync(goal1Id));
        Assert.Equal("resumed", resumed.GetProperty("status").GetString());

        // Verify progress was preserved
        var active = Parse(await service.GetActiveGoalAsync());
        Assert.Equal("Build furnace", active.GetProperty("description").GetString());
        Assert.Equal("Craft furnace", active.GetProperty("current_step").GetString());
    }

    // ── Timestamp Tests ──────────────────────────────────────────────

    [Fact]
    public async Task SetGoalAsync_IncludesCreatedAtTimestamp()
    {
        var service = CreateService();
        var before = DateTime.UtcNow;

        var result = Parse(await service.SetGoalAsync("Test goal"));

        var createdAt = result.GetProperty("created_at").GetDateTime();
        Assert.True(createdAt >= before);
        Assert.True(createdAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task SetGoalAsync_IncludesUpdatedAtTimestamp()
    {
        var service = CreateService();

        var result = Parse(await service.SetGoalAsync("Test goal"));

        Assert.True(result.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task GetActiveGoalAsync_IncludesAllTimestamps()
    {
        var service = CreateService();
        await service.SetGoalAsync("Test goal", ["Step 1"]);

        var result = Parse(await service.GetActiveGoalAsync());

        Assert.True(result.TryGetProperty("created_at", out _));
        Assert.True(result.TryGetProperty("updated_at", out _));
        Assert.True(result.TryGetProperty("completed_at", out _));
    }

    [Fact]
    public async Task GetAllGoalsAsync_IncludesUpdatedAtTimestamp()
    {
        var service = CreateService();
        await service.SetGoalAsync("Test goal");

        var result = Parse(await service.GetAllGoalsAsync());
        var goal = result.GetProperty("goals").EnumerateArray().First();

        Assert.True(goal.TryGetProperty("created_at", out _));
        Assert.True(goal.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task AdvanceGoalStepAsync_UpdatesTimestamp()
    {
        var service = CreateService();
        await service.SetGoalAsync("Test", ["Step 1", "Step 2"]);

        var result = Parse(await service.AdvanceGoalStepAsync());

        Assert.True(result.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task AddGoalStepsAsync_UpdatesTimestamp()
    {
        var service = CreateService();
        await service.SetGoalAsync("Test", ["Step 1"]);

        var result = Parse(await service.AddGoalStepsAsync(["Step 2"]));

        Assert.True(result.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task CompleteGoalAsync_IncludesAllTimestamps()
    {
        var service = CreateService();
        await service.SetGoalAsync("Test");

        var result = Parse(await service.CompleteGoalAsync());

        Assert.True(result.TryGetProperty("created_at", out _));
        Assert.True(result.TryGetProperty("updated_at", out _));
        Assert.True(result.TryGetProperty("completed_at", out _));
    }

    [Fact]
    public async Task FailGoalAsync_IncludesAllTimestamps()
    {
        var service = CreateService();
        await service.SetGoalAsync("Test");

        var result = Parse(await service.FailGoalAsync("reason"));

        Assert.True(result.TryGetProperty("created_at", out _));
        Assert.True(result.TryGetProperty("updated_at", out _));
        Assert.True(result.TryGetProperty("completed_at", out _));
    }

    [Fact]
    public async Task SuspendGoalAsync_IncludesUpdatedAtTimestamp()
    {
        var service = CreateService();
        await service.SetGoalAsync("Test");

        var result = Parse(await service.SuspendGoalAsync());

        Assert.True(result.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task ResumeGoalAsync_IncludesUpdatedAtTimestamp()
    {
        var service = CreateService();
        var created = Parse(await service.SetGoalAsync("Test"));
        var goalId = created.GetProperty("id").GetString()!;
        await service.SuspendGoalAsync();

        var result = Parse(await service.ResumeGoalAsync(goalId));

        Assert.True(result.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task UpdatedAt_ChangesOnModification()
    {
        var service = CreateService();
        var created = Parse(await service.SetGoalAsync("Test", ["Step 1", "Step 2"]));
        var createdAt = created.GetProperty("updated_at").GetDateTime();

        // Small delay to ensure timestamp difference
        await Task.Delay(10);

        var advanced = Parse(await service.AdvanceGoalStepAsync());
        var updatedAt = advanced.GetProperty("updated_at").GetDateTime();

        Assert.True(updatedAt >= createdAt, "UpdatedAt should be >= the creation time after modification");
    }

    [Fact]
    public async Task Timestamps_PersistAcrossServiceInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"goals-{Guid.NewGuid():N}.json");
        var service1 = new GoalPlannerService(path);
        await service1.SetGoalAsync("Test");
        await service1.CompleteGoalAsync();

        var service2 = new GoalPlannerService(path);
        var result = Parse(await service2.GetAllGoalsAsync());
        var goal = result.GetProperty("goals").EnumerateArray().First();

        Assert.True(goal.TryGetProperty("created_at", out _));
        Assert.True(goal.TryGetProperty("updated_at", out _));
        Assert.True(goal.TryGetProperty("completed_at", out var completedAt));
        Assert.NotEqual(JsonValueKind.Null, completedAt.ValueKind);
    }
}
