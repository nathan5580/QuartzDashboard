using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Represents a request to update a job's data map.
/// </summary>
public sealed record UpdateJobRequest
{
    /// <summary>
    /// Gets the key-value pairs merged into the existing Quartz job data map.
    /// </summary>
    [JsonPropertyName("jobDataMap")]
    public Dictionary<string, string>? JobDataMap { get; init; }
}
