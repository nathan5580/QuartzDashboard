namespace QuartzDashboard.Models;

/// <summary>
/// Aggregated execution statistics for a single minute bucket.
/// </summary>
public sealed record ExecutionBucket
{
    /// <summary>Bucket timestamp (rounded to minute).</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Number of executions in this minute.</summary>
    public int ExecutionCount { get; set; }

    /// <summary>Cumulative duration in milliseconds.</summary>
    public double TotalDurationMs { get; set; }

    /// <summary>Number of failed executions.</summary>
    public int ErrorCount { get; set; }
}
