using System;
using System.Globalization;
using System.Xml;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// XSD dateTime string kontrolü — tam şema doğrulaması değil; XmlConvert + ISO fallback.
/// </summary>
public static class FinanzOnlineRkdbXsDateTime
{
    public static bool TryParse(string? s, out DateTimeOffset normalizedUtc)
    {
        normalizedUtc = default;
        if (string.IsNullOrWhiteSpace(s))
            return false;
        var t = s.Trim();
        if (DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            normalizedUtc = dto.ToUniversalTime();
            return true;
        }

        try
        {
            var dt = XmlConvert.ToDateTime(t, XmlDateTimeSerializationMode.Utc);
            normalizedUtc = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
