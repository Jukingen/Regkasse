using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// <c>status_kasse</c> protokol sorgusu için zorunlu alanlar — SOAP transport dışında tutulur.
/// </summary>
public static class FinanzOnlineStatusKasseQueryContextValidator
{
    /// <summary>Real TEST <c>status_kasse</c> reconciliation; tüm kontroller başarısızsa boş değil liste döner.</summary>
    public static IReadOnlyList<string> ValidateForStatusKasse(FinanzOnlineTransmissionStatusQueryRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TransmissionId))
            errors.Add("paket_nr (TransmissionId) is required.");
        else if (!int.TryParse(request.TransmissionId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pak) ||
                 pak < 1 || pak > 999_999_999)
            errors.Add("paket_nr must be an integer 1..999999999.");

        if (string.IsNullOrWhiteSpace(request.RkdbTsErstellungIso))
            errors.Add("ts_erstellung (RkdbTsErstellungIso) is required for status_kasse.");
        else if (!FinanzOnlineRkdbXsDateTime.TryParse(request.RkdbTsErstellungIso, out _))
            errors.Add("ts_erstellung must be a valid XSD dateTime-compatible string.");

        if (request.RkdbSatzNr < 1 || request.RkdbSatzNr > 999_999_999)
            errors.Add("satznr (RkdbSatzNr) must be between 1 and 999999999.");

        var reg = request.Scope.RegisterId?.Trim();
        if (string.IsNullOrEmpty(reg))
            errors.Add("kassenidentifikationsnummer (Scope.RegisterId) is required; empty or placeholder values are not accepted.");
        else if (reg.Length > 50)
            errors.Add("kassenidentifikationsnummer (RegisterId) max length is 50.");

        if (!string.IsNullOrWhiteSpace(request.ExternalReferenceFastNr))
        {
            var f = request.ExternalReferenceFastNr.Trim();
            if (f.Length != 9 || !f.All(char.IsDigit))
                errors.Add("fastnr (ExternalReferenceFastNr) must be exactly 9 digits when provided.");
        }

        return errors;
    }
}
