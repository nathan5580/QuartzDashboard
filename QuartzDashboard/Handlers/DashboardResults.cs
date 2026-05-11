using Microsoft.AspNetCore.Http;

namespace QuartzDashboard.Handlers;

internal static class DashboardResults
{
    public static IResult ReadOnly()
        => Results.Json(new
        {
            Error = "Dashboard is in read-only mode.",
            Code = "read_only"
        }, statusCode: StatusCodes.Status403Forbidden);
}
