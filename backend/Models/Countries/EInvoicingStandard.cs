using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.Countries;

/// <summary>
/// E-invoicing document standards a country profile may declare. Declaring a standard does not
/// enable a builder — every one of these is gated behind an <c>EInvoicing.*</c> feature flag and
/// none is implemented yet. See <c>docs/EINVOICING_EU.md</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EInvoicingStandard
{
    /// <summary>Germany — hybrid PDF/A-3 + XML. Planned.</summary>
    ZUGFERD = 1,

    /// <summary>Germany — public-sector XML profile. Planned.</summary>
    XRECHNUNG = 2,

    /// <summary>Switzerland — SIX QR-bill. Planned. Not an EN 16931 profile.</summary>
    QR_RECHNUNG = 3,

    /// <summary>EU — semantic data model for electronic invoicing. Planned.</summary>
    EN_16931 = 4,
}
