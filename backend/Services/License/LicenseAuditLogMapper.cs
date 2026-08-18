using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.License;

/// <summary>Maps billing / fiscal audit rows into a unified Super Admin license audit DTO.</summary>
public static partial class LicenseAuditLogMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public const string SourceBilling = "billing";
    public const string SourceAudit = "audit";

    /// <summary>Internal merge row before paging / dedupe.</summary>
    public sealed record Candidate(
        Guid Id,
        DateTime CreatedAtUtc,
        Guid? TenantId,
        string? TenantName,
        string Action,
        string? FromStatus,
        string? ToStatus,
        string? PerformedBy,
        string? Reason,
        string Source);

    public static Candidate FromBillingRow(
        Guid id,
        DateTime timestampUtc,
        Guid? tenantId,
        string? tenantName,
        string action,
        string? detailsJson,
        string? performedBy,
        DateTime? referenceUtc = null)
    {
        var normalized = NormalizeBillingAction(action);
        var reason = SummarizeBillingDetails(detailsJson, action);
        return new Candidate(
            id,
            DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc),
            tenantId,
            tenantName,
            normalized,
            FromStatus: null,
            ToStatus: null,
            performedBy,
            reason,
            SourceBilling);
    }

    public static Candidate FromAuditLogRow(
        Guid id,
        DateTime timestamp,
        Guid? tenantId,
        string? tenantName,
        AuditEventType? actionType,
        string? action,
        string? description,
        string? requestDataJson,
        string? performedBy,
        int gracePeriodDays = 7)
    {
        var normalized = NormalizeAuditAction(actionType, action);
        var reference = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        var (fromStatus, toStatus) = InferStatusesFromRequestData(requestDataJson, reference, gracePeriodDays);
        var reason = SummarizeAuditReason(description, requestDataJson);
        return new Candidate(
            id,
            reference,
            tenantId,
            tenantName,
            normalized,
            fromStatus,
            toStatus,
            performedBy,
            reason,
            SourceAudit);
    }

    public static LicenseAuditLogItemDto ToDto(Candidate row) =>
        new(
            row.Id,
            row.CreatedAtUtc,
            row.TenantId,
            row.TenantName,
            row.Action,
            row.FromStatus,
            row.ToStatus,
            row.PerformedBy,
            row.Reason);

    public static string NormalizeBillingAction(string action) =>
        string.IsNullOrWhiteSpace(action) ? "UNKNOWN" : action.Trim().ToUpperInvariant();

    public static string NormalizeAuditAction(AuditEventType? actionType, string? action)
    {
        if (actionType == AuditEventType.LicenseRenewed
            || string.Equals(action, AuditLogActions.LICENSE_RENEWED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_RENEWED";
        if (actionType == AuditEventType.LicenseExtended
            || string.Equals(action, AuditLogActions.LICENSE_EXTENDED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_EXTENDED";
        if (actionType == AuditEventType.LicenseUpdated
            || string.Equals(action, AuditLogActions.LICENSE_UPDATED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_UPDATED";
        if (actionType == AuditEventType.LicenseRenewalPageViewed
            || string.Equals(action, AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_RENEWAL_PAGE_VIEWED";
        if (actionType == AuditEventType.LicenseActivated
            || string.Equals(action, AuditLogActions.LICENSE_ACTIVATED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_ACTIVATED";
        if (actionType == AuditEventType.LicenseRevoked
            || string.Equals(action, AuditLogActions.LICENSE_REVOKED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_REVOKED";
        if (actionType == AuditEventType.LicenseActivationFailed
            || string.Equals(action, AuditLogActions.LICENSE_ACTIVATION_FAILED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_ACTIVATION_FAILED";
        if (actionType == AuditEventType.LicensePreviewed
            || string.Equals(action, AuditLogActions.LICENSE_PREVIEWED, StringComparison.OrdinalIgnoreCase))
            return "LICENSE_PREVIEWED";
        if (!string.IsNullOrWhiteSpace(action))
            return action.Trim().ToUpperInvariant();
        return "LICENSE_UPDATED";
    }

    /// <summary>
    /// Best-effort lifecycle labels from previous/new expiry timestamps in request JSON.
    /// </summary>
    public static (string? FromStatus, string? ToStatus) InferStatusesFromRequestData(
        string? requestDataJson,
        DateTime referenceUtc,
        int gracePeriodDays = 7)
    {
        if (string.IsNullOrWhiteSpace(requestDataJson))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(requestDataJson);
            var root = doc.RootElement;
            var previous = TryReadDate(root, "PreviousExpiryUtc", "old_valid_until_utc", "previousExpiryUtc");
            var next = TryReadDate(root, "NewExpiryDate", "new_valid_until_utc", "newExpiryDate", "NewExpiryUtc");
            return (
                InferLifecycleStatus(previous, referenceUtc, gracePeriodDays),
                InferLifecycleStatus(next, referenceUtc, gracePeriodDays));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    public static string? InferLifecycleStatus(
        DateTime? expiryUtc,
        DateTime referenceUtc,
        int gracePeriodDays = 7)
    {
        if (expiryUtc is null)
            return null;

        var until = DateTime.SpecifyKind(expiryUtc.Value, DateTimeKind.Utc);
        var days = (until.Date - referenceUtc.Date).TotalDays;
        if (days >= 0)
            return "Active";

        var overdue = -days;
        if (overdue <= Math.Max(1, gracePeriodDays))
            return "Grace";

        return "Expired";
    }

    public static string? SummarizeBillingDetails(string? detailsJson, string action)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("cancellationReason", out var cancel)
                && cancel.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(cancel.GetString()))
            {
                return Truncate(cancel.GetString()!.Trim(), 200);
            }

            if (root.TryGetProperty("recipientEmail", out var email)
                && email.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(email.GetString()))
            {
                var days = root.TryGetProperty("daysBeforeExpiry", out var d) && d.TryGetInt32(out var daysVal)
                    ? daysVal
                    : (int?)null;
                return days is null
                    ? $"Reminder → {email.GetString()}"
                    : $"Reminder ({days}d) → {email.GetString()}";
            }

            if (root.TryGetProperty("invoiceNumber", out var inv)
                && inv.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(inv.GetString()))
            {
                var plan = root.TryGetProperty("licensePlan", out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString()
                    : null;
                return string.IsNullOrWhiteSpace(plan)
                    ? $"Invoice {inv.GetString()}"
                    : $"Invoice {inv.GetString()} ({plan})";
            }

            if (root.TryGetProperty("licenseKey", out var key)
                && key.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(key.GetString()))
            {
                return $"Key {MaskLicenseKey(key.GetString()!)}";
            }
        }
        catch (JsonException)
        {
            return Truncate(MaskSecretsInText(detailsJson.Trim()), 200);
        }

        return Truncate(MaskSecretsInText(detailsJson.Trim()), 200);
    }

    public static string? SummarizeAuditReason(string? description, string? requestDataJson)
    {
        if (!string.IsNullOrWhiteSpace(description))
            return Truncate(description.Trim(), 200);

        if (string.IsNullOrWhiteSpace(requestDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(requestDataJson);
            var root = doc.RootElement;
            var previous = TryReadDate(root, "PreviousExpiryUtc", "old_valid_until_utc");
            var next = TryReadDate(root, "NewExpiryDate", "new_valid_until_utc", "NewExpiryUtc");
            if (previous is not null || next is not null)
            {
                var from = previous?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "—";
                var to = next?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "—";
                return $"Gültig bis {from} → {to}";
            }
        }
        catch (JsonException)
        {
            /* fall through */
        }

        return Truncate(MaskSecretsInText(requestDataJson.Trim()), 200);
    }

    public static string MaskLicenseKey(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length <= 8)
            return trimmed + "…";
        return trimmed[..8] + "…";
    }

    public static string MaskSecretsInText(string text) =>
        RegkKeyRegex().Replace(text, m => MaskLicenseKey(m.Value));

    /// <summary>
    /// Prefer billing rows when an audit row matches the same tenant + action within one minute.
    /// </summary>
    public static IReadOnlyList<Candidate> DeduplicatePreferBilling(IEnumerable<Candidate> rows)
    {
        var ordered = rows.OrderByDescending(r => r.CreatedAtUtc).ToList();
        var billing = ordered.Where(r => r.Source == SourceBilling).ToList();
        var kept = new List<Candidate>(ordered.Count);
        foreach (var row in ordered)
        {
            if (row.Source == SourceAudit
                && billing.Any(k =>
                    k.TenantId == row.TenantId
                    && string.Equals(k.Action, row.Action, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs((k.CreatedAtUtc - row.CreatedAtUtc).TotalMinutes) <= 1))
            {
                continue;
            }

            kept.Add(row);
        }

        return kept;
    }

    private static DateTime? TryReadDate(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.String
                && DateTime.TryParse(
                    el.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                return DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc);
            }

            if (el.ValueKind == JsonValueKind.Null)
                continue;
        }

        return null;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    [GeneratedRegex(@"REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}|REGK-\d{8}-[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*-[A-Z0-9]{8}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RegkKeyRegex();
}
