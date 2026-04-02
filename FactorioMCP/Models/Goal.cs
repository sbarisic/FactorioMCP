namespace FactorioMCP.Models;

/// <summary>
/// Lifecycle state of a goal.
/// </summary>
internal enum GoalStatus
{
    Pending,
    Active,
    Suspended,
    Completed,
    Failed
}

/// <summary>
/// Lifecycle state of a goal step.
/// </summary>
internal enum StepStatus
{
    Pending,
    InProgress,
    Completed,
    Skipped
}

/// <summary>
/// A sub-task within a goal. Steps are executed sequentially —
/// only one step is <see cref="StepStatus.InProgress"/> at a time.
/// </summary>
internal sealed class GoalStep
{
    public required string Description { get; set; }
    public StepStatus Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// A high-level objective the AI is working toward, composed of ordered steps.
/// Only one goal may be <see cref="GoalStatus.Active"/> at any time.
/// </summary>
internal sealed class Goal
{
    public required string Id { get; init; }
    public required string Description { get; set; }
    public GoalStatus Status { get; set; }
    public List<GoalStep> Steps { get; init; } = [];
    public string? FailureReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
