namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Manual restore approval token or state errors (maps to HTTP 400).</summary>
public sealed class ManualRestoreApprovalException : Exception
{
    public const string InvalidTokenCode = "MANUAL_RESTORE_INVALID_TOKEN";
    public const string ExpiredTokenCode = "MANUAL_RESTORE_TOKEN_EXPIRED";

    public ManualRestoreApprovalException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
