using Microsoft.AspNetCore.Http;

namespace QuartzDashboard.Handlers;

internal static class DashboardResults
{
    public static IResult ReadOnly()
        => Results.Json(new
        {
            error = "Dashboard is in read-only mode.",
            code = "read_only"
        }, statusCode: StatusCodes.Status403Forbidden);
}
