using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataAccess;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataRetention;
using KasseAPI_Final.Services.DataRights;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.DataExport;

public sealed class ExportResult
{
    public bool Succeeded { get; init; } = true;
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public string? Status { get; init; }
    public Guid? TenantId { get; init; }
    public string? FileName { get; init; }
    public byte[]? Data { get; init; }
    public long? FileSize { get; init; }
    public IReadOnlyDictionary<string, int>? TableRowCounts { get; init; }
    public DateTime ExportedAtUtc { get; init; } = DateTime.UtcNow;
    public Guid? RequestId { get; init; }
    public string? Link { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? DownloadToken { get; init; }

    public static ExportResult Fail(string error, string? code = null) =>
        new()
        {
            Succeeded = false,
            Error = error,
            ErrorCode = code,
        };

    public static ExportResult Success(Guid requestId, Guid? tenantId = null, string? status = null) =>
        new()
        {
            Succeeded = true,
            RequestId = requestId,
            TenantId = tenantId,
            Status = status,
        };
}

public static class DataExportErrorCodes
{
    public const string NotFound = "not_found";
    public const string NotReady = "not_ready";
    public const string InvalidType = "invalid_type";
    public const string ArtifactMissing = "artifact_missing";
}

public interface IDataExportService
{
    /// <summary>Legacy/sync ZIP bytes for a tenant (no download token).</summary>
    Task<ExportResult> ExportAllDataAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Creates a GDPR export request, builds the ZIP when possible, stores artifact + 7-day download link, notifies requester.
    /// Background retry via <c>DataRightsExportProcessorService</c> if packaging is not ready immediately.
    /// </summary>
    Task<ExportResult> RequestDataExportAsync(
        Guid tenantId,
        string? requestedByUserId = null,
        CancellationToken ct = default);

    /// <summary>Status of an export rights request (id = <see cref="ExportResult.RequestId"/>).</summary>
    Task<ExportResult> GetExportStatusAsync(Guid exportId, CancellationToken ct = default);

    /// <summary>Authenticated ZIP download for a ready/completed export request.</summary>
    Task<ExportResult> DownloadExportAsync(Guid exportId, CancellationToken ct = default);

    /// <summary>
    /// Request-scoped export: collect → ZIP → secure store → 7-day download link → notify requester.
    /// </summary>
    Task<ExportResult> CreateExportAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>Resolve a non-expired download token to ZIP bytes.</summary>
    Task<ExportResult?> GetExportByDownloadTokenAsync(string token, CancellationToken ct = default);

    Task<TenantDataManagementSummaryDto> GetSummaryAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Builds the canonical GDPR export document (used by ZIP packaging and tests).</summary>
    Task<TenantDataExportDocument> BuildExportDocumentAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// GDPR / expired-license tenant data export.
/// Produces a ZIP containing <c>data-export.json</c> (v2 document) and <c>manifest.json</c>
/// (exportId, tenantSlug, exportedAt, fileName). RKSV rows are included masked; Identity credentials are excluded.
/// </summary>
public sealed class DataExportService : IDataExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = true,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILicenseLifecycleResolver _lifecycle;
    private readonly IRksvDataRetentionService _retention;
    private readonly IDataRightsArtifactStore _artifacts;
    private readonly IDataAccessNotificationService _notificationService;
    private readonly IOptions<DataExportOptions> _options;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<DataExportService> _logger;

    public DataExportService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILicenseLifecycleResolver lifecycle,
        IRksvDataRetentionService retention,
        IDataRightsArtifactStore artifacts,
        IDataAccessNotificationService notificationService,
        IOptions<DataExportOptions> options,
        IFileNamingService fileNaming,
        ILogger<DataExportService> logger)
    {
        _dbFactory = dbFactory;
        _lifecycle = lifecycle;
        _retention = retention;
        _artifacts = artifacts;
        _notificationService = notificationService;
        _options = options;
        _fileNaming = fileNaming;
        _logger = logger;
    }

    public async Task<ExportResult> ExportAllDataAsync(Guid tenantId, CancellationToken ct = default)
    {
        var document = await CollectAllDataAsync(tenantId, ct).ConfigureAwait(false);
        var exportId = Guid.NewGuid();
        var fileName = _fileNaming.GenerateFileName(
            DataExportFileNames.Prefix,
            "zip",
            tenantSlug: document.Tenant.Slug);
        var zip = await CreateZipAsync(document, exportId, fileName, ct).ConfigureAwait(false);
        var counts = CountDocument(document);

        _logger.LogInformation(
            "Tenant data export created (v2). TenantId={TenantId}, Bytes={Bytes}, Tables={TableCount}, FileName={FileName}",
            tenantId,
            zip.Length,
            counts.Count,
            fileName);

        return new ExportResult
        {
            Succeeded = true,
            TenantId = tenantId,
            RequestId = exportId,
            Status = TenantDataRightsRequestStatuses.Completed,
            FileName = fileName,
            Data = zip,
            FileSize = zip.LongLength,
            TableRowCounts = counts,
            ExportedAtUtc = document.Tenant.ExportedAt,
        };
    }

    public async Task<ExportResult> RequestDataExportAsync(
        Guid tenantId,
        string? requestedByUserId = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenantExists = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        if (!tenantExists)
            return ExportResult.Fail("Tenant not found", DataExportErrorCodes.NotFound);

        var now = DateTime.UtcNow;
        var row = new TenantDataRightsRequest
        {
            TenantId = tenantId,
            RequestType = TenantDataRightsRequestTypes.Export,
            Status = TenantDataRightsRequestStatuses.Processing,
            ApprovalMode = TenantDataRightsApprovalModes.Auto,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = now,
            ApprovedAtUtc = now,
            ProcessingDeadlineUtc = now.AddHours(CustomerDataRightsService.ExportMaxProcessingHours),
            CreatedAt = now,
        };

        db.TenantDataRightsRequests.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            var packaged = await CreateExportAsync(row.Id, ct).ConfigureAwait(false);
            packaged = new ExportResult
            {
                Succeeded = true,
                TenantId = tenantId,
                RequestId = row.Id,
                Status = TenantDataRightsRequestStatuses.Ready,
                FileName = packaged.FileName,
                Data = packaged.Data,
                FileSize = packaged.Data?.LongLength ?? packaged.FileSize,
                TableRowCounts = packaged.TableRowCounts,
                ExportedAtUtc = packaged.ExportedAtUtc,
                Link = packaged.Link,
                ExpiresAt = packaged.ExpiresAt,
                DownloadToken = packaged.DownloadToken,
            };
            return packaged;
        }
        catch (Exception ex)
        {
            // Keep processing for background retry within 24h SLA (DataRightsExportProcessorService).
            await using var db2 = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var pending = await db2.TenantDataRightsRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == row.Id, ct)
                .ConfigureAwait(false);
            if (pending != null)
            {
                pending.Status = TenantDataRightsRequestStatuses.Processing;
                pending.ErrorMessage = Truncate(ex.Message, 1000);
                pending.UpdatedAt = DateTime.UtcNow;
                await db2.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            _logger.LogWarning(
                ex,
                "Export not ready immediately; queued for retry. RequestId={RequestId}, TenantId={TenantId}",
                row.Id,
                tenantId);

            return new ExportResult
            {
                Succeeded = true,
                TenantId = tenantId,
                RequestId = row.Id,
                Status = TenantDataRightsRequestStatuses.Processing,
                Error = Truncate(ex.Message, 500),
            };
        }
    }

    public async Task<ExportResult> GetExportStatusAsync(Guid exportId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.TenantDataRightsRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == exportId, ct)
            .ConfigureAwait(false);

        if (row == null)
            return ExportResult.Fail("Export request not found", DataExportErrorCodes.NotFound);

        if (!string.Equals(row.RequestType, TenantDataRightsRequestTypes.Export, StringComparison.Ordinal))
            return ExportResult.Fail("Request is not an export", DataExportErrorCodes.InvalidType);

        return new ExportResult
        {
            Succeeded = true,
            TenantId = row.TenantId,
            RequestId = row.Id,
            Status = row.Status,
            FileName = row.ArtifactFileName,
            FileSize = row.ArtifactByteSize,
            ExportedAtUtc = row.ReadyAtUtc ?? row.RequestedAtUtc,
            Link = string.IsNullOrWhiteSpace(row.DownloadToken)
                ? null
                : BuildDownloadLink(_options.Value, row.DownloadToken),
            ExpiresAt = row.DownloadExpiresAtUtc,
            DownloadToken = row.DownloadToken,
            Error = row.ErrorMessage,
        };
    }

    public async Task<ExportResult> DownloadExportAsync(Guid exportId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == exportId, ct)
            .ConfigureAwait(false);

        if (row == null)
            return ExportResult.Fail("Export request not found", DataExportErrorCodes.NotFound);

        if (!string.Equals(row.RequestType, TenantDataRightsRequestTypes.Export, StringComparison.Ordinal))
            return ExportResult.Fail("Request is not an export", DataExportErrorCodes.InvalidType);

        if (row.Status is not (TenantDataRightsRequestStatuses.Ready or TenantDataRightsRequestStatuses.Completed)
            || string.IsNullOrWhiteSpace(row.ArtifactRelativePath))
        {
            return ExportResult.Fail("Export is not ready for download", DataExportErrorCodes.NotReady);
        }

        var bytes = await _artifacts.ReadAsync(row.ArtifactRelativePath, ct).ConfigureAwait(false);
        if (bytes == null)
            return ExportResult.Fail("Export artifact missing", DataExportErrorCodes.ArtifactMissing);

        if (row.Status == TenantDataRightsRequestStatuses.Ready)
        {
            row.Status = TenantDataRightsRequestStatuses.Completed;
            row.CompletedAtUtc = DateTime.UtcNow;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return new ExportResult
        {
            Succeeded = true,
            TenantId = row.TenantId,
            RequestId = row.Id,
            Status = TenantDataRightsRequestStatuses.Completed,
            FileName = row.ArtifactFileName
                ?? _fileNaming.GenerateFileName(DataExportFileNames.Prefix, "zip"),
            Data = bytes,
            FileSize = bytes.LongLength,
            ExportedAtUtc = row.ReadyAtUtc ?? row.RequestedAtUtc,
            Link = string.IsNullOrWhiteSpace(row.DownloadToken)
                ? null
                : BuildDownloadLink(_options.Value, row.DownloadToken),
            ExpiresAt = row.DownloadExpiresAtUtc,
            DownloadToken = row.DownloadToken,
        };
    }

    public async Task<ExportResult> CreateExportAsync(Guid requestId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var request = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Data access request not found.");

        if (!string.Equals(request.RequestType, TenantDataRightsRequestTypes.Export, StringComparison.Ordinal))
            throw new InvalidOperationException("Only export requests can create download packages.");

        request.Status = TenantDataRightsRequestStatuses.Processing;
        request.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // 1. Collect all data
        var data = await CollectAllDataAsync(request.TenantId, ct).ConfigureAwait(false);

        // 2. Create ZIP (data-export.json + manifest.json)
        var fileName = _fileNaming.GenerateFileName(
            DataExportFileNames.Prefix,
            "zip",
            tenantSlug: data.Tenant.Slug);
        var zip = await CreateZipAsync(data, request.Id, fileName, ct).ConfigureAwait(false);

        // 3. Save to secure location
        if (!string.IsNullOrWhiteSpace(request.ArtifactRelativePath))
            _artifacts.TryDelete(request.ArtifactRelativePath);

        var path = await _artifacts
            .SaveExportAsync(request.TenantId, request.Id, zip, ct)
            .ConfigureAwait(false);

        var opts = _options.Value;
        var validDays = Math.Clamp(opts.DownloadLinkValidDays, 1, 30);
        var expiresAt = DateTime.UtcNow.AddDays(validDays);
        var token = Guid.NewGuid().ToString("N");
        var link = BuildDownloadLink(opts, token);

        request.ArtifactRelativePath = path;
        request.ArtifactFileName = fileName;
        request.ArtifactByteSize = zip.LongLength;
        request.DownloadToken = token;
        request.DownloadExpiresAtUtc = expiresAt;
        request.Status = TenantDataRightsRequestStatuses.Ready;
        request.ReadyAtUtc = DateTime.UtcNow;
        request.ErrorMessage = null;
        request.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // 5. Notify user (German HTML + plain export-ready template)
        string? adminName = null;
        if (!string.IsNullOrWhiteSpace(request.RequestedByUserId))
        {
            var requester = await db.Users.AsNoTracking()
                .Where(u => u.Id == request.RequestedByUserId)
                .Select(u => new { u.FirstName, u.LastName })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (requester != null)
            {
                var combined = $"{requester.FirstName} {requester.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(combined))
                    adminName = combined;
            }
        }

        var email = DataExportReadyEmailComposer.Build(
            DataExportReadyEmailComposer.CreateModel(
                data.Tenant.Name,
                link,
                expiresAt,
                validDays,
                adminName));

        await _notificationService.NotifyUserAsync(
            request.RequestedByUserId,
            request.TenantId,
            request.Id,
            email.Subject,
            email.PlainBody,
            email.HtmlBody,
            ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Export package ready with download link. RequestId={RequestId}, TenantId={TenantId}, FileName={FileName}, ExpiresAt={ExpiresAt}",
            request.Id,
            request.TenantId,
            fileName,
            expiresAt);

        return new ExportResult
        {
            Succeeded = true,
            TenantId = request.TenantId,
            RequestId = request.Id,
            Status = TenantDataRightsRequestStatuses.Ready,
            FileName = fileName,
            Data = zip,
            FileSize = zip.LongLength,
            TableRowCounts = CountDocument(data),
            ExportedAtUtc = data.Tenant.ExportedAt,
            Link = link,
            ExpiresAt = expiresAt,
            DownloadToken = token,
        };
    }

    public async Task<ExportResult?> GetExportByDownloadTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var normalized = token.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var request = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.DownloadToken == normalized, ct)
            .ConfigureAwait(false);

        if (request == null)
            return null;

        if (request.DownloadExpiresAtUtc is not { } expires || expires < DateTime.UtcNow)
            return null;

        if (string.IsNullOrWhiteSpace(request.ArtifactRelativePath))
            return null;

        var bytes = await _artifacts.ReadAsync(request.ArtifactRelativePath, ct).ConfigureAwait(false);
        if (bytes == null)
            return null;

        return new ExportResult
        {
            RequestId = request.Id,
            FileName = request.ArtifactFileName
                ?? _fileNaming.GenerateFileName(DataExportFileNames.Prefix, "zip"),
            Data = bytes,
            ExportedAtUtc = request.ReadyAtUtc ?? request.RequestedAtUtc,
            Link = BuildDownloadLink(_options.Value, normalized),
            ExpiresAt = expires,
            DownloadToken = normalized,
        };
    }

    private Task<TenantDataExportDocument> CollectAllDataAsync(Guid tenantId, CancellationToken ct) =>
        BuildExportDocumentAsync(tenantId, ct);

    private static async Task<byte[]> CreateZipAsync(
        TenantDataExportDocument document,
        Guid exportId,
        string fileName,
        CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"regkasse-data-export-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var zipStream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous))
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var dataEntry = zip.CreateEntry(TenantDataExportDocument.ZipEntryName, CompressionLevel.Optimal);
                    await using (var entryStream = dataEntry.Open())
                    {
                        await JsonSerializer.SerializeAsync(entryStream, document, JsonOptions, ct)
                            .ConfigureAwait(false);
                    }

                    var manifest = new DataExportManifest
                    {
                        ExportId = exportId,
                        TenantSlug = document.Tenant.Slug,
                        ExportedAt = document.Tenant.ExportedAt,
                        FileName = fileName,
                    };
                    var manifestEntry = zip.CreateEntry(
                        DataExportFileNames.ManifestZipEntryName,
                        CompressionLevel.Optimal);
                    await using (var manifestStream = manifestEntry.Open())
                    {
                        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, ct)
                            .ConfigureAwait(false);
                    }
                }

                await zipStream.FlushAsync(ct).ConfigureAwait(false);
            }

            return await File.ReadAllBytesAsync(tempPath, ct).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    internal static string BuildDownloadLink(DataExportOptions opts, string token)
    {
        var baseUrl = (opts.PublicApiBaseUrl ?? "https://api.regkasse.at").Trim().TrimEnd('/');
        var path = (opts.DownloadPathTemplate ?? "/data/download/{token}")
            .Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
        if (!path.StartsWith('/'))
            path = "/" + path;
        return baseUrl + path;
    }

    public async Task<TenantDataExportDocument> BuildExportDocumentAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var exportedAt = DateTime.UtcNow;
        if (exportedAt.Kind == DateTimeKind.Unspecified)
            exportedAt = DateTime.SpecifyKind(exportedAt, DateTimeKind.Utc);

        var cashRegisterIds = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var products = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var categories = await db.Categories.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var customers = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var payments = await db.PaymentDetails.AsNoTracking()
            .Where(p => cashRegisterIds.Contains(p.CashRegisterId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var receipts = await db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var invoices = await db.Invoices.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var orders = await db.OnlineOrders.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var orderIds = orders.Select(o => o.Id).ToList();
        var orderItems = orderIds.Count == 0
            ? new List<OnlineOrderItem>()
            : await db.OnlineOrderItems.AsNoTracking()
                .Where(i => orderIds.Contains(i.OnlineOrderId))
                .ToListAsync(ct)
                .ConfigureAwait(false);
        var itemsByOrder = orderItems.GroupBy(i => i.OnlineOrderId)
            .ToDictionary(g => g.Key, g => (IEnumerable<OnlineOrderItem>)g.ToList());

        var vouchers = await db.Vouchers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var settings = await db.CompanySettings.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return new TenantDataExportDocument
        {
            Tenant = new TenantDataExportTenantSection
            {
                Name = tenant.Name,
                Slug = tenant.Slug,
                ExportedAt = exportedAt,
            },
            Data = new TenantDataExportDataSection
            {
                Products = products.Select(DataExportMasking.MapProduct).Cast<object>().ToList(),
                Categories = categories.Select(DataExportMasking.MapCategory).Cast<object>().ToList(),
                Customers = customers.Select(DataExportMasking.MapCustomer).Cast<object>().ToList(),
                Payments = payments.Select(DataExportMasking.MapPayment).Cast<object>().ToList(),
                Receipts = receipts.Select(DataExportMasking.MapReceipt).Cast<object>().ToList(),
                Invoices = invoices.Select(DataExportMasking.MapInvoice).Cast<object>().ToList(),
                Orders = orders
                    .Select(o => DataExportMasking.MapOrder(
                        o,
                        itemsByOrder.TryGetValue(o.Id, out var items) ? items : Array.Empty<OnlineOrderItem>()))
                    .Cast<object>()
                    .ToList(),
                Vouchers = vouchers.Select(DataExportMasking.MapVoucher).Cast<object>().ToList(),
                Settings = settings == null ? null : DataExportMasking.MapSettings(settings),
            },
            Rksv = new TenantDataExportRksvSection
            {
                Note = TenantDataExportDocument.RksvRetentionNote,
                RetentionUntil = exportedAt.AddYears(RksvDataRetentionService.RetentionYears),
            },
        };
    }

    public async Task<TenantDataManagementSummaryDto> GetSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var pendingDeletion = await db.TenantDataDeletionRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                r => r.TenantId == tenantId
                     && (r.Status == TenantDataDeletionRequestStatuses.Pending
                         || r.Status == TenantDataDeletionRequestStatuses.ExportReady
                         || r.Status == TenantDataDeletionRequestStatuses.Confirmed),
                ct)
            .ConfigureAwait(false);

        var lifecycle = _lifecycle.Resolve(tenant, pendingDeletion);

        var cashRegisterIds = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var dataTypes = new List<TenantDataTypeSummaryDto>
        {
            await CountAsync("products", "Products", isRksv: false,
                db.Products.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("categories", "Categories", isRksv: false,
                db.Categories.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("customers", "Customers", isRksv: false,
                db.Customers.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("invoices_non_fiscal", "Invoices (non-fiscal)", isRksv: false,
                db.Invoices.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && x.SourcePaymentId == null, ct)).ConfigureAwait(false),
            await CountAsync("invoices_fiscal", "Invoices (fiscal)", isRksv: true,
                db.Invoices.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && x.SourcePaymentId != null, ct)).ConfigureAwait(false),
            await CountAsync("vouchers", "Vouchers", isRksv: true,
                db.Vouchers.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("online_orders", "Online orders", isRksv: true,
                db.OnlineOrders.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("payment_details", "Payments (fiscal)", isRksv: true,
                db.PaymentDetails.CountAsync(p => cashRegisterIds.Contains(p.CashRegisterId), ct)).ConfigureAwait(false),
            await CountAsync("receipts", "Receipts (RKSV)", isRksv: true,
                db.Receipts.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("daily_closings", "Daily closings (RKSV)", isRksv: true,
                db.DailyClosings.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("audit_logs", "Audit logs", isRksv: true,
                db.AuditLogs.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
            await CountAsync("tenant_customizations", "Customizations", isRksv: false,
                db.TenantCustomizations.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId, ct)).ConfigureAwait(false),
        };

        var latestEntity = await db.TenantDataDeletionRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var latestRequest = latestEntity == null
            ? null
            : DataDeletionService.Map(latestEntity);

        var daysOverdue = 0;
        if (tenant.LicenseValidUntilUtc.HasValue)
        {
            var until = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc.Value, DateTimeKind.Utc);
            daysOverdue = Math.Max(0, (DateTime.UtcNow - until).Days);
        }

        var isGrace = lifecycle == LicenseLifecycleState.Grace;
        var isLocked = lifecycle is LicenseLifecycleState.Locked or LicenseLifecycleState.Archived;
        var isArchived = lifecycle == LicenseLifecycleState.Archived
            || new TenantLicenseValidator().GetStatus(tenant.LicenseValidUntilUtc) == TenantLicenseStatus.Archived;
        var graceRemaining = isGrace
            ? Math.Max(0, LicenseGracePeriodConfig.GracePeriodDays - daysOverdue)
            : 0;

        var retention = await _retention.GetRetentionStatusAsync(tenantId, ct).ConfigureAwait(false);

        var canConfirm = latestRequest != null
            && !tenant.CustomerDataPurgedAtUtc.HasValue
            && latestRequest.Status is TenantDataDeletionRequestStatuses.Pending
                or TenantDataDeletionRequestStatuses.ExportReady;

        var canExecute = latestRequest != null
            && latestRequest.Status == TenantDataDeletionRequestStatuses.Confirmed
            && latestRequest.PurgeEligibleAtUtc.HasValue
            && latestRequest.PurgeEligibleAtUtc.Value <= DateTime.UtcNow
            && !tenant.CustomerDataPurgedAtUtc.HasValue;

        return new TenantDataManagementSummaryDto
        {
            TenantId = tenantId,
            TenantSlug = tenant.Slug,
            TenantName = tenant.Name,
            LifecycleState = lifecycle.ToString(),
            LicenseValidUntilUtc = tenant.LicenseValidUntilUtc,
            DaysOverdue = daysOverdue,
            IsInGracePeriod = isGrace,
            GracePeriodRemainingDays = graceRemaining,
            IsLocked = isLocked,
            IsArchived = isArchived,
            CustomerDataPurgedAtUtc = tenant.CustomerDataPurgedAtUtc,
            RksvRetentionYears = RksvDataRetentionService.RetentionYears,
            RksvRetentionNote =
                "Payment receipts, daily closings, TSE signatures, fiscal invoices, audit logs, online orders, and vouchers are retained for at least 7 years under Austrian RKSV. Deletion removes non-fiscal customer/business data only and is irreversible.",
            CanExport = true,
            CanRequestDeletion = !tenant.CustomerDataPurgedAtUtc.HasValue
                && isArchived
                && (latestRequest == null
                    || latestRequest.Status is TenantDataDeletionRequestStatuses.Cancelled
                        or TenantDataDeletionRequestStatuses.Completed),
            CanConfirmDeletion = canConfirm,
            CanExecutePurge = canExecute,
            DataTypes = dataTypes,
            LatestDeletionRequest = latestRequest,
            Retention = retention,
        };
    }

    private static IReadOnlyDictionary<string, int> CountDocument(TenantDataExportDocument document) =>
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["products"] = document.Data.Products.Count,
            ["categories"] = document.Data.Categories.Count,
            ["customers"] = document.Data.Customers.Count,
            ["payments"] = document.Data.Payments.Count,
            ["receipts"] = document.Data.Receipts.Count,
            ["invoices"] = document.Data.Invoices.Count,
            ["orders"] = document.Data.Orders.Count,
            ["vouchers"] = document.Data.Vouchers.Count,
            ["settings"] = document.Data.Settings == null ? 0 : 1,
        };

    private static async Task<TenantDataTypeSummaryDto> CountAsync(
        string key,
        string label,
        bool isRksv,
        Task<int> countTask)
    {
        var count = await countTask.ConfigureAwait(false);
        return new TenantDataTypeSummaryDto
        {
            Key = key,
            Label = label,
            RowCount = count,
            IsRksvRetained = isRksv,
            DeletedOnPurge = !isRksv,
        };
    }

    private static string Truncate(string value, int maxLen) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLen
            ? value
            : value[..maxLen];
}
