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
            ActivityEventType.DailyClosingPendingReminder => "Daily closing pending reminder",
            ActivityEventType.OnlineOrderPushedToPos => "Online order pushed to POS",
            ActivityEventType.OnlineOrderPaid => "Online order paid",
            ActivityEventType.OnlineOrderStatusChanged => "Online order status changed",
            ActivityEventType.OnlineOrderConfirmed => "Online order confirmed",
            ActivityEventType.DigitalServiceRequested => "Digital service creation requested",
            ActivityEventType.DataAccessDeleteRequested => "Data deletion request (GDPR)",
            ActivityEventType.DataExportReady => "Data export ready",
            ActivityEventType.RoleCreated => ResolveRoleActivityTitle(metadata, "created"),
            ActivityEventType.RoleDeleted => ResolveRoleActivityTitle(metadata, "deleted"),
            ActivityEventType.RolePermissionsUpdated => ResolveRoleActivityTitle(metadata, "updated"),
            ActivityEventType.UserPermissionOverridesChanged => ResolveOverrideTitle(metadata),
            ActivityEventType.SystemPermissionChange => ResolveRoleActivityTitle(metadata, "system"),
            ActivityEventType.PermissionRequested => "Permission requested",
            ActivityEventType.PermissionRequestApproved => "Permission request approved",
            ActivityEventType.PermissionRequestRejected => "Permission request rejected",
            ActivityEventType.UserPermissionOverrideExpiringSoon => "Temporary permission expiring soon",
            ActivityEventType.UserPermissionOverrideExpired => "Temporary permission expired",
            _ => type.ToString(),
        };

    private static string ResolveRoleActivityTitle(IReadOnlyDictionary<string, object>? metadata, string kind)
    {
        var roleName = metadata == null ? null : TryGetString(metadata, "RoleName");
        var actorName = metadata == null
            ? null
            : TryGetString(metadata, "ActorName") ?? TryGetString(metadata, "ActorEmail");
        var rolePart = string.IsNullOrWhiteSpace(roleName) ? "role" : $"'{roleName}'";
        var who = string.IsNullOrWhiteSpace(actorName) ? "Admin" : actorName;
        return kind switch
        {
            "created" => $"{who} created role {rolePart}",
            "deleted" => $"{who} deleted role {rolePart}",
            "system" => $"{who} made a system permission change on {rolePart}",
            _ => $"{who} updated role {rolePart}",
        };
    }

    private static string ResolveOverrideTitle(IReadOnlyDictionary<string, object>? metadata)
    {
        var permission = metadata == null ? null : TryGetString(metadata, "PermissionKey");
        var actorName = metadata == null
            ? null
            : TryGetString(metadata, "ActorName") ?? TryGetString(metadata, "ActorEmail");
        var who = string.IsNullOrWhiteSpace(actorName) ? "Admin" : actorName;
        if (!string.IsNullOrWhiteSpace(permission))
            return $"{who} changed user permission '{permission}'";
        return $"{who} changed a user permission override";
    }

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
            ActivityEventType.OnlineOrderPushedToPos =>
                TryFormatOnlineOrder(metadata),
            ActivityEventType.OnlineOrderPaid =>
                TryFormatOnlineOrderPaid(metadata),
            ActivityEventType.OnlineOrderStatusChanged =>
                TryFormatOnlineOrderStatusChanged(metadata),
            ActivityEventType.OnlineOrderConfirmed =>
                TryFormatOnlineOrder(metadata),
            ActivityEventType.DigitalServiceRequested =>
                TryGetString(metadata, "Message")
                ?? TryFormatDigitalServiceRequest(metadata),
            ActivityEventType.DataAccessDeleteRequested =>
                TryGetString(metadata, "Message")
                ?? TryGetString(metadata, "Subject"),
            ActivityEventType.DataExportReady =>
                TryGetString(metadata, "Message")
                ?? TryGetString(metadata, "Subject"),
            ActivityEventType.RoleCreated
                or ActivityEventType.RoleDeleted
                or ActivityEventType.RolePermissionsUpdated
                or ActivityEventType.UserPermissionOverridesChanged
                or ActivityEventType.SystemPermissionChange =>
                TryGetString(metadata, "WhatChanged")
                ?? TryGetString(metadata, "Message")
                ?? TryGetString(metadata, "Description"),
            _ => TryGetString(metadata, "ErrorMessage")
                ?? TryGetString(metadata, "Message")
                ?? TryGetString(metadata, "Description"),
        };
    }

    private static string? TryFormatDigitalServiceRequest(IReadOnlyDictionary<string, object> metadata)
    {
        var tenantName = TryGetString(metadata, "TenantName");
        var serviceType = TryGetString(metadata, "ServiceType");
        if (tenantName == null || serviceType == null)
            return null;
        return $"{tenantName} requested {serviceType} creation.";
    }

    private static string? TryFormatOnlineOrderPaid(IReadOnlyDictionary<string, object> metadata)
    {
        var orderNumber = TryGetString(metadata, "OrderNumber");
        var total = TryGetString(metadata, "Total");
        if (orderNumber == null)
            return null;
        return total == null ? orderNumber : $"{orderNumber} — {total}";
    }

    private static string? TryFormatOnlineOrderStatusChanged(IReadOnlyDictionary<string, object> metadata)
    {
        var orderNumber = TryGetString(metadata, "OrderNumber");
        var status = TryGetString(metadata, "OrderStatus") ?? TryGetString(metadata, "NewStatus");
        if (orderNumber == null)
            return null;
        return status == null ? orderNumber : $"{orderNumber} → {status}";
    }

    private static string? TryFormatOnlineOrder(IReadOnlyDictionary<string, object> metadata)
    {
        var orderNumber = TryGetString(metadata, "OrderNumber");
        var customer = TryGetString(metadata, "CustomerName");
        if (orderNumber == null && customer == null)
            return null;
        if (orderNumber != null && customer != null)
            return $"{orderNumber} — {customer}";
        return orderNumber ?? customer;
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
            ActivityEventType.DailyClosingPendingReminder
                => ("cash_register", TryGetString(metadata, "cashRegisterId")),
            ActivityEventType.OnlineOrderPushedToPos
                => ("online_order", TryGetString(metadata, "OnlineOrderId")),
            ActivityEventType.OnlineOrderPaid
                => ("online_order", TryGetString(metadata, "OnlineOrderId")),
            ActivityEventType.OnlineOrderStatusChanged
                => ("online_order", TryGetString(metadata, "OnlineOrderId")),
            ActivityEventType.OnlineOrderConfirmed
                => ("online_order", TryGetString(metadata, "OnlineOrderId")),
            ActivityEventType.DigitalServiceRequested
                => ("digital_service_request", TryGetString(metadata, "RequestId")),
            ActivityEventType.DataAccessDeleteRequested
                => ("tenant_data_rights_request", TryGetString(metadata, "RequestId")),
            ActivityEventType.DataExportReady
                => ("tenant_data_rights_request", TryGetString(metadata, "RequestId")),
            ActivityEventType.RoleCreated
                or ActivityEventType.RoleDeleted
                or ActivityEventType.RolePermissionsUpdated
                or ActivityEventType.SystemPermissionChange
                => ("role", TryGetString(metadata, "RoleName")),
            ActivityEventType.UserPermissionOverridesChanged
                => ("user", TryGetString(metadata, "TargetUserId")),
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
