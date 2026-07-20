using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Website;

public interface IPublicTenantCatalogService
{
    Task<PublicTenantProfileDto?> GetProfileAsync(string slug, CancellationToken ct = default);
    Task<PublicTenantMenuDto?> GetMenuAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Website/app working-hours status only (isOpen / canOrder / isSpecial). Never for POS/FA.
    /// </summary>
    Task<WebsiteStatusDto?> GetWebsiteStatusAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Today's special-day override from WorkingHours JSON (website only).
    /// </summary>
    Task<WebsiteSpecialDayDto?> GetWebsiteSpecialDayAsync(string slug, CancellationToken ct = default);
}

/// <summary>
/// Shared live catalog for dynamic websites + PWAs (same data as AppGenerator snapshots).
/// Resolved by tenant slug — no ambient tenant required.
/// Online-order acceptance is gated by working hours here (Web/App only — never POS).
/// </summary>
public sealed class PublicTenantCatalogService : IPublicTenantCatalogService
{
    private static readonly Regex SafeSlugRegex = new(
        @"^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly TimeProvider _time;

    public PublicTenantCatalogService(
        IDbContextFactory<AppDbContext> dbFactory,
        TimeProvider? time = null)
    {
        _dbFactory = dbFactory;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PublicTenantProfileDto?> GetProfileAsync(string slug, CancellationToken ct = default)
    {
        var tenant = await ResolveTenantAsync(slug, ct);
        if (tenant is null)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var company = await db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.IsActive, ct);

        var categoryColor = await db.Categories.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenant.Id && c.IsActive && c.Color != null && c.Color != "")
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Color)
            .FirstOrDefaultAsync(ct);

        var status = EvaluateHours(company);
        return new PublicTenantProfileDto
        {
            Slug = tenant.Slug,
            DisplayName = company?.CompanyName ?? tenant.Name,
            Description = company?.CompanyDescription,
            Phone = company?.CompanyPhone ?? tenant.Phone,
            Email = company?.CompanyEmail ?? tenant.Email,
            Address = company?.CompanyAddress ?? tenant.Address,
            LogoUrl = company?.CompanyLogo,
            PrimaryColor = NormalizeHex(categoryColor) ?? "#0f172a",
            AccentColor = "#38bdf8",
            AcceptingOnlineOrders = status.CanOrder,
            RestaurantIsOpen = status.IsOpen,
            OrderStatusMessage = status.Message
        };
    }

    public async Task<WebsiteStatusDto?> GetWebsiteStatusAsync(string slug, CancellationToken ct = default)
    {
        var tenant = await ResolveTenantAsync(slug, ct);
        if (tenant is null)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var company = await db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.IsActive, ct);

        var status = EvaluateHours(company);
        return new WebsiteStatusDto
        {
            IsOpen = status.IsOpen,
            CanOrder = status.CanOrder,
            Message = status.Message,
            OpenTime = status.OpenTime,
            CloseTime = status.CloseTime,
            IsSpecial = status.IsSpecial,
        };
    }

    public async Task<WebsiteSpecialDayDto?> GetWebsiteSpecialDayAsync(
        string slug,
        CancellationToken ct = default)
    {
        var tenant = await ResolveTenantAsync(slug, ct);
        if (tenant is null)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var company = await db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.IsActive, ct);

        var hours = company?.WorkingHours ?? WorkingHoursSettings.CreateDefault();
        hours.Normalize();
        var timeZone = string.IsNullOrWhiteSpace(company?.TimeZone)
            ? "Europe/Vienna"
            : company!.TimeZone.Trim();
        var info = hours.EvaluateSpecialDay(_time.GetUtcNow(), timeZone);
        return new WebsiteSpecialDayDto
        {
            IsSpecial = info.IsSpecial,
            IsClosed = info.IsClosed,
            Message = info.Message,
            OpenTime = info.OpenTime,
            CloseTime = info.CloseTime,
            Date = info.Date,
        };
    }

    public async Task<PublicTenantMenuDto?> GetMenuAsync(string slug, CancellationToken ct = default)
    {
        var tenant = await ResolveTenantAsync(slug, ct);
        if (tenant is null)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var categories = await db.Categories.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenant.Id && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new PublicTenantCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Color = c.Color,
                SortOrder = c.SortOrder
            })
            .ToListAsync(ct);

        var items = await db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant.Id && p.IsActive)
            .OrderBy(p => p.Name)
            .Take(200)
            .Select(p => new PublicTenantMenuItemDto
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.CategoryNavigation != null ? p.CategoryNavigation.Name : p.Category,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                Description = p.Description
            })
            .ToListAsync(ct);

        return new PublicTenantMenuDto
        {
            Slug = tenant.Slug,
            Currency = "EUR",
            Categories = categories,
            Items = items
        };
    }

    private WorkingHoursWebsiteStatus EvaluateHours(CompanySettings? company)
    {
        var hours = company?.WorkingHours ?? WorkingHoursSettings.CreateDefault();
        hours.Normalize();
        var timeZone = string.IsNullOrWhiteSpace(company?.TimeZone)
            ? "Europe/Vienna"
            : company!.TimeZone.Trim();
        return hours.EvaluateWebsiteStatus(_time.GetUtcNow(), timeZone);
    }

    private async Task<TenantRef?> ResolveTenantAsync(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var normalized = slug.Trim().ToLowerInvariant();
        if (!SafeSlugRegex.IsMatch(normalized))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Slug.ToLower() == normalized && t.IsActive && t.DeletedAtUtc == null)
            .Select(t => new TenantRef(t.Id, t.Slug, t.Name, t.Phone, t.Email, t.Address))
            .FirstOrDefaultAsync(ct);

        return tenant;
    }

    private static string? NormalizeHex(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;
        var c = color.Trim();
        if (!c.StartsWith('#'))
            c = "#" + c;
        return c.Length is 4 or 7 ? c : null;
    }

    private sealed record TenantRef(
        Guid Id,
        string Slug,
        string Name,
        string? Phone,
        string? Email,
        string? Address);
}
