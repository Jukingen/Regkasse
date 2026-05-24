using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.AdminCashRegisters;
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
    private readonly ILogger<AdminCashRegistersController> _logger;

    public AdminCashRegistersController(
        ICashRegisterDecommissionService decommission,
        ICashRegisterManagementService cashRegisterManagement,
        ILogger<AdminCashRegistersController> logger)
    {
        _decommission = decommission;
        _cashRegisterManagement = cashRegisterManagement;
        _logger = logger;
    }

    /// <summary>
    /// Lists cash registers for the effective tenant. SuperAdmin may pass <paramref name="tenantId"/> to list another mandant.
    /// </summary>
    [HttpGet]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(PagedResult<CashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CashRegisterDto>>> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cashRegisterManagement.ListAsync(
                tenantId,
                User.IsInRole(Roles.SuperAdmin),
                page,
                pageSize,
                cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Cash register list rejected: missing tenant context");
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("TenantId filter is only allowed", StringComparison.Ordinal))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Tenant not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
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
            return StatusCode(500, new { message = "Kasa oluşturulurken bir hata oluştu", error = ex.Message });
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
            return StatusCode(500, new { message = "Kasa güncellenirken bir hata oluştu", error = ex.Message });
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
            DecommissionedAtUtc = register.DecommissionedAtUtc,
            DecommissionReason = register.DecommissionReason,
            CreatedAt = register.CreatedAt,
            CreatedBy = register.CreatedBy,
            UpdatedAt = register.UpdatedAt,
            UpdatedBy = register.UpdatedBy,
        };
}
