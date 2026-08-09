using System.Security.Cryptography;
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

    /// <summary>
    /// Optional override for simulation stamp. When null, resolved from <see cref="IRksvEnvironmentService"/>.
    /// </summary>
    public bool? IsSimulated { get; init; }

    /// <summary>Optional override; when null and simulated, uses <see cref="RksvDepExportService.SimulationNoteEn"/>.</summary>
    public string? SimulationNote { get; init; }
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

    /// <summary>Returns the tenant-scoped history entity for download / token APIs.</summary>
    Task<DepExportHistory?> GetExportEntityAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a completed export by opaque download token for the ambient tenant.
    /// Expired / missing / cross-tenant → <c>null</c> (404 semantics).
    /// </summary>
    Task<DepExportHistory?> GetExportEntityByTokenAsync(
        string token,
        Guid ambientTenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Most recent non-purged exports for the tenant (newest first).</summary>
    Task<IReadOnlyList<DepExportHistoryResponse>> GetRecentExportsAsync(
        Guid tenantId,
        int limit = 10,
        Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Latest completed (non-purged) export for the tenant, optionally filtered by register.</summary>
    Task<DepExportLastExportResponse> GetLastExportAsync(
        Guid tenantId,
        Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stored export for the ambient tenant.
    /// Cross-tenant / missing rows → <see cref="DepExportDownloadFailureKind.NotFound"/> (404 semantics).
    /// </summary>
    Task<DepExportDownloadAttempt> TryOpenDownloadAsync(
        Guid id,
        Guid ambientTenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens by opaque download token (24h TTL). Expired tokens →
    /// <see cref="DepExportDownloadFailureKind.TokenExpired"/>.
    /// </summary>
    Task<DepExportDownloadAttempt> TryOpenDownloadByTokenAsync(
        string token,
        Guid ambientTenantId,
        CancellationToken cancellationToken = default);

    Task<DepExportDownloadTokenResponse?> IssueDownloadTokenAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Marks <see cref="DepExportHistory.DownloadedAt"/> after a successful download.</summary>
    Task MarkDownloadedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a recent export for the ambient tenant: removes hot file / tokens.
    /// Failed rows are hard-deleted; archived fiscal rows keep archive metadata.
    /// </summary>
    Task<bool> DeleteRecentExportAsync(
        Guid id,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes hot storage files older than <see cref="DepExportStorageOptions.HotStorageRetentionDays"/>
    /// (keeps archive path), clears expired download tokens, and hard-deletes stale Failed/purged metadata
    /// older than <see cref="DepExportStorageOptions.MetadataRetentionDays"/>.
    /// </summary>
    Task<DepExportStorageCleanupResult> CleanupExpiredStorageAsync(
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
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly IOptionsMonitor<DepExportArchiveOptions> _archiveOptions;
    private readonly IOptionsMonitor<DepExportStorageOptions> _storageOptions;
    private readonly IHostEnvironment _env;
    private readonly ILogger<DepExportHistoryService> _logger;

    public DepExportHistoryService(
        AppDbContext context,
        IFileNamingService fileNaming,
        IDepExportRequirementService requirementService,
        IDepExportValidationService validationService,
        IDepExportArchiveService archiveService,
        IDepExportPushNotificationService pushNotification,
        IDepExportAuditService audit,
        IRksvEnvironmentService rksvEnv,
        IOptionsMonitor<DepExportArchiveOptions> archiveOptions,
        IOptionsMonitor<DepExportStorageOptions> storageOptions,
        IHostEnvironment env,
        ILogger<DepExportHistoryService> logger)
    {
        _context = context;
        _fileNaming = fileNaming;
        _requirementService = requirementService;
        _validationService = validationService;
        _archiveService = archiveService;
        _pushNotification = pushNotification;
        _audit = audit;
        _rksvEnv = rksvEnv;
        _archiveOptions = archiveOptions;
        _storageOptions = storageOptions;
        _env = env;
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

        var storagePath = request.StoragePath;
        if (string.IsNullOrWhiteSpace(storagePath) || !File.Exists(storagePath))
        {
            storagePath = await PersistExportJsonAsync(
                    request.TenantId,
                    fileName,
                    json,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var storageOpts = _storageOptions.CurrentValue;
        string? downloadToken = null;
        DateTime? downloadExpires = null;
        if (storageOpts.IssueDownloadTokenOnComplete)
        {
            downloadToken = CreateDownloadTokenValue();
            downloadExpires = DateTime.UtcNow.AddHours(Math.Clamp(storageOpts.DownloadTokenTtlHours, 1, 168));
        }

        var exportedAt = DateTime.UtcNow;
        var hotDays = Math.Max(1, storageOpts.HotStorageRetentionDays);
        var isSimulated = request.IsSimulated
            ?? (_rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated());
        var simulationNote = isSimulated
            ? (string.IsNullOrWhiteSpace(request.SimulationNote)
                ? RksvDepExportService.SimulationNoteEn
                : request.SimulationNote.Trim())
            : null;

        var row = new DepExportHistory
        {
            TenantId = request.TenantId,
            CashRegisterId = request.CashRegisterId,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            ExportedAt = exportedAt,
            ExportedByUserId = request.ExportedByUserId,
            FileName = fileName,
            FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(json),
            SignatureCount = signatureCount,
            GroupCount = groupCount,
            LegacyJwsCount = legacyJwsCount,
            Status = DepExportStatus.Completed.ToString(),
            StoragePath = storagePath,
            DownloadToken = downloadToken,
            DownloadTokenExpiresAtUtc = downloadExpires,
            ExpiresAt = exportedAt.AddDays(hotDays),
            ScheduleId = request.ScheduleId,
            IncludeSpecialReceipts = request.IncludeSpecialReceipts,
            IncludeDailyClosings = request.IncludeDailyClosings,
            IsSimulated = isSimulated,
            SimulationNote = simulationNote,
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
                        $"from={request.FromUtc:O}; to={request.ToUtc:O}; signatures={signatureCount}; " +
                        $"schedule={request.ScheduleId}; isSimulated={isSimulated}; size={row.FileSizeBytes}",
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
            "DEP export history recorded id={HistoryId} register={RegisterId} signatures={SignatureCount} path={StoragePath}",
            row.Id,
            row.CashRegisterId,
            row.SignatureCount,
            row.StoragePath);

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

        var isSimulated = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
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
            IsSimulated = isSimulated,
            SimulationNote = isSimulated ? RksvDepExportService.SimulationNoteEn : null,
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

        // Soft-purged recent exports are hidden from the list (archive metadata may remain).
        var query = _context.DepExportHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.PurgedAt == null);
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

    public async Task<DepExportHistory?> GetExportEntityAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await _context.DepExportHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<DepExportHistory?> GetExportEntityByTokenAsync(
        string token,
        Guid ambientTenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var normalized = token.Trim();
        var row = await _context.DepExportHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.DownloadToken == normalized, cancellationToken)
            .ConfigureAwait(false);

        // Cross-tenant tokens are invisible under EF filters → null (do not reveal existence).
        if (row is null || row.TenantId != ambientTenantId)
            return null;

        if (row.DownloadTokenExpiresAtUtc is null ||
            row.DownloadTokenExpiresAtUtc.Value < DateTime.UtcNow)
            return null;

        if (row.PurgedAt.HasValue)
            return null;

        return row;
    }

    public async Task<IReadOnlyList<DepExportHistoryResponse>> GetRecentExportsAsync(
        Guid tenantId,
        int limit = 10,
        Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var list = await ListAsync(
                tenantId,
                cashRegisterId,
                page: 1,
                pageSize: limit,
                cancellationToken)
            .ConfigureAwait(false);
        return list.Items;
    }

    public async Task<DepExportLastExportResponse> GetLastExportAsync(
        Guid tenantId,
        Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DepExportHistories
            .AsNoTracking()
            .Where(h =>
                h.TenantId == tenantId &&
                h.PurgedAt == null &&
                h.Status == DepExportStatus.Completed.ToString());

        if (cashRegisterId is { } registerId)
            query = query.Where(h => h.CashRegisterId == registerId);

        var row = await query
            .OrderByDescending(h => h.ExportedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return new DepExportLastExportResponse
            {
                HasExport = false,
            };
        }

        string? registerNumber = null;
        if (row.CashRegisterId != Guid.Empty)
        {
            registerNumber = await _context.CashRegisters
                .AsNoTracking()
                .Where(c => c.Id == row.CashRegisterId)
                .Select(c => c.RegisterNumber)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new DepExportLastExportResponse
        {
            HasExport = true,
            LastExportAt = row.ExportedAt,
            Formatted = row.ExportedAt.ToString("dd.MM.yyyy HH:mm"),
            FileName = row.FileName,
            FileSizeBytes = row.FileSizeBytes,
            IsSimulated = row.IsSimulated,
            DownloadCount = row.DownloadCount,
            ExportId = row.Id,
            CashRegisterId = row.CashRegisterId,
            RegisterNumber = registerNumber,
        };
    }

    public async Task<DepExportDownloadAttempt> TryOpenDownloadAsync(
        Guid id,
        Guid ambientTenantId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.DepExportHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);

        // EF tenant filter already scopes; explicit check preserves 404 semantics if filters ever bypassed.
        if (row is null)
            return DepExportDownloadAttempt.Fail(DepExportDownloadFailureKind.NotFound, id);

        if (row.TenantId != ambientTenantId)
            return DepExportDownloadAttempt.Fail(DepExportDownloadFailureKind.ForbiddenTenant, id, row.FileName);

        if (row.PurgedAt.HasValue)
            return DepExportDownloadAttempt.Fail(DepExportDownloadFailureKind.Purged, row.Id, row.FileName);

        return ResolveReadableDownloadAttempt(row);
    }

    public async Task<DepExportDownloadAttempt> TryOpenDownloadByTokenAsync(
        string token,
        Guid ambientTenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return DepExportDownloadAttempt.Fail(DepExportDownloadFailureKind.NotFound);

        var normalized = token.Trim();
        var row = await _context.DepExportHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.DownloadToken == normalized, cancellationToken)
            .ConfigureAwait(false);

        // Cross-tenant tokens are invisible under EF filters → NotFound (do not reveal existence).
        if (row is null)
            return DepExportDownloadAttempt.Fail(DepExportDownloadFailureKind.NotFound);

        if (row.TenantId != ambientTenantId)
            return DepExportDownloadAttempt.Fail(
                DepExportDownloadFailureKind.ForbiddenTenant,
                row.Id,
                row.FileName);

        if (row.DownloadTokenExpiresAtUtc is null ||
            row.DownloadTokenExpiresAtUtc.Value < DateTime.UtcNow)
        {
            return DepExportDownloadAttempt.Fail(
                DepExportDownloadFailureKind.TokenExpired,
                row.Id,
                row.FileName);
        }

        if (row.PurgedAt.HasValue)
            return DepExportDownloadAttempt.Fail(DepExportDownloadFailureKind.Purged, row.Id, row.FileName);

        return ResolveReadableDownloadAttempt(row);
    }

    /// <summary>
    /// Opens the on-disk file when present. If nothing is readable and the hot retention
    /// window has elapsed → <see cref="DepExportDownloadFailureKind.HotExpired"/> (HTTP 400).
    /// </summary>
    private static DepExportDownloadAttempt ResolveReadableDownloadAttempt(DepExportHistory row)
    {
        var readable = ResolveReadablePath(row);
        if (readable is null)
        {
            if (row.ExpiresAt is { } expiresAt && expiresAt < DateTime.UtcNow)
            {
                return DepExportDownloadAttempt.Fail(
                    DepExportDownloadFailureKind.HotExpired,
                    row.Id,
                    row.FileName);
            }

            return DepExportDownloadAttempt.Fail(
                DepExportDownloadFailureKind.FileMissing,
                row.Id,
                row.FileName);
        }

        return OpenStoredFileAttempt(row);
    }

    public async Task<DepExportDownloadTokenResponse?> IssueDownloadTokenAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.DepExportHistories
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return null;

        if (!string.Equals(row.Status, DepExportStatus.Completed.ToString(), StringComparison.Ordinal))
            return null;

        if (ResolveReadablePath(row) is null)
            return null;

        var ttlHours = Math.Clamp(_storageOptions.CurrentValue.DownloadTokenTtlHours, 1, 168);
        var token = CreateDownloadTokenValue();
        var expires = DateTime.UtcNow.AddHours(ttlHours);

        row.DownloadToken = token;
        row.DownloadTokenExpiresAtUtc = expires;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new DepExportDownloadTokenResponse
        {
            ExportId = row.Id,
            Token = token,
            ExpiresAtUtc = expires,
            DownloadPath = $"/api/admin/rksv/dep-export/download/token/{token}",
            FileName = row.FileName,
        };
    }

    public async Task MarkDownloadedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.DepExportHistories
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        row.DownloadedAt = DateTime.UtcNow;
        row.DownloadCount = checked(row.DownloadCount + 1);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteRecentExportAsync(
        Guid id,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.DepExportHistories
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return false;

        TryDeleteHotFile(row);

        row.DownloadToken = null;
        row.DownloadTokenExpiresAtUtc = null;
        row.StoragePath = null;
        row.ExpiresAt = DateTime.UtcNow;

        var isFailed = string.Equals(row.Status, DepExportStatus.Failed.ToString(), StringComparison.Ordinal);
        var hasArchive = !string.IsNullOrWhiteSpace(row.ArchivePath);

        // Failed rows have no fiscal payload — hard-delete immediately.
        if (isFailed)
        {
            await TryLogAuditAsync(
                    new DepExportAuditEntry
                    {
                        TenantId = row.TenantId,
                        Action = DepExportAuditActions.Deleted,
                        ExportName = row.FileName,
                        ExportHistoryId = row.Id,
                        UserId = actorUserId,
                        Details = "recent-export-hard-delete-failed-row",
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            _context.DepExportHistories.Remove(row);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        // Soft-hide from Recent Exports / download APIs.
        // ArchivePath (if any) is retained for RKSV; metadata without archive is cleaned after MetadataRetentionDays.
        row.PurgedAt ??= DateTime.UtcNow;
        row.PurgeReason ??= "Removed from recent exports by user";

        await TryLogAuditAsync(
                new DepExportAuditEntry
                {
                    TenantId = row.TenantId,
                    Action = DepExportAuditActions.Deleted,
                    ExportName = row.FileName,
                    ExportHistoryId = row.Id,
                    UserId = actorUserId,
                    Details = hasArchive
                        ? "recent-export-soft-purged; archive retained"
                        : "recent-export-soft-purged; awaiting metadata retention",
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<DepExportStorageCleanupResult> CleanupExpiredStorageAsync(
        CancellationToken cancellationToken = default)
    {
        var opts = _storageOptions.CurrentValue;
        var result = new DepExportStorageCleanupResult
        {
            CutoffUtc = DateTime.UtcNow.AddDays(-Math.Max(1, opts.HotStorageRetentionDays)),
        };

        if (!opts.CleanupEnabled)
            return result;

        var batch = Math.Clamp(opts.CleanupMaxBatchSize, 1, 500);
        var hotRoot = ResolveStorageRoot();

        // Clear expired download tokens (all tenants — hosted sweep).
        var expiredTokens = await _context.DepExportHistories
            .IgnoreQueryFilters()
            .Where(h =>
                h.DownloadToken != null &&
                h.DownloadTokenExpiresAtUtc != null &&
                h.DownloadTokenExpiresAtUtc < DateTime.UtcNow)
            .OrderBy(h => h.DownloadTokenExpiresAtUtc)
            .Take(batch)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in expiredTokens)
        {
            row.DownloadToken = null;
            row.DownloadTokenExpiresAtUtc = null;
            result.TokensCleared++;
        }

        // Delete hot storage copies older than HotStorageRetentionDays (default 7).
        // Prefer ExpiresAt when set; otherwise ExportedAt. Archive path (if any) is retained.
        var hotCandidates = await _context.DepExportHistories
            .IgnoreQueryFilters()
            .Where(h =>
                h.PurgedAt == null &&
                h.StoragePath != null &&
                h.StoragePath != "" &&
                ((h.ExpiresAt != null && h.ExpiresAt < DateTime.UtcNow) ||
                 (h.ExpiresAt == null && h.ExportedAt < result.CutoffUtc)))
            .OrderBy(h => h.ExportedAt)
            .Take(batch)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in hotCandidates)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(row.StoragePath))
                    continue;

                var isArchivePath = !string.IsNullOrWhiteSpace(row.ArchivePath) &&
                    string.Equals(row.StoragePath, row.ArchivePath, StringComparison.OrdinalIgnoreCase);
                if (isArchivePath)
                    continue;

                var isHotCopy = row.StoragePath.StartsWith(hotRoot, StringComparison.OrdinalIgnoreCase);
                if (!isHotCopy)
                    continue;

                if (File.Exists(row.StoragePath))
                    File.Delete(row.StoragePath);

                // Prefer archive for continued download; otherwise clear hot path.
                row.StoragePath = !string.IsNullOrWhiteSpace(row.ArchivePath)
                    ? row.ArchivePath
                    : null;
                result.HotFilesDeleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.FailedCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to delete expired DEP hot storage for history {HistoryId}",
                    row.Id);
            }
        }

        // Hard-delete Failed / purged-without-archive metadata older than MetadataRetentionDays.
        // Completed+archived fiscal rows stay until the 7-year archive purge.
        var metaCutoff = DateTime.UtcNow.AddDays(-Math.Max(1, opts.MetadataRetentionDays));
        var stale = await _context.DepExportHistories
            .IgnoreQueryFilters()
            .Where(h =>
                (h.Status == DepExportStatus.Failed.ToString() && h.ExportedAt < metaCutoff) ||
                (h.PurgedAt != null &&
                 h.PurgedAt < metaCutoff &&
                 (h.ArchivePath == null || h.ArchivePath == "")))
            .OrderBy(h => h.ExportedAt)
            .Take(batch)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in stale)
        {
            try
            {
                TryDeleteHotFile(row);
                if (!string.IsNullOrWhiteSpace(row.ArchivePath) && File.Exists(row.ArchivePath))
                {
                    try { File.Delete(row.ArchivePath); }
                    catch (IOException) { /* best-effort */ }
                    catch (UnauthorizedAccessException) { /* best-effort */ }
                }

                _context.DepExportHistories.Remove(row);
                result.MetadataRowsDeleted++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                _logger.LogWarning(ex, "Failed to delete stale DEP history {HistoryId}", row.Id);
            }
        }

        if (result.TokensCleared > 0 ||
            result.HotFilesDeleted > 0 ||
            result.MetadataRowsDeleted > 0 ||
            result.FailedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void TryDeleteHotFile(DepExportHistory row)
    {
        if (string.IsNullOrWhiteSpace(row.StoragePath))
            return;
        if (!string.IsNullOrWhiteSpace(row.ArchivePath) &&
            string.Equals(row.StoragePath, row.ArchivePath, StringComparison.OrdinalIgnoreCase))
            return;
        if (!File.Exists(row.StoragePath))
            return;
        try
        {
            File.Delete(row.StoragePath);
        }
        catch (IOException)
        {
            // best-effort
        }
        catch (UnauthorizedAccessException)
        {
            // best-effort
        }
    }

    private static DepExportDownloadAttempt OpenStoredFileAttempt(DepExportHistory row)
    {
        var path = ResolveReadablePath(row);
        if (path is null)
            return DepExportDownloadAttempt.Fail(DepExportDownloadFailureKind.FileMissing, row.Id, row.FileName);

        var stream = File.OpenRead(path);
        return DepExportDownloadAttempt.Success(
            new DepExportDownloadOpen
            {
                Stream = stream,
                FileName = row.FileName,
                ContentType = "application/json",
                ExportId = row.Id,
                TenantId = row.TenantId,
            });
    }

    private async Task<string> PersistExportJsonAsync(
        Guid tenantId,
        string fileName,
        string json,
        CancellationToken cancellationToken)
    {
        var root = ResolveStorageRoot();
        var dir = Path.Combine(root, tenantId.ToString("D"));
        Directory.CreateDirectory(dir);

        var safeName = SanitizeFileName(fileName);
        var path = Path.Combine(dir, safeName);
        if (File.Exists(path))
        {
            path = Path.Combine(
                dir,
                $"{Path.GetFileNameWithoutExtension(safeName)}_{Guid.NewGuid():N}.json");
        }

        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        return path;
    }

    private string ResolveStorageRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_storageOptions.CurrentValue.StorageRootRelativeDirectory)
            ? "App_Data/dep-exports"
            : _storageOptions.CurrentValue.StorageRootRelativeDirectory.Trim();

        if (Path.IsPathRooted(configured))
            return configured;

        return Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }

    private static string CreateDownloadTokenValue() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName).Trim();
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            return $"dep-export_{Guid.NewGuid():N}.json";

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
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

    private static bool HasActiveDownloadToken(DepExportHistory row) =>
        !string.IsNullOrWhiteSpace(row.DownloadToken) &&
        row.DownloadTokenExpiresAtUtc is { } expires &&
        expires >= DateTime.UtcNow;

    private static DepExportHistoryResponse ToResponse(DepExportHistory row, string? registerNumber)
    {
        var hasStoredFile = ResolveReadablePath(row) is not null;
        return new()
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
            IsSimulated = row.IsSimulated,
            SimulationNote = row.SimulationNote,
            ValidationStatus = row.ValidationStatus,
            ValidatedAt = row.ValidatedAt,
            ArchivedAt = row.ArchivedAt,
            RetentionUntil = row.RetentionUntil,
            PurgedAt = row.PurgedAt,
            ArchiveChecksum = row.ArchiveChecksum,
            HasArchiveFile = !string.IsNullOrWhiteSpace(row.ArchivePath) &&
                             row.PurgedAt == null &&
                             File.Exists(row.ArchivePath),
            HasStoredFile = hasStoredFile,
            HasActiveDownloadToken = HasActiveDownloadToken(row),
            DownloadTokenExpiresAtUtc = row.DownloadTokenExpiresAtUtc,
            DownloadUrl = hasStoredFile
                ? $"/api/admin/rksv/dep-export/download/{row.Id:D}"
                : null,
            ExpiresAt = row.ExpiresAt,
            DownloadedAt = row.DownloadedAt,
            DownloadCount = row.DownloadCount,
            CanDelete = row.PurgedAt == null,
        };
    }
}
