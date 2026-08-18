namespace KasseAPI_Final.Services.License;

/// <summary>Parsed unified REGK key or legacy deployment display key.</summary>
public sealed class LicenseKeyParseResult
{
    public bool IsValid { get; init; }

    public string Normalized { get; init; } = string.Empty;

    public string? Slug { get; init; }

    public DateTime? ValidUntilUtc { get; init; }

    public string? RandomPart { get; init; }

    public bool IsSystem { get; init; }

    public bool IsTenant { get; init; }

    public bool IsLegacyDisplay { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public static LicenseKeyParseResult Invalid(string input, string? message = null) =>
        new()
        {
            IsValid = false,
            Normalized = (input ?? string.Empty).Trim(),
            ErrorCode = LicenseKeyErrorCodes.InvalidFormat,
            Message = message,
        };
}
