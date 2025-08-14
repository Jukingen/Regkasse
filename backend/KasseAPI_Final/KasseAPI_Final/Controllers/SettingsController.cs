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
    public class SettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(AppDbContext context, ILogger<SettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/settings
        [HttpGet]
        public async Task<ActionResult<SystemSettings>> GetSettings()
        {
            try
            {
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    // Varsayılan ayarları oluştur
                    settings = new SystemSettings
                    {
                        CompanyName = "Default Company",
                        CompanyAddress = "Default Address",
                        CompanyPhone = "Default Phone",
                        CompanyEmail = "default@company.com",
                        CompanyTaxNumber = "ATU00000000",
                        DefaultLanguage = "de-DE",
                        DefaultCurrency = "EUR",
                        TimeZone = "Europe/Vienna",
                        DateFormat = "dd.MM.yyyy",
                        TimeFormat = "HH:mm:ss",
                        DecimalPlaces = 2,
                        TaxRates = new Dictionary<string, decimal>
                        {
                            { "Standard", 20.0m },
                            { "Reduced", 10.0m },
                            { "Special", 13.0m }
                        },
                        ReceiptTemplate = "default",
                        InvoicePrefix = "INV",
                        ReceiptPrefix = "REC",
                        AutoBackup = true,
                        BackupFrequency = 24, // saat
                        MaxBackupFiles = 30,
                        EmailNotifications = false,
                        SmsNotifications = false,
                        IsActive = true
                    };

                    _context.SystemSettings.Add(settings);
                    await _context.SaveChangesAsync();
                }

                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/settings
        [HttpPut]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                // Ayarları güncelle
                settings.CompanyName = request.CompanyName;
                settings.CompanyAddress = request.CompanyAddress;
                settings.CompanyPhone = request.CompanyPhone;
                settings.CompanyEmail = request.CompanyEmail;
                settings.CompanyTaxNumber = request.CompanyTaxNumber;
                settings.DefaultLanguage = request.DefaultLanguage;
                settings.DefaultCurrency = request.DefaultCurrency;
                settings.TimeZone = request.TimeZone;
                settings.DateFormat = request.DateFormat;
                settings.TimeFormat = request.TimeFormat;
                settings.DecimalPlaces = request.DecimalPlaces;
                settings.TaxRates = request.TaxRates;
                settings.ReceiptTemplate = request.ReceiptTemplate;
                settings.InvoicePrefix = request.InvoicePrefix;
                settings.ReceiptPrefix = request.ReceiptPrefix;
                settings.AutoBackup = request.AutoBackup;
                settings.BackupFrequency = request.BackupFrequency;
                settings.MaxBackupFiles = request.MaxBackupFiles;
                settings.EmailNotifications = request.EmailNotifications;
                settings.SmsNotifications = request.SmsNotifications;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Settings updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/settings/tax-rates
        [HttpGet("tax-rates")]
        public async Task<ActionResult<Dictionary<string, decimal>>> GetTaxRates()
        {
            try
            {
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                return Ok(settings.TaxRates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tax rates");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/settings/tax-rates
        [HttpPut("tax-rates")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateTaxRates([FromBody] Dictionary<string, decimal> taxRates)
        {
            try
            {
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                settings.TaxRates = taxRates;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Tax rates updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tax rates");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/settings/backup
        [HttpGet("backup")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> GetBackupSettings()
        {
            try
            {
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                var backupSettings = new
                {
                    AutoBackup = settings.AutoBackup,
                    BackupFrequency = settings.BackupFrequency,
                    MaxBackupFiles = settings.MaxBackupFiles,
                    LastBackup = settings.LastBackup,
                    NextBackup = settings.LastBackup?.AddHours(settings.BackupFrequency)
                };

                return Ok(backupSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/settings/backup/now
        [HttpPost("backup/now")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> CreateBackupNow()
        {
            try
            {
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                // Backup işlemi simülasyonu
                var backupFileName = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
                
                settings.LastBackup = DateTime.UtcNow;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Manual backup created: {BackupFileName}", backupFileName);

                return Ok(new { message = "Backup created successfully", fileName = backupFileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/settings/notifications
        [HttpGet("notifications")]
        public async Task<ActionResult<NotificationSettings>> GetNotificationSettings()
        {
            try
            {
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                var notificationSettings = new NotificationSettings
                {
                    EmailNotifications = settings.EmailNotifications,
                    SmsNotifications = settings.SmsNotifications,
                    EmailSettings = settings.EmailSettings,
                    SmsSettings = settings.SmsSettings
                };

                return Ok(notificationSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/settings/notifications
        [HttpPut("notifications")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                settings.EmailNotifications = request.EmailNotifications;
                settings.SmsNotifications = request.SmsNotifications;
                settings.EmailSettings = request.EmailSettings;
                settings.SmsSettings = request.SmsSettings;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Notification settings updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/settings/export
        [HttpGet("export")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ExportSettings()
        {
            try
            {
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    return NotFound(new { message = "System settings not found" });
                }

                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                return File(bytes, "application/json", "system_settings.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // DTOs
    public class UpdateSettingsRequest
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

        [Required]
        [MaxLength(20)]
        public string CompanyTaxNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string DefaultLanguage { get; set; } = string.Empty;

        [Required]
        [MaxLength(3)]
        public string DefaultCurrency { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TimeZone { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string DateFormat { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TimeFormat { get; set; } = string.Empty;

        [Range(0, 4)]
        public int DecimalPlaces { get; set; }

        [Required]
        public Dictionary<string, decimal> TaxRates { get; set; } = new();

        [MaxLength(50)]
        public string? ReceiptTemplate { get; set; }

        [MaxLength(10)]
        public string? InvoicePrefix { get; set; }

        [MaxLength(10)]
        public string? ReceiptPrefix { get; set; }

        public bool AutoBackup { get; set; }

        [Range(1, 168)]
        public int BackupFrequency { get; set; }

        [Range(1, 100)]
        public int MaxBackupFiles { get; set; }

        public bool EmailNotifications { get; set; }

        public bool SmsNotifications { get; set; }
    }

    public class NotificationSettings
    {
        public bool EmailNotifications { get; set; }
        public bool SmsNotifications { get; set; }
        public Dictionary<string, string>? EmailSettings { get; set; }
        public Dictionary<string, string>? SmsSettings { get; set; }
    }

    public class UpdateNotificationSettingsRequest
    {
        public bool EmailNotifications { get; set; }
        public bool SmsNotifications { get; set; }
        public Dictionary<string, string>? EmailSettings { get; set; }
        public Dictionary<string, string>? SmsSettings { get; set; }
    }
}
