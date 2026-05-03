using System.Text.Json.Serialization;

namespace QuartzDashboard.Models;

/// <summary>
/// Request to create a new job in the scheduler.
/// </summary>
public sealed record CreateJobRequest
{
    /// <summary>Job name (required).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>Job group (default: "DEFAULT").</summary>
    [JsonPropertyName("group")]
    public string? Group { get; init; }

    /// <summary>Optional description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Fully-qualified IJob type name (e.g. "MyApp.Jobs.MyJob").</summary>
    [JsonPropertyName("jobType")]
    public string? JobType { get; init; }

    /// <summary>Whether the job should be durable (survive without triggers).</summary>
    [JsonPropertyName("isDurable")]
    public bool IsDurable { get; init; }

    /// <summary>Persist JobDataMap after execution.</summary>
    [JsonPropertyName("persistJobDataAfterExecution")]
    public bool PersistJobDataAfterExecution { get; init; }

    /// <summary>Disallow concurrent execution of this job.</summary>
    [JsonPropertyName("disallowConcurrentExecution")]
    public bool DisallowConcurrentExecution { get; init; }
}
