using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class TriggerHandlerTests : IClassFixture<QuartzTestFixture>
{
    private readonly QuartzTestFixture _fixture;
    private readonly HttpClient _client;

    public TriggerHandlerTests(QuartzTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private async Task<string> CreateDurableJob(string name, string group = "DEFAULT")
    {
        var resp = await _client.PostAsync("/quartz/api/jobs",
            Json(new { name, group, description = "", jobType = "", isDurable = true }));
        resp.EnsureSuccessStatusCode();
        return name;
    }

    // ── list ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllTriggers_ReturnsOkWithPaginationShape()
    {
        var response = await _client.GetAsync("/quartz/api/triggers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("data", out _));
        Assert.True(doc.RootElement.TryGetProperty("total", out _));
        Assert.True(doc.RootElement.TryGetProperty("offset", out _));
        Assert.True(doc.RootElement.TryGetProperty("limit", out _));
    }

    [Fact]
    public async Task GetAllTriggers_OffsetAndLimit_AreRespected()
    {
        var response = await _client.GetAsync("/quartz/api/triggers?offset=0&limit=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("offset").GetInt32());
        Assert.Equal(5, doc.RootElement.GetProperty("limit").GetInt32());
    }

    // ── create ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTrigger_WithCronSchedule_ReturnsOk()
    {
        await CreateDurableJob("CronTriggerJob");

        var response = await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "CronTrigger",
            group = "DEFAULT",
            jobName = "CronTriggerJob",
            jobGroup = "DEFAULT",
            cronExpression = "0/30 * * * * ?",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("created", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateTrigger_WithIntervalSchedule_ReturnsOk()
    {
        await CreateDurableJob("IntervalTriggerJob");

        var response = await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "IntervalTrigger",
            group = "DEFAULT",
            jobName = "IntervalTriggerJob",
            jobGroup = "DEFAULT",
            intervalSeconds = 60,
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("created", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateTrigger_ForNonExistentJob_ReturnsNotFound()
    {
        var response = await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "OrphanTrigger",
            group = "DEFAULT",
            jobName = "DoesNotExistJob",
            jobGroup = "DEFAULT",
            intervalSeconds = 60,
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTrigger_MissingName_ReturnsBadRequest()
    {
        await CreateDurableJob("NamelessTriggerJob");

        var response = await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "",
            jobName = "NamelessTriggerJob",
            intervalSeconds = 60,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTrigger_MissingSchedule_ReturnsBadRequest()
    {
        await CreateDurableJob("NoScheduleJob");

        var response = await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "NoScheduleTrigger",
            jobName = "NoScheduleJob",
            // no cronExpression, no intervalSeconds
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── detail ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTriggerDetail_ValidKey_ReturnsOkWithFields()
    {
        await CreateDurableJob("DetailJob");
        await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "DetailTrigger",
            group = "DEFAULT",
            jobName = "DetailJob",
            jobGroup = "DEFAULT",
            intervalSeconds = 120,
        }));

        var response = await _client.GetAsync("/quartz/api/triggers/DEFAULT/DetailTrigger");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("DetailTrigger", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("DEFAULT", doc.RootElement.GetProperty("group").GetString());
        Assert.True(doc.RootElement.TryGetProperty("state", out _));
        Assert.True(doc.RootElement.TryGetProperty("jobName", out _));
        Assert.True(doc.RootElement.TryGetProperty("intervalSeconds", out _));
        Assert.True(doc.RootElement.TryGetProperty("misfireInstruction", out _));
    }

    [Fact]
    public async Task UpdateTrigger_WithCronSchedule_ReturnsUpdated()
    {
        await CreateDurableJob("UpdateCronJob");
        await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "UpdateCronTrigger",
            group = "DEFAULT",
            jobName = "UpdateCronJob",
            jobGroup = "DEFAULT",
            cronExpression = "0/30 * * * * ?",
        }));

        var response = await _client.PutAsync("/quartz/api/triggers/DEFAULT/UpdateCronTrigger", Json(new
        {
            cronExpression = "0/45 * * * * ?",
            misfireInstruction = "doNothing",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detailResponse = await _client.GetAsync("/quartz/api/triggers/DEFAULT/UpdateCronTrigger");
        var detailJson = await detailResponse.Content.ReadAsStringAsync();
        using var detailDoc = JsonDocument.Parse(detailJson);
        Assert.Equal("0/45 * * * * ?", detailDoc.RootElement.GetProperty("cronExpression").GetString());
        Assert.Equal("DoNothing", detailDoc.RootElement.GetProperty("misfireInstruction").GetString());
    }

    [Fact]
    public async Task GetTriggerDetail_InvalidKey_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/quartz/api/triggers/DEFAULT/NonExistentTrigger");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTriggerNextFires_ReturnsArrayOfDateTimeOffsets()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var jobName = $"NextFiresJob-{suffix}";
        var triggerName = $"NextFiresTrigger-{suffix}";
        var startTimeUtc = DateTimeOffset.UtcNow.AddHours(1);

        await CreateDurableJob(jobName);
        var createResponse = await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = triggerName,
            group = "DEFAULT",
            jobName,
            jobGroup = "DEFAULT",
            intervalSeconds = 60,
            startTimeUtc,
        }));
        createResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/quartz/api/triggers/DEFAULT/{triggerName}/next-fires");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(10, doc.RootElement.GetArrayLength());
        Assert.All(doc.RootElement.EnumerateArray().Select(e => e.GetDateTimeOffset()).ToArray(), fireTime =>
            Assert.True(fireTime >= startTimeUtc));
    }

    [Fact]
    public async Task GetTriggerNextFires_NonExistentTrigger_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/quartz/api/triggers/DEFAULT/DoesNotExist/next-fires");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Trigger not found", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetTriggerNextFires_CountQueryParameter_IsRespected()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var jobName = $"NextFiresCountJob-{suffix}";
        var triggerName = $"NextFiresCountTrigger-{suffix}";
        var startTimeUtc = DateTimeOffset.UtcNow.AddHours(2);

        await CreateDurableJob(jobName);
        var createResponse = await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = triggerName,
            group = "DEFAULT",
            jobName,
            jobGroup = "DEFAULT",
            intervalSeconds = 120,
            startTimeUtc,
        }));
        createResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/quartz/api/triggers/DEFAULT/{triggerName}/next-fires?count=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }

    // ── pause / resume ───────────────────────────────────────────────────

    [Fact]
    public async Task PauseTrigger_ExistingTrigger_ReturnsOk()
    {
        await CreateDurableJob("PauseJob");
        await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "PauseTrigger",
            group = "DEFAULT",
            jobName = "PauseJob",
            jobGroup = "DEFAULT",
            intervalSeconds = 120,
        }));

        var response = await _client.PostAsync(
            "/quartz/api/triggers/DEFAULT/PauseTrigger/pause",
            new StringContent(""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("paused", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ResumeTrigger_PausedTrigger_ReturnsOk()
    {
        await CreateDurableJob("ResumeJob");
        await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "ResumeTrigger",
            group = "DEFAULT",
            jobName = "ResumeJob",
            jobGroup = "DEFAULT",
            intervalSeconds = 120,
        }));
        await _client.PostAsync(
            "/quartz/api/triggers/DEFAULT/ResumeTrigger/pause",
            new StringContent(""));

        var response = await _client.PostAsync(
            "/quartz/api/triggers/DEFAULT/ResumeTrigger/resume",
            new StringContent(""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("resumed", doc.RootElement.GetProperty("status").GetString());
    }

    // ── delete ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTrigger_ExistingTrigger_ReturnsOk()
    {
        await CreateDurableJob("DeleteTriggerJob");
        await _client.PostAsync("/quartz/api/triggers", Json(new
        {
            name = "DeleteThisTrigger",
            group = "DEFAULT",
            jobName = "DeleteTriggerJob",
            jobGroup = "DEFAULT",
            intervalSeconds = 120,
        }));

        var response = await _client.DeleteAsync(
            "/quartz/api/triggers/DEFAULT/DeleteThisTrigger");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("deleted", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteTrigger_NonExistentKey_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync(
            "/quartz/api/triggers/DEFAULT/NeverCreatedTrigger");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
