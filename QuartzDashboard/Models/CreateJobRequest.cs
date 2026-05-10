using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Represents a request to create a Quartz job.
/// </summary>
public sealed record CreateJobRequest
{
    /// <summary>
    /// Gets the job name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the job group name.
    /// </summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>
    /// Gets an optional job description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the fully qualified job type name.
    /// </summary>
    [JsonPropertyName("jobType")]
    public string? JobType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the job should remain stored without active triggers.
    /// </summary>
    [JsonPropertyName("isDurable")]
    public bool IsDurable { get; init; }

    /// <summary>
    /// Gets a value indicating whether Quartz should persist the job data map after execution.
    /// </summary>
    [JsonPropertyName("persistJobDataAfterExecution")]
    public bool PersistJobDataAfterExecution { get; init; }

    /// <summary>
    /// Gets a value indicating whether concurrent executions of the job are disallowed.
    /// </summary>
    [JsonPropertyName("disallowConcurrentExecution")]
    public bool DisallowConcurrentExecution { get; init; }
}
