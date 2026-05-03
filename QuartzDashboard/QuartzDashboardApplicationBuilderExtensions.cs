using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Quartz;
using QuartzDashboard.Handlers;
using QuartzDashboard.Internal;
using QuartzDashboard.Middleware;
using QuartzDashboard.Services;
using System.Reflection;

namespace QuartzDashboard;

/// <summary>
/// Extension methods for mounting the Quartz Dashboard.
/// Call <c>app.UseQuartzDashboard()</c> at any point in the pipeline.
/// </summary>
public static class QuartzDashboardApplicationBuilderExtensions
{
    private static readonly Assembly ThisAssembly =
        typeof(QuartzDashboardApplicationBuilderExtensions).Assembly;

    private static readonly EmbeddedFileProvider EmbeddedFiles =
        new(ThisAssembly, "QuartzDashboard.wwwroot");

    /// <summary>
    /// Mounts the Quartz Dashboard SPA and REST API at the configured path (default: /quartz).
    /// Uses <c>app.Map()</c> to intercept requests before endpoint routing/fallback.
    /// </summary>
    public static IApplicationBuilder UseQuartzDashboard(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<QuartzDashboardOptions>();

        // Plan A: Dev-only gating — no-op when disabled
        if (!options.Enabled)
            return app;

        var basePath = options.Path.TrimEnd('/');

        // app.Map() creates a branch that runs BEFORE endpoint routing middleware.
        // This is critical — it prevents MapFallbackToFile from catching our routes.
        app.Map(basePath, branch =>
        {
            // Plan B: Optional auth middleware
            if (options.RequireAuthentication)
            {
                branch.UseAuthentication();
                branch.UseMiddleware<QuartzDashboardAuthMiddleware>(options);
            }

            branch.Run(async ctx =>
            {
                var path = ctx.Request.Path.Value ?? "";

                // --- API endpoints ---
                if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    var schedFactory = app.ApplicationServices
                        .GetRequiredService<ISchedulerFactory>();
                    await HandleApi(ctx, await schedFactory.GetScheduler(), path, options);
                    return;
                }

                // --- SPA static files ---
                await ServeStaticFile(ctx, path);
            });
        });

        // Map SignalR hub (outside Map() branch for endpoint routing)
        if (options.UseSignalR)
        {
            ((IEndpointRouteBuilder)app).MapHub<QuartzDashboardHub>($"{basePath}/hub");
        }

        return app;
    }

    // ============= Main API Router =============

    private static async Task HandleApi(HttpContext ctx, IScheduler sched,
        string path, QuartzDashboardOptions options)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Strip "/api" or "/api/v1" prefix for API versioning support
        var apiOffset = 1; // skip "api"
        if (segments.Length > 1 && string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase))
            apiOffset = 2;
        var route = segments.Length > apiOffset ? segments[apiOffset..] : [];
        var method = ctx.Request.Method;

        object? result = null;

        try
        {
            // -- Health --
            if (method == "GET" && route is ["health"])
            {
                var historyStore = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();
                result = await HealthHandlers.GetHealth(sched, historyStore);
            }

            // -- Config --
            else if (method == "GET" && route is ["config"])
                result = ConfigHandlers.GetDashboardConfig(ctx, options);

            // -- Scheduler --
            else if (method == "GET" && route is ["scheduler"])
                result = await SchedulerHandlers.GetSchedulerInfo(sched);
            else if (method == "POST" && route is ["scheduler", "standby"])
                result = await SchedulerHandlers.StandbyScheduler(sched, options);
            else if (method == "POST" && route is ["scheduler", "start"])
                result = await SchedulerHandlers.StartScheduler(sched, options);

            // -- Jobs --
            else if (method == "GET" && route is ["jobs"] && !ctx.Request.Query.ContainsKey("batch"))
                result = await JobHandlers.GetAllJobs(sched, ctx, options);
            else if (method == "GET" && route is ["jobs", _, _])
                result = await JobHandlers.GetJobDetail(sched, route[1], route[2]);
            else if (method == "POST" && route is ["jobs", _, _, "trigger"])
                result = await JobHandlers.TriggerJob(sched, route[1], route[2], options);
            else if (method == "POST" && route is ["jobs", _, _, "pause"])
                result = await JobHandlers.PauseJob(sched, route[1], route[2], options);
            else if (method == "POST" && route is ["jobs", _, _, "resume"])
                result = await JobHandlers.ResumeJob(sched, route[1], route[2], options);
            else if (method == "POST" && route is ["jobs"])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.CreateJobRequest>();
                result = await JobHandlers.CreateJob(sched, req, options);
            }
            else if (method == "DELETE" && route is ["jobs", _, _])
                result = await JobHandlers.DeleteJob(sched, route[1], route[2], options);
            else if (method == "PUT" && route is ["jobs", _, _])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.UpdateJobRequest>();
                result = await JobHandlers.UpdateJob(sched, route[1], route[2], req, options);
            }
            else if (method == "GET" && route is ["jobs", _, _, "logs"])
                result = JobHandlers.GetJobLogs(ctx, route[1], route[2]);

            // -- Batch operations --
            else if (method == "POST" && route is ["jobs", "batch", "pause"])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.BatchJobRequest>();
                result = await JobHandlers.BatchPauseJobs(sched, req, options);
            }
            else if (method == "POST" && route is ["jobs", "batch", "resume"])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.BatchJobRequest>();
                result = await JobHandlers.BatchResumeJobs(sched, req, options);
            }
            else if (method == "POST" && route is ["jobs", "batch", "trigger"])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.BatchJobRequest>();
                result = await JobHandlers.BatchTriggerJobs(sched, req, options);
            }
            else if (method == "POST" && route is ["jobs", "batch", "delete"])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.BatchJobRequest>();
                result = await JobHandlers.BatchDeleteJobs(sched, req, options);
            }

            // -- Triggers --
            else if (method == "GET" && route is ["triggers"])
                result = await TriggerHandlers.GetAllTriggers(sched, ctx);
            else if (method == "GET" && route is ["triggers", _, _])
                result = await TriggerHandlers.GetTriggerDetail(sched, route[1], route[2]);
            else if (method == "POST" && route is ["triggers", _, _, "pause"])
                result = await TriggerHandlers.PauseTrigger(sched, route[1], route[2], options);
            else if (method == "POST" && route is ["triggers", _, _, "resume"])
                result = await TriggerHandlers.ResumeTrigger(sched, route[1], route[2], options);
            else if (method == "POST" && route is ["triggers"])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.CreateTriggerRequest>();
                result = await TriggerHandlers.CreateTrigger(sched, req, options);
            }
            else if (method == "DELETE" && route is ["triggers", _, _])
                result = await TriggerHandlers.DeleteTrigger(sched, route[1], route[2], options);

            // -- Executing --
            else if (method == "GET" && route is ["executing"])
                result = await GetExecutingJobs(sched);

            // -- History --
            else if (method == "GET" && route is ["history"])
                result = HistoryHandlers.GetFireHistory(ctx);

            // -- Stats --
            else if (method == "GET" && route is ["stats"])
            {
                var bucketService = ctx.RequestServices
                    .GetRequiredService<ExecutionBucketService>();
                result = await HistoryHandlers.GetStats(sched, bucketService);
            }

            // -- Timeline --
            else if (method == "GET" && route is ["timeline"])
                result = HistoryHandlers.GetTimeline(ctx);

            // -- Calendars --
            else if (method == "GET" && route is ["calendars"])
                result = await CalendarHandlers.GetAllCalendars(sched);
            else if (method == "POST" && route is ["calendars"])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.CreateCalendarRequest>();
                result = await CalendarHandlers.CreateCalendar(sched, req, options);
            }
            else if (method == "DELETE" && route is ["calendars", _])
                result = await CalendarHandlers.DeleteCalendar(sched, route[1], options);

            else
                result = Results.NotFound(new
                {
                    Error = "Unknown endpoint",
                    Path = string.Join("/", route)
                });

            if (result is IResult ires)
                await ires.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new { Error = ex.Message }));
        }
    }

    // ============= Executing Jobs =============

    private static async Task<IResult> GetExecutingJobs(IScheduler sched)
    {
        var jobs = await sched.GetCurrentlyExecutingJobs();
        return Results.Ok(jobs.Select(j => new
        {
            JobName = j.JobDetail.Key.Name,
            JobGroup = j.JobDetail.Key.Group,
            JobType = j.JobDetail.JobType.FullName,
            TriggerName = j.Trigger.Key.Name,
            TriggerGroup = j.Trigger.Key.Group,
            FireTime = j.FireTimeUtc,
            ScheduledFireTime = j.ScheduledFireTimeUtc,
            PreviousFireTime = j.PreviousFireTimeUtc,
            NextFireTime = j.NextFireTimeUtc,
            RefireCount = j.RefireCount,
            Recovering = j.Recovering,
            Duration = DateTimeOffset.UtcNow - j.FireTimeUtc,
        }));
    }

    // ============= Execution Recording =============

    internal static void RecordExecution(string jobKey, string triggerKey,
        TimeSpan duration, bool success)
    {
        // This is called from DashboardJobListener. The bucket service is a singleton,
        // but we use the static ExecutionBucketService accessor registered in DI.
    }

    // ============= Static File Serving =============

    private static async Task ServeStaticFile(HttpContext ctx, string path)
    {
        var relativePath = path.TrimStart('/');
        if (string.IsNullOrEmpty(relativePath))
            relativePath = "index.html";

        var filePath = relativePath.Contains('?')
            ? relativePath[..relativePath.IndexOf('?')]
            : relativePath;

        var fileInfo = EmbeddedFiles.GetFileInfo(filePath);

        if (fileInfo.Exists && !fileInfo.IsDirectory)
        {
            ctx.Response.ContentType = ScheduleHelper.GetContentType(filePath);
            ctx.Response.Headers.CacheControl = filePath == "index.html"
                ? "no-cache"
                : "public, max-age=86400";
            await ctx.Response.SendFileAsync(fileInfo);
        }
        else
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.CacheControl = "no-cache";
            await ctx.Response.SendFileAsync(EmbeddedFiles.GetFileInfo("index.html"));
        }
    }
}
