using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using KasseAPI_Final.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Cash register <em>inventory</em> and shift operations (admin / back-office). Responses include all rows and statuses where applicable.
    /// </summary>
    /// <remarks>
    /// POS self-assignment and payment picker must use <c>GET /api/pos/cash-register/selectable</c>
    /// (<see cref="ICashRegisterResolutionService.ListSelectableForPosPickerAsync"/>) — not <c>GET /api/CashRegister</c>.
    /// </remarks>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CashRegisterController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CashRegisterController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICashRegisterShiftService _cashRegisterShift;
        private readonly ISettingsTenantResolver _settingsTenantResolver;

        public CashRegisterController(
            ILogger<CashRegisterController> logger,
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ICashRegisterShiftService cashRegisterShift,
            ISettingsTenantResolver settingsTenantResolver)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _cashRegisterShift = cashRegisterShift;
            _settingsTenantResolver = settingsTenantResolver;
        }

        /// <summary>Full register inventory (open and closed). Not filtered for POS assignment.</summary>
        [HasPermission(AppPermissions.CashRegisterView)]
        [HttpGet]
        public async Task<IActionResult> GetCashRegisters()
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(HttpContext?.RequestAborted ?? default);
                var registers = await _context.CashRegisters
                    .Include(cr => cr.CurrentUser)
                    .AsNoTracking()
                    .Where(cr => cr.TenantId == tenantId)
                    .ToListAsync();

                return Ok(new { message = "Kasalar başarıyla getirildi", registers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasalar getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Kasalar getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HasPermission(AppPermissions.CashRegisterView)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCashRegister(Guid id)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(HttpContext?.RequestAborted ?? default);
                var register = await _context.CashRegisters
                    .Include(r => r.CurrentUser)
                    .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

                if (register == null)
                {
                    return NotFound(new { message = "Kasa bulunamadı" });
                }

                return Ok(new { message = "Kasa başarıyla getirildi", register });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost]
        [HasPermission(AppPermissions.CashRegisterManage)]
        public async Task<IActionResult> CreateCashRegister([FromBody] CreateCashRegisterModel model)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(HttpContext?.RequestAborted ?? default);
                var register = new CashRegister
                {
                    TenantId = tenantId,
                    RegisterNumber = await GenerateRegisterNumberAsync(tenantId),
                    Location = model.Location,
                    StartingBalance = model.StartingBalance,
                    CurrentBalance = model.StartingBalance,
                    LastBalanceUpdate = DateTime.UtcNow,
                    Status = RegisterStatus.Closed
                };

                _context.CashRegisters.Add(register);
                // Do not insert TransactionType.Open here: register is Created as Closed. Shift open is logged by CashRegisterShiftService.TryOpenCashRegisterAsync.
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCashRegister), new { id = register.Id }, 
                    new { message = "Kasa başarıyla oluşturuldu", register });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasa oluşturulurken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa oluşturulurken bir hata oluştu", error = ex.Message });
            }
        }

        [HasPermission(AppPermissions.ShiftOpen)]
        [HttpPost("{id}/open")]
        public async Task<IActionResult> OpenCashRegister(Guid id, [FromBody] OpenCashRegisterModel model, CancellationToken cancellationToken)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });
                }

                var result = await _cashRegisterShift.TryOpenCashRegisterAsync(
                    id,
                    userId,
                    model.OpeningBalance,
                    "Kasa açılışı",
                    allowIdempotentSameUser: false,
                    cancellationToken);

                return result.Kind switch
                {
                    CashRegisterOpenKind.SuccessOpened or CashRegisterOpenKind.SuccessIdempotentAlreadyOpen =>
                        Ok(new { message = "Kasa başarıyla açıldı" }),
                    CashRegisterOpenKind.FailedNotFound => NotFound(new { message = "Kasa bulunamadı" }),
                    CashRegisterOpenKind.FailedAlreadyOpenSameUserNotIdempotent =>
                        BadRequest(new { message = "Kasa zaten açık" }),
                    CashRegisterOpenKind.FailedConflictOtherUser =>
                        Conflict(new { message = "Kasa ist von einem anderen Benutzer geöffnet." }),
                    CashRegisterOpenKind.FailedActorAlreadyHasOtherOpenRegister =>
                        Conflict(new
                        {
                            message =
                                "Sie haben bereits eine andere geöffnete Kasse. Bitte schließen Sie diese zuerst, bevor Sie eine weitere öffnen."
                        }),
                    CashRegisterOpenKind.FailedStartbelegRequired =>
                        BadRequest(new
                        {
                            message = "Startbeleg muss erstellt werden.",
                            code = "STARTBELEG_REQUIRED"
                        }),
                    CashRegisterOpenKind.FailedMonatsbelegRequired =>
                        BadRequest(new
                        {
                            message = "Monatsbeleg muss für den aktuellen Monat erstellt werden.",
                            code = "MONATSBELEG_REQUIRED"
                        }),
                    CashRegisterOpenKind.FailedInvalidState =>
                        BadRequest(new { message = "Kasa kann in diesem Zustand nicht geöffnet werden." }),
                    _ => StatusCode(500, new { message = "Kasa açılırken bir hata oluştu" })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa açılırken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa açılırken bir hata oluştu", error = ex.Message });
            }
        }

        [HasPermission(AppPermissions.ShiftClose)]
        [HttpPost("{id}/close")]
        public async Task<IActionResult> CloseCashRegister(Guid id, [FromBody] CloseCashRegisterModel model, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = User.GetActorUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid();
                }

                var result = await _cashRegisterShift.TryCloseCashRegisterAsync(
                    id,
                    userId,
                    model.ClosingBalance,
                    cancellationToken);

                return result.Kind switch
                {
                    CashRegisterCloseKind.Success =>
                        Ok(new { message = "Kasa başarıyla kapatıldı" }),
                    CashRegisterCloseKind.FailedNotFound =>
                        NotFound(new { message = "Kasa bulunamadı" }),
                    CashRegisterCloseKind.FailedAlreadyClosed =>
                        BadRequest(new { message = "Kasa zaten kapalı" }),
                    CashRegisterCloseKind.FailedForbidden =>
                        Forbid(),
                    _ => StatusCode(500, new { message = "Kasa kapatılırken bir hata oluştu" })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa kapatılırken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa kapatılırken bir hata oluştu", error = ex.Message });
            }
        }

        [HasPermission(AppPermissions.CashRegisterView)]
        [HttpGet("{id}/transactions")]
        public async Task<IActionResult> GetCashRegisterTransactions(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(HttpContext?.RequestAborted ?? default);
                var registerOk = await _context.CashRegisters.AsNoTracking()
                    .AnyAsync(r => r.Id == id && r.TenantId == tenantId);
                if (!registerOk)
                    return NotFound(new { message = "Kasa bulunamadı" });

                var query = _context.CashRegisterTransactions
                    .Where(t => t.CashRegisterId == id);

                // Austria calendar-day half-open filter on instants: [lower, upper) in UTC (see PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds).
                var (lowerInclusiveUtc, upperExclusiveUtc) =
                    PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
                if (lowerInclusiveUtc.HasValue)
                    query = query.Where(t => t.TransactionDate >= lowerInclusiveUtc.Value);
                if (upperExclusiveUtc.HasValue)
                    query = query.Where(t => t.TransactionDate < upperExclusiveUtc.Value);

                var transactions = await query
                    .Include(t => t.User)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                return Ok(new { message = "İşlemler başarıyla getirildi", transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasanın işlemleri getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "İşlemler getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        private async Task<string> GenerateRegisterNumberAsync(Guid tenantId)
        {
            var lastRegister = await _context.CashRegisters
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.RegisterNumber)
                .FirstOrDefaultAsync();

            if (lastRegister == null)
            {
                return "K001";
            }

            var lastNumber = int.Parse(lastRegister.RegisterNumber.Substring(1));
            return $"K{(lastNumber + 1):D3}";
        }
    }

    // DTOs
    public class CreateCashRegisterModel
    {
        [Required]
        public string Location { get; set; } = string.Empty;
        
        [Required]
        public decimal StartingBalance { get; set; }
    }

    public class OpenCashRegisterModel
    {
        [Required]
        public decimal OpeningBalance { get; set; }
    }

    public class CloseCashRegisterModel
    {
        [Required]
        public decimal ClosingBalance { get; set; }
    }
}
