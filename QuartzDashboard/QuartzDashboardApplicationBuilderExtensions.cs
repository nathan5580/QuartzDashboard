using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;
using System.Collections.Concurrent;
using System.Reflection;

namespace QuartzDashboard;

/// <summary>
/// Extension methods for mounting the Quartz Dashboard.
/// Call <c>app.UseQuartzDashboard()</c> at any point in the pipeline.
/// Uses <c>app.Map()</c> to intercept requests before endpoint routing.
/// </summary>
public static class QuartzDashboardApplicationBuilderExtensions
{
    private static readonly Assembly ThisAssembly = typeof(QuartzDashboardApplicationBuilderExtensions).Assembly;
    private static readonly EmbeddedFileProvider EmbeddedFiles = new(ThisAssembly, "QuartzDashboard.wwwroot");

    /// <summary>
    /// Mounts the Quartz Dashboard SPA and REST API at the configured path (default: /quartz).
    /// Creates a pipeline branch that intercepts requests before endpoint routing/fallback.
    /// </summary>
    public static IApplicationBuilder UseQuartzDashboard(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<QuartzDashboardOptions>();
        var basePath = options.Path.TrimEnd('/');

        // app.Map() creates a branch that runs BEFORE endpoint routing middleware.
        // This is critical — it prevents MapFallbackToFile from catching our routes.
        app.Map(basePath, branch =>
        {
            branch.Use(async (HttpContext ctx, RequestDelegate next) =>
            {
                var path = ctx.Request.Path.Value ?? "";

                // --- API endpoints ---
                if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    var sched = app.ApplicationServices.GetRequiredService<ISchedulerFactory>();
                    await HandleApi(ctx, await sched.GetScheduler(), path);
                    return;
                }

                // --- SPA static files ---
                var relativePath = path.TrimStart('/');
                if (string.IsNullOrEmpty(relativePath)) relativePath = "index.html";

                var filePath = relativePath.Contains('?') ? relativePath[..relativePath.IndexOf('?')] : relativePath;
                var fileInfo = EmbeddedFiles.GetFileInfo(filePath);

                if (fileInfo.Exists && !fileInfo.IsDirectory)
                {
                    ctx.Response.ContentType = GetContentType(filePath);
                    ctx.Response.Headers.CacheControl = filePath == "index.html" ? "no-cache" : "public, max-age=86400";
                    await ctx.Response.SendFileAsync(fileInfo);
                }
                else
                {
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.Headers.CacheControl = "no-cache";
                    await ctx.Response.SendFileAsync(EmbeddedFiles.GetFileInfo("index.html"));
                }
            });
        });

        // Map SignalR hub
        if (options.UseSignalR)
        {
            ((IEndpointRouteBuilder)app).MapHub<QuartzDashboardHub>($"{basePath}/hub");
        }

        return app;
    }

    private static async Task HandleApi(HttpContext ctx, IScheduler sched, string path)
    {
        // Strip "/api" prefix
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // segments[0] is "api", actual route starts at index 1
        var route = segments.Length > 1 ? segments[1..] : [];
        var method = ctx.Request.Method;

        object? result = null;

        try
        {
            if (method == "GET" && route is ["scheduler"])
                result = await GetSchedulerInfo(sched);
            else if (method == "POST" && route is ["scheduler", "standby"])
                result = await StandbyScheduler(sched);
            else if (method == "POST" && route is ["scheduler", "start"])
                result = await StartScheduler(sched);
            else if (method == "GET" && route is ["jobs"])
                result = await GetAllJobs(sched);
            else if (method == "GET" && route is ["jobs", _, _])
                result = await GetJobDetail(sched, route[1], route[2]);
            else if (method == "POST" && route is ["jobs", _, _, "trigger"])
                result = await TriggerJob(sched, route[1], route[2]);
            else if (method == "POST" && route is ["jobs", _, _, "pause"])
                result = await PauseJob(sched, route[1], route[2]);
            else if (method == "POST" && route is ["jobs", _, _, "resume"])
                result = await ResumeJob(sched, route[1], route[2]);
            else if (method == "GET" && route is ["triggers"])
                result = await GetAllTriggers(sched);
            else if (method == "GET" && route is ["triggers", _, _])
                result = await GetTriggerDetail(sched, route[1], route[2]);
            else if (method == "POST" && route is ["triggers", _, _, "pause"])
                result = await PauseTrigger(sched, route[1], route[2]);
            else if (method == "POST" && route is ["triggers", _, _, "resume"])
                result = await ResumeTrigger(sched, route[1], route[2]);
            else if (method == "GET" && route is ["executing"])
                result = await GetExecutingJobs(sched);
            else if (method == "GET" && route is ["history"])
                result = GetFireHistory();
            else if (method == "GET" && route is ["stats"])
                result = await GetStats(sched);
            // Create job
            else if (method == "POST" && route is ["jobs"])
                result = await CreateJob(sched, await ctx.Request.ReadFromJsonAsync<CreateJobRequest>());
            // Delete job  
            else if (method == "DELETE" && route is ["jobs", _, _])
                result = await DeleteJob(sched, route[1], route[2]);
            // Update job (JobDataMap)
            else if (method == "PUT" && route is ["jobs", _, _])
                result = await UpdateJob(sched, route[1], route[2], await ctx.Request.ReadFromJsonAsync<UpdateJobRequest>());
            // Create trigger
            else if (method == "POST" && route is ["triggers"])
                result = await CreateTrigger(sched, await ctx.Request.ReadFromJsonAsync<CreateTriggerRequest>());
            // Delete trigger
            else if (method == "DELETE" && route is ["triggers", _, _])
                result = await DeleteTrigger(sched, route[1], route[2]);
            // Get timeline
            else if (method == "GET" && route is ["timeline"])
                result = GetTimeline();
            else
                result = Results.NotFound(new { Error = "Unknown endpoint", Path = string.Join("/", route) });

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

    // ============= API Handlers =============

    private static async Task<IResult> GetSchedulerInfo(IScheduler sched)
    {
        var meta = await sched.GetMetaData();
        var executing = await sched.GetCurrentlyExecutingJobs();
        return Results.Ok(new
        {
            Name = meta.SchedulerName,
            InstanceId = meta.SchedulerInstanceId,
            IsStarted = sched.IsStarted,
            IsShutdown = sched.IsShutdown,
            IsStandbyMode = sched.InStandbyMode,
            JobStoreType = meta.JobStoreType?.Name ?? "Unknown",
            ThreadPoolType = meta.ThreadPoolType?.Name ?? "Unknown",
            NumberOfJobsExecuted = meta.NumberOfJobsExecuted,
            Summary = meta.GetSummary(),
            RunningJobs = executing.Count,
            Version = meta.Version ?? "?",
            UpSince = meta.RunningSince,
            UpTime = meta.RunningSince.HasValue ? DateTimeOffset.UtcNow - meta.RunningSince.Value : TimeSpan.Zero,
        });
    }

    private static async Task<IResult> StandbyScheduler(IScheduler sched)
    {
        if (!sched.InStandbyMode) { await sched.Standby(); return Results.Ok(new { Status = "standby" }); }
        return Results.Ok(new { Status = "already_standby" });
    }

    private static async Task<IResult> StartScheduler(IScheduler sched)
    {
        if (!sched.IsStarted || sched.InStandbyMode) { await sched.Start(); return Results.Ok(new { Status = "started" }); }
        return Results.Ok(new { Status = "already_running" });
    }

    private static async Task<IResult> GetAllJobs(IScheduler sched)
    {
        var groups = await sched.GetJobGroupNames();
        var result = new List<object>();
        foreach (var group in groups)
        {
            var keys = await sched.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group));
            foreach (var key in keys)
            {
                var detail = await sched.GetJobDetail(key);
                if (detail == null) continue;
                var triggers = await sched.GetTriggersOfJob(key);
                var executing = await sched.GetCurrentlyExecutingJobs();
                var isExecuting = executing.Any(j => j.JobDetail.Key.Equals(key));
                result.Add(new
                {
                    Group = key.Group,
                    Name = key.Name,
                    Description = detail.Description ?? "",
                    JobType = detail.JobType.Name,
                    IsDurable = detail.Durable,
                    PersistJobDataAfterExecution = detail.PersistJobDataAfterExecution,
                    ConcurrentExecutionDisallowed = detail.ConcurrentExecutionDisallowed,
                    Triggers = await GetTriggerList(sched, triggers),
                    IsExecuting = isExecuting,
                });
            }
        }
        return Results.Ok(result);
    }

    private static async Task<List<object>> GetTriggerList(IScheduler sched, IReadOnlyCollection<ITrigger> triggers)
    {
        var list = new List<object>();
        foreach (var t in triggers)
        {
            var state = await sched.GetTriggerState(t.Key);
            list.Add(new
            {
                Name = t.Key.Name,
                Group = t.Key.Group,
                Type = t.GetType().Name.Replace("Impl", ""),
                State = state.ToString(),
                StartTime = t.StartTimeUtc,
                EndTime = t.EndTimeUtc,
                LastFireTime = t.GetPreviousFireTimeUtc(),
                NextFireTime = t.GetNextFireTimeUtc(),
                MayFireAgain = t.GetMayFireAgain(),
                Description = t.Description ?? "",
                CalendarName = t.CalendarName ?? "",
                ScheduleDescription = GetScheduleDescription(t),
            });
        }
        return list;
    }

    private static async Task<IResult> GetJobDetail(IScheduler sched, string group, string name)
    {
        var key = new JobKey(name, group);
        var detail = await sched.GetJobDetail(key);
        if (detail == null) return Results.NotFound(new { Error = $"Job '{group}.{name}' not found" });
        var triggers = await sched.GetTriggersOfJob(key);
        var executing = await sched.GetCurrentlyExecutingJobs();
        return Results.Ok(new
        {
            Group = key.Group, Name = key.Name, Description = detail.Description ?? "",
            JobType = detail.JobType.FullName ?? "", IsDurable = detail.Durable,
            PersistJobDataAfterExecution = detail.PersistJobDataAfterExecution,
            ConcurrentExecutionDisallowed = detail.ConcurrentExecutionDisallowed,
            JobDataMap = detail.JobDataMap.WrappedMap.ToDictionary(k => k.Key.ToString(), k => k.Value?.ToString() ?? ""),
            Triggers = triggers.Select(t => new
            {
                Name = t.Key.Name, Group = t.Key.Group, Type = t.GetType().Name.Replace("Impl", ""),
                State = sched.GetTriggerState(t.Key).Result.ToString(), StartTime = t.StartTimeUtc,
                EndTime = t.EndTimeUtc, LastFireTime = t.GetPreviousFireTimeUtc(),
                NextFireTime = t.GetNextFireTimeUtc(), MayFireAgain = t.GetMayFireAgain(),
                Description = t.Description ?? "", CalendarName = t.CalendarName ?? "",
                FinalFireTime = t.FinalFireTimeUtc,
            }).ToList(),
            IsExecuting = executing.Any(j => j.JobDetail.Key.Equals(key)),
        });
    }

    private static async Task<IResult> TriggerJob(IScheduler sched, string group, string name)
    {
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key)) { await sched.TriggerJob(key); return Results.Ok(new { Status = "triggered" }); }
        return Results.NotFound(new { Error = $"Job '{group}.{name}' not found" });
    }

    private static async Task<IResult> PauseJob(IScheduler sched, string group, string name)
    {
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key)) { await sched.PauseJob(key); return Results.Ok(new { Status = "paused" }); }
        return Results.NotFound(new { Error = $"Job '{group}.{name}' not found" });
    }

    private static async Task<IResult> ResumeJob(IScheduler sched, string group, string name)
    {
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key)) { await sched.ResumeJob(key); return Results.Ok(new { Status = "resumed" }); }
        return Results.NotFound(new { Error = $"Job '{group}.{name}' not found" });
    }

    private static async Task<IResult> GetAllTriggers(IScheduler sched)
    {
        var groups = await sched.GetTriggerGroupNames();
        var result = new List<object>();
        foreach (var group in groups)
        {
            var keys = await sched.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupEquals(group));
            foreach (var key in keys)
            {
                var trigger = await sched.GetTrigger(key);
                if (trigger == null) continue;
                var state = await sched.GetTriggerState(key);
                result.Add(new
                {
                    Name = key.Name, Group = key.Group, Type = trigger.GetType().Name.Replace("Impl", ""),
                    State = state.ToString(), StartTime = trigger.StartTimeUtc, EndTime = trigger.EndTimeUtc,
                    LastFireTime = trigger.GetPreviousFireTimeUtc(), NextFireTime = trigger.GetNextFireTimeUtc(),
                    MayFireAgain = trigger.GetMayFireAgain(), Description = trigger.Description ?? "",
                    CalendarName = trigger.CalendarName ?? "", JobName = trigger.JobKey.Name, JobGroup = trigger.JobKey.Group,
                    ScheduleDescription = GetScheduleDescription(trigger),
                });
            }
        }
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTriggerDetail(IScheduler sched, string group, string name)
    {
        var key = new TriggerKey(name, group);
        var trigger = await sched.GetTrigger(key);
        if (trigger == null) return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });
        var state = await sched.GetTriggerState(key);
        return Results.Ok(new
        {
            Name = key.Name, Group = key.Group, Type = trigger.GetType().Name.Replace("Impl", ""),
            State = state.ToString(), StartTime = trigger.StartTimeUtc, EndTime = trigger.EndTimeUtc,
            LastFireTime = trigger.GetPreviousFireTimeUtc(), NextFireTime = trigger.GetNextFireTimeUtc(),
            MayFireAgain = trigger.GetMayFireAgain(), Description = trigger.Description ?? "",
            CalendarName = trigger.CalendarName ?? "", JobName = trigger.JobKey.Name, JobGroup = trigger.JobKey.Group,
            Priority = trigger.Priority,
        });
    }

    private static async Task<IResult> PauseTrigger(IScheduler sched, string group, string name)
    {
        var key = new TriggerKey(name, group);
        if (await sched.CheckExists(key)) { await sched.PauseTrigger(key); return Results.Ok(new { Status = "paused" }); }
        return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });
    }

    private static async Task<IResult> ResumeTrigger(IScheduler sched, string group, string name)
    {
        var key = new TriggerKey(name, group);
        if (await sched.CheckExists(key)) { await sched.ResumeTrigger(key); return Results.Ok(new { Status = "resumed" }); }
        return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });
    }

    private static async Task<IResult> GetExecutingJobs(IScheduler sched)
    {
        var jobs = await sched.GetCurrentlyExecutingJobs();
        return Results.Ok(jobs.Select(j => new
        {
            JobName = j.JobDetail.Key.Name, JobGroup = j.JobDetail.Key.Group,
            JobType = j.JobDetail.JobType.FullName, TriggerName = j.Trigger.Key.Name,
            TriggerGroup = j.Trigger.Key.Group, FireTime = j.FireTimeUtc,
            ScheduledFireTime = j.ScheduledFireTimeUtc, PreviousFireTime = j.PreviousFireTimeUtc,
            NextFireTime = j.NextFireTimeUtc, RefireCount = j.RefireCount,
            Recovering = j.Recovering, Duration = DateTimeOffset.UtcNow - j.FireTimeUtc,
        }));
    }

    private static IResult GetFireHistory()
    {
        return Results.Ok(FireHistory.Reverse().Take(50).ToList());
    }

    private static async Task<IResult> CreateJob(IScheduler sched, CreateJobRequest? req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { Error = "Job name is required" });
        
        var jobType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.GetInterfaces().Contains(typeof(IJob)) && t.Name == req.JobType);
        
        if (jobType == null && !string.IsNullOrWhiteSpace(req.JobType))
            return Results.BadRequest(new { Error = $"Job type '{req.JobType}' not found. Must be an IJob implementation." });
        
        var key = new JobKey(req.Name, req.Group ?? "DEFAULT");
        
        if (await sched.CheckExists(key))
            return Results.Conflict(new { Error = $"Job '{key.Group}.{key.Name}' already exists" });
        
        var detail = jobType != null
            ? JobBuilder.Create(jobType).WithIdentity(key).Build()
            : JobBuilder.Create<PlaceholderJob>().WithIdentity(key).Build();
        
        if (!string.IsNullOrWhiteSpace(req.Description))
            detail = detail.GetJobBuilder().WithDescription(req.Description).Build();
        
        if (req.PersistJobDataAfterExecution)
            detail = detail.GetJobBuilder().PersistJobDataAfterExecution().Build();
        
        if (req.DisallowConcurrentExecution)
            detail = detail.GetJobBuilder().DisallowConcurrentExecution().Build();
        
        if (req.IsDurable)
            detail = detail.GetJobBuilder().StoreDurably().Build();
        
        await sched.AddJob(detail, replace: false);
        return Results.Ok(new { Status = "created", Job = $"{key.Group}.{key.Name}" });
    }

    private static async Task<IResult> DeleteJob(IScheduler sched, string group, string name)
    {
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.DeleteJob(key);
            return Results.Ok(new { Status = "deleted", Job = $"{group}.{name}" });
        }
        return Results.NotFound(new { Error = $"Job '{group}.{name}' not found" });
    }

    private static async Task<IResult> UpdateJob(IScheduler sched, string group, string name, UpdateJobRequest? req)
    {
        var key = new JobKey(name, group);
        var detail = await sched.GetJobDetail(key);
        if (detail == null)
            return Results.NotFound(new { Error = $"Job '{group}.{name}' not found" });
        
        if (req?.JobDataMap != null)
        {
            var builder = detail.GetJobBuilder();
            foreach (var (k, v) in req.JobDataMap)
                builder.UsingJobData(k, v ?? "");
            await sched.AddJob(builder.Build(), replace: true);
        }
        
        return Results.Ok(new { Status = "updated", Job = $"{group}.{name}" });
    }

    private static async Task<IResult> CreateTrigger(IScheduler sched, CreateTriggerRequest? req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { Error = "Trigger name is required" });

        var triggerKey = new TriggerKey(req.Name, req.Group ?? "DEFAULT");
        var jobKey = new JobKey(req.JobName, req.JobGroup ?? "DEFAULT");

        if (!await sched.CheckExists(jobKey))
            return Results.NotFound(new { Error = $"Job '{jobKey.Group}.{jobKey.Name}' not found" });

        ITrigger trigger;
        var builder = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey);

        if (!string.IsNullOrWhiteSpace(req.Description))
            builder.WithDescription(req.Description);

        if (req.Priority.HasValue)
            builder.WithPriority(req.Priority.Value);

        if (req.StartTimeUtc.HasValue)
            builder.StartAt(req.StartTimeUtc.Value);
        else
            builder.StartNow();

        if (req.EndTimeUtc.HasValue)
            builder.EndAt(req.EndTimeUtc.Value);

        if (!string.IsNullOrWhiteSpace(req.CronExpression))
        {
            trigger = builder.WithCronSchedule(req.CronExpression).Build();
        }
        else if (req.IntervalSeconds.HasValue)
        {
            trigger = req.RepeatCount.HasValue
                ? builder.WithSimpleSchedule(x => x.WithIntervalInSeconds(req.IntervalSeconds.Value).WithRepeatCount(req.RepeatCount.Value)).Build()
                : builder.WithSimpleSchedule(x => x.WithIntervalInSeconds(req.IntervalSeconds.Value).RepeatForever()).Build();
        }
        else
        {
            return Results.BadRequest(new { Error = "Either cronExpression or intervalSeconds is required" });
        }

        await sched.ScheduleJob(trigger);
        return Results.Ok(new { Status = "created", Trigger = $"{triggerKey.Group}.{triggerKey.Name}" });
    }

    private static async Task<IResult> DeleteTrigger(IScheduler sched, string group, string name)
    {
        var key = new TriggerKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.UnscheduleJob(key);
            return Results.Ok(new { Status = "deleted", Trigger = $"{group}.{name}" });
        }
        return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });
    }

    private static IResult GetTimeline()
    {
        var events = FireHistory.Reverse().Take(100).Select(f => new
        {
            jobKey = f.JobKey,
            triggerKey = f.TriggerKey,
            fireTime = f.FireTime,
            duration = f.Duration.TotalMilliseconds,
            success = f.Success,
            relativeTime = (DateTimeOffset.UtcNow - f.FireTime).TotalSeconds,
        }).ToList();

        return Results.Ok(events);
    }

    private static string GetContentType(string path)
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
            ".woff2" => "font/woff2",
            ".woff" => "font/woff",
            _ => "application/octet-stream",
        };
    }

    private static string GetScheduleDescription(ITrigger trigger)
    {
        var typeName = trigger.GetType().Name;
        return trigger switch
        {
            // Use interface pattern matching — concrete impl types are internal in Quartz 3.x
            ICronTrigger cron when cron.CronExpressionString != null => cron.CronExpressionString,
            ISimpleTrigger simple => $"Every {simple.RepeatInterval}",
            IDailyTimeIntervalTrigger daily => $"Every {daily.RepeatInterval}",
            _ => typeName.Replace("Impl", ""),
        };
    }

    internal static readonly ConcurrentQueue<FireRecord> FireHistory = new();
    internal const int MaxFireHistory = 100;

    internal static void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration)
    {
        FireHistory.Enqueue(new FireRecord { JobKey = jobKey, TriggerKey = triggerKey, FireTime = fireTime, Duration = duration, Success = true });
        while (FireHistory.Count > MaxFireHistory) FireHistory.TryDequeue(out _);
        RecordExecution(jobKey, triggerKey, duration, true);
    }

    private static async Task<IResult> GetStats(IScheduler sched)
    {
        var meta = await sched.GetMetaData();
        var buckets = ExecutionBuckets.OrderBy(b => b.Timestamp).Select(b => new
        {
            Minute = b.Timestamp.ToString("HH:mm"),
            Count = b.ExecutionCount,
            AvgDurationMs = b.ExecutionCount > 0 ? Math.Round(b.TotalDurationMs / b.ExecutionCount, 1) : 0,
            ErrorRate = b.ExecutionCount > 0 ? Math.Round((double)b.ErrorCount / b.ExecutionCount * 100, 1) : 0,
        }).ToList();

        return Results.Ok(new
        {
            TotalExecutions = meta.NumberOfJobsExecuted,
            UptimeMinutes = meta.RunningSince.HasValue ? Math.Round((DateTimeOffset.UtcNow - meta.RunningSince.Value).TotalMinutes, 1) : 0,
            ExecutionBuckets = buckets,
            ExecutionRate = buckets.Count >= 2
                ? Math.Round(buckets.TakeLast(5).Average(b => b.Count), 1)
                : 0,
        });
    }

    internal static readonly ConcurrentQueue<ExecutionBucket> ExecutionBuckets = new();
    private const int MaxBuckets = 120;

    internal static void RecordExecution(string jobKey, string triggerKey, TimeSpan duration, bool success)
    {
        var now = DateTimeOffset.UtcNow;
        var rounded = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset);
        if (ExecutionBuckets.TryPeek(out var last) && last.Timestamp == rounded)
        {
            ExecutionBuckets.TryDequeue(out _);
            ExecutionBuckets.Enqueue(last with
            {
                ExecutionCount = last.ExecutionCount + 1,
                TotalDurationMs = last.TotalDurationMs + duration.TotalMilliseconds,
                ErrorCount = last.ErrorCount + (success ? 0 : 1)
            });
        }
        else
        {
            ExecutionBuckets.Enqueue(new ExecutionBucket
            {
                Timestamp = rounded,
                ExecutionCount = 1,
                TotalDurationMs = duration.TotalMilliseconds,
                ErrorCount = success ? 0 : 1
            });
        }
        while (ExecutionBuckets.Count > MaxBuckets) ExecutionBuckets.TryDequeue(out _);
    }
}

internal sealed record FireRecord
{
    public string JobKey { get; set; } = "";
    public string TriggerKey { get; set; } = "";
    public DateTimeOffset FireTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
}

internal sealed record ExecutionBucket
{
    public DateTimeOffset Timestamp { get; set; }
    public int ExecutionCount { get; set; }
    public double TotalDurationMs { get; set; }
    public int ErrorCount { get; set; }
}

public sealed record CreateJobRequest
{
    public string Name { get; init; } = "";
    public string? Group { get; init; }
    public string? Description { get; init; }
    public string? JobType { get; init; }
    public bool IsDurable { get; init; }
    public bool PersistJobDataAfterExecution { get; init; }
    public bool DisallowConcurrentExecution { get; init; }
}

public sealed record UpdateJobRequest
{
    public Dictionary<string, string>? JobDataMap { get; init; }
}

public sealed record CreateTriggerRequest
{
    public string Name { get; init; } = "";
    public string? Group { get; init; }
    public string JobName { get; init; } = "";
    public string? JobGroup { get; init; }
    public string? Description { get; init; }
    public string? CronExpression { get; init; }
    public int? IntervalSeconds { get; init; }
    public int? RepeatCount { get; init; }
    public int? Priority { get; init; }
    public DateTimeOffset? StartTimeUtc { get; init; }
    public DateTimeOffset? EndTimeUtc { get; init; }
}

/// <summary>
/// Fallback job type used when a user creates a job without specifying a real IJob type.
/// Logs a message when executed.
/// </summary>
public sealed class PlaceholderJob(ILogger<PlaceholderJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Placeholder job '{Job}' executed — replace with a real IJob type", 
            $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
        return Task.CompletedTask;
    }
}
