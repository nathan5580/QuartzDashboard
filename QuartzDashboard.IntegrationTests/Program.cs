using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using QuartzDashboard.IntegrationTests.Fixtures;
using QuartzDashboard.IntegrationTests.Support;

namespace QuartzDashboard.IntegrationTests;

public sealed class Program
{
    public static IHostBuilder CreateHostBuilder(string[]? args = null)
    {
        return Host.CreateDefaultBuilder(args ?? [])
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(options => options.AddServerHeader = false);
                webBuilder.ConfigureServices((context, services) =>
                {
                    var scenario = DashboardTestScenario.FromConfiguration(context.Configuration);
                    services.AddSingleton(scenario);
                    services.AddSingleton<OnAuthorizeTracker>();
                    services.AddSingleton<JobExecutionTracker>();

                    services.AddControllers().AddApplicationPart(typeof(Program).Assembly);

                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("DashboardPolicy", policy =>
                        {
                            policy.AddAuthenticationSchemes(TestAuthHandler.SchemeName);
                            policy.RequireAuthenticatedUser();
                            policy.RequireClaim("permission", "dashboard");
                        });
                    });

                    services.AddQuartz(quartz =>
                    {
                        quartz.SchedulerId = scenario.SchedulerName;
                        quartz.SchedulerName = scenario.SchedulerName;

                        var fastJob = new JobKey("FastJob", "demo");
                        quartz.AddJob<FastJob>(options => options.WithIdentity(fastJob).WithDescription("Runs fast for dashboard history."));
                        quartz.AddTrigger(options => options
                            .ForJob(fastJob)
                            .WithIdentity("FastJob-trigger", "demo")
                            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(1).RepeatForever())
                            .StartNow());

                        var slowJob = new JobKey("SlowJob", "demo");
                        quartz.AddJob<SlowJob>(options => options.WithIdentity(slowJob).WithDescription("Runs a little slower."));
                        quartz.AddTrigger(options => options
                            .ForJob(slowJob)
                            .WithIdentity("SlowJob-trigger", "demo")
                            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(2).RepeatForever())
                            .StartNow());

                        var flakyJob = new JobKey("FlakyJob", "demo");
                        quartz.AddJob<FlakyJob>(options => options.WithIdentity(flakyJob).WithDescription("Fails every other execution."));
                        quartz.AddTrigger(options => options
                            .ForJob(flakyJob)
                            .WithIdentity("FlakyJob-trigger", "demo")
                            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(3).RepeatForever())
                            .StartNow());

                        var cronJob = new JobKey("CronJob", "demo");
                        quartz.AddJob<CronJob>(options => options.WithIdentity(cronJob).WithDescription("Cron based execution."));
                        quartz.AddTrigger(options => options
                            .ForJob(cronJob)
                            .WithIdentity("CronJob-trigger", "demo")
                            .WithCronSchedule("0/4 * * * * ?")
                            .StartNow());

                        quartz.AddJob<ManualJob>(options => options
                            .WithIdentity(new JobKey("ManualJob", "demo"))
                            .WithDescription("Triggered on demand from tests.")
                            .StoreDurably());
                    });

                    services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
                    services.AddQuartzDashboard(options => scenario.Apply(options, services));
                });

                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.Use(async (context, next) =>
                    {
                        context.Response.Headers["X-Test-Middleware"] = "executed";
                        await next();
                    });
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseQuartzDashboard();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapGet("/", () => Results.Ok(new { name = "QuartzDashboard test host" }));
                        endpoints.MapQuartzDashboard();
                    });
                });
            });
    }
}
