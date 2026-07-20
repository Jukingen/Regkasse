using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Tenant company master data (RKSV §8 header, FinanzOnline, TSE defaults).
    /// Canonical route: <c>/api/company/settings</c>; legacy alias: <c>/api/CompanySettings</c>.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/company/settings")]
    [Route("api/CompanySettings")]
    public class CompanySettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CompanySettingsController> _logger;
        private readonly ICurrentTenantAccessor _tenantAccessor;
        private readonly IAuditLogService _auditLogService;

        public CompanySettingsController(
            AppDbContext context,
            ILogger<CompanySettingsController> logger,
            ICurrentTenantAccessor tenantAccessor,
            IAuditLogService auditLogService)
        {
            _context = context;
            _logger = logger;
            _tenantAccessor = tenantAccessor;
            _auditLogService = auditLogService;
        }

        private string ActorUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        private string ActorRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";

        [HasPermission(AppPermissions.SettingsView)]
        [HttpGet]
        public async Task<ActionResult<CompanySettings>> GetCompanySettings()
        {
            try
            {
                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await _context.CompanySettings
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
                if (settings == null)
                    return Ok(CreateSettingsShell(tenantId));

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
        [HasPermission(AppPermissions.SettingsManage)]
        public async Task<IActionResult> UpdateCompanySettings([FromBody] UpdateCompanySettingsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await _context.CompanySettings
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
                var isCreate = settings == null;
                if (isCreate)
                {
                    settings = CreateSettingsShell(tenantId);
                    _context.CompanySettings.Add(settings);
                }

                var oldRksvSnapshot = isCreate ? null : ToRksvAuditSnapshot(settings);

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
                // TODO: optional – tenant/branch restriction if multi-tenant; FinanzOnline config is typically tenant-wide.
                if (request.FinanzOnlineUsername != null) settings.FinanzOnlineUsername = request.FinanzOnlineUsername;
                if (request.FinanzOnlinePassword != null) settings.FinanzOnlinePassword = request.FinanzOnlinePassword;
                if (request.FinanzOnlineApiUrl != null) settings.FinanzOnlineApiUrl = request.FinanzOnlineApiUrl;
                if (request.FinanzOnlineEnabled.HasValue) settings.FinanzOnlineEnabled = request.FinanzOnlineEnabled.Value;
                if (request.FinanzOnlineAutoSubmit.HasValue) settings.FinanzOnlineAutoSubmit = request.FinanzOnlineAutoSubmit.Value;
                if (request.FinanzOnlineSubmitInterval.HasValue) settings.FinanzOnlineSubmitInterval = request.FinanzOnlineSubmitInterval.Value;
                if (request.FinanzOnlineRetryAttempts.HasValue) settings.FinanzOnlineRetryAttempts = request.FinanzOnlineRetryAttempts.Value;
                if (request.FinanzOnlineEnableValidation.HasValue) settings.FinanzOnlineEnableValidation = request.FinanzOnlineEnableValidation.Value;
                if (request.DefaultTseDeviceId != null) settings.DefaultTseDeviceId = request.DefaultTseDeviceId;
                if (request.TseAutoConnect.HasValue) settings.TseAutoConnect = request.TseAutoConnect.Value;
                if (request.TseConnectionTimeout.HasValue) settings.TseConnectionTimeout = request.TseConnectionTimeout.Value;
                // Defense in depth: never persist under a tenant other than the resolved effective tenant.
                settings.TenantId = tenantId;
                settings.UpdatedAt = DateTime.UtcNow;
                settings.UpdatedBy = ActorUserId;

                await _context.SaveChangesAsync();

                var newRksvSnapshot = ToRksvAuditSnapshot(settings);
                try
                {
                    await _auditLogService.LogEntityChangeAsync(
                        AuditLogActions.COMPANY_SETTINGS_UPDATE,
                        AuditLogEntityTypes.COMPANY_SETTINGS,
                        settings.Id,
                        ActorUserId,
                        ActorRole,
                        oldValues: oldRksvSnapshot,
                        newValues: newRksvSnapshot,
                        description: "Company settings updated (RKSV header fields and tenant profile).");
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "Company settings saved but audit log failed for tenant settings {SettingsId}", settings.Id);
                }

                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HasPermission(AppPermissions.SettingsView)]
        [HttpGet("business-hours")]
        public async Task<ActionResult<Dictionary<string, string>>> GetBusinessHours()
        {
            try
            {
                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsManage)]
        public async Task<IActionResult> UpdateBusinessHours([FromBody] Dictionary<string, string> businessHours)
        {
            try
            {
                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsView)]
        public async Task<ActionResult<BankingInfo>> GetBankingInfo()
        {
            try
            {
                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsManage)]
        public async Task<IActionResult> UpdateBankingInfo([FromBody] UpdateBankingInfoRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsView)]
        [HttpGet("localization")]
        public async Task<ActionResult<LocalizationSettings>> GetLocalizationSettings()
        {
            try
            {
                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsManage)]
        public async Task<IActionResult> UpdateLocalizationSettings([FromBody] LocalizationSettings request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsView)]
        public async Task<ActionResult<BillingSettings>> GetBillingSettings()
        {
            try
            {
                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsManage)]
        public async Task<IActionResult> UpdateBillingSettings([FromBody] UpdateBillingSettingsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        [HasPermission(AppPermissions.SettingsManage)]
        public async Task<IActionResult> ExportCompanySettings()
        {
            try
            {
                if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                    return NotFound();

                var settings = await LoadCompanySettingsForTenantAsync(
                    tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
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

        private static CompanySettings CreateSettingsShell(Guid tenantId) => new()
        {
            TenantId = tenantId,
            CompanyName = string.Empty,
            CompanyAddress = string.Empty,
            CompanyTaxNumber = string.Empty,
            BusinessHours = new Dictionary<string, string>(),
            WorkingHours = WorkingHoursSettings.CreateDefault(),
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
            IsActive = true,
        };

        private static object ToRksvAuditSnapshot(CompanySettings settings) => new
        {
            settings.CompanyName,
            settings.CompanyAddress,
            settings.CompanyTaxNumber,
            settings.CompanyPhone,
            settings.CompanyEmail,
            settings.CompanyWebsite,
        };

        private Task<CompanySettings?> LoadCompanySettingsForTenantAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            _context.CompanySettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

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

        public int? FinanzOnlineRetryAttempts { get; set; }

        public bool? FinanzOnlineEnableValidation { get; set; }

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
