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
    public class CompanySettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CompanySettingsController> _logger;

        public CompanySettingsController(AppDbContext context, ILogger<CompanySettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/companysettings
        [HttpGet]
        public async Task<ActionResult<CompanySettings>> GetCompanySettings()
        {
            try
            {
                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    // Varsayılan şirket ayarlarını oluştur
                    settings = new CompanySettings
                    {
                        CompanyName = "Default Company",
                        CompanyAddress = "Default Address",
                        CompanyPhone = "Default Phone",
                        CompanyEmail = "default@company.com",
                        CompanyWebsite = "www.defaultcompany.com",
                        CompanyTaxNumber = "ATU00000000",
                        CompanyRegistrationNumber = "FN000000",
                        CompanyVatNumber = "ATU00000000",
                        CompanyLogo = "default-logo.png",
                        CompanyDescription = "Default company description",
                        BusinessHours = new Dictionary<string, string>
                        {
                            { "Monday", "09:00-18:00" },
                            { "Tuesday", "09:00-18:00" },
                            { "Wednesday", "09:00-18:00" },
                            { "Thursday", "09:00-18:00" },
                            { "Friday", "09:00-18:00" },
                            { "Saturday", "10:00-16:00" },
                            { "Sunday", "Closed" }
                        },
                        ContactPerson = "Default Contact",
                        ContactPhone = "Default Phone",
                        ContactEmail = "contact@defaultcompany.com",
                        BankName = "Default Bank",
                        BankAccountNumber = "0000000000",
                        BankRoutingNumber = "000000000",
                        BankSwiftCode = "DEFAULT",
                        PaymentTerms = "Net 30",
                        Currency = "EUR",
                        Language = "de-DE",
                        TimeZone = "Europe/Vienna",
                        DateFormat = "dd.MM.yyyy",
                        TimeFormat = "HH:mm:ss",
                        DecimalPlaces = 2,
                        TaxCalculationMethod = "Standard",
                        InvoiceNumbering = "Sequential",
                        ReceiptNumbering = "Sequential",
                        DefaultPaymentMethod = "Cash",
                        IsActive = true
                    };

                    _context.CompanySettings.Add(settings);
                    await _context.SaveChangesAsync();
                }

                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/companysettings
        [HttpPut]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateCompanySettings([FromBody] UpdateCompanySettingsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                // Şirket bilgilerini güncelle
                settings.CompanyName = request.CompanyName;
                settings.CompanyAddress = request.CompanyAddress;
                settings.CompanyPhone = request.CompanyPhone;
                settings.CompanyEmail = request.CompanyEmail;
                settings.CompanyWebsite = request.CompanyWebsite;
                settings.CompanyTaxNumber = request.CompanyTaxNumber;
                settings.CompanyRegistrationNumber = request.CompanyRegistrationNumber;
                settings.CompanyVatNumber = request.CompanyVatNumber;
                settings.CompanyLogo = request.CompanyLogo;
                settings.CompanyDescription = request.CompanyDescription;
                settings.BusinessHours = request.BusinessHours;
                settings.ContactPerson = request.ContactPerson;
                settings.ContactPhone = request.ContactPhone;
                settings.ContactEmail = request.ContactEmail;
                settings.BankName = request.BankName;
                settings.BankAccountNumber = request.BankAccountNumber;
                settings.BankRoutingNumber = request.BankRoutingNumber;
                settings.BankSwiftCode = request.BankSwiftCode;
                settings.PaymentTerms = request.PaymentTerms;
                settings.Currency = request.DefaultCurrency;
                settings.Language = request.DefaultLanguage;
                settings.TimeZone = request.DefaultTimeZone;
                settings.DateFormat = request.DefaultDateFormat;
                settings.TimeFormat = request.DefaultTimeFormat;
                settings.DecimalPlaces = request.DefaultDecimalPlaces;
                settings.TaxCalculationMethod = request.TaxCalculationMethod;
                settings.InvoiceNumbering = request.InvoiceNumbering;
                settings.ReceiptNumbering = request.ReceiptNumbering;
                settings.DefaultPaymentMethod = request.DefaultPaymentMethod;
                if (request.FinanzOnlineUsername != null) settings.FinanzOnlineUsername = request.FinanzOnlineUsername;
                if (request.FinanzOnlinePassword != null) settings.FinanzOnlinePassword = request.FinanzOnlinePassword;
                if (request.FinanzOnlineApiUrl != null) settings.FinanzOnlineApiUrl = request.FinanzOnlineApiUrl;
                if (request.FinanzOnlineEnabled.HasValue) settings.FinanzOnlineEnabled = request.FinanzOnlineEnabled.Value;
                if (request.FinanzOnlineAutoSubmit.HasValue) settings.FinanzOnlineAutoSubmit = request.FinanzOnlineAutoSubmit.Value;
                if (request.FinanzOnlineSubmitInterval.HasValue) settings.FinanzOnlineSubmitInterval = request.FinanzOnlineSubmitInterval.Value;
                if (request.DefaultTseDeviceId != null) settings.DefaultTseDeviceId = request.DefaultTseDeviceId;
                if (request.TseAutoConnect.HasValue) settings.TseAutoConnect = request.TseAutoConnect.Value;
                if (request.TseConnectionTimeout.HasValue) settings.TseConnectionTimeout = request.TseConnectionTimeout.Value;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Company settings updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/companysettings/business-hours
        [HttpGet("business-hours")]
        public async Task<ActionResult<Dictionary<string, string>>> GetBusinessHours()
        {
            try
            {
                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                return Ok(settings.BusinessHours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting business hours");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/companysettings/business-hours
        [HttpPut("business-hours")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateBusinessHours([FromBody] Dictionary<string, string> businessHours)
        {
            try
            {
                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                settings.BusinessHours = businessHours;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Business hours updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating business hours");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/companysettings/banking
        [HttpGet("banking")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<BankingInfo>> GetBankingInfo()
        {
            try
            {
                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                var bankingInfo = new BankingInfo
                {
                    BankName = settings.BankName,
                    BankAccountNumber = settings.BankAccountNumber,
                    BankRoutingNumber = settings.BankRoutingNumber,
                    BankSwiftCode = settings.BankSwiftCode
                };

                return Ok(bankingInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting banking info");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/companysettings/banking
        [HttpPut("banking")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateBankingInfo([FromBody] UpdateBankingInfoRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                settings.BankName = request.BankName;
                settings.BankAccountNumber = request.BankAccountNumber;
                settings.BankRoutingNumber = request.BankRoutingNumber;
                settings.BankSwiftCode = request.BankSwiftCode;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Banking info updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating banking info");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/companysettings/localization
        [HttpGet("localization")]
        public async Task<ActionResult<LocalizationSettings>> GetLocalizationSettings()
        {
            try
            {
                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                var localizationSettings = new LocalizationSettings
                {
                    DefaultLanguage = settings.Language,
                    SupportedLanguages = new List<string> { settings.Language },
                    DefaultCurrency = settings.Currency,
                    SupportedCurrencies = new List<string> { settings.Currency },
                    DefaultTimeZone = settings.TimeZone,
                    SupportedTimeZones = new List<string> { settings.TimeZone },
                    DefaultDateFormat = settings.DateFormat,
                    DefaultTimeFormat = settings.TimeFormat,
                    DefaultDecimalPlaces = settings.DecimalPlaces,
                    NumberFormat = "Standard",
                    DateFormatOptions = new Dictionary<string, string> { { settings.Language, settings.DateFormat } },
                    TimeFormatOptions = new Dictionary<string, string> { { settings.Language, settings.TimeFormat } },
                    CurrencySymbols = new Dictionary<string, string> { { settings.Currency, GetCurrencySymbol(settings.Currency) } }
                };

                return Ok(localizationSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localization settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/companysettings/localization
        [HttpPut("localization")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateLocalizationSettings([FromBody] LocalizationSettings request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                settings.Currency = request.DefaultCurrency;
                settings.Language = request.DefaultLanguage;
                settings.TimeZone = request.DefaultTimeZone;
                settings.DateFormat = request.DefaultDateFormat;
                settings.TimeFormat = request.DefaultTimeFormat;
                settings.DecimalPlaces = request.DefaultDecimalPlaces;
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

        // GET: api/companysettings/billing
        [HttpGet("billing")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<BillingSettings>> GetBillingSettings()
        {
            try
            {
                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                var billingSettings = new BillingSettings
                {
                    TaxCalculationMethod = settings.TaxCalculationMethod,
                    InvoiceNumbering = settings.InvoiceNumbering,
                    ReceiptNumbering = settings.ReceiptNumbering,
                    DefaultPaymentMethod = settings.DefaultPaymentMethod,
                    PaymentTerms = settings.PaymentTerms
                };

                return Ok(billingSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting billing settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/companysettings/billing
        [HttpPut("billing")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateBillingSettings([FromBody] UpdateBillingSettingsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                settings.TaxCalculationMethod = request.TaxCalculationMethod;
                settings.InvoiceNumbering = request.InvoiceNumbering;
                settings.ReceiptNumbering = request.ReceiptNumbering;
                settings.DefaultPaymentMethod = request.DefaultPaymentMethod;
                settings.PaymentTerms = request.PaymentTerms;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Billing settings updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating billing settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/companysettings/export
        [HttpGet("export")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ExportCompanySettings()
        {
            try
            {
                var settings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "Company settings not found" });
                }

                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                return File(bytes, "application/json", "company_settings.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting company settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private string GetCurrencySymbol(string currency)
        {
            return currency switch
            {
                "EUR" => "€",
                "USD" => "$",
                "TRY" => "₺",
                _ => currency
            };
        }
    }

    // DTOs
    public class UpdateCompanySettingsRequest
    {
        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CompanyAddress { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CompanyPhone { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? CompanyEmail { get; set; }

        [MaxLength(100)]
        public string? CompanyWebsite { get; set; }

        [Required]
        [MaxLength(20)]
        public string CompanyTaxNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CompanyRegistrationNumber { get; set; }

        [MaxLength(20)]
        public string? CompanyVatNumber { get; set; }

        [MaxLength(100)]
        public string? CompanyLogo { get; set; }

        [MaxLength(500)]
        public string? CompanyDescription { get; set; }

        [Required]
        public Dictionary<string, string> BusinessHours { get; set; } = new();

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? ContactEmail { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(50)]
        public string? BankRoutingNumber { get; set; }

        [MaxLength(20)]
        public string? BankSwiftCode { get; set; }

        [MaxLength(50)]
        public string? PaymentTerms { get; set; }

        [Required]
        [MaxLength(3)]
        public string DefaultCurrency { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string DefaultLanguage { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DefaultTimeZone { get; set; } = string.Empty;

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
        public string TaxCalculationMethod { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string InvoiceNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ReceiptNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DefaultPaymentMethod { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? FinanzOnlineApiUrl { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlineUsername { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlinePassword { get; set; }

        public bool? FinanzOnlineEnabled { get; set; }

        public bool? FinanzOnlineAutoSubmit { get; set; }

        public int? FinanzOnlineSubmitInterval { get; set; }

        [MaxLength(100)]
        public string? DefaultTseDeviceId { get; set; }

        public bool? TseAutoConnect { get; set; }

        public int? TseConnectionTimeout { get; set; }
    }

    public class BankingInfo
    {
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankRoutingNumber { get; set; }
        public string? BankSwiftCode { get; set; }
    }

    public class UpdateBankingInfoRequest
    {
        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(50)]
        public string? BankRoutingNumber { get; set; }

        [MaxLength(20)]
        public string? BankSwiftCode { get; set; }
    }

    public class LocalizationSettings
    {
        public string DefaultLanguage { get; set; } = string.Empty;
        public List<string> SupportedLanguages { get; set; } = new();
        public string DefaultCurrency { get; set; } = string.Empty;
        public List<string> SupportedCurrencies { get; set; } = new();
        public string DefaultTimeZone { get; set; } = string.Empty;
        public List<string> SupportedTimeZones { get; set; } = new();
        public string DefaultDateFormat { get; set; } = string.Empty;
        public string DefaultTimeFormat { get; set; } = string.Empty;
        public int DefaultDecimalPlaces { get; set; }
        public string NumberFormat { get; set; } = string.Empty;
        public Dictionary<string, string> DateFormatOptions { get; set; } = new();
        public Dictionary<string, string> TimeFormatOptions { get; set; } = new();
        public Dictionary<string, string> CurrencySymbols { get; set; } = new();
    }



    public class BillingSettings
    {
        public string TaxCalculationMethod { get; set; } = string.Empty;
        public string InvoiceNumbering { get; set; } = string.Empty;
        public string ReceiptNumbering { get; set; } = string.Empty;
        public string DefaultPaymentMethod { get; set; } = string.Empty;
        public string? PaymentTerms { get; set; }
    }

    public class UpdateBillingSettingsRequest
    {
        [Required]
        [MaxLength(50)]
        public string TaxCalculationMethod { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string InvoiceNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ReceiptNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DefaultPaymentMethod { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PaymentTerms { get; set; }
    }
}
