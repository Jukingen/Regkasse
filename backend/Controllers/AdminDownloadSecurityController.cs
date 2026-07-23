using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Download rate limits, sensitive-export approvals, and policy for FA clients.</summary>
[Authorize]
[ApiController]
[Route("api/admin/download-security")]
[Produces("application/json")]
public sealed class AdminDownloadSecurityController : ControllerBase
{
    private readonly IDownloadSecurityService _security;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminDownloadSecurityController(
        IDownloadSecurityService security,
        ICurrentTenantAccessor tenantAccessor)
    {
        _security = security;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("policy")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(DownloadSecurityPolicyDto), StatusCodes.Status200OK)]
    public ActionResult<DownloadSecurityPolicyDto> GetPolicy() => Ok(_security.GetPolicy());

    [HttpGet("approvals/mine")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(IReadOnlyList<SensitiveExportApprovalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SensitiveExportApprovalDto>>> ListMine(
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();
        return Ok(await _security.ListMineAsync(userId, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("approvals/pending")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(IReadOnlyList<SensitiveExportApprovalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SensitiveExportApprovalDto>>> ListPending(
        CancellationToken cancellationToken) =>
        Ok(await _security.ListPendingAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost("approvals")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(SensitiveExportApprovalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SensitiveExportApprovalDto>> RequestApproval(
        [FromBody] CreateSensitiveExportApprovalBody body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();
        if (body == null || !SensitiveExportKinds.IsValid(body.ExportKind))
            return BadRequest(new { code = "INVALID_EXPORT_KIND", message = "Unknown export kind." });

        try
        {
            var dto = await _security.RequestApprovalAsync(
                userId,
                _tenantAccessor.TenantId,
                body.ExportKind.Trim(),
                body.ResourceId,
                body.Reason,
                cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(ListMine), dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = ex.Message });
        }
    }

    [HttpPost("approvals/{id:guid}/approve")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(SensitiveExportApprovalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SensitiveExportApprovalDto>> Approve(
        Guid id,
        [FromBody] ResolveSensitiveExportApprovalBody? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var (ok, code, message, dto) = await _security
            .ApproveAsync(id, userId, body?.Note, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
        {
            if (code == "NOT_FOUND")
                return NotFound(new { code, message });
            return BadRequest(new { code, message });
        }

        return Ok(dto);
    }

    [HttpPost("approvals/{id:guid}/reject")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(SensitiveExportApprovalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SensitiveExportApprovalDto>> Reject(
        Guid id,
        [FromBody] ResolveSensitiveExportApprovalBody? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var (ok, code, message, dto) = await _security
            .RejectAsync(id, userId, body?.Note, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
        {
            if (code == "NOT_FOUND")
                return NotFound(new { code, message });
            return BadRequest(new { code, message });
        }

        return Ok(dto);
    }
}

public sealed class CreateSensitiveExportApprovalBody
{
    [Required]
    [MaxLength(64)]
    public string ExportKind { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ResourceId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class ResolveSensitiveExportApprovalBody
{
    [MaxLength(500)]
    public string? Note { get; set; }
}
