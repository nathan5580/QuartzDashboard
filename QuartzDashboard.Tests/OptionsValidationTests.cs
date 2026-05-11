using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace QuartzDashboard.Tests;

public sealed class OptionsValidationTests
{
    [Fact]
    public void AddQuartzDashboard_ThrowsWhenPathIsEmpty()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
            services.AddQuartzDashboard(options =>
            {
                options.Path = "";
                options.UseSignalR = false;
            }));
    }

    [Fact]
    public void AddQuartzDashboard_ThrowsWhenPathHasNoLeadingSlash()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
            services.AddQuartzDashboard(options =>
            {
                options.Path = "quartz";
                options.UseSignalR = false;
            }));
    }

    [Fact]
    public void AddQuartzDashboard_ThrowsWhenMaxFireHistoryIsNegative()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
            services.AddQuartzDashboard(options =>
            {
                options.MaxFireHistory = -1;
                options.UseSignalR = false;
            }));
    }

    [Fact]
    public void AddQuartzDashboard_ThrowsWhenWebhookUrlIsNotAbsolute()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
            services.AddQuartzDashboard(options =>
            {
                options.WebhookUrl = "/hooks/slack";
                options.UseSignalR = false;
            }));
    }

    [Fact]
    public void AddQuartzDashboard_ThrowsWhenWebhookSchemeIsNotHttp()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Assert.Throws<ArgumentException>(() =>
            services.AddQuartzDashboard(options =>
            {
                options.WebhookUrl = "file:///tmp/x";
                options.UseSignalR = false;
            }));
    }

    [Fact]
    public void AddQuartzDashboard_AcceptsValidWebhookUrl()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddQuartzDashboard(options =>
        {
            options.WebhookUrl = "https://hooks.example.com/abc";
            options.UseSignalR = false;
        });
    }
}
