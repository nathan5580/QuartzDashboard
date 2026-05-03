using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace QuartzDashboard.Middleware;

/// <summary>
/// Authentication and authorization middleware for the Quartz Dashboard branch.
/// Handles 401 for unauthenticated requests, 403 for unauthorized roles/policies.
/// </summary>
internal sealed class QuartzDashboardAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly QuartzDashboardOptions _options;

    public QuartzDashboardAuthMiddleware(RequestDelegate next, QuartzDashboardOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(
                    new { Error = "Authentication required to access the dashboard" }));
            return;
        }

        // Check specific policy if configured
        if (!string.IsNullOrWhiteSpace(_options.RequiredPolicy))
        {
            var authService = ctx.RequestServices
                .GetRequiredService<IAuthorizationService>();
            var result = await authService.AuthorizeAsync(ctx.User, null, _options.RequiredPolicy);
            if (!result.Succeeded)
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(
                        new { Error = "Insufficient permissions to access the dashboard" }));
                return;
            }
        }
        // Check allowed roles if configured (and no specific policy)
        else if (_options.AllowedRoles.Length > 0)
        {
            if (!_options.AllowedRoles.Any(role => ctx.User.IsInRole(role)))
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(
                        new { Error = "Insufficient role to access the dashboard" }));
                return;
            }
        }

        await _next(ctx);
    }
}
