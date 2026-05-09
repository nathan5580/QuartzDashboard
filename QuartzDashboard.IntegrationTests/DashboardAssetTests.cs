using System.Net;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardAssetTests : IAsyncLifetime
{
    private TestWebAppFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestWebAppFactory();
        await _factory.StartServerAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetAppCss_DefaultConfiguration_ReturnsCss()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/app.css");
        var css = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.AssertTextContentType("text/css");
        Assert.Contains("@keyframes", css);
    }

    [Fact]
    public async Task GetAppJs_DefaultConfiguration_ReturnsJavaScript()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/app.js");
        var js = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.AssertTextContentType("application/javascript");
        Assert.Contains("function dashboard()", js);
    }

    [Fact]
    public async Task GetIndex_DefaultConfiguration_ReferencesEmbeddedAssets()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("app.css", html);
        Assert.Contains("app.js", html);
        Assert.Contains("signalr.min.js", html);
    }

    [Fact]
    public async Task GetFonts_UseSystemFontsFalse_ReturnsEmbeddedFonts()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/fonts/inter-latin.woff2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 10000);
    }

    [Fact]
    public async Task GetFonts_UseSystemFontsTrue_ReturnsNotFound()
    {
        await using var customFactory = new TestWebAppFactory(options => options.UseSystemFonts = true);
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/fonts/inter-latin.woff2");
        var html = await client.GetStringAsync("/quartz/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("BlinkMacSystemFont", html);
    }
}
