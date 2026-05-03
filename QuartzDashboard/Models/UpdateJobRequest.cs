using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Request to update an existing job's JobDataMap.
/// </summary>
public sealed record UpdateJobRequest
{
    /// <summary>Key-value pairs to merge into the job's JobDataMap.</summary>
    [JsonPropertyName("jobDataMap")]
    public Dictionary<string, string>? JobDataMap { get; init; }
}
