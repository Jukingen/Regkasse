using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.Countries;

/// <summary>
/// Cash-register fiscal module that applies to a country. Only <see cref="RKSV_AT"/> is implemented;
/// the others name a planned module so a profile can declare intent without enabling anything.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FiscalSystem
{
    /// <summary>No cash-register fiscalisation module. Used by non-AT profiles until their module ships.</summary>
    NONE = 0,

    /// <summary>Austria — RKSV / TSE / FinanzOnline. The only production fiscal system.</summary>
    RKSV_AT = 1,

    /// <summary>Germany — KassenSicherheit. Planned; no implementation exists.</summary>
    KASSENSICHERHEIT_DE = 2,

    /// <summary>Switzerland — MWST. Planned; no implementation exists.</summary>
    MWST_CH = 3,
}
