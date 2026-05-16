namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Design-time and unit tests: no ambient tenant; global filters are not applied.
/// </summary>
public sealed class NullCurrentTenantAccessor : ICurrentTenantAccessor
{
    public static readonly NullCurrentTenantAccessor Instance = new();

    public Guid? TenantId { get; set; }
}
