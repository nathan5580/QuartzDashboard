using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Xunit;

namespace QuartzDashboard.Tests;

/// <summary>
/// Shared test fixture that creates a TestServer host for scheduler and job handler tests.
/// Uses IClassFixture so all tests in a class share one host instance,
/// avoiding Quartz LogProvider static logger disposal issues.
/// </summary>
public sealed class QuartzTestFixture : IAsyncLifetime
{
    public IHost Host { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddQuartz(q =>
                        {
                            q.SchedulerId = "TestScheduler";
                            q.SchedulerName = "TestScheduler";
                        });
                        services.AddQuartzDashboard(options =>
                        {
                            options.UseSignalR = false;
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseQuartzDashboard();
                    });
            })
            .StartAsync();

        // Start the scheduler manually (we didn't add Quartz hosted service)
        var schedFactory = Host.Services.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedFactory.GetScheduler();
        if (!scheduler.IsStarted)
            await scheduler.Start();

        Client = Host.GetTestClient();
        Client.BaseAddress = new Uri("http://localhost");
    }

    public async Task DisposeAsync()
    {
        if (Host != null)
        {
            await Host.StopAsync();
            Host.Dispose();
        }
    }
}
