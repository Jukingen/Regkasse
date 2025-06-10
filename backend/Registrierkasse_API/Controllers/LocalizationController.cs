using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Services;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocalizationController : ControllerBase
    {
        private readonly ILocalizationService _localizationService;

        public LocalizationController(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        [HttpGet("translations")]
        public async Task<IActionResult> GetTranslations([FromQuery] string language = "de-DE")
        {
            var translations = await _localizationService.GetAllTranslationsAsync(language);
            return Ok(translations);
        }

        [HttpGet("translate")]
        public async Task<IActionResult> GetTranslation([FromQuery] string key, [FromQuery] string language = "de-DE")
        {
            var translation = await _localizationService.GetTranslationAsync(key, language);
            return Ok(new { key, translation });
        }

        [HttpPost("language")]
        public async Task<IActionResult> SetLanguage([FromBody] SetLanguageRequest request)
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var success = await _localizationService.SetUserLanguageAsync(userId, request.Language);
            if (!success)
            {
                return NotFound("User settings not found");
            }

            return Ok(new { message = "Language updated successfully" });
        }
    }

    public class SetLanguageRequest
    {
        public string Language { get; set; }
    }
} 