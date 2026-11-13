namespace KasseAPI_Final.DTOs;

public sealed class TenantDomainDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Domain { get; init; } = string.Empty;
    public string Subdomain { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    /// <summary>Only returned until verified (DNS TXT value).</summary>
    public string? VerificationToken { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public bool IsActive { get; init; }
    public bool IsPrimary { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class AddTenantDomainRequestDto
{
    public string Domain { get; set; } = string.Empty;
    /// <summary>Super Admin only — target tenant when ambient context is missing.</summary>
    public Guid? TenantId { get; set; }
}

public sealed class VerifyTenantDomainRequestDto
{
    public string? Token { get; set; }
    public Guid? TenantId { get; set; }
}

public sealed class SetTenantDomainEnabledRequestDto
{
    public bool Enabled { get; set; }
    public Guid? TenantId { get; set; }
}

public sealed class PublishTenantSiteRequestDto
{
    public string? TemplateId { get; set; }
    public Guid? TenantId { get; set; }
}

public sealed class GenerateTenantWebsitePackageRequestDto
{
    /// <summary>Custom domain (e.g. cafe-muster.at). Falls back to primary verified domain when omitted.</summary>
    public string? Domain { get; set; }

    public string? TemplateId { get; set; }

    /// <summary>Super Admin only — target tenant when ambient context is missing.</summary>
    public Guid? TenantId { get; set; }
}

public sealed class TenantDomainPublishResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? Url { get; init; }
    public string? CustomDomain { get; init; }
}
