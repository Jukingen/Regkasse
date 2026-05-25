namespace KasseAPI_Final.Tenancy;

/// <summary>Thrown when a user cannot receive a JWT because their tenant membership targets a disabled tenant.</summary>
public sealed class LoginTenantBlockedException : Exception
{
    public const string CodeTenantDisabled = "TENANT_DISABLED";
    public const string CodeTenantLicenseLockdown = "TENANT_LICENSE_LOCKDOWN";

    public string ErrorCode { get; }

    public LoginTenantBlockedException(string message, string errorCode = CodeTenantDisabled)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
