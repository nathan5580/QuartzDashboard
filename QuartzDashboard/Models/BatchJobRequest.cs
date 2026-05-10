using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Represents a request to perform a batch operation on multiple jobs.
/// </summary>
public sealed record BatchJobRequest
{
    /// <summary>
    /// Gets the job keys to operate on, expressed in <c>group.name</c> format.
    /// </summary>
    [JsonPropertyName("jobs")]
    public string[] Jobs { get; init; } = [];
}
