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
        using var response = await client.GetAsync("/quartz/app.min.css");
        var css = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.AssertTextContentType("text/css");
        Assert.Contains("@keyframes", css);
    }

    [Fact]
    public async Task GetAppJs_DefaultConfiguration_ReturnsJavaScript()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/app.min.js");
        var js = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.AssertTextContentType("application/javascript");
        Assert.NotEmpty(js);
        Assert.Contains("loadSchedulers", js);
    }

    [Fact]
    public async Task GetIndex_DefaultConfiguration_ReferencesEmbeddedAssets()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("app.min.css", html);
        Assert.Contains("app.min.js", html);
        Assert.Contains("charts.min.js", html);
        Assert.Contains("signalr.min.js", html);
    }

    [Fact]
    public async Task GetAppCss_DefaultConfiguration_UsesSystemFontStack()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/app.min.css");
        var css = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("BlinkMacSystemFont", css);
        Assert.DoesNotContain("@font-face", css);
    }
}
