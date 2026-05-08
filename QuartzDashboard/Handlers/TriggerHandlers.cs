using Microsoft.AspNetCore.Http;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Impl.Triggers;
using QuartzDashboard.Models;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handlers for trigger CRUD endpoints.
/// </summary>
internal static class TriggerHandlers
{
    public static async Task<IResult> GetAllTriggers(IScheduler sched, HttpContext ctx)
    {
        var offset = int.TryParse(ctx.Request.Query["offset"], out var o) ? o : 0;
        var limit = int.TryParse(ctx.Request.Query["limit"], out var l) ? Math.Min(l, 200) : 50;

        var groups = await sched.GetTriggerGroupNames();
        var allTriggers = new List<object>();

        foreach (var group in groups)
        {
            var keys = await sched.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupEquals(group));
            foreach (var key in keys)
            {
                var trigger = await sched.GetTrigger(key);
                if (trigger == null) continue;

                var state = await sched.GetTriggerState(key);
                var cronTrigger = trigger as ICronTrigger;
                var isCron = cronTrigger != null;
                var simpleTrigger = trigger as ISimpleTrigger;

                allTriggers.Add(new
                {
                    Name = key.Name,
                    Group = key.Group,
                    Type = trigger.GetType().Name.Replace("Impl", ""),
                    State = state.ToString(),
                    StartTime = trigger.StartTimeUtc,
                    EndTime = trigger.EndTimeUtc,
                    LastFireTime = trigger.GetPreviousFireTimeUtc(),
                    NextFireTime = trigger.GetNextFireTimeUtc(),
                    MayFireAgain = trigger.GetMayFireAgain(),
                    Description = trigger.Description ?? "",
                    CalendarName = trigger.CalendarName ?? "",
                    JobName = trigger.JobKey.Name,
                    JobGroup = trigger.JobKey.Group,
                    Priority = trigger.Priority,
                    ScheduleDescription = ScheduleHelper.GetScheduleDescription(trigger),
                    CronExpression = cronTrigger?.CronExpressionString,
                    IntervalSeconds = simpleTrigger != null ? (int?)Math.Max(1, (int)Math.Round(simpleTrigger.RepeatInterval.TotalSeconds)) : null,
                    RepeatCount = simpleTrigger?.RepeatCount,
                    MisfireInstruction = MisfireInstructionName(trigger.MisfireInstruction, isCron),
                    MisfireInstructionValue = MisfireInstructionValue(trigger.MisfireInstruction, isCron),
                });
            }
        }

        var total = allTriggers.Count;
        allTriggers.Sort((a, b) =>
        {
            var ao = (dynamic)a;
            var bo = (dynamic)b;
            var jg = string.Compare(ao.JobGroup, bo.JobGroup, StringComparison.OrdinalIgnoreCase);
            if (jg != 0) return jg;
            var jn = string.Compare(ao.JobName, bo.JobName, StringComparison.OrdinalIgnoreCase);
            if (jn != 0) return jn;
            var g = string.Compare(ao.Group, bo.Group, StringComparison.OrdinalIgnoreCase);
            return g != 0 ? g : string.Compare(ao.Name, bo.Name, StringComparison.OrdinalIgnoreCase);
        });
        var page = allTriggers.Skip(offset).Take(limit).ToList();
        return Results.Ok(new { data = page, total, offset, limit });
    }

    public static async Task<IResult> GetTriggerDetail(IScheduler sched, string group, string name)
    {
        var key = new TriggerKey(name, group);
        var trigger = await sched.GetTrigger(key);
        if (trigger == null)
            return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });

        var state = await sched.GetTriggerState(key);
        var cronTrigger = trigger as ICronTrigger;
        var isCron = cronTrigger != null;
        var simpleTrigger = trigger as ISimpleTrigger;

        return Results.Ok(new
        {
            Name = key.Name,
            Group = key.Group,
            Type = trigger.GetType().Name.Replace("Impl", ""),
            State = state.ToString(),
            StartTime = trigger.StartTimeUtc,
            EndTime = trigger.EndTimeUtc,
            LastFireTime = trigger.GetPreviousFireTimeUtc(),
            NextFireTime = trigger.GetNextFireTimeUtc(),
            MayFireAgain = trigger.GetMayFireAgain(),
            Description = trigger.Description ?? "",
            CalendarName = trigger.CalendarName ?? "",
            JobName = trigger.JobKey.Name,
            JobGroup = trigger.JobKey.Group,
            Priority = trigger.Priority,
            ScheduleDescription = ScheduleHelper.GetScheduleDescription(trigger),
            CronExpression = cronTrigger?.CronExpressionString,
            IntervalSeconds = simpleTrigger != null ? (int?)Math.Max(1, (int)Math.Round(simpleTrigger.RepeatInterval.TotalSeconds)) : null,
            RepeatCount = simpleTrigger?.RepeatCount,
            MisfireInstruction = MisfireInstructionName(trigger.MisfireInstruction, isCron),
            MisfireInstructionValue = MisfireInstructionValue(trigger.MisfireInstruction, isCron),
        });
    }

    public static async Task<IResult> PauseTrigger(IScheduler sched, string group, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return Results.Forbid();
        var key = new TriggerKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.PauseTrigger(key);
            return Results.Ok(new { Status = "paused" });
        }
        return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });
    }

    public static async Task<IResult> ResumeTrigger(IScheduler sched, string group, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return Results.Forbid();
        var key = new TriggerKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.ResumeTrigger(key);
            return Results.Ok(new { Status = "resumed" });
        }
        return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });
    }

    public static async Task<IResult> CreateTrigger(IScheduler sched, CreateTriggerRequest? req,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return Results.Forbid();
        if (req == null || string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { Error = "Trigger name is required" });

        var triggerKey = new TriggerKey(req.Name, req.Group ?? "DEFAULT");
        var jobKey = new JobKey(req.JobName, req.JobGroup ?? "DEFAULT");

        if (!await sched.CheckExists(jobKey))
            return Results.NotFound(new { Error = $"Job '{jobKey.Group}.{jobKey.Name}' not found" });

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

        ITrigger trigger;
        if (!string.IsNullOrWhiteSpace(req.CronExpression))
        {
            var misfireInstruction = req.MisfireInstruction;
            trigger = builder.WithCronSchedule(req.CronExpression, cron =>
            {
                if (!string.IsNullOrEmpty(misfireInstruction))
                    ApplyMisfireInstruction(cron, misfireInstruction);
            }).Build();
        }
        else if (req.IntervalSeconds.HasValue)
        {
            var simpleBuilder = req.RepeatCount.HasValue
                ? builder.WithSimpleSchedule(x =>
                {
                    x.WithIntervalInSeconds(req.IntervalSeconds.Value)
                     .WithRepeatCount(req.RepeatCount.Value);
                    ApplySimpleMisfireInstruction(x, req.MisfireInstruction);
                })
                : builder.WithSimpleSchedule(x =>
                {
                    x.WithIntervalInSeconds(req.IntervalSeconds.Value).RepeatForever();
                    ApplySimpleMisfireInstruction(x, req.MisfireInstruction);
                });
            trigger = simpleBuilder.Build();
        }
        else
        {
            return Results.BadRequest(new { Error = "Either cronExpression or intervalSeconds is required" });
        }

        if (!string.IsNullOrWhiteSpace(req.CalendarName))
        {
            var calendars = await sched.GetCalendarNames();
            if (!calendars.Contains(req.CalendarName))
                return Results.BadRequest(new { Error = $"Calendar '{req.CalendarName}' not found" });
            trigger = trigger.GetTriggerBuilder().ModifiedByCalendar(req.CalendarName).Build();
        }

        await sched.ScheduleJob(trigger);
        return Results.Ok(new { Status = "created", Trigger = $"{triggerKey.Group}.{triggerKey.Name}" });
    }

    public static async Task<IResult> UpdateTrigger(IScheduler sched, string group, string name,
        UpdateTriggerRequest? req, QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return Results.Forbid();
        if (req == null)
            return Results.BadRequest(new { Error = "Trigger update payload is required" });

        var key = new TriggerKey(name, group);
        var existing = await sched.GetTrigger(key);
        if (existing == null)
            return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });

        var builder = TriggerBuilder.Create()
            .WithIdentity(existing.Key)
            .ForJob(existing.JobKey)
            .StartAt(existing.StartTimeUtc)
            .WithPriority(existing.Priority);

        if (!string.IsNullOrWhiteSpace(existing.Description))
            builder.WithDescription(existing.Description);
        if (existing.EndTimeUtc.HasValue)
            builder.EndAt(existing.EndTimeUtc.Value);
        if (!string.IsNullOrWhiteSpace(existing.CalendarName))
            builder.ModifiedByCalendar(existing.CalendarName);

        ITrigger updatedTrigger;
        if (existing is ICronTrigger cronTrigger)
        {
            var cronExpression = req.CronExpression ?? cronTrigger.CronExpressionString;
            if (string.IsNullOrWhiteSpace(cronExpression))
                return Results.BadRequest(new { Error = "cronExpression is required for cron triggers" });

            updatedTrigger = builder.WithCronSchedule(cronExpression, cron =>
            {
                ApplyMisfireInstruction(cron, req.MisfireInstruction ?? MisfireInstructionValue(existing.MisfireInstruction, true));
            }).Build();
        }
        else if (existing is ISimpleTrigger simpleTrigger)
        {
            var intervalSeconds = req.IntervalSeconds ?? Math.Max(1, (int)Math.Round(simpleTrigger.RepeatInterval.TotalSeconds));
            if (intervalSeconds <= 0)
                return Results.BadRequest(new { Error = "intervalSeconds must be greater than zero for simple triggers" });

            updatedTrigger = builder.WithSimpleSchedule(schedule =>
            {
                schedule.WithIntervalInSeconds(intervalSeconds);
                if (simpleTrigger.RepeatCount == SimpleTriggerImpl.RepeatIndefinitely)
                    schedule.RepeatForever();
                else
                    schedule.WithRepeatCount(simpleTrigger.RepeatCount);

                ApplySimpleMisfireInstruction(schedule, req.MisfireInstruction ?? MisfireInstructionValue(existing.MisfireInstruction, false));
            }).Build();
        }
        else
        {
            return Results.BadRequest(new { Error = $"Trigger type '{existing.GetType().Name}' is not supported for updates" });
        }

        await sched.RescheduleJob(key, updatedTrigger);
        return Results.Ok(new { Status = "updated", Trigger = $"{group}.{name}" });
    }

    public static async Task<IResult> DeleteTrigger(IScheduler sched, string group, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return Results.Forbid();
        var key = new TriggerKey(name, group);
        if (await sched.CheckExists(key))
        {
            await sched.UnscheduleJob(key);
            return Results.Ok(new { Status = "deleted", Trigger = $"{group}.{name}" });
        }
        return Results.NotFound(new { Error = $"Trigger '{group}.{name}' not found" });
    }

    internal static string MisfireInstructionName(int code, bool isCron)
    {
        if (code == MisfireInstruction.IgnoreMisfirePolicy)
            return "IgnoreMisfirePolicy";

        return isCron
            ? code switch
            {
                MisfireInstruction.SmartPolicy => "SmartPolicy",
                MisfireInstruction.CronTrigger.FireOnceNow => "FireOnceNow",
                MisfireInstruction.CronTrigger.DoNothing => "DoNothing",
                _ => code.ToString(),
            }
            : code switch
            {
                MisfireInstruction.SmartPolicy => "SmartPolicy",
                MisfireInstruction.SimpleTrigger.FireNow => "FireNow",
                MisfireInstruction.SimpleTrigger.RescheduleNowWithExistingRepeatCount => "RescheduleNowWithExistingCount",
                MisfireInstruction.SimpleTrigger.RescheduleNowWithRemainingRepeatCount => "RescheduleNowWithRemainingCount",
                MisfireInstruction.SimpleTrigger.RescheduleNextWithRemainingCount => "RescheduleNextWithRemainingCount",
                MisfireInstruction.SimpleTrigger.RescheduleNextWithExistingCount => "RescheduleNextWithExistingCount",
                _ => code.ToString(),
            };
    }

    internal static string MisfireInstructionValue(int code, bool isCron)
    {
        if (code == MisfireInstruction.IgnoreMisfirePolicy)
            return "ignoreMisfirePolicy";

        return isCron
            ? code switch
            {
                MisfireInstruction.CronTrigger.FireOnceNow => "fireOnceNow",
                MisfireInstruction.CronTrigger.DoNothing => "doNothing",
                _ => "smartPolicy",
            }
            : code switch
            {
                MisfireInstruction.SimpleTrigger.FireNow => "fireNow",
                MisfireInstruction.SimpleTrigger.RescheduleNowWithExistingRepeatCount => "rescheduleNowWithExistingCount",
                MisfireInstruction.SimpleTrigger.RescheduleNowWithRemainingRepeatCount => "rescheduleNowWithRemainingCount",
                MisfireInstruction.SimpleTrigger.RescheduleNextWithRemainingCount => "rescheduleNextWithRemainingCount",
                MisfireInstruction.SimpleTrigger.RescheduleNextWithExistingCount => "rescheduleNextWithExistingCount",
                _ => "smartPolicy",
            };
    }

    // ============= Misfire Instruction Helpers =============

    internal static void ApplyMisfireInstruction(CronScheduleBuilder builder, string? instruction)
    {
        switch (instruction)
        {
            case "fireOnceNow":
                builder.WithMisfireHandlingInstructionFireAndProceed();
                break;
            case "doNothing":
                builder.WithMisfireHandlingInstructionDoNothing();
                break;
            case "ignoreMisfirePolicy":
                builder.WithMisfireHandlingInstructionIgnoreMisfires();
                break;
        }
    }

    internal static void ApplySimpleMisfireInstruction(SimpleScheduleBuilder builder, string? instruction)
    {
        switch (instruction)
        {
            case "fireOnceNow":
            case "fireNow":
                builder.WithMisfireHandlingInstructionFireNow();
                break;
            case "doNothing":
            case "rescheduleNextWithExistingCount":
                builder.WithMisfireHandlingInstructionNextWithExistingCount();
                break;
            case "rescheduleNowWithExistingCount":
                builder.WithMisfireHandlingInstructionNowWithExistingCount();
                break;
            case "rescheduleNowWithRemainingCount":
                builder.WithMisfireHandlingInstructionNowWithRemainingCount();
                break;
            case "rescheduleNextWithRemainingCount":
                builder.WithMisfireHandlingInstructionNextWithRemainingCount();
                break;
            case "ignoreMisfirePolicy":
                builder.WithMisfireHandlingInstructionIgnoreMisfires();
                break;
        }
    }
}
