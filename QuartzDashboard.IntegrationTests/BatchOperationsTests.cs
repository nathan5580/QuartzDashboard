using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class BatchOperationsTests
{
    [Fact]
    public async Task BatchDeleteJobs_WithValidKeys_DeletesAllJobs()
    {
        await using var factory = new TestWebAppFactory();
        using var client = factory.CreateAnonymousClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await CreateDurableJob(client, $"BatchDeleteA-{suffix}");
        await CreateDurableJob(client, $"BatchDeleteB-{suffix}");

        var payload = new
        {
            jobs = new[] { $"DEFAULT.BatchDeleteA-{suffix}", $"DEFAULT.BatchDeleteB-{suffix}" }
        };

        using var response = await client.PostAsync("/quartz/api/jobs/batch/delete",
            JsonContent.Create(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var list = await client.GetAsync("/quartz/api/jobs");
        var body = await list.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var found = false;
        foreach (var job in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var name = job.GetProperty("name").GetString();
            if (name == $"BatchDeleteA-{suffix}" || name == $"BatchDeleteB-{suffix}")
                found = true;
        }
        Assert.False(found, "Batch-deleted jobs should not appear in job list");
    }

    [Fact]
    public async Task BatchDeleteJobs_WithEmptyArray_ReturnsBadRequest()
    {
        await using var factory = new TestWebAppFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.PostAsync("/quartz/api/jobs/batch/delete",
            JsonContent.Create(new { jobs = Array.Empty<string>() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BatchPauseJobs_WithValidKeys_PausesAll()
    {
        await using var factory = new TestWebAppFactory();
        using var client = factory.CreateAnonymousClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var jobName = $"BatchPauseJob-{suffix}";
        await CreateDurableJob(client, jobName);
        await CreateSimpleTrigger(client, jobName, $"Trigger-{suffix}", 120);

        var payload = new { jobs = new[] { $"DEFAULT.{jobName}" } };
        using var pauseResp = await client.PostAsync("/quartz/api/jobs/batch/pause",
            JsonContent.Create(payload));

        Assert.Equal(HttpStatusCode.OK, pauseResp.StatusCode);

        var body = await pauseResp.Content.ReadAsStringAsync();
        using var pauseDoc = JsonDocument.Parse(body);
        var results = pauseDoc.RootElement.GetProperty("results");
        Assert.True(results.GetArrayLength() > 0);
        Assert.Equal("paused", results[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task BatchResumeJobs_WithValidKeys_ResumesAll()
    {
        await using var factory = new TestWebAppFactory();
        using var client = factory.CreateAnonymousClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var jobName = $"BatchResumeJob-{suffix}";
        await CreateDurableJob(client, jobName);
        await CreateSimpleTrigger(client, jobName, $"Trigger-{suffix}", 120);
        await client.PostAsync("/quartz/api/jobs/batch/pause",
            JsonContent.Create(new { jobs = new[] { $"DEFAULT.{jobName}" } }));

        using var resumeResp = await client.PostAsync("/quartz/api/jobs/batch/resume",
            JsonContent.Create(new { jobs = new[] { $"DEFAULT.{jobName}" } }));

        Assert.Equal(HttpStatusCode.OK, resumeResp.StatusCode);

        var body = await resumeResp.Content.ReadAsStringAsync();
        using var resumeDoc = JsonDocument.Parse(body);
        var results = resumeDoc.RootElement.GetProperty("results");
        Assert.True(results.GetArrayLength() > 0);
        Assert.Equal("resumed", results[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task BatchTriggerJobs_WithValidKeys_TriggersAll()
    {
        await using var factory = new TestWebAppFactory();
        using var client = factory.CreateAnonymousClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var jobName = $"BatchTriggerJob-{suffix}";
        await CreateDurableJob(client, jobName);

        using var resp = await client.PostAsync("/quartz/api/jobs/batch/trigger",
            JsonContent.Create(new { jobs = new[] { $"DEFAULT.{jobName}" } }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task BatchOperations_ReadOnlyMode_ReturnsForbidden()
    {
        await using var factory = new TestWebAppFactory(options => options.ReadOnly = true);
        using var client = factory.CreateAnonymousClient();

        var endpoints = new[]
        {
            "/quartz/api/jobs/batch/delete",
            "/quartz/api/jobs/batch/pause",
            "/quartz/api/jobs/batch/resume",
            "/quartz/api/jobs/batch/trigger",
        };

        foreach (var path in endpoints)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new { jobs = new[] { "DEFAULT.SomeJob" } })
            };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task BatchDeleteJobs_WithMixedKeys_ReportsNotFoundForMissing()
    {
        await using var factory = new TestWebAppFactory();
        using var client = factory.CreateAnonymousClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await CreateDurableJob(client, $"BatchMixed-{suffix}");

        var payload = new
        {
            jobs = new[] { $"DEFAULT.BatchMixed-{suffix}", "DEFAULT.DoesNotExist" }
        };

        using var response = await client.PostAsync("/quartz/api/jobs/batch/delete",
            JsonContent.Create(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var results = doc.RootElement.GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());
    }

    private static async Task CreateDurableJob(HttpClient client, string name, string group = "DEFAULT")
    {
        var resp = await client.PostAsync("/quartz/api/jobs",
            JsonContent.Create(new { name, group, description = "", jobType = "", isDurable = true }));
        resp.EnsureSuccessStatusCode();
    }

    private static async Task CreateSimpleTrigger(HttpClient client, string jobName, string triggerName, int intervalSeconds)
    {
        var resp = await client.PostAsync("/quartz/api/triggers",
            JsonContent.Create(new
            {
                name = triggerName,
                group = "DEFAULT",
                jobName,
                jobGroup = "DEFAULT",
                intervalSeconds,
            }));
        resp.EnsureSuccessStatusCode();
    }
}
