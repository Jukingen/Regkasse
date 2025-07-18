using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Services;
using System;
using System.Threading.Tasks;

namespace Registrierkasse_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TseStatusController : ControllerBase
    {
        private readonly ITseService _tseService;
        private readonly ILogger<TseStatusController> _logger;

        public TseStatusController(ITseService tseService, ILogger<TseStatusController> logger)
        {
            _tseService = tseService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _tseService.GetStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE durum bilgisi alınamadı");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
} 
