namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Design-time and unit tests: no ambient tenant; tenant-scoped global filters return no rows (fail-closed).
/// The <see cref="Instance"/> singleton always reports null ambient tenant (setter ignored) to avoid
/// cross-test pollution when tests mutate shared accessors.
/// </summary>
public sealed class NullCurrentTenantAccessor : ICurrentTenantAccessor
{
    public static readonly NullCurrentTenantAccessor Instance = new(isSingleton: true);

    private readonly bool _isSingleton;
    private Guid? _tenantId;

    public NullCurrentTenantAccessor()
        : this(isSingleton: false)
    {
    }

    private NullCurrentTenantAccessor(bool isSingleton)
    {
        _isSingleton = isSingleton;
    }

    public Guid? TenantId
    {
        get => _isSingleton ? null : _tenantId;
        set
        {
            if (_isSingleton)
                return;
            _tenantId = value;
        }
    }
}
