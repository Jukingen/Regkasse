using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

/// <summary>
/// VAT regime a tenant is invoiced and reported under. Independent from the operating
/// <see cref="CompanySettings.Country"/>: an Austrian mandant may still invoice under
/// <see cref="EU_REVERSE_CHARGE"/>.
///
/// Persisted as the member name (<c>company_settings.vat_regime</c>, <c>varchar(32)</c>), so renaming
/// a member is a breaking schema change — add a new member instead.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VatRegime
{
    /// <summary>Austria, standard RKSV cash-register VAT. Default for every existing mandant.</summary>
    AT_RKSV_STANDARD = 0,

    /// <summary>Germany, standard UStG VAT.</summary>
    DE_USTG_STANDARD = 1,

    /// <summary>Germany, small-business exemption (UStG §19).</summary>
    DE_KLEINUNTERNEHMER = 2,

    /// <summary>Switzerland, standard MWST.</summary>
    CH_MWST_STANDARD = 3,

    /// <summary>Switzerland, small-business exemption.</summary>
    CH_KLEINUNTERNEHMER = 4,

    /// <summary>Intra-EU B2B supply; VAT accounted for by the recipient.</summary>
    EU_REVERSE_CHARGE = 5,

    /// <summary>EU One-Stop-Shop reporting.</summary>
    EU_OSS = 6,

    /// <summary>Outside the EU VAT area.</summary>
    NON_EU = 7,
}

/// <summary>Persisted string values for <see cref="CompanySettings.VatRegime"/>.</summary>
public static class VatRegimeNames
{
    public const string Default = nameof(VatRegime.AT_RKSV_STANDARD);

    /// <summary>Max persisted length; keep in sync with the <c>vat_regime</c> column.</summary>
    public const int MaxLength = 32;
}
