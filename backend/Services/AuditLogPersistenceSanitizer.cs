using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services;

/// <summary>
/// AuditLog column alignment: truncates to varchar limits and redacts sensitive JSON property names
/// before persistence. Truncation alone is not enough for secrets — callers should still avoid
/// passing passwords / voucher codes; this layer is defense-in-depth.
/// </summary>
internal static class AuditLogPersistenceSanitizer
{
    public const string RedactedPlaceholder = "***REDACTED***";

    /// <summary>
    /// Property names scrubbed from serialized audit JSON (case-insensitive).
    /// Keep in sync with <see cref="UserAuditDiffHelper.ForbiddenKeys"/> intent.
    /// </summary>
    private static readonly HashSet<string> SensitiveJsonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "PasswordHash",
        "passwordHash",
        "newPassword",
        "oldPassword",
        "confirmPassword",
        "generatedPassword",
        "token",
        "access_token",
        "refresh_token",
        "accessToken",
        "refreshToken",
        "SecurityStamp",
        "ConcurrencyStamp",
        "credentials",
        "apiKey",
        "api_key",
        "voucherCode",
        "VoucherCode",
        "taxNumber",
        "TaxNumber",
    };

    public const int EndpointMaxLength = 100;

    /// <summary>Aligned with <c>audit_logs.user_id</c> (AspNetUsers Id length).</summary>
    public const int UserIdMaxLength = 450;

    public const int CorrelationIdMaxLength = 100;

    public const int EntityNameMaxLength = 100;

    public const int TransactionIdMaxLength = 100;

    /// <summary>Matches <see cref="Models.AuditLog.Action"/> MaxLength.</summary>
    public const int ActionMaxLength = 50;

    /// <summary>Matches <see cref="Models.AuditLog.EntityType"/> MaxLength.</summary>
    public const int EntityTypeMaxLength = 100;

    /// <summary>Matches <see cref="Models.AuditLog.UserRole"/> MaxLength.</summary>
    public const int UserRoleMaxLength = 50;

    /// <summary>Matches <see cref="Models.AuditLog.Description"/> MaxLength.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Matches <see cref="Models.AuditLog.Notes"/> MaxLength.</summary>
    public const int NotesMaxLength = 500;

    /// <summary>Matches <see cref="Models.AuditLog.ErrorDetails"/> MaxLength.</summary>
    public const int ErrorDetailsMaxLength = 500;

    /// <summary>Matches <see cref="Models.AuditLog.RequestData"/> / ResponseData / OldValues / NewValues MaxLength.</summary>
    public const int JsonPayloadMaxLength = 4000;

    /// <summary>Matches <see cref="Models.AuditLog.IpAddress"/> MaxLength (IPv6).</summary>
    public const int IpAddressMaxLength = 45;

    public static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public static string TruncateUserId(string userId) => Truncate(userId, UserIdMaxLength) ?? userId;

    public static string? TruncateEndpoint(HttpContext? httpContext)
    {
        if (httpContext == null)
            return null;
        return Truncate(httpContext.Request.Path.Value, EndpointMaxLength);
    }

    public static string TruncateForAction(string action)
    {
        var a = string.IsNullOrWhiteSpace(action) ? "UNKNOWN" : action.Trim();
        return Truncate(a, ActionMaxLength) ?? a;
    }

    public static string TruncateForEntityType(string entityType)
    {
        var e = string.IsNullOrWhiteSpace(entityType) ? "UNKNOWN" : entityType.Trim();
        return Truncate(e, EntityTypeMaxLength) ?? e;
    }

    public static string TruncateForUserRole(string userRole)
    {
        var r = string.IsNullOrWhiteSpace(userRole) ? "Unknown" : userRole.Trim();
        return Truncate(r, UserRoleMaxLength) ?? r;
    }

    /// <summary>
    /// Serializes <paramref name="value"/> to JSON, redacts sensitive property names, then truncates
    /// to the column max length (truncated JSON may be syntactically incomplete — acceptable for audit storage).
    /// </summary>
    public static string? SerializeObjectToJsonColumn(object? value, int maxLength = JsonPayloadMaxLength)
    {
        if (value == null)
            return null;
        try
        {
            var node = JsonSerializer.SerializeToNode(value);
            RedactSensitiveProperties(node);
            var json = node?.ToJsonString() ?? "{}";
            return json.Length <= maxLength ? json : json[..maxLength];
        }
        catch (JsonException)
        {
            var fallback = JsonSerializer.Serialize(value);
            return fallback.Length <= maxLength ? fallback : fallback[..maxLength];
        }
    }

    public static bool IsSensitiveJsonKey(string? key) =>
        !string.IsNullOrEmpty(key) && SensitiveJsonKeys.Contains(key);

    private static void RedactSensitiveProperties(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                {
                    foreach (var prop in obj.ToList())
                    {
                        if (IsSensitiveJsonKey(prop.Key))
                            obj[prop.Key] = RedactedPlaceholder;
                        else
                            RedactSensitiveProperties(prop.Value);
                    }

                    break;
                }
            case JsonArray arr:
                {
                    foreach (var item in arr)
                        RedactSensitiveProperties(item);
                    break;
                }
        }
    }
}
