using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzDashboard.Handlers;
using QuartzDashboard.Internal;
using QuartzDashboard.Abstractions;
using QuartzDashboard.Services;
using System.Reflection;
using System.Text.RegularExpressions;

namespace QuartzDashboard;

/// <summary>
/// Extension methods for mounting the Quartz Dashboard.
/// Call <c>app.UseQuartzDashboard()</c> at any point in the pipeline.
/// </summary>
public static partial class QuartzDashboardApplicationBuilderExtensions
{
    // Source-generated, compiled regex — validated once at type-init,
    // zero per-request allocation. Replaces the inline Regex.IsMatch.
    [GeneratedRegex(@"^[\w\-. ]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SchedulerNameRegex();

    private static readonly Assembly ThisAssembly =
        typeof(QuartzDashboardApplicationBuilderExtensions).Assembly;

    private static readonly EmbeddedFileProvider EmbeddedFiles =
        new(ThisAssembly, "QuartzDashboard.wwwroot");

    /// <summary>
    /// Mounts the Quartz Dashboard single-page application and REST API at the configured base path.
    /// This middleware intercepts dashboard requests before endpoint fallbacks so the embedded UI and API remain reachable.
    /// </summary>
    /// <param name="app">The application builder used to register the dashboard middleware.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance so additional middleware can be chained.</returns>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Services.AddQuartz();
    /// builder.Services.AddQuartzHostedService();
    /// builder.Services.AddQuartzDashboard();
    ///
    /// var app = builder.Build();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.UseQuartzDashboard();
    /// app.Run();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseQuartzDashboard(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<QuartzDashboardOptions>();

        // Plan A: Dev-only gating — no-op when disabled
        if (!options.Enabled)
            return app;

        var basePath = options.Path.TrimEnd('/');

        // Use app.Use() instead of app.Map() so that /hub/* requests can pass through
        // to the MapHub endpoint registered below in the outer endpoint routing pipeline.
        app.Use(async (ctx, next) =>
        {
            var reqPath = ctx.Request.Path;

            // Only handle requests that start with our base path
            if (!reqPath.StartsWithSegments(basePath, out var suffix))
            {
                await next(ctx);
                return;
            }

            var suffixStr = suffix.Value ?? "";

            // Auth checks apply to every dashboard request including SignalR negotiate.
            // OnAuthorize returns 403 when the user is authenticated (permission denied)
            // and 401 otherwise (no identity). RequireAuthentication / RequiredPolicy /
            // AllowedRoles run after, in the documented order.
            if (options.OnAuthorize != null && !options.OnAuthorize(ctx))
            {
                var authed = ctx.User.Identity?.IsAuthenticated == true;
                await WriteJsonError(ctx, authed ? 403 : 401, authed ? "Forbidden" : "Unauthorized");
                return;
            }

            if (options.RequireAuthentication)
            {
                if (ctx.User.Identity?.IsAuthenticated != true)
                {
                    await WriteJsonError(ctx, 401, "Authentication required");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(options.RequiredPolicy))
                {
                    var authService = ctx.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
                    var result = await authService.AuthorizeAsync(ctx.User, null, options.RequiredPolicy);
                    if (!result.Succeeded)
                    {
                        await WriteJsonError(ctx, 403, "Insufficient permissions");
                        return;
                    }
                }
                else if (options.AllowedRoles.Length > 0 && !options.AllowedRoles.Any(r => ctx.User.IsInRole(r)))
                {
                    await WriteJsonError(ctx, 403, "Insufficient role");
                    return;
                }
            }

            // SignalR hub negotiate/connect: hand off to MapHub endpoint routing.
            // The auth checks above already gated the request — this lets OnAuthorize
            // and friends apply to the hub the same way they apply to the REST API.
            if (suffixStr.StartsWith("/hub", StringComparison.OrdinalIgnoreCase))
            {
                await next(ctx);
                return;
            }

            // --- API endpoints ---
            if (suffixStr.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                // CSRF: mutating verbs must carry a custom header. Browsers cannot send
                // X-Requested-With or X-CSRF-Token via a simple cross-origin form submit
                // without triggering a preflight (which we never respond OK to from a
                // third-party origin), so the header acts as a same-origin assertion.
                if (options.RequireCsrfHeader && IsMutatingMethod(ctx.Request.Method))
                {
                    var hasHeader = ctx.Request.Headers.ContainsKey("X-CSRF-Token")
                        || (ctx.Request.Headers.TryGetValue("X-Requested-With", out var xrw)
                            && xrw.Any(v => string.Equals(v, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)));
                    if (!hasHeader)
                    {
                        await WriteJsonError(ctx, 403, "Missing CSRF guard header. Send X-Requested-With: XMLHttpRequest or X-CSRF-Token.");
                        return;
                    }
                }
                var schedFactory = app.ApplicationServices.GetRequiredService<ISchedulerFactory>();

                // Multi-scheduler: ?scheduler=SchedulerName header or query param selects which scheduler
                IScheduler sched;
                var schedulerName = ctx.Request.Query["scheduler"].FirstOrDefault();
                if (!string.IsNullOrEmpty(schedulerName) &&
                    (schedulerName.Length > 100 || !SchedulerNameRegex().IsMatch(schedulerName)))
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Invalid scheduler name" });
                    return;
                }
                if (!string.IsNullOrWhiteSpace(schedulerName))
                    sched = await schedFactory.GetScheduler(schedulerName) ?? await schedFactory.GetScheduler();
                else
                    sched = await schedFactory.GetScheduler();

                await HandleApi(ctx, sched, schedFactory, suffixStr, options);
                return;
            }

            // Redirect /quartz → /quartz/ so relative asset URLs resolve correctly
            if (suffixStr == "" && !(ctx.Request.Path.Value ?? "").EndsWith('/'))
            {
                ctx.Response.Redirect(basePath + "/", permanent: false);
                return;
            }

            // --- SPA static files (and root /quartz/ → index.html) ---
            await ServeStaticFile(ctx, suffixStr, basePath, options);
        });

        // Map SignalR hub — only when app is an IEndpointRouteBuilder (WebApplication satisfies this;
        // plain IApplicationBuilder from test hosts does not — call MapQuartzDashboard() separately in that case)
        if (options.UseSignalR && app is IEndpointRouteBuilder erb)
        {
            MapHubEndpoint(erb, basePath, options);
        }

        return app;
    }

    /// <summary>
    /// Maps the dashboard SignalR hub when the application builder used with <see cref="UseQuartzDashboard(IApplicationBuilder)"/>
    /// does not also implement <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    /// <param name="app">The endpoint route builder used to register the dashboard hub endpoint.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder"/> instance so endpoint mappings can be chained.</returns>
    public static IEndpointRouteBuilder MapQuartzDashboard(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<QuartzDashboardOptions>();
        if (!options.Enabled || !options.UseSignalR)
            return app;

        var basePath = options.Path.TrimEnd('/');
        MapHubEndpoint(app, basePath, options);
        return app;
    }

    private static void MapHubEndpoint(IEndpointRouteBuilder app, string basePath, QuartzDashboardOptions options)
    {
        var hub = app.MapHub<QuartzDashboardHub>($"{basePath}/hub");

        if (!options.RequireAuthentication)
            return;

        if (!string.IsNullOrWhiteSpace(options.RequiredPolicy))
        {
            hub.RequireAuthorization(options.RequiredPolicy);
            return;
        }

        if (options.AllowedRoles.Length > 0)
        {
            hub.RequireAuthorization(new AuthorizeAttribute
            {
                Roles = string.Join(',', options.AllowedRoles)
            });
            return;
        }

        hub.RequireAuthorization();
    }

    // ============= Main API Router =============

    private static async Task HandleApi(HttpContext ctx, IScheduler sched,
        ISchedulerFactory schedFactory,
        string path, QuartzDashboardOptions options)
    {
        // Defensive headers apply to JSON responses too, not just static files.
        // Cheap, idempotent, and consistent across the dashboard's surface.
        ApplySecurityHeaders(ctx);

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Strip "/api" or "/api/v1" prefix for API versioning support
        var apiOffset = 1; // skip "api"
        if (segments.Length > 1 && string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase))
            apiOffset = 2;
        var route = segments.Length > apiOffset ? segments[apiOffset..] : [];

        try
        {
            var rc = new ApiRouteContext(ctx, sched, schedFactory, options, route);
            var result = await ApiRouter.Dispatch(rc);

            if (result is IResult ires)
                await ires.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            // Generate a correlation id so operators can match a client-visible 500 to a log entry.
            // Never echo ex.Message — it commonly contains internal details (file paths, SQL, etc.).
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            var logger = ctx.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("QuartzDashboard.Api");
            logger.LogError(ex, "Unhandled error in {Method} {Path} (correlationId={CorrelationId})",
                ctx.Request.Method, ctx.Request.Path.Value, correlationId);

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "Internal server error",
                        correlationId,
                    }));
            }
        }
    }

    private static bool IsMutatingMethod(string method) =>
        string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase);

    private static Task WriteJsonError(HttpContext ctx, int statusCode, string message)
    {
        // Browser navigations send Accept: text/html — return a small HTML page with
        // remediation guidance instead of a raw JSON blob. JSON is still returned for
        // XHR/fetch/curl clients that ask for it or don't set Accept at all.
        var accept = ctx.Request.Headers["Accept"].ToString();
        var wantsHtml = accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);

        ctx.Response.StatusCode = statusCode;

        if (wantsHtml)
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            return ctx.Response.WriteAsync(RenderAuthErrorPage(statusCode, message));
        }

        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = message }));
    }

    private static string RenderAuthErrorPage(int statusCode, string message)
    {
        var title = statusCode == 403 ? "Access denied" : "Authentication required";
        var hint = statusCode == 403
            ? "Your account is signed in but does not have permission to view this dashboard."
            : "This dashboard requires an authenticated user. Sign in to the host application and reload.";
        var safeMessage = System.Net.WebUtility.HtmlEncode(message);
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"" />
<meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
<title>{title} — Quartz Dashboard</title>
<style>
  :root {{ color-scheme: dark light; }}
  body {{ margin:0; min-height:100vh; display:grid; place-items:center; background:#030712; color:#e5e7eb;
    font-family: system-ui, -apple-system, BlinkMacSystemFont, ""Segoe UI"", sans-serif; }}
  .card {{ max-width: 32rem; width: 90vw; padding: 2rem 2.25rem; background: rgba(17,24,39,0.85);
    border: 1px solid rgba(255,255,255,0.08); border-radius: 0.75rem; box-shadow: 0 20px 60px rgba(0,0,0,0.5); }}
  .badge {{ display:inline-block; padding: 2px 10px; border-radius: 9999px; font-size: 0.7rem; font-weight:600;
    background: rgba(239,68,68,0.15); color: #fca5a5; box-shadow: inset 0 0 0 1px rgba(239,68,68,0.2); }}
  h1 {{ margin: 0.75rem 0 0.5rem; font-size: 1.25rem; font-weight: 600; color: #f3f4f6; }}
  p {{ margin: 0 0 0.5rem; line-height: 1.55; color: #cbd5e1; }}
  p.muted {{ color: #9ca3af; font-size: 0.85rem; }}
  code {{ background: rgba(255,255,255,0.05); padding: 1px 6px; border-radius: 4px; font-size: 0.85em; }}
  a {{ color: #818cf8; text-decoration: none; }}
  a:hover {{ text-decoration: underline; }}
  @media (prefers-color-scheme: light) {{
    body {{ background: #f3f4f6; color: #1f2937; }}
    .card {{ background: #ffffff; border-color: rgba(0,0,0,0.08); }}
    h1 {{ color: #111827; }} p {{ color: #374151; }} p.muted {{ color: #6b7280; }}
    code {{ background: rgba(0,0,0,0.05); }}
  }}
</style>
</head>
<body>
  <main class=""card"" role=""main"">
    <span class=""badge"">{statusCode} — {safeMessage}</span>
    <h1>{title}</h1>
    <p>{hint}</p>
    <p class=""muted"">If you are the developer: this dashboard's <code>RequireAuthentication</code> option defaults to <code>true</code> since v4.2.0. Either sign in via the host app's authentication middleware, or set <code>options.RequireAuthentication = false</code> for trusted local environments. See <a href=""https://github.com/nathan5580/QuartzDashboard#authentication"">the auth docs</a>.</p>
  </main>
</body>
</html>";
    }

    private static readonly string AssemblyVersion =
        ThisAssembly.GetName().Version?.ToString(3) ?? "0";

    // ============= Static File Serving =============

    // ETag for embedded static assets — bundled with the package so they only
    // change across releases. Using the assembly version as a weak validator
    // means Day-2 visits get 304s for every cached asset.
    private static readonly string AssetETag = $"W/\"{AssemblyVersion}\"";

    private static async Task ServeStaticFile(HttpContext ctx, string path, string basePath, QuartzDashboardOptions options)
    {
        var relativePath = path.TrimStart('/');
        if (string.IsNullOrEmpty(relativePath))
            relativePath = "index.html";

        var filePath = relativePath.Contains('?')
            ? relativePath[..relativePath.IndexOf('?')]
            : relativePath;

        var fileInfo = EmbeddedFiles.GetFileInfo(filePath);

        if (fileInfo.Exists && !fileInfo.IsDirectory)
        {
            ctx.Response.ContentType = ScheduleHelper.GetContentType(filePath);
            ctx.Response.Headers.CacheControl = filePath == "index.html"
                ? "no-cache"
                : "public, max-age=86400";
            ApplySecurityHeaders(ctx);

            if (filePath == "index.html")
            {
                await ServeIndexHtml(ctx, basePath, options);
                return;
            }

            // ETag short-circuit for the long-cached assets. AssetETag is keyed
            // to the package version, so a deploy invalidates correctly without
            // needing per-file content hashes.
            ctx.Response.Headers.ETag = AssetETag;
            if (ctx.Request.Headers.IfNoneMatch.Count > 0
                && ctx.Request.Headers.IfNoneMatch.ToString() == AssetETag)
            {
                ctx.Response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            await ctx.Response.SendFileAsync(fileInfo);
        }
        else if (filePath.Contains('.'))
        {
            // Known file extension but not embedded — return 404 (don't SPA-fallback for assets)
            ctx.Response.StatusCode = 404;
        }
        else
        {
            // SPA fallback for client-side routes
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.CacheControl = "no-cache";
            ApplySecurityHeaders(ctx);
            await ServeIndexHtml(ctx, basePath, options);
        }
    }

    private static void ApplySecurityHeaders(HttpContext ctx)
    {
        // Defensive headers for the dashboard surface. These are deliberately self-contained
        // (set on each response we own) so they apply even when the host app hasn't wired up
        // a global security-headers middleware.
        var headers = ctx.Response.Headers;
        if (!headers.ContainsKey("X-Content-Type-Options")) headers["X-Content-Type-Options"] = "nosniff";
        if (!headers.ContainsKey("X-Frame-Options")) headers["X-Frame-Options"] = "SAMEORIGIN";
        if (!headers.ContainsKey("Referrer-Policy")) headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    }

    // Cache the token-replaced index.html bytes once per (basePath, title) tuple.
    // The embedded resource is read + 3× string-replace + UTF-8 encoded only on
    // first hit — every subsequent navigation writes the cached byte[] straight
    // to the response. Concurrent first-hits race safely (last write wins on
    // identical content). The keying tuple matters because basePath comes from
    // options.Path (mostly "/quartz") and title is host-configurable.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string BasePath, string Title), byte[]> _indexHtmlCache = new();

    private static async Task ServeIndexHtml(HttpContext ctx, string basePath, QuartzDashboardOptions options)
    {
        var key = (basePath, options.Title);
        var bytes = _indexHtmlCache.GetOrAdd(key, static k =>
        {
            var fileInfo = EmbeddedFiles.GetFileInfo("index.html");
            using var stream = fileInfo.CreateReadStream();
            using var reader = new System.IO.StreamReader(stream);
            var html = reader.ReadToEnd();
            html = html.Replace("'__QUARTZ_BASE__'", $"'{k.BasePath}'");
            html = html.Replace("__QUARTZ_VERSION__", AssemblyVersion);
            html = html.Replace("__QUARTZ_TITLE__", System.Text.Encodings.Web.HtmlEncoder.Default.Encode(k.Title));
            return System.Text.Encoding.UTF8.GetBytes(html);
        });

        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.Body.WriteAsync(bytes);
    }
}

