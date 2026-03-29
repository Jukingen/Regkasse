using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// İzole restore açıkken yönetim connection adı zorunlu; timeout alt sınırı.
/// </summary>
public sealed class RestoreVerificationOptionsValidator : IValidateOptions<RestoreVerificationOptions>
{
    public ValidateOptionsResult Validate(string? name, RestoreVerificationOptions options)
    {
        if (options.IsolatedPgRestoreEnabled
            && string.IsNullOrWhiteSpace(options.IsolatedRestoreAdminConnectionStringName))
        {
            return ValidateOptionsResult.Fail(
                "RestoreVerification:IsolatedPgRestoreEnabled requires RestoreVerification:IsolatedRestoreAdminConnectionStringName.");
        }

        if (options.IsolatedPgRestoreTimeoutSeconds is > 0 and < 60)
        {
            return ValidateOptionsResult.Fail(
                "RestoreVerification:IsolatedPgRestoreTimeoutSeconds must be at least 60 when set.");
        }

        return ValidateOptionsResult.Success;
    }
}
