namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Current mandant row bound on <see cref="Tenancy.ICurrentTenantAccessor"/> (FA tenant context provider).</summary>
public sealed class CurrentTenantDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? LicenseValidUntilUtc { get; init; }
    public bool LicenseValid { get; init; }
}
