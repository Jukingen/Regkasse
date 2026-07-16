using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using KasseAPI_Final.Services;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.AdminCashRegisters;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Localization;
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
        private readonly ICurrentTenantAccessor _tenantAccessor;
        private readonly ICashRegisterManagementService _cashRegisterManagement;
        private readonly ICashRegisterListEnrichmentService _enrichment;
        private readonly IApiMessageLocalizer _messages;

        public CashRegisterController(
            ILogger<CashRegisterController> logger,
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ICashRegisterShiftService cashRegisterShift,
            ISettingsTenantResolver settingsTenantResolver,
            ICurrentTenantAccessor tenantAccessor,
            ICashRegisterManagementService cashRegisterManagement,
            ICashRegisterListEnrichmentService enrichment,
            IApiMessageLocalizer messages)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _cashRegisterShift = cashRegisterShift;
            _settingsTenantResolver = settingsTenantResolver;
            _tenantAccessor = tenantAccessor;
            _cashRegisterManagement = cashRegisterManagement;
            _enrichment = enrichment;
            _messages = messages;
        }

        /// <summary>Tenant register inventory with TSE/offline/sync telemetry (admin FA).</summary>
        [HasPermission(AppPermissions.CashRegisterView)]
        [HttpGet("enhanced")]
        [ProducesResponseType(typeof(IReadOnlyList<EnhancedCashRegisterDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<EnhancedCashRegisterDto>>> GetEnhancedCashRegisters(
            CancellationToken cancellationToken)
        {
            try
            {
                var actorIsSuperAdmin = User.IsInRole(Roles.SuperAdmin);
                var page = await _cashRegisterManagement.ListAsync(
                    tenantIdFilter: null,
                    excludeStatus: null,
                    actorIsSuperAdmin,
                    page: 1,
                    pageSize: 500,
                    cancellationToken).ConfigureAwait(false);

                var enhanced = page.Items.Select(EnhancedCashRegisterDto.From).ToList();
                return Ok(enhanced);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Enhanced cash registers list failed");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegistersFetchError), error = ex.Message });
            }
        }

        /// <summary>Full register inventory (open and closed). Not filtered for POS assignment.</summary>
        [HasPermission(AppPermissions.CashRegisterView)]
        [HttpGet]
        public async Task<IActionResult> GetCashRegisters()
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(HttpContext?.RequestAborted ?? default);
                // Project to DTO — serializing CashRegister + CurrentUser causes circular JSON and truncated responses.
                var registers = await _context.CashRegisters
                    .AsNoTracking()
                    .Where(cr => cr.TenantId == tenantId)
                    .OrderBy(cr => cr.RegisterNumber)
                    .Select(cr => new CashRegisterDto
                    {
                        Id = cr.Id,
                        TenantId = cr.TenantId,
                        RegisterNumber = cr.RegisterNumber,
                        Location = cr.Location,
                        Status = cr.Status,
                        StartingBalance = cr.StartingBalance,
                        CurrentBalance = cr.CurrentBalance,
                        LastBalanceUpdate = cr.LastBalanceUpdate,
                        CurrentUserId = cr.CurrentUserId,
                        IsActive = cr.IsActive,
                        IsDefaultForTenant = cr.IsDefaultForTenant,
                        DecommissionedAtUtc = cr.DecommissionedAtUtc,
                        DecommissionReason = cr.DecommissionReason,
                        CreatedAt = cr.CreatedAt,
                        CreatedBy = cr.CreatedBy,
                        UpdatedAt = cr.UpdatedAt,
                        UpdatedBy = cr.UpdatedBy,
                    })
                    .ToListAsync();

                return Ok(new { message = _messages.Get(ApiMessageKeys.RegistersFetchSuccess), registers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasalar getirilirken bir hata oluştu");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegistersFetchError), error = ex.Message });
            }
        }

        /// <summary>Active registers for the selected mandant; default register is listed first.</summary>
        [HasPermission(AppPermissions.CashRegisterView)]
        [HttpGet("by-tenant")]
        [ProducesResponseType(typeof(IReadOnlyList<CashRegisterDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IReadOnlyList<CashRegisterDto>>> GetCashRegistersByTenant(
            CancellationToken cancellationToken)
        {
            if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
                return BadRequest(new { message = "No tenant selected" });

            try
            {
                var registers = await _context.CashRegisters
                    .AsNoTracking()
                    .Include(r => r.Tenant)
                    .Where(r => r.TenantId == tenantId && r.IsActive)
                    .OrderByDescending(r => r.IsDefaultForTenant)
                    .ThenBy(r => r.RegisterNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var dtos = registers.Select(MapToCashRegisterDto).ToList();
                await _enrichment.ApplyAsync(dtos, registers, cancellationToken).ConfigureAwait(false);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cash registers by tenant list failed for TenantId={TenantId}", tenantId);
                return StatusCode(500, new { message = "Failed to load cash registers", error = ex.Message });
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
                    return NotFound(new { message = _messages.Get(ApiMessageKeys.RegisterNotFound) });
                }

                return Ok(new { message = _messages.Get(ApiMessageKeys.RegisterFetchSuccess), register });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa getirilirken bir hata oluştu");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegisterFetchError), error = ex.Message });
            }
        }

        /// <summary>
        /// Creates a new cash register row for the effective tenant (Closed, no shift transaction).
        /// Requires <see cref="AppPermissions.CashRegisterManage"/> — SuperAdmin and Manager only.
        /// </summary>
        [HttpPost]
        [HasPermission(AppPermissions.CashRegisterManage)]
        public async Task<IActionResult> CreateCashRegister(
            [FromBody] CreateCashRegisterRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                return BadRequest(new { message = "Request body is required." });

            var actorUserId = User.GetActorUserId();
            if (string.IsNullOrEmpty(actorUserId))
                return Unauthorized(new { message = "User not authenticated." });

            var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;

            try
            {
                var register = await _cashRegisterManagement.CreateAsync(
                    request,
                    actorUserId,
                    actorRole,
                    User.IsInRole(Roles.SuperAdmin),
                    cancellationToken).ConfigureAwait(false);

                return CreatedAtAction(nameof(GetCashRegister), new { id = register.Id },
                    new { message = _messages.Get(ApiMessageKeys.RegisterCreateSuccess), register });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Cash register create rejected: missing tenant context");
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("TenantId is only allowed", StringComparison.Ordinal))
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Tenant not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = ex.Message, registerNumber = request.RegisterNumber.Trim() });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasa oluşturulurken bir hata oluştu");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegisterCreateError), error = ex.Message });
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
                    return Unauthorized(new { message = _messages.Get(ApiMessageKeys.UserNotFound) });
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
                        Ok(new { message = _messages.Get(ApiMessageKeys.RegisterOpenSuccess) }),
                    CashRegisterOpenKind.FailedNotFound => NotFound(new { message = _messages.Get(ApiMessageKeys.RegisterNotFound) }),
                    CashRegisterOpenKind.FailedAlreadyOpenSameUserNotIdempotent =>
                        BadRequest(new { message = _messages.Get(ApiMessageKeys.RegisterAlreadyOpen) }),
                    CashRegisterOpenKind.FailedConflictOtherUser =>
                        Conflict(new { message = _messages.Get(ApiMessageKeys.RegisterOpenedByOtherUser) }),
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
                            message = _messages.Get(ApiMessageKeys.MonthlyReceiptRequired),
                            code = "MONATSBELEG_REQUIRED"
                        }),
                    CashRegisterOpenKind.FailedInvalidState =>
                        BadRequest(new { message = _messages.Get(ApiMessageKeys.RegisterCannotOpenInState) }),
                    _ => StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegisterOpenError) })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa açılırken bir hata oluştu");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegisterOpenError), error = ex.Message });
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
                        Ok(new { message = _messages.Get(ApiMessageKeys.RegisterCloseSuccess) }),
                    CashRegisterCloseKind.FailedNotFound =>
                        NotFound(new { message = _messages.Get(ApiMessageKeys.RegisterNotFound) }),
                    CashRegisterCloseKind.FailedAlreadyClosed =>
                        BadRequest(new { message = _messages.Get(ApiMessageKeys.RegisterAlreadyClosed) }),
                    CashRegisterCloseKind.FailedForbidden =>
                        Forbid(),
                    _ => StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegisterCloseError) })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa kapatılırken bir hata oluştu");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegisterCloseError), error = ex.Message });
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
                    return NotFound(new { message = _messages.Get(ApiMessageKeys.RegisterNotFound) });

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

                return Ok(new { message = _messages.Get(ApiMessageKeys.TransactionsFetchSuccess), transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasanın işlemleri getirilirken bir hata oluştu");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.TransactionsFetchError), error = ex.Message });
            }
        }

        private static CashRegisterDto MapToCashRegisterDto(CashRegister register) =>
            new()
            {
                Id = register.Id,
                TenantId = register.TenantId,
                TenantName = register.Tenant?.Name,
                TenantSlug = register.Tenant?.Slug,
                RegisterNumber = register.RegisterNumber,
                Location = register.Location,
                Status = register.Status,
                StartingBalance = register.StartingBalance,
                CurrentBalance = register.CurrentBalance,
                LastBalanceUpdate = register.LastBalanceUpdate,
                CurrentUserId = register.CurrentUserId,
                IsActive = register.IsActive,
                IsDefaultForTenant = register.IsDefaultForTenant,
                DecommissionedAtUtc = register.DecommissionedAtUtc,
                DecommissionReason = register.DecommissionReason,
                CreatedAt = register.CreatedAt,
                CreatedBy = register.CreatedBy,
                UpdatedAt = register.UpdatedAt,
                UpdatedBy = register.UpdatedBy,
            };
    }

    // DTOs
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
