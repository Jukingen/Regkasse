using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// L6 harici bağımlılık iskeleti: yapılandırma sinyalleri ve alan bazlı durum; canlı TSE/FinanzOnline/API kanıtı üretmez.
/// </summary>
public sealed class ExternalDependencyRecoveryEvidenceBuilder : IExternalDependencyRecoveryEvidenceBuilder
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<TseOptions> _tse;
    private readonly IOptionsMonitor<BackupOptions> _backup;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreVerification;
    private readonly IHostEnvironment _hostEnvironment;

    public ExternalDependencyRecoveryEvidenceBuilder(
        IConfiguration configuration,
        IOptionsMonitor<TseOptions> tseOptions,
        IOptionsMonitor<BackupOptions> backupOptions,
        IOptionsMonitor<RestoreVerificationOptions> restoreVerificationOptions,
        IHostEnvironment hostEnvironment)
    {
        _configuration = configuration;
        _tse = tseOptions;
        _backup = backupOptions;
        _restoreVerification = restoreVerificationOptions;
        _hostEnvironment = hostEnvironment;
    }

    public ExternalDependencyRecoveryEvidenceBlock Build()
    {
        var domains = new List<ExternalDependencyDomainEvidence>(5);
        var flatChecks = new List<ExternalDependencyCheckRow>(16);

        var tseDomain = BuildTseDomain(flatChecks);
        domains.Add(tseDomain);

        var secretsDomain = BuildSecretsDomain(flatChecks);
        domains.Add(secretsDomain);

        var toolingDomain = BuildBackupToolingDomain(flatChecks);
        domains.Add(toolingDomain);

        var archiveDomain = BuildArchiveDomain(flatChecks);
        domains.Add(archiveDomain);

        var foDomain = BuildFinanzOnlineDomain(flatChecks);
        domains.Add(foDomain);

        const string rollupNotes =
            "L6 skeleton: configuration and path signals only; does not prove TSE hardware, FinanzOnline reachability, archive immutability, or secret correctness at runtime.";

        var rollup = ExternalDependencyRecoveryRollupCalculator.Compute(domains, rollupNotes);
        var legacyOutcome = ExternalDependencyProofBandMapper.ToLegacyOverallOutcome(rollup);

        var interpretation =
            "configuration_snapshot_and_domain_states; not a substitute for live vendor/API/hardware validation; see domains[].state and rollup.overallState";

        return new ExternalDependencyRecoveryEvidenceBlock
        {
            L6EvidenceSchemaVersion = 1,
            Rollup = rollup,
            Domains = domains,
            OverallOutcome = legacyOutcome,
            Interpretation = interpretation,
            Checks = flatChecks
        };
    }

    private ExternalDependencyDomainEvidence BuildTseDomain(List<ExternalDependencyCheckRow> flat)
    {
        var tse = _tse.CurrentValue ?? new TseOptions();
        var checks = new List<ExternalDependencyCheckRow>(2);

        var modeOk = !string.IsNullOrWhiteSpace(tse.Mode);
        checks.Add(new ExternalDependencyCheckRow
        {
            Id = "tse_options_mode",
            Passed = modeOk,
            Detail = "tse_mode:" + (string.IsNullOrWhiteSpace(tse.Mode) ? "unset" : tse.Mode.Trim())
        });

        var tseModeOk = !string.IsNullOrWhiteSpace(tse.TseMode);
        checks.Add(new ExternalDependencyCheckRow
        {
            Id = "tse_options_tse_mode",
            Passed = tseModeOk,
            Detail = "tse_tse_mode:" + (string.IsNullOrWhiteSpace(tse.TseMode) ? "unset" : tse.TseMode.Trim())
        });

        foreach (var c in checks)
            flat.Add(c);

        return new ExternalDependencyDomainEvidence
        {
            Domain = ExternalDependencyRecoveryDomain.TseDeviceVendor,
            State = ExternalDependencyProofState.ManualCheckRequired,
            Notes = "TSE device/vendor readiness is not exercised by restore drill; options presence is not hardware proof.",
            Reason = "restore_drill_does_not_touch_tse_hardware",
            Checks = checks
        };
    }

    private ExternalDependencyDomainEvidence BuildSecretsDomain(List<ExternalDependencyCheckRow> flat)
    {
        var defaultConn = _configuration.GetConnectionString("DefaultConnection");
        var hasDefault = !string.IsNullOrWhiteSpace(defaultConn);
        var row = new ExternalDependencyCheckRow
        {
            Id = "default_connection_configured",
            Passed = hasDefault,
            Detail = hasDefault ? "default_connection_present" : "default_connection_missing"
        };
        flat.Add(row);

        return new ExternalDependencyDomainEvidence
        {
            Domain = ExternalDependencyRecoveryDomain.SecretsAndConfiguration,
            State = hasDefault ? ExternalDependencyProofState.NotProven : ExternalDependencyProofState.Failed,
            Notes = "Connection string names only; secret values and env readiness are not validated here.",
            Reason = hasDefault ? "connection_name_signal_only" : "default_connection_missing",
            Checks = new[] { row }
        };
    }

    private ExternalDependencyDomainEvidence BuildBackupToolingDomain(List<ExternalDependencyCheckRow> flat)
    {
        var backup = _backup.CurrentValue ?? new BackupOptions();
        var restore = _restoreVerification.CurrentValue ?? new RestoreVerificationOptions();

        var pgDumpConfigured = !string.IsNullOrWhiteSpace(backup.PgDumpExecutablePath);
        var pgRestoreConfigured = !string.IsNullOrWhiteSpace(restore.PgRestoreExecutablePath);

        var c1 = new ExternalDependencyCheckRow
        {
            Id = "pg_dump_executable_configured",
            Passed = pgDumpConfigured,
            Detail = pgDumpConfigured ? "path_set" : "path_unset_assume_path"
        };
        var c2 = new ExternalDependencyCheckRow
        {
            Id = "pg_restore_executable_configured",
            Passed = pgRestoreConfigured,
            Detail = pgRestoreConfigured ? "path_set" : "path_unset_assume_path"
        };
        flat.Add(c1);
        flat.Add(c2);

        return new ExternalDependencyDomainEvidence
        {
            Domain = ExternalDependencyRecoveryDomain.BackupTooling,
            State = ExternalDependencyProofState.NotImplemented,
            Notes = "Automated pg_dump/pg_restore execution proof deferred; path flags are informational only.",
            Reason = "tooling_execution_not_in_l6_block",
            Checks = new[] { c1, c2 }
        };
    }

    private ExternalDependencyDomainEvidence BuildArchiveDomain(List<ExternalDependencyCheckRow> flat)
    {
        var backup = _backup.CurrentValue ?? new BackupOptions();
        var hasExternalRoot = !string.IsNullOrWhiteSpace(backup.ExternalArchiveRoot);
        var row = new ExternalDependencyCheckRow
        {
            Id = "external_archive_root_configured",
            Passed = hasExternalRoot,
            Detail = hasExternalRoot ? "external_archive_root_present" : "external_archive_root_absent"
        };
        flat.Add(row);

        var prodLike = !_hostEnvironment.IsDevelopment();
        var needsImmutabilityPosture = prodLike
                                       && backup.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
                                       && hasExternalRoot;

        ExternalDependencyProofState state;
        string? notes;
        string? reason;

        if (!hasExternalRoot)
        {
            state = ExternalDependencyProofState.NotProven;
            notes = "External archive root not set; immutability posture not evaluated.";
            reason = "external_archive_root_absent";
        }
        else if (needsImmutabilityPosture && backup.RequireExternalArchiveImmutableTarget
                                          && !backup.ExternalArchiveImmutabilityAcknowledged
                                          && !backup.ExternalArchiveMutableTargetAccepted)
        {
            state = ExternalDependencyProofState.ManualCheckRequired;
            notes = "Production-like PgDump with external archive: operator immutability acknowledgement required.";
            reason = "immutability_operator_ack_pending";
        }
        else
        {
            state = ExternalDependencyProofState.ManualCheckRequired;
            notes = "Archive accessibility and WORM/object-lock posture require manual/ops validation.";
            reason = "archive_not_exercised_by_restore_drill";
        }

        return new ExternalDependencyDomainEvidence
        {
            Domain = ExternalDependencyRecoveryDomain.ArchiveStorage,
            State = state,
            Notes = notes,
            Reason = reason,
            Checks = new[] { row }
        };
    }

    private ExternalDependencyDomainEvidence BuildFinanzOnlineDomain(List<ExternalDependencyCheckRow> flat)
    {
        var foSection = _configuration.GetSection("FinanzOnline");
        var foPresent = foSection.Exists() && foSection.GetChildren().Any();
        var row = new ExternalDependencyCheckRow
        {
            Id = "finanz_online_config_section",
            Passed = foPresent,
            Detail = foPresent ? "finanz_online_section_present" : "finanz_online_section_absent"
        };
        flat.Add(row);

        return new ExternalDependencyDomainEvidence
        {
            Domain = ExternalDependencyRecoveryDomain.FinanzOnlineExternal,
            State = foPresent
                ? ExternalDependencyProofState.ManualCheckRequired
                : ExternalDependencyProofState.NotProven,
            Notes = "FinanzOnline section presence does not prove API/session/transmission health.",
            Reason = foPresent ? "reachability_not_tested" : "finanz_online_section_absent",
            Checks = new[] { row }
        };
    }
}
