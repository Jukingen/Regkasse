using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ILogger<SettingsController> _logger;
        private readonly IUserSettingsService _userSettingsService;

        public SettingsController(
            ILogger<SettingsController> logger,
            IUserSettingsService userSettingsService)
        {
            _logger = logger;
            _userSettingsService = userSettingsService;
        }

        /// <summary>
        /// Kullanıcı ayarlarını getir
        /// </summary>
        [HttpGet("user")]
        public async Task<ActionResult<UserSettings>> GetUserSettings()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var settings = await _userSettingsService.GetUserSettingsAsync(userId);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user settings");
                return BadRequest(new { error = "Failed to get user settings", details = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı ayarlarını güncelle
        /// </summary>
        [HttpPut("user")]
        public async Task<ActionResult<UserSettings>> UpdateUserSettings([FromBody] UpdateSettingsRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var updatedSettings = await _userSettingsService.UpdateUserSettingsAsync(userId, request);
                return Ok(updatedSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user settings");
                return BadRequest(new { error = "Failed to update user settings", details = ex.Message });
            }
        }

        /// <summary>
        /// Kullanıcı ayarlarını sıfırla
        /// </summary>
        [HttpPost("user/reset")]
        public async Task<ActionResult<UserSettings>> ResetUserSettings()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var resetSettings = await _userSettingsService.ResetUserSettingsAsync(userId);
                return Ok(resetSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset user settings");
                return BadRequest(new { error = "Failed to reset user settings", details = ex.Message });
            }
        }
    }

    public class UpdateSettingsRequest
    {
        public string? Language { get; set; }
        public string? Theme { get; set; }
        public string? Currency { get; set; }
        public string? TimeZone { get; set; }
        public bool? NotificationsEnabled { get; set; }
        public string? ReceiptTemplate { get; set; }
        public string? InvoiceTemplate { get; set; }
        public Dictionary<string, object>? CustomSettings { get; set; }
    }
} 