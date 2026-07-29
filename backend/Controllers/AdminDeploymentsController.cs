using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Deployment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin deployment status dashboard + CI ingest + compliance gate + rollback.</summary>
[ApiController]
[Produces("application/json")]
public sealed class AdminDeploymentsController : ControllerBase
{
    private readonly IDeploymentStatusService _deployments;
    private readonly IDeploymentRollbackService _rollback;
    private readonly ITenantDeploymentService _tenantDeployments;
    private readonly IDeploymentAuditService _deploymentAudit;
    private readonly IDeploymentComplianceService _compliance;
    private readonly DeploymentOptions _options;
    private readonly ILogger<AdminDeploymentsController> _logger;

    public AdminDeploymentsController(
        IDeploymentStatusService deployments,
        IDeploymentRollbackService rollback,
        ITenantDeploymentService tenantDeployments,
        IDeploymentAuditService deploymentAudit,
        IDeploymentComplianceService compliance,
        IOptions<DeploymentOptions> options,
        ILogger<AdminDeploymentsController> logger)
    {
        _deployments = deployments;
        _rollback = rollback;
        _tenantDeployments = tenantDeployments;
        _deploymentAudit = deploymentAudit;
        _compliance = compliance;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>List recent deployment runs (Super Admin).</summary>
    [HttpGet("api/admin/deployments")]
    [Authorize]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(DeploymentRunListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeploymentRunListResponseDto>> List(
        [FromQuery] string? stage = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _deployments.ListAsync(stage, take, cancellationToken).ConfigureAwait(false));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Overall tenant deployment status (canary progressive rollouts).</summary>
    [HttpGet("api/admin/deployments/status")]
    [Authorize]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(DeploymentOverallStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeploymentOverallStatusDto>> OverallStatus(
        CancellationToken cancellationToken = default)
    {
        return Ok(await _tenantDeployments.GetOverallStatusAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Latest deployment version per tenant.</summary>
    [HttpGet("api/admin/deployments/tenants")]
    [Authorize]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDeploymentHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantDeploymentHistoryDto>>> ListTenants(
        CancellationToken cancellationToken = default)
    {
        return Ok(await _tenantDeployments.ListLatestPerTenantAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Rollback a single tenant to previousVersion (confirm = "rollback").</summary>
    [HttpPost("api/admin/deployments/tenants/{tenantId:guid}/rollback")]
    [Authorize]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(DeploymentRollbackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeploymentRollbackResultDto>> RollbackTenant(
        Guid tenantId,
        [FromBody] TenantDeploymentRollbackRequest? body,
        CancellationToken cancellationToken = default)
    {
        if (body is null)
            return BadRequest(new { message = "Body is required." });

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";
        try
        {
            var result = await _tenantDeployments
                .RollbackTenantAsync(tenantId, body, actor, cancellationToken)
                .ConfigureAwait(false);
            await _deploymentAudit.LogRollbackAsync(
                result.Stage,
                version: null,
                previousVersion: result.PreviousImageTag,
                tenantIds: new[] { tenantId.ToString("D") },
                actor: actor,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Manual stage rollback (confirm = "rollback"). Invokes configured stage webhook.</summary>
    [HttpPost("api/admin/deployments/rollback")]
    [Authorize]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(DeploymentRollbackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeploymentRollbackResultDto>> Rollback(
        [FromBody] DeploymentRollbackRequest? body,
        CancellationToken cancellationToken = default)
    {
        if (body is null)
            return BadRequest(new { message = "Body is required." });

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";
        try
        {
            var result = await _rollback.RollbackAsync(body, actor, cancellationToken).ConfigureAwait(false);
            await _deploymentAudit.LogRollbackAsync(
                result.Stage,
                version: null,
                previousVersion: result.PreviousImageTag,
                tenantIds: null,
                actor: actor,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Compliance officer (or Super Admin) signs off the RKSV production checklist for an image tag.
    /// </summary>
    [HttpPost("api/admin/deployments/compliance/signoff")]
    [Authorize]
    [HasPermission(AppPermissions.DeploymentApprove)]
    [ProducesResponseType(typeof(DeploymentComplianceSignoffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeploymentComplianceSignoffDto>> ComplianceSignOff(
        [FromBody] DeploymentComplianceSignoffRequest? body,
        CancellationToken cancellationToken = default)
    {
        if (body is null)
            return BadRequest(new { message = "Body is required." });

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role)
                        ?? User.FindFirst("role")?.Value
                        ?? Roles.ComplianceOfficer;
        var display = User.FindFirst("name")?.Value ?? User.Identity?.Name;

        try
        {
            var dto = await _compliance
                .SignOffAsync(body, actorId, actorRole, display, cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Compliance gate status for an image (FA + CI).</summary>
    [HttpGet("api/admin/deployments/compliance/gate")]
    [Authorize]
    [HasPermission(AppPermissions.DeploymentApprove)]
    [ProducesResponseType(typeof(DeploymentComplianceGateStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeploymentComplianceGateStatusDto>> ComplianceGate(
        [FromQuery] string imageTag,
        [FromQuery] string stage = "production",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageTag))
            return BadRequest(new { message = "imageTag is required." });

        try
        {
            return Ok(await _compliance.GetGateStatusAsync(imageTag, stage, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// CI compliance gate check. Auth via deploy token (same as ci-report).
    /// </summary>
    [HttpGet("api/webhooks/deployments/compliance-gate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DeploymentComplianceGateStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeploymentComplianceGateStatusDto>> CiComplianceGate(
        [FromQuery] string imageTag,
        [FromQuery] string stage = "production",
        CancellationToken cancellationToken = default)
    {
        if (!IsDeployTokenValid())
            return Unauthorized(new { message = "Invalid deploy token." });

        if (string.IsNullOrWhiteSpace(imageTag))
            return BadRequest(new { message = "imageTag is required." });

        try
        {
            return Ok(await _compliance.GetGateStatusAsync(imageTag, stage, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// CI status webhook. Auth via <c>Deployment:StatusReportToken</c>.
    /// Also records per-tenant history and immutable audit events.
    /// </summary>
    [HttpPost("api/webhooks/deployments/ci-report")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DeploymentRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeploymentRunDto>> CiReport(
        [FromBody] DeploymentCiReportRequest? body,
        CancellationToken cancellationToken = default)
    {
        if (!IsDeployTokenValid())
        {
            _logger.LogWarning("Deployment CI report rejected: invalid or missing token");
            return Unauthorized(new { message = "Invalid deploy token." });
        }

        if (body is null)
            return BadRequest(new { message = "Body is required." });

        try
        {
            var dto = await _deployments.ReportAsync(body, cancellationToken).ConfigureAwait(false);
            await _deploymentAudit.LogFromCiReportAsync(body, cancellationToken).ConfigureAwait(false);

            if (body.TenantIds is { Count: > 0 }
                && !string.IsNullOrWhiteSpace(body.ImageTag)
                && ShouldRecordTenantHistory(body.Status))
            {
                var historyStatus = MapTenantHistoryStatus(body.Status);
                await _tenantDeployments.RecordFromCiAsync(
                    body.TenantIds,
                    body.ImageTag,
                    body.Stage,
                    historyStatus,
                    body.GitSha,
                    body.RunUrl,
                    body.TriggeredBy,
                    body.SmokePassed,
                    body.ErrorMessage,
                    body.SoakHours,
                    cancellationToken).ConfigureAwait(false);
            }

            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static bool ShouldRecordTenantHistory(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        return s is "succeeded" or "failed" or "rolled_back" or "deploying" or "canary_soak";
    }

    private static string MapTenantHistoryStatus(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        return s switch
        {
            "smoke_running" => "deploying",
            "succeeded" => "succeeded",
            "failed" => "failed",
            "rolled_back" => "rolled_back",
            "deploying" => "deploying",
            "pending" => "pending",
            "canary_soak" => "canary_soak",
            _ => "pending",
        };
    }

    private bool IsDeployTokenValid()
    {
        var configured = _options.StatusReportToken;
        if (string.IsNullOrWhiteSpace(configured))
        {
            _logger.LogWarning("Deployment:StatusReportToken is not configured");
            return false;
        }

        var provided = ResolveProvidedToken();
        if (string.IsNullOrEmpty(provided))
            return false;

        var a = Encoding.UTF8.GetBytes(configured);
        var b = Encoding.UTF8.GetBytes(provided);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private string? ResolveProvidedToken()
    {
        if (Request.Headers.TryGetValue("X-Deploy-Token", out var header) &&
            !string.IsNullOrWhiteSpace(header))
            return header.ToString().Trim();

        var auth = Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth["Bearer ".Length..].Trim();

        return null;
    }
}
