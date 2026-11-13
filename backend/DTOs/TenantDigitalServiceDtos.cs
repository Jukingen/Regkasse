namespace KasseAPI_Final.DTOs;

/// <summary>One tenant row for Super Admin digital-service management.</summary>
public sealed class TenantDigitalServiceRowDto
{
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required TenantDigitalServiceStateDto Website { get; init; }
    public required TenantDigitalServiceStateDto App { get; init; }
}

public sealed class TenantDigitalServiceStateDto
{
    public string ServiceType { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool IsActive { get; init; }
    public bool IsAvailable { get; init; }
    /// <summary>Provision lifecycle: none / pending / created / published / rejected.</summary>
    public string Status { get; init; } = "none";
    public bool HasRequest { get; init; }
    public string? Url { get; init; }
    public string? TemplateId { get; init; }
    public string? Customization { get; init; }
    public DateTime? RequestedAt { get; init; }
    public DateTime? ArtifactCreatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    /// <summary>Effective monthly price (custom override or catalog default).</summary>
    public decimal Price { get; init; }
    public decimal? CustomPrice { get; init; }
    public decimal ListPrice { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime? ActivatedAt { get; init; }
    public DateTime? DeactivatedAt { get; init; }
    public string? DeactivationReason { get; init; }
}

public sealed class ToggleTenantDigitalServiceRequestDto
{
    public string ServiceType { get; set; } = string.Empty;
    /// <summary>Maps to <c>TenantServiceStatus.IsActive</c> (Super Admin platform gate).</summary>
    public bool Active { get; set; }
    public string? Reason { get; set; }
}

public sealed class EnableTenantDigitalServiceRequestDto
{
    public string ServiceType { get; set; } = string.Empty;
    /// <summary>Maps to <c>TenantServiceStatus.IsEnabled</c> (Mandanten preference).</summary>
    public bool Enabled { get; set; }
}

public sealed class UpdateTenantDigitalServicePriceRequestDto
{
    public string ServiceType { get; set; } = string.Empty;
    /// <summary>Null clears the override (catalog list price applies).</summary>
    public decimal? CustomPrice { get; set; }
}

public sealed class TenantDigitalServiceMutationResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public TenantDigitalServiceRowDto? Tenant { get; init; }
}
