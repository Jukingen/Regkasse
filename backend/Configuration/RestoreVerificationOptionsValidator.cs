using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// İzole restore açıkken yönetim connection adı zorunlu; timeout alt sınırı.
/// </summary>
public sealed class RestoreVerificationOptionsValidator : IValidateOptions<RestoreVerificationOptions>
{
    public ValidateOptionsResult Validate(string? name, RestoreVerificationOptions options)
    {
        var leaseErr = RunLeaseOptionsValidation.Validate(
            options.RunLeaseTimeout,
            options.HeartbeatInterval,
            options.StaleRecoveryScanInterval);
        if (leaseErr != null)
            return ValidateOptionsResult.Fail(leaseErr);

        var multErr = RunLeaseOptionsValidation.ValidateStaleRecoveryNullLeaseGraceMultiplier(
            options.StaleRecoveryNullLeaseGraceMultiplier);
        if (multErr != null)
            return ValidateOptionsResult.Fail(multErr);

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

        if (options.ScheduledProofCadenceDays < 1)
        {
            return ValidateOptionsResult.Fail(
                "RestoreVerification:ScheduledProofCadenceDays must be at least 1.");
        }

        if (options.DumpFallbackDepth is < 1 or > 100)
        {
            return ValidateOptionsResult.Fail(
                "RestoreVerification:DumpFallbackDepth must be between 1 and 100.");
        }

        if (options.ApplicationSmokeProbeEnabled)
        {
            if (string.IsNullOrWhiteSpace(options.ApplicationSmokeProbeBaseUrl))
            {
                return ValidateOptionsResult.Fail(
                    "RestoreVerification:ApplicationSmokeProbeEnabled requires RestoreVerification:ApplicationSmokeProbeBaseUrl (absolute http or https URL).");
            }

            if (!Uri.TryCreate(options.ApplicationSmokeProbeBaseUrl.Trim(), UriKind.Absolute, out var smokeUri)
                || (!string.Equals(smokeUri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(smokeUri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                return ValidateOptionsResult.Fail(
                    "RestoreVerification:ApplicationSmokeProbeBaseUrl must be an absolute http or https URL.");
            }
        }

        if (options.ApplicationSmokeProbeTimeoutSeconds is > 0 and < 5)
        {
            return ValidateOptionsResult.Fail(
                "RestoreVerification:ApplicationSmokeProbeTimeoutSeconds must be at least 5 when set.");
        }

        if (options.RestoredDatabaseApplicationSmokeEnabled && !options.PostRestoreSqlChecksEnabled)
        {
            return ValidateOptionsResult.Fail(
                "RestoreVerification:RestoredDatabaseApplicationSmokeEnabled requires RestoreVerification:PostRestoreSqlChecksEnabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
