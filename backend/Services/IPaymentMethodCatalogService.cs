using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Konfigurierbare Zahlungsarten: POS-Liste, Legacy-Raw-Auflösung für payment_details.PaymentMethod (varchar 0–5).
/// </summary>
public interface IPaymentMethodCatalogService
{
    Task<IReadOnlyList<PosPaymentMethodDto>> GetActivePosMethodsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Für Zahlungserfassung: DB-Zeile aktiv → Legacy-Raw; Zeile inaktiv → Fehler; keine Zeile → Legacy-Fallback (Abwärtskompatibilität).
    /// </summary>
    Task<PaymentMethodResolutionResult> ResolveForPaymentAsync(string? methodCode, CancellationToken cancellationToken = default);

    /// <summary>Für Filter/Reports: mappt Code auf Legacy-Raw; ignoriert IsActive (historische Daten).</summary>
    Task<string> ResolveRawForFilterAsync(string? methodCode, CancellationToken cancellationToken = default);
}

/// <param name="MatchedPaymentMethodDefinition">True when resolution used a tenant <c>payment_method_definitions</c> row (not legacy string fallback).</param>
public sealed record PaymentMethodResolutionResult(
    bool Ok,
    string LegacyRaw,
    string? ErrorMessage,
    bool MatchedPaymentMethodDefinition = false);
