using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services.Rksv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class DepExportHistoryRecordRequest
{
    public required Guid TenantId { get; init; }
    public required Guid CashRegisterId { get; init; }
    public required DateTime FromUtc { get; init; }
    public required DateTime ToUtc { get; init; }
    public required string ExportedByUserId { get; init; }
    public required RksvDepExportRootDto Export { get; init; }
    public bool IncludeSpecialReceipts { get; init; } = true;
    public bool IncludeDailyClosings { get; init; } = true;
    public Guid? ScheduleId { get; init; }
    public string? StoragePath { get; init; }
    /// <summary>When set, stored as-is; otherwise built as <c>dep-export_{slug}_{register}_{stamp}.json</c>.</summary>
    public string? FileName { get; init; }
}

public interface IDepExportHistoryService
{
    Task<DepExportHistory> RecordCompletedAsync(
        DepExportHistoryRecordRequest request,
        CancellationToken cancellationToken = default);

    Task<DepExportHistory> RecordFailedAsync(
        Guid tenantId,
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        string exportedByUserId,
        string errorMessage,
        bool includeSpecialReceipts = true,
        bool includeDailyClosings = true,
        Guid? scheduleId = null,
        CancellationToken cancellationToken = default);

    Task<DepExportHistoryListResponse> ListAsync(
        Guid tenantId,
        Guid? cashRegisterId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<DepExportHistoryResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(Stream Stream, string FileName, string ContentType)?> TryOpenDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(string TenantSlug, string RegisterNumber)> ResolveNamingAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<string> BuildFileNameAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default,
        DateTime? at = null);
}

public sealed class DepExportHistoryService : IDepExportHistoryService
{
    private readonly AppDbContext _context;
    private readonly IFileNamingService _fileNaming;
    private readonly IDepExportRequirementService _requirementService;
    private readonly IDepExportValidationService _validationService;
    private readonly IDepExportArchiveService _archiveService;
    private readonly IDepExportPushNotificationService _pushNotification;
    private readonly IDepExportAuditService _audit;
    private readonly IOptionsMonitor<DepExportArchiveOptions> _archiveOptions;
    private readonly ILogger<DepExportHistoryService> _logger;

    public DepExportHistoryService(
        AppDbContext context,
        IFileNamingService fileNaming,
        IDepExportRequirementService requirementService,
        IDepExportValidationService validationService,
        IDepExportArchiveService archiveService,
        IDepExportPushNotificationService pushNotification,
        IDepExportAuditService audit,
        IOptionsMonitor<DepExportArchiveOptions> archiveOptions,
        ILogger<DepExportHistoryService> logger)
    {
        _context = context;
        _fileNaming = fileNaming;
        _requirementService = requirementService;
        _validationService = validationService;
        _archiveService = archiveService;
        _pushNotification = pushNotification;
        _audit = audit;
        _archiveOptions = archiveOptions;
        _logger = logger;
    }

    public async Task<DepExportHistory> RecordCompletedAsync(
        DepExportHistoryRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var (groupCount, signatureCount) = RksvDepExportStats.Count(request.Export);
        var (_, legacyJwsCount) = RksvDepExportService.CountJwsCompliance(request.Export);
        var json = JsonSerializer.Serialize(request.Export);
        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? await BuildFileNameAsync(request.TenantId, request.CashRegisterId, cancellationToken)
                .ConfigureAwait(false)
            : request.FileName.Trim();

        var row = new DepExportHistory
        {
            TenantId = request.TenantId,
            CashRegisterId = request.CashRegisterId,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            ExportedAt = DateTime.UtcNow,
            ExportedByUserId = request.ExportedByUserId,
            FileName = fileName,
            FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(json),
            SignatureCount = signatureCount,
            GroupCount = groupCount,
            LegacyJwsCount = legacyJwsCount,
            Status = DepExportStatus.Completed.ToString(),
            StoragePath = request.StoragePath,
            ScheduleId = request.ScheduleId,
            IncludeSpecialReceipts = request.IncludeSpecialReceipts,
            IncludeDailyClosings = request.IncludeDailyClosings,
            ValidationStatus = DepExportValidationStatuses.Pending,
        };

        _context.DepExportHistories.Add(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryLogAuditAsync(
                new DepExportAuditEntry
                {
                    TenantId = request.TenantId,
                    Action = DepExportAuditActions.Created,
                    ExportName = fileName,
                    ExportHistoryId = row.Id,
                    UserId = request.ExportedByUserId,
                    Details =
                        $"from={request.FromUtc:O}; to={request.ToUtc:O}; signatures={signatureCount}; schedule={request.ScheduleId}",
                },
                cancellationToken)
            .ConfigureAwait(false);

        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

        try
        {
            await _requirementService.TryCompletePeriodsForExportAsync(
                    request.TenantId,
                    request.FromUtc,
                    request.ToUtc,
                    request.ExportedByUserId,
                    fileName,
                    fileHash,
                    row.Id,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to update DEP compliance periods after history {HistoryId}",
                row.Id);
        }

        try
        {
            var validation = await _validationService
                .ValidateExportAsync(row.Id, json, cancellationToken)
                .ConfigureAwait(false);
            await _context.Entry(row).ReloadAsync(cancellationToken).ConfigureAwait(false);

            await TryLogAuditAsync(
                    new DepExportAuditEntry
                    {
                        TenantId = request.TenantId,
                        Action = DepExportAuditActions.Validated,
                        ExportName = fileName,
                        ExportHistoryId = row.Id,
                        UserId = request.ExportedByUserId,
                        Details =
                            $"auto=true; valid={validation.IsValid}; status={row.ValidationStatus}",
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Automatic DEP export validation failed for history {HistoryId}",
                row.Id);
            row.ValidationStatus = DepExportValidationStatuses.Failed;
            row.ValidatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var archiveOpts = _archiveOptions.CurrentValue;
            if (archiveOpts.Enabled && archiveOpts.AutoArchiveOnComplete)
            {
                var archive = await _archiveService
                    .ArchiveExportAsync(row.Id, json, cancellationToken)
                    .ConfigureAwait(false);
                if (!archive.Success && archive.ErrorMessage is not null)
                {
                    _logger.LogWarning(
                        "Automatic DEP export archive skipped/failed for history {HistoryId}: {Message}",
                        row.Id,
                        archive.ErrorMessage);
                }

                await _context.Entry(row).ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Automatic DEP export archive failed for history {HistoryId}",
                row.Id);
        }

        try
        {
            await _pushNotification
                .SendSuccessNotificationAsync(request.TenantId, fileName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "DEP export success push failed for history {HistoryId}",
                row.Id);
        }

        _logger.LogInformation(
            "DEP export history recorded id={HistoryId} register={RegisterId} signatures={SignatureCount}",
            row.Id,
            row.CashRegisterId,
            row.SignatureCount);

        return row;
    }

    public async Task<DepExportHistory> RecordFailedAsync(
        Guid tenantId,
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        string exportedByUserId,
        string errorMessage,
        bool includeSpecialReceipts = true,
        bool includeDailyClosings = true,
        Guid? scheduleId = null,
        CancellationToken cancellationToken = default)
    {
        var fileName = await BuildFileNameAsync(tenantId, cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        var row = new DepExportHistory
        {
            TenantId = tenantId,
            CashRegisterId = cashRegisterId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            ExportedAt = DateTime.UtcNow,
            ExportedByUserId = exportedByUserId,
            FileName = fileName,
            FileSizeBytes = 0,
            SignatureCount = 0,
            GroupCount = 0,
            Status = DepExportStatus.Failed.ToString(),
            ErrorMessage = errorMessage,
            ScheduleId = scheduleId,
            IncludeSpecialReceipts = includeSpecialReceipts,
            IncludeDailyClosings = includeDailyClosings,
        };

        _context.DepExportHistories.Add(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryLogAuditAsync(
                new DepExportAuditEntry
                {
                    TenantId = tenantId,
                    Action = DepExportAuditActions.Failed,
                    ExportName = fileName,
                    ExportHistoryId = row.Id,
                    UserId = exportedByUserId,
                    Details = errorMessage,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return row;
    }

    public async Task<DepExportHistoryListResponse> ListAsync(
        Guid tenantId,
        Guid? cashRegisterId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.DepExportHistories.AsNoTracking().Where(h => h.TenantId == tenantId);
        if (cashRegisterId.HasValue)
            query = query.Where(h => h.CashRegisterId == cashRegisterId.Value);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(h => h.ExportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerIds = rows.Select(r => r.CashRegisterId).Distinct().ToList();
        var registerNumbers = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => registerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RegisterNumber })
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken)
            .ConfigureAwait(false);

        return new DepExportHistoryListResponse
        {
            TotalCount = totalCount,
            Items = rows.Select(r => ToResponse(r, registerNumbers.GetValueOrDefault(r.CashRegisterId))).ToList(),
        };
    }

    public async Task<DepExportHistoryResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.DepExportHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return null;

        var registerNumber = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => r.Id == row.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToResponse(row, registerNumber);
    }

    public async Task<(Stream Stream, string FileName, string ContentType)?> TryOpenDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.DepExportHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return null;

        var path = ResolveReadablePath(row);
        if (path is null)
            return null;

        var stream = File.OpenRead(path);
        return (stream, row.FileName, "application/json");
    }

    private static string? ResolveReadablePath(DepExportHistory row)
    {
        if (row.PurgedAt.HasValue)
            return null;

        if (!string.IsNullOrWhiteSpace(row.StoragePath) && File.Exists(row.StoragePath))
            return row.StoragePath;

        if (!string.IsNullOrWhiteSpace(row.ArchivePath) && File.Exists(row.ArchivePath))
            return row.ArchivePath;

        return null;
    }

    /// <summary>Resolves tenant slug + register number for the canonical DEP export file name.</summary>
    public async Task<(string TenantSlug, string RegisterNumber)> ResolveNamingAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var slug = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerNumber = await _context.CashRegisters
            .AsNoTracking()
            .Where(c => c.Id == cashRegisterId)
            .Select(c => c.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return (slug ?? string.Empty, registerNumber ?? string.Empty);
    }

    public async Task<string> BuildFileNameAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default,
        DateTime? at = null)
    {
        var (slug, registerNumber) = await ResolveNamingAsync(tenantId, cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        return _fileNaming.GenerateFileName(
            RksvDepExportFileNames.Prefix,
            "json",
            registerNumber,
            tenantSlug: slug,
            at: at);
    }

    private async Task TryLogAuditAsync(DepExportAuditEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await _audit.LogExportActionAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "DEP export audit log failed for action {Action} history {HistoryId}",
                entry.Action,
                entry.ExportHistoryId);
        }
    }

    private static DepExportHistoryResponse ToResponse(DepExportHistory row, string? registerNumber) =>
        new()
        {
            Id = row.Id,
            CashRegisterId = row.CashRegisterId,
            RegisterNumber = registerNumber,
            FromUtc = row.FromUtc,
            ToUtc = row.ToUtc,
            ExportedAt = row.ExportedAt,
            ExportedByUserId = row.ExportedByUserId,
            FileName = row.FileName,
            FileSizeBytes = row.FileSizeBytes,
            SignatureCount = row.SignatureCount,
            GroupCount = row.GroupCount,
            LegacyJwsCount = row.LegacyJwsCount,
            PrueftoolCompatible = row.LegacyJwsCount == 0,
            Status = Enum.TryParse<DepExportStatus>(row.Status, out var status)
                ? status
                : DepExportStatus.Completed,
            ErrorMessage = row.ErrorMessage,
            ScheduleId = row.ScheduleId,
            IncludeSpecialReceipts = row.IncludeSpecialReceipts,
            IncludeDailyClosings = row.IncludeDailyClosings,
            ValidationStatus = row.ValidationStatus,
            ValidatedAt = row.ValidatedAt,
            ArchivedAt = row.ArchivedAt,
            RetentionUntil = row.RetentionUntil,
            PurgedAt = row.PurgedAt,
            ArchiveChecksum = row.ArchiveChecksum,
            HasArchiveFile = !string.IsNullOrWhiteSpace(row.ArchivePath) &&
                             row.PurgedAt == null &&
                             File.Exists(row.ArchivePath),
            HasStoredFile = ResolveReadablePath(row) is not null,
        };
}
