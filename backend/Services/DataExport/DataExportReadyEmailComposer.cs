using System.Globalization;
using System.Net;

namespace KasseAPI_Final.Services.DataExport;

/// <summary>Inputs for GDPR data-export-ready emails (HTML + plain).</summary>
public sealed record DataExportReadyEmailModel(
    string TenantName,
    string AdminName,
    string DownloadLink,
    DateTime ExpiryDateUtc,
    int ValidDays,
    string SupportEmail);

/// <summary>Composed subject + bodies for an export-ready send.</summary>
public sealed record DataExportReadyEmailContent(
    string Subject,
    string HtmlBody,
    string PlainBody);

/// <summary>
/// German HTML + plain composer for “data export ready” notifications.
/// Same pattern as <c>LicenseReminderEmailComposer</c> (no parallel Emails/ stack).
/// </summary>
public static class DataExportReadyEmailComposer
{
    public const string DefaultSupportEmail = "support@regkasse.at";
    public const string DefaultAdminName = "Mandanten-Admin";
    public const string DefaultPrivacyEmail = "privacy@regkasse.at";

    public static DataExportReadyEmailModel CreateModel(
        string tenantName,
        string downloadLink,
        DateTime expiryDateUtc,
        int validDays = 7,
        string? adminName = null,
        string? supportEmail = null)
    {
        var expiry = expiryDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expiryDateUtc, DateTimeKind.Utc)
            : expiryDateUtc.ToUniversalTime();

        return new DataExportReadyEmailModel(
            TenantName: string.IsNullOrWhiteSpace(tenantName) ? "Mandant" : tenantName.Trim(),
            AdminName: string.IsNullOrWhiteSpace(adminName) ? DefaultAdminName : adminName.Trim(),
            DownloadLink: string.IsNullOrWhiteSpace(downloadLink) ? "#" : downloadLink.Trim(),
            ExpiryDateUtc: expiry,
            ValidDays: Math.Clamp(validDays, 1, 30),
            SupportEmail: string.IsNullOrWhiteSpace(supportEmail)
                ? DefaultSupportEmail
                : supportEmail.Trim());
    }

    public static DataExportReadyEmailContent Build(DataExportReadyEmailModel model) =>
        new(BuildSubject(model), BuildHtmlBody(model), BuildPlainBody(model));

    public static string BuildSubject(DataExportReadyEmailModel model) =>
        $"[Regkasse] Datenexport bereit — {model.TenantName}";

    public static string BuildHtmlBody(DataExportReadyEmailModel model)
    {
        var greeting = $"Liebe/r {WebUtility.HtmlEncode(model.AdminName)},";
        var tenant = WebUtility.HtmlEncode(model.TenantName);
        var link = WebUtility.HtmlEncode(model.DownloadLink);
        var expiry = FormatDateLabel(model.ExpiryDateUtc);
        var support = WebUtility.HtmlEncode(model.SupportEmail);
        var privacy = WebUtility.HtmlEncode(DefaultPrivacyEmail);

        return $"""
            <!DOCTYPE html>
            <html lang="de">
            <head><meta charset="utf-8" /><title>Regkasse Datenexport</title></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#262626;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden;border:1px solid #f0f0f0;">
                      <tr>
                        <td style="background:#f6ffed;border-bottom:3px solid #b7eb8f;padding:20px 24px;">
                          <div style="font-size:18px;font-weight:700;color:#389e0d;">Datenexport bereit</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px;">
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">{greeting}</p>
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">
                            Ihr Datenexport für <strong>{tenant}</strong> ist bereit.
                          </p>
                          <div style="background:#f6ffed;padding:15px;border-radius:8px;border:1px solid #b7eb8f;margin:0 0 16px;">
                            <p style="margin:0 0 8px;font-size:14px;">
                              <strong>Download:</strong>
                              <a href="{link}" style="color:#389e0d;font-weight:600;">Hier klicken</a>
                            </p>
                            <p style="margin:0;font-size:14px;">
                              <strong>Gültig bis:</strong> {expiry}
                            </p>
                          </div>
                          <p style="margin:0 0 16px;font-size:14px;line-height:1.5;color:#595959;">
                            Der Link ist {model.ValidDays} Tage gültig. RKSV-Geheimnisse im Export sind maskiert.
                          </p>
                          <p style="margin:0;font-size:13px;color:#8c8c8c;">
                            Fragen? {support} · Datenschutz: {privacy}
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

    public static string BuildPlainBody(DataExportReadyEmailModel model)
    {
        var expiry = FormatDateLabel(model.ExpiryDateUtc);
        return
            $"Liebe/r {model.AdminName},\n\n" +
            $"Ihr Datenexport für {model.TenantName} ist bereit.\n\n" +
            $"Download: {model.DownloadLink}\n" +
            $"Gültig bis: {expiry}\n\n" +
            $"Der Link ist {model.ValidDays} Tage gültig.\n\n" +
            $"Support: {model.SupportEmail}\n" +
            $"Datenschutz: {DefaultPrivacyEmail}\n";
    }

    public static string FormatDateLabel(DateTime utc) =>
        utc.ToUniversalTime().ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-AT"));
}
