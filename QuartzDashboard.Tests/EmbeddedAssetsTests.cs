using System.Net;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class EmbeddedAssetsTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public EmbeddedAssetsTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    /// <summary>
    /// Tests that the embedded static assets (SignalR JS, Alpine.js, app.css, etc.) are
    /// served correctly — ensuring the NuGet is fully autonomous with no CDN dependencies
    /// for the critical JS runtime.
    /// </summary>
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
        var response = await _client.GetAsync("/quartz/app.min.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("css", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task ChartsJs_IsServedFromEmbeddedResources()
    {
        var response = await _client.GetAsync("/quartz/charts.min.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task AppJs_IsServedFromEmbeddedResources()
    {
        var response = await _client.GetAsync("/quartz/app.min.js");
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
    public async Task IndexHtml_ReferencesEmbeddedAppCss()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("app.min.css", html);
        Assert.Contains("app.min.js", html);
        Assert.Contains("charts.min.js", html);
    }

    [Fact]
    public async Task IndexHtml_HasXCloakForNoFouc()
    {
        var response = await _client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("x-cloak", html);
    }

    [Fact]
    public async Task AppCss_UsesSystemFontStack()
    {
        var response = await _client.GetAsync("/quartz/app.min.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("BlinkMacSystemFont", content);
        Assert.DoesNotContain("@font-face", content);
    }

    [Fact]
    public async Task Fonts_InterIsNoLongerServed()
    {
        var response = await _client.GetAsync("/quartz/fonts/inter-latin.woff2");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Fonts_JetBrainsMonoIsNoLongerServed()
    {
        var response = await _client.GetAsync("/quartz/fonts/jetbrains-mono-latin.woff2");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
