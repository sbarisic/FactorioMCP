using FactorioMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FactorioMCP.Tools;

/// <summary>
/// MCP tools for managing AI goals. Goals help the AI track what it's working toward,
/// maintain progress through ordered steps, and resume after interruptions.
/// Only one goal can be active at a time. Goals persist across server restarts.
/// </summary>
[McpServerToolType]
internal sealed class GoalTools(GoalPlannerService goals)
{
    [McpServerTool, Description(
        "Create a new goal with a description and optional ordered steps. " +
        "If no goal is currently active, the new goal is automatically activated. " +
        "Only one goal can be active at a time — if a goal is already active, " +
        "the new goal will be created with 'pending' status. " +
        "Use this to plan multi-step tasks like 'Build iron smelting setup'.")]
    public Task<string> SetGoal(
        [Description("What the goal aims to achieve (e.g. 'Build iron smelting setup')")]
        string description,
        [Description("Optional ordered list of steps to complete this goal. " +
            "Example: ['Mine stone', 'Craft furnace', 'Place furnace', 'Add fuel']")]
        List<string>? steps = null,
        CancellationToken cancellationToken = default)
    {
        return goals.SetGoalAsync(description, steps, cancellationToken);
    }

    [McpServerTool, Description(
        "Get the currently active goal with full step details. " +
        "Use this to check what you're supposed to be working on, " +
        "especially after an interruption or at the start of a session. " +
        "Shows the current step in progress, completed steps, and remaining work.")]
    public Task<string> GetActiveGoal(CancellationToken cancellationToken = default)
    {
        return goals.GetActiveGoalAsync(cancellationToken);
    }

    [McpServerTool, Description(
        "Get a summary of all goals including completed, failed, suspended, and pending goals. " +
        "Useful for reviewing history and finding suspended goals that can be resumed.")]
    public Task<string> GetAllGoals(CancellationToken cancellationToken = default)
    {
        return goals.GetAllGoalsAsync(cancellationToken);
    }

    [McpServerTool, Description(
        "Mark the current step as completed and advance to the next step. " +
        "Call this after finishing a step to move to the next one. " +
        "If all steps are completed, the response will indicate the goal is ready for completion. " +
        "Optionally add notes about what was accomplished.")]
    public Task<string> AdvanceGoalStep(
        [Description("Optional notes about what was accomplished in this step")]
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return goals.AdvanceGoalStepAsync(notes, cancellationToken);
    }

    [McpServerTool, Description(
        "Add new steps to the active goal. Use this when a plan needs to evolve — " +
        "for example, when you discover additional work is needed. " +
        "New steps are added to the end of the step list.")]
    public Task<string> AddGoalSteps(
        [Description("List of step descriptions to add to the active goal")]
        List<string> steps,
        CancellationToken cancellationToken = default)
    {
        return goals.AddGoalStepsAsync(steps, cancellationToken);
    }

    [McpServerTool, Description(
        "Mark the active goal as completed. Any remaining in-progress steps are " +
        "marked completed and pending steps are skipped. " +
        "Use this when the overall objective has been achieved.")]
    public Task<string> CompleteGoal(
        [Description("Optional notes about the completed goal")]
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return goals.CompleteGoalAsync(notes, cancellationToken);
    }

    [McpServerTool, Description(
        "Mark the active goal as failed with a reason. " +
        "Use this when the goal cannot be completed — for example, " +
        "missing resources, blocked by enemies, or impossible to achieve.")]
    public Task<string> FailGoal(
        [Description("Reason why the goal failed")]
        string reason,
        CancellationToken cancellationToken = default)
    {
        return goals.FailGoalAsync(reason, cancellationToken);
    }

    [McpServerTool, Description(
        "Suspend the active goal to work on something more urgent. " +
        "The goal's current progress is preserved and can be resumed later " +
        "with ResumeGoal. Use this when interrupted by a higher-priority task.")]
    public Task<string> SuspendGoal(CancellationToken cancellationToken = default)
    {
        return goals.SuspendGoalAsync(cancellationToken);
    }

    [McpServerTool, Description(
        "Resume a previously suspended goal. The goal must be in 'suspended' state, " +
        "and no other goal can be currently active. Use GetAllGoals to find the goal ID of " +
        "the suspended goal you want to resume.")]
    public Task<string> ResumeGoal(
        [Description("The ID of the suspended goal to resume (from GetAllGoals)")]
        string goalId,
        CancellationToken cancellationToken = default)
    {
        return goals.ResumeGoalAsync(goalId, cancellationToken);
    }
}
