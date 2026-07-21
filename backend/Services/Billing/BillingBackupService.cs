using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Billing;

public sealed class BillingBackupService : IBillingBackupService
{
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly AppDbContext _dbContext;
    private readonly IBillingService _billingService;
    private readonly IInvoicePdfGenerator _pdfGenerator;
    private readonly BillingBackupConfig _config;
    private readonly string _basePath;
    private readonly ILogger<BillingBackupService> _logger;

    public BillingBackupService(
        AppDbContext dbContext,
        IBillingService billingService,
        IInvoicePdfGenerator pdfGenerator,
        IOptions<BillingBackupConfig> config,
        IWebHostEnvironment environment,
        ILogger<BillingBackupService> logger)
    {
        _dbContext = dbContext;
        _billingService = billingService;
        _pdfGenerator = pdfGenerator;
        _config = config.Value;
        _logger = logger;
        _basePath = ResolveBasePath(environment.ContentRootPath, _config.BasePath);

        if (_config.Enabled)
        {
            Directory.CreateDirectory(Path.Combine(_basePath, BillingBackupTypes.Sale));
            Directory.CreateDirectory(Path.Combine(_basePath, BillingBackupTypes.Daily));
            Directory.CreateDirectory(Path.Combine(_basePath, BillingBackupTypes.Weekly));
            Directory.CreateDirectory(Path.Combine(_basePath, BillingBackupTypes.Full));
        }
    }

    public async Task<BackupResult> BackupSaleAsync(
        Guid saleId,
        Guid? triggeredByUserId = null,
        CancellationToken ct = default)
    {
        var result = new BackupResult { BackupRunId = GenerateBackupRunId() };
        var startTime = DateTime.UtcNow;

        if (!_config.Enabled)
        {
            result.Success = false;
            result.Errors.Add("Backup is disabled");
            return result;
        }

        try
        {
            var sale = await _billingService.GetLicenseSaleAsync(saleId, ct).ConfigureAwait(false);
            var pdfBytes = await _pdfGenerator.GenerateInvoicePdfAsync(saleId, ct).ConfigureAwait(false);

            var backupData = new
            {
                Sale = sale,
                GeneratedAt = DateTime.UtcNow,
                result.BackupRunId,
                Version = "1.0",
            };

            var json = JsonSerializer.Serialize(backupData, JsonOptions);
            var salePath = Path.Combine(_basePath, BillingBackupTypes.Sale, sale.InvoiceNumber);
            Directory.CreateDirectory(salePath);

            var jsonPath = Path.Combine(salePath, "data.json");
            var pdfPath = Path.Combine(salePath, "invoice.pdf");

            await File.WriteAllTextAsync(jsonPath, json, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(pdfPath, pdfBytes, ct).ConfigureAwait(false);

            var hash = ComputeHash(json);
            var fileSize = pdfBytes.Length + Encoding.UTF8.GetByteCount(json);

            var db = _dbContext;
            var history = CreateHistoryRecord(
                result.BackupRunId,
                BillingBackupTypes.Sale,
                salePath,
                fileSize,
                hash,
                recordCount: 1,
                BillingBackupStatuses.Success,
                saleId,
                triggeredByUserId,
                startTime,
                errorMessage: null);

            db.BillingBackupHistories.Add(history);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            result.Success = true;
            result.RecordCount = 1;
            result.BackupPath = salePath;
            result.FileHash = hash;
            result.FileSizeBytes = fileSize;
            result.CompletedAtUtc = DateTime.UtcNow;

            _logger.LogInformation("Sale backup completed: {InvoiceNumber}", sale.InvoiceNumber);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Sale backup failed for sale {SaleId}", saleId);

            await LogFailedBackupAsync(
                result.BackupRunId,
                BillingBackupTypes.Sale,
                saleId,
                triggeredByUserId,
                startTime,
                ex.Message,
                ct).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<BackupResult> BackupDailyAsync(
        DateTime date,
        Guid? triggeredByUserId = null,
        CancellationToken ct = default)
    {
        var (dayStart, dayEnd) = GetUtcDayRange(date);
        var folderName = dayStart.ToString("yyyy-MM-dd");
        var zipName = $"{folderName}.zip";

        return await CreatePeriodArchiveAsync(
            BillingBackupTypes.Daily,
            Path.Combine(_basePath, BillingBackupTypes.Daily, folderName),
            Path.Combine(_basePath, BillingBackupTypes.Daily, zipName),
            sales => sales.Where(s => s.SoldAtUtc >= dayStart && s.SoldAtUtc < dayEnd),
            summary => new
            {
                Date = dayStart,
                TotalSales = summary.RecordCount,
                Sales = summary.Sales,
                summary.BackupRunId,
                GeneratedAt = DateTime.UtcNow,
            },
            triggeredByUserId,
            ct).ConfigureAwait(false);
    }

    public async Task<BackupResult> BackupWeeklyAsync(
        DateTime weekStart,
        Guid? triggeredByUserId = null,
        CancellationToken ct = default)
    {
        var start = DateTime.SpecifyKind(weekStart.Date, DateTimeKind.Utc);
        var end = start.AddDays(7);
        var folderName = start.ToString("yyyy-MM-dd");
        var zipName = $"{folderName}_week.zip";

        return await CreatePeriodArchiveAsync(
            BillingBackupTypes.Weekly,
            Path.Combine(_basePath, BillingBackupTypes.Weekly, folderName),
            Path.Combine(_basePath, BillingBackupTypes.Weekly, zipName),
            sales => sales.Where(s => s.SoldAtUtc >= start && s.SoldAtUtc < end),
            summary => new
            {
                WeekStart = start,
                WeekEnd = end,
                TotalSales = summary.RecordCount,
                Sales = summary.Sales,
                summary.BackupRunId,
                GeneratedAt = DateTime.UtcNow,
            },
            triggeredByUserId,
            ct).ConfigureAwait(false);
    }

    public async Task<BackupResult> BackupFullAsync(
        Guid? triggeredByUserId = null,
        CancellationToken ct = default)
    {
        var folderName = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
        var zipName = $"{folderName}_full.zip";

        return await CreatePeriodArchiveAsync(
            BillingBackupTypes.Full,
            Path.Combine(_basePath, BillingBackupTypes.Full, folderName),
            Path.Combine(_basePath, BillingBackupTypes.Full, zipName),
            sales => sales,
            summary => new
            {
                GeneratedAt = DateTime.UtcNow,
                TotalSales = summary.RecordCount,
                Sales = summary.Sales,
                summary.BackupRunId,
                Version = "1.0",
            },
            triggeredByUserId,
            ct).ConfigureAwait(false);
    }

    public async Task<BackupHistoryListResponse> ListBackupHistoryAsync(
        BackupHistoryQuery query,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var historyQuery = db.BillingBackupHistories
            .Include(h => h.Sale)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.BackupType))
        {
            var backupType = query.BackupType.Trim();
            if (!BillingBackupTypes.IsValid(backupType))
                throw new ArgumentException("Invalid backup type filter.", nameof(query));

            historyQuery = historyQuery.Where(h => h.BackupType == backupType);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            if (!BillingBackupStatuses.IsValid(status))
                throw new ArgumentException("Invalid status filter.", nameof(query));

            historyQuery = historyQuery.Where(h => h.Status == status);
        }

        if (query.SaleId.HasValue)
            historyQuery = historyQuery.Where(h => h.SaleId == query.SaleId.Value);

        if (query.FromDate.HasValue)
            historyQuery = historyQuery.Where(h => h.StartedAtUtc >= ToUtcInstant(query.FromDate.Value));

        if (query.ToDate.HasValue)
            historyQuery = historyQuery.Where(h => h.StartedAtUtc <= ToUtcInstant(query.ToDate.Value));

        var totalCount = await historyQuery.CountAsync(ct).ConfigureAwait(false);
        var items = await historyQuery
            .OrderByDescending(h => h.StartedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new BackupHistoryListResponse
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
        };
    }

    public async Task<BackupHistoryResponse> GetBackupDetailsAsync(
        Guid backupId,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var history = await db.BillingBackupHistories
            .Include(h => h.Sale)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == backupId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Backup {backupId} not found");

        return MapToResponse(history);
    }

    public async Task<byte[]> DownloadBackupFileAsync(
        Guid backupId,
        CancellationToken ct = default)
    {
        var history = await GetBackupDetailsAsync(backupId, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(history.BackupPath))
            throw new FileNotFoundException($"Backup file not found for {backupId}");

        if (File.Exists(history.BackupPath))
            return await File.ReadAllBytesAsync(history.BackupPath, ct).ConfigureAwait(false);

        if (Directory.Exists(history.BackupPath))
        {
            var tempZip = Path.Combine(Path.GetTempPath(), $"billing-backup-{backupId:N}.zip");
            try
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);

                ZipFile.CreateFromDirectory(history.BackupPath, tempZip, CompressionLevel.Optimal, false);
                return await File.ReadAllBytesAsync(tempZip, ct).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
        }

        throw new FileNotFoundException($"Backup file not found: {history.BackupPath}");
    }

    public async Task<int> CleanupExpiredBackupsAsync(CancellationToken ct = default)
    {
        var db = _dbContext;

        var now = DateTime.UtcNow;
        var expired = await db.BillingBackupHistories
            .Where(h => h.RetentionUntilUtc != null && h.RetentionUntilUtc <= now)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var deleted = 0;
        foreach (var item in expired)
        {
            try
            {
                if (Directory.Exists(item.BackupPath))
                    Directory.Delete(item.BackupPath, recursive: true);
                else if (File.Exists(item.BackupPath))
                    File.Delete(item.BackupPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete backup files for {BackupId}", item.Id);
            }

            db.BillingBackupHistories.Remove(item);
            deleted++;
        }

        if (deleted > 0)
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Cleaned up {Count} expired billing backups", deleted);
        return deleted;
    }

    #region Private Methods

    private sealed record PeriodArchiveSummary(string BackupRunId, int RecordCount, List<LicenseSale> Sales);

    private async Task<BackupResult> CreatePeriodArchiveAsync(
        string backupType,
        string workingDirectory,
        string zipPath,
        Func<IQueryable<LicenseSale>, IQueryable<LicenseSale>> salesFilter,
        Func<PeriodArchiveSummary, object> summaryFactory,
        Guid? triggeredByUserId,
        CancellationToken ct)
    {
        var result = new BackupResult { BackupRunId = GenerateBackupRunId() };
        var startTime = DateTime.UtcNow;

        if (!_config.Enabled)
        {
            result.Success = false;
            result.Errors.Add("Backup is disabled");
            return result;
        }

        try
        {
            var db = _dbContext;

            var salesQuery = salesFilter(db.LicenseSales.IgnoreQueryFilters());
            var sales = await salesQuery
                .OrderBy(s => s.SoldAtUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (sales.Count == 0)
            {
                result.Success = true;
                result.RecordCount = 0;
                result.CompletedAtUtc = DateTime.UtcNow;
                _logger.LogInformation("No sales found for {BackupType} backup", backupType);
                return result;
            }

            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, recursive: true);

            Directory.CreateDirectory(workingDirectory);

            var exportFailures = 0;
            foreach (var sale in sales)
            {
                try
                {
                    var pdfBytes = await _pdfGenerator.GenerateInvoicePdfAsync(sale.Id, ct).ConfigureAwait(false);
                    var pdfPath = Path.Combine(workingDirectory, $"{sale.InvoiceNumber}.pdf");
                    await File.WriteAllBytesAsync(pdfPath, pdfBytes, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exportFailures++;
                    _logger.LogWarning(ex, "Could not export PDF for sale {SaleId}", sale.Id);
                }
            }

            var summary = summaryFactory(new PeriodArchiveSummary(result.BackupRunId, sales.Count, sales));
            var json = JsonSerializer.Serialize(summary, JsonOptions);
            var jsonPath = Path.Combine(workingDirectory, "summary.json");
            await File.WriteAllTextAsync(jsonPath, json, ct).ConfigureAwait(false);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(workingDirectory, zipPath, CompressionLevel.Optimal, false);

            var hash = ComputeHash(json);
            var fileSize = new FileInfo(zipPath).Length;
            var status = exportFailures == 0
                ? BillingBackupStatuses.Success
                : exportFailures < sales.Count
                    ? BillingBackupStatuses.Partial
                    : BillingBackupStatuses.Failed;

            var history = CreateHistoryRecord(
                result.BackupRunId,
                backupType,
                zipPath,
                fileSize,
                hash,
                sales.Count,
                status,
                saleId: null,
                triggeredByUserId,
                startTime,
                exportFailures > 0 ? $"{exportFailures} PDF export(s) failed." : null);

            db.BillingBackupHistories.Add(history);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            Directory.Delete(workingDirectory, recursive: true);

            result.Success = status != BillingBackupStatuses.Failed;
            result.RecordCount = sales.Count;
            result.BackupPath = zipPath;
            result.FileHash = hash;
            result.FileSizeBytes = fileSize;
            result.CompletedAtUtc = DateTime.UtcNow;

            if (exportFailures > 0)
                result.Errors.Add($"{exportFailures} PDF export(s) failed.");

            _logger.LogInformation(
                "{BackupType} backup completed with {Count} sales ({Status})",
                backupType,
                sales.Count,
                status);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "{BackupType} backup failed", backupType);

            await LogFailedBackupAsync(
                result.BackupRunId,
                backupType,
                saleId: null,
                triggeredByUserId,
                startTime,
                ex.Message,
                ct).ConfigureAwait(false);
        }

        return result;
    }

    private BillingBackupHistory CreateHistoryRecord(
        string backupRunId,
        string backupType,
        string backupPath,
        long fileSizeBytes,
        string fileHash,
        int recordCount,
        string status,
        Guid? saleId,
        Guid? triggeredByUserId,
        DateTime startedAtUtc,
        string? errorMessage)
    {
        var completedAt = DateTime.UtcNow;
        return new BillingBackupHistory
        {
            Id = Guid.NewGuid(),
            BackupRunId = backupRunId,
            SaleId = saleId,
            BackupType = backupType,
            BackupPath = backupPath,
            FileSizeBytes = fileSizeBytes,
            FileHash = fileHash,
            RecordCount = recordCount,
            Status = status,
            ErrorMessage = errorMessage,
            TriggeredByUserId = triggeredByUserId,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAt,
            RetentionUntilUtc = status == BillingBackupStatuses.Failed
                ? null
                : completedAt.AddYears(_config.RetentionYears),
            CreatedAt = completedAt,
        };
    }

    private async Task LogFailedBackupAsync(
        string backupRunId,
        string backupType,
        Guid? saleId,
        Guid? triggeredByUserId,
        DateTime startedAtUtc,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            var db = _dbContext;
            db.BillingBackupHistories.Add(new BillingBackupHistory
            {
                Id = Guid.NewGuid(),
                BackupRunId = backupRunId,
                SaleId = saleId,
                BackupType = backupType,
                BackupPath = string.Empty,
                FileSizeBytes = 0,
                FileHash = string.Empty,
                RecordCount = 0,
                Status = BillingBackupStatuses.Failed,
                ErrorMessage = errorMessage,
                TriggeredByUserId = triggeredByUserId,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTime.UtcNow,
                RetentionUntilUtc = null,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to persist billing backup failure for run {BackupRunId}", backupRunId);
        }
    }

    private static string ResolveBasePath(string contentRootPath, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        var trimmed = configuredPath.TrimStart('.', '/', '\\');
        return Path.Combine(contentRootPath, trimmed);
    }

    private static (DateTime Start, DateTime End) GetUtcDayRange(DateTime date)
    {
        var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        return (start, start.AddDays(1));
    }

    private static DateTime ToUtcInstant(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private string GenerateBackupRunId()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var seq = DateTime.UtcNow.Ticks.ToString()[^4..];
        return $"BAK-{date}-{seq}";
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private BackupHistoryResponse MapToResponse(BillingBackupHistory history) =>
        new()
        {
            Id = history.Id,
            BackupRunId = history.BackupRunId,
            SaleId = history.SaleId,
            InvoiceNumber = history.Sale?.InvoiceNumber,
            BackupType = history.BackupType,
            BackupPath = history.BackupPath,
            FileSizeBytes = history.FileSizeBytes,
            FileHash = history.FileHash,
            RecordCount = history.RecordCount,
            Status = history.Status,
            ErrorMessage = history.ErrorMessage,
            TriggeredBy = history.TriggeredByUserId.HasValue
                ? history.TriggeredByUserId.Value.ToString("D")
                : "System",
            StartedAtUtc = history.StartedAtUtc,
            CompletedAtUtc = history.CompletedAtUtc,
            RetentionUntilUtc = history.RetentionUntilUtc,
        };

    #endregion
}
