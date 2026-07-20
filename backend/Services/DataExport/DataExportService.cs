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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.DataExport;

public sealed class ExportResult
{
    public string? FileName { get; init; }
    public byte[]? Data { get; init; }
    public IReadOnlyDictionary<string, int>? TableRowCounts { get; init; }
    public DateTime ExportedAtUtc { get; init; } = DateTime.UtcNow;
    public Guid? RequestId { get; init; }
    public string? Link { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? DownloadToken { get; init; }
}

public interface IDataExportService
{
    /// <summary>Legacy/sync ZIP bytes for a tenant (no download token).</summary>
    Task<ExportResult> ExportAllDataAsync(Guid tenantId, CancellationToken ct = default);

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
/// Produces a ZIP containing <c>data-export.json</c> in the v2 document shape
/// (tenant + data + rksv). RKSV rows are included masked; Identity credentials are excluded.
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
    private readonly ILogger<DataExportService> _logger;

    public DataExportService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILicenseLifecycleResolver lifecycle,
        IRksvDataRetentionService retention,
        IDataRightsArtifactStore artifacts,
        IDataAccessNotificationService notificationService,
        IOptions<DataExportOptions> options,
        ILogger<DataExportService> logger)
    {
        _dbFactory = dbFactory;
        _lifecycle = lifecycle;
        _retention = retention;
        _artifacts = artifacts;
        _notificationService = notificationService;
        _options = options;
        _logger = logger;
    }

    public async Task<ExportResult> ExportAllDataAsync(Guid tenantId, CancellationToken ct = default)
    {
        var document = await CollectAllDataAsync(tenantId, ct).ConfigureAwait(false);
        var zip = await CreateZipAsync(document, ct).ConfigureAwait(false);
        var counts = CountDocument(document);

        _logger.LogInformation(
            "Tenant data export created (v2). TenantId={TenantId}, Bytes={Bytes}, Tables={TableCount}",
            tenantId,
            zip.Length,
            counts.Count);

        return new ExportResult
        {
            FileName = $"tenant_{document.Tenant.Slug}_export_{document.Tenant.ExportedAt:yyyyMMddHHmmss}.zip",
            Data = zip,
            TableRowCounts = counts,
            ExportedAtUtc = document.Tenant.ExportedAt,
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

        // 2. Create ZIP
        var zip = await CreateZipAsync(data, ct).ConfigureAwait(false);

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
        var fileName = $"tenant_{data.Tenant.Slug}_export_{data.Tenant.ExportedAt:yyyyMMddHHmmss}.zip";

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

        // 5. Notify user
        await _notificationService.NotifyUserAsync(
            request.RequestedByUserId,
            request.TenantId,
            request.Id,
            subject: "Your data export is ready",
            body: $"Your data export is ready. Download within {validDays} days: {link}",
            ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Export package ready with download link. RequestId={RequestId}, TenantId={TenantId}, ExpiresAt={ExpiresAt}",
            request.Id,
            request.TenantId,
            expiresAt);

        return new ExportResult
        {
            RequestId = request.Id,
            FileName = fileName,
            Data = zip,
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
            FileName = request.ArtifactFileName ?? $"export_{request.Id:N}.zip",
            Data = bytes,
            ExportedAtUtc = request.ReadyAtUtc ?? request.RequestedAtUtc,
            Link = BuildDownloadLink(_options.Value, normalized),
            ExpiresAt = expires,
            DownloadToken = normalized,
        };
    }

    private Task<TenantDataExportDocument> CollectAllDataAsync(Guid tenantId, CancellationToken ct) =>
        BuildExportDocumentAsync(tenantId, ct);

    private static async Task<byte[]> CreateZipAsync(TenantDataExportDocument document, CancellationToken ct)
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
                    var entry = zip.CreateEntry(TenantDataExportDocument.ZipEntryName, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await JsonSerializer.SerializeAsync(entryStream, document, JsonOptions, ct).ConfigureAwait(false);
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
}
