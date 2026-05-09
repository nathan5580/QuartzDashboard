using Microsoft.AspNetCore.Http;

namespace QuartzDashboard;

/// <summary>
/// Options for configuring the Quartz Dashboard middleware.
/// </summary>
public class QuartzDashboardOptions
{
    /// <summary>
    /// The base path where the dashboard is served. Default: "/quartz"
    /// </summary>
    public string Path { get; set; } = "/quartz";

    /// <summary>
    /// Whether the dashboard is enabled at all. When false, UseQuartzDashboard()
    /// is a no-op and no routes are registered. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the dashboard is read-only (no trigger/start/stop/delete buttons).
    /// Default: false
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// Whether to enable SignalR for real-time updates.
    /// When true, the dashboard pushes execution events instantly to connected browsers.
    /// Default: true
    /// </summary>
    public bool UseSignalR { get; set; } = true;

    /// <summary>
    /// Whether to require authentication for all dashboard routes.
    /// When true, unauthenticated requests receive 401.
    /// Default: false
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;

    /// <summary>
    /// Optional roles that are allowed to access the dashboard.
    /// Only checked when RequireAuthentication is true.
    /// When empty, any authenticated user may access.
    /// Default: empty
    /// </summary>
    public string[] AllowedRoles { get; set; } = [];

    /// <summary>
    /// Optional authorization policy name that must be satisfied.
    /// Only checked when RequireAuthentication is true.
    /// When empty, fallback to AllowedRoles check or any authenticated user.
    /// Default: ""
    /// </summary>
    public string RequiredPolicy { get; set; } = "";

    /// <summary>
    /// Maximum number of fire history records to keep in memory.
    /// Only applies when using the default in-memory store.
    /// Default: 100
    /// </summary>
    public int MaxFireHistory { get; set; } = 500;

    /// <summary>
    /// Maximum number of execution log entries per job.
    /// Default: 50
    /// </summary>
    public int MaxExecutionLogsPerJob { get; set; } = 50;

    /// <summary>
    /// Authorization callback invoked on every dashboard request. Return <c>false</c> to reject with 401.
    /// Example: <c>OnAuthorize = ctx => ctx.User.IsInRole("Admin")</c>
    /// </summary>
    public Func<HttpContext, bool>? OnAuthorize { get; set; }

    /// <summary>
    /// When true, uses the system font stack instead of embedded Inter/JetBrains Mono fonts.
    /// Reduces package payload by ~286KB. Default: false
    /// </summary>
    public bool UseSystemFonts { get; set; } = false;

    /// <summary>
    /// Custom title shown in the sidebar header and browser tab.
    /// Default: "QuartzDash"
    /// </summary>
    public string Title { get; set; } = "QuartzDash";

    /// <summary>
    /// Number of hours of fire history to retain. Records older than this are pruned automatically.
    /// Set to 0 to disable TTL pruning (keep all records up to MaxFireHistory).
    /// Default: 24
    /// </summary>
    public int HistoryRetentionHours { get; set; } = 24;

    /// <summary>
    /// Optional file path for persisting fire history to disk as JSON.
    /// When set, history survives application restarts and is loaded on startup.
    /// Example: <c>options.PersistHistoryPath = "quartz-history.json"</c>
    /// Default: null (in-memory only)
    /// </summary>
    public string? PersistHistoryPath { get; set; }

    /// <summary>
    /// Optional async callback invoked every time a job fails (throws an exception during execution).
    /// Useful for Slack alerts, PagerDuty notifications, webhooks, etc.
    /// The first argument is the job key (group.name), the second is the exception.
    /// Example: <c>options.OnJobFailed = async (jobKey, ex) => await notifier.AlertAsync(jobKey, ex);</c>
    /// </summary>
    public Func<string, Exception, Task>? OnJobFailed { get; set; }

    /// <summary>
    /// Optional webhook URL that receives a JSON payload whenever a job execution fails.
    /// Default: null
    /// </summary>
    public string? WebhookUrl { get; set; }
}
