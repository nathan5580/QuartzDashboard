namespace QuartzDashboard.Internal;

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
    /// Whether the dashboard is read-only (no trigger/start/stop buttons).
    /// Default: false
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// Whether to use the browser's localStorage to persist dashboard preferences.
    /// Default: true
    /// </summary>
    public bool PersistPreferences { get; set; } = true;
}
