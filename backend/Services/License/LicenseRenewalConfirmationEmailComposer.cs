using System.Globalization;
using System.Net;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.License;

/// <summary>Inputs for mandant license renewal confirmation emails (HTML + plain).</summary>
public sealed record LicenseRenewalConfirmationEmailModel(
    string TenantName,
    string AdminName,
    DateTime NewExpiryDateUtc,
    string LicenseKeyDisplay,
    string DashboardLink,
    string SupportEmail);

/// <summary>Composed subject + bodies for a renewal confirmation send.</summary>
public sealed record LicenseRenewalConfirmationEmailContent(
    string Subject,
    string HtmlBody,
    string PlainBody);

/// <summary>
/// German HTML + plain composer for successful mandant license renewal confirmation.
/// Prefer this over a disconnected <c>backend/Emails/</c> stack (same pattern as reminder mail).
/// </summary>
public static class LicenseRenewalConfirmationEmailComposer
{
    public const string DefaultAdminDashboardUrl = "https://admin.regkasse.at/dashboard";
    public const string DefaultSupportEmail = LicenseReminderEmailComposer.DefaultSupportEmail;
    public const string DefaultAdminName = LicenseReminderEmailComposer.DefaultAdminName;

    public static LicenseRenewalConfirmationEmailModel CreateModel(
        string tenantName,
        DateTime newExpiryDateUtc,
        string? licenseKey = null,
        string? adminName = null,
        string? dashboardLink = null,
        string? supportEmail = null)
    {
        var expiry = newExpiryDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(newExpiryDateUtc, DateTimeKind.Utc)
            : newExpiryDateUtc.ToUniversalTime();

        return new LicenseRenewalConfirmationEmailModel(
            TenantName: string.IsNullOrWhiteSpace(tenantName) ? "Mandant" : tenantName.Trim(),
            AdminName: string.IsNullOrWhiteSpace(adminName) ? DefaultAdminName : adminName.Trim(),
            NewExpiryDateUtc: expiry,
            LicenseKeyDisplay: MaskLicenseKey(licenseKey),
            DashboardLink: string.IsNullOrWhiteSpace(dashboardLink)
                ? DefaultAdminDashboardUrl
                : dashboardLink.Trim(),
            SupportEmail: string.IsNullOrWhiteSpace(supportEmail)
                ? DefaultSupportEmail
                : supportEmail.Trim());
    }

    public static LicenseRenewalConfirmationEmailContent Build(LicenseRenewalConfirmationEmailModel model) =>
        new(BuildSubject(model), BuildHtmlBody(model), BuildPlainBody(model));

    public static string BuildSubject(LicenseRenewalConfirmationEmailModel model) =>
        $"[Regkasse] Lizenz erfolgreich verlängert - {model.TenantName}";

    public static string BuildHtmlBody(LicenseRenewalConfirmationEmailModel model)
    {
        var greeting = $"Liebe/r {WebUtility.HtmlEncode(model.AdminName)},";
        var tenant = WebUtility.HtmlEncode(model.TenantName);
        var validUntil = FormatExpiryLabel(model.NewExpiryDateUtc);
        var key = WebUtility.HtmlEncode(model.LicenseKeyDisplay);
        var dashUrl = WebUtility.HtmlEncode(model.DashboardLink);
        var support = WebUtility.HtmlEncode(model.SupportEmail);

        return $"""
            <!DOCTYPE html>
            <html lang="de">
            <head><meta charset="utf-8" /><title>Regkasse Lizenz verlängert</title></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#262626;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden;border:1px solid #f0f0f0;">
                      <tr>
                        <td style="background:#f6ffed;border-bottom:3px solid #52c41a;padding:20px 24px;text-align:center;">
                          <div style="font-size:20px;font-weight:700;color:#389e0d;">Lizenz erfolgreich verlängert</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px;">
                          <p style="margin:0 0 12px 0;font-size:15px;line-height:1.5;">{greeting}</p>
                          <p style="margin:0 0 16px 0;font-size:15px;line-height:1.5;">
                            Ihre Regkasse-Lizenz wurde erfolgreich verlängert.
                          </p>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f6ffed;border-radius:8px;margin:0 0 20px 0;">
                            <tr>
                              <td style="padding:16px;">
                                <p style="margin:0 0 8px 0;font-size:14px;"><strong>Tenant:</strong> {tenant}</p>
                                <p style="margin:0 0 8px 0;font-size:14px;"><strong>Neue Gültigkeit:</strong> {validUntil}</p>
                                <p style="margin:0;font-size:14px;"><strong>Lizenzschlüssel:</strong> {key}</p>
                              </td>
                            </tr>
                          </table>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                            <tr>
                              <td align="center" style="padding:8px 0 20px 0;">
                                <a href="{dashUrl}" style="display:inline-block;background:#1890ff;color:#ffffff;padding:12px 28px;text-decoration:none;border-radius:6px;font-size:15px;font-weight:600;">
                                  Zum Dashboard
                                </a>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:0;font-size:12px;line-height:1.5;color:#8c8c8c;">
                            Vielen Dank für Ihr Vertrauen!<br />
                            Ihr Regkasse Team<br />
                            <a href="mailto:{support}" style="color:#8c8c8c;">{support}</a>
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

    public static string BuildPlainBody(LicenseRenewalConfirmationEmailModel model)
    {
        var validUntil = FormatExpiryLabel(model.NewExpiryDateUtc);
        return $"""
            Liebe/r {model.AdminName},

            Ihre Regkasse-Lizenz wurde erfolgreich verlängert.

            Tenant: {model.TenantName}
            Neue Gültigkeit: {validUntil}
            Lizenzschlüssel: {model.LicenseKeyDisplay}

            Dashboard: {model.DashboardLink}

            Vielen Dank für Ihr Vertrauen!
            Ihr Regkasse Team
            {model.SupportEmail}
            """;
    }

    public static string MaskLicenseKey(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return "—";
        var key = licenseKey.Trim();
        return key.Length <= 16 ? key : key[..16] + "…";
    }

    public static string FormatExpiryLabel(DateTime expiryUtc) =>
        expiryUtc.ToUniversalTime().ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-AT"));
}
