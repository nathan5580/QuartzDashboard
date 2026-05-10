using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Represents a request to trigger a job manually.
/// </summary>
public sealed record TriggerJobRequest
{
    /// <summary>
    /// Gets the transient job data map values supplied for the manual trigger.
    /// </summary>
    [JsonPropertyName("dataMap")]
    public Dictionary<string, string?>? DataMap { get; init; }
}
