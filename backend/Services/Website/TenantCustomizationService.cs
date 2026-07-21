using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Website;

public interface ITenantCustomizationService
{
    Task<TenantCustomization?> GetAsync(Guid tenantId, string surface, CancellationToken ct = default);

    /// <summary>Returns stored row or in-memory defaults (not persisted until upsert).</summary>
    Task<TenantCustomization> GetOrDefaultAsync(Guid tenantId, string surface, CancellationToken ct = default);

    Task<TenantCustomizationResult> UpsertAsync(
        Guid tenantId,
        TenantCustomizationUpsert input,
        CancellationToken ct = default);
}

public sealed class TenantCustomizationUpsert
{
    public required string Surface { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? TextColor { get; init; }
    public string? FontFamily { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public IReadOnlyList<string>? Pages { get; init; }
    public IReadOnlyList<string>? Features { get; init; }
    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
}

public sealed class TenantCustomizationResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public TenantCustomization? Customization { get; private init; }

    public static TenantCustomizationResult Ok(TenantCustomization row) =>
        new() { Succeeded = true, Customization = row };

    public static TenantCustomizationResult Fail(string code, string error) =>
        new() { Succeeded = false, Code = code, Error = error };
}

public sealed class TenantCustomizationService : ITenantCustomizationService
{
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string InvalidSurfaceCode = "INVALID_SURFACE";
    public const string ValidationFailedCode = "CUSTOMIZATION_VALIDATION_FAILED";

    public const int MaxCustomCssLength = 32_000;
    public const int MaxCustomJsLength = 16_000;

    private static readonly Regex HexColorRegex = new(
        @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SafeFontRegex = new(
        @"^[a-zA-Z0-9\s,\-]{1,128}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<TenantCustomizationService> _logger;

    public TenantCustomizationService(
        IDbContextFactory<AppDbContext> dbFactory,
        TimeProvider clock,
        ILogger<TenantCustomizationService> logger)
    {
        _dbFactory = dbFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task<TenantCustomization?> GetAsync(Guid tenantId, string surface, CancellationToken ct = default)
    {
        var normalized = NormalizeSurface(surface);
        if (normalized is null)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.TenantCustomizations.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Surface == normalized, ct);
    }

    public async Task<TenantCustomization> GetOrDefaultAsync(
        Guid tenantId,
        string surface,
        CancellationToken ct = default)
    {
        var existing = await GetAsync(tenantId, surface, ct);
        if (existing is not null)
            return existing;

        var normalized = NormalizeSurface(surface) ?? TenantCustomization.TypeWebsite;
        return CreateDefault(tenantId, normalized);
    }

    public async Task<TenantCustomizationResult> UpsertAsync(
        Guid tenantId,
        TenantCustomizationUpsert input,
        CancellationToken ct = default)
    {
        var surface = NormalizeSurface(input.Surface);
        if (surface is null)
            return TenantCustomizationResult.Fail(InvalidSurfaceCode, "Surface must be 'website' or 'app'.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantExists = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null, ct);
        if (!tenantExists)
            return TenantCustomizationResult.Fail(TenantNotFoundCode, "Tenant not found.");

        var primary = NormalizeOptionalHex(input.PrimaryColor);
        if (input.PrimaryColor is not null && primary is null)
            return TenantCustomizationResult.Fail(ValidationFailedCode, "PrimaryColor must be a hex color (#RGB or #RRGGBB).");

        var secondary = NormalizeOptionalHex(input.SecondaryColor);
        if (input.SecondaryColor is not null && secondary is null)
            return TenantCustomizationResult.Fail(ValidationFailedCode, "SecondaryColor must be a hex color.");

        var background = NormalizeOptionalHex(input.BackgroundColor);
        if (input.BackgroundColor is not null && background is null)
            return TenantCustomizationResult.Fail(ValidationFailedCode, "BackgroundColor must be a hex color.");

        var text = NormalizeOptionalHex(input.TextColor);
        if (input.TextColor is not null && text is null)
            return TenantCustomizationResult.Fail(ValidationFailedCode, "TextColor must be a hex color.");

        string? font = null;
        if (!string.IsNullOrWhiteSpace(input.FontFamily))
        {
            font = input.FontFamily.Trim();
            if (!SafeFontRegex.IsMatch(font))
                return TenantCustomizationResult.Fail(ValidationFailedCode, "FontFamily contains invalid characters.");
        }

        var pages = FilterAllowed(input.Pages, TenantCustomization.AllowedPages, TenantCustomization.DefaultPages);
        var features = FilterAllowed(input.Features, TenantCustomization.AllowedFeatures, ["live-menu"]);

        var css = SanitizeSnippet(input.CustomCss, MaxCustomCssLength, "</style>");
        var js = SanitizeSnippet(input.CustomJs, MaxCustomJsLength, "</script>");

        var now = _clock.GetUtcNow().UtcDateTime;
        var row = await db.TenantCustomizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Surface == surface, ct);

        if (row is null)
        {
            row = new TenantCustomization
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Surface = surface,
                CreatedAt = now
            };
            db.TenantCustomizations.Add(row);
        }

        row.PrimaryColor = primary;
        row.SecondaryColor = secondary;
        row.BackgroundColor = background;
        row.TextColor = text;
        row.FontFamily = font;
        row.LogoUrl = TrimUrl(input.LogoUrl);
        row.FaviconUrl = TrimUrl(input.FaviconUrl);
        row.PagesJson = JsonSerializer.Serialize(pages);
        row.FeaturesJson = JsonSerializer.Serialize(features);
        row.CustomCss = css;
        row.CustomJs = js;
        row.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Tenant customization upserted for tenant {TenantId} surface {Surface}",
            tenantId,
            surface);

        return TenantCustomizationResult.Ok(row);
    }

    public static IReadOnlyList<string> ParseJsonList(string? json, IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list is { Count: > 0 } ? list : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static TenantCustomization CreateDefault(Guid tenantId, string surface) =>
        new()
        {
            Id = Guid.Empty,
            TenantId = tenantId,
            Surface = surface,
            PrimaryColor = surface == TenantCustomization.TypeApp ? "#0f172a" : "#0f172a",
            SecondaryColor = "#38bdf8",
            BackgroundColor = "#ffffff",
            TextColor = "#0f172a",
            FontFamily = "system-ui, sans-serif",
            PagesJson = JsonSerializer.Serialize(TenantCustomization.DefaultPages),
            FeaturesJson = """["live-menu"]""",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static string? NormalizeSurface(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim().ToLowerInvariant();
        // Sketch used Type; accept both "website"/"app" and legacy "type" aliases.
        return TenantCustomization.AllowedTypes.Contains(v, StringComparer.Ordinal) ? v : null;
    }

    private static string? NormalizeOptionalHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (!v.StartsWith('#'))
            v = "#" + v;
        return HexColorRegex.IsMatch(v) ? v.ToLowerInvariant() : null;
    }

    private static IReadOnlyList<string> FilterAllowed(
        IReadOnlyList<string>? input,
        IReadOnlyList<string> allowed,
        IReadOnlyList<string> fallback)
    {
        if (input is null || input.Count == 0)
            return fallback;

        var filtered = input
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => allowed.Contains(x, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return filtered.Count > 0 ? filtered : fallback;
    }

    private static string? TrimUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var v = url.Trim();
        return v.Length > 2048 ? v[..2048] : v;
    }

    private static string? SanitizeSnippet(string? value, int maxLen, string forbiddenClose)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (v.Length > maxLen)
            v = v[..maxLen];
        // Prevent breaking out of enclosing tags when embedded in generated HTML.
        v = v.Replace(forbiddenClose, string.Empty, StringComparison.OrdinalIgnoreCase);
        return v;
    }
}
