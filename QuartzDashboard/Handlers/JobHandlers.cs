using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.Matchers;
using QuartzDashboard.Internal;
using QuartzDashboard.Models;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handlers for job CRUD, detail, pause/resume/trigger, batch operations, and execution logs.
/// </summary>
internal static class JobHandlers
{
    /// <summary>
    /// Returns all jobs with pagination and trigger summaries.
    /// </summary>
    public static async Task<IResult> GetAllJobs(IScheduler sched, HttpContext ctx, QuartzDashboardOptions options)
    {
        var offset = int.TryParse(ctx.Request.Query["offset"], out var o) ? o : 0;
        var limit = int.TryParse(ctx.Request.Query["limit"], out var l) ? Math.Min(l, 200) : 50;
        var ct = ctx.RequestAborted;

        var groups = await sched.GetJobGroupNames(ct);
        var executingJobs = await sched.GetCurrentlyExecutingJobs(ct);
        var executingKeys = new HashSet<JobKey>(executingJobs.Select(j => j.JobDetail.Key));
        var allJobs = new List<object>();

        // First pass: collect every job + its triggers without touching trigger state
        // (which is the expensive call). The previous implementation called
        // GetTriggerState per trigger inside the loop — N+1 against the scheduler.
        // After this pass we batch-fetch all trigger states in parallel.
        var jobBundles = new List<(JobKey Key, IJobDetail Detail, IReadOnlyCollection<ITrigger> Triggers)>();
        foreach (var group in groups)
        {
            var keys = await sched.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group), ct);
            foreach (var key in keys)
            {
                var detail = await sched.GetJobDetail(key, ct);
                if (detail == null) continue;
                var rawTriggers = await sched.GetTriggersOfJob(key, ct);
                jobBundles.Add((key, detail, rawTriggers));
            }
        }

        var allTriggerKeys = jobBundles.SelectMany(b => b.Triggers.Select(t => t.Key)).ToArray();
        var stateTasks = allTriggerKeys.Select(k => sched.GetTriggerState(k, ct)).ToArray();
        await Task.WhenAll(stateTasks);
        var stateByKey = new Dictionary<TriggerKey, TriggerState>(allTriggerKeys.Length);
        for (var i = 0; i < allTriggerKeys.Length; i++)
            stateByKey[allTriggerKeys[i]] = stateTasks[i].Result;

        foreach (var (key, detail, rawTriggers) in jobBundles)
        {
            {
                var isExecuting = executingKeys.Contains(key);

                var triggerData = new List<object>();
                var allPaused = rawTriggers.Any();
                DateTimeOffset? nearestNextFire = null;

                foreach (var t in rawTriggers)
                {
                    var state = stateByKey[t.Key];
                    if (state != TriggerState.Paused) allPaused = false;
                    var nft = t.GetNextFireTimeUtc();
                    if (nft.HasValue && (!nearestNextFire.HasValue || nft < nearestNextFire))
                        nearestNextFire = nft;

                    var cronTrigger = t as ICronTrigger;
                    var isCron = cronTrigger != null;
                    var simpleTrigger = t as ISimpleTrigger;

                    triggerData.Add(new
                    {
                        Name = t.Key.Name,
                        Group = t.Key.Group,
                        Type = t.GetType().Name.Replace("Impl", ""),
                        State = state.ToString(),
                        NextFireTime = nft,
                        LastFireTime = t.GetPreviousFireTimeUtc(),
                        ScheduleDescription = ScheduleHelper.GetScheduleDescription(t),
                        Priority = t.Priority,
                        CronExpression = cronTrigger?.CronExpressionString,
                        IntervalSeconds = simpleTrigger != null ? (int?)Math.Max(1, (int)Math.Round(simpleTrigger.RepeatInterval.TotalSeconds)) : null,
                        RepeatCount = simpleTrigger?.RepeatCount,
                        MisfireInstruction = TriggerHandlers.MisfireInstructionName(t.MisfireInstruction, isCron),
                        MisfireInstructionValue = TriggerHandlers.MisfireInstructionValue(t.MisfireInstruction, isCron),
                    });
                }

                var hasTriggers = triggerData.Count > 0;
                var status = isExecuting ? "Executing"
                    : hasTriggers && allPaused ? "Paused"
                    : hasTriggers ? "Scheduled"
                    : detail.Durable ? "Durable"
                    : "Idle";

                allJobs.Add(new
                {
                    Group = key.Group,
                    Name = key.Name,
                    Description = detail.Description ?? "",
                    JobType = detail.JobType.Name,
                    IsDurable = detail.Durable,
                    Status = status,
                    NextFireTime = nearestNextFire,
                    PersistJobDataAfterExecution = detail.PersistJobDataAfterExecution,
                    ConcurrentExecutionDisallowed = detail.ConcurrentExecutionDisallowed,
                    Triggers = triggerData,
                    IsExecuting = isExecuting,
                    JobDataMap = detail.JobDataMap.WrappedMap
                        .ToDictionary(k => k.Key.ToString(), k => k.Value?.ToString() ?? ""),
                });
            }
        }

        var total = allJobs.Count;
        allJobs.Sort((a, b) =>
        {
            var ao = (dynamic)a;
            var bo = (dynamic)b;
            var g = string.Compare(ao.Group, bo.Group, StringComparison.OrdinalIgnoreCase);
            return g != 0 ? g : string.Compare(ao.Name, bo.Name, StringComparison.OrdinalIgnoreCase);
        });
        var page = allJobs.Skip(offset).Take(limit).ToList();
        return Results.Ok(new { data = page, total, offset, limit });
    }

    public static async Task<IResult> GetJobDetail(IScheduler sched, string group, string name)
    {
        var key = new JobKey(name, group);
        var detail = await sched.GetJobDetail(key);
        if (detail == null)
            return Results.NotFound(new { error = $"Job '{group}.{name}' not found" });

        var triggers = await sched.GetTriggersOfJob(key);
        var executing = await sched.GetCurrentlyExecutingJobs();
        return Results.Ok(new
        {
            Group = key.Group,
            Name = key.Name,
            Description = detail.Description ?? "",
            JobType = detail.JobType.FullName ?? "",
            IsDurable = detail.Durable,
            PersistJobDataAfterExecution = detail.PersistJobDataAfterExecution,
            ConcurrentExecutionDisallowed = detail.ConcurrentExecutionDisallowed,
            JobDataMap = detail.JobDataMap.WrappedMap
                .ToDictionary(k => k.Key.ToString(), k => k.Value?.ToString() ?? ""),
            Triggers = triggers.Select(t =>
            {
                var cronTrigger = t as ICronTrigger;
                var isCron = cronTrigger != null;
                var simpleTrigger = t as ISimpleTrigger;
                return new
                {
                    Name = t.Key.Name,
                    Group = t.Key.Group,
                    Type = t.GetType().Name.Replace("Impl", ""),
                    StartTime = t.StartTimeUtc,
                    EndTime = t.EndTimeUtc,
                    LastFireTime = t.GetPreviousFireTimeUtc(),
                    NextFireTime = t.GetNextFireTimeUtc(),
                    MayFireAgain = t.GetMayFireAgain(),
                    Description = t.Description ?? "",
                    CalendarName = t.CalendarName ?? "",
                    FinalFireTime = t.FinalFireTimeUtc,
                    Priority = t.Priority,
                    CronExpression = cronTrigger?.CronExpressionString,
                    IntervalSeconds = simpleTrigger != null ? (int?)Math.Max(1, (int)Math.Round(simpleTrigger.RepeatInterval.TotalSeconds)) : null,
                    RepeatCount = simpleTrigger?.RepeatCount,
                    MisfireInstruction = TriggerHandlers.MisfireInstructionName(t.MisfireInstruction, isCron),
                    MisfireInstructionValue = TriggerHandlers.MisfireInstructionValue(t.MisfireInstruction, isCron),
                };
            }).ToList(),
            IsExecuting = executing.Any(j => j.JobDetail.Key.Equals(key)),
        });
    }

    public static async Task<IResult> TriggerJob(IScheduler sched, string group, string name,
        TriggerJobRequest? req, QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key))
        {
            if (req?.DataMap?.Count > 0)
            {
                var dataMap = new JobDataMap();
                foreach (var (mapKey, mapValue) in req.DataMap.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
                    dataMap[mapKey] = mapValue ?? string.Empty;

                await sched.TriggerJob(key, dataMap);
            }
            else
            {
                await sched.TriggerJob(key);
            }

            return Results.Ok(new StatusResponse("triggered"));
        }
        return Results.NotFound(new { error = $"Job '{group}.{name}' not found" });
    }

    public static async Task<IResult> PauseJob(IScheduler sched, string group, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.PauseJob(key);
            return Results.Ok(new StatusResponse("paused"));
        }
        return Results.NotFound(new { error = $"Job '{group}.{name}' not found" });
    }

    public static async Task<IResult> ResumeJob(IScheduler sched, string group, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.ResumeJob(key);
            return Results.Ok(new StatusResponse("resumed"));
        }
        return Results.NotFound(new { error = $"Job '{group}.{name}' not found" });
    }

    public static async Task<IResult> CreateJob(IScheduler sched, CreateJobRequest? req,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        if (req == null)
            return Results.BadRequest(new { error = "Request body is required" });
        if (QuartzDashboard.Internal.NameValidation.Validate(req.Name, "Job name") is { } nameErr)
            return Results.BadRequest(new { error = nameErr });
        if (!string.IsNullOrEmpty(req.Group)
            && QuartzDashboard.Internal.NameValidation.Validate(req.Group, "Job group") is { } grpErr)
            return Results.BadRequest(new { error = grpErr });

        var jobType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.GetInterfaces().Contains(typeof(IJob)) && t.Name == req.JobType);

        if (jobType == null && !string.IsNullOrWhiteSpace(req.JobType))
            return Results.BadRequest(new
                { error = $"Job type '{req.JobType}' not found. Must be an IJob implementation." });

        var key = new JobKey(req.Name, req.Group ?? "DEFAULT");
        if (await sched.CheckExists(key))
            return Results.Conflict(new { error = $"Job '{key.Group}.{key.Name}' already exists" });

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
        return Results.Ok(new StatusResponse("created", Job: $"{key.Group}.{key.Name}"));
    }

    public static async Task<IResult> DeleteJob(IScheduler sched, string group, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        var key = new JobKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.DeleteJob(key);
            return Results.NoContent();
        }
        return Results.NotFound(new { error = $"Job '{group}.{name}' not found" });
    }

    public static async Task<IResult> UpdateJob(IScheduler sched, string group, string name,
        UpdateJobRequest? req, QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        var key = new JobKey(name, group);
        var detail = await sched.GetJobDetail(key);
        if (detail == null)
            return Results.NotFound(new { error = $"Job '{group}.{name}' not found" });

        if (req?.JobDataMap != null)
        {
            var builder = detail.GetJobBuilder();
            foreach (var (k, v) in req.JobDataMap)
                builder.UsingJobData(k, v ?? "");
            await sched.AddJob(builder.Build(), replace: true);
        }
        return Results.Ok(new StatusResponse("updated", Job: $"{group}.{name}"));
    }

    public static IResult GetJobLogs(HttpContext ctx, string group, string name)
    {
        var key = $"{group}.{name}";
        var logBuffer = ctx.RequestServices.GetService<ExecutionLogBuffer>();
        var logs = logBuffer?.GetLogs(key) ?? Array.Empty<string>();
        return Results.Ok(new { jobKey = key, logs });
    }

    // ============= Batch Operations =============

    public static async Task<IResult> BatchPauseJobs(IScheduler sched, BatchJobRequest? req,
        QuartzDashboardOptions options)
        => await BatchOperation(sched, req, options, (s, k) => s.PauseJob(k), "paused");

    public static async Task<IResult> BatchResumeJobs(IScheduler sched, BatchJobRequest? req,
        QuartzDashboardOptions options)
        => await BatchOperation(sched, req, options, (s, k) => s.ResumeJob(k), "resumed");

    public static async Task<IResult> BatchTriggerJobs(IScheduler sched, BatchJobRequest? req,
        QuartzDashboardOptions options)
        => await BatchOperation(sched, req, options, (s, k) => s.TriggerJob(k), "triggered");

    public static async Task<IResult> BatchDeleteJobs(IScheduler sched, BatchJobRequest? req,
        QuartzDashboardOptions options)
        => await BatchOperation(sched, req, options, (s, k) => s.DeleteJob(k), "deleted");

    private static async Task<IResult> BatchOperation(IScheduler sched, BatchJobRequest? req,
        QuartzDashboardOptions options, Func<IScheduler, JobKey, Task> operation, string statusLabel)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        if (req?.Jobs == null || req.Jobs.Length == 0)
            return Results.BadRequest(new { error = "No jobs specified" });

        var results = new List<object>();
        foreach (var jk in req.Jobs)
        {
            var parts = jk.Split('.', 2);
            var key = parts.Length == 2 ? new JobKey(parts[1], parts[0]) : new JobKey(jk);
            if (await sched.CheckExists(key))
            {
                await operation(sched, key);
                results.Add(new { Job = jk, Status = statusLabel });
            }
            else
            {
                results.Add(new { Job = jk, Status = "not_found" });
            }
        }
        return Results.Ok(new { results });
    }

    public static async Task<IResult> InterruptJob(IScheduler sched, string group, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        var key = new JobKey(name, group);
        var interrupted = await sched.Interrupt(key);
        return Results.Ok(new { interrupted });
    }
}
