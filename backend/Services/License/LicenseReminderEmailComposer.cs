using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.License;

/// <summary>German plain-text bodies for mandant license expiry reminder emails.</summary>
public static class LicenseReminderEmailComposer
{
    public static string BuildMandantExpirySubject(string tenantName, int daysRemaining) =>
        $"Regkasse Lizenz-Erinnerung ({daysRemaining} Tag(e)) - {tenantName}";

    public static string BuildMandantExpiryBody(Tenant tenant, int? daysRemaining, string kind)
    {
        var validUntilLabel = tenant.LicenseValidUntilUtc?.ToString("dd.MM.yyyy") ?? "—";
        var statusLabel = kind switch
        {
            "active" => "Aktiv",
            "grace_write" => "Grace Write",
            "grace_read_only" => "Grace ReadOnly",
            "lockdown" => "Lockdown",
            "no_license" => "Keine Lizenz",
            _ => kind,
        };
        var remainingLabel = daysRemaining.HasValue ? daysRemaining.Value.ToString() : "—";

        return string.Join(Environment.NewLine,
        [
            "Guten Tag,",
            string.Empty,
            $"für den Mandanten \"{tenant.Name}\" läuft Ihre Regkasse-Lizenz in Kürze ab.",
            string.Empty,
            $"Mandant: {tenant.Name}",
            $"Subdomain: {tenant.Slug}",
            $"Lizenzstatus: {statusLabel}",
            $"Gültig bis: {validUntilLabel}",
            $"Verbleibende Tage: {remainingLabel}",
            string.Empty,
            "Bitte prüfen Sie die Lizenzverlängerung im Regkasse Adminbereich.",
        ]);
    }
}
