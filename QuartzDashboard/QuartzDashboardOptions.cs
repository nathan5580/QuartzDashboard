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
    public int MaxFireHistory { get; set; } = 100;

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
}
