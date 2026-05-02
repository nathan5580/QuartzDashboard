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
    /// Whether the dashboard is read-only (no trigger/start/stop buttons).
    /// Default: false
    /// </summary>
    public bool ReadOnly { get; set; } = false;
}
