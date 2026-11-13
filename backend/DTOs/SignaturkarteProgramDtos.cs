namespace KasseAPI_Final.DTOs;

public sealed class SignaturkarteProgramStatusDto
{
    public bool Enabled { get; init; }
    public string DisplayName { get; init; } = "Mai 2027 Signaturkarte";
    public DateTime DeadlineUtc { get; init; }
    public int DaysRemaining { get; init; }
    public string? BannerSeverity { get; init; }
    public SignaturkarteProgramTotalsDto Totals { get; init; } = new();
    public int? MilestonesNext { get; init; }
    /// <summary>True when countdown refers to certificate ExpiresAt — always false here (program deadline).</summary>
    public bool IsCertificateExpiry { get; init; }
    public string SeparationNote { get; init; } =
        "This countdown is the Mai 2027 Signaturkarte program deadline, not certificate ExpiresAt.";
}

public sealed class SignaturkarteProgramTotalsDto
{
    public int Compliant { get; init; }
    public int NonCompliant { get; init; }
    public int Excluded { get; init; }
    public int Revoked { get; init; }
    public int Total { get; init; }
}

public sealed class SignaturkarteProgramDeviceDto
{
    public Guid DeviceId { get; init; }
    public Guid? TenantId { get; init; }
    public string? TenantSlug { get; init; }
    public string? TenantName { get; init; }
    public string SerialNumber { get; init; } = string.Empty;
    public string? Provider { get; init; }
    public string? DeviceType { get; init; }
    public string? CertificateStatus { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? ProgramCompliantAtUtc { get; init; }
    public string? ProgramCompliantBy { get; init; }
    public string? ProgramNote { get; init; }
    public string Status { get; init; } = "Open";
    public int DaysToDeadline { get; init; }
    public bool CertificateExpiresBeforeDeadline { get; init; }
}

public sealed class SignaturkarteProgramMarkCompliantRequest
{
    public string? Note { get; set; }
}

public sealed class SignaturkarteProgramMarkCompliantResponse
{
    public bool Success { get; init; }
    public Guid DeviceId { get; init; }
    public DateTime CompliantAtUtc { get; init; }
    public string? Message { get; init; }
}
