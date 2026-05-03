using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Request to create a new Quartz calendar.
/// </summary>
public sealed record CreateCalendarRequest
{
    /// <summary>Calendar name (required).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// Calendar type: "holiday", "monthly", "weekly", "daily", "cron", "annual".
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Optional description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Cron expression (required for "cron" type).</summary>
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; init; }

    /// <summary>Timezone (optional).</summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    /// <summary>Base calendar name to compose with.</summary>
    [JsonPropertyName("baseCalendarName")]
    public string? BaseCalendarName { get; init; }
}
