namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Caller context for restore-drill reads. System dumps stay Super Admin only.
/// </summary>
public sealed record RestoreVerificationAccessScope(bool IsSuperAdmin, Guid? CallerTenantId)
{
    public bool IsDeploymentWide => IsSuperAdmin;
}
