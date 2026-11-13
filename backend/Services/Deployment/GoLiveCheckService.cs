using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Deployment;

/// <summary>
/// Automated GO / NO-GO for production cutover. Evaluates current config against Production rules
/// (not "is this host currently Production"). Human attestations stay fail-closed via <see cref="GoLiveOptions"/>.
/// </summary>
public sealed class GoLiveCheckService : IGoLiveCheckService
{
    internal const int SystemBackupMaxAgeDays = 7;

    private readonly IConfiguration _configuration;
    private readonly IOptions<TseOptions> _tseOptions;
    private readonly IOptions<FiskalyOptions> _fiskalyOptions;
    private readonly IOptions<BackupOptions> _backupOptions;
    private readonly IOptions<MonitoringOptions> _monitoringOptions;
    private readonly IOptions<RksvFinanzOnlineSubmissionClientOptions> _fonSubmissionOptions;
    private readonly IOptions<GoLiveOptions> _goLiveOptions;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IGoLiveStatusStore _store;
    private readonly ILogger<GoLiveCheckService> _logger;

    public GoLiveCheckService(
        IConfiguration configuration,
        IOptions<TseOptions> tseOptions,
        IOptions<FiskalyOptions> fiskalyOptions,
        IOptions<BackupOptions> backupOptions,
        IOptions<MonitoringOptions> monitoringOptions,
        IOptions<RksvFinanzOnlineSubmissionClientOptions> fonSubmissionOptions,
        IOptions<GoLiveOptions> goLiveOptions,
        IDbContextFactory<AppDbContext> dbFactory,
        IGoLiveStatusStore store,
        ILogger<GoLiveCheckService> logger)
    {
        _configuration = configuration;
        _tseOptions = tseOptions;
        _fiskalyOptions = fiskalyOptions;
        _backupOptions = backupOptions;
        _monitoringOptions = monitoringOptions;
        _fonSubmissionOptions = fonSubmissionOptions;
        _goLiveOptions = goLiveOptions;
        _dbFactory = dbFactory;
        _store = store;
        _logger = logger;
    }

    public async Task<GoLiveStatusDto> CheckAllConditionsAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<GoLiveCheckDto>
        {
            CheckFiskaly(),
            CheckConfiguration(),
            CheckFon(),
            await CheckBackupAsync(cancellationToken).ConfigureAwait(false),
            CheckMonitoring(),
            await CheckSignOffAsync(cancellationToken).ConfigureAwait(false),
        };

        var failed = checks.Count(c => !c.Passed);
        var allPassed = failed == 0;
        var status = new GoLiveStatusDto
        {
            Status = allPassed ? GoLiveStatusDto.StatusGo : GoLiveStatusDto.StatusNoGo,
            Checks = checks,
            CheckedAtUtc = DateTime.UtcNow,
            Summary = allPassed
                ? "All conditions met. Ready for production."
                : $"Missing {failed} conditions.",
        };

        _store.Save(status);
        _logger.LogInformation(
            "Go-live check completed: status={Status} failed={Failed} of {Total}",
            status.Status,
            failed,
            checks.Count);
        return status;
    }

    public async Task<GoLiveStatusDto> GetLatestStatusAsync(CancellationToken cancellationToken = default)
    {
        var latest = _store.GetLatest();
        if (latest is not null)
            return latest;

        return await CheckAllConditionsAsync(cancellationToken).ConfigureAwait(false);
    }

    internal GoLiveCheckDto CheckFiskaly()
    {
        var tse = _tseOptions.Value;
        var fiskaly = _fiskalyOptions.Value;
        var failures = new List<string>();

        if (tse.AllowUnsafeFiscalModesInProduction)
            failures.Add("Tse:AllowUnsafeFiscalModesInProduction is true (production lock bypassed).");

        var fiscalReasons = TseFiscalConfigLockEvaluator.CollectViolations(_configuration, tse)
            .Where(r => r != TseFiscalConfigLockEvaluator.ReasonFinanzOnlineSimulation)
            .ToList();
        failures.AddRange(fiscalReasons);

        var provider = TseOptions.NormalizeProviderName(tse.Provider);
        if (!string.Equals(provider, TseOptions.ProviderFiskaly, StringComparison.OrdinalIgnoreCase))
            failures.Add("Tse:Provider must be fiskaly for this Fiskaly LIVE gate.");

        if (!fiskaly.Enabled)
            failures.Add("Fiskaly:Enabled is false.");
        if (!fiskaly.HasApiCredentials)
            failures.Add("Fiskaly:ApiKey / Fiskaly:ApiSecret are not configured.");
        if (string.IsNullOrWhiteSpace(fiskaly.SignatureCreationUnitId))
            failures.Add("Fiskaly SCU id (Fiskaly:ScuId / SignatureCreationUnitId) is missing.");
        if (!string.Equals(fiskaly.ResolveEnvironment(), FiskalyOptions.LiveEnvironment, StringComparison.OrdinalIgnoreCase))
            failures.Add("Fiskaly:Environment must be LIVE (not TEST).");

        if (failures.Count == 0)
        {
            return Passed(
                GoLiveCheckDto.NameFiskaly,
                GoLiveCheckDto.CategoryFiskaly,
                "TSE Device/Real, provider fiskaly, LIVE SCU credentials present.");
        }

        return Failed(
            GoLiveCheckDto.NameFiskaly,
            GoLiveCheckDto.CategoryFiskaly,
            string.Join(" ", failures),
            "Set Tse:TseMode=Device, Tse:Mode=Real, Tse:Provider=fiskaly; Fiskaly LIVE API keys + SCU in the secret store. See docs/FISKALY_PRODUCTION_CUTOVER.md.");
    }

    internal GoLiveCheckDto CheckConfiguration()
    {
        var errors = ProductionRuntimeConfigurationGuard.CollectViolations(_configuration)
            .Where(e => e != ProductionRuntimeConfigurationGuard.FonSimulationNotAllowed)
            .Where(e => e != ProductionRuntimeConfigurationGuard.BackupMustUsePgDump)
            .ToList();

        if (errors.Count == 0)
        {
            return Passed(
                GoLiveCheckDto.NameConfiguration,
                GoLiveCheckDto.CategoryConfig,
                "CSRF, SuperAdmin 2FA, rate limiting, Redis, and payment gateway meet Production lock.");
        }

        return Failed(
            GoLiveCheckDto.NameConfiguration,
            GoLiveCheckDto.CategoryConfig,
            string.Join(" ", errors),
            "Enable CSRF, TwoFactorAuth, RateLimiting, Redis (with connection string), and a non-Mock PaymentGateway. See ProductionRuntimeConfigurationGuard.");
    }

    internal GoLiveCheckDto CheckFon()
    {
        var failures = new List<string>();
        if (TseFiscalConfigLockEvaluator.IsFinanzOnlineSimulated(_configuration))
            failures.Add(TseFiscalConfigLockEvaluator.ReasonFinanzOnlineSimulation);

        var fon = _fonSubmissionOptions.Value;
        if (fon.ClientKind != RksvFinanzOnlineSubmissionClientKind.Real)
            failures.Add($"FinanzOnline:RksvSubmission:ClientKind is {fon.ClientKind} (must be Real).");
        if (fon.AllowFakeClientInProduction)
            failures.Add("FinanzOnline:RksvSubmission:AllowFakeClientInProduction is true.");

        if (failures.Count == 0)
        {
            return Passed(
                GoLiveCheckDto.NameFon,
                GoLiveCheckDto.CategoryFon,
                "FinanzOnline is not in Simulation and RKSV submission ClientKind=Real.");
        }

        return Failed(
            GoLiveCheckDto.NameFon,
            GoLiveCheckDto.CategoryFon,
            string.Join(" ", failures),
            "Set FinanzOnline:Session/Registrierkassen/TransmissionQuery UseSimulation=false, FinanzOnline:Mode=Production, RksvSubmission:ClientKind=Real. See docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md.");
    }

    internal async Task<GoLiveCheckDto> CheckBackupAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var backup = _backupOptions.Value;
        if (backup.ExecutionAdapterKind != BackupExecutionAdapterKind.PgDump)
            failures.Add($"Backup:ExecutionAdapterKind is {backup.ExecutionAdapterKind} (must be PgDump).");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var cutoff = DateTime.UtcNow.AddDays(-SystemBackupMaxAgeDays);

        var latestSystem = await db.BackupRuns.AsNoTracking()
            .Where(r => r.Strategy == BackupStrategyKind.System && r.Status == BackupRunStatus.Succeeded)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (latestSystem is null)
        {
            failures.Add("No Succeeded System backup run found.");
        }
        else
        {
            var when = latestSystem.CompletedAt ?? latestSystem.RequestedAt;
            if (when < cutoff)
            {
                failures.Add(
                    $"Latest Succeeded System backup is older than {SystemBackupMaxAgeDays} days ({when:O}).");
            }
        }

        var restorePassed = await db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Succeeded)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (restorePassed is null)
            failures.Add("No Succeeded restore-drill (RestoreVerificationRun) found.");

        if (failures.Count == 0)
        {
            var backupWhen = latestSystem!.CompletedAt ?? latestSystem.RequestedAt;
            var drillWhen = restorePassed!.CompletedAt ?? restorePassed.RequestedAt;
            return Passed(
                GoLiveCheckDto.NameBackup,
                GoLiveCheckDto.CategoryBackup,
                $"PgDump adapter; System backup Succeeded at {backupWhen:O}; restore drill Succeeded at {drillWhen:O}.");
        }

        return Failed(
            GoLiveCheckDto.NameBackup,
            GoLiveCheckDto.CategoryBackup,
            string.Join(" ", failures),
            "Set Backup:ExecutionAdapterKind=PgDump, run a System backup, then an isolated restore drill. See docs/BACKUP_RESTORE_DRILL_EVIDENCE.md.");
    }

    internal GoLiveCheckDto CheckMonitoring()
    {
        var failures = new List<string>();
        var monitoring = _monitoringOptions.Value;
        if (!monitoring.Enabled)
            failures.Add("Monitoring:Enabled is false.");
        if (!monitoring.Prometheus.Enabled)
            failures.Add("Monitoring:Prometheus:Enabled is false.");

        var goLive = _goLiveOptions.Value;
        if (!goLive.AlertmanagerReceiversConfigured)
            failures.Add("GoLive:AlertmanagerReceiversConfigured is false (host receivers + routing test not attested).");

        if (failures.Count == 0)
        {
            return Passed(
                GoLiveCheckDto.NameMonitoring,
                GoLiveCheckDto.CategoryMonitoring,
                "API metrics enabled; Alertmanager receivers attested on the host.");
        }

        return Failed(
            GoLiveCheckDto.NameMonitoring,
            GoLiveCheckDto.CategoryMonitoring,
            string.Join(" ", failures),
            "Enable Monitoring + Prometheus scrape. Render Alertmanager receivers on the host, fire a test alert, then set GoLive:AlertmanagerReceiversConfigured=true. See docs/ALERTING.md.");
    }

    internal async Task<GoLiveCheckDto> CheckSignOffAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var goLive = _goLiveOptions.Value;
        if (!goLive.Section8Signed)
            failures.Add("GoLive:Section8Signed is false (GO_LIVE_CHECKLIST.md §8 unsigned).");
        if (!goLive.AvvSignedForPilots)
            failures.Add("GoLive:AvvSignedForPilots is false.");
        if (!goLive.OnCallNamed)
            failures.Add("GoLive:OnCallNamed is false.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var signoff = await db.DeploymentComplianceSignoffs.AsNoTracking()
            .Where(s => s.Stage == "production")
            .Where(s => s.ExpiresAtUtc == null || s.ExpiresAtUtc > now)
            .OrderByDescending(s => s.SignedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (signoff is null)
            failures.Add("No unexpired production DeploymentComplianceSignoff found.");

        if (failures.Count == 0)
        {
            return Passed(
                GoLiveCheckDto.NameSignOff,
                GoLiveCheckDto.CategorySignOff,
                $"§8 / AVV / on-call attested; compliance sign-off {signoff!.ImageTag} by {signoff.SignedByDisplayName ?? signoff.SignedByUserId} at {signoff.SignedAtUtc:O}.");
        }

        return Failed(
            GoLiveCheckDto.NameSignOff,
            GoLiveCheckDto.CategorySignOff,
            string.Join(" ", failures),
            "Named humans must sign docs/GO_LIVE_CHECKLIST.md §8 and docs/GO_LIVE_SIGN_OFF_PACKET.md. Record image sign-off at /admin/deployments/compliance. Then set GoLive:Section8Signed, AvvSignedForPilots, and OnCallNamed only after that evidence exists.");
    }

    private static GoLiveCheckDto Passed(string name, string category, string details) => new()
    {
        Name = name,
        Category = category,
        Passed = true,
        Details = details,
        Remediation = string.Empty,
    };

    private static GoLiveCheckDto Failed(string name, string category, string details, string remediation) => new()
    {
        Name = name,
        Category = category,
        Passed = false,
        Details = details,
        Remediation = remediation,
    };
}
