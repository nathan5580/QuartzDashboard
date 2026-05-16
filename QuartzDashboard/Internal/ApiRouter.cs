using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.Matchers;
using QuartzDashboard.Abstractions;
using QuartzDashboard.Handlers;
using QuartzDashboard.Models;
using QuartzDashboard.Services;

namespace QuartzDashboard.Internal;

/// <summary>
/// Per-request context handed to route handlers — contains the HTTP context, the selected scheduler,
/// the dashboard options, and the parsed path segments so handlers can pull out route parameters by index.
/// </summary>
internal sealed class ApiRouteContext(
    HttpContext http,
    IScheduler scheduler,
    ISchedulerFactory schedulerFactory,
    QuartzDashboardOptions options,
    string[] segments)
{
    public HttpContext Http { get; } = http;
    public IScheduler Scheduler { get; } = scheduler;
    public ISchedulerFactory SchedulerFactory { get; } = schedulerFactory;
    public QuartzDashboardOptions Options { get; } = options;
    public string[] Segments { get; } = segments;

    /// <summary>
    /// CancellationToken bound to <see cref="HttpContext.RequestAborted"/>. Handlers should pass
    /// this through to every <see cref="IScheduler"/> call so client disconnects (and graceful
    /// shutdown) cancel in-flight work instead of running to completion against a dead socket.
    /// </summary>
    public CancellationToken Ct => Http.RequestAborted;

    public string Param(int index) => Segments[index];

    public async Task<T?> ReadJson<T>() where T : class
    {
        if (Http.Request.ContentLength is null or 0)
            return null;
        return await Http.Request.ReadFromJsonAsync<T>(Ct);
    }

    public int QueryInt(string key, int @default)
        => int.TryParse(Http.Request.Query[key], out var v) ? v : @default;
}

/// <summary>
/// Declarative API route table. Replaces the previous hand-rolled if/else chain — each route is a
/// (method, pattern, handler) triple. <c>{}</c> in a pattern matches any single non-empty segment.
/// Routes are matched in order; the first match wins. Dispatch is O(routes); the table is small enough
/// that this is faster than a trie and trivial to read.
/// </summary>
internal static class ApiRouter
{
    private sealed record Route(string Method, string[] Pattern, Func<ApiRouteContext, Task<object?>> Handler);

    private static Route Get(string pattern, Func<ApiRouteContext, Task<object?>> h) => new("GET", Split(pattern), h);
    private static Route Post(string pattern, Func<ApiRouteContext, Task<object?>> h) => new("POST", Split(pattern), h);
    private static Route Put(string pattern, Func<ApiRouteContext, Task<object?>> h) => new("PUT", Split(pattern), h);
    private static Route Delete(string pattern, Func<ApiRouteContext, Task<object?>> h) => new("DELETE", Split(pattern), h);

    private static string[] Split(string pattern) => pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static readonly Route[] Routes =
    [
        // -- Health / config / schedulers --
        Get("health", static async rc =>
        {
            var store = rc.Http.RequestServices.GetRequiredService<IFireHistoryStore>();
            return await HealthHandlers.GetHealth(rc.Scheduler, store);
        }),
        Get("config", static async rc => await ConfigHandlers.GetDashboardConfig(rc.Http, rc.Options)),
        Get("schedulers", static async rc => await SchedulerHandlers.GetSchedulers(rc.SchedulerFactory)),

        // -- Scheduler --
        Get("scheduler", static async rc => await SchedulerHandlers.GetSchedulerInfo(rc.Scheduler)),
        Post("scheduler/standby", static async rc => await SchedulerHandlers.StandbyScheduler(rc.Scheduler, rc.Options)),
        Post("scheduler/start", static async rc => await SchedulerHandlers.StartScheduler(rc.Scheduler, rc.Options)),

        // -- Jobs: batch operations come before /jobs/{group}/{name} so "batch" isn't treated as a group --
        Post("jobs/batch/pause", static async rc =>
            await JobHandlers.BatchPauseJobs(rc.Scheduler, await rc.ReadJson<Models.BatchJobRequest>(), rc.Options)),
        Post("jobs/batch/resume", static async rc =>
            await JobHandlers.BatchResumeJobs(rc.Scheduler, await rc.ReadJson<Models.BatchJobRequest>(), rc.Options)),
        Post("jobs/batch/trigger", static async rc =>
            await JobHandlers.BatchTriggerJobs(rc.Scheduler, await rc.ReadJson<Models.BatchJobRequest>(), rc.Options)),
        Post("jobs/batch/delete", static async rc =>
            await JobHandlers.BatchDeleteJobs(rc.Scheduler, await rc.ReadJson<Models.BatchJobRequest>(), rc.Options)),

        // -- Group operations on jobs --
        Post("jobs/group/{}/pause", static async rc =>
        {
            if (rc.Options.ReadOnly) return DashboardResults.ReadOnly();
            await rc.Scheduler.PauseJobs(GroupMatcher<JobKey>.GroupEquals(rc.Param(2)), rc.Ct);
            return Results.Ok(new StatusResponse("paused", Group: rc.Param(2)));
        }),
        Post("jobs/group/{}/resume", static async rc =>
        {
            if (rc.Options.ReadOnly) return DashboardResults.ReadOnly();
            await rc.Scheduler.ResumeJobs(GroupMatcher<JobKey>.GroupEquals(rc.Param(2)), rc.Ct);
            return Results.Ok(new StatusResponse("resumed", Group: rc.Param(2)));
        }),

        // -- Jobs --
        Get("jobs", static async rc => await JobHandlers.GetAllJobs(rc.Scheduler, rc.Http, rc.Options)),
        Post("jobs", static async rc =>
            await JobHandlers.CreateJob(rc.Scheduler, await rc.ReadJson<Models.CreateJobRequest>(), rc.Options)),
        Get("jobs/{}/{}", static async rc => await JobHandlers.GetJobDetail(rc.Scheduler, rc.Param(1), rc.Param(2))),
        Put("jobs/{}/{}", static async rc =>
            await JobHandlers.UpdateJob(rc.Scheduler, rc.Param(1), rc.Param(2), await rc.ReadJson<Models.UpdateJobRequest>(), rc.Options)),
        Delete("jobs/{}/{}", static async rc => await JobHandlers.DeleteJob(rc.Scheduler, rc.Param(1), rc.Param(2), rc.Options)),
        Get("jobs/{}/{}/logs", static rc => Task.FromResult<object?>(JobHandlers.GetJobLogs(rc.Http, rc.Param(1), rc.Param(2)))),
        Post("jobs/{}/{}/trigger", static async rc =>
            await JobHandlers.TriggerJob(rc.Scheduler, rc.Param(1), rc.Param(2), await rc.ReadJson<Models.TriggerJobRequest>(), rc.Options)),
        Post("jobs/{}/{}/pause", static async rc => await JobHandlers.PauseJob(rc.Scheduler, rc.Param(1), rc.Param(2), rc.Options)),
        Post("jobs/{}/{}/resume", static async rc => await JobHandlers.ResumeJob(rc.Scheduler, rc.Param(1), rc.Param(2), rc.Options)),
        Post("jobs/{}/{}/interrupt", static async rc => await JobHandlers.InterruptJob(rc.Scheduler, rc.Param(1), rc.Param(2), rc.Options)),

        // -- Trigger groups (must precede /triggers/{group}/{name}) --
        Post("triggers/group/{}/pause", static async rc =>
        {
            if (rc.Options.ReadOnly) return DashboardResults.ReadOnly();
            await rc.Scheduler.PauseTriggers(GroupMatcher<TriggerKey>.GroupEquals(rc.Param(2)), rc.Ct);
            return Results.Ok(new StatusResponse("paused", Group: rc.Param(2)));
        }),
        Post("triggers/group/{}/resume", static async rc =>
        {
            if (rc.Options.ReadOnly) return DashboardResults.ReadOnly();
            await rc.Scheduler.ResumeTriggers(GroupMatcher<TriggerKey>.GroupEquals(rc.Param(2)), rc.Ct);
            return Results.Ok(new StatusResponse("resumed", Group: rc.Param(2)));
        }),

        // -- Triggers --
        Get("triggers", static async rc => await TriggerHandlers.GetAllTriggers(rc.Scheduler, rc.Http)),
        Post("triggers", static async rc =>
            await TriggerHandlers.CreateTrigger(rc.Scheduler, await rc.ReadJson<Models.CreateTriggerRequest>(), rc.Options)),
        Get("triggers/{}/{}/next-fires", static async rc =>
            await TriggerHandlers.GetNextFires(rc.Scheduler, rc.Param(1), rc.Param(2), rc.QueryInt("count", 10))),
        Get("triggers/{}/{}", static async rc => await TriggerHandlers.GetTriggerDetail(rc.Scheduler, rc.Param(1), rc.Param(2))),
        Put("triggers/{}/{}", static async rc =>
            await TriggerHandlers.UpdateTrigger(rc.Scheduler, rc.Param(1), rc.Param(2), await rc.ReadJson<Models.UpdateTriggerRequest>(), rc.Options)),
        Delete("triggers/{}/{}", static async rc => await TriggerHandlers.DeleteTrigger(rc.Scheduler, rc.Param(1), rc.Param(2), rc.Options)),
        Post("triggers/{}/{}/pause", static async rc => await TriggerHandlers.PauseTrigger(rc.Scheduler, rc.Param(1), rc.Param(2), rc.Options)),
        Post("triggers/{}/{}/resume", static async rc => await TriggerHandlers.ResumeTrigger(rc.Scheduler, rc.Param(1), rc.Param(2), rc.Options)),

        // -- Executing / history / stats --
        Get("executing", static async rc => await GetExecutingJobs(rc.Scheduler, rc.Ct)),
        Get("history", static rc => Task.FromResult<object?>(HistoryHandlers.GetFireHistory(rc.Http))),
        Get("stats", static async rc =>
        {
            var bucketService = rc.Http.RequestServices.GetRequiredService<ExecutionBucketService>();
            var historyStore = rc.Http.RequestServices.GetRequiredService<IFireHistoryStore>();
            return await HistoryHandlers.GetStats(rc.Scheduler, bucketService, historyStore);
        }),
        Get("stats/history", static rc => Task.FromResult<object?>(HistoryHandlers.GetHistoryBuckets(rc.Http))),
        Get("timeline", static rc => Task.FromResult<object?>(HistoryHandlers.GetTimeline(rc.Http))),
        Get("heatmap", static rc => Task.FromResult<object?>(HistoryHandlers.GetHeatmap(rc.Http))),

        // -- Calendars --
        Get("calendars", static async rc => await CalendarHandlers.GetAllCalendars(rc.Scheduler)),
        Post("calendars", static async rc =>
            await CalendarHandlers.CreateCalendar(rc.Scheduler, await rc.ReadJson<Models.CreateCalendarRequest>(), rc.Options)),
        Delete("calendars/{}", static async rc => await CalendarHandlers.DeleteCalendar(rc.Scheduler, rc.Param(1), rc.Options)),

        // -- Cron describe --
        Post("cron/describe", static async rc =>
        {
            var body = await rc.Http.Request.ReadFromJsonAsync<Dictionary<string, string>>(rc.Ct);
            var expression = body?.GetValueOrDefault("expression") ?? "";
            try
            {
                var cron = new CronExpression(expression);
                var nextFires = new List<string>();
                DateTimeOffset? next = DateTimeOffset.UtcNow;
                for (int i = 0; i < 5 && next.HasValue; i++)
                {
                    next = cron.GetNextValidTimeAfter(next.Value);
                    if (next.HasValue) nextFires.Add(next.Value.ToString("o"));
                }
                return Results.Ok(new { valid = true, description = cron.CronExpressionString, nextFireTimes = nextFires });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { valid = false, error = ex.Message, nextFireTimes = Array.Empty<string>() });
            }
        }),

        // -- Export / import --
        Get("export", static async rc => await ExportImport.ExportJobs(rc.Scheduler)),
        Post("import", static async rc =>
        {
            if (rc.Options.ReadOnly) return DashboardResults.ReadOnly();
            var body = await rc.Http.Request.ReadFromJsonAsync<ExportImport.ExportPayload>();
            return await ExportImport.ImportJobs(rc.Scheduler, body);
        }),
    ];

    /// <summary>
    /// Dispatches an API request after the /api[/v1] prefix has been stripped from the route segments.
    /// Returns the handler's result (an <see cref="IResult"/> or an awaited value), or a 404 if nothing matched.
    /// </summary>
    public static async Task<object?> Dispatch(ApiRouteContext rc)
    {
        var method = rc.Http.Request.Method;

        foreach (var route in Routes)
        {
            if (!string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Matches(route.Pattern, rc.Segments))
                continue;
            return await route.Handler(rc);
        }

        return Results.NotFound(new
        {
            Error = "Unknown endpoint",
            Path = string.Join("/", rc.Segments),
        });
    }

    private static bool Matches(string[] pattern, string[] segments)
    {
        if (pattern.Length != segments.Length) return false;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == "{}") continue;
            if (!string.Equals(pattern[i], segments[i], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    // ============= Executing Jobs =============

    private static async Task<IResult> GetExecutingJobs(IScheduler sched, CancellationToken ct)
    {
        var jobs = await sched.GetCurrentlyExecutingJobs(ct);
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
}
