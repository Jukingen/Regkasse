using System.Globalization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Localized mandant license status copy (de/en/tr). Prefer returning
/// <see cref="LicenseStatusMessageKeys"/> + structured fields so clients can re-format.
/// </summary>
public static class LicenseStatusMessages
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Templates =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [LicenseStatusMessageKeys.Active] = Msg(
                de: "Lizenz aktiv",
                en: "License is active",
                tr: "Lisans aktif"),
            [LicenseStatusMessageKeys.ExpiringSoon] = Msg(
                de: "Lizenz läuft in {0} Tag(en) ab. Bitte rechtzeitig verlängern.",
                en: "License expires in {0} day(s). Please renew before expiry.",
                tr: "Lisans {0} gün içinde sona eriyor. Lütfen zamanında yenileyin."),
            [LicenseStatusMessageKeys.Grace] = Msg(
                de: "Mandantenlizenz seit {0} Tag(en) abgelaufen. POS kann noch {1} Tag(e) genutzt werden. Am {2} wird POS gesperrt.",
                en: "Tenant license expired {0} day(s) ago. POS can still be used for {1} more day(s). POS will lock on {2}.",
                tr: "Kiracı lisansı {0} gün önce sona erdi. POS {1} gün daha kullanılabilir. POS {2} tarihinde kilitlenecek."),
            [LicenseStatusMessageKeys.Locked] = Msg(
                de: "Lizenz abgelaufen! POS ist gesperrt. Nur Super-Administrator kann entsperren. Bitte Lizenz erneuern.",
                en: "License expired! POS is locked. Only a Super Admin can unlock. Please renew the license.",
                tr: "Lisans süresi doldu! POS kilitli. Yalnızca Süper Yönetici kilidi açabilir. Lütfen lisansı yenileyin."),
            [LicenseStatusMessageKeys.None] = Msg(
                de: "Keine Mandantenlizenz konfiguriert.",
                en: "No tenant license configured.",
                tr: "Kiracı lisansı yapılandırılmamış."),
        };

    public static string Format(
        string messageKey,
        string? language,
        int daysRemaining = 0,
        int daysOverdue = 0,
        int gracePeriodRemaining = 0,
        DateTime? lockDateUtc = null)
    {
        var lang = LanguageMiddleware.NormalizeLanguage(language);
        if (!Templates.TryGetValue(messageKey, out var translations))
            return messageKey;

        if (!translations.TryGetValue(lang, out var template))
            template = translations[LanguageMiddleware.DefaultLanguage];

        return messageKey switch
        {
            LicenseStatusMessageKeys.ExpiringSoon =>
                string.Format(CultureInfo.InvariantCulture, template, daysRemaining),
            LicenseStatusMessageKeys.Grace =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    template,
                    daysOverdue,
                    gracePeriodRemaining,
                    FormatLockDate(lockDateUtc, lang)),
            _ => template,
        };
    }

    public static IReadOnlyList<string> GetRestrictions(bool isExpired, bool isGracePeriod, bool isLocked)
    {
        if (isLocked)
        {
            return
            [
                LicenseStatusRestrictionCodes.PosLocked,
                LicenseStatusRestrictionCodes.SuperAdminUnlockOnly,
                LicenseStatusRestrictionCodes.RenewalRequired,
            ];
        }

        if (isGracePeriod)
        {
            return
            [
                LicenseStatusRestrictionCodes.PosOperational,
                LicenseStatusRestrictionCodes.WarningsActive,
                LicenseStatusRestrictionCodes.RenewalRecommended,
                LicenseStatusRestrictionCodes.LockPending,
            ];
        }

        if (isExpired)
        {
            return [LicenseStatusRestrictionCodes.RenewalRequired];
        }

        return Array.Empty<string>();
    }

    public static DateTime? ComputeLockDate(DateTime? validUntilUtc, bool isExpired)
    {
        if (!isExpired || !validUntilUtc.HasValue)
            return null;

        var until = DateTime.SpecifyKind(validUntilUtc.Value, DateTimeKind.Utc);
        return until.AddDays(LicenseGracePeriodConfig.GracePeriodDays);
    }

    private static string FormatLockDate(DateTime? lockDateUtc, string language)
    {
        if (!lockDateUtc.HasValue)
            return "—";

        var local = DateTime.SpecifyKind(lockDateUtc.Value, DateTimeKind.Utc);
        return language switch
        {
            "en" => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        };
    }

    private static IReadOnlyDictionary<string, string> Msg(string de, string en, string tr) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["de"] = de,
            ["en"] = en,
            ["tr"] = tr,
        };
}
