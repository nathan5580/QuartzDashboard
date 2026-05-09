using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardSignalRTests : IAsyncLifetime
{
    private TestWebAppFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestWebAppFactory();
        await _factory.StartServerAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SignalRHub_DefaultConfiguration_IsConnectable()
    {
        await using var connection = _factory.CreateHubConnection();

        await connection.StartAsync();
        await connection.InvokeAsync("Subscribe");

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task SignalRHub_WhenManualJobExecutes_ClientReceivesEvents()
    {
        using var client = _factory.CreateAnonymousClient();
        await using var connection = _factory.CreateHubConnection();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<JsonElement[]>("jobExecutedBatch", events =>
        {
            if (events.Any(item => item.GetProperty("jobKey").GetString() == "demo.ManualJob"))
                completion.TrySetResult(true);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("Subscribe");
        await client.TriggerManualJobAsync();

        var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(completion.Task, completed);
        Assert.True(await completion.Task);
    }

    [Fact]
    public async Task SignalRHub_RequireAuthenticationWithoutUser_ReturnsUnauthorized()
    {
        await using var customFactory = new TestWebAppFactory(options => options.RequireAuthentication = true);
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.PostAsync("/quartz/hub/negotiate?negotiateVersion=1", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignalRHub_AllowedRolesWithMatchingUser_CanConnect()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.RequireAuthentication = true;
            options.AllowedRoles = ["Admin"];
        });
        await using var connection = customFactory.CreateHubConnection(user: "admin@example.com", roles: ["Admin"]);

        await connection.StartAsync();
        await connection.InvokeAsync("Subscribe");

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }
}
