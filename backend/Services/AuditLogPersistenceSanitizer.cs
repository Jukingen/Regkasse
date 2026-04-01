using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Services;

/// <summary>
/// AuditLog kolonları (varchar sınırları) ile HTTP/actor alanları arasında güvenli hizalama — SaveChanges hatası önlenir.
/// </summary>
internal static class AuditLogPersistenceSanitizer
{
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
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public static string TruncateUserId(string userId) => Truncate(userId, UserIdMaxLength) ?? userId;

    public static string? TruncateEndpoint(HttpContext? httpContext)
    {
        if (httpContext == null) return null;
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

    /// <summary>JSON serileştirir; kolon taşmasını önlemek için keser (geçersiz JSON sonu kabul edilebilir).</summary>
    public static string? SerializeObjectToJsonColumn(object? value, int maxLength = JsonPayloadMaxLength)
    {
        if (value == null) return null;
        var json = JsonSerializer.Serialize(value);
        return json.Length <= maxLength ? json : json[..maxLength];
    }
}
