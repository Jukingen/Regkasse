namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// Per-tenant acknowledgement of open CH QR print gaps.
/// The row is an audit record. It does not change the PDF and it does not enable bank submission.
/// </summary>
public interface IChQrGapAcceptanceService
{
    /// <summary>Fixture gap ids. This is the list the acceptance UI must show.</summary>
    IReadOnlyList<ChQrKnownGap> KnownGaps { get; }

    /// <summary>Current acceptance for the tenant, or null when no override is stored.</summary>
    Task<ChQrGapAcceptance?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <c>tenant_settings</c> key <see cref="ChQrGapAcceptanceService.SettingsKey"/> for this tenant only.
    /// Every id must exist in <see cref="KnownGaps"/>.
    /// </summary>
    Task<ChQrGapAcceptance> AcceptAsync(
        Guid tenantId,
        IReadOnlyList<string> acceptedGaps,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs and publishes a warning for open gaps that this tenant has not accepted.
    /// Does not throw and does not block the invoice.
    /// </summary>
    Task WarnOutstandingAsync(Guid tenantId, Guid? invoiceId, CancellationToken cancellationToken = default);
}

/// <summary>Stored operator acknowledgement. Not a compliance claim.</summary>
public sealed record ChQrGapAcceptance(
    IReadOnlyList<string> AcceptedGaps,
    string AcceptedBy,
    DateTime AcceptedAtUtc);

/// <summary>One or more accepted ids are not in <c>ChQrKnownGaps.json</c>.</summary>
public sealed class ChQrUnknownGapException : Exception
{
    public ChQrUnknownGapException(IReadOnlyList<string> invalidGapIds)
        : base("Unknown CH QR gap id.")
    {
        InvalidGapIds = invalidGapIds;
    }

    public IReadOnlyList<string> InvalidGapIds { get; }
}
