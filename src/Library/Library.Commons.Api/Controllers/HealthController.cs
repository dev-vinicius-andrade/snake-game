using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Library.Commons.Api.Controllers;

[Route("[controller]")]
[ApiController]
[AllowAnonymous]
public class HealthController : Controller
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        this._healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        HealthReport report = await this._healthCheckService.CheckHealthAsync();
        var result = new
        {
            status = report.Status.ToString(),
            errors = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description?.ToString() ?? e.Value.Status.ToString() })
        };
        return report.Status == HealthStatus.Healthy ? Ok(result) : StatusCode((int)HttpStatusCode.ServiceUnavailable, result);
    }
}