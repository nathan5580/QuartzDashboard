using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Represents a request to create a Quartz trigger.
/// </summary>
public sealed record CreateTriggerRequest
{
    /// <summary>
    /// Gets the trigger name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the trigger group name.
    /// </summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>
    /// Gets the name of the job targeted by the trigger.
    /// </summary>
    [JsonPropertyName("jobName")]
    public string JobName { get; init; } = "";

    /// <summary>
    /// Gets the group of the job targeted by the trigger.
    /// </summary>
    [JsonPropertyName("jobGroup")]
    public string? JobGroup { get; init; }

    /// <summary>
    /// Gets an optional trigger description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the cron expression used for cron triggers.
    /// </summary>
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; init; }

    /// <summary>
    /// Gets the interval, in seconds, used for simple triggers.
    /// </summary>
    [JsonPropertyName("intervalSeconds")]
    public int? IntervalSeconds { get; init; }

    /// <summary>
    /// Gets the repeat count for simple triggers, where <c>-1</c> means repeat forever.
    /// </summary>
    [JsonPropertyName("repeatCount")]
    public int? RepeatCount { get; init; }

    /// <summary>
    /// Gets the Quartz trigger priority.
    /// </summary>
    [JsonPropertyName("priority")]
    public int? Priority { get; init; }

    /// <summary>
    /// Gets the UTC time when the trigger becomes active.
    /// </summary>
    [JsonPropertyName("startTimeUtc")]
    public DateTimeOffset? StartTimeUtc { get; init; }

    /// <summary>
    /// Gets the UTC time when the trigger expires.
    /// </summary>
    [JsonPropertyName("endTimeUtc")]
    public DateTimeOffset? EndTimeUtc { get; init; }

    /// <summary>
    /// Gets the misfire instruction for the trigger.
    /// </summary>
    [JsonPropertyName("misfireInstruction")]
    public string? MisfireInstruction { get; init; }

    /// <summary>
    /// Gets the optional calendar associated with the trigger.
    /// </summary>
    [JsonPropertyName("calendarName")]
    public string? CalendarName { get; init; }
}
