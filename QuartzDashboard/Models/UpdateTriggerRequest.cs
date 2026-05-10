using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Represents a request to update an existing Quartz trigger.
/// </summary>
public sealed record UpdateTriggerRequest
{
    /// <summary>
    /// Gets the cron expression to apply when updating a cron trigger.
    /// </summary>
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; init; }

    /// <summary>
    /// Gets the interval, in seconds, to apply when updating a simple trigger.
    /// </summary>
    [JsonPropertyName("intervalSeconds")]
    public int? IntervalSeconds { get; init; }

    /// <summary>
    /// Gets the misfire instruction to apply to the trigger.
    /// </summary>
    [JsonPropertyName("misfireInstruction")]
    public string? MisfireInstruction { get; init; }
}
