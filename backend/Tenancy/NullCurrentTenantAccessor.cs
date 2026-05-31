namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Design-time and unit tests: no ambient tenant; tenant-scoped global filters return no rows (fail-closed).
/// </summary>
public sealed class NullCurrentTenantAccessor : ICurrentTenantAccessor
{
    public static readonly NullCurrentTenantAccessor Instance = new();

    public Guid? TenantId { get; set; }
}
