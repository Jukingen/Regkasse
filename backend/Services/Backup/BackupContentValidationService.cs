using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Reads tenant VerificationManifest <c>tableRowCounts</c> or System GlobalsDump <c>sectionRowCounts</c>,
/// compares selected live counts, runs fiscal integrity checks on live tenant data, and persists
/// a <see cref="BackupVerification"/> row (<see cref="IBackupContentValidationService.VerifierSourceContentValidation"/>).
/// </summary>
public sealed class BackupContentValidationService : IBackupContentValidationService
{
    private static readonly string[] CriticalTenantTables =
    {
        "products.json",
        "categories.json",
        "customers.json",
        "payment_details.json",
        "receipts.json",
    };

    private static readonly TimeSpan FiscalLookback = TimeSpan.FromDays(90);
    private const int MaxReceiptsPerRegister = 2000;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupEncryptionService _encryption;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BackupContentValidationService> _logger;

    public BackupContentValidationService(
        AppDbContext db,
        IOptionsMonitor<BackupOptions> options,
        IBackupEncryptionService encryption,
        IHostEnvironment hostEnvironment,
        ILogger<BackupContentValidationService> logger)
    {
        _db = db;
        _options = options;
        _encryption = encryption;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<BackupContentValidationDto> GetOrRunValidationAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var cached = await TryLoadCachedReportAsync(runId, cancellationToken).ConfigureAwait(false);
        if (cached != null)
            return cached;

        return await ValidateContentAsync(runId, cancellationToken).ConfigureAwait(false);
    }

    public Task<BackupContentValidationDto> ValidateAsync(
        Guid backupRunId,
        CancellationToken cancellationToken = default) =>
        ValidateContentAsync(backupRunId, cancellationToken);

    public async Task<BackupContentValidationDto> ValidateContentAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var run = await _db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run == null)
            throw new KeyNotFoundException($"Backup run {runId} not found.");

        var warnings = new List<string>();
        BackupContentValidationDto report = run.Strategy == BackupStrategyKind.Tenant
            ? await ValidateTenantAsync(run, startedAt, warnings, cancellationToken).ConfigureAwait(false)
            : await ValidateSystemAsync(run, startedAt, warnings, cancellationToken).ConfigureAwait(false);

        var verificationId = await PersistVerificationAsync(run, report, startedAt, cancellationToken)
            .ConfigureAwait(false);

        return CloneWithVerificationId(report, verificationId);
    }

    private async Task<BackupContentValidationDto?> TryLoadCachedReportAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.BackupRuns.AsNoTracking()
            .AnyAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Backup run {runId} not found.");

        var row = await _db.BackupVerifications.AsNoTracking()
            .Where(v =>
                v.BackupRunId == runId
                && v.VerifierSource == IBackupContentValidationService.VerifierSourceContentValidation)
            .OrderByDescending(v => v.CompletedAt ?? v.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row == null || string.IsNullOrWhiteSpace(row.DetailsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(row.DetailsJson);
            if (doc.RootElement.TryGetProperty("report", out var reportEl)
                && reportEl.ValueKind == JsonValueKind.Object)
            {
                var hydrated = JsonSerializer.Deserialize<BackupContentValidationDto>(
                    reportEl.GetRawText(),
                    JsonOptions);
                if (hydrated != null)
                    return CloneWithVerificationId(hydrated, row.Id);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to hydrate cached content validation for run {BackupRunId}; will re-run.",
                runId);
        }

        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static BackupContentValidationDto CloneWithVerificationId(
        BackupContentValidationDto report,
        Guid verificationId) =>
        new()
        {
            RunId = report.RunId,
            ValidatedAtUtc = report.ValidatedAtUtc,
            VerificationId = verificationId,
            OverallStatus = report.OverallStatus,
            Summary = report.Summary,
            Strategy = report.Strategy,
            Tables = report.Tables,
            FiscalChecks = report.FiscalChecks,
            Fiscal = report.Fiscal,
            Warnings = report.Warnings,
        };

    private async Task<Guid> PersistVerificationAsync(
        BackupRun run,
        BackupContentValidationDto report,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTime.UtcNow;
        var passed = report.OverallStatus is BackupContentValidationStatuses.Passed
            or BackupContentValidationStatuses.Partial;
        var failureReason = passed
            ? null
            : Truncate(report.Summary ?? report.OverallStatus, 4000);

        var verification = new BackupVerification
        {
            Id = Guid.NewGuid(),
            BackupRunId = run.Id,
            Status = passed ? BackupVerificationStatus.Passed : BackupVerificationStatus.Failed,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            VerifierSource = IBackupContentValidationService.VerifierSourceContentValidation,
            CompletenessFlag = run.Artifacts.Any(a => a.ArtifactType == BackupArtifactType.LogicalDump),
            FailureReason = failureReason,
            DetailsJson = JsonSerializer.Serialize(new
            {
                kind = IBackupContentValidationService.VerifierSourceContentValidation,
                runId = report.RunId,
                overallStatus = report.OverallStatus,
                strategy = report.Strategy,
                summary = report.Summary,
                report,
                tables = report.Tables.Select(t => new
                {
                    tableName = t.TableName,
                    t.ManifestCount,
                    actualCount = t.ActualCount,
                    t.Match,
                    t.Status,
                    t.Detail
                }),
                fiscalChecks = report.FiscalChecks.Select(c => new
                {
                    c.CheckName,
                    c.Passed,
                    c.Details
                }),
                warnings = report.Warnings
            })
        };

        _db.BackupVerifications.Add(verification);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Content validation for run {BackupRunId}: status={Status} verificationId={VerificationId}",
            run.Id,
            report.OverallStatus,
            verification.Id);

        return verification.Id;
    }

    private async Task<BackupContentValidationDto> ValidateTenantAsync(
        BackupRun run,
        DateTime validatedAt,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var counts = await TryReadTenantTableRowCountsAsync(run, warnings, cancellationToken)
            .ConfigureAwait(false);

        if (counts == null || counts.Count == 0)
        {
            return new BackupContentValidationDto
            {
                RunId = run.Id,
                ValidatedAtUtc = validatedAt,
                OverallStatus = BackupContentValidationStatuses.Unavailable,
                Strategy = nameof(BackupStrategyKind.Tenant),
                Summary = "Manifest tableRowCounts not available for this tenant backup.",
                Warnings = warnings,
                FiscalChecks =
                [
                    new BackupContentFiscalCheckDto
                    {
                        CheckName = "manifest_available",
                        Passed = false,
                        Details = "VerificationManifest tableRowCounts missing or unreadable.",
                    }
                ],
            };
        }

        var tables = new List<BackupContentTableValidationDto>();
        var tenantId = run.TenantId;
        foreach (var key in CriticalTenantTables)
        {
            counts.TryGetValue(key, out var manifestCount);
            int? live = null;
            if (tenantId.HasValue)
                live = await CountLiveTenantTableAsync(key, tenantId.Value, cancellationToken)
                    .ConfigureAwait(false);

            tables.Add(ScoreTable(key, manifestCount, live));
        }

        var payments = counts.GetValueOrDefault("payment_details.json");
        var receipts = counts.GetValueOrDefault("receipts.json");
        var (fiscal, fiscalChecks) = await BuildFiscalAsync(
                tenantId,
                payments,
                receipts,
                cancellationToken)
            .ConfigureAwait(false);

        if (payments <= 0 && receipts <= 0)
            warnings.Add("Tenant backup has zero payments and zero receipts in manifest.");

        var overall = DeriveOverall(tables, fiscalChecks, fiscal, requireContent: payments > 0 || receipts > 0);
        if (payments <= 0 && receipts <= 0 && overall == BackupContentValidationStatuses.Passed)
            overall = BackupContentValidationStatuses.Partial;

        return new BackupContentValidationDto
        {
            RunId = run.Id,
            ValidatedAtUtc = validatedAt,
            OverallStatus = overall,
            Strategy = nameof(BackupStrategyKind.Tenant),
            Summary = overall switch
            {
                BackupContentValidationStatuses.Passed => "All critical tables and fiscal checks validated.",
                BackupContentValidationStatuses.Partial => "Content validation completed with warnings / partial matches.",
                BackupContentValidationStatuses.Failed => "Content validation failed for one or more checks.",
                _ => "Content validation unavailable.",
            },
            Tables = tables,
            FiscalChecks = fiscalChecks,
            Fiscal = fiscal,
            Warnings = warnings,
        };
    }

    private async Task<BackupContentValidationDto> ValidateSystemAsync(
        BackupRun run,
        DateTime validatedAt,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var sectionCounts = TryReadSystemSectionRowCounts(run, warnings);
        var tables = new List<BackupContentTableValidationDto>();
        var fiscalChecks = new List<BackupContentFiscalCheckDto>
        {
            new()
            {
                CheckName = "system_fiscal_package",
                Passed = true,
                Details = "Fiscal chain/sequence proof for System dumps is covered by restore drill, not package content validation.",
            }
        };

        if (sectionCounts == null || sectionCounts.Count == 0)
        {
            warnings.Add("System sectionRowCounts not found (expected on composite system ZIP GlobalsDump metadata).");
            return new BackupContentValidationDto
            {
                RunId = run.Id,
                ValidatedAtUtc = validatedAt,
                OverallStatus = BackupContentValidationStatuses.Unavailable,
                Strategy = nameof(BackupStrategyKind.System),
                Summary = "System content counts unavailable for this adapter/package.",
                Warnings = warnings,
                FiscalChecks = fiscalChecks,
                Fiscal = new BackupContentFiscalValidationDto
                {
                    Status = "skipped",
                    Detail = "Fiscal package validation applies to tenant JSON packages; use restore drill for System dumps.",
                },
            };
        }

        var liveTenantCount = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(t => t.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var tenantSections = sectionCounts.Count(kv =>
            kv.Key.Contains("tenant", StringComparison.OrdinalIgnoreCase)
            || kv.Key.EndsWith(".tenant.zip", StringComparison.OrdinalIgnoreCase));

        var tenantManifest = tenantSections > 0 ? tenantSections : sectionCounts.Count;
        tables.Add(new BackupContentTableValidationDto
        {
            TableKey = "tenants",
            ManifestCount = tenantManifest,
            LiveCount = liveTenantCount,
            Match = tenantManifest == liveTenantCount,
            Status = (tenantSections > 0 || sectionCounts.Count > 0) ? "passed" : "failed",
            Detail = tenantSections > 0
                ? $"{tenantSections} tenant section(s) in manifest; {liveTenantCount} active tenants live."
                : "No tenant sections in sectionRowCounts.",
        });

        foreach (var (key, count) in sectionCounts.OrderBy(k => k.Key).Take(20))
        {
            if (string.Equals(key, "tenants", StringComparison.OrdinalIgnoreCase))
                continue;
            tables.Add(new BackupContentTableValidationDto
            {
                TableKey = key,
                ManifestCount = count,
                LiveCount = null,
                Match = false,
                Status = count >= 0 ? "passed" : "failed",
                Detail = "Section present in system package metadata.",
            });
        }

        var overall = tenantSections >= 1
            ? BackupContentValidationStatuses.Passed
            : BackupContentValidationStatuses.Failed;

        if (tenantSections < 1)
            warnings.Add("System backup must include at least one tenant section.");

        return new BackupContentValidationDto
        {
            RunId = run.Id,
            ValidatedAtUtc = validatedAt,
            OverallStatus = overall,
            Strategy = nameof(BackupStrategyKind.System),
            Summary = overall == BackupContentValidationStatuses.Passed
                ? "System package sections validated."
                : "System package content validation failed.",
            Tables = tables,
            FiscalChecks = fiscalChecks,
            Fiscal = new BackupContentFiscalValidationDto
            {
                Status = "skipped",
                Detail = "Use restore drill for System dump fiscal continuity.",
            },
            Warnings = warnings,
        };
    }

    private async Task<Dictionary<string, int>?> TryReadTenantTableRowCountsAsync(
        BackupRun run,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var manifest = run.Artifacts
            .Where(a => a.ArtifactType == BackupArtifactType.VerificationManifest)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (manifest == null)
        {
            warnings.Add("No VerificationManifest artifact on run.");
            return null;
        }

        if (!BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                run.Id,
                manifest,
                _options.CurrentValue,
                _logger,
                _hostEnvironment,
                "ContentValidation",
                out var path)
            || string.IsNullOrWhiteSpace(path))
        {
            warnings.Add("VerificationManifest file not found on disk/archive.");
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (_encryption.LooksEncrypted(bytes))
                bytes = _encryption.Decrypt(bytes);

            using var doc = JsonDocument.Parse(bytes);
            if (!doc.RootElement.TryGetProperty("tableRowCounts", out var countsEl)
                || countsEl.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("Manifest JSON missing tableRowCounts object.");
                return null;
            }

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in countsEl.EnumerateObject())
            {
                if (prop.Value.TryGetInt32(out var n))
                    map[prop.Name] = n;
            }

            return map;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed reading tenant manifest for run {RunId}", run.Id);
            warnings.Add("Failed to parse VerificationManifest JSON.");
            return null;
        }
    }

    private static Dictionary<string, int>? TryReadSystemSectionRowCounts(BackupRun run, List<string> warnings)
    {
        var globals = run.Artifacts
            .Where(a => a.ArtifactType == BackupArtifactType.GlobalsDump)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (globals == null)
        {
            warnings.Add("No GlobalsDump artifact on system run.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(globals.MetadataJson))
        {
            warnings.Add("GlobalsDump MetadataJson empty.");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(globals.MetadataJson);
            if (!doc.RootElement.TryGetProperty("sectionRowCounts", out var countsEl)
                || countsEl.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("GlobalsDump metadata missing sectionRowCounts.");
                return null;
            }

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in countsEl.EnumerateObject())
            {
                if (prop.Value.TryGetInt32(out var n))
                    map[prop.Name] = n;
            }

            return map;
        }
        catch (Exception)
        {
            warnings.Add("Failed to parse GlobalsDump MetadataJson.");
            return null;
        }
    }

    private async Task<int?> CountLiveTenantTableAsync(
        string tableKey,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        switch (tableKey)
        {
            case "products.json":
                return await _db.Products.IgnoreQueryFilters()
                    .CountAsync(p => p.TenantId == tenantId, cancellationToken);
            case "categories.json":
                return await _db.Categories.IgnoreQueryFilters()
                    .CountAsync(c => c.TenantId == tenantId, cancellationToken);
            case "customers.json":
                return await _db.Customers.IgnoreQueryFilters()
                    .CountAsync(c => c.TenantId == tenantId, cancellationToken);
            case "payment_details.json":
            {
                var registerIds = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                return await _db.PaymentDetails.AsNoTracking()
                    .CountAsync(p => registerIds.Contains(p.CashRegisterId), cancellationToken);
            }
            case "receipts.json":
                return await _db.Receipts.IgnoreQueryFilters()
                    .CountAsync(r => r.TenantId == tenantId, cancellationToken);
            default:
                return null;
        }
    }

    private async Task<(BackupContentFiscalValidationDto Fiscal, List<BackupContentFiscalCheckDto> Checks)>
        BuildFiscalAsync(
            Guid? tenantId,
            int paymentsManifest,
            int receiptsManifest,
            CancellationToken cancellationToken)
    {
        var checks = new List<BackupContentFiscalCheckDto>();

        checks.Add(new BackupContentFiscalCheckDto
        {
            CheckName = "manifest_fiscal_presence",
            Passed = paymentsManifest > 0 && receiptsManifest > 0,
            Details = paymentsManifest > 0 && receiptsManifest > 0
                ? $"Manifest payments={paymentsManifest}, receipts={receiptsManifest}."
                : $"Manifest missing fiscal content (payments={paymentsManifest}, receipts={receiptsManifest}).",
        });

        if (!tenantId.HasValue)
        {
            checks.Add(new BackupContentFiscalCheckDto
            {
                CheckName = "live_tenant_context",
                Passed = false,
                Details = "No tenant id on run; live fiscal integrity checks skipped.",
            });

            return (new BackupContentFiscalValidationDto
            {
                Status = "skipped",
                PaymentsInManifest = paymentsManifest,
                ReceiptsInManifest = receiptsManifest,
                Detail = "No tenant id on run; fiscal live check skipped.",
            }, checks);
        }

        var tid = tenantId.Value;
        var registers = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tid)
            .Select(x => new { x.Id, x.RegisterNumber })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var registerIds = registers.Select(r => r.Id).ToList();

        var signed = await _db.PaymentDetails.AsNoTracking()
            .CountAsync(
                p => registerIds.Contains(p.CashRegisterId)
                     && p.TseSignature != null
                     && p.TseSignature != "",
                cancellationToken)
            .ConfigureAwait(false);
        var unsigned = await _db.PaymentDetails.AsNoTracking()
            .CountAsync(
                p => registerIds.Contains(p.CashRegisterId)
                     && (p.TseSignature == null || p.TseSignature == ""),
                cancellationToken)
            .ConfigureAwait(false);

        checks.Add(new BackupContentFiscalCheckDto
        {
            CheckName = "live_signed_payments",
            Passed = signed >= unsigned || (signed + unsigned) == 0,
            Details = $"Live signed={signed}, unsigned={unsigned}.",
        });

        var fromUtc = DateTime.UtcNow - FiscalLookback;
        var toUtc = DateTime.UtcNow;
        var totalChainBreaks = 0;
        var totalSeqGaps = 0;
        var totalDuplicates = 0;
        var registersAnalyzed = 0;

        foreach (var reg in registers)
        {
            var receiptRows = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
                .Where(r => r.TenantId == tid && r.CashRegisterId == reg.Id && r.CreatedAt >= fromUtc)
                .OrderByDescending(r => r.CreatedAt)
                .Take(MaxReceiptsPerRegister)
                .Select(r => new
                {
                    r.ReceiptId,
                    r.ReceiptNumber,
                    r.CreatedAt,
                    r.SignatureValue,
                    r.PrevSignatureValue,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (receiptRows.Count == 0)
                continue;

            registersAnalyzed++;
            var ordered = receiptRows
                .OrderBy(r => r.CreatedAt)
                .Select(r =>
                {
                    var parsed = TseChainContinuityAnalyzer.TryParseBelegNrSequence(
                        r.ReceiptNumber,
                        out var seq,
                        out var ymd);
                    return new TseChainContinuityAnalyzer.ReceiptLink
                    {
                        ReceiptId = r.ReceiptId,
                        ReceiptNumber = r.ReceiptNumber,
                        CreatedAtUtc = r.CreatedAt,
                        SignatureValue = r.SignatureValue,
                        PrevSignatureValue = r.PrevSignatureValue,
                        ParsedSequence = parsed ? seq : null,
                        ParsedSequenceDateYmd = ymd,
                    };
                })
                .ToList();

            var report = TseChainContinuityAnalyzer.AnalyzeRegister(
                reg.Id,
                reg.RegisterNumber,
                fromUtc,
                toUtc,
                ordered,
                lastCounterFromState: 0);

            totalChainBreaks += report.ChainBreakCount;
            totalSeqGaps += report.SequenceGapCount;
            totalDuplicates += report.DuplicateCount;
        }

        checks.Add(new BackupContentFiscalCheckDto
        {
            CheckName = "receipt_signature_chain",
            Passed = totalChainBreaks == 0,
            Details = registersAnalyzed == 0
                ? "No live receipts in lookback window; chain check skipped (treated as pass)."
                : $"Chain breaks={totalChainBreaks} across {registersAnalyzed} register(s) (live DB, last {FiscalLookback.TotalDays:0}d).",
        });

        checks.Add(new BackupContentFiscalCheckDto
        {
            CheckName = "receipt_sequence_continuity",
            Passed = totalSeqGaps == 0,
            Details = registersAnalyzed == 0
                ? "No live receipts in lookback window; sequence check skipped (treated as pass)."
                : $"BelegNr sequence gaps={totalSeqGaps} across {registersAnalyzed} register(s).",
        });

        checks.Add(new BackupContentFiscalCheckDto
        {
            CheckName = "receipt_number_uniqueness",
            Passed = totalDuplicates == 0,
            Details = registersAnalyzed == 0
                ? "No live receipts in lookback window; uniqueness check skipped (treated as pass)."
                : $"Duplicate receipt number groups={totalDuplicates}.",
        });

        string status;
        string detail;
        if (paymentsManifest <= 0 && receiptsManifest <= 0)
        {
            status = "warning";
            detail = "Manifest has no fiscal payments/receipts.";
        }
        else if (totalChainBreaks > 0)
        {
            status = "failed";
            detail = $"Live signature chain breaks detected ({totalChainBreaks}). Full package-embedded proof still requires restore drill.";
        }
        else if (totalSeqGaps > 0 || totalDuplicates > 0 || unsigned > signed)
        {
            status = "warning";
            detail =
                $"Fiscal warnings: seqGaps={totalSeqGaps}, duplicates={totalDuplicates}, unsigned={unsigned}, signed={signed}.";
        }
        else
        {
            status = "passed";
            detail = "Fiscal presence + live receipt chain/sequence checks passed.";
        }

        return (new BackupContentFiscalValidationDto
        {
            Status = status,
            PaymentsInManifest = paymentsManifest,
            ReceiptsInManifest = receiptsManifest,
            LiveSignedPayments = signed,
            LiveUnsignedPayments = unsigned,
            ChainBreakCount = totalChainBreaks,
            SequenceGapCount = totalSeqGaps,
            DuplicateReceiptCount = totalDuplicates,
            Detail = detail,
        }, checks);
    }

    private static BackupContentTableValidationDto ScoreTable(string key, int manifestCount, int? liveCount)
    {
        if (liveCount == null)
        {
            return new BackupContentTableValidationDto
            {
                TableKey = key,
                ManifestCount = manifestCount,
                LiveCount = null,
                Match = false,
                Status = "passed",
                Detail = "Manifest count present; live compare skipped.",
            };
        }

        var match = liveCount.Value == manifestCount;
        if (liveCount.Value >= manifestCount)
        {
            return new BackupContentTableValidationDto
            {
                TableKey = key,
                ManifestCount = manifestCount,
                LiveCount = liveCount,
                Match = match,
                Status = "passed",
                Detail = match
                    ? "Manifest matches live count."
                    : "Live count >= manifest (post-backup growth OK).",
            };
        }

        var delta = manifestCount - liveCount.Value;
        return new BackupContentTableValidationDto
        {
            TableKey = key,
            ManifestCount = manifestCount,
            LiveCount = liveCount,
            Match = false,
            Status = delta > Math.Max(5, manifestCount / 10) ? "failed" : "warning",
            Detail = $"Live count below manifest by {delta} (possible deletion after backup).",
        };
    }

    private static string DeriveOverall(
        IReadOnlyList<BackupContentTableValidationDto> tables,
        IReadOnlyList<BackupContentFiscalCheckDto> fiscalChecks,
        BackupContentFiscalValidationDto? fiscal,
        bool requireContent)
    {
        if (tables.Any(t => t.Status == "failed"))
            return BackupContentValidationStatuses.Failed;
        if (fiscal?.Status == "failed")
            return BackupContentValidationStatuses.Failed;
        if (fiscalChecks.Any(c =>
                !c.Passed
                && (c.CheckName is "receipt_signature_chain"
                    or "receipt_sequence_continuity"
                    or "receipt_number_uniqueness")))
        {
            // Sequence/chain failures already reflected in fiscal.Status; hard-fail only when fiscal says failed.
            if (fiscal?.Status == "failed")
                return BackupContentValidationStatuses.Failed;
        }

        if (tables.Any(t => t.Status == "warning")
            || fiscal?.Status == "warning"
            || fiscalChecks.Any(c => !c.Passed)
            || !requireContent)
        {
            return BackupContentValidationStatuses.Partial;
        }

        return BackupContentValidationStatuses.Passed;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;
        return value[..max];
    }
}
