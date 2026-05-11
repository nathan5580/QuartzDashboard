namespace QuartzDashboard.Abstractions;

/// <summary>
/// Represents a single recorded Quartz job execution.
/// </summary>
public sealed record FireRecord
{
    /// <summary>
    /// Gets or sets the fully qualified Quartz job key in <c>group.name</c> format.
    /// </summary>
    public string JobKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the fully qualified Quartz trigger key in <c>group.name</c> format.
    /// </summary>
    public string TriggerKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the UTC time when the execution started.
    /// </summary>
    public DateTimeOffset FireTime { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the Quartz refire count for the execution.
    /// </summary>
    public int RefireCount { get; set; }

    /// <summary>
    /// Gets or sets the captured exception message for a failed execution, if available.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Gets or sets the captured exception type for a failed execution, if available.
    /// </summary>
    public string? ExceptionType { get; set; }
}
