using Microsoft.AspNetCore.Http;
using Quartz;
using Quartz.Impl.Matchers;
using QuartzDashboard.Handlers;

namespace QuartzDashboard.Internal;

/// <summary>
/// Export / import helpers for the dashboard's snapshot endpoints. Job-type lookup is cached
/// so an import of N jobs doesn't issue N×|assemblies| reflection scans.
/// </summary>
internal static class ExportImport
{
    internal sealed record ExportPayload
    {
        public List<ExportedJob>? Jobs { get; set; }
    }

    internal sealed record ExportedJob
    {
        public string Group { get; set; } = "DEFAULT";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string JobType { get; set; } = "";
        public bool Durable { get; set; }
        public bool RequestsRecovery { get; set; }
        public Dictionary<string, string>? JobDataMap { get; set; }
        public List<ExportedTrigger>? Triggers { get; set; }
    }

    internal sealed record ExportedTrigger
    {
        public string Group { get; set; } = "DEFAULT";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? CronExpression { get; set; }
        public int? IntervalSeconds { get; set; }
        public int? RepeatCount { get; set; }
        public int Priority { get; set; } = 5;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> _jobTypeCache = new();
    private static int _loadedAssemblyCountSnapshot;

    private static (Type ResolvedType, bool FellBackToPlaceholder) ResolveJobType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return (typeof(PlaceholderJob), true);

        var currentCount = AppDomain.CurrentDomain.GetAssemblies().Length;
        if (Interlocked.Exchange(ref _loadedAssemblyCountSnapshot, currentCount) != currentCount)
            _jobTypeCache.Clear();

        var resolved = _jobTypeCache.GetOrAdd(typeName, name =>
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
                catch { continue; }

                foreach (var t in types)
                {
                    // Match name AND require IJob — without the interface filter, a malicious
                    // import payload could probe loaded types ("does this host have HttpClient
                    // loaded?") even though JobBuilder.Create later rejects non-IJob types.
                    // Same gate JobHandlers.CreateJob already enforces.
                    if ((t.FullName == name || t.Name == name)
                        && typeof(Quartz.IJob).IsAssignableFrom(t))
                        return t;
                }
            }
            return typeof(PlaceholderJob);
        });

        return (resolved, resolved == typeof(PlaceholderJob));
    }

    public static async Task<IResult> ExportJobs(IScheduler sched)
    {
        var jobGroupNames = await sched.GetJobGroupNames();
        var exported = new List<ExportedJob>();

        foreach (var group in jobGroupNames)
        {
            var jobKeys = await sched.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group));
            foreach (var jobKey in jobKeys)
            {
                var job = await sched.GetJobDetail(jobKey);
                if (job == null) continue;

                var triggers = await sched.GetTriggersOfJob(jobKey);
                var exportedTriggers = triggers.Select(t =>
                {
                    var et = new ExportedTrigger
                    {
                        Group = t.Key.Group,
                        Name = t.Key.Name,
                        Description = t.Description,
                        Priority = t.Priority
                    };
                    if (t is ICronTrigger ct) et.CronExpression = ct.CronExpressionString;
                    if (t is ISimpleTrigger st)
                    {
                        et.IntervalSeconds = (int)st.RepeatInterval.TotalSeconds;
                        et.RepeatCount = st.RepeatCount;
                    }
                    return et;
                }).ToList();

                exported.Add(new ExportedJob
                {
                    Group = job.Key.Group,
                    Name = job.Key.Name,
                    Description = job.Description,
                    JobType = job.JobType.FullName ?? job.JobType.Name,
                    Durable = job.Durable,
                    RequestsRecovery = job.RequestsRecovery,
                    JobDataMap = job.JobDataMap?.Count > 0
                        ? job.JobDataMap.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "")
                        : null,
                    Triggers = exportedTriggers
                });
            }
        }

        return Results.Ok(new { jobs = exported, exportedAt = DateTimeOffset.UtcNow });
    }

    public static async Task<IResult> ImportJobs(IScheduler sched, ExportPayload? payload)
    {
        if (payload?.Jobs == null || payload.Jobs.Count == 0)
            return Results.BadRequest(new { error = "No jobs to import" });

        int jobsCreated = 0, triggersCreated = 0, errors = 0;
        var errorMessages = new List<string>();
        var placeholderJobs = new List<string>();

        foreach (var ej in payload.Jobs)
        {
            try
            {
                // Apply the same name validation as the create endpoints — an import
                // payload from an attacker (or a corrupted export) shouldn't be allowed
                // to insert names containing quotes / HTML metacharacters / control chars.
                if (NameValidation.Validate(ej.Name, "Job name") is { } jobNameErr)
                {
                    errors++;
                    errorMessages.Add($"{ej.Group}.{ej.Name}: {jobNameErr}");
                    continue;
                }
                if (!string.IsNullOrEmpty(ej.Group)
                    && NameValidation.Validate(ej.Group, "Job group") is { } jobGrpErr)
                {
                    errors++;
                    errorMessages.Add($"{ej.Group}.{ej.Name}: {jobGrpErr}");
                    continue;
                }

                var (jobType, fellBack) = ResolveJobType(ej.JobType);
                if (fellBack && !string.IsNullOrEmpty(ej.JobType))
                    placeholderJobs.Add($"{ej.Group}.{ej.Name} ({ej.JobType})");

                var jobBuilder = JobBuilder.Create(jobType)
                    .WithIdentity(ej.Name, ej.Group)
                    .WithDescription(ej.Description)
                    .StoreDurably(ej.Durable)
                    .RequestRecovery(ej.RequestsRecovery);

                if (ej.JobDataMap != null)
                {
                    foreach (var kv in ej.JobDataMap)
                        jobBuilder.UsingJobData(kv.Key, kv.Value);
                }

                var jobDetail = jobBuilder.Build();
                await sched.AddJob(jobDetail, true);
                jobsCreated++;

                if (ej.Triggers != null)
                {
                    foreach (var et in ej.Triggers)
                    {
                        TriggerBuilder tb = TriggerBuilder.Create()
                            .WithIdentity(et.Name, et.Group)
                            .WithDescription(et.Description)
                            .WithPriority(et.Priority)
                            .ForJob(jobDetail);

                        if (!string.IsNullOrWhiteSpace(et.CronExpression))
                            tb.WithCronSchedule(et.CronExpression);
                        else if (et.IntervalSeconds.HasValue)
                            tb.WithSimpleSchedule(s => s
                                .WithIntervalInSeconds(et.IntervalSeconds.Value)
                                .WithRepeatCount(et.RepeatCount ?? -1));

                        await sched.ScheduleJob(tb.Build());
                        triggersCreated++;
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"{ej.Group}.{ej.Name}: {ex.Message}");
            }
        }

        return Results.Ok(new
        {
            jobsCreated,
            triggersCreated,
            errors,
            errorMessages,
            placeholderJobs,
            // When non-empty, the indicated jobs could not resolve their original IJob type and
            // were imported as no-op PlaceholderJob entries. Re-import after referencing the
            // assemblies that define the missing types, or supply fully qualified names.
            placeholderWarning = placeholderJobs.Count > 0
                ? "Some jobs were imported as PlaceholderJob because their type could not be resolved in the host AppDomain."
                : null,
        });
    }
}
