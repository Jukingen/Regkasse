using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Whitelist and builder for user lifecycle audit diff. Invariant 4: Sensitive data must never be logged.
    /// AllowedKeys include all editable user fields (name, email, role, TaxNumber, Notes, EmployeeNumber, etc.) for "Änderungen ansehen".
    /// Never log: password, tokens, credentials, security stamps (see ForbiddenKeys).
    /// </summary>
    public static class UserAuditDiffHelper
    {
        /// <summary>
        /// Allowed property names for user audit diff. Includes all editable user fields for "Änderungen ansehen".
        /// Never log: Password, security stamps, tokens (see ForbiddenKeys).
        /// </summary>
        public static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(new[]
        {
            "FirstName", "LastName", "Email", "UserName", "Role", "IsActive", "IsDemo",
            "TaxNumber", "Notes", "EmployeeNumber"
        }, System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Field names that must never appear in audit logs (credentials, tokens, security stamps only).
        /// TaxNumber, Notes, EmployeeNumber are allowed in diff for audit trail of user edits.
        /// </summary>
        public static readonly IReadOnlySet<string> ForbiddenKeys = new HashSet<string>(new[]
        {
            "password", "PasswordHash", "NormalizedUserName", "NormalizedEmail",
            "SecurityStamp", "ConcurrencyStamp",
            "token", "access_token", "refresh_token", "credentials", "apiKey"
        }, System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Builds a snapshot object with only whitelisted properties for audit OldValues/NewValues.
        /// Safe for serialization into AuditLog; no credentials or sensitive identifiers.
        /// </summary>
        public static object CreateSafeSnapshot(ApplicationUser user)
        {
            if (user == null) return new { };
            return new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.UserName,
                user.Role,
                user.IsActive,
                user.IsDemo,
                user.TaxNumber,
                user.Notes,
                user.EmployeeNumber
            };
        }

        /// <summary>
        /// Builds structured diff list for AuditLog.Changes. Compares old and new snapshots using only AllowedKeys.
        /// Returns list of { field, oldValue, newValue } for changed fields. Never includes sensitive data.
        /// </summary>
        public static List<AuditChangeItem> BuildStructuredChanges(object? oldValues, object? newValues)
        {
            var changes = new List<AuditChangeItem>();
            var oldDict = ToStringDictionary(oldValues);
            var newDict = ToStringDictionary(newValues);
            var keys = AllowedKeys.Where(k => oldDict.ContainsKey(k) || newDict.ContainsKey(k)).ToList();
            foreach (var key in keys)
            {
                if (ForbiddenKeys.Contains(key)) continue;
                var oldVal = oldDict.TryGetValue(key, out var o) ? o : null;
                var newVal = newDict.TryGetValue(key, out var n) ? n : null;
                if (Equals(oldVal, newVal)) continue;
                changes.Add(new AuditChangeItem
                {
                    Field = key,
                    OldValue = oldVal,
                    NewValue = newVal
                });
            }
            return changes;
        }

        private static Dictionary<string, string?> ToStringDictionary(object? obj)
        {
            if (obj == null) return new Dictionary<string, string?>();
            try
            {
                var json = JsonSerializer.Serialize(obj);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict == null) return new Dictionary<string, string?>();
                return dict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ValueKind == JsonValueKind.Null || kvp.Value.ValueKind == JsonValueKind.Undefined
                        ? null
                        : kvp.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string?>();
            }
        }

        private static bool Equals(string? a, string? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return string.Equals(a, b, System.StringComparison.Ordinal);
        }
    }
}
