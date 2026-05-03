using Microsoft.AspNetCore.Http;
using QuartzDashboard;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handles the /api/config endpoint for dashboard configuration.
/// </summary>
internal static class ConfigHandlers
{
    public static IResult GetDashboardConfig(HttpContext ctx, QuartzDashboardOptions options)
    {
        var isAuthenticated = ctx.User.Identity?.IsAuthenticated == true;
        var hasFullAccess = true;

        if (options.RequireAuthentication)
        {
            if (!isAuthenticated)
                hasFullAccess = false;
            else if (!string.IsNullOrWhiteSpace(options.RequiredPolicy))
                hasFullAccess = false;
            else if (options.AllowedRoles.Length > 0 &&
                     !options.AllowedRoles.Any(r => ctx.User.IsInRole(r)))
                hasFullAccess = false;
        }

        return Results.Ok(new
        {
            readOnly = options.ReadOnly,
            useSignalR = options.UseSignalR,
            hasFullAccess,
            isAuthenticated,
            basePath = options.Path,
            maxFireHistory = options.MaxFireHistory,
        });
    }
}
