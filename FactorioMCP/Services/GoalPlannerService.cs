using System.Text.Json;
using System.Text.Json.Serialization;
using FactorioMCP.Models;
using Microsoft.Extensions.Configuration;

namespace FactorioMCP.Services;

/// <summary>
/// Manages AI goals with a state machine lifecycle and JSON file persistence.
/// Goals track what the AI is working toward, with ordered steps for progress tracking.
/// Only one goal may be active at a time. State persists across server restarts.
/// </summary>
internal sealed class GoalPlannerService
{
    private static readonly JsonSerializerOptions s_responseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private static readonly JsonSerializerOptions s_fileOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Goal>? _goals;

    public GoalPlannerService(IConfiguration configuration)
    {
        _filePath = configuration["FACTORIO_GOALS_FILE"] ?? "goals.json";
    }

    internal GoalPlannerService(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Creates a new goal with optional steps. Auto-activates if no goal is currently active.
    /// </summary>
    public async Task<string> SetGoalAsync(
        string description,
        List<string>? steps = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);

            var goal = new Goal
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Description = description,
                Steps = steps?.Select(s => new GoalStep { Description = s }).ToList() ?? []
            };

            if (!goals.Exists(g => g.Status == GoalStatus.Active))
            {
                ActivateGoal(goal);
            }

            goals.Add(goal);
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "created",
                goal.Id,
                GoalStatus = goal.Status,
                goal.Description,
                StepCount = goal.Steps.Count,
                goal.CreatedAt,
                goal.UpdatedAt
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the currently active goal with full step details.
    /// </summary>
    public async Task<string> GetActiveGoalAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);
            var active = goals.Find(g => g.Status == GoalStatus.Active);

            if (active is null)
            {
                return Respond(new
                {
                    Status = "no_active_goal",
                    SuspendedCount = goals.Count(g => g.Status == GoalStatus.Suspended),
                    PendingCount = goals.Count(g => g.Status == GoalStatus.Pending)
                });
            }

            return Respond(FormatGoalDetail(active));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns a summary of all goals across all states.
    /// </summary>
    public async Task<string> GetAllGoalsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);

            return Respond(new
            {
                Status = "ok",
                Count = goals.Count,
                Goals = goals.Select(g => new
                {
                    g.Id,
                    g.Description,
                    GoalStatus = g.Status,
                    StepProgress = $"{g.Steps.Count(s => s.Status == StepStatus.Completed)}/{g.Steps.Count}",
                    g.CreatedAt,
                    g.UpdatedAt,
                    g.CompletedAt
                })
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Completes the current in-progress step and advances to the next pending step.
    /// </summary>
    public async Task<string> AdvanceGoalStepAsync(
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);
            var active = goals.Find(g => g.Status == GoalStatus.Active);

            if (active is null)
                return Respond(new { Status = "error", Error = "no_active_goal" });

            var current = active.Steps.Find(s => s.Status == StepStatus.InProgress);

            if (current is null)
                return Respond(new { Status = "error", Error = "no_step_in_progress" });

            current.Status = StepStatus.Completed;
            if (notes is not null)
                current.Notes = notes;

            var next = active.Steps.Find(s => s.Status == StepStatus.Pending);
            if (next is not null)
                next.Status = StepStatus.InProgress;

            active.UpdatedAt = DateTime.UtcNow;
            var remaining = active.Steps.Count(s => s.Status == StepStatus.Pending);
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "advanced",
                CompletedStep = current.Description,
                NextStep = next?.Description,
                StepsRemaining = remaining,
                AllStepsComplete = next is null && remaining == 0,
                active.UpdatedAt
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Adds new steps to the active goal. If no step is currently in progress,
    /// the first new step is automatically started.
    /// </summary>
    public async Task<string> AddGoalStepsAsync(
        List<string> steps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
            throw new ArgumentException("At least one step is required.", nameof(steps));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);
            var active = goals.Find(g => g.Status == GoalStatus.Active);

            if (active is null)
                return Respond(new { Status = "error", Error = "no_active_goal" });

            foreach (var step in steps)
                active.Steps.Add(new GoalStep { Description = step });

            // If no step is in progress, start the first pending one
            if (!active.Steps.Exists(s => s.Status == StepStatus.InProgress))
            {
                var next = active.Steps.Find(s => s.Status == StepStatus.Pending);
                if (next is not null)
                    next.Status = StepStatus.InProgress;
            }

            active.UpdatedAt = DateTime.UtcNow;
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "added",
                AddedCount = steps.Count,
                TotalSteps = active.Steps.Count,
                active.UpdatedAt
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Marks the active goal as completed.
    /// remaining pending steps are marked skipped.
    /// </summary>
    public async Task<string> CompleteGoalAsync(
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);
            var active = goals.Find(g => g.Status == GoalStatus.Active);

            if (active is null)
                return Respond(new { Status = "error", Error = "no_active_goal" });

            active.Status = GoalStatus.Completed;
            active.CompletedAt = DateTime.UtcNow;
            active.UpdatedAt = DateTime.UtcNow;
            if (notes is not null)
                active.Notes = notes;

            foreach (var step in active.Steps.Where(s => s.Status == StepStatus.InProgress))
                step.Status = StepStatus.Completed;

            foreach (var step in active.Steps.Where(s => s.Status == StepStatus.Pending))
                step.Status = StepStatus.Skipped;

            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "completed",
                active.Id,
                active.Description,
                active.CreatedAt,
                active.UpdatedAt,
                active.CompletedAt
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Marks the active goal as failed with a reason.
    /// </summary>
    public async Task<string> FailGoalAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);
            var active = goals.Find(g => g.Status == GoalStatus.Active);

            if (active is null)
                return Respond(new { Status = "error", Error = "no_active_goal" });

            active.Status = GoalStatus.Failed;
            active.FailureReason = reason;
            active.CompletedAt = DateTime.UtcNow;
            active.UpdatedAt = DateTime.UtcNow;
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "failed",
                active.Id,
                active.Description,
                Reason = reason,
                active.CreatedAt,
                active.UpdatedAt,
                active.CompletedAt
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Suspends the active goal so a higher-priority task can be worked on.
    /// Progress is preserved and the goal can be resumed later.
    /// </summary>
    public async Task<string> SuspendGoalAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);
            var active = goals.Find(g => g.Status == GoalStatus.Active);

            if (active is null)
                return Respond(new { Status = "error", Error = "no_active_goal" });

            active.Status = GoalStatus.Suspended;
            active.UpdatedAt = DateTime.UtcNow;
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "suspended",
                active.Id,
                active.Description,
                active.UpdatedAt
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Resumes a previously suspended goal. Fails if another goal is currently active.
    /// </summary>
    public async Task<string> ResumeGoalAsync(
        string goalId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalId);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var goals = await EnsureLoadedAsync(cancellationToken);

            if (goals.Exists(g => g.Status == GoalStatus.Active))
                return Respond(new { Status = "error", Error = "another_goal_active", Message = "Suspend or complete the active goal first" });

            var goal = goals.Find(g => g.Id == goalId);
            if (goal is null)
                return Respond(new { Status = "error", Error = "goal_not_found", GoalId = goalId });

            if (goal.Status != GoalStatus.Suspended)
                return Respond(new { Status = "error", Error = "goal_not_suspended", GoalStatus = goal.Status });

            ActivateGoal(goal);
            goal.UpdatedAt = DateTime.UtcNow;
            await PersistAsync(cancellationToken);

            return Respond(new
            {
                Status = "resumed",
                goal.Id,
                goal.Description,
                goal.UpdatedAt
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void ActivateGoal(Goal goal)
    {
        goal.Status = GoalStatus.Active;

        // Only set a step to InProgress if none is already in progress
        // (preserves step state when resuming a suspended goal)
        if (!goal.Steps.Exists(s => s.Status == StepStatus.InProgress))
        {
            var firstPending = goal.Steps.Find(s => s.Status == StepStatus.Pending);
            if (firstPending is not null)
                firstPending.Status = StepStatus.InProgress;
        }
    }

    private static object FormatGoalDetail(Goal goal)
    {
        return new
        {
            Status = "active",
            goal.Id,
            goal.Description,
            goal.Notes,
            goal.CreatedAt,
            goal.UpdatedAt,
            goal.CompletedAt,
            CurrentStep = goal.Steps.Find(s => s.Status == StepStatus.InProgress)?.Description,
            StepsCompleted = goal.Steps.Count(s => s.Status == StepStatus.Completed),
            TotalSteps = goal.Steps.Count,
            Steps = goal.Steps.Select((s, i) => new
            {
                Index = i,
                s.Description,
                StepStatus = s.Status,
                s.Notes
            })
        };
    }

    private static string Respond(object data) =>
        JsonSerializer.Serialize(data, s_responseOptions);

    // ── Persistence ─────────────────────────────────────────────────

    private async Task<List<Goal>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_goals is not null)
            return _goals;

        if (File.Exists(_filePath))
        {
            await using var stream = File.OpenRead(_filePath);
            _goals = await JsonSerializer.DeserializeAsync<List<Goal>>(stream, s_fileOptions, cancellationToken) ?? [];
        }
        else
        {
            _goals = [];
        }

        return _goals;
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        if (_goals is null) return;

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, _goals, s_fileOptions, cancellationToken);
    }
}
