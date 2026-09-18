namespace KasseAPI_Final.Services.Countries.Vat;

/// <summary>
/// VIES lookup. Implementations must not persist or log the VAT number. The production default is
/// <see cref="DisabledViesClient"/> (no HTTP). A live SOAP client is a later package.
/// </summary>
public interface IViesClient
{
    /// <summary>
    /// Wire format: ISO country code and the national number as separate arguments — the stored
    /// VAT-ID is not prefix-stripped before it is kept.
    /// </summary>
    Task<ViesLookupResult> LookupAsync(
        string countryCode,
        string vatNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of an optional VIES registration check. Never a shape-validation answer.</summary>
public enum ViesLookupStatus
{
    Valid,
    Invalid,
    Unavailable,
}

/// <summary>
/// VIES lookup result. Reason codes are <see cref="InvalidErrorCode"/> and
/// <see cref="UnavailableErrorCode"/>; a valid answer has no error code.
/// </summary>
public sealed record ViesLookupResult
{
    public const string InvalidErrorCode = "VIES_INVALID";
    public const string UnavailableErrorCode = "VIES_UNAVAILABLE";

    private ViesLookupResult(ViesLookupStatus status, string? errorCode)
    {
        Status = status;
        ErrorCode = errorCode;
    }

    public ViesLookupStatus Status { get; }

    public string? ErrorCode { get; }

    public bool IsValid => Status == ViesLookupStatus.Valid;

    public static ViesLookupResult Valid() => new(ViesLookupStatus.Valid, null);

    public static ViesLookupResult Invalid() => new(ViesLookupStatus.Invalid, InvalidErrorCode);

    public static ViesLookupResult Unavailable() =>
        new(ViesLookupStatus.Unavailable, UnavailableErrorCode);
}
