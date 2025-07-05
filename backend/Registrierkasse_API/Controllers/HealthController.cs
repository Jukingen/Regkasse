using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult HealthCheck()
        {
            try
            {
                _logger.LogInformation("Health check requested");
                
                return Ok(new 
                { 
                    status = "healthy", 
                    timestamp = DateTime.UtcNow,
                    version = "1.0.0",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
        }

        [HttpHead]
        public ActionResult HealthCheckHead()
        {
            return Ok();
        }
    }
} 