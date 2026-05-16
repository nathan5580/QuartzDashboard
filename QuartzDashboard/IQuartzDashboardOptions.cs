using Microsoft.AspNetCore.Http;

namespace QuartzDashboard;

/// <summary>
/// Read-only view of <see cref="QuartzDashboardOptions"/>. Handlers and external integrations
/// should depend on this interface rather than the mutable concrete class — it prevents
/// accidental runtime mutation of the configured options after registration.
/// </summary>
public interface IQuartzDashboardOptions
{
    /// <summary>Base path where the dashboard is served.</summary>
    string Path { get; }

    /// <summary>Whether the dashboard is enabled (false = <c>UseQuartzDashboard()</c> is a no-op).</summary>
    bool Enabled { get; }

    /// <summary>Whether all mutating actions are disabled.</summary>
    bool ReadOnly { get; }

    /// <summary>Whether SignalR is used for real-time updates.</summary>
    bool UseSignalR { get; }

    /// <summary>Whether authentication is required for dashboard routes.</summary>
    bool RequireAuthentication { get; }

    /// <summary>Whether mutating endpoints require a CSRF guard header (<c>X-Requested-With</c> or <c>X-CSRF-Token</c>).</summary>
    bool RequireCsrfHeader { get; }

    /// <summary>The role whitelist (only honored when <see cref="RequireAuthentication"/> is true and no policy is set).</summary>
    IReadOnlyList<string> AllowedRoles { get; }

    /// <summary>The authorization policy that must succeed for access.</summary>
    string RequiredPolicy { get; }

    /// <summary>Maximum number of fire history records the default store retains.</summary>
    int MaxFireHistory { get; }

    /// <summary>Maximum number of execution log entries retained per job.</summary>
    int MaxExecutionLogsPerJob { get; }

    /// <summary>Optional callback invoked for every dashboard request. Return <see langword="false"/> to reject.</summary>
    Func<HttpContext, bool>? OnAuthorize { get; }

    /// <summary>The title shown in the dashboard UI.</summary>
    string Title { get; }

    /// <summary>Hours that fire history records are retained.</summary>
    int HistoryRetentionHours { get; }

    /// <summary>Path to a JSON file used for cross-restart history persistence (if configured).</summary>
    string? PersistHistoryPath { get; }

    /// <summary>Optional callback invoked when a job execution fails.</summary>
    Func<string, Exception, Task>? OnJobFailed { get; }

    /// <summary>Optional webhook URL receiving a JSON payload on job failure.</summary>
    string? WebhookUrl { get; }
}
