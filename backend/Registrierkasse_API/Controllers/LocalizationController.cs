using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Services;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocalizationController : ControllerBase
    {
        private readonly LocalizationService _localizationService;

        public LocalizationController(LocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        [HttpGet("translations")]
        public async Task<IActionResult> GetTranslations([FromQuery] string language = "de-DE")
        {
            try
            {
                var supportedLanguages = _localizationService.GetSupportedLanguages();
                var defaultLanguage = _localizationService.GetDefaultLanguage();
                
                return Ok(new
                {
                    supported_languages = supportedLanguages,
                    default_language = defaultLanguage,
                    current_language = language
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get translations" });
            }
        }

        [HttpGet("translate")]
        public async Task<IActionResult> GetTranslation([FromQuery] string key, [FromQuery] string language = "de-DE")
        {
            try
            {
                var translation = _localizationService.Translate(key, language);
                return Ok(new { key, translation, language });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Translation failed" });
            }
        }

        [HttpPost("language")]
        public async Task<IActionResult> SetLanguage([FromBody] SetLanguageRequest request)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                if (!_localizationService.IsValidLanguage(request.Language))
                {
                    return BadRequest(new { error = "Invalid language" });
                }

                return Ok(new { message = "Language preference updated", language = request.Language });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update language" });
            }
        }
    }

    public class SetLanguageRequest
    {
        public string Language { get; set; } = string.Empty;
    }
} 
