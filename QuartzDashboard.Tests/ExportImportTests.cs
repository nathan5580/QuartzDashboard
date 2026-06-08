using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class ExportImportTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public ExportImportTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Export_ReturnsValidJsonWithJobsArray()
    {
        await CreateDurableJob("ExportTestJob");

        var response = await _client.GetAsync("/quartz/api/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("jobs", out var jobs));
        Assert.True(doc.RootElement.TryGetProperty("exportedAt", out _));
        Assert.Equal(JsonValueKind.Array, jobs.ValueKind);
    }

    [Fact]
    public async Task Export_IncludesCreatedJob()
    {
        var name = "ExportFindJob";
        await CreateDurableJob(name);

        var response = await _client.GetAsync("/quartz/api/export");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var jobs = doc.RootElement.GetProperty("jobs");

        var found = false;
        foreach (var job in jobs.EnumerateArray())
        {
            if (job.GetProperty("name").GetString() == name)
            {
                found = true;
                Assert.Equal("DEFAULT", job.GetProperty("group").GetString());
                break;
            }
        }
        Assert.True(found, $"Exported jobs should contain '{name}'");
    }

    [Fact]
    public async Task Import_CreatesNewJob()
    {
        var importPayload = new
        {
            jobs = new[]
            {
                new
                {
                    group = "DEFAULT",
                    name = "ImportedTestJob",
                    description = "Imported via test",
                    jobType = "",
                    durable = true,
                }
            }
        };

        var response = await _client.PostAsync("/quartz/api/import", JsonContent(importPayload));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("jobsCreated").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("errors").GetInt32());

        var getResponse = await _client.GetAsync("/quartz/api/jobs/DEFAULT/ImportedTestJob");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Import_WithTrigger_CreatesJobAndTrigger()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var jobName = $"ImportWithTrigger-{suffix}";
        var triggerName = $"T-{suffix}";

        var importPayload = new
        {
            jobs = new[]
            {
                new
                {
                    group = "DEFAULT",
                    name = jobName,
                    jobType = "",
                    durable = true,
                    triggers = new[]
                    {
                        new
                        {
                            group = "DEFAULT",
                            name = triggerName,
                            intervalSeconds = 120,
                        }
                    }
                }
            }
        };

        var response = await _client.PostAsync("/quartz/api/import", JsonContent(importPayload));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("jobsCreated").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("triggersCreated").GetInt32());

        using var triggerDetail = await _client.GetAsync($"/quartz/api/triggers/DEFAULT/{triggerName}");
        Assert.Equal(HttpStatusCode.OK, triggerDetail.StatusCode);
    }

    [Fact]
    public async Task Import_WithCronTrigger_CreatesCorrectly()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var jobName = $"ImportCron-{suffix}";
        var triggerName = $"CronT-{suffix}";

        var importPayload = new
        {
            jobs = new[]
            {
                new
                {
                    group = "DEFAULT",
                    name = jobName,
                    jobType = "",
                    durable = true,
                    triggers = new[]
                    {
                        new
                        {
                            group = "DEFAULT",
                            name = triggerName,
                            cronExpression = "0/30 * * * * ?",
                        }
                    }
                }
            }
        };

        using var response = await _client.PostAsync("/quartz/api/import", JsonContent(importPayload));
        response.EnsureSuccessStatusCode();

        using var detail = await _client.GetAsync($"/quartz/api/triggers/DEFAULT/{triggerName}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task Import_WithMissingType_ReportsPlaceholderWarning()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var importPayload = new
        {
            jobs = new[]
            {
                new
                {
                    group = "DEFAULT",
                    name = $"ImportPlaceholder-{suffix}",
                    jobType = "NonExistentType",
                    durable = true,
                }
            }
        };

        using var response = await _client.PostAsync("/quartz/api/import", JsonContent(importPayload));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var placeholderJobs = doc.RootElement.GetProperty("placeholderJobs");
        Assert.True(placeholderJobs.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Import_EmptyPayload_ReturnsBadRequest()
    {
        using var response = await _client.PostAsync("/quartz/api/import",
            JsonContent(new { jobs = Array.Empty<object>() }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_MissingName_ReturnsValidationError()
    {
        var importPayload = new
        {
            jobs = new[]
            {
                new
                {
                    group = "DEFAULT",
                    name = "",
                    jobType = "",
                    durable = true,
                }
            }
        };

        using var response = await _client.PostAsync("/quartz/api/import", JsonContent(importPayload));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("jobsCreated").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task Import_WithExistingJobName_ReplacesJob()
    {
        await CreateDurableJob("ReplaceMeJob");

        var importPayload = new
        {
            jobs = new[]
            {
                new
                {
                    group = "DEFAULT",
                    name = "ReplaceMeJob",
                    description = "Updated description",
                    jobType = "",
                    durable = true,
                }
            }
        };

        using var response = await _client.PostAsync("/quartz/api/import", JsonContent(importPayload));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("jobsCreated").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("errors").GetInt32());
    }

    private async Task CreateDurableJob(string name, string group = "DEFAULT")
    {
        var resp = await _client.PostAsync("/quartz/api/jobs",
            JsonContent(new { name, group, description = "", jobType = "", isDurable = true }));
        resp.EnsureSuccessStatusCode();
    }
}
