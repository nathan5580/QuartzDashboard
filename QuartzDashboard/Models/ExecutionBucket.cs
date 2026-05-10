namespace QuartzDashboard.Models;

/// <summary>
/// Represents aggregated execution statistics for a single minute bucket.
/// </summary>
public sealed record ExecutionBucket
{
    /// <summary>
    /// Gets or sets the minute bucket timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the number of executions recorded in the bucket.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the cumulative execution duration, in milliseconds.
    /// </summary>
    public double TotalDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions recorded in the bucket.
    /// </summary>
    public int ErrorCount { get; set; }
}
