using System.Net;
using Xunit;

namespace QuartzDashboard.Tests;

/// <summary>
/// Tests that the embedded static assets (SignalR JS, Alpine.js, app.css, etc.) are
/// served correctly — ensuring the NuGet is fully autonomous with no CDN dependencies
/// for the critical JS runtime.
/// </summary>
public class EmbeddedAssetsTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public EmbeddedAssetsTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task SignalRJs_IsServedFromEmbeddedResources()
    {
        var response = await _client.GetAsync("/quartz/signalr.min.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task SignalRJs_ContainsHubConnectionBuilder()
    {
        var response = await _client.GetAsync("/quartz/signalr.min.js");
        var content = await response.Content.ReadAsStringAsync();
        // The SignalR JS client always contains HubConnectionBuilder
        Assert.Contains("HubConnectionBuilder", content);
    }

    [Fact]
    public async Task AlpineJs_IsServedFromEmbeddedResources()
    {
        var response = await _client.GetAsync("/quartz/alpine.min.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task AlpineJs_ContainsAlpineMarker()
    {
        var response = await _client.GetAsync("/quartz/alpine.min.js");
        var content = await response.Content.ReadAsStringAsync();
        // Alpine.js always contains its version marker or Alpine reference
        Assert.True(content.Contains("Alpine") || content.Contains("alpine"),
            "alpine.min.js should contain Alpine marker");
    }

    [Fact]
    public async Task AppCss_IsServedFromEmbeddedResources()
    {
        var response = await _client.GetAsync("/quartz/app.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("css", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task ChartsJs_IsServedFromEmbeddedResources()
    {
        var response = await _client.GetAsync("/quartz/charts.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task AppJs_IsServedFromEmbeddedResources()
    {
        var response = await _client.GetAsync("/quartz/app.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task IndexHtml_DoesNotReferenceExternalSignalRCdn()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cdn.jsdelivr.net/npm/@microsoft/signalr", html);
    }

    [Fact]
    public async Task IndexHtml_DoesNotReferenceExternalAlpineCdn()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cdn.jsdelivr.net/npm/alpinejs", html);
    }

    [Fact]
    public async Task IndexHtml_ReferencesEmbeddedSignalRJs()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("signalr.min.js", html);
    }

    [Fact]
    public async Task IndexHtml_ReferencesEmbeddedAlpineJs()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("alpine.min.js", html);
    }

    [Fact]
    public async Task IndexHtml_DoesNotReferenceTailwindCdn()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cdn.tailwindcss.com", html);
    }

    [Fact]
    public async Task IndexHtml_DoesNotReferenceGoogleFontsCdn()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("fonts.googleapis.com", html);
    }

    [Fact]
    public async Task IndexHtml_ReferencesEmbeddedTailwindCss()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("tailwind.css", html);
    }

    [Fact]
    public async Task IndexHtml_HasXCloakForNoFouc()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("x-cloak", html);
    }

    [Fact]
    public async Task TailwindCss_IsServedAsEmbeddedResource()
    {
        var response = await _client.GetAsync("/quartz/tailwind.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(".bg-gray-950", content);
    }

    [Fact]
    public async Task Fonts_InterIsServedAsEmbeddedResource()
    {
        var response = await _client.GetAsync("/quartz/fonts/inter-latin.woff2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 10000);
    }

    [Fact]
    public async Task Fonts_JetBrainsMonoIsServedAsEmbeddedResource()
    {
        var response = await _client.GetAsync("/quartz/fonts/jetbrains-mono-latin.woff2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 10000);
    }
}
