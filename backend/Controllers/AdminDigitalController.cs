using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Digital-service lifecycle: Super Admin activate/price; Mandanten read own status + request creation.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/digital")]
[Produces("application/json")]
public sealed class AdminDigitalController : ControllerBase
{
    private readonly ITenantServiceStatusService _statuses;
    private readonly IDigitalServiceRequestService _requests;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminDigitalController(
        ITenantServiceStatusService statuses,
        IDigitalServiceRequestService requests,
        ICurrentTenantAccessor tenantAccessor)
    {
        _statuses = statuses;
        _requests = requests;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>List all tenants with website/app service status and effective prices.</summary>
    [HttpGet("tenants")]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDigitalServiceRowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantDigitalServiceRowDto>>> ListTenants(
        CancellationToken cancellationToken)
    {
        var rows = await _statuses.ListTenantStatusesAsync(cancellationToken);
        return Ok(rows);
    }

    /// <summary>
    /// Mandanten portal / tenant page: status for one tenant (own tenant only unless Super Admin).
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}")]
    [HasPermission(AppPermissions.DigitalView)]
    [ProducesResponseType(typeof(TenantDigitalServiceRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDigitalServiceRowDto>> GetTenant(
        [FromRoute] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode);

        var row = await _statuses.GetForTenantAsync(resolved.TenantId, cancellationToken);
        if (row is null)
            return NotFound();

        return Ok(row);
    }

    /// <summary>Super Admin platform gate: set <c>IsActive</c> for website or app.</summary>
    [HttpPost("{tenantId:guid}/toggle")]
    [HasPermission(AppPermissions.DigitalActivate)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDigitalServiceMutationResponseDto>> Toggle(
        [FromRoute] Guid tenantId,
        [FromBody] ToggleTenantDigitalServiceRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ServiceType))
        {
            return BadRequest(new TenantDigitalServiceMutationResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "ServiceType is required.",
            });
        }

        var result = await _statuses.SetActiveAsync(
            tenantId,
            body.ServiceType,
            body.Active,
            User.GetActorUserId(),
            body.Reason,
            cancellationToken);

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                TenantServiceStatusService.TenantNotFoundCode => NotFound(result),
                _ => BadRequest(result),
            };
        }

        return Ok(result);
    }

    /// <summary>Mandanten preference: enable/disable own digital service (<c>IsEnabled</c>).</summary>
    [HttpPost("{tenantId:guid}/enable")]
    [HasPermission(AppPermissions.DigitalView)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDigitalServiceMutationResponseDto>> SetEnabled(
        [FromRoute] Guid tenantId,
        [FromBody] EnableTenantDigitalServiceRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ServiceType))
        {
            return BadRequest(new TenantDigitalServiceMutationResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "ServiceType is required.",
            });
        }

        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
        {
            return StatusCode(resolved.StatusCode, new TenantDigitalServiceMutationResponseDto
            {
                Succeeded = false,
                Code = "TENANT_NOT_FOUND",
                Error = "Tenant not found.",
            });
        }

        var result = await _statuses.SetEnabledAsync(
            resolved.TenantId,
            body.ServiceType,
            body.Enabled,
            User.GetActorUserId(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                TenantServiceStatusService.TenantNotFoundCode => NotFound(result),
                _ => BadRequest(result),
            };
        }

        return Ok(result);
    }

    /// <summary>Super Admin custom monthly price override (null clears override).</summary>
    [HttpPut("{tenantId:guid}/price")]
    [HasPermission(AppPermissions.DigitalPricingManage)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TenantDigitalServiceMutationResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDigitalServiceMutationResponseDto>> UpdatePrice(
        [FromRoute] Guid tenantId,
        [FromBody] UpdateTenantDigitalServicePriceRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ServiceType))
        {
            return BadRequest(new TenantDigitalServiceMutationResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "ServiceType is required.",
            });
        }

        var result = await _statuses.SetCustomPriceAsync(
            tenantId,
            body.ServiceType,
            body.CustomPrice,
            User.GetActorUserId(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                TenantServiceStatusService.TenantNotFoundCode => NotFound(result),
                _ => BadRequest(result),
            };
        }

        return Ok(result);
    }

    /// <summary>
    /// Manager: request website or app creation for own tenant (Super Admin approval queue).
    /// </summary>
    [HttpPost("{tenantId:guid}/request")]
    [HasPermission(AppPermissions.DigitalRequest)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DigitalServiceRequestResponseDto>> CreateRequest(
        [FromRoute] Guid tenantId,
        [FromBody] CreateDigitalServiceRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ServiceType))
        {
            return BadRequest(new DigitalServiceRequestResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "ServiceType is required.",
            });
        }

        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
        {
            return StatusCode(resolved.StatusCode, new DigitalServiceRequestResponseDto
            {
                Succeeded = false,
                Code = resolved.Code,
                Error = resolved.Error,
            });
        }

        var result = await _requests.CreateAsync(
            resolved.TenantId,
            body.ServiceType,
            body.Note,
            User.GetActorUserId(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                DigitalServiceRequestService.TenantNotFoundCode => NotFound(result),
                _ => BadRequest(result),
            };
        }

        return Ok(result);
    }

    /// <summary>List creation requests for one tenant (own tenant unless Super Admin).</summary>
    [HttpGet("{tenantId:guid}/requests")]
    [HasPermission(AppPermissions.DigitalView)]
    [ProducesResponseType(typeof(IReadOnlyList<DigitalServiceRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DigitalServiceRequestDto>>> ListTenantRequests(
        [FromRoute] Guid tenantId,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode);

        var rows = await _requests.ListAsync(status, resolved.TenantId, cancellationToken);
        return Ok(rows);
    }

    /// <summary>Super Admin: list digital creation requests (default Pending; pass status=all for every status).</summary>
    [HttpGet("requests")]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(typeof(IReadOnlyList<DigitalServiceRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DigitalServiceRequestDto>>> ListRequests(
        [FromQuery] string? status = DigitalServiceRequestStatuses.Pending,
        CancellationToken cancellationToken = default)
    {
        var filter = string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : status;
        var rows = await _requests.ListAsync(filter, tenantId: null, cancellationToken);
        return Ok(rows);
    }

    /// <summary>Super Admin: approve a pending creation request (does not auto-generate).</summary>
    [HttpPost("requests/{id:guid}/approve")]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DigitalServiceRequestResponseDto>> ApproveRequest(
        [FromRoute] Guid id,
        [FromBody] ResolveDigitalServiceRequestDto? body,
        CancellationToken cancellationToken)
    {
        var result = await _requests.ApproveAsync(
            id,
            User.GetActorUserId(),
            body?.Note,
            cancellationToken);

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                DigitalServiceRequestService.RequestNotFoundCode => NotFound(result),
                _ => BadRequest(result),
            };
        }

        return Ok(result);
    }

    /// <summary>Super Admin: reject a pending creation request.</summary>
    [HttpPost("requests/{id:guid}/reject")]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DigitalServiceRequestResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DigitalServiceRequestResponseDto>> RejectRequest(
        [FromRoute] Guid id,
        [FromBody] ResolveDigitalServiceRequestDto? body,
        CancellationToken cancellationToken)
    {
        var result = await _requests.RejectAsync(
            id,
            User.GetActorUserId(),
            body?.Note,
            cancellationToken);

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                DigitalServiceRequestService.RequestNotFoundCode => NotFound(result),
                _ => BadRequest(result),
            };
        }

        return Ok(result);
    }

    private TenantResolveOutcome ResolveTenantId(Guid requestedTenantId)
    {
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var ambient = _tenantAccessor.TenantId;

        if (!isSuperAdmin)
        {
            if (!ambient.HasValue)
            {
                return TenantResolveOutcome.Fail(
                    StatusCodes.Status404NotFound,
                    "TENANT_CONTEXT_REQUIRED",
                    "Tenant context is required.");
            }

            if (requestedTenantId != ambient.Value)
            {
                return TenantResolveOutcome.Fail(
                    StatusCodes.Status404NotFound,
                    "TENANT_NOT_FOUND",
                    "Tenant not found.");
            }

            return TenantResolveOutcome.Ok(ambient.Value);
        }

        return TenantResolveOutcome.Ok(requestedTenantId);
    }

    private readonly record struct TenantResolveOutcome(
        bool Succeeded,
        Guid TenantId,
        int StatusCode,
        string? Code,
        string? Error)
    {
        public static TenantResolveOutcome Ok(Guid tenantId) =>
            new(true, tenantId, StatusCodes.Status200OK, null, null);

        public static TenantResolveOutcome Fail(int statusCode, string code, string error) =>
            new(false, default, statusCode, code, error);
    }
}
