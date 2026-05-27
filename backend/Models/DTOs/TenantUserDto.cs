namespace KasseAPI_Final.Models.DTOs;

/// <summary>Tenant-scoped user row for GET /api/admin/tenants/{tenantId}/users.</summary>
public sealed record TenantUserDto(
    string UserId,
    string UserName,
    string Email,
    string Name,
    string Role,
    bool IsOwner,
    DateTime JoinedAtUtc);
