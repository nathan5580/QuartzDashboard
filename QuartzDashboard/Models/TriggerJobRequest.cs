using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Request to manually trigger a job with an optional transient data map.
/// </summary>
public sealed record TriggerJobRequest
{
    [JsonPropertyName("dataMap")]
    public Dictionary<string, string?>? DataMap { get; init; }
}
