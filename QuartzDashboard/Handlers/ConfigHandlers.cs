using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using QuartzDashboard;
using QuartzDashboard.Abstractions;
using QuartzDashboard.Internal;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handles the /api/config endpoint for dashboard configuration.
/// </summary>
internal static class ConfigHandlers
{
    public static async Task<IResult> GetDashboardConfig(HttpContext ctx, QuartzDashboardOptions options)
    {
        var isAuthenticated = ctx.User.Identity?.IsAuthenticated == true;
        var hasFullAccess = true;

        if (options.RequireAuthentication)
        {
            if (!isAuthenticated)
            {
                hasFullAccess = false;
            }
            else if (!string.IsNullOrWhiteSpace(options.RequiredPolicy))
            {
                var authService = ctx.RequestServices.GetRequiredService<IAuthorizationService>();
                hasFullAccess = (await authService.AuthorizeAsync(ctx.User, null, options.RequiredPolicy)).Succeeded;
            }
            else if (options.AllowedRoles.Length > 0 &&
                     !options.AllowedRoles.Any(r => ctx.User.IsInRole(r)))
            {
                hasFullAccess = false;
            }
        }

        return Results.Ok(new
        {
            readOnly = options.ReadOnly,
            useSignalR = options.UseSignalR,
            hasFullAccess,
            isAuthenticated,
            basePath = options.Path,
            maxFireHistory = options.MaxFireHistory,
            title = options.Title,
            historyRetentionHours = options.HistoryRetentionHours,
            hasPersistentHistory = IsPersistentStore(ctx),
            hasWebhookConfigured = !string.IsNullOrWhiteSpace(options.WebhookUrl),
        });
    }

    // A persistent store is any IFireHistoryStore implementation other than the default in-memory one.
    // This avoids the main package having to know about specific store packages (Sqlite, custom, etc.).
    private static bool IsPersistentStore(HttpContext ctx)
    {
        var store = ctx.RequestServices.GetService<IFireHistoryStore>();
        return store is not null && store is not InMemoryFireHistoryStore;
    }
}
