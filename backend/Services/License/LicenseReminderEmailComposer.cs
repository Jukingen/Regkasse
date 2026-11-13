using System.Globalization;
using System.Net;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;

namespace KasseAPI_Final.Services.License;

/// <summary>Inputs for mandant license expiry reminder emails (HTML + plain).</summary>
public sealed record LicenseReminderEmailModel(
    string TenantName,
    string AdminName,
    int DaysUntilExpiry,
    DateTime? ExpiryDateUtc,
    string RenewalLink,
    string SupportEmail,
    LicenseType? LicenseType = null);

/// <summary>Composed subject + bodies for a reminder send or FA preview.</summary>
public sealed record LicenseReminderEmailContent(
    string Subject,
    string HtmlBody,
    string PlainBody);

/// <summary>German HTML + plain-text composer for mandant license expiry reminder emails.</summary>
public static class LicenseReminderEmailComposer
{
    public const string DefaultAdminLicenseUrl = "https://admin.regkasse.at/license";
    public const string DefaultSupportEmail = "support@regkasse.at";
    public const string DefaultAdminName = "Mandanten-Admin";
    public const string DefaultSampleTenantName = "Cafe Muster";

    public static LicenseReminderEmailModel CreateModel(
        string tenantName,
        int daysUntilExpiry,
        DateTime? expiryDateUtc,
        string? adminName = null,
        string? renewalLink = null,
        string? supportEmail = null,
        LicenseType? licenseType = null)
    {
        return new LicenseReminderEmailModel(
            TenantName: string.IsNullOrWhiteSpace(tenantName) ? "Mandant" : tenantName.Trim(),
            AdminName: string.IsNullOrWhiteSpace(adminName) ? DefaultAdminName : adminName.Trim(),
            DaysUntilExpiry: daysUntilExpiry,
            ExpiryDateUtc: expiryDateUtc,
            RenewalLink: string.IsNullOrWhiteSpace(renewalLink)
                ? DefaultAdminLicenseUrl
                : renewalLink.Trim(),
            SupportEmail: string.IsNullOrWhiteSpace(supportEmail)
                ? DefaultSupportEmail
                : supportEmail.Trim(),
            LicenseType: licenseType);
    }

    public static LicenseReminderEmailModel FromTenant(
        Tenant tenant,
        int? daysRemaining,
        string? recipientName = null,
        string? adminLicenseUrl = null,
        string? supportEmail = null,
        LicenseType? licenseType = null) =>
        CreateModel(
            tenant.Name,
            daysRemaining ?? 0,
            tenant.LicenseValidUntilUtc,
            recipientName,
            adminLicenseUrl,
            supportEmail,
            licenseType);

    /// <summary>Synthetic sample for Super Admin FA preview (no real tenant PII).</summary>
    public static LicenseReminderEmailModel CreateSample(
        int daysUntilExpiry,
        string? tenantName = null,
        string? adminName = null,
        DateTime? expiryDateUtc = null,
        string? renewalLink = null,
        string? supportEmail = null,
        LicenseType? licenseType = null)
    {
        var expiry = expiryDateUtc
            ?? DateTime.UtcNow.Date.AddDays(Math.Max(daysUntilExpiry, 0));
        return CreateModel(
            tenantName ?? DefaultSampleTenantName,
            daysUntilExpiry,
            expiry,
            adminName,
            renewalLink,
            supportEmail,
            licenseType);
    }

    public static LicenseReminderEmailContent Build(LicenseReminderEmailModel model) =>
        new(BuildSubject(model), BuildHtmlBody(model), BuildPlainBody(model));

    public static string BuildSubject(LicenseReminderEmailModel model)
    {
        if (model.DaysUntilExpiry <= 0)
            return $"[DRINGEND] Ihre Regkasse-Lizenz ist abgelaufen - {model.TenantName}";

        return $"[Erinnerung] Ihre Regkasse-Lizenz läuft in {model.DaysUntilExpiry} Tagen ab - {model.TenantName}";
    }

    public static string BuildHtmlBody(LicenseReminderEmailModel model)
    {
        var (bandBg, bandBorder, bandAccent) = ResolveUrgencyColors(model.DaysUntilExpiry);
        var greeting = $"Liebe/r {WebUtility.HtmlEncode(model.AdminName)},";
        var tenant = WebUtility.HtmlEncode(model.TenantName);
        var validUntil = FormatExpiryLabel(model.ExpiryDateUtc);
        var renewUrl = WebUtility.HtmlEncode(model.RenewalLink);
        var support = WebUtility.HtmlEncode(model.SupportEmail);
        var packageLine = FormatPackageHtml(model.LicenseType);
        var lead = BuildLeadHtml(model, tenant, validUntil);

        return $"""
            <!DOCTYPE html>
            <html lang="de">
            <head><meta charset="utf-8" /><title>Regkasse Lizenz</title></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#262626;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden;border:1px solid #f0f0f0;">
                      <tr>
                        <td style="background:{bandBg};border-bottom:3px solid {bandBorder};padding:20px 24px;">
                          <div style="font-size:18px;font-weight:700;color:{bandAccent};">Regkasse Lizenzhinweis</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px;">
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">{greeting}</p>
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">{lead}</p>
                          {packageLine}
                          <p style="margin:0 0 24px;text-align:center;">
                            <a href="{renewUrl}" style="display:inline-block;background:{bandAccent};color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:12px 20px;border-radius:6px;">Jetzt Lizenz verlängern</a>
                          </p>
                          <p style="margin:0 0 8px;font-size:13px;line-height:1.5;color:#595959;">
                            Oder öffnen Sie: <a href="{renewUrl}" style="color:{bandAccent};">{renewUrl}</a>
                          </p>
                          <p style="margin:0 0 16px;font-size:13px;line-height:1.5;color:#595959;">
                            Bei Fragen: <a href="mailto:{support}" style="color:{bandAccent};">{support}</a>
                          </p>
                          <p style="margin:0;font-size:14px;line-height:1.5;">
                            Mit freundlichen Grüßen<br />Ihr Regkasse Team
                          </p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    public static string BuildPlainBody(LicenseReminderEmailModel model)
    {
        var greeting = $"Liebe/r {model.AdminName},";
        var validUntil = FormatExpiryLabel(model.ExpiryDateUtc);
        var packagePlain = model.LicenseType.HasValue
            ? $"Paket: {model.LicenseType.Value}"
            : null;

        if (model.DaysUntilExpiry <= 0)
        {
            var daysOverdue = Math.Abs(model.DaysUntilExpiry);
            var overdueLabel = daysOverdue <= 0 ? "heute" : $"{daysOverdue} Tag(en)";
            var lines = new List<string>
            {
                greeting,
                string.Empty,
                $"Ihre Regkasse-Lizenz für \"{model.TenantName}\" ist seit {overdueLabel} abgelaufen ({validUntil}).",
            };
            if (packagePlain != null)
                lines.Add(packagePlain);

            lines.AddRange(
            [
                string.Empty,
                "Das System wurde aus Compliance-Gründen eingeschränkt.",
                "Bitte verlängern Sie Ihre Lizenz umgehend.",
                string.Empty,
                $"Jetzt verlängern: {model.RenewalLink}",
                string.Empty,
                $"Bei Fragen kontaktieren Sie uns bitte: {model.SupportEmail}",
                string.Empty,
                "Mit freundlichen Grüßen",
                "Ihr Regkasse Team",
            ]);
            return string.Join(Environment.NewLine, lines);
        }

        var bodyLines = new List<string>
        {
            greeting,
            string.Empty,
            $"Ihre Regkasse-Lizenz für \"{model.TenantName}\" läuft in {model.DaysUntilExpiry} Tag(en) ab ({validUntil}).",
        };
        if (packagePlain != null)
            bodyLines.Add(packagePlain);

        bodyLines.AddRange(
        [
            string.Empty,
            "Bitte verlängern Sie Ihre Lizenz, um alle Funktionen weiterhin nutzen zu können.",
            string.Empty,
            $"Jetzt verlängern: {model.RenewalLink}",
            string.Empty,
            $"Bei Fragen kontaktieren Sie uns bitte: {model.SupportEmail}",
            string.Empty,
            "Mit freundlichen Grüßen",
            "Ihr Regkasse Team",
        ]);
        return string.Join(Environment.NewLine, bodyLines);
    }

    /// <summary>Legacy subject helper — prefer <see cref="BuildSubject"/>.</summary>
    public static string BuildMandantExpirySubject(string tenantName, int daysRemaining) =>
        BuildSubject(CreateModel(tenantName, daysRemaining, null));

    /// <summary>Legacy plain-text helper — prefer <see cref="BuildPlainBody"/> / <see cref="Build"/>.</summary>
    public static string BuildMandantExpiryBody(
        Tenant tenant,
        int? daysRemaining,
        string kind,
        string? recipientName = null,
        string? adminLicenseUrl = null,
        string? supportEmail = null)
    {
        _ = kind;
        return BuildPlainBody(FromTenant(tenant, daysRemaining, recipientName, adminLicenseUrl, supportEmail));
    }

    public static (string Background, string Border, string Accent) ResolveUrgencyColors(int daysUntilExpiry)
    {
        if (daysUntilExpiry <= 0)
            return ("#fff1f0", "#cf1322", "#cf1322");
        if (daysUntilExpiry <= 7)
            return ("#fff7e6", "#faad14", "#d48806");
        return ("#e6f7ff", "#1890ff", "#1890ff");
    }

    private static string FormatPackageHtml(LicenseType? licenseType)
    {
        if (!licenseType.HasValue)
            return string.Empty;

        var label = WebUtility.HtmlEncode(licenseType.Value.ToString());
        return $"""
            <p style="margin:0 0 16px;font-size:14px;line-height:1.5;color:#595959;">
              Paket: <strong>{label}</strong>
            </p>
            """;
    }

    private static string BuildLeadHtml(LicenseReminderEmailModel model, string tenantHtml, string validUntil)
    {
        if (model.DaysUntilExpiry <= 0)
        {
            var daysOverdue = Math.Abs(model.DaysUntilExpiry);
            var overdueLabel = daysOverdue <= 0 ? "heute" : $"{daysOverdue} Tag(en)";
            return
                $"Ihre Regkasse-Lizenz für <strong>\"{tenantHtml}\"</strong> ist seit {overdueLabel} abgelaufen ({WebUtility.HtmlEncode(validUntil)})."
                + " Das System wurde aus Compliance-Gründen eingeschränkt. Bitte verlängern Sie Ihre Lizenz umgehend.";
        }

        return
            $"Ihre Regkasse-Lizenz für <strong>\"{tenantHtml}\"</strong> läuft in <strong>{model.DaysUntilExpiry} Tag(en)</strong> ab ({WebUtility.HtmlEncode(validUntil)})."
            + " Bitte verlängern Sie Ihre Lizenz, um alle Funktionen weiterhin nutzen zu können.";
    }

    private static string FormatExpiryLabel(DateTime? expiryDateUtc)
    {
        if (expiryDateUtc is null)
            return "—";
        var utc = DateTime.SpecifyKind(expiryDateUtc.Value, DateTimeKind.Utc);
        return utc.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
