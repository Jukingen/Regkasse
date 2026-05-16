namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Ambient tenant for the current HTTP request or background scope.
/// When <see cref="TenantId"/> is null, <see cref="Data.AppDbContext"/> tenant query filters are disabled.
/// </summary>
public interface ICurrentTenantAccessor
{
    Guid? TenantId { get; set; }
}
