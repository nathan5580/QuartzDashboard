namespace QuartzDashboard.Abstractions;

/// <summary>
/// Represents a single recorded Quartz job execution.
/// </summary>
public sealed record FireRecord
{
    /// <summary>
    /// Gets the fully qualified Quartz job key in <c>group.name</c> format.
    /// </summary>
    public string JobKey { get; init; } = "";

    /// <summary>
    /// Gets the fully qualified Quartz trigger key in <c>group.name</c> format.
    /// </summary>
    public string TriggerKey { get; init; } = "";

    /// <summary>
    /// Gets the UTC time when the execution started.
    /// </summary>
    public DateTimeOffset FireTime { get; init; }

    /// <summary>
    /// Gets the total duration of the execution.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets a value indicating whether the execution completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the Quartz refire count for the execution.
    /// </summary>
    public int RefireCount { get; init; }

    /// <summary>
    /// Gets the captured exception message for a failed execution, if available.
    /// </summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>
    /// Gets the captured exception type for a failed execution, if available.
    /// </summary>
    public string? ExceptionType { get; init; }
}
