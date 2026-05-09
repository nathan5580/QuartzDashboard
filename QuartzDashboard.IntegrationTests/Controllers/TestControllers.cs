using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuartzDashboard.IntegrationTests.Controllers;

[ApiController]
[Route("api/weather")]
public sealed class WeatherForecastController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<object>> GetWeatherForecast()
    {
        return Ok(new[]
        {
            new { day = "Monday", temperatureC = 21, summary = "Sunny" },
            new { day = "Tuesday", temperatureC = 18, summary = "Cloudy" }
        });
    }
}

[ApiController]
[Route("api/health")]
public sealed class HostHealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> GetHealth()
    {
        return Ok(new
        {
            status = "ok",
            middleware = Response.Headers["X-Test-Middleware"].ToString()
        });
    }
}

[ApiController]
[Route("api/secure")]
public sealed class SecureController : ControllerBase
{
    [Authorize]
    [HttpGet("ping")]
    public ActionResult<object> GetPing() => Ok(new { status = "secure-ok" });
}

[ApiController]
[Route("api/quartz-host")]
public sealed class QuartzHostController : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<object> GetStatus() => Ok(new { area = "host" });
}

[ApiController]
[Route("quartz-status")]
public sealed class QuartzStatusController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> GetStatus() => Ok(new { area = "host-root" });
}
