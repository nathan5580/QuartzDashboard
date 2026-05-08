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

        // Use app.Use() instead of app.Map() so that /hub/* requests can pass through
        // to the MapHub endpoint registered below in the outer endpoint routing pipeline.
        app.Use(async (ctx, next) =>
        {
            var reqPath = ctx.Request.Path;

            // Only handle requests that start with our base path
            if (!reqPath.StartsWithSegments(basePath, out var suffix))
            {
                await next(ctx);
                return;
            }

            var suffixStr = suffix.Value ?? "";

            // Let SignalR hub negotiate/connect pass through to MapHub endpoint routing
            if (suffixStr.StartsWith("/hub", StringComparison.OrdinalIgnoreCase))
            {
                await next(ctx);
                return;
            }

            // Optional custom authorization callback
            if (options.OnAuthorize != null && !options.OnAuthorize(ctx))
            {
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("{\"error\":\"Unauthorized\"}");
                return;
            }

            // Optional auth check
            if (options.RequireAuthentication)
            {
                if (ctx.User.Identity?.IsAuthenticated != true)
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync("{\"error\":\"Authentication required\"}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(options.RequiredPolicy))
                {
                    var authService = ctx.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
                    var result = await authService.AuthorizeAsync(ctx.User, null, options.RequiredPolicy);
                    if (!result.Succeeded)
                    {
                        ctx.Response.StatusCode = 403;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Insufficient permissions\"}");
                        return;
                    }
                }
                else if (options.AllowedRoles.Length > 0 && !options.AllowedRoles.Any(r => ctx.User.IsInRole(r)))
                {
                    ctx.Response.StatusCode = 403;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync("{\"error\":\"Insufficient role\"}");
                    return;
                }
            }

            // --- API endpoints ---
            if (suffixStr.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                var schedFactory = app.ApplicationServices.GetRequiredService<ISchedulerFactory>();

                // Multi-scheduler: ?scheduler=SchedulerName header or query param selects which scheduler
                IScheduler sched;
                var schedulerName = ctx.Request.Query["scheduler"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(schedulerName))
                    sched = await schedFactory.GetScheduler(schedulerName) ?? await schedFactory.GetScheduler();
                else
                    sched = await schedFactory.GetScheduler();

                await HandleApi(ctx, sched, schedFactory, suffixStr, options);
                return;
            }

            // Redirect /quartz → /quartz/ so relative asset URLs resolve correctly
            if (suffixStr == "" && !(ctx.Request.Path.Value ?? "").EndsWith('/'))
            {
                ctx.Response.Redirect(basePath + "/", permanent: false);
                return;
            }

            // --- SPA static files (and root /quartz/ → index.html) ---
            await ServeStaticFile(ctx, suffixStr, basePath, options);
        });

        // Map SignalR hub — only when app is an IEndpointRouteBuilder (WebApplication satisfies this;
        // plain IApplicationBuilder from test hosts does not — call MapQuartzDashboard() separately in that case)
        if (options.UseSignalR && app is IEndpointRouteBuilder erb)
        {
            erb.MapHub<QuartzDashboardHub>($"{basePath}/hub");
        }

        return app;
    }

    /// <summary>
    /// Maps the SignalR hub for real-time dashboard updates.
    /// Call this on your <c>IEndpointRouteBuilder</c> (e.g. <c>app</c> in a minimal-API app)
    /// when <c>UseQuartzDashboard()</c> was called on a plain <c>IApplicationBuilder</c>
    /// that does not implement <c>IEndpointRouteBuilder</c> (e.g. in a test host).
    /// In standard <c>WebApplication</c> usage this is called automatically by <c>UseQuartzDashboard()</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapQuartzDashboard(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<QuartzDashboardOptions>();
        if (!options.Enabled || !options.UseSignalR)
            return app;

        var basePath = options.Path.TrimEnd('/');
        app.MapHub<QuartzDashboardHub>($"{basePath}/hub");
        return app;
    }

    // ============= Main API Router =============

    private static async Task HandleApi(HttpContext ctx, IScheduler sched,
        ISchedulerFactory schedFactory,
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

            // -- Multi-scheduler: list all schedulers --
            else if (method == "GET" && route is ["schedulers"])
            {
                var allSchedulers = await schedFactory.GetAllSchedulers();
                result = Results.Ok(allSchedulers.Select(s => new
                {
                    name = s.SchedulerName,
                    instanceId = s.SchedulerInstanceId,
                    isStarted = s.IsStarted,
                    isInStandbyMode = s.InStandbyMode,
                    isShutdown = s.IsShutdown,
                    isCurrent = s.SchedulerName == sched.SchedulerName,
                }).ToList());
            }

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
            {
                Models.TriggerJobRequest? req = null;
                if (ctx.Request.ContentLength > 0)
                    req = await ctx.Request.ReadFromJsonAsync<Models.TriggerJobRequest>();
                result = await JobHandlers.TriggerJob(sched, route[1], route[2], req, options);
            }
            else if (method == "POST" && route is ["jobs", _, _, "pause"])
                result = await JobHandlers.PauseJob(sched, route[1], route[2], options);
            else if (method == "POST" && route is ["jobs", _, _, "resume"])
                result = await JobHandlers.ResumeJob(sched, route[1], route[2], options);
            else if (method == "POST" && route is ["jobs", _, _, "interrupt"])
                result = await JobHandlers.InterruptJob(sched, route[1], route[2]);
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
            else if (method == "PUT" && route is ["triggers", _, _])
            {
                var req = await ctx.Request
                    .ReadFromJsonAsync<Models.UpdateTriggerRequest>();
                result = await TriggerHandlers.UpdateTrigger(sched, route[1], route[2], req, options);
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
            else if (method == "GET" && route is ["stats", "history"])
                result = HistoryHandlers.GetHistoryBuckets(ctx);

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
        return Results.Ok(jobs
            .OrderBy(j => j.JobDetail.Key.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(j => j.JobDetail.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(j => new
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

    private static async Task ServeStaticFile(HttpContext ctx, string path, string basePath, QuartzDashboardOptions options)
    {
        var relativePath = path.TrimStart('/');
        if (string.IsNullOrEmpty(relativePath))
            relativePath = "index.html";

        var filePath = relativePath.Contains('?')
            ? relativePath[..relativePath.IndexOf('?')]
            : relativePath;

        // Block font files when UseSystemFonts is enabled
        if (options.UseSystemFonts && filePath.StartsWith("fonts/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        var fileInfo = EmbeddedFiles.GetFileInfo(filePath);

        if (fileInfo.Exists && !fileInfo.IsDirectory)
        {
            ctx.Response.ContentType = ScheduleHelper.GetContentType(filePath);
            ctx.Response.Headers.CacheControl = filePath == "index.html"
                ? "no-cache"
                : "public, max-age=86400";

            if (filePath == "index.html")
            {
                await ServeIndexHtml(ctx, basePath, options);
                return;
            }

            await ctx.Response.SendFileAsync(fileInfo);
        }
        else
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.CacheControl = "no-cache";
            await ServeIndexHtml(ctx, basePath, options);
        }
    }

    private static readonly string AssemblyVersion =
        ThisAssembly.GetName().Version?.ToString(3) ?? "0";

    private static async Task ServeIndexHtml(HttpContext ctx, string basePath, QuartzDashboardOptions options)
    {
        var fileInfo = EmbeddedFiles.GetFileInfo("index.html");
        using var stream = fileInfo.CreateReadStream();
        using var reader = new System.IO.StreamReader(stream);
        var html = await reader.ReadToEndAsync();

        html = html.Replace("'__QUARTZ_BASE__'", $"'{basePath}'");
        html = html.Replace("__QUARTZ_VERSION__", AssemblyVersion);
        html = html.Replace("__QUARTZ_TITLE__", System.Text.Encodings.Web.HtmlEncoder.Default.Encode(options.Title));

        if (options.UseSystemFonts)
        {
            // Inject a style override that uses system fonts instead of embedded woff2
            const string systemFontOverride = """
            <style>
              body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif !important; }
              .mono { font-family: ui-monospace, SFMono-Regular, 'SF Mono', Menlo, Consolas, monospace !important; }
            </style>
            """;
            html = html.Replace("</head>", systemFontOverride + "</head>");
        }

        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync(html);
    }
}
