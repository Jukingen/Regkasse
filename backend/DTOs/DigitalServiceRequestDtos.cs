namespace KasseAPI_Final.DTOs;

public sealed class CreateDigitalServiceRequestDto
{
    /// <summary><c>website</c> or <c>app</c>.</summary>
    public string ServiceType { get; set; } = string.Empty;

    public string? Note { get; set; }
}

public sealed class ResolveDigitalServiceRequestDto
{
    public string? Note { get; set; }
}

public sealed class DigitalServiceRequestDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public string ServiceType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? RequestedByUserId { get; init; }
    public DateTime RequestedAt { get; init; }
    public string? Note { get; init; }
    public string? ResolvedByUserId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
}

public sealed class DigitalServiceRequestResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public DigitalServiceRequestDto? Request { get; init; }
}
