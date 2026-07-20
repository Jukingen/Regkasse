using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Custom domain management for tenant websites / customer apps.</summary>
[Authorize]
[ApiController]
[Route("api/admin/tenant-domains")]
[Produces("application/json")]
public sealed class AdminTenantDomainsController : ControllerBase
{
    private readonly ITenantDomainService _domains;
    private readonly ITenantWebsiteGenerator _websiteGenerator;
    private readonly ITenantServiceStatusService _serviceStatuses;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTenantDomainsController(
        ITenantDomainService domains,
        ITenantWebsiteGenerator websiteGenerator,
        ITenantServiceStatusService serviceStatuses,
        ICurrentTenantAccessor tenantAccessor)
    {
        _domains = domains;
        _websiteGenerator = websiteGenerator;
        _serviceStatuses = serviceStatuses;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [HasPermission(AppPermissions.WebsiteManage)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TenantDomainDto>>> List(
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var rows = await _domains.ListAsync(resolved.TenantId, ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpPost]
    [HasPermission(AppPermissions.WebsiteManage)]
    [ProducesResponseType(typeof(TenantDomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDomainDto>> Add(
        [FromBody] AddTenantDomainRequestDto? body,
        CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Domain))
            return BadRequest(new { message = "domain is required." });

        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var result = await _domains.AddDomainAsync(resolved.TenantId, body.Domain, ct);
        return MapResult(result);
    }

    [HttpPost("{id:guid}/verify")]
    [HasPermission(AppPermissions.WebsiteManage)]
    public async Task<ActionResult<TenantDomainDto>> Verify(
        Guid id,
        [FromQuery] Guid? tenantId = null,
        [FromBody] VerifyTenantDomainRequestDto? body = null,
        CancellationToken ct = default)
    {
        var resolved = ResolveTenantId(tenantId ?? body?.TenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var result = await _domains.VerifyAsync(resolved.TenantId, id, body?.Token, ct);
        return MapResult(result);
    }

    [HttpPut("{id:guid}/website-enabled")]
    [HasPermission(AppPermissions.WebsiteManage)]
    public async Task<ActionResult<TenantDomainDto>> SetWebsiteEnabled(
        Guid id,
        [FromQuery] Guid? tenantId = null,
        [FromBody] SetTenantDomainEnabledRequestDto? body = null,
        CancellationToken ct = default)
    {
        if (body is null)
            return BadRequest(new { message = "enabled is required." });

        var resolved = ResolveTenantId(tenantId ?? body.TenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var result = await _domains.SetWebsiteEnabledAsync(resolved.TenantId, id, body.Enabled, ct);
        return MapResult(result);
    }

    [HttpPut("{id:guid}/primary")]
    [HasPermission(AppPermissions.WebsiteManage)]
    public async Task<ActionResult<TenantDomainDto>> SetPrimary(
        Guid id,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var result = await _domains.SetPrimaryAsync(resolved.TenantId, id, ct);
        return MapResult(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.WebsiteManage)]
    public async Task<ActionResult<TenantDomainDto>> Remove(
        Guid id,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var result = await _domains.RemoveAsync(resolved.TenantId, id, ct);
        return MapResult(result);
    }

    [HttpPost("publish")]
    [HasPermission(AppPermissions.DigitalPublish)]
    [ProducesResponseType(typeof(TenantDomainPublishResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TenantDomainPublishResponseDto>> Publish(
        [FromBody] PublishTenantSiteRequestDto? body,
        CancellationToken ct = default)
    {
        body ??= new PublishTenantSiteRequestDto();
        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new TenantDomainPublishResponseDto
            {
                Succeeded = false,
                Code = resolved.Code,
                Error = resolved.Error
            });

        var result = await _domains.PublishStaticSiteAsync(resolved.TenantId, body.TemplateId, ct);
        var dto = new TenantDomainPublishResponseDto
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            Url = result.Url,
            CustomDomain = result.CustomDomain
        };
        if (!result.Succeeded)
            return BadRequest(dto);

        await _serviceStatuses.MarkCreatedAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            result.Url,
            body.TemplateId,
            ct);
        await _serviceStatuses.MarkPublishedAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            result.Url,
            ct);

        return Ok(dto);
    }

    /// <summary>
    /// Download a static website ZIP (HTML/CSS/JS + deploy script) for a custom domain.
    /// </summary>
    [HttpPost("generate-package")]
    [HasPermission(AppPermissions.DigitalCreate)]
    [Produces("application/zip", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GeneratePackage(
        [FromBody] GenerateTenantWebsitePackageRequestDto? body,
        CancellationToken ct = default)
    {
        body ??= new GenerateTenantWebsitePackageRequestDto();
        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var domain = body.Domain;
        if (string.IsNullOrWhiteSpace(domain))
        {
            var rows = await _domains.ListAsync(resolved.TenantId, ct);
            domain = rows
                .Where(d => d.IsVerified && d.IsActive)
                .OrderByDescending(d => d.IsPrimary)
                .Select(d => d.Domain)
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(domain))
        {
            return BadRequest(new
            {
                code = TenantWebsiteGenerator.InvalidDomainCode,
                message = "domain is required (or add a verified custom domain first)."
            });
        }

        var result = await _websiteGenerator.GenerateWebsiteAsync(
            resolved.TenantId,
            domain,
            body.TemplateId,
            ct);

        if (!result.Succeeded || result.ZipFile is null)
        {
            return result.Code switch
            {
                TenantWebsiteGenerator.TenantNotFoundCode => NotFound(new { code = result.Code, message = result.Error }),
                TenantWebsiteGenerator.DisabledCode => StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { code = result.Code, message = result.Error }),
                _ => BadRequest(new { code = result.Code, message = result.Error })
            };
        }

        await _serviceStatuses.MarkCreatedAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            result.PublishedUrl,
            body.TemplateId,
            ct);

        Response.Headers["X-Published-Url"] = result.PublishedUrl ?? string.Empty;
        Response.Headers["X-Custom-Domain"] = result.Domain ?? string.Empty;
        return File(result.ZipFile, "application/zip", result.FileName ?? "website.zip");
    }

    private TenantResolveOutcome ResolveTenantId(Guid? requestedTenantId)
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

            if (requestedTenantId.HasValue && requestedTenantId.Value != ambient.Value)
            {
                return TenantResolveOutcome.Fail(
                    StatusCodes.Status404NotFound,
                    "TENANT_NOT_FOUND",
                    "Tenant not found.");
            }

            return TenantResolveOutcome.Ok(ambient.Value);
        }

        if (requestedTenantId.HasValue)
            return TenantResolveOutcome.Ok(requestedTenantId.Value);

        if (ambient.HasValue)
            return TenantResolveOutcome.Ok(ambient.Value);

        return TenantResolveOutcome.Fail(
            StatusCodes.Status400BadRequest,
            "TENANT_ID_REQUIRED",
            "TenantId is required for Super Admin without ambient tenant context.");
    }

    private ActionResult<TenantDomainDto> MapResult(TenantDomainResult result)
    {
        if (!result.Succeeded || result.Domain is null)
        {
            return result.Code switch
            {
                TenantDomainService.TenantNotFoundCode or TenantDomainService.DomainNotFoundCode
                    => NotFound(new { code = result.Code, message = result.Error }),
                _ => BadRequest(new { code = result.Code, message = result.Error })
            };
        }

        return Ok(Map(result.Domain));
    }

    private static TenantDomainDto Map(Models.TenantDomain d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        Domain = d.Domain,
        Subdomain = d.Subdomain,
        IsVerified = d.IsVerified,
        VerificationToken = d.IsVerified ? null : d.VerificationToken,
        VerifiedAt = d.VerifiedAt,
        IsActive = d.IsActive,
        IsPrimary = d.IsPrimary,
        CreatedAt = d.CreatedAt
    };

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
