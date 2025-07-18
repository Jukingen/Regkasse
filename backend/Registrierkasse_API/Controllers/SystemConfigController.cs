using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Registrierkasse_API.Data;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemConfigController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SystemConfigController> _logger;

        public SystemConfigController(AppDbContext context, ILogger<SystemConfigController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("config")]
        public async Task<ActionResult<SystemConfigDto>> GetConfiguration()
        {
            try
            {
                var config = await _context.SystemConfigurations.FirstOrDefaultAsync();
                if (config == null)
                {
                    config = new SystemConfiguration();
                    _context.SystemConfigurations.Add(config);
                    await _context.SaveChangesAsync();
                }
                return Ok(new SystemConfigDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("config")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult> UpdateConfiguration([FromBody] SystemConfigDto configDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                var config = await _context.SystemConfigurations.FirstOrDefaultAsync();
                if (config == null)
                {
                    config = new SystemConfiguration();
                    _context.SystemConfigurations.Add(config);
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("System configuration updated");
                return Ok(new { message = "Configuration updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    public class SystemConfigDto
    {
        // Sadece zorunlu online çalışma için sadeleştirildi
    }
} 
