using Microsoft.AspNetCore.Http;
using Quartz;
using QuartzDashboard.Models;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handlers for Quartz calendar CRUD.
/// </summary>
internal static class CalendarHandlers
{
    public static async Task<IResult> GetAllCalendars(IScheduler sched)
    {
        var names = await sched.GetCalendarNames();
        var calendars = new List<object>();
        foreach (var name in names)
        {
            var cal = await sched.GetCalendar(name);
            calendars.Add(new
            {
                Name = name,
                Type = cal?.GetType().Name.Replace("Calendar", "") ?? "Unknown",
                Description = cal?.Description ?? "",
            });
        }
        return Results.Ok(calendars);
    }

    public static async Task<IResult> CreateCalendar(IScheduler sched, CreateCalendarRequest? req,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        if (req == null || string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { Error = "Calendar name is required" });

        var names = await sched.GetCalendarNames();
        if (names.Contains(req.Name))
            return Results.Conflict(new { Error = $"Calendar '{req.Name}' already exists" });

        ICalendar? calendar = req.Type?.ToLowerInvariant() switch
        {
            "holiday" => new Quartz.Impl.Calendar.HolidayCalendar(),
            "monthly" => new Quartz.Impl.Calendar.MonthlyCalendar(),
            "weekly" => new Quartz.Impl.Calendar.WeeklyCalendar(),
            "daily" => new Quartz.Impl.Calendar.DailyCalendar("00:00", "23:59"),
            "cron" => new Quartz.Impl.Calendar.CronCalendar(req.CronExpression ?? "0 0 0 * * ?"),
            "annual" => new Quartz.Impl.Calendar.AnnualCalendar(),
            _ => null,
        };

        if (calendar == null)
            return Results.BadRequest(new
                { Error = $"Unsupported calendar type '{req.Type}'. Supported: holiday, monthly, weekly, daily, cron, annual" });

        if (!string.IsNullOrWhiteSpace(req.Description))
            calendar.Description = req.Description;

        await sched.AddCalendar(req.Name, calendar, replace: false, updateTriggers: false);
        return Results.Ok(new StatusResponse("created", Calendar: req.Name));
    }

    public static async Task<IResult> DeleteCalendar(IScheduler sched, string name,
        QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return DashboardResults.ReadOnly();
        var names = await sched.GetCalendarNames();
        if (!names.Contains(name))
            return Results.NotFound(new { Error = $"Calendar '{name}' not found" });

        await sched.DeleteCalendar(name);
        return Results.Ok(new StatusResponse("deleted", Calendar: name));
    }
}
