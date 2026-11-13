using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.Enums;

/// <summary>
/// Mandant lifecycle status. Stored as lowercase string on <c>tenants.status</c>
/// (see <see cref="TenantStatuses"/>). Legacy <c>deleted</c> maps to <see cref="Archived"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TenantStatus
{
    Lead = 0,
    InOnboarding = 1,
    Active = 2,
    Suspended = 3,
    Cancelled = 4,
    Archived = 5,
}
