using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.App;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// One-click website and mobile app generator from FA tenant / company settings.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/website")]
[Produces("application/json")]
public sealed class AdminWebsiteController : ControllerBase
{
    public const string DigitalServiceInactiveCode = "DIGITAL_SERVICE_INACTIVE";

    private readonly IWebsiteGeneratorService _websiteGenerator;
    private readonly IAppGeneratorService _appGenerator;
    private readonly ITenantAppGenerator _tenantAppGenerator;
    private readonly IDigitalServicePricingService _pricing;
    private readonly ISubscriptionService _subscriptions;
    private readonly ITenantServiceStatusService _serviceStatuses;
    private readonly AppDbContext _db;
    private readonly IOptions<WebsiteGeneratorOptions> _websiteOptions;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminWebsiteController(
        IWebsiteGeneratorService websiteGenerator,
        IAppGeneratorService appGenerator,
        ITenantAppGenerator tenantAppGenerator,
        IDigitalServicePricingService pricing,
        ISubscriptionService subscriptions,
        ITenantServiceStatusService serviceStatuses,
        AppDbContext db,
        IOptions<WebsiteGeneratorOptions> websiteOptions,
        ICurrentTenantAccessor tenantAccessor)
    {
        _websiteGenerator = websiteGenerator;
        _appGenerator = appGenerator;
        _tenantAppGenerator = tenantAppGenerator;
        _pricing = pricing;
        _subscriptions = subscriptions;
        _serviceStatuses = serviceStatuses;
        _db = db;
        _websiteOptions = websiteOptions;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("templates")]
    [HasPermission(AppPermissions.DigitalView)]
    [ProducesResponseType(typeof(IReadOnlyList<WebsiteTemplateDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WebsiteTemplateDto>> ListTemplates()
    {
        var items = _websiteGenerator.GetTemplates()
            .Select(t => new WebsiteTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                PreviewImage = t.PreviewImage
            })
            .ToList();
        return Ok(items);
    }

    /// <summary>Digital service list prices (website / app tiers) for FA portal and Super Admin.</summary>
    [HttpGet("pricing")]
    [HasPermission(AppPermissions.DigitalView)]
    [ProducesResponseType(typeof(IReadOnlyList<ServicePricingDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ServicePricingDto>> ListPricing([FromQuery] string? type = null)
    {
        var items = _pricing.GetPricing(type)
            .Select(p => new ServicePricingDto
            {
                ServiceId = p.ServiceId,
                Name = p.Name,
                Type = p.Type,
                Tier = p.Tier,
                PriceMonthly = p.PriceMonthly,
                PriceYearly = p.PriceYearly,
                Features = p.Features,
                Currency = p.Currency
            })
            .ToList();
        return Ok(items);
    }

    /// <summary>Mandanten portal: active digital subscriptions for ambient (or Super Admin requested) tenant.</summary>
    [HttpGet("my-services")]
    [HasPermission(AppPermissions.DigitalView)]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDigitalServiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CustomerDigitalServiceDto>>> ListMyServices(
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
            return StatusCode(resolved.StatusCode);

        var db = _db;
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == resolved.TenantId && t.IsActive && t.DeletedAtUtc == null,
                cancellationToken);
        if (tenant is null)
            return NotFound();

        var subscriptions = await _subscriptions.ListForTenantAsync(resolved.TenantId, cancellationToken);
        var opts = _websiteOptions.Value;
        var prefix = NormalizePublicPrefix(opts.PublicUrlPathPrefix);
        var origin = string.IsNullOrWhiteSpace(opts.PublicBaseUrl)
            ? string.Empty
            : opts.PublicBaseUrl.TrimEnd('/');

        var items = subscriptions.Select(sub =>
        {
            var catalog = _pricing.GetByServiceId(sub.ServiceId);
            return new CustomerDigitalServiceDto
            {
                Id = sub.Id,
                ServiceId = sub.ServiceId,
                Name = catalog?.Name ?? sub.ServiceId,
                Type = catalog?.Type ?? "unknown",
                Tier = catalog?.Tier ?? string.Empty,
                Price = sub.Price,
                Currency = sub.Currency,
                Status = sub.Status,
                CreatedAt = sub.CreatedAt,
                NextBillingDate = sub.NextBillingDate,
                Url = BuildPublishedUrl(origin, prefix, tenant.Slug, sub.ServiceId)
            };
        }).ToList();

        return Ok(items);
    }

    /// <summary>Re-publish website + PWA from current catalog (menu sync).</summary>
    [HttpPost("menu-sync")]
    [HasPermission(AppPermissions.DigitalPublish)]
    [ProducesResponseType(typeof(MenuSyncResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MenuSyncResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MenuSyncResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuSyncResponseDto>> SyncMenu(
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveTenantId(tenantId);
        if (!resolved.Succeeded)
        {
            return StatusCode(resolved.StatusCode, new MenuSyncResponseDto
            {
                Succeeded = false,
                Code = resolved.Code,
                Error = resolved.Error
            });
        }

        var websiteAvailable = await _serviceStatuses.IsServiceAvailableAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            cancellationToken);
        var appAvailable = await _serviceStatuses.IsServiceAvailableAsync(
            resolved.TenantId,
            TenantServiceTypes.App,
            cancellationToken);
        if (!websiteAvailable || !appAvailable)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new MenuSyncResponseDto
            {
                Succeeded = false,
                Code = DigitalServiceInactiveCode,
                Error = "Digital website or app service is not available for this tenant."
            });
        }

        var website = await _websiteGenerator.GenerateWebsiteAsync(
            resolved.TenantId,
            "modern",
            cancellationToken);
        if (!website.Succeeded)
        {
            return BadRequest(new MenuSyncResponseDto
            {
                Succeeded = false,
                Code = website.Code,
                Error = website.Error
            });
        }

        var app = await _appGenerator.GenerateAppAsync(
            resolved.TenantId,
            AppType.Pwa,
            cancellationToken);
        if (!app.Succeeded)
        {
            return BadRequest(new MenuSyncResponseDto
            {
                Succeeded = false,
                Code = app.Code,
                Error = app.Error,
                WebsiteUrl = website.Url
            });
        }

        await _serviceStatuses.MarkCreatedAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            website.Url,
            website.TemplateId ?? "modern",
            cancellationToken);
        await _serviceStatuses.MarkPublishedAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            website.Url,
            cancellationToken);
        await _serviceStatuses.MarkCreatedAsync(
            resolved.TenantId,
            TenantServiceTypes.App,
            app.DownloadUrl,
            AppType.Pwa.ToString().ToLowerInvariant(),
            cancellationToken);
        await _serviceStatuses.MarkPublishedAsync(
            resolved.TenantId,
            TenantServiceTypes.App,
            app.DownloadUrl,
            cancellationToken);

        return Ok(new MenuSyncResponseDto
        {
            Succeeded = true,
            WebsiteUrl = website.Url,
            AppUrl = app.DownloadUrl
        });
    }

    private static string NormalizePublicPrefix(string prefix)
    {
        var p = prefix.TrimEnd('/');
        return p.StartsWith('/') ? p : "/" + p;
    }

    private static string? BuildPublishedUrl(string origin, string prefix, string slug, string serviceId)
    {
        var relative = serviceId.StartsWith("app-native", StringComparison.OrdinalIgnoreCase)
            ? $"{prefix}/{slug}/app-native/app-source.zip"
            : serviceId.StartsWith("app-", StringComparison.OrdinalIgnoreCase)
                ? $"{prefix}/{slug}/app/"
                : $"{prefix}/{slug}/";
        return string.IsNullOrEmpty(origin) ? relative : origin + relative;
    }

    [HttpPost("preview")]
    [HasPermission(AppPermissions.DigitalPreview)]
    [ProducesResponseType(typeof(PreviewWebsiteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PreviewWebsiteResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PreviewWebsiteResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PreviewWebsiteResponseDto>> PreviewWebsite(
        [FromBody] PreviewWebsiteRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.TemplateId))
        {
            return BadRequest(new PreviewWebsiteResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "TemplateId is required."
            });
        }

        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
        {
            return StatusCode(resolved.StatusCode, new PreviewWebsiteResponseDto
            {
                Succeeded = false,
                Code = resolved.Code,
                Error = resolved.Error
            });
        }

        var result = await _websiteGenerator.PreviewWebsiteAsync(
            resolved.TenantId,
            body.TemplateId,
            new WebsitePreviewOverrides
            {
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
                CustomJs = body.CustomJs,
            },
            cancellationToken);

        var dto = new PreviewWebsiteResponseDto
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            Html = result.Html,
            Css = result.Css,
            Js = result.Js,
            TemplateId = result.TemplateId,
            TemplateName = result.TemplateName,
            LogoUrl = result.LogoUrl,
            MenuItemCount = result.MenuItemCount,
            CategoryCount = result.CategoryCount
        };

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                WebsiteGeneratorService.TenantNotFoundCode => NotFound(dto),
                WebsiteGeneratorService.TemplateNotFoundCode => BadRequest(dto),
                WebsiteGeneratorService.InvalidSlugCode => BadRequest(dto),
                WebsiteGeneratorService.DisabledCode => StatusCode(StatusCodes.Status503ServiceUnavailable, dto),
                _ => BadRequest(dto)
            };
        }

        return Ok(dto);
    }

    [HttpPost("generate")]
    [HasPermission(AppPermissions.DigitalCreate)]
    [ProducesResponseType(typeof(GenerateWebsiteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GenerateWebsiteResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GenerateWebsiteResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateWebsiteResponseDto>> GenerateWebsite(
        [FromBody] GenerateWebsiteRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.TemplateId))
        {
            return BadRequest(new GenerateWebsiteResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "TemplateId is required."
            });
        }

        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
        {
            return StatusCode(resolved.StatusCode, new GenerateWebsiteResponseDto
            {
                Succeeded = false,
                Code = resolved.Code,
                Error = resolved.Error
            });
        }

        if (!await _serviceStatuses.IsServiceAvailableAsync(
                resolved.TenantId,
                TenantServiceTypes.Website,
                cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new GenerateWebsiteResponseDto
            {
                Succeeded = false,
                Code = DigitalServiceInactiveCode,
                Error = "Digital website service is not available for this tenant."
            });
        }

        var result = await _websiteGenerator.GenerateWebsiteAsync(
            resolved.TenantId,
            body.TemplateId,
            cancellationToken);

        var dto = new GenerateWebsiteResponseDto
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            Url = result.Url,
            TemplateId = result.TemplateId,
            TemplateName = result.TemplateName,
            MenuItemCount = result.MenuItemCount,
            CategoryCount = result.CategoryCount,
            ProgressPercent = result.Progress?.Percent,
            ProgressStage = result.Progress?.Stage
        };

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                WebsiteGeneratorService.TenantNotFoundCode => NotFound(dto),
                WebsiteGeneratorService.TemplateNotFoundCode => BadRequest(dto),
                WebsiteGeneratorService.InvalidSlugCode => BadRequest(dto),
                WebsiteGeneratorService.DisabledCode => StatusCode(StatusCodes.Status503ServiceUnavailable, dto),
                _ => BadRequest(dto)
            };
        }

        await _serviceStatuses.MarkCreatedAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            result.Url,
            result.TemplateId,
            cancellationToken);
        await _serviceStatuses.MarkPublishedAsync(
            resolved.TenantId,
            TenantServiceTypes.Website,
            result.Url,
            cancellationToken);

        return Ok(dto);
    }

    [HttpPost("mobile/generate")]
    [HasPermission(AppPermissions.DigitalCreate)]
    [ProducesResponseType(typeof(GenerateMobileAppResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GenerateMobileAppResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GenerateMobileAppResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateMobileAppResponseDto>> GenerateMobileApp(
        [FromBody] GenerateMobileAppRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new GenerateMobileAppRequestDto();

        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
        {
            return StatusCode(resolved.StatusCode, new GenerateMobileAppResponseDto
            {
                Succeeded = false,
                Code = resolved.Code,
                Error = resolved.Error
            });
        }

        if (!await _serviceStatuses.IsServiceAvailableAsync(
                resolved.TenantId,
                TenantServiceTypes.App,
                cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new GenerateMobileAppResponseDto
            {
                Succeeded = false,
                Code = DigitalServiceInactiveCode,
                Error = "Digital app service is not available for this tenant."
            });
        }

        var result = await _appGenerator.GenerateAppAsync(
            resolved.TenantId,
            body.AppType,
            cancellationToken);

        var dto = new GenerateMobileAppResponseDto
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            Url = result.DownloadUrl,
            AppType = result.AppType
        };

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                AppGeneratorService.TenantNotFoundCode => NotFound(dto),
                AppGeneratorService.InvalidSlugCode => BadRequest(dto),
                AppGeneratorService.UnsupportedAppTypeCode => BadRequest(dto),
                AppGeneratorService.DisabledCode => StatusCode(StatusCodes.Status503ServiceUnavailable, dto),
                _ => BadRequest(dto)
            };
        }

        await _serviceStatuses.MarkCreatedAsync(
            resolved.TenantId,
            TenantServiceTypes.App,
            result.DownloadUrl,
            result.AppType?.ToString().ToLowerInvariant(),
            cancellationToken);
        await _serviceStatuses.MarkPublishedAsync(
            resolved.TenantId,
            TenantServiceTypes.App,
            result.DownloadUrl,
            cancellationToken);

        return Ok(dto);
    }

    /// <summary>
    /// Download a tenant app ZIP (PWA files or native Expo source) plus INSTRUCTIONS.txt.
    /// </summary>
    [HttpPost("mobile/package")]
    [HasPermission(AppPermissions.DigitalCreate)]
    [Produces("application/zip", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateMobileAppPackage(
        [FromBody] GenerateMobileAppRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new GenerateMobileAppRequestDto();

        var resolved = ResolveTenantId(body.TenantId);
        if (!resolved.Succeeded)
        {
            return StatusCode(resolved.StatusCode, new
            {
                code = resolved.Code,
                message = resolved.Error
            });
        }

        if (!await _serviceStatuses.IsServiceAvailableAsync(
                resolved.TenantId,
                TenantServiceTypes.App,
                cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = DigitalServiceInactiveCode,
                message = "Digital app service is not available for this tenant."
            });
        }

        var result = await _tenantAppGenerator.GenerateAppAsync(
            resolved.TenantId,
            body.AppType,
            cancellationToken);

        if (!result.Succeeded || result.ZipFile is null)
        {
            return result.Code switch
            {
                TenantAppGenerator.TenantNotFoundCode => NotFound(new { code = result.Code, message = result.Error }),
                TenantAppGenerator.DisabledCode => StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { code = result.Code, message = result.Error }),
                _ => BadRequest(new { code = result.Code, message = result.Error })
            };
        }

        await _serviceStatuses.MarkCreatedAsync(
            resolved.TenantId,
            TenantServiceTypes.App,
            result.DownloadUrl,
            result.AppType?.ToString().ToLowerInvariant(),
            cancellationToken);

        Response.Headers["X-Published-Url"] = result.DownloadUrl ?? string.Empty;
        Response.Headers["X-App-Type"] = result.AppType?.ToString() ?? string.Empty;
        return File(result.ZipFile, "application/zip", result.FileName ?? "app-package.zip");
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

            // Cross-tenant body must not widen Mandanten-Admin scope → 404 semantics.
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
