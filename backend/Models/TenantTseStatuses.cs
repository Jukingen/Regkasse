namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant-level Fiskaly / TSE provisioning status stored in <see cref="Tenant.TseStatus"/>.
/// SIGN AT maps German TSS → SCU; do not invent a second TSS id family.
/// </summary>
public static class TenantTseStatuses
{
    /// <summary>Fiskaly SIGN AT SCU was created and stamped on the tenant.</summary>
    public const string Active = "active";

    /// <summary>Fiskaly ensure failed after retries; Soft TSE is operational for this tenant.</summary>
    public const string SoftFallback = "soft_fallback";

    /// <summary>Soft TSE provisioned without a Fiskaly attempt (Demo / UseSoftTseWhenNoDevice).</summary>
    public const string Soft = "soft";

    /// <summary>Fake / demo signing device (TseMode=Demo, Mode=Fake).</summary>
    public const string Fake = "fake";

    /// <summary>Device-mode placeholder; operator must wire hardware or Fiskaly credentials later.</summary>
    public const string Pending = "pending";

    /// <summary>TSE auto-provision skipped (TseMode=Off or AutoProvisionOnTenantCreate=false).</summary>
    public const string Skipped = "skipped";
}
