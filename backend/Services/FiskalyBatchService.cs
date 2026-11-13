using System.IO.Compression;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface IFiskalyBatchService
{
    FiskalyBatchLimitsDto GetLimits();

    Task<FiskalyBatchOperationResultDto> StornoAsync(
        FiskalyBatchStornoRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyBatchOperationResultDto> SonderbelegeAsync(
        FiskalyBatchSonderbelegeRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyBatchDepExportResult> DepExportAsync(
        FiskalyBatchDepExportRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}

public sealed class FiskalyBatchService : IFiskalyBatchService
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly FiskalyOptions _options;
    private readonly IFiskalyReceiptService _receipts;
    private readonly IRksvDepExportService _depExport;
    private readonly IDepExportHistoryService _depHistory;
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _auditLog;
    private readonly IFiskalyOperationStatusBroadcaster? _broadcaster;
    private readonly ILogger<FiskalyBatchService> _logger;

    public FiskalyBatchService(
        IOptions<FiskalyOptions> options,
        IFiskalyReceiptService receipts,
        IRksvDepExportService depExport,
        IDepExportHistoryService depHistory,
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService auditLog,
        ILogger<FiskalyBatchService> logger,
        IFiskalyOperationStatusBroadcaster? broadcaster = null)
    {
        _options = options.Value;
        _receipts = receipts;
        _depExport = depExport;
        _depHistory = depHistory;
        _db = db;
        _tenantAccessor = tenantAccessor;
        _auditLog = auditLog;
        _logger = logger;
        _broadcaster = broadcaster;
    }

    public FiskalyBatchLimitsDto GetLimits() =>
        new()
        {
            MaxItems = _options.ResolveBatchMaxItems(),
            WarnAtItems = _options.ResolveBatchWarnAtItems()
        };

    public async Task<FiskalyBatchOperationResultDto> StornoAsync(
        FiskalyBatchStornoRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        EnsureAmbientTenant();
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5)
            throw new FiskalyBatchException(
                FiskalyBatchErrorCodes.BatchValidation,
                "Cancellation reason must be at least 5 characters.");

        var items = DeduplicateStornoItems(request.Items);
        EnsureCount(items.Count);

        var batchId = request.BatchId is { } id && id != Guid.Empty ? id : Guid.NewGuid();
        var results = new List<FiskalyBatchItemResultDto>(items.Count);
        var success = 0;
        var failed = 0;
        var index = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            var label = string.IsNullOrWhiteSpace(item.ReceiptNumber)
                ? item.OriginalReceiptId.ToString("D")
                : item.ReceiptNumber.Trim();
            await PublishProgressAsync(
                    batchId,
                    "storno",
                    index,
                    items.Count,
                    label,
                    success,
                    failed,
                    done: false,
                    cancellationToken)
                .ConfigureAwait(false);

            var op = await _receipts
                .CancelReceiptAsync(
                    item.CashRegisterId,
                    item.OriginalReceiptId,
                    reason,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false);

            if (op.Success)
                success++;
            else
                failed++;

            results.Add(new FiskalyBatchItemResultDto
            {
                Key = item.OriginalReceiptId.ToString("D"),
                Success = op.Success,
                HistoryId = op.HistoryId,
                Label = label,
                Error = op.Error
            });
        }

        var summary = Finish(batchId, items.Count, success, failed, results);
        await AuditAsync("storno", actorUserId, actorIsSuperAdmin, summary, cancellationToken).ConfigureAwait(false);
        await PublishProgressAsync(batchId, "storno", items.Count, items.Count, null, success, failed, true, cancellationToken)
            .ConfigureAwait(false);
        return summary;
    }

    public async Task<FiskalyBatchOperationResultDto> SonderbelegeAsync(
        FiskalyBatchSonderbelegeRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        EnsureAmbientTenant();
        var kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind is not (FiskalyOperationTypes.Startbeleg or FiskalyOperationTypes.Monatsbeleg or FiskalyOperationTypes.Jahresbeleg))
        {
            throw new FiskalyBatchException(
                FiskalyBatchErrorCodes.BatchForbiddenKind,
                "Batch Sonderbelege supports Startbeleg, Monatsbeleg, and Jahresbeleg only.");
        }

        var registerIds = (request.CashRegisterIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        EnsureCount(registerIds.Count);

        var batchId = request.BatchId is { } id && id != Guid.Empty ? id : Guid.NewGuid();
        var results = new List<FiskalyBatchItemResultDto>(registerIds.Count);
        var success = 0;
        var failed = 0;
        var index = 0;

        foreach (var registerId in registerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            await PublishProgressAsync(
                    batchId,
                    "sonderbelege",
                    index,
                    registerIds.Count,
                    registerId.ToString("D"),
                    success,
                    failed,
                    done: false,
                    cancellationToken)
                .ConfigureAwait(false);

            var op = await ExecuteSonderbelegAsync(
                    kind,
                    registerId,
                    request.Year,
                    request.Month,
                    request.Reason,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false);

            if (op.Success)
                success++;
            else
                failed++;

            results.Add(new FiskalyBatchItemResultDto
            {
                Key = registerId.ToString("D"),
                Success = op.Success,
                HistoryId = op.HistoryId,
                Label = op.Data?.ReceiptNumber ?? registerId.ToString("D"),
                Error = op.Error
            });
        }

        var summary = Finish(batchId, registerIds.Count, success, failed, results);
        await AuditAsync($"sonderbelege:{kind}", actorUserId, actorIsSuperAdmin, summary, cancellationToken)
            .ConfigureAwait(false);
        await PublishProgressAsync(
                batchId,
                "sonderbelege",
                registerIds.Count,
                registerIds.Count,
                null,
                success,
                failed,
                true,
                cancellationToken)
            .ConfigureAwait(false);
        return summary;
    }

    public async Task<FiskalyBatchDepExportResult> DepExportAsync(
        FiskalyBatchDepExportRequest request,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!actorIsSuperAdmin)
            throw new KeyNotFoundException("Batch DEP export is SuperAdmin only.");

        EnsureAmbientTenant();

        if (request.ToUtc <= request.FromUtc)
            throw new FiskalyBatchException(
                FiskalyBatchErrorCodes.BatchValidation,
                "toUtc must be after fromUtc.");

        var tenantIds = (request.TenantIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        EnsureCount(tenantIds.Count);

        var jobs = await PlanDepJobsAsync(tenantIds, cancellationToken).ConfigureAwait(false);
        EnsureCount(jobs.Count);

        var batchId = request.BatchId is { } id && id != Guid.Empty ? id : Guid.NewGuid();
        var results = new List<FiskalyBatchItemResultDto>(jobs.Count);
        var success = 0;
        var failed = 0;
        var index = 0;
        var previousTenant = _tenantAccessor.TenantId;
        var previousSlug = _tenantAccessor.TenantSlug;
        try
        {
        await using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var job in jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
                var label = job.Missing
                    ? job.TenantSlug
                    : $"{job.TenantSlug}/{job.RegisterNumber}";
                await PublishProgressAsync(
                        batchId,
                        "dep-export",
                        index,
                        jobs.Count,
                        label,
                        success,
                        failed,
                        done: false,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (job.Missing)
                {
                    failed++;
                    results.Add(new FiskalyBatchItemResultDto
                    {
                        Key = job.TenantId.ToString("D"),
                        Success = false,
                        Label = label,
                        Error = new FiskalyReceiptErrorDto
                        {
                            Code = FiskalyReceiptErrorCodes.CashRegisterNotFound,
                            Message = "Tenant or cash register not found."
                        }
                    });
                    continue;
                }

                _tenantAccessor.TenantId = job.TenantId;
                try
                {
                    var fileName = await _depHistory
                        .BuildFileNameAsync(job.TenantId, job.CashRegisterId, cancellationToken)
                        .ConfigureAwait(false);
                    var export = await _depExport
                        .GenerateDepExportAsync(
                            job.CashRegisterId,
                            request.FromUtc.ToUniversalTime(),
                            request.ToUtc.ToUniversalTime(),
                            request.IncludeSpecialReceipts,
                            request.IncludeDailyClosings,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await _depHistory
                        .RecordCompletedAsync(
                            new DepExportHistoryRecordRequest
                            {
                                TenantId = job.TenantId,
                                CashRegisterId = job.CashRegisterId,
                                FromUtc = request.FromUtc.ToUniversalTime(),
                                ToUtc = request.ToUtc.ToUniversalTime(),
                                ExportedByUserId = string.IsNullOrWhiteSpace(actorUserId) ? "unknown" : actorUserId,
                                Export = export,
                                IncludeSpecialReceipts = request.IncludeSpecialReceipts,
                                IncludeDailyClosings = request.IncludeDailyClosings,
                                FileName = fileName
                            },
                            cancellationToken)
                        .ConfigureAwait(false);

                    var json = JsonSerializer.Serialize(export);
                    var entryName = $"{SanitizeZipSegment(job.TenantSlug)}/{SanitizeZipSegment(fileName)}";
                    var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                    await using (var entryStream = entry.Open())
                    {
                        var bytes = Encoding.UTF8.GetBytes(json);
                        await entryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    }

                    success++;
                    results.Add(new FiskalyBatchItemResultDto
                    {
                        Key = $"{job.TenantId:D}:{job.CashRegisterId:D}",
                        Success = true,
                        Label = label
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex,
                        "Batch DEP export failed for tenant {TenantId} register {RegisterId}",
                        job.TenantId,
                        job.CashRegisterId);
                    failed++;
                    results.Add(new FiskalyBatchItemResultDto
                    {
                        Key = $"{job.TenantId:D}:{job.CashRegisterId:D}",
                        Success = false,
                        Label = label,
                        Error = new FiskalyReceiptErrorDto
                        {
                            Code = FiskalyReceiptErrorCodes.FiskalyApiError,
                            Message = ex.Message
                        }
                    });
                }
            }

            var summaryForManifest = Finish(batchId, jobs.Count, success, failed, results);
            var manifest = zip.CreateEntry("manifest.json", CompressionLevel.Fastest);
            await using (var manifestStream = manifest.Open())
            {
                await JsonSerializer.SerializeAsync(manifestStream, summaryForManifest, ManifestJson, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var summary = Finish(batchId, jobs.Count, success, failed, results);
        await AuditAsync("dep-export", actorUserId, actorIsSuperAdmin: true, summary, cancellationToken)
            .ConfigureAwait(false);
        await PublishProgressAsync(batchId, "dep-export", jobs.Count, jobs.Count, null, success, failed, true, cancellationToken)
            .ConfigureAwait(false);

        if (success == 0)
        {
            throw new FiskalyBatchException(
                FiskalyBatchErrorCodes.BatchValidation,
                "All DEP exports in the batch failed.");
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "Z";
        return new FiskalyBatchDepExportResult
        {
            ZipBytes = zipStream.ToArray(),
            FileName = $"dep-export-batch_{stamp}.zip",
            Summary = summary
        };
        }
        finally
        {
            _tenantAccessor.TenantId = previousTenant;
            _tenantAccessor.TenantSlug = previousSlug;
        }
    }

    private async Task<List<DepJob>> PlanDepJobsAsync(List<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var jobs = new List<DepJob>();
        foreach (var tenantId in tenantIds)
        {
            var tenant = await _db.Tenants.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(t => t.Id == tenantId)
                .Select(t => new { t.Id, t.Slug, t.Name, t.Status, t.IsActive })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (tenant is null || !tenant.IsActive || TenantStatuses.IsRemoved(tenant.Status))
            {
                jobs.Add(new DepJob(tenantId, tenantId.ToString("N")[..8], Guid.Empty, string.Empty, Missing: true));
                continue;
            }

            var registers = await _db.CashRegisters.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId
                    && r.IsActive
                    && r.Status != RegisterStatus.Decommissioned)
                .Select(r => new { r.Id, r.RegisterNumber })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var slug = string.IsNullOrWhiteSpace(tenant.Slug) ? tenant.Id.ToString("N")[..8] : tenant.Slug.Trim();
            if (registers.Count == 0)
            {
                jobs.Add(new DepJob(tenant.Id, slug, Guid.Empty, string.Empty, Missing: true));
                continue;
            }

            foreach (var register in registers)
            {
                jobs.Add(new DepJob(tenant.Id, slug, register.Id, register.RegisterNumber, Missing: false));
            }
        }

        return jobs;
    }

    private async Task<FiskalyReceiptOperationResult> ExecuteSonderbelegAsync(
        string kind,
        Guid cashRegisterId,
        int? year,
        int? month,
        string? reason,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        return kind switch
        {
            FiskalyOperationTypes.Startbeleg => await _receipts
                .CreateStartbelegAsync(cashRegisterId, reason, actorUserId, cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Monatsbeleg => await _receipts
                .CreateMonatsbelegAsync(
                    cashRegisterId,
                    year ?? 0,
                    month ?? 0,
                    reason,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Jahresbeleg => await _receipts
                .CreateJahresbelegAsync(cashRegisterId, year ?? 0, reason, actorUserId, cancellationToken)
                .ConfigureAwait(false),
            _ => FiskalyReceiptOperationResult.Fail(
                400,
                FiskalyBatchErrorCodes.BatchForbiddenKind,
                "Unsupported batch Sonderbeleg kind.")
        };
    }

    private void EnsureAmbientTenant()
    {
        if (_tenantAccessor.TenantId is null || _tenantAccessor.TenantId == Guid.Empty)
            throw new KeyNotFoundException("Tenant context is required.");
    }

    private void EnsureCount(int count)
    {
        var max = _options.ResolveBatchMaxItems();
        if (count <= 0)
            throw new FiskalyBatchException(FiskalyBatchErrorCodes.BatchEmpty, "Batch contains no items.", maxItems: max);
        if (count > max)
        {
            throw new FiskalyBatchException(
                FiskalyBatchErrorCodes.BatchTooLarge,
                $"Batch exceeds the maximum of {max} items.",
                maxItems: max);
        }
    }

    private static List<FiskalyBatchStornoItemRequest> DeduplicateStornoItems(
        IEnumerable<FiskalyBatchStornoItemRequest>? items)
    {
        var seen = new HashSet<Guid>();
        var list = new List<FiskalyBatchStornoItemRequest>();
        foreach (var item in items ?? [])
        {
            if (item.OriginalReceiptId == Guid.Empty || item.CashRegisterId == Guid.Empty)
                continue;
            if (!seen.Add(item.OriginalReceiptId))
                continue;
            list.Add(item);
        }

        return list;
    }

    private static FiskalyBatchOperationResultDto Finish(
        Guid batchId,
        int total,
        int success,
        int failed,
        List<FiskalyBatchItemResultDto> results) =>
        new()
        {
            BatchId = batchId,
            Total = total,
            SuccessCount = success,
            FailedCount = failed,
            Results = results
        };

    private async Task PublishProgressAsync(
        Guid batchId,
        string kind,
        int current,
        int total,
        string? currentLabel,
        int success,
        int failed,
        bool done,
        CancellationToken cancellationToken)
    {
        if (_broadcaster is null)
            return;
        await _broadcaster
            .PublishBatchProgressAsync(
                new FiskalyBatchProgressEventDto
                {
                    BatchId = batchId,
                    TenantId = _tenantAccessor.TenantId,
                    Kind = kind,
                    Current = current,
                    Total = total,
                    CurrentLabel = currentLabel,
                    SuccessCount = success,
                    FailedCount = failed,
                    Done = done
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task AuditAsync(
        string kind,
        string actorUserId,
        bool actorIsSuperAdmin,
        FiskalyBatchOperationResultDto summary,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            await _auditLog
                .LogSystemOperationAsync(
                    action: AuditLogActions.FISKALY_BATCH_COMPLETED,
                    entityType: "FiskalyBatch",
                    userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                    userRole: actorIsSuperAdmin ? Roles.SuperAdmin : Roles.Manager,
                    description: $"Fiskaly batch {kind} completed ({summary.SuccessCount}/{summary.Total} succeeded).",
                    status: summary.FailedCount == 0 ? AuditLogStatus.Success : AuditLogStatus.Failed,
                    actionType: AuditEventType.FiskalyBatchCompleted,
                    tenantId: _tenantAccessor.TenantId,
                    newValues: new
                    {
                        summary.BatchId,
                        Kind = kind,
                        summary.Total,
                        summary.SuccessCount,
                        summary.FailedCount
                    })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Fiskaly batch audit for {Kind}", kind);
        }
    }

    private static string SanitizeZipSegment(string value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "item" : value.Trim();
        var chars = raw.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_').ToArray();
        var cleaned = new string(chars).Trim('_');
        return string.IsNullOrEmpty(cleaned) ? "item" : cleaned;
    }

    private sealed record DepJob(
        Guid TenantId,
        string TenantSlug,
        Guid CashRegisterId,
        string RegisterNumber,
        bool Missing);
}
