using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Request for batch job operations (pause, resume, trigger, delete).
/// </summary>
public sealed record BatchJobRequest
{
    /// <summary>Array of job keys in "Group.Name" format.</summary>
    [JsonPropertyName("jobs")]
    public string[] Jobs { get; init; } = [];
}
