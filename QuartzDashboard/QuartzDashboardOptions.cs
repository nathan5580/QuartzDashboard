using Microsoft.AspNetCore.Http;

namespace QuartzDashboard;

/// <summary>
/// Options used to configure the Quartz Dashboard middleware and related services.
/// Implements <see cref="IQuartzDashboardOptions"/>, the read-only contract consumed by handlers.
/// </summary>
public class QuartzDashboardOptions : IQuartzDashboardOptions
{
    /// <summary>
    /// Gets or sets the base path where the dashboard is served.
    /// The default value is <c>"/quartz"</c>.
    /// </summary>
    public string Path { get; set; } = "/quartz";

    /// <summary>
    /// Gets or sets a value indicating whether the dashboard is enabled.
    /// When <see langword="false"/>, <c>UseQuartzDashboard()</c> does not register dashboard routes.
    /// The default value is <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the dashboard UI should disable mutating actions.
    /// When enabled, trigger, pause, resume, start, standby, interrupt, and delete actions are blocked.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether SignalR should be enabled for real-time updates.
    /// When disabled, the dashboard still works but refreshes data through HTTP requests only.
    /// The default value is <see langword="true"/>.
    /// </summary>
    public bool UseSignalR { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether authentication is required for all dashboard routes.
    /// Unauthenticated requests receive a 401 response when this option is enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> since v4.2.0. The dashboard exposes job-trigger,
    /// pause, resume, and delete endpoints, so an unauthenticated default would be remotely
    /// equivalent to anonymous code execution on the host. Set this to <see langword="false"/>
    /// only when the dashboard is reachable solely from a trusted network (localhost-only,
    /// internal VPN, etc.); doing so writes a warning to the logger.
    /// </remarks>
    public bool RequireAuthentication { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether mutating dashboard endpoints (POST / PUT / DELETE)
    /// must carry a CSRF guard header (<c>X-Requested-With: XMLHttpRequest</c> or
    /// <c>X-CSRF-Token: *</c>). The bundled SPA always sends the header.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>. Blocks the classic CSRF attack where a third-party
    /// site triggers job mutations through a logged-in operator's browser session, since
    /// custom request headers are restricted under the same-origin policy. Set to
    /// <see langword="false"/> only if you have an alternative anti-forgery defence in place
    /// (e.g., an upstream gateway that strips and validates a CSRF cookie).
    /// </remarks>
    public bool RequireCsrfHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed roles for dashboard access.
    /// This setting is only evaluated when <see cref="RequireAuthentication"/> is enabled and <see cref="RequiredPolicy"/> is not set.
    /// An empty array allows any authenticated user.
    /// </summary>
    public string[] AllowedRoles { get; set; } = [];

    /// <inheritdoc />
    IReadOnlyList<string> IQuartzDashboardOptions.AllowedRoles => AllowedRoles;

    /// <summary>
    /// Gets or sets the authorization policy that must succeed for dashboard access.
    /// When specified, this policy takes precedence over <see cref="AllowedRoles"/>.
    /// The default value is an empty string.
    /// </summary>
    public string RequiredPolicy { get; set; } = "";

    /// <summary>
    /// Gets or sets the maximum number of fire history records retained by the default store.
    /// The default value is <c>500</c>.
    /// </summary>
    public int MaxFireHistory { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum number of execution log entries retained per job.
    /// The default value is <c>50</c>.
    /// </summary>
    public int MaxExecutionLogsPerJob { get; set; } = 50;

    /// <summary>
    /// Gets or sets an optional authorization callback invoked for every dashboard request.
    /// Return <see langword="false"/> to reject the request with a 401 response.
    /// </summary>
    public Func<HttpContext, bool>? OnAuthorize { get; set; }


    /// <summary>
    /// Gets or sets the title shown in the dashboard UI.
    /// The default value is <c>"QuartzDash"</c>.
    /// </summary>
    public string Title { get; set; } = "QuartzDash";

    /// <summary>
    /// Gets or sets the number of hours that fire history records are retained.
    /// Set this value to <c>0</c> to disable time-based pruning.
    /// The default value is <c>24</c>.
    /// </summary>
    public int HistoryRetentionHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets an optional file path used to persist fire history as JSON.
    /// When set, history is loaded on startup and survives application restarts.
    /// For higher-volume schedulers, prefer the SQLite store via <c>AddQuartzDashboardSqliteHistory</c>
    /// in the <c>Dot.QuartzDashboard.Sqlite</c> package.
    /// </summary>
    public string? PersistHistoryPath { get; set; }

    /// <summary>
    /// Gets or sets an optional callback invoked whenever a job execution fails.
    /// The callback receives the job key and the thrown exception.
    /// </summary>
    public Func<string, Exception, Task>? OnJobFailed { get; set; }

    /// <summary>
    /// Gets or sets an optional webhook URL that receives a JSON payload when a job execution fails.
    /// </summary>
    public string? WebhookUrl { get; set; }
}
