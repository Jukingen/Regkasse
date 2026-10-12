namespace KasseAPI_Final.Services.Extensions;

/// <summary>Stable hook names stored only as call-site strings. Not fiscal document types.</summary>
public static class TenantExtensionHooks
{
    public const string PaymentBeforeSign = "payment.before-sign";
    public const string PaymentAfterPersist = "payment.after-persist";
    public const string ReceiptBeforePrint = "receipt.before-print";
    public const string CustomerBeforeSave = "customer.before-save";
    public const string FolioBeforeClose = "folio.before-close";
}

public static class TenantExtensionSettings
{
    /// <summary>tenant_settings key. Value is a JSON array of extension names.</summary>
    public const string EnabledKey = "Extensions.Enabled";
}

public static class TenantExtensionNames
{
    public const string HairSalonCampaign = "hair_salon_campaign";
    public const string PrescriptionRequired = "prescription_required";
}

/// <summary>
/// Declares which hooks an <see cref="ITenantFeatureExtension"/> participates in.
/// The registry reads this attribute; core services do not branch on tenant id.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class TenantExtensionHookAttribute : Attribute
{
    public TenantExtensionHookAttribute(string hookName)
    {
        HookName = hookName ?? string.Empty;
    }

    public string HookName { get; }
}
