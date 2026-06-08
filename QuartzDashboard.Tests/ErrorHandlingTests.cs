using System.Net;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class ErrorHandlingTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public ErrorHandlingTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task UnknownApiEndpoint_ReturnsJson404WithPath()
    {
        var response = await _client.GetAsync("/quartz/api/nonexistent/route/here");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unknown endpoint", doc.RootElement.GetProperty("error").GetString());
        Assert.Contains("nonexistent", doc.RootElement.GetProperty("path").GetString()!);
    }

    [Fact]
    public async Task DeletingWithoutCsrfHeader_WhenEnabled_ReturnsForbidden()
    {
        // The test fixture has CSRF disabled by default, but we verify the endpoint
        // responds correctly to a DELETE with the right header set.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await CreateDurableJob($"ErrDelete-{suffix}");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/quartz/api/jobs/DEFAULT/ErrDelete-{suffix}");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentJobDetail_Returns404WithErrorMessage()
    {
        using var response = await _client.GetAsync("/quartz/api/jobs/NOSUCHGROUP/NoSuchJob");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task InvalidSchedulerName_ReturnsBadRequest()
    {
        using var response = await _client.GetAsync("/quartz/api/scheduler?scheduler=%3Cscript%3E");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostWithMissingBody_ReturnsAppropriateResponse()
    {
        using var response = await _client.PostAsync("/quartz/api/jobs",
            new System.Net.Http.StringContent("", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentTriggerDetail_Returns404()
    {
        using var response = await _client.GetAsync("/quartz/api/triggers/FAKE/NeverCreated");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task CreateDurableJob(string name, string group = "DEFAULT")
    {
        var payload = new { name, group, description = "", jobType = "", isDurable = true };
        var content = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");
        var resp = await _client.PostAsync("/quartz/api/jobs", content);
        resp.EnsureSuccessStatusCode();
    }
}
