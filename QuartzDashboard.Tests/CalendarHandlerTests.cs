using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class CalendarHandlerTests : IClassFixture<QuartzTestFixture>
{
    private readonly QuartzTestFixture _fixture;
    private readonly HttpClient _client;

    public CalendarHandlerTests(QuartzTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    // ── list ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCalendars_ReturnsOkWithArray()
    {
        var response = await _client.GetAsync("/quartz/api/calendars");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    // ── create ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCalendar_Holiday_ReturnsCreated()
    {
        var response = await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "HolidayCal",
            type = "holiday",
            description = "Public holidays",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("created", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("HolidayCal", doc.RootElement.GetProperty("calendar").GetString());
    }

    [Fact]
    public async Task CreateCalendar_Weekly_ReturnsCreated()
    {
        var response = await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "WeeklyCal",
            type = "weekly",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("created", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateCalendar_Cron_ReturnsCreated()
    {
        var response = await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "CronCal",
            type = "cron",
            cronExpression = "0 0 0 * * ?",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("created", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateCalendar_Annual_ReturnsCreated()
    {
        var response = await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "AnnualCal",
            type = "annual",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateCalendar_InvalidType_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "BadTypeCal",
            type = "unknown_type",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task CreateCalendar_MissingName_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "",
            type = "holiday",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCalendar_NullBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/quartz/api/calendars",
            new StringContent("null", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCalendar_Duplicate_ReturnsConflict()
    {
        await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "DuplicateCal",
            type = "monthly",
        }));

        var second = await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "DuplicateCal",
            type = "monthly",
        }));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ── delete ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCalendar_ExistingCalendar_ReturnsOk()
    {
        await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "DeleteMe",
            type = "holiday",
        }));

        var response = await _client.DeleteAsync("/quartz/api/calendars/DeleteMe");

        // DELETE returns 204 NoContent per REST convention (handler updated; the SPA
        // checks res.ok, so 204 is fine on the consumer side).
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Confirm the calendar is actually gone from the scheduler.
        var listResponse = await _client.GetAsync("/quartz/api/calendars");
        var listJson = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"DeleteMe\"", listJson);
    }

    [Fact]
    public async Task DeleteCalendar_NonExistentCalendar_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/quartz/api/calendars/DoesNotExist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── list after create ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCalendars_AfterCreate_IncludesNewCalendar()
    {
        await _client.PostAsync("/quartz/api/calendars", Json(new
        {
            name = "ListVerifyCal",
            type = "annual",
        }));

        var response = await _client.GetAsync("/quartz/api/calendars");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var found = doc.RootElement.EnumerateArray()
            .Any(el => el.TryGetProperty("name", out var n) && n.GetString() == "ListVerifyCal");
        Assert.True(found);
    }
}
