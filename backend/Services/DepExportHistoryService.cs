using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using Microsoft.EntityFrameworkCore;

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
}

public sealed class DepExportHistoryService : IDepExportHistoryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DepExportHistoryService> _logger;

    public DepExportHistoryService(AppDbContext context, ILogger<DepExportHistoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DepExportHistory> RecordCompletedAsync(
        DepExportHistoryRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var (groupCount, signatureCount) = RksvDepExportStats.Count(request.Export);
        var json = JsonSerializer.Serialize(request.Export);
        var fileName = BuildFileName(request.CashRegisterId, request.FromUtc, request.ToUtc);

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
            Status = DepExportStatus.Completed.ToString(),
            StoragePath = request.StoragePath,
            ScheduleId = request.ScheduleId,
            IncludeSpecialReceipts = request.IncludeSpecialReceipts,
            IncludeDailyClosings = request.IncludeDailyClosings,
        };

        _context.DepExportHistories.Add(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
        var row = new DepExportHistory
        {
            TenantId = tenantId,
            CashRegisterId = cashRegisterId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            ExportedAt = DateTime.UtcNow,
            ExportedByUserId = exportedByUserId,
            FileName = BuildFileName(cashRegisterId, fromUtc, toUtc),
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
        if (row is null || string.IsNullOrWhiteSpace(row.StoragePath) || !File.Exists(row.StoragePath))
            return null;

        var stream = File.OpenRead(row.StoragePath);
        return (stream, row.FileName, "application/json");
    }

    internal static string BuildFileName(Guid cashRegisterId, DateTime fromUtc, DateTime toUtc) =>
        $"dep-export-{cashRegisterId:N}-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}.json";

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
            Status = Enum.TryParse<DepExportStatus>(row.Status, out var status)
                ? status
                : DepExportStatus.Completed,
            ErrorMessage = row.ErrorMessage,
            HasStoredFile = !string.IsNullOrWhiteSpace(row.StoragePath) && File.Exists(row.StoragePath),
            ScheduleId = row.ScheduleId,
            IncludeSpecialReceipts = row.IncludeSpecialReceipts,
            IncludeDailyClosings = row.IncludeDailyClosings,
        };
}
