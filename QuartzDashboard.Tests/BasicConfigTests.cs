using Xunit;
using QuartzDashboard;

namespace QuartzDashboard.Tests;

/// <summary>
/// Tests that QuartzDashboardOptions have correct default values.
/// </summary>
public class BasicConfigTests
{
    [Fact]
    public void DefaultOptions_HaveCorrectPath()
    {
        var options = new QuartzDashboardOptions();
        Assert.Equal("/quartz", options.Path);
    }

    [Fact]
    public void DefaultOptions_EnabledIsTrue()
    {
        var options = new QuartzDashboardOptions();
        Assert.True(options.Enabled);
    }

    [Fact]
    public void DefaultOptions_ReadOnlyIsFalse()
    {
        var options = new QuartzDashboardOptions();
        Assert.False(options.ReadOnly);
    }

    [Fact]
    public void DefaultOptions_UseSignalRIsTrue()
    {
        var options = new QuartzDashboardOptions();
        Assert.True(options.UseSignalR);
    }

    [Fact]
    public void DefaultOptions_RequireAuthenticationIsFalse()
    {
        var options = new QuartzDashboardOptions();
        Assert.False(options.RequireAuthentication);
    }

    [Fact]
    public void DefaultOptions_AllowedRolesIsEmpty()
    {
        var options = new QuartzDashboardOptions();
        Assert.Empty(options.AllowedRoles);
    }

    [Fact]
    public void DefaultOptions_RequiredPolicyIsEmpty()
    {
        var options = new QuartzDashboardOptions();
        Assert.Equal("", options.RequiredPolicy);
    }

    [Fact]
    public void DefaultOptions_MaxFireHistoryIs500()
    {
        var options = new QuartzDashboardOptions();
        Assert.Equal(500, options.MaxFireHistory);
    }

    [Fact]
    public void DefaultOptions_MaxExecutionLogsPerJobIs50()
    {
        var options = new QuartzDashboardOptions();
        Assert.Equal(50, options.MaxExecutionLogsPerJob);
    }

    [Fact]
    public void CanSetCustomOptions()
    {
        var options = new QuartzDashboardOptions
        {
            Path = "/admin",
            Enabled = false,
            ReadOnly = true,
            UseSignalR = false,
            RequireAuthentication = true,
            AllowedRoles = ["Admin", "Operator"],
            RequiredPolicy = "DashboardAccess",
            MaxFireHistory = 500,
            MaxExecutionLogsPerJob = 100,
        };

        Assert.Equal("/admin", options.Path);
        Assert.False(options.Enabled);
        Assert.True(options.ReadOnly);
        Assert.False(options.UseSignalR);
        Assert.True(options.RequireAuthentication);
        Assert.Equal(2, options.AllowedRoles.Length);
        Assert.Contains("Admin", options.AllowedRoles);
        Assert.Contains("Operator", options.AllowedRoles);
        Assert.Equal("DashboardAccess", options.RequiredPolicy);
        Assert.Equal(500, options.MaxFireHistory);
        Assert.Equal(100, options.MaxExecutionLogsPerJob);
    }

    [Fact]
    public void MaxFireHistory_MustBePositive()
    {
        var options = new QuartzDashboardOptions { MaxFireHistory = 0 };
        Assert.Equal(0, options.MaxFireHistory);
    }

    [Fact]
    public void AllowedRoles_CanBeAssignedAndRead()
    {
        var options = new QuartzDashboardOptions();
        options.AllowedRoles = ["Role1", "Role2", "Role3"];
        Assert.Equal(3, options.AllowedRoles.Length);
    }
}
