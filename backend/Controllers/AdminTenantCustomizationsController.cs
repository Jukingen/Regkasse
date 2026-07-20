using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Website / app branding customization (colors, logo, pages, features, custom CSS/JS).</summary>
[Authorize]
[ApiController]
[Route("api/admin/tenant-customizations")]
[Produces("application/json")]
public sealed class AdminTenantCustomizationsController : ControllerBase
{
    private readonly ITenantCustomizationService _customizations;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTenantCustomizationsController(
        ITenantCustomizationService customizations,
        ICurrentTenantAccessor tenantAccessor)
    {
        _customizations = customizations;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [HasPermission(AppPermissions.WebsiteManage)]
    [ProducesResponseType(typeof(TenantCustomizationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantCustomizationDto>> Get(
        [FromQuery] string type = TenantCustomization.TypeWebsite,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var row = await _customizations.GetOrDefaultAsync(resolved.TenantId, type, ct);
        return Ok(Map(row));
    }

    [HttpPut]
    [HasPermission(AppPermissions.WebsiteManage)]
    [ProducesResponseType(typeof(TenantCustomizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantCustomizationDto>> Upsert(
        [FromBody] UpsertTenantCustomizationRequestDto? body,
        CancellationToken ct = default)
    {
        if (body is null)
            return BadRequest(new { message = "Body is required." });

        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode, new { code = resolved.Code, message = resolved.Error });

        var result = await _customizations.UpsertAsync(
            resolved.TenantId,
            new TenantCustomizationUpsert
            {
                Surface = body.Type,
                PrimaryColor = body.PrimaryColor,
                SecondaryColor = body.SecondaryColor,
                BackgroundColor = body.BackgroundColor,
                TextColor = body.TextColor,
                FontFamily = body.FontFamily,
                LogoUrl = body.LogoUrl,
                FaviconUrl = body.FaviconUrl,
                Pages = body.Pages,
                Features = body.Features,
                CustomCss = body.CustomCss,
                CustomJs = body.CustomJs
            },
            ct);

        if (!result.Succeeded || result.Customization is null)
        {
            return result.Code switch
            {
                TenantCustomizationService.TenantNotFoundCode =>
                    NotFound(new { code = result.Code, message = result.Error }),
                _ => BadRequest(new { code = result.Code, message = result.Error })
            };
        }

        return Ok(Map(result.Customization));
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

    private static TenantCustomizationDto Map(TenantCustomization c) =>
        new()
        {
            Id = c.Id,
            TenantId = c.TenantId,
            Type = c.Surface,
            PrimaryColor = c.PrimaryColor,
            SecondaryColor = c.SecondaryColor,
            BackgroundColor = c.BackgroundColor,
            TextColor = c.TextColor,
            FontFamily = c.FontFamily,
            LogoUrl = c.LogoUrl,
            FaviconUrl = c.FaviconUrl,
            Pages = TenantCustomizationService.ParseJsonList(c.PagesJson, TenantCustomization.DefaultPages),
            Features = TenantCustomizationService.ParseJsonList(c.FeaturesJson, ["live-menu"]),
            CustomCss = c.CustomCss,
            CustomJs = c.CustomJs,
            UpdatedAt = c.UpdatedAt
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
