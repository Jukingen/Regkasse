namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
public sealed class CurrentTenantAccessor : ICurrentTenantAccessor
{
    /// <inheritdoc />
    public Guid? TenantId { get; set; }
}
