namespace KasseAPI_Final.DTOs;

public sealed class TseCertificateInfoDto
{
    public Guid DeviceRowId { get; set; }
    public string? VendorDeviceId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? CertificateSerialNumber { get; set; }
    public string? Thumbprint { get; set; }
    public string? Issuer { get; set; }
    public string? Subject { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public double? TimeUntilExpiryDays { get; set; }
    public bool IsExpired { get; set; }
    public bool IsRevoked { get; set; }
    public string Status { get; set; } = nameof(Models.TseCertLifecycleStatus.Invalid);
    public string? Source { get; set; }
    public DateTime? ScheduledRenewalAt { get; set; }
    public IReadOnlyList<TseCertificateWarningDto> Warnings { get; set; } =
        Array.Empty<TseCertificateWarningDto>();
}

public sealed class TseCertificateWarningDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
}

public sealed class TseCertificateValidationResultDto
{
    public bool IsValid { get; set; }
    public string Status { get; set; } = nameof(Models.TseCertLifecycleStatus.Invalid);
    public string? Message { get; set; }
    public TseCertificateInfoDto? Certificate { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}

public sealed class TseCertificateRenewalResultDto
{
    public bool Success { get; set; }
    public string Outcome { get; set; } = "Failed";
    public string? Message { get; set; }
    public TseCertificateInfoDto? Certificate { get; set; }
}

public sealed class ScheduleTseCertificateRenewalRequestDto
{
    public DateTime RenewalDateUtc { get; set; }
}
