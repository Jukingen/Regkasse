using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.Localization;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Tenant-scoped cash register admin operations (list, create, update, Schlussbeleg decommission, dev-only hard delete).</summary>
[ApiController]
[Route("api/admin/cash-registers")]
[Authorize]
[Produces("application/json")]
public sealed class AdminCashRegistersController : ControllerBase
{
    private readonly ICashRegisterDecommissionService _decommission;
    private readonly ICashRegisterManagementService _cashRegisterManagement;
    private readonly ICashRegisterListEnrichmentService _enrichment;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<AdminCashRegistersController> _logger;
    private readonly IApiMessageLocalizer _messages;

    public AdminCashRegistersController(
        ICashRegisterDecommissionService decommission,
        ICashRegisterManagementService cashRegisterManagement,
        ICashRegisterListEnrichmentService enrichment,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<AdminCashRegistersController> logger,
        IApiMessageLocalizer messages)
    {
        _decommission = decommission;
        _cashRegisterManagement = cashRegisterManagement;
        _enrichment = enrichment;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
        _messages = messages;
    }

    /// <summary>
    /// Lists cash registers. SuperAdmin may pass <paramref name="tenantId"/> to filter a mandant, or omit it to list all tenants.
    /// Pass <paramref name="cashRegisterId"/> to fetch a single register (same authorization rules).
    /// </summary>
    [HttpGet]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(PagedResult<CashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CashRegisterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] string? excludeStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var actorIsSuperAdmin = User.IsInRole(Roles.SuperAdmin);

        try
        {
            if (cashRegisterId is Guid registerId && registerId != Guid.Empty)
            {
                var single = await _cashRegisterManagement.GetByIdAsync(
                    registerId,
                    tenantId,
                    actorIsSuperAdmin,
                    cancellationToken).ConfigureAwait(false);
                if (single == null)
                    return NotFound();
                return Ok(single);
            }

            var result = await _cashRegisterManagement.ListAsync(
                tenantId,
                excludeStatus,
                actorIsSuperAdmin,
                page,
                pageSize,
                cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) when (ex.Message.Contains("other tenants", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Cash register list rejected: missing tenant context");
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Tenant not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Active registers for the selected mandant; default register is listed first.</summary>
    [HttpGet("by-tenant")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(IReadOnlyList<CashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CashRegisterDto>>> ListByTenant(
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return BadRequest(new { message = "No tenant selected" });

        var actorIsSuperAdmin = User.IsInRole(Roles.SuperAdmin);

        try
        {
            var result = await _cashRegisterManagement.ListAsync(
                tenantId,
                nameof(RegisterStatus.Decommissioned),
                actorIsSuperAdmin,
                page: 1,
                pageSize: 100,
                cancellationToken).ConfigureAwait(false);

            var items = result.Items.Where(r => r.IsActive).ToList();
            return Ok(items);
        }
        catch (UnauthorizedAccessException ex) when (ex.Message.Contains("other tenants", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Cash register by-tenant list rejected: missing tenant context");
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Tenant not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Non-decommissioned cash register count for a tenant (Manager: own tenant only).</summary>
    [HttpGet("~/api/admin/tenants/{tenantId:guid}/cash-registers/count")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<int>> GetCashRegisterCount(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorIsSuperAdmin = User.IsInRole(Roles.SuperAdmin);

        try
        {
            var count = await _cashRegisterManagement.GetActiveCountForTenantAsync(
                tenantId,
                actorIsSuperAdmin,
                cancellationToken).ConfigureAwait(false);
            return Ok(count);
        }
        catch (UnauthorizedAccessException ex) when (ex.Message.Contains("other tenants", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Cash register count rejected: missing tenant context");
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Tenant not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>TSE health snapshot for a single cash register (includes offline queue count).</summary>
    [HttpGet("{id:guid}/tse-health")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(CashRegisterTseHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashRegisterTseHealthDto>> GetTseHealth(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var health = await _enrichment.GetTseHealthAsync(id, cancellationToken).ConfigureAwait(false);
        if (health == null)
            return NotFound();

        return Ok(health);
    }

    /// <summary>Feature flags for FA (hard delete visible only in Development + AllowHardDelete).</summary>
    [HttpGet("capabilities")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(AdminCashRegisterCapabilitiesDto), StatusCodes.Status200OK)]
    public ActionResult<AdminCashRegisterCapabilitiesDto> GetCapabilities()
    {
        return Ok(new AdminCashRegisterCapabilitiesDto
        {
            AllowHardDelete = _decommission.IsHardDeleteAllowed(),
            DecommissionViaSchlussbeleg = true,
        });
    }

    /// <summary>
    /// Creates a new cash register row for the effective tenant (Closed, no shift transaction).
    /// Requires <see cref="AppPermissions.CashRegisterManage"/> — SuperAdmin and Manager only.
    /// </summary>
    [HttpPost]
    [HasPermission(AppPermissions.CashRegisterManage)]
    [ProducesResponseType(typeof(CashRegisterDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashRegisterDto>> Create(
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

            var dto = MapToDto(register);
            await _enrichment.ApplyAsync([dto], [register], cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(List), new { id = register.Id }, dto);
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

    /// <summary>Updates register number and location for a tenant-scoped inventory row.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.CashRegisterManage)]
    [ProducesResponseType(typeof(CashRegisterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashRegisterDto>> Update(
        Guid id,
        [FromBody] UpdateCashRegisterRequest request,
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
            var dto = await _cashRegisterManagement.UpdateAsync(
                id,
                request,
                actorUserId,
                actorRole,
                User.IsInRole(Roles.SuperAdmin),
                cancellationToken).ConfigureAwait(false);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Decommissioned", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = ex.Message });
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
            _logger.LogError(ex, "Cash register update failed RegisterId={RegisterId}", id);
            return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.RegisterUpdateError), error = ex.Message });
        }
    }

    /// <summary>
    /// Permanently decommissions a cash register via RKSV Schlussbeleg (Endbeleg) and sets status to Decommissioned atomically.
    /// </summary>
    [HttpPut("{id:guid}/decommission")]
    [HasPermission(AppPermissions.CashRegisterDecommission)]
    [ProducesResponseType(typeof(DecommissionCashRegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DecommissionCashRegisterResponse>> Decommission(
        Guid id,
        [FromBody] DecommissionCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetActorUserId();
        if (string.IsNullOrEmpty(actorUserId))
            return Unauthorized();

        var actorRole = User.GetActorRole() ?? "Unknown";

        try
        {
            var result = await _decommission.DecommissionAsync(
                id,
                request?.Reason,
                actorUserId,
                actorRole,
                cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (RksvOperationGuardException ex)
        {
            _logger.LogWarning(ex, "Decommission rejected for register {RegisterId}", id);
            var status = ex.ErrorCode switch
            {
                RksvGuardErrorCodes.RegisterAlreadyDecommissioned => StatusCodes.Status409Conflict,
                RksvGuardErrorCodes.DuplicateSchlussbeleg => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return StatusCode(status, new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Hard-deletes an empty cash register row (Development + CashRegister:AllowHardDelete only).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> HardDelete(
        Guid id,
        [FromBody] HardDeleteCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!_decommission.IsHardDeleteAllowed())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Hard delete is not enabled for this environment." });

        var actorUserId = User.GetActorUserId();
        if (string.IsNullOrEmpty(actorUserId))
            return Unauthorized();

        var actorRole = User.GetActorRole() ?? "Unknown";

        try
        {
            await _decommission.HardDeleteAsync(id, request.ConfirmPhrase, actorUserId, actorRole, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static CashRegisterDto MapToDto(CashRegister register) =>
        new()
        {
            Id = register.Id,
            TenantId = register.TenantId,
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
