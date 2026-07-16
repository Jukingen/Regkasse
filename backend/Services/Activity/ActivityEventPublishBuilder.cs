using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Activity;

/// <summary>Builds <see cref="ActivityEventPublishRequest"/> from event type and metadata payloads.</summary>
internal static class ActivityEventPublishBuilder
{
    public static ActivityEventPublishRequest FromMetadata(
        Guid tenantId,
        ActivityEventType type,
        object? metadata,
        string? actorUserId = null,
        string? dedupKey = null)
    {
        var dict = MetadataToDictionary(metadata);
        var title = ResolveTitle(type, dict);
        var description = ResolveDescription(type, dict);
        var (entityType, entityId) = ResolveEntity(type, dict);

        return new ActivityEventPublishRequest(
            tenantId,
            type,
            title,
            Description: description,
            DedupKey: dedupKey,
            ActorUserId: actorUserId ?? TryGetString(dict, "ActorId"),
            EntityType: entityType,
            EntityId: entityId,
            Metadata: dict);
    }

    public static IReadOnlyDictionary<string, object>? MetadataToDictionary(object? metadata)
    {
        if (metadata == null)
            return null;

        if (metadata is IReadOnlyDictionary<string, object> readOnly)
            return readOnly;

        if (metadata is Dictionary<string, object> dict)
            return dict;

        var element = JsonSerializer.SerializeToElement(metadata);
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
            result[prop.Name] = JsonElementToObject(prop.Value);

        return result;
    }

    private static object JsonElementToObject(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number when value.TryGetInt64(out var l) => l,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => "",
            _ => value.GetRawText(),
        };

    private static string ResolveTitle(ActivityEventType type, IReadOnlyDictionary<string, object>? metadata) =>
        type switch
        {
            ActivityEventType.UserCreated => "User created",
            ActivityEventType.UserUpdated => "User updated",
            ActivityEventType.UserDeleted => "User deleted",
            ActivityEventType.CashRegisterOpened => "Cash register opened",
            ActivityEventType.CashRegisterClosed => "Cash register closed",
            ActivityEventType.CashRegisterDecommissioned => "Cash register decommissioned",
            ActivityEventType.LicenseExpiringSoon => ResolveLicenseExpiringTitle(metadata),
            ActivityEventType.LicenseExpired => "License expired",
            ActivityEventType.OfflineQueueGrowing => "Offline queue growing",
            ActivityEventType.OfflineOrdersBacklogGrowing => "Offline orders backlog growing",
            ActivityEventType.OfflineOrdersExpiringSoon => "Offline orders expiring soon",
            ActivityEventType.OfflineSyncStalled => "Offline sync stalled",
            ActivityEventType.FinanzOnlineSubmissionFailed => "FinanzOnline submission failed",
            ActivityEventType.BackupFailed => "Backup failed",
            ActivityEventType.BackupSucceeded => "Backup succeeded",
            ActivityEventType.RestoreDrillFailed => "Restore drill failed",
            ActivityEventType.RestoreDrillSucceeded => "Restore drill succeeded",
            ActivityEventType.DailyClosingBackdatedCreated => "Backdated daily closing created",
            _ => type.ToString(),
        };

    private static string ResolveLicenseExpiringTitle(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata != null && TryGetInt(metadata, "DaysRemaining", out var days))
            return $"License expires in {days} day(s)";
        return "License expiring soon";
    }

    private static string? ResolveDescription(ActivityEventType type, IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata == null)
            return null;

        return type switch
        {
            ActivityEventType.UserCreated =>
                TryFormatUserCreated(metadata),
            ActivityEventType.BackupFailed or ActivityEventType.BackupSucceeded =>
                TryFormatBackup(metadata),
            ActivityEventType.LicenseExpiringSoon or ActivityEventType.LicenseExpired =>
                TryFormatLicense(metadata),
            _ => TryGetString(metadata, "ErrorMessage")
                ?? TryGetString(metadata, "Message")
                ?? TryGetString(metadata, "Description"),
        };
    }

    private static string? TryFormatUserCreated(IReadOnlyDictionary<string, object> metadata)
    {
        var email = TryGetString(metadata, "UserEmail");
        var role = TryGetString(metadata, "Role");
        if (email == null && role == null)
            return null;
        return role == null ? email : $"{email} ({role})";
    }

    private static string? TryFormatBackup(IReadOnlyDictionary<string, object> metadata)
    {
        var error = TryGetString(metadata, "ErrorMessage");
        if (!string.IsNullOrWhiteSpace(error))
            return error;

        if (TryGetInt(metadata, "DurationSeconds", out var seconds)
            && TryGetLong(metadata, "ArtifactSize", out var size))
            return $"Completed in {seconds}s, artifact size {size} bytes.";

        if (TryGetInt(metadata, "DurationSeconds", out seconds))
            return $"Completed in {seconds}s.";

        return null;
    }

    private static string? TryFormatLicense(IReadOnlyDictionary<string, object> metadata)
    {
        if (TryGetString(metadata, "ExpiryDate") is { } expiry)
            return $"Expiry: {expiry}";
        return null;
    }

    private static (string? EntityType, string? EntityId) ResolveEntity(
        ActivityEventType type,
        IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata == null)
            return (null, null);

        return type switch
        {
            ActivityEventType.UserCreated or ActivityEventType.UserUpdated or ActivityEventType.UserDeleted
                => ("user", TryGetString(metadata, "UserId")),
            ActivityEventType.BackupFailed or ActivityEventType.BackupSucceeded
                => ("backup_run", TryGetString(metadata, "BackupRunId")),
            ActivityEventType.CashRegisterOpened
                or ActivityEventType.CashRegisterClosed
                or ActivityEventType.CashRegisterDecommissioned
                => ("cash_register", TryGetString(metadata, "CashRegisterId")),
            ActivityEventType.DailyClosingBackdatedCreated
                => ("DailyClosing", TryGetString(metadata, "ClosingId")),
            _ => (null, null),
        };
    }

    private static string? TryGetString(IReadOnlyDictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var raw) || raw == null)
            return null;
        return raw.ToString();
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, object> metadata, string key, out int value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out var raw) || raw == null)
            return false;
        return int.TryParse(raw.ToString(), out value);
    }

    private static bool TryGetLong(IReadOnlyDictionary<string, object> metadata, string key, out long value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out var raw) || raw == null)
            return false;
        return long.TryParse(raw.ToString(), out value);
    }
}
