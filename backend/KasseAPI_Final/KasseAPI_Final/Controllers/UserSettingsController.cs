using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/user/settings")]
    public class UserSettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserSettingsController> _logger;

        public UserSettingsController(AppDbContext context, ILogger<UserSettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/user/settings
        [HttpGet]
        public async Task<ActionResult<UserSettings>> GetUserSettings()
        {
            try
            {
                // Debug: Tüm claims'leri logla
                _logger.LogInformation("User claims: {Claims}", string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}")));
                
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Extracted userId from NameIdentifier: {UserId}", userId);
                
                // Alternatif olarak user_id custom claim'den de dene
                if (string.IsNullOrEmpty(userId))
                {
                    userId = User.FindFirst("user_id")?.Value;
                    _logger.LogInformation("Extracted userId from user_id claim: {UserId}", userId);
                }
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("No user ID found in claims");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                _logger.LogInformation("Getting user settings for user: {UserId}", userId);

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    // Varsayılan ayarları oluştur
                    userSettings = new UserSettings
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        
                        // Dil ve lokalizasyon ayarları (Avusturya için)
                        Language = "de-DE",
                        Currency = "EUR",
                        DateFormat = "DD.MM.YYYY",
                        TimeFormat = "24h",
                        
                        // Kasa konfigürasyonu
                        DefaultTaxRate = 20, // Avusturya standart vergi oranı
                        EnableDiscounts = true,
                        EnableCoupons = true,
                        AutoPrintReceipts = false,
                        ReceiptHeader = "Registrierkasse - Kassenbeleg",
                        ReceiptFooter = "Vielen Dank für Ihren Einkauf!",
                        
                        // TSE ayarları
                        FinanzOnlineEnabled = false,
                        
                        // Güvenlik ayarları
                        SessionTimeout = 30, // 30 dakika
                        RequirePinForRefunds = true,
                        MaxDiscountPercentage = 50,
                        
                        // Görünüm ayarları
                        Theme = "light",
                        CompactMode = false,
                        ShowProductImages = true,
                        
                        // Bildirim ayarları
                        EnableNotifications = true,
                        LowStockAlert = true,
                        
                        // Varsayılan değerler
                        DefaultPaymentMethod = "mixed",
                        DefaultTableNumber = "1",
                        DefaultWaiterName = "Kasiyer",
                        
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.UserSettings.Add(userSettings);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Created default user settings for user: {UserId}", userId);
                }

                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/user/settings
        [HttpPut]
        public async Task<ActionResult<UserSettings>> UpdateUserSettings([FromBody] UpdateUserSettingsRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    return NotFound(new { message = "User settings not found" });
                }

                // Ayarları güncelle
                if (request.Language != null) userSettings.Language = request.Language;
                if (request.Currency != null) userSettings.Currency = request.Currency;
                if (request.DateFormat != null) userSettings.DateFormat = request.DateFormat;
                if (request.TimeFormat != null) userSettings.TimeFormat = request.TimeFormat;
                if (request.DefaultTaxRate.HasValue) userSettings.DefaultTaxRate = request.DefaultTaxRate.Value;
                if (request.EnableDiscounts.HasValue) userSettings.EnableDiscounts = request.EnableDiscounts.Value;
                if (request.EnableCoupons.HasValue) userSettings.EnableCoupons = request.EnableCoupons.Value;
                if (request.AutoPrintReceipts.HasValue) userSettings.AutoPrintReceipts = request.AutoPrintReceipts.Value;
                if (request.ReceiptHeader != null) userSettings.ReceiptHeader = request.ReceiptHeader;
                if (request.ReceiptFooter != null) userSettings.ReceiptFooter = request.ReceiptFooter;
                if (request.FinanzOnlineEnabled.HasValue) userSettings.FinanzOnlineEnabled = request.FinanzOnlineEnabled.Value;
                if (request.SessionTimeout.HasValue) userSettings.SessionTimeout = request.SessionTimeout.Value;
                if (request.RequirePinForRefunds.HasValue) userSettings.RequirePinForRefunds = request.RequirePinForRefunds.Value;
                if (request.MaxDiscountPercentage.HasValue) userSettings.MaxDiscountPercentage = request.MaxDiscountPercentage.Value;
                if (request.Theme != null) userSettings.Theme = request.Theme;
                if (request.CompactMode.HasValue) userSettings.CompactMode = request.CompactMode.Value;
                if (request.ShowProductImages.HasValue) userSettings.ShowProductImages = request.ShowProductImages.Value;
                if (request.EnableNotifications.HasValue) userSettings.EnableNotifications = request.EnableNotifications.Value;
                if (request.LowStockAlert.HasValue) userSettings.LowStockAlert = request.LowStockAlert.Value;
                if (request.DefaultPaymentMethod != null) userSettings.DefaultPaymentMethod = request.DefaultPaymentMethod;
                if (request.DefaultTableNumber != null) userSettings.DefaultTableNumber = request.DefaultTableNumber;
                if (request.DefaultWaiterName != null) userSettings.DefaultWaiterName = request.DefaultWaiterName;

                userSettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated user settings for user: {UserId}", userId);
                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/user/settings/language
        [HttpPut("language")]
        public async Task<ActionResult<UserSettings>> UpdateLanguage([FromBody] UpdateLanguageRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    return NotFound(new { message = "User settings not found" });
                }

                userSettings.Language = request.Language;
                userSettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated language for user: {UserId} to {Language}", userId, request.Language);
                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user language");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/user/settings/cash-register
        [HttpPut("cash-register")]
        public async Task<ActionResult<UserSettings>> UpdateCashRegisterConfig([FromBody] UpdateCashRegisterConfigRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    return NotFound(new { message = "User settings not found" });
                }

                // Kasa konfigürasyonunu güncelle
                if (request.DefaultTaxRate.HasValue) userSettings.DefaultTaxRate = request.DefaultTaxRate.Value;
                if (request.EnableDiscounts.HasValue) userSettings.EnableDiscounts = request.EnableDiscounts.Value;
                if (request.EnableCoupons.HasValue) userSettings.EnableCoupons = request.EnableCoupons.Value;
                if (request.AutoPrintReceipts.HasValue) userSettings.AutoPrintReceipts = request.AutoPrintReceipts.Value;
                if (request.ReceiptHeader != null) userSettings.ReceiptHeader = request.ReceiptHeader;
                if (request.ReceiptFooter != null) userSettings.ReceiptFooter = request.ReceiptFooter;

                userSettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated cash register config for user: {UserId}", userId);
                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cash register config");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/user/settings/tse
        [HttpPut("tse")]
        public async Task<ActionResult<UserSettings>> UpdateTSESettings([FromBody] UpdateTSESettingsRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    return NotFound(new { message = "User settings not found" });
                }

                // TSE ayarlarını güncelle
                if (request.TseDeviceId != null) userSettings.TseDeviceId = request.TseDeviceId;
                if (request.FinanzOnlineEnabled.HasValue) userSettings.FinanzOnlineEnabled = request.FinanzOnlineEnabled.Value;
                if (request.FinanzOnlineUsername != null) userSettings.FinanzOnlineUsername = request.FinanzOnlineUsername;

                userSettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated TSE settings for user: {UserId}", userId);
                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating TSE settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/user/settings/security
        [HttpPut("security")]
        public async Task<ActionResult<UserSettings>> UpdateSecuritySettings([FromBody] UpdateSecuritySettingsRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    return NotFound(new { message = "User settings not found" });
                }

                // Güvenlik ayarlarını güncelle
                if (request.SessionTimeout.HasValue) userSettings.SessionTimeout = request.SessionTimeout.Value;
                if (request.RequirePinForRefunds.HasValue) userSettings.RequirePinForRefunds = request.RequirePinForRefunds.Value;
                if (request.MaxDiscountPercentage.HasValue) userSettings.MaxDiscountPercentage = request.MaxDiscountPercentage.Value;

                userSettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated security settings for user: {UserId}", userId);
                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating security settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/user/settings/reset
        [HttpPost("reset")]
        public async Task<ActionResult<UserSettings>> ResetUserSettings()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == userId);

                if (userSettings == null)
                {
                    return NotFound(new { message = "User settings not found" });
                }

                // Varsayılan ayarlara sıfırla
                userSettings.Language = "de-DE";
                userSettings.Currency = "EUR";
                userSettings.DateFormat = "DD.MM.YYYY";
                userSettings.TimeFormat = "24h";
                userSettings.DefaultTaxRate = 20;
                userSettings.EnableDiscounts = true;
                userSettings.EnableCoupons = true;
                userSettings.AutoPrintReceipts = false;
                userSettings.ReceiptHeader = "Registrierkasse - Kassenbeleg";
                userSettings.ReceiptFooter = "Vielen Dank für Ihren Einkauf!";
                userSettings.FinanzOnlineEnabled = false;
                userSettings.SessionTimeout = 30;
                userSettings.RequirePinForRefunds = true;
                userSettings.MaxDiscountPercentage = 50;
                userSettings.Theme = "light";
                userSettings.CompactMode = false;
                userSettings.ShowProductImages = true;
                userSettings.EnableNotifications = true;
                userSettings.LowStockAlert = true;
                userSettings.DefaultPaymentMethod = "mixed";
                userSettings.DefaultTableNumber = "1";
                userSettings.DefaultWaiterName = "Kasiyer";

                userSettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Reset user settings for user: {UserId}", userId);
                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // Request Models
    public class UpdateUserSettingsRequest
    {
        public string? Language { get; set; }
        public string? Currency { get; set; }
        public string? DateFormat { get; set; }
        public string? TimeFormat { get; set; }
        public int? DefaultTaxRate { get; set; }
        public bool? EnableDiscounts { get; set; }
        public bool? EnableCoupons { get; set; }
        public bool? AutoPrintReceipts { get; set; }
        public string? ReceiptHeader { get; set; }
        public string? ReceiptFooter { get; set; }
        public bool? FinanzOnlineEnabled { get; set; }
        public int? SessionTimeout { get; set; }
        public bool? RequirePinForRefunds { get; set; }
        public int? MaxDiscountPercentage { get; set; }
        public string? Theme { get; set; }
        public bool? CompactMode { get; set; }
        public bool? ShowProductImages { get; set; }
        public bool? EnableNotifications { get; set; }
        public bool? LowStockAlert { get; set; }
        public string? DefaultPaymentMethod { get; set; }
        public string? DefaultTableNumber { get; set; }
        public string? DefaultWaiterName { get; set; }
    }

    public class UpdateLanguageRequest
    {
        [Required]
        public string Language { get; set; } = string.Empty;
    }

    public class UpdateCashRegisterConfigRequest
    {
        public int? DefaultTaxRate { get; set; }
        public bool? EnableDiscounts { get; set; }
        public bool? EnableCoupons { get; set; }
        public bool? AutoPrintReceipts { get; set; }
        public string? ReceiptHeader { get; set; }
        public string? ReceiptFooter { get; set; }
    }

    public class UpdateTSESettingsRequest
    {
        public string? TseDeviceId { get; set; }
        public bool? FinanzOnlineEnabled { get; set; }
        public string? FinanzOnlineUsername { get; set; }
    }

    public class UpdateSecuritySettingsRequest
    {
        public int? SessionTimeout { get; set; }
        public bool? RequirePinForRefunds { get; set; }
        public int? MaxDiscountPercentage { get; set; }
    }
}
