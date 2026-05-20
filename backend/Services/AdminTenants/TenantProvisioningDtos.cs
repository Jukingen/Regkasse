namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>One-time provisioning details returned when a tenant is created.</summary>
public sealed record TenantProvisioningDto(
    Guid CashRegisterId,
    string CashRegisterNumber,
    string AdminUserId,
    string AdminEmail,
    string GeneratedPassword,
    Guid CategoryId,
    IReadOnlyList<Guid> ProductIds,
    DateTime? TrialLicenseValidUntilUtc);

public sealed class TenantProvisioningResult
{
    public required Guid CashRegisterId { get; init; }
    public required string CashRegisterNumber { get; init; }
    public required string AdminUserId { get; init; }
    public required string AdminEmail { get; init; }
    public required string GeneratedPassword { get; init; }
    public required Guid CategoryId { get; init; }
    public required IReadOnlyList<Guid> ProductIds { get; init; }
    public DateTime? TrialLicenseValidUntilUtc { get; init; }

    public TenantProvisioningDto ToDto() =>
        new(
            CashRegisterId,
            CashRegisterNumber,
            AdminUserId,
            AdminEmail,
            GeneratedPassword,
            CategoryId,
            ProductIds,
            TrialLicenseValidUntilUtc);
}
