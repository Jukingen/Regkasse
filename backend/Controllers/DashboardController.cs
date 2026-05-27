using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Dashboard;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin dashboard widget catalog and per-user layout preferences (FA).</summary>
[Authorize]
[ApiController]
[Route("api/admin/dashboard")]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        AppDbContext context,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    /// <summary>Available widgets for the current user (filtered by granted permissions).</summary>
    [HttpGet("widgets")]
    public async Task<ActionResult<IReadOnlyList<DashboardWidgetCatalogItemDto>>> GetWidgets(CancellationToken cancellationToken)
    {
        var permissions = await GetGrantedPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var items = DashboardWidgetCatalog.FilterByPermissions(permissions)
            .Select(ToCatalogDto)
            .ToList();
        return Ok(items);
    }

    /// <summary>Saved layout for the current user and effective tenant; defaults when none stored.</summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<DashboardPreferencesResponseDto>> GetPreferences(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var permissions = await GetGrantedPermissionsAsync(cancellationToken).ConfigureAwait(false);

        var row = await _context.DashboardPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
        {
            return Ok(new DashboardPreferencesResponseDto
            {
                Widgets = DashboardWidgetCatalog.BuildDefaultLayout(permissions).Select(ToPreferenceDto).ToList(),
                UpdatedAtUtc = null,
            });
        }

        var merged = MergeWithCatalog(row.Widgets, permissions);
        return Ok(new DashboardPreferencesResponseDto
        {
            Widgets = merged.Select(ToPreferenceDto).ToList(),
            UpdatedAtUtc = row.UpdatedAtUtc,
        });
    }

    /// <summary>Persist widget order, visibility, and settings for the current user and tenant.</summary>
    [HttpPost("preferences")]
    public async Task<ActionResult<DashboardPreferencesResponseDto>> SavePreferences(
        [FromBody] SaveDashboardPreferencesRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Widgets == null || request.Widgets.Count == 0)
            return BadRequest(new { message = "At least one widget is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var permissions = await GetGrantedPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var allowedIds = new HashSet<string>(
            DashboardWidgetCatalog.FilterByPermissions(permissions).Select(w => w.WidgetId),
            StringComparer.OrdinalIgnoreCase);

        var normalized = new List<DashboardWidget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var w in request.Widgets.OrderBy(x => x.Order))
        {
            if (string.IsNullOrWhiteSpace(w.WidgetId) || !allowedIds.Contains(w.WidgetId))
                continue;
            if (!seen.Add(w.WidgetId))
                continue;

            normalized.Add(new DashboardWidget
            {
                WidgetId = w.WidgetId,
                Order = order++,
                IsVisible = w.IsVisible,
                Settings = SanitizeSettings(w.WidgetId, w.Settings),
            });
        }

        if (normalized.Count == 0)
            return BadRequest(new { message = "No valid widgets in request." });

        foreach (var missing in DashboardWidgetCatalog.FilterByPermissions(permissions)
                     .Where(d => normalized.All(n => !string.Equals(n.WidgetId, d.WidgetId, StringComparison.OrdinalIgnoreCase))))
        {
            normalized.Add(new DashboardWidget
            {
                WidgetId = missing.WidgetId,
                Order = order++,
                IsVisible = false,
                Settings = missing.WidgetId == DashboardWidgetCatalog.TopSellingProducts
                    ? new Dictionary<string, JsonElement> { ["period"] = JsonSerializer.SerializeToElement("today") }
                    : null,
            });
        }

        var row = await _context.DashboardPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
        {
            row = new DashboardPreferences
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
            };
            _context.DashboardPreferences.Add(row);
        }

        row.Widgets = normalized.OrderBy(w => w.Order).ToList();
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Dashboard preferences saved for user {UserId} tenant {TenantId} ({WidgetCount} widgets)",
            userId,
            tenantId,
            normalized.Count);

        return Ok(new DashboardPreferencesResponseDto
        {
            Widgets = normalized.Select(ToPreferenceDto).ToList(),
            UpdatedAtUtc = row.UpdatedAtUtc,
        });
    }

    private Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(CancellationToken cancellationToken)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).Where(v => !string.IsNullOrEmpty(v));
        var set = RolePermissionMatrix.GetPermissionsForRoles(roles);
        IReadOnlyList<string> list = set.ToList();
        return Task.FromResult(list);
    }

    private static List<DashboardWidget> MergeWithCatalog(List<DashboardWidget> saved, IReadOnlyList<string> permissions)
    {
        var allowed = DashboardWidgetCatalog.FilterByPermissions(permissions).ToList();
        var byId = saved.ToDictionary(w => w.WidgetId, StringComparer.OrdinalIgnoreCase);
        var merged = new List<DashboardWidget>();
        var order = 0;

        foreach (var w in saved.Where(s => allowed.Any(a => string.Equals(a.WidgetId, s.WidgetId, StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(s => s.Order))
        {
            merged.Add(new DashboardWidget
            {
                WidgetId = w.WidgetId,
                Order = order++,
                IsVisible = w.IsVisible,
                Settings = w.Settings,
            });
            byId.Remove(w.WidgetId);
        }

        foreach (var def in allowed.Where(a => !merged.Any(m => string.Equals(m.WidgetId, a.WidgetId, StringComparison.OrdinalIgnoreCase))))
        {
            merged.Add(new DashboardWidget
            {
                WidgetId = def.WidgetId,
                Order = order++,
                IsVisible = false,
                Settings = def.WidgetId == DashboardWidgetCatalog.TopSellingProducts
                    ? new Dictionary<string, JsonElement> { ["period"] = JsonSerializer.SerializeToElement("today") }
                    : null,
            });
        }

        return merged;
    }

    private static Dictionary<string, JsonElement>? SanitizeSettings(string widgetId, Dictionary<string, JsonElement>? settings)
    {
        if (settings == null || settings.Count == 0)
            return null;

        if (!string.Equals(widgetId, DashboardWidgetCatalog.TopSellingProducts, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!settings.TryGetValue("period", out var periodEl))
            return new Dictionary<string, JsonElement> { ["period"] = JsonSerializer.SerializeToElement("today") };

        var period = periodEl.GetString();
        var normalized = string.Equals(period, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "today";
        return new Dictionary<string, JsonElement> { ["period"] = JsonSerializer.SerializeToElement(normalized) };
    }

    private static DashboardWidgetCatalogItemDto ToCatalogDto(DashboardWidgetDefinition d) => new()
    {
        WidgetId = d.WidgetId,
        Title = d.Title,
        Description = d.Description,
        RequiredPermission = d.RequiredPermission,
        DefaultOrder = d.DefaultOrder,
        DefaultVisible = d.DefaultVisible,
        SupportsAutoRefresh = d.SupportsAutoRefresh,
    };

    private static DashboardWidgetPreferenceDto ToPreferenceDto(DashboardWidget w) => new()
    {
        WidgetId = w.WidgetId,
        Order = w.Order,
        IsVisible = w.IsVisible,
        Settings = w.Settings,
    };
}
