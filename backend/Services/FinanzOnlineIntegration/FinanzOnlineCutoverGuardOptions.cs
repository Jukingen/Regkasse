namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public sealed class FinanzOnlineCutoverGuardOptions
{
    public const string SectionName = "FinanzOnline:CutoverGuard";

    // Default deny for production mode.
    public bool AllowProdMode { get; set; } = false;

    // Additional explicit confirmation switch.
    public bool RequireExplicitProdApproval { get; set; } = true;

    // Optional human-controlled token/checkpoint value for change windows.
    public string ProdApprovalToken { get; set; } = string.Empty;
}
