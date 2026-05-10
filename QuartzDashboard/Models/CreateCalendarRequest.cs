using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Represents a request to create a Quartz calendar.
/// </summary>
public sealed record CreateCalendarRequest
{
    /// <summary>
    /// Gets the unique calendar name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the calendar type, such as <c>holiday</c>, <c>monthly</c>, <c>weekly</c>, <c>daily</c>, <c>cron</c>, or <c>annual</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets an optional calendar description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the cron expression used when creating a cron calendar.
    /// </summary>
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; init; }

    /// <summary>
    /// Gets the time zone identifier applied to the calendar.
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    /// <summary>
    /// Gets the optional base calendar name used for calendar composition.
    /// </summary>
    [JsonPropertyName("baseCalendarName")]
    public string? BaseCalendarName { get; init; }
}
