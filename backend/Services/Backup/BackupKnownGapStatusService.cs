using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Live checks for the four catalog ids. Reads backup and drill rows; does not write them.
/// </summary>
public sealed class BackupKnownGapStatusService : IBackupKnownGapStatusService
{
    public const string ExternalArchiveNotSet = "external-archive-not-set";
    public const string ScheduledDrillNotRun = "scheduled-drill-not-run";
    public const string SystemLegalHoldNotConfigured = "system-legal-hold-not-configured";
    public const string DevOnlyFakeAdapter = "dev-only-fake-adapter";

    public static readonly TimeSpan DrillOverdueAfter = TimeSpan.FromDays(7);

    private readonly IBackupKnownGapsCatalog _catalog;
    private readonly IBackupKnownGapAcceptanceService _acceptance;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IOptionsMonitor<BackupKnownGapsAcceptanceOptions> _acceptanceOptions;
    private readonly IHostEnvironment _environment;
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public BackupKnownGapStatusService(
        IBackupKnownGapsCatalog catalog,
        IBackupKnownGapAcceptanceService acceptance,
        IOptionsMonitor<BackupOptions> backupOptions,
        IOptionsMonitor<BackupKnownGapsAcceptanceOptions> acceptanceOptions,
        IHostEnvironment environment,
        AppDbContext db,
        TimeProvider time)
    {
        _catalog = catalog;
        _acceptance = acceptance;
        _backupOptions = backupOptions;
        _acceptanceOptions = acceptanceOptions;
        _environment = environment;
        _db = db;
        _time = time;
    }

    public async Task<BackupKnownGapViewDto> GetViewAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var stored = tenantId == Guid.Empty
            ? new BackupKnownGapAcceptanceDto()
            : await _acceptance.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var accepted = new HashSet<string>(stored.AcceptedGaps, StringComparer.Ordinal);
        var open = await OpenIdsAsync(cancellationToken).ConfigureAwait(false);
        var allowsOverride = _acceptanceOptions.CurrentValue.AllowsGateOverride;

        var gaps = _catalog.Gaps.Select(definition =>
        {
            var isOpen = open.Contains(definition.Id);
            var isAccepted = accepted.Contains(definition.Id);
            return new BackupKnownGapCardDto
            {
                Id = definition.Id,
                Title = definition.Title,
                Severity = definition.Severity,
                Open = isOpen,
                Accepted = isAccepted,
                AcceptedAtUtc = isAccepted ? stored.AcceptedAtUtc : null,
                AcceptedBy = isAccepted ? stored.AcceptedBy : null,
                DisplayColor = DisplayColor(isOpen, isAccepted),
            };
        }).ToArray();

        return new BackupKnownGapViewDto
        {
            CatalogVersion = _catalog.Version,
            AllowsGateOverride = allowsOverride,
            Verdict = Verdict(gaps, allowsOverride),
            Acceptance = stored,
            Gaps = gaps,
        };
    }

    internal static string DisplayColor(bool open, bool accepted)
    {
        if (!open)
            return "green";
        return accepted ? "amber" : "red";
    }

    /// <summary>
    /// Open gaps fail the verdict. Accepted gaps still fail it unless <paramref name="allowsGateOverride"/> is true.
    /// </summary>
    internal static string Verdict(IReadOnlyList<BackupKnownGapCardDto> gaps, bool allowsGateOverride)
    {
        var open = gaps.Where(gap => gap.Open).ToArray();
        if (open.Length == 0)
            return "pass";
        if (!allowsGateOverride)
            return "fail";
        return open.All(gap => gap.Accepted) ? "pass" : "fail";
    }

    private async Task<HashSet<string>> OpenIdsAsync(CancellationToken cancellationToken)
    {
        var options = _backupOptions.CurrentValue;
        var now = _time.GetUtcNow().UtcDateTime;
        var open = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(options.ExternalArchiveRoot))
            open.Add(ExternalArchiveNotSet);

        if (_environment.IsDevelopment()
            && options.ExecutionAdapterKind == BackupExecutionAdapterKind.Fake)
        {
            open.Add(DevOnlyFakeAdapter);
        }

        var proofAt = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(run => run.TriggerSource == RestoreVerificationTriggerSource.Scheduled
                          && run.Status == RestoreVerificationStatus.Succeeded
                          && run.CompletedAt != null)
            .OrderByDescending(run => run.CompletedAt)
            .Select(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (proofAt is null || now - proofAt.Value > DrillOverdueAfter)
            open.Add(ScheduledDrillNotRun);

        var legalHold = await _db.BackupRuns.AsNoTracking()
            .Where(run => run.Strategy == BackupStrategyKind.System
                          && run.Status == BackupRunStatus.Succeeded)
            .OrderByDescending(run => run.CompletedAt ?? run.RequestedAt)
            .Select(run => (bool?)run.LegalHold)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (legalHold != true)
            open.Add(SystemLegalHoldNotConfigured);

        return open;
    }
}
