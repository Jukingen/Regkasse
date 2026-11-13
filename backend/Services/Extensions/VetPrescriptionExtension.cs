using KasseAPI_Final.Services.VerticalProfiles;

namespace KasseAPI_Final.Services.Extensions;

/// <summary>
/// Rejects a sale when a cart line requires a prescription and the payment has no
/// prescription reference. Applies only when the tenant's effective vertical profile
/// has both patient-record and prescription flags. Does not sign or change the cart.
/// </summary>
[TenantExtensionHook(TenantExtensionHooks.PaymentBeforeSign)]
public sealed class VetPrescriptionExtension : ITenantFeatureExtension
{
    public const string RejectionCode = "VET_PRESCRIPTION_REQUIRED";
    private const string PrescriptionFeature = "prescription";

    private readonly IVerticalProfileService _profiles;

    public VetPrescriptionExtension(IVerticalProfileService profiles)
    {
        _profiles = profiles;
    }

    public string FeatureName => TenantExtensionNames.VetPrescription;

    public bool AppliesTo(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            return false;

        // The registry calls AppliesTo synchronously. The profile service is the
        // effective-profile resolver; this extension does not read company_settings.
        var profile = _profiles
            .GetForAdminTenantAsync(tenantId)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        return profile?.HasPosFeature(VerticalProfileFeatureKeys.PatientRecord) == true
            && profile.HasPosFeature(PrescriptionFeature);
    }

    public Task<ExtensionResult> ExecuteAsync(ExtensionContext context, CancellationToken ct)
    {
        var lines = ExtensionCart.ReadLines(context.Payload);
        if (lines.Count == 0 || !lines.Any(line => line.RequiresPrescription))
            return Task.FromResult(new ExtensionResult(true));

        if (HasPrescriptionReference(context.Payload))
            return Task.FromResult(new ExtensionResult(true));

        return Task.FromResult(new ExtensionResult(false, RejectionCode));
    }

    internal static bool HasPrescriptionReference(IReadOnlyDictionary<string, object?>? payload)
    {
        if (payload is null
            || !payload.TryGetValue(ExtensionCart.PrescriptionReferenceKey, out var raw)
            || raw is not string text)
            return false;

        return !string.IsNullOrWhiteSpace(text);
    }
}
