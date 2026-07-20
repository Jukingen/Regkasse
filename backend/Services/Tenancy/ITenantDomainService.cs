using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tenancy;

public interface ITenantDomainService
{
    Task<IReadOnlyList<TenantDomain>> ListAsync(Guid tenantId, CancellationToken ct = default);

    Task<TenantDomainResult> AddDomainAsync(
        Guid tenantId,
        string domain,
        CancellationToken ct = default);

    Task<TenantDomainResult> VerifyAsync(
        Guid tenantId,
        Guid domainId,
        string? submittedToken = null,
        CancellationToken ct = default);

    Task<TenantDomainResult> SetWebsiteEnabledAsync(
        Guid tenantId,
        Guid domainId,
        bool enabled,
        CancellationToken ct = default);

    Task<TenantDomainResult> SetPrimaryAsync(
        Guid tenantId,
        Guid domainId,
        CancellationToken ct = default);

    Task<TenantDomainResult> RemoveAsync(
        Guid tenantId,
        Guid domainId,
        CancellationToken ct = default);

    /// <summary>Resolve tenant slug from a verified custom host (e.g. cafe-muster.at).</summary>
    Task<string?> TryResolveSlugByHostAsync(string host, CancellationToken ct = default);

    /// <summary>Publish live HTML to the static sites root for this tenant slug.</summary>
    Task<TenantDomainPublishResult> PublishStaticSiteAsync(
        Guid tenantId,
        string? templateId = null,
        CancellationToken ct = default);
}

public sealed class TenantDomainResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public TenantDomain? Domain { get; init; }

    public static TenantDomainResult Ok(TenantDomain domain) =>
        new() { Succeeded = true, Domain = domain };

    public static TenantDomainResult Fail(string code, string error) =>
        new() { Succeeded = false, Code = code, Error = error };
}

public sealed class TenantDomainPublishResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? Url { get; init; }
    public string? CustomDomain { get; init; }

    public static TenantDomainPublishResult Ok(string url, string? customDomain) =>
        new() { Succeeded = true, Url = url, CustomDomain = customDomain };

    public static TenantDomainPublishResult Fail(string code, string error) =>
        new() { Succeeded = false, Code = code, Error = error };
}
