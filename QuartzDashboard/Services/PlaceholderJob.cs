using Microsoft.Extensions.Logging;
using Quartz;

namespace QuartzDashboard.Services;

/// <summary>
/// Fallback job type used when a user creates a job without specifying a real IJob type.
/// Logs a message when executed.
/// </summary>
public sealed class PlaceholderJob(ILogger<PlaceholderJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation(
            "Placeholder job '{Job}' executed — replace with a real IJob type",
            $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
        return Task.CompletedTask;
    }
}
