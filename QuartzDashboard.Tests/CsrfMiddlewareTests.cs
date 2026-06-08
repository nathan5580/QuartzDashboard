using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class CsrfMiddlewareTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public CsrfMiddlewareTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    // NOTE: The default test fixture has RequireCsrfHeader = false (predates v4.2 defaults).
    // These tests verify that GET endpoints are NOT blocked even when CSRF is disabled,
    // and that the API endpoint itself responds correctly.

    [Fact]
    public async Task GetEndpoints_WithoutCsrfHeader_StillWork()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/quartz/api/scheduler");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetApiJobs_WithoutCsrfHeader_ReturnsOk()
    {
        var response = await _client.GetAsync("/quartz/api/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetApiTriggers_WithoutCsrfHeader_ReturnsOk()
    {
        var response = await _client.GetAsync("/quartz/api/triggers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_WithXRequestedWithHeader_Succeeds()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var payload = new { name = $"CsrfJob-{suffix}", group = "DEFAULT", description = "", jobType = "", isDurable = true };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/quartz/api/jobs")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_WithXCrsfTokenHeader_Succeeds()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var payload = new { name = $"CsrfTokenJob-{suffix}", group = "DEFAULT", description = "", jobType = "", isDurable = true };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/quartz/api/jobs")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-CSRF-Token", "test-token");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteJob_WithXRequestedWithHeader_Succeeds()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await CreateDurableJob($"CsrfDelete-{suffix}");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/quartz/api/jobs/DEFAULT/CsrfDelete-{suffix}");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK);
    }

    private async Task CreateDurableJob(string name, string group = "DEFAULT")
    {
        var payload = new { name, group, description = "", jobType = "", isDurable = true };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/quartz/api/jobs")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        var resp = await _client.SendAsync(request);
        resp.EnsureSuccessStatusCode();
    }
}
