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

/// <summary>Paged GET /api/admin/tenants/{tenantId}/users body.</summary>
public sealed class TenantUserListPage
{
    public List<TenantUserDto> Items { get; init; } = new();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
