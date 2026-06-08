using System.Net;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class ConfigHandlersTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public ConfigHandlersTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetConfig_ReturnsAllExpectedFields()
    {
        var response = await _client.GetAsync("/quartz/api/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("readOnly", out _));
        Assert.True(doc.RootElement.TryGetProperty("useSignalR", out _));
        Assert.True(doc.RootElement.TryGetProperty("hasFullAccess", out _));
        Assert.True(doc.RootElement.TryGetProperty("isAuthenticated", out _));
        Assert.True(doc.RootElement.TryGetProperty("basePath", out _));
        Assert.True(doc.RootElement.TryGetProperty("maxFireHistory", out _));
        Assert.True(doc.RootElement.TryGetProperty("title", out _));
        Assert.True(doc.RootElement.TryGetProperty("historyRetentionHours", out _));
        Assert.True(doc.RootElement.TryGetProperty("hasPersistentHistory", out _));
        Assert.True(doc.RootElement.TryGetProperty("hasWebhookConfigured", out _));
    }

    [Fact]
    public async Task GetConfig_WithAuthDisabled_IsNotAuthenticated()
    {
        var response = await _client.GetAsync("/quartz/api/config");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task GetConfig_ReturnsCorrectBasePath()
    {
        var response = await _client.GetAsync("/quartz/api/config");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("/quartz", doc.RootElement.GetProperty("basePath").GetString());
    }

    [Fact]
    public async Task GetConfig_DoesNotExposeWebhookUrl()
    {
        var response = await _client.GetAsync("/quartz/api/config");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("webhookUrl", out _));
    }

    [Fact]
    public async Task GetConfig_HasPersistentHistory_IsFalseForDefaultInMemoryStore()
    {
        var response = await _client.GetAsync("/quartz/api/config");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.GetProperty("hasPersistentHistory").GetBoolean());
    }
}
