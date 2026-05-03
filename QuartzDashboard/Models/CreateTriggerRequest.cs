using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Request to create a new trigger in the scheduler.
/// </summary>
public sealed record CreateTriggerRequest
{
    /// <summary>Trigger name (required).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>Trigger group (default: "DEFAULT").</summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>Target job name (required).</summary>
    [JsonPropertyName("jobName")]
    public string JobName { get; init; } = "";

    /// <summary>Target job group (default: "DEFAULT").</summary>
    [JsonPropertyName("jobGroup")]
    public string? JobGroup { get; init; }

    /// <summary>Optional description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Cron expression (required for cron triggers).</summary>
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; init; }

    /// <summary>Interval in seconds (required for simple triggers).</summary>
    [JsonPropertyName("intervalSeconds")]
    public int? IntervalSeconds { get; init; }

    /// <summary>Repeat count (-1 = infinite).</summary>
    [JsonPropertyName("repeatCount")]
    public int? RepeatCount { get; init; }

    /// <summary>Trigger priority (default: 5).</summary>
    [JsonPropertyName("priority")]
    public int? Priority { get; init; }

    /// <summary>UTC start time.</summary>
    [JsonPropertyName("startTimeUtc")]
    public DateTimeOffset? StartTimeUtc { get; init; }

    /// <summary>UTC end time.</summary>
    [JsonPropertyName("endTimeUtc")]
    public DateTimeOffset? EndTimeUtc { get; init; }

    /// <summary>
    /// Misfire instruction: "fireOnceNow", "doNothing", "ignoreMisfirePolicy", or null for default.
    /// </summary>
    [JsonPropertyName("misfireInstruction")]
    public string? MisfireInstruction { get; init; }

    /// <summary>Calendar name to associate with this trigger.</summary>
    [JsonPropertyName("calendarName")]
    public string? CalendarName { get; init; }
}
