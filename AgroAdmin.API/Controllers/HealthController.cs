using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Health;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(IHealthService healthService, ILogger<HealthController> logger)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthCheckDto>> GetHealth()
    {
        try
        {
            var health = await healthService.CheckAllAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting health status");
            return StatusCode(500, new { error = "Health check failed" });
        }
    }
}