using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Fails startup (ValidateOnStart) when configuration is unsafe for the current environment.
/// </summary>
public sealed class BackupOptionsValidator : IValidateOptions<BackupOptions>
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public BackupOptionsValidator(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, BackupOptions options)
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

        var retentionErr = BackupRetentionOptionsValidation.Validate(options);
        if (retentionErr != null)
            return ValidateOptionsResult.Fail(retentionErr);

        var snap = BackupConfigurationEvaluation.Evaluate(options, _environment, _configuration);
        if (snap.Level == BackupConfigurationHealthLevel.Unhealthy)
            return ValidateOptionsResult.Fail(string.Join(" ", snap.Issues));
        return ValidateOptionsResult.Success;
    }
}
