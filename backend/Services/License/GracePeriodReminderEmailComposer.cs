using System.Globalization;
using System.Net;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.License;

/// <summary>Inputs for mandant grace-period reminder emails (HTML + plain).</summary>
public sealed record GracePeriodReminderEmailModel(
    string TenantName,
    string AdminName,
    int DaysRemaining,
    DateTime LockdownDateUtc,
    string RenewalLink,
    string SupportEmail);

/// <summary>Composed subject + bodies for a grace reminder send or FA preview.</summary>
public sealed record GracePeriodReminderEmailContent(
    string Subject,
    string HtmlBody,
    string PlainBody);

/// <summary>
/// German HTML + plain composer for mandant license grace-period reminder emails.
/// Prefer this over a disconnected <c>backend/Emails/</c> stack (same pattern as
/// <see cref="LicenseReminderEmailComposer"/>).
/// </summary>
public static class GracePeriodReminderEmailComposer
{
    public const string DefaultAdminLicenseUrl = LicenseReminderEmailComposer.DefaultAdminLicenseUrl;
    public const string DefaultSupportEmail = LicenseReminderEmailComposer.DefaultSupportEmail;
    public const string DefaultAdminName = LicenseReminderEmailComposer.DefaultAdminName;
    public const string DefaultSampleTenantName = LicenseReminderEmailComposer.DefaultSampleTenantName;

    /// <summary>Banner / subject escalate when this many grace days (or fewer) remain.</summary>
    public const int UrgentDaysThreshold = 2;

    public static GracePeriodReminderEmailModel CreateModel(
        string tenantName,
        int daysRemaining,
        DateTime lockdownDateUtc,
        string? adminName = null,
        string? renewalLink = null,
        string? supportEmail = null)
    {
        var lockdown = lockdownDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(lockdownDateUtc, DateTimeKind.Utc)
            : lockdownDateUtc.ToUniversalTime();

        return new GracePeriodReminderEmailModel(
            TenantName: string.IsNullOrWhiteSpace(tenantName) ? "Mandant" : tenantName.Trim(),
            AdminName: string.IsNullOrWhiteSpace(adminName) ? DefaultAdminName : adminName.Trim(),
            DaysRemaining: Math.Max(0, daysRemaining),
            LockdownDateUtc: lockdown,
            RenewalLink: string.IsNullOrWhiteSpace(renewalLink)
                ? DefaultAdminLicenseUrl
                : renewalLink.Trim(),
            SupportEmail: string.IsNullOrWhiteSpace(supportEmail)
                ? DefaultSupportEmail
                : supportEmail.Trim());
    }

    public static GracePeriodReminderEmailModel FromTenant(
        Tenant tenant,
        int daysRemaining,
        DateTime lockdownDateUtc,
        string? recipientName = null,
        string? adminLicenseUrl = null,
        string? supportEmail = null) =>
        CreateModel(
            tenant.Name,
            daysRemaining,
            lockdownDateUtc,
            recipientName,
            adminLicenseUrl,
            supportEmail);

    /// <summary>Synthetic sample for Super Admin FA preview (no real tenant PII).</summary>
    public static GracePeriodReminderEmailModel CreateSample(
        int daysRemaining,
        string? tenantName = null,
        string? adminName = null,
        DateTime? lockdownDateUtc = null,
        string? renewalLink = null,
        string? supportEmail = null)
    {
        var lockdown = lockdownDateUtc
            ?? DateTime.UtcNow.Date.AddDays(Math.Max(daysRemaining, 0));
        return CreateModel(
            tenantName ?? DefaultSampleTenantName,
            daysRemaining,
            lockdown,
            adminName,
            renewalLink,
            supportEmail);
    }

    public static GracePeriodReminderEmailContent Build(GracePeriodReminderEmailModel model) =>
        new(BuildSubject(model), BuildHtmlBody(model), BuildPlainBody(model));

    public static bool IsUrgent(int daysRemaining) => daysRemaining <= UrgentDaysThreshold;

    public static string BuildSubject(GracePeriodReminderEmailModel model)
    {
        if (IsUrgent(model.DaysRemaining))
        {
            return
                $"[DRINGEND] Grace-Period endet in {model.DaysRemaining} Tagen - {model.TenantName}";
        }

        return
            $"[Erinnerung] Grace-Period: {model.DaysRemaining} Tage verbleibend - {model.TenantName}";
    }

    public static string BuildHtmlBody(GracePeriodReminderEmailModel model)
    {
        var urgent = IsUrgent(model.DaysRemaining);
        var urgencyLabel = urgent ? "DRINGEND" : "Erinnerung";
        var accent = urgent ? "#cf1322" : "#faad14";
        var bandBg = urgent ? "#fff1f0" : "#fff7e6";
        var greeting = $"Liebe/r {WebUtility.HtmlEncode(model.AdminName)},";
        var tenant = WebUtility.HtmlEncode(model.TenantName);
        var lockdown = FormatDateLabel(model.LockdownDateUtc);
        var renewUrl = WebUtility.HtmlEncode(model.RenewalLink);
        var support = WebUtility.HtmlEncode(model.SupportEmail);

        return $"""
            <!DOCTYPE html>
            <html lang="de">
            <head><meta charset="utf-8" /><title>Regkasse Grace-Period</title></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#262626;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden;border:1px solid #f0f0f0;">
                      <tr>
                        <td style="background:{bandBg};border-bottom:3px solid {accent};padding:20px 24px;text-align:center;">
                          <div style="font-size:20px;font-weight:700;color:{accent};">{urgencyLabel}</div>
                          <div style="font-size:16px;font-weight:600;color:{accent};margin-top:4px;">Grace-Period</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px;">
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">{greeting}</p>
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">
                            Ihre Lizenz für <strong>{tenant}</strong> ist abgelaufen.
                            Sie befinden sich in der <strong>Grace-Period</strong>.
                          </p>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#fff7e6;border-radius:8px;margin:0 0 20px;">
                            <tr>
                              <td style="padding:16px;">
                                <p style="margin:0 0 8px;font-size:14px;line-height:1.5;">
                                  <strong>Verbleibende Tage:</strong> {model.DaysRemaining} Tag(e)
                                </p>
                                <p style="margin:0;font-size:14px;line-height:1.5;">
                                  <strong>Sperrung am:</strong> {WebUtility.HtmlEncode(lockdown)}
                                </p>
                              </td>
                            </tr>
                          </table>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#fff1f0;border-radius:8px;margin:0 0 24px;">
                            <tr>
                              <td style="padding:16px;">
                                <p style="margin:0 0 8px;font-size:14px;font-weight:700;color:#cf1322;">
                                  Was passiert nach der Grace-Period?
                                </p>
                                <ul style="margin:0;padding-left:20px;font-size:14px;line-height:1.6;color:#595959;">
                                  <li>Das System wird gesperrt</li>
                                  <li>Sie können nur noch lesend auf Ihre Daten zugreifen</li>
                                  <li>Keine Änderungen oder Neuanlagen möglich</li>
                                </ul>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:0 0 24px;text-align:center;">
                            <a href="{renewUrl}" style="display:inline-block;background:{accent};color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:12px 24px;border-radius:6px;">Jetzt Lizenz verlängern</a>
                          </p>
                          <p style="margin:0 0 8px;font-size:13px;line-height:1.5;color:#595959;">
                            Oder öffnen Sie: <a href="{renewUrl}" style="color:{accent};">{renewUrl}</a>
                          </p>
                          <p style="margin:0 0 16px;font-size:13px;line-height:1.5;color:#595959;">
                            Bei Fragen: <a href="mailto:{support}" style="color:{accent};">{support}</a>
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

    public static string BuildPlainBody(GracePeriodReminderEmailModel model)
    {
        var greeting = $"Liebe/r {model.AdminName},";
        var lockdown = FormatDateLabel(model.LockdownDateUtc);
        var urgency = IsUrgent(model.DaysRemaining) ? "DRINGEND" : "Erinnerung";

        return string.Join(Environment.NewLine,
        [
            greeting,
            string.Empty,
            $"{urgency}: Ihre Regkasse-Lizenz für \"{model.TenantName}\" ist abgelaufen.",
            $"Sie befinden sich in der Grace-Period ({model.DaysRemaining} Tag(e) verbleibend).",
            $"Sperrung am: {lockdown}",
            string.Empty,
            "Nach Ablauf der Grace-Period wird das System gesperrt.",
            "Sie können dann nur noch lesend auf Ihre Daten zugreifen.",
            string.Empty,
            $"Jetzt verlängern: {model.RenewalLink}",
            string.Empty,
            $"Bei Fragen kontaktieren Sie uns bitte: {model.SupportEmail}",
            string.Empty,
            "Mit freundlichen Grüßen",
            "Ihr Regkasse Team",
        ]);
    }

    private static string FormatDateLabel(DateTime dateUtc)
    {
        var utc = DateTime.SpecifyKind(dateUtc, DateTimeKind.Utc);
        return utc.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
