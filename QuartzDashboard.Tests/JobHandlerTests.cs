using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

/// <summary>
/// Tests that job CRUD operations work correctly via TestServer.
/// Uses shared CollectionFixture to avoid Quartz LogProvider disposal conflicts.
/// </summary>
[Collection("QuartzDashboard")]
public class JobHandlerTests : IClassFixture<QuartzTestFixture>
{
    private readonly QuartzTestFixture _fixture;
    private readonly HttpClient _client;

    public JobHandlerTests(QuartzTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAllJobs_ReturnsOk()
    {
        var response = await _client.GetAsync("/quartz/api/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("total", out _));
        Assert.True(root.TryGetProperty("data", out _));
        Assert.True(root.TryGetProperty("offset", out _));
        Assert.True(root.TryGetProperty("limit", out _));
    }

    [Fact]
    public async Task CreateJob_ReturnsCreated()
    {
        var payload = new
        {
            name = "TestJob",
            group = "DEFAULT",
            description = "A test job",
            jobType = "",
            isDurable = true,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/quartz/api/jobs", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("created", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateAndGetJob_WorksCorrectly()
    {
        // Create a job
        var createPayload = new
        {
            name = "GetTestJob",
            group = "DEFAULT",
            description = "Job to test GET",
            jobType = "",
            isDurable = true,
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createPayload),
            Encoding.UTF8,
            "application/json");

        var createResponse = await _client.PostAsync("/quartz/api/jobs", createContent);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        // Get all jobs and verify it appears
        var getResponse = await _client.GetAsync("/quartz/api/jobs");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var json = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("total").GetInt32() >= 1);

        var data = root.GetProperty("data");
        var found = false;
        foreach (var job in data.EnumerateArray())
        {
            if (job.GetProperty("name").GetString() == "GetTestJob")
            {
                found = true;
                Assert.Equal("DEFAULT", job.GetProperty("group").GetString());
                Assert.Equal("Job to test GET", job.GetProperty("description").GetString());
                break;
            }
        }
        Assert.True(found, "Created job should be found in list");
    }

    [Fact]
    public async Task GetJobDetail_ReturnsJobDetail()
    {
        // First create a job
        var createPayload = new
        {
            name = "DetailTestJob",
            group = "DEFAULT",
            description = "Job for detail test",
            jobType = "",
            isDurable = true,
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createPayload),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/quartz/api/jobs", createContent);

        // Get job detail
        var response = await _client.GetAsync("/quartz/api/jobs/DEFAULT/DetailTestJob");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("DetailTestJob", root.GetProperty("name").GetString());
        Assert.Equal("DEFAULT", root.GetProperty("group").GetString());
        Assert.Equal("Job for detail test", root.GetProperty("description").GetString());
    }

    [Fact]
    public async Task GetNonExistentJobDetail_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/quartz/api/jobs/NONEXISTENT/FakeJob");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteJob_RemovesJob()
    {
        // Create a job
        var createPayload = new
        {
            name = "DeleteTestJob",
            group = "DEFAULT",
            description = "Job to delete",
            jobType = "",
            isDurable = true,
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createPayload),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/quartz/api/jobs", createContent);

        // Delete the job
        var deleteResponse = await _client.DeleteAsync("/quartz/api/jobs/DEFAULT/DeleteTestJob");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var json = await deleteResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("deleted", doc.RootElement.GetProperty("status").GetString());

        // Verify it's gone
        var getResponse = await _client.GetAsync("/quartz/api/jobs/DEFAULT/DeleteTestJob");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task TriggerJob_ReturnsTriggered()
    {
        // Create a durable job
        var createPayload = new
        {
            name = "TriggerTestJob",
            group = "DEFAULT",
            description = "Job to trigger",
            jobType = "",
            isDurable = true,
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createPayload),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/quartz/api/jobs", createContent);

        // Trigger the job
        var triggerResponse = await _client.PostAsync(
            "/quartz/api/jobs/DEFAULT/TriggerTestJob/trigger", null);
        Assert.Equal(HttpStatusCode.OK, triggerResponse.StatusCode);

        var json = await triggerResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("triggered", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task TriggerJob_WithDataMap_ReturnsTriggered()
    {
        var createPayload = new
        {
            name = "TriggerJobWithDataMap",
            group = "DEFAULT",
            description = "Job to trigger with payload",
            jobType = "",
            isDurable = true,
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createPayload),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/quartz/api/jobs", createContent);

        var triggerPayload = new
        {
            dataMap = new Dictionary<string, string>
            {
                ["source"] = "dashboard-tests",
                ["mode"] = "manual",
            },
        };

        var triggerContent = new StringContent(
            JsonSerializer.Serialize(triggerPayload),
            Encoding.UTF8,
            "application/json");

        var triggerResponse = await _client.PostAsync(
            "/quartz/api/jobs/DEFAULT/TriggerJobWithDataMap/trigger", triggerContent);
        Assert.Equal(HttpStatusCode.OK, triggerResponse.StatusCode);

        var json = await triggerResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("triggered", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PauseAndResumeJob_WorksCorrectly()
    {
        // Create a job
        var createPayload = new
        {
            name = "PauseResumeTestJob",
            group = "DEFAULT",
            description = "Job to pause/resume",
            jobType = "",
            isDurable = true,
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(createPayload),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/quartz/api/jobs", createContent);

        // Pause
        var pauseResponse = await _client.PostAsync(
            "/quartz/api/jobs/DEFAULT/PauseResumeTestJob/pause", null);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);

        // Resume
        var resumeResponse = await _client.PostAsync(
            "/quartz/api/jobs/DEFAULT/PauseResumeTestJob/resume", null);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);

        var json = await resumeResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("resumed", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task JobsEndpoint_WithV1Prefix_Works()
    {
        var response = await _client.GetAsync("/quartz/api/v1/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("total", out _));
    }

    [Fact]
    public async Task CreateJob_WithoutName_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "",
            group = "DEFAULT",
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/quartz/api/jobs", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNonExistentJob_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/quartz/api/jobs/DEFAULT/NoSuchJob");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
