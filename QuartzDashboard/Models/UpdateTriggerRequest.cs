using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Request to update an existing trigger schedule.
/// </summary>
public sealed record UpdateTriggerRequest
{
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; init; }

    [JsonPropertyName("intervalSeconds")]
    public int? IntervalSeconds { get; init; }

    [JsonPropertyName("misfireInstruction")]
    public string? MisfireInstruction { get; init; }
}
