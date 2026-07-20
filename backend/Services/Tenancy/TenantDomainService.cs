using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tenancy;

public sealed class TenantDomainService : ITenantDomainService
{
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string DomainNotFoundCode = "DOMAIN_NOT_FOUND";
    public const string InvalidDomainCode = "INVALID_DOMAIN";
    public const string DomainTakenCode = "DOMAIN_TAKEN";
    public const string VerifyFailedCode = "VERIFY_FAILED";
    public const string PublishFailedCode = "PUBLISH_FAILED";

    private static readonly Regex DomainRegex = new(
        @"^(?=.{1,253}$)(?!-)[a-z0-9-]+(\.[a-z0-9-]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantWebsiteService _website;
    private readonly IOptions<WebsiteGeneratorOptions> _websiteOptions;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _time;
    private readonly ILogger<TenantDomainService> _logger;

    public TenantDomainService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantWebsiteService website,
        IOptions<WebsiteGeneratorOptions> websiteOptions,
        IHostEnvironment environment,
        TimeProvider time,
        ILogger<TenantDomainService> logger)
    {
        _dbFactory = dbFactory;
        _website = website;
        _websiteOptions = websiteOptions;
        _environment = environment;
        _time = time;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantDomain>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.TenantDomains.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.Domain)
            .ToListAsync(ct);
    }

    public async Task<TenantDomainResult> AddDomainAsync(
        Guid tenantId,
        string domain,
        CancellationToken ct = default)
    {
        var normalized = NormalizeDomain(domain);
        if (normalized is null)
            return TenantDomainResult.Fail(InvalidDomainCode, "Domain format is invalid.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null, ct);
        if (tenant is null)
            return TenantDomainResult.Fail(TenantNotFoundCode, "Tenant not found.");

        var taken = await db.TenantDomains.IgnoreQueryFilters()
            .AnyAsync(d => d.Domain == normalized, ct);
        if (taken)
            return TenantDomainResult.Fail(DomainTakenCode, "Domain is already registered.");

        var now = _time.GetUtcNow().UtcDateTime;
        var row = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = normalized,
            Subdomain = tenant.Slug,
            IsVerified = false,
            VerificationToken = CreateVerificationToken(),
            IsActive = true,
            IsPrimary = !await db.TenantDomains.IgnoreQueryFilters()
                .AnyAsync(d => d.TenantId == tenantId && d.IsPrimary, ct),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.TenantDomains.Add(row);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant domain {Domain} added for tenant {TenantId}; verification token issued",
            normalized,
            tenantId);

        return TenantDomainResult.Ok(row);
    }

    public async Task<TenantDomainResult> VerifyAsync(
        Guid tenantId,
        Guid domainId,
        string? submittedToken = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.TenantDomains.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == domainId && d.TenantId == tenantId, ct);
        if (row is null)
            return TenantDomainResult.Fail(DomainNotFoundCode, "Domain not found.");

        // MVP: operator pastes DNS TXT / meta token. DNS live lookup can replace this later.
        if (string.IsNullOrWhiteSpace(submittedToken)
            || !string.Equals(submittedToken.Trim(), row.VerificationToken, StringComparison.Ordinal))
        {
            return TenantDomainResult.Fail(
                VerifyFailedCode,
                "Verification token mismatch. Create a TXT record regkasse-verify=<token> then submit the token.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        row.IsVerified = true;
        row.VerifiedAt = now;
        row.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return TenantDomainResult.Ok(row);
    }

    public async Task<TenantDomainResult> SetWebsiteEnabledAsync(
        Guid tenantId,
        Guid domainId,
        bool enabled,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.TenantDomains.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == domainId && d.TenantId == tenantId, ct);
        if (row is null)
            return TenantDomainResult.Fail(DomainNotFoundCode, "Domain not found.");

        row.IsActive = enabled;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return TenantDomainResult.Ok(row);
    }

    public async Task<TenantDomainResult> SetPrimaryAsync(
        Guid tenantId,
        Guid domainId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await db.TenantDomains.IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(ct);
        var target = rows.FirstOrDefault(d => d.Id == domainId);
        if (target is null)
            return TenantDomainResult.Fail(DomainNotFoundCode, "Domain not found.");

        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var row in rows)
        {
            row.IsPrimary = row.Id == domainId;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return TenantDomainResult.Ok(target);
    }

    public async Task<TenantDomainResult> RemoveAsync(
        Guid tenantId,
        Guid domainId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.TenantDomains.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == domainId && d.TenantId == tenantId, ct);
        if (row is null)
            return TenantDomainResult.Fail(DomainNotFoundCode, "Domain not found.");

        db.TenantDomains.Remove(row);
        await db.SaveChangesAsync(ct);
        return TenantDomainResult.Ok(row);
    }

    public async Task<string?> TryResolveSlugByHostAsync(string host, CancellationToken ct = default)
    {
        var normalized = NormalizeDomain(host);
        if (normalized is null)
            return null;

        // Strip www. for matching apex registrations
        var candidates = new List<string> { normalized };
        if (normalized.StartsWith("www.", StringComparison.Ordinal))
            candidates.Add(normalized["www.".Length..]);
        else
            candidates.Add("www." + normalized);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var match = await db.TenantDomains.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.IsVerified && d.IsActive && candidates.Contains(d.Domain))
            .Select(d => new { d.TenantId, d.Subdomain })
            .FirstOrDefaultAsync(ct);

        if (match is null)
            return null;

        var slug = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == match.TenantId && t.IsActive && t.DeletedAtUtc == null)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(slug) ? match.Subdomain : slug;
    }

    public async Task<TenantDomainPublishResult> PublishStaticSiteAsync(
        Guid tenantId,
        string? templateId = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null, ct);
        if (tenant is null)
            return TenantDomainPublishResult.Fail(TenantNotFoundCode, "Tenant not found.");

        var html = await _website.GetWebsiteHtmlAsync(tenant.Slug, templateId, ct);
        if (string.IsNullOrEmpty(html))
            return TenantDomainPublishResult.Fail(PublishFailedCode, "Could not render website HTML.");

        try
        {
            var opts = _websiteOptions.Value;
            var root = string.IsNullOrWhiteSpace(opts.RootRelativeDirectory)
                ? _environment.ContentRootPath
                : Path.Combine(_environment.ContentRootPath, opts.RootRelativeDirectory);
            var deployDir = Path.Combine(root, tenant.Slug);
            Directory.CreateDirectory(deployDir);
            var indexPath = Path.Combine(deployDir, "index.html");
            await File.WriteAllTextAsync(indexPath, html, Encoding.UTF8, ct);

            var prefix = opts.PublicUrlPathPrefix.TrimEnd('/');
            if (!prefix.StartsWith('/'))
                prefix = "/" + prefix;
            var relative = $"{prefix}/{tenant.Slug}/";
            var url = string.IsNullOrWhiteSpace(opts.PublicBaseUrl)
                ? relative
                : opts.PublicBaseUrl.TrimEnd('/') + relative;

            var custom = await db.TenantDomains.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(d => d.TenantId == tenantId && d.IsVerified && d.IsActive)
                .OrderByDescending(d => d.IsPrimary)
                .Select(d => d.Domain)
                .FirstOrDefaultAsync(ct);

            return TenantDomainPublishResult.Ok(url, custom);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Static site publish failed for tenant {TenantId}", tenantId);
            return TenantDomainPublishResult.Fail(PublishFailedCode, "Static site publish failed.");
        }
    }

    private static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var host = value.Trim().ToLowerInvariant();
        if (host.StartsWith("http://", StringComparison.Ordinal) || host.StartsWith("https://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(host, UriKind.Absolute, out var uri))
                return null;
            host = uri.Host;
        }

        host = host.Trim().TrimEnd('.');
        if (host.Contains(':'))
            host = host.Split(':')[0];

        if (!DomainRegex.IsMatch(host))
            return null;

        // Block platform hosts
        if (host is "regkasse.at" or "localhost"
            || host.EndsWith(".regkasse.at", StringComparison.Ordinal)
            || host.EndsWith(".regkasse.local", StringComparison.Ordinal))
        {
            return null;
        }

        return host;
    }

    private static string CreateVerificationToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
