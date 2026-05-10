using Quartz;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Shared helper methods for Quartz-related operations.
/// </summary>
internal static class ScheduleHelper
{
    /// <summary>
    /// Returns a human-readable description of a trigger's schedule.
    /// </summary>
    public static string GetScheduleDescription(ITrigger trigger)
    {
        return trigger switch
        {
            ICronTrigger cron when cron.CronExpressionString != null => cron.CronExpressionString,
            ISimpleTrigger simple => $"Every {simple.RepeatInterval}",
            IDailyTimeIntervalTrigger daily => $"Every {daily.RepeatInterval}",
            _ => trigger.GetType().Name.Replace("Impl", ""),
        };
    }

    /// <summary>
    /// Returns the MIME content type for a static file path.
    /// </summary>
    public static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".ico" => "image/x-icon",
            ".json" => "application/json",
            _ => "application/octet-stream",
        };
    }
}
