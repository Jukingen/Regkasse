using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LocalizationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LocalizationController> _logger;

        public LocalizationController(AppDbContext context, ILogger<LocalizationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/localization
        [HttpGet]
        public async Task<ActionResult<LocalizationSettings>> GetLocalizationSettings()
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    // Varsayılan lokalizasyon ayarlarını oluştur
                    settings = new Models.LocalizationSettings
                    {
                        DefaultLanguage = "de-DE",
                        SupportedLanguages = new List<string> { "de-DE", "en", "tr" },
                        DefaultCurrency = "EUR",
                        SupportedCurrencies = new List<string> { "EUR", "USD", "TRY" },
                        DefaultTimeZone = "Europe/Vienna",
                        SupportedTimeZones = new List<string> 
                        { 
                            "Europe/Vienna", 
                            "Europe/Berlin", 
                            "Europe/Istanbul", 
                            "America/New_York" 
                        },
                        DefaultDateFormat = "dd.MM.yyyy",
                        DefaultTimeFormat = "HH:mm:ss",
                        DefaultDecimalPlaces = 2,
                        NumberFormat = "German",
                        DateFormatOptions = new Dictionary<string, string>
                        {
                            { "de-DE", "dd.MM.yyyy" },
                            { "en", "MM/dd/yyyy" },
                            { "tr", "dd.MM.yyyy" }
                        },
                        TimeFormatOptions = new Dictionary<string, string>
                        {
                            { "de-DE", "HH:mm:ss" },
                            { "en", "h:mm:ss tt" },
                            { "tr", "dd.MM.yyyy" }
                        },
                        CurrencySymbols = new Dictionary<string, string>
                        {
                            { "EUR", "€" },
                            { "USD", "$" },
                            { "TRY", "₺" }
                        }
                    };

                    _context.LocalizationSettings.Add(settings);
                    await _context.SaveChangesAsync();
                }

                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localization settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/localization
        [HttpPut]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateLocalizationSettings([FromBody] UpdateLocalizationSettingsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                // Lokalizasyon ayarlarını güncelle
                settings.DefaultLanguage = request.DefaultLanguage;
                settings.SupportedLanguages = request.SupportedLanguages;
                settings.DefaultCurrency = request.DefaultCurrency;
                settings.SupportedCurrencies = request.SupportedCurrencies;
                settings.DefaultTimeZone = request.DefaultTimeZone;
                settings.SupportedTimeZones = request.SupportedTimeZones;
                settings.DefaultDateFormat = request.DefaultDateFormat;
                settings.DefaultTimeFormat = request.DefaultTimeFormat;
                settings.DefaultDecimalPlaces = request.DefaultDecimalPlaces;
                settings.NumberFormat = request.NumberFormat;
                settings.DateFormatOptions = request.DateFormatOptions;
                settings.TimeFormatOptions = request.TimeFormatOptions;
                settings.CurrencySymbols = request.CurrencySymbols;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Localization settings updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating localization settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/localization/languages
        [HttpGet("languages")]
        public async Task<ActionResult<List<string>>> GetSupportedLanguages()
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                return Ok(settings.SupportedLanguages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported languages");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/localization/currencies
        [HttpGet("currencies")]
        public async Task<ActionResult<List<string>>> GetSupportedCurrencies()
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                return Ok(settings.SupportedCurrencies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported currencies");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/localization/timezones
        [HttpGet("timezones")]
        public async Task<ActionResult<List<string>>> GetSupportedTimeZones()
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                return Ok(settings.SupportedTimeZones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported time zones");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/localization/format/{language}
        [HttpGet("format/{language}")]
        public async Task<ActionResult<LanguageFormat>> GetLanguageFormat(string language)
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                var languageFormat = new LanguageFormat
                {
                    Language = language,
                    DateFormat = settings.DateFormatOptions.ContainsKey(language) 
                        ? settings.DateFormatOptions[language] 
                        : settings.DefaultDateFormat,
                    TimeFormat = settings.TimeFormatOptions.ContainsKey(language) 
                        ? settings.TimeFormatOptions[language] 
                        : settings.DefaultTimeFormat,
                    DecimalPlaces = settings.DefaultDecimalPlaces,
                    NumberFormat = settings.NumberFormat
                };

                return Ok(languageFormat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting language format for {Language}", language);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/localization/currency/{currency}
        [HttpGet("currency/{currency}")]
        public async Task<ActionResult<CurrencyInfo>> GetCurrencyInfo(string currency)
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                var currencyInfo = new CurrencyInfo
                {
                    Code = currency,
                    Symbol = settings.CurrencySymbols.ContainsKey(currency) 
                        ? settings.CurrencySymbols[currency] 
                        : currency,
                    DecimalPlaces = settings.DefaultDecimalPlaces,
                    IsSupported = settings.SupportedCurrencies.Contains(currency)
                };

                return Ok(currencyInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting currency info for {Currency}", currency);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/localization/add-language
        [HttpPost("add-language")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AddSupportedLanguage([FromBody] AddLanguageRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                if (settings.SupportedLanguages.Contains(request.Language))
                {
                    return BadRequest(new { message = "Language already supported" });
                }

                settings.SupportedLanguages.Add(request.Language);
                settings.DateFormatOptions[request.Language] = request.DateFormat;
                settings.TimeFormatOptions[request.Language] = request.TimeFormat;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Language added successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding supported language");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/localization/add-currency
        [HttpPost("add-currency")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AddSupportedCurrency([FromBody] AddCurrencyRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                if (settings.SupportedCurrencies.Contains(request.Currency))
                {
                    return BadRequest(new { message = "Currency already supported" });
                }

                settings.SupportedCurrencies.Add(request.Currency);
                settings.CurrencySymbols[request.Currency] = request.Symbol;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Currency added successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding supported currency");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/localization/remove-language/{language}
        [HttpDelete("remove-language/{language}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> RemoveSupportedLanguage(string language)
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                if (language == settings.DefaultLanguage)
                {
                    return BadRequest(new { message = "Cannot remove default language" });
                }

                if (!settings.SupportedLanguages.Contains(language))
                {
                    return BadRequest(new { message = "Language not supported" });
                }

                settings.SupportedLanguages.Remove(language);
                settings.DateFormatOptions.Remove(language);
                settings.TimeFormatOptions.Remove(language);
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Language removed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing supported language {Language}", language);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/localization/remove-currency/{currency}
        [HttpDelete("remove-currency/{currency}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> RemoveSupportedCurrency(string currency)
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                if (currency == settings.DefaultCurrency)
                {
                    return BadRequest(new { message = "Cannot remove default currency" });
                }

                if (!settings.SupportedCurrencies.Contains(currency))
                {
                    return BadRequest(new { message = "Currency not supported" });
                }

                settings.SupportedCurrencies.Remove(currency);
                settings.CurrencySymbols.Remove(currency);
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Currency removed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing supported currency {Currency}", currency);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/localization/export
        [HttpGet("export")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ExportLocalizationSettings()
        {
            try
            {
                var settings = await _context.LocalizationSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Localization settings not found" });
                }

                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                return File(bytes, "application/json", "localization_settings.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting localization settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // DTOs
    public class UpdateLocalizationSettingsRequest
    {
        [Required]
        [MaxLength(10)]
        public string DefaultLanguage { get; set; } = string.Empty;

        [Required]
        public List<string> SupportedLanguages { get; set; } = new();

        [Required]
        [MaxLength(3)]
        public string DefaultCurrency { get; set; } = string.Empty;

        [Required]
        public List<string> SupportedCurrencies { get; set; } = new();

        [Required]
        [MaxLength(50)]
        public string DefaultTimeZone { get; set; } = string.Empty;

        [Required]
        public List<string> SupportedTimeZones { get; set; } = new();

        [Required]
        [MaxLength(20)]
        public string DefaultDateFormat { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string DefaultTimeFormat { get; set; } = string.Empty;

        [Range(0, 4)]
        public int DefaultDecimalPlaces { get; set; }

        [Required]
        [MaxLength(50)]
        public string NumberFormat { get; set; } = string.Empty;

        [Required]
        public Dictionary<string, string> DateFormatOptions { get; set; } = new();

        [Required]
        public Dictionary<string, string> TimeFormatOptions { get; set; } = new();

        [Required]
        public Dictionary<string, string> CurrencySymbols { get; set; } = new();
    }

    public class LanguageFormat
    {
        public string Language { get; set; } = string.Empty;
        public string DateFormat { get; set; } = string.Empty;
        public string TimeFormat { get; set; } = string.Empty;
        public int DecimalPlaces { get; set; }
        public string NumberFormat { get; set; } = string.Empty;
    }

    public class CurrencyInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public int DecimalPlaces { get; set; }
        public bool IsSupported { get; set; }
    }

    public class AddLanguageRequest
    {
        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string DateFormat { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TimeFormat { get; set; } = string.Empty;
    }

    public class AddCurrencyRequest
    {
        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Symbol { get; set; } = string.Empty;
    }
}
