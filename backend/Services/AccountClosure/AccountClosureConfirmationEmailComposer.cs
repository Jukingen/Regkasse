using System.Globalization;
using System.Net;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataExport;

namespace KasseAPI_Final.Services.AccountClosure;

/// <summary>Inputs for account-closure confirmation emails (HTML + plain).</summary>
public sealed record AccountClosureConfirmationEmailModel(
    string TenantName,
    string AdminName,
    DateTime ScheduledDateUtc,
    bool HasRksvData,
    int ConfirmationWaitDays,
    string SupportEmail);

/// <summary>Composed subject + bodies for closure confirmation.</summary>
public sealed record AccountClosureConfirmationEmailContent(
    string Subject,
    string HtmlBody,
    string PlainBody);

/// <summary>
/// German HTML + plain composer for account-closure confirmation
/// (sent after FA confirmation; purge eligible after <see cref="DataDeletionService.ConfirmationWaitDays"/>).
/// </summary>
public static class AccountClosureConfirmationEmailComposer
{
    public const string DefaultSupportEmail = DataExportReadyEmailComposer.DefaultSupportEmail;
    public const string DefaultAdminName = DataExportReadyEmailComposer.DefaultAdminName;
    public const string DefaultPrivacyEmail = DataExportReadyEmailComposer.DefaultPrivacyEmail;

    public static AccountClosureConfirmationEmailModel CreateModel(
        string tenantName,
        DateTime scheduledDateUtc,
        bool hasRksvData,
        string? adminName = null,
        int confirmationWaitDays = DataDeletionService.ConfirmationWaitDays,
        string? supportEmail = null)
    {
        var scheduled = scheduledDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(scheduledDateUtc, DateTimeKind.Utc)
            : scheduledDateUtc.ToUniversalTime();

        return new AccountClosureConfirmationEmailModel(
            TenantName: string.IsNullOrWhiteSpace(tenantName) ? "Mandant" : tenantName.Trim(),
            AdminName: string.IsNullOrWhiteSpace(adminName) ? DefaultAdminName : adminName.Trim(),
            ScheduledDateUtc: scheduled,
            HasRksvData: hasRksvData,
            ConfirmationWaitDays: Math.Max(1, confirmationWaitDays),
            SupportEmail: string.IsNullOrWhiteSpace(supportEmail)
                ? DefaultSupportEmail
                : supportEmail.Trim());
    }

    public static AccountClosureConfirmationEmailContent Build(AccountClosureConfirmationEmailModel model) =>
        new(BuildSubject(model), BuildHtmlBody(model), BuildPlainBody(model));

    public static string BuildSubject(AccountClosureConfirmationEmailModel model) =>
        $"[Regkasse] Kontoschließung bestätigt — {model.TenantName}";

    public static string BuildHtmlBody(AccountClosureConfirmationEmailModel model)
    {
        var greeting = $"Liebe/r {WebUtility.HtmlEncode(model.AdminName)},";
        var tenant = WebUtility.HtmlEncode(model.TenantName);
        var scheduled = FormatDateLabel(model.ScheduledDateUtc);
        var support = WebUtility.HtmlEncode(model.SupportEmail);
        var privacy = WebUtility.HtmlEncode(DefaultPrivacyEmail);
        var rksvLine = model.HasRksvData
            ? "Ihre RKSV-Daten bleiben mindestens 7 Jahre gespeichert"
            : "Es liegen keine RKSV-Fiskaldaten vor; nicht-fiskalische Daten werden vollständig gelöscht";

        return $"""
            <!DOCTYPE html>
            <html lang="de">
            <head><meta charset="utf-8" /><title>Regkasse Kontoschließung</title></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#262626;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden;border:1px solid #f0f0f0;">
                      <tr>
                        <td style="background:#fff1f0;border-bottom:3px solid #ffa39e;padding:20px 24px;">
                          <div style="font-size:18px;font-weight:700;color:#cf1322;">Kontoschließung bestätigt</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px;">
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">{greeting}</p>
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">
                            Die Kontoschließung für <strong>{tenant}</strong> wurde bestätigt.
                            Die Löschung nicht-fiskalischer Daten ist ab dem <strong>{scheduled}</strong> möglich
                            ({model.ConfirmationWaitDays}-tägige Wartezeit nach Bestätigung).
                          </p>
                          <div style="background:#fff1f0;padding:15px;border-radius:8px;border:1px solid #ffa39e;margin:0 0 16px;">
                            <h3 style="margin:0 0 10px;font-size:15px;color:#cf1322;">Wichtige Hinweise</h3>
                            <ul style="margin:0;padding-left:20px;font-size:14px;line-height:1.6;color:#595959;">
                              <li>Nach dem Purge können Sie sich mit diesem Mandantenkonto nicht mehr normal anmelden</li>
                              <li>Ihre nicht-RKSV-Daten werden unwiderruflich gelöscht</li>
                              <li>{WebUtility.HtmlEncode(rksvLine)}</li>
                            </ul>
                          </div>
                          <p style="margin:0 0 16px;font-size:14px;line-height:1.5;color:#595959;">
                            Wenn Sie diese Aktion nicht autorisiert haben, kontaktieren Sie uns bitte umgehend.
                          </p>
                          <p style="margin:0;font-size:13px;color:#8c8c8c;">
                            Support: {support} · Datenschutz: {privacy}
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

    public static string BuildPlainBody(AccountClosureConfirmationEmailModel model)
    {
        var scheduled = FormatDateLabel(model.ScheduledDateUtc);
        var rksvLine = model.HasRksvData
            ? "Ihre RKSV-Daten bleiben mindestens 7 Jahre gespeichert."
            : "Es liegen keine RKSV-Fiskaldaten vor; nicht-fiskalische Daten werden vollständig gelöscht.";

        return
            $"Liebe/r {model.AdminName},\n\n" +
            $"Die Kontoschließung für {model.TenantName} wurde bestätigt.\n" +
            $"Löschung nicht-fiskalischer Daten möglich ab: {scheduled} " +
            $"({model.ConfirmationWaitDays}-tägige Wartezeit nach Bestätigung).\n\n" +
            "Wichtige Hinweise:\n" +
            "- Nach dem Purge ist die normale Anmeldung für diesen Mandanten nicht mehr möglich\n" +
            "- Nicht-RKSV-Daten werden unwiderruflich gelöscht\n" +
            $"- {rksvLine}\n\n" +
            "Wenn Sie diese Aktion nicht autorisiert haben, kontaktieren Sie uns bitte umgehend.\n\n" +
            $"Support: {model.SupportEmail}\n" +
            $"Datenschutz: {DefaultPrivacyEmail}\n";
    }

    public static string FormatDateLabel(DateTime utc) =>
        utc.ToUniversalTime().ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-AT"));
}
