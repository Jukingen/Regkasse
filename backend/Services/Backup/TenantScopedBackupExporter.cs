using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Package metadata for a tenant-scoped logical backup (no Identity / platform secrets).
/// Row payloads are written to ZIP entries — not held entirely in this object.
/// </summary>
public sealed class TenantBackupData
{
    public Guid TenantId { get; init; }
    public string TenantSlug { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public DateTime ExportedAtUtc { get; init; }
    public string Format { get; init; } = "regkasse.tenant-backup.v1";
    public IReadOnlyDictionary<string, int> TableRowCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IReadOnlyList<string> ExcludedCategories { get; init; } =
        new[] { "identity", "platform_settings", "user_credentials", "other_tenants" };
}

public interface ITenantScopedBackupExporter
{
    /// <summary>
    /// Writes a ZIP of JSON table extracts for <paramref name="tenantId"/> under staging.
    /// Uses <c>IgnoreQueryFilters</c> + explicit tenant predicates (worker has no ambient tenant).
    /// When <paramref name="changedSinceUtc"/> is set, only rows changed since that watermark are exported
    /// (incremental package — not a standalone restore source).
    /// </summary>
    Task<TenantScopedBackupExportResult> ExportAsync(
        AppDbContext db,
        Guid tenantId,
        string tenantSlug,
        string absoluteZipPath,
        CancellationToken ct = default,
        DateTime? changedSinceUtc = null);
}

public sealed class TenantScopedBackupExportResult
{
    public required TenantBackupData Manifest { get; init; }
    public long ByteSize { get; init; }
}

/// <summary>
/// Streams tenant-only business/fiscal rows into a ZIP. Explicitly skips AspNet Identity,
/// UserTenantMembership, NtpAdminSettings, and other platform tables.
/// </summary>
public sealed class TenantScopedBackupExporter : ITenantScopedBackupExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    private readonly ICompressionService _compression;

    public TenantScopedBackupExporter(ICompressionService? compression = null)
    {
        _compression = compression ?? CompressionService.Shared;
    }

    public async Task<TenantScopedBackupExportResult> ExportAsync(
        AppDbContext db,
        Guid tenantId,
        string tenantSlug,
        string absoluteZipPath,
        CancellationToken ct = default,
        DateTime? changedSinceUtc = null)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var dir = Path.GetDirectoryName(absoluteZipPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(absoluteZipPath))
            File.Delete(absoluteZipPath);

        var since = changedSinceUtc.HasValue
            ? DateTime.SpecifyKind(changedSinceUtc.Value.ToUniversalTime(), DateTimeKind.Utc)
            : (DateTime?)null;
        var isIncremental = since.HasValue;
        var format = isIncremental
            ? "regkasse.tenant-backup.incremental.v1"
            : "regkasse.tenant-backup.v1";

        await using (var zipStream = new FileStream(
                         absoluteZipPath,
                         FileMode.CreateNew,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         64 * 1024,
                         FileOptions.Asynchronous))
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            await WriteJsonEntryAsync(zip, "tenant.json", new
            {
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                tenant.Status,
                tenant.IsActive,
                tenant.CreatedAt,
                packageKind = isIncremental ? BackupIncrementalPackageMetadata.PackageKindIncremental : BackupIncrementalPackageMetadata.PackageKindFull,
                changedSinceUtc = since
            }, ct);

            // Core catalog / CRM
            await ExportEntityAsync(zip, counts, "products.json",
                FilterBase(db.Products.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since), ct);
            await ExportEntityAsync(zip, counts, "categories.json",
                FilterBase(db.Categories.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since), ct);
            await ExportEntityAsync(zip, counts, "customers.json",
                FilterBase(db.Customers.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since), ct);

            // Registers: always include full register set for referential context (small).
            var cashRegisterIds = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId)
                .Select(x => x.Id)
                .ToListAsync(ct);

            await ExportEntityAsync(zip, counts, "cash_registers.json",
                db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct);
            await ExportEntityAsync(zip, counts, "cash_register_settings.json",
                db.CashRegisterSettings.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct);

            await ExportEntityAsync(zip, counts, "payment_details.json",
                FilterBase(db.PaymentDetails.AsNoTracking().Where(p => cashRegisterIds.Contains(p.CashRegisterId)), since), ct);
            await ExportEntityAsync(zip, counts, "payment_reversal_approvals.json",
                FilterBase(db.PaymentReversalApprovals.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since), ct);
            var paymentMethodQuery = db.PaymentMethodDefinitions.AsNoTracking().IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId);
            if (since.HasValue)
            {
                var s = since.Value;
                paymentMethodQuery = paymentMethodQuery.Where(x =>
                    x.CreatedAtUtc >= s || (x.UpdatedAtUtc != null && x.UpdatedAtUtc >= s));
            }

            await ExportEntityAsync(zip, counts, "payment_method_definitions.json", paymentMethodQuery, ct);

            // Receipts / fiscal
            var receiptQuery = db.Receipts.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId);
            if (since.HasValue)
            {
                var s = since.Value;
                receiptQuery = receiptQuery.Where(x => x.CreatedAt >= s || x.IssuedAt >= s);
            }

            var changedReceiptIds = since.HasValue
                ? await receiptQuery.Select(r => r.ReceiptId).ToListAsync(ct)
                : null;

            await ExportEntityAsync(zip, counts, "receipts.json", receiptQuery, ct);
            await ExportEntityAsync(zip, counts, "receipt_items.json",
                changedReceiptIds == null
                    ? db.ReceiptItems.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId)
                    : db.ReceiptItems.AsNoTracking().IgnoreQueryFilters()
                        .Where(x => x.TenantId == tenantId && changedReceiptIds.Contains(x.ReceiptId)), ct);
            await ExportEntityAsync(zip, counts, "receipt_tax_lines.json",
                changedReceiptIds == null
                    ? db.ReceiptTaxLines.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId)
                    : db.ReceiptTaxLines.AsNoTracking().IgnoreQueryFilters()
                        .Where(x => x.TenantId == tenantId && changedReceiptIds.Contains(x.ReceiptId)), ct);

            var dailyQuery = db.DailyClosings.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId);
            if (since.HasValue)
            {
                var s = since.Value;
                dailyQuery = dailyQuery.Where(x => x.CreatedAt >= s || (x.UpdatedAt != null && x.UpdatedAt >= s));
            }

            await ExportEntityAsync(zip, counts, "daily_closings.json", dailyQuery, ct);
            await ExportEntityAsync(zip, counts, "monatsbelege.json",
                FilterUtcCreated(db.Monatsbelege.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since, x => x.CreatedAtUtc), ct);
            await ExportEntityAsync(zip, counts, "jahresbelege.json",
                FilterUtcCreated(db.Jahresbelege.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since, x => x.CreatedAtUtc), ct);
            await ExportEntityAsync(zip, counts, "tse_signatures.json",
                FilterCreatedAt(db.TseSignatures.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since, x => x.CreatedAt), ct);
            var fonQuery = db.FinanzOnlineSubmissions.AsNoTracking().IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId);
            if (since.HasValue)
                fonQuery = fonQuery.Where(x => x.SubmittedAt >= since.Value);
            await ExportEntityAsync(zip, counts, "finanz_online_submissions.json", fonQuery, ct);

            // Commerce / vouchers / invoices
            await ExportEntityAsync(zip, counts, "invoices.json",
                FilterBase(db.Invoices.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since), ct);
            await ExportEntityAsync(zip, counts, "vouchers.json",
                FilterUtcCreated(db.Vouchers.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since, x => x.CreatedAtUtc), ct);
            await ExportEntityAsync(zip, counts, "voucher_ledger_entries.json",
                FilterUtcCreated(db.VoucherLedgerEntries.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since, x => x.CreatedAtUtc), ct);
            await ExportEntityAsync(zip, counts, "offline_orders.json",
                FilterUtcCreated(db.OfflineOrders.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since, x => x.CreatedAtUtc), ct);

            // Tenant company + report metadata (PDF bytes stay on filesystem)
            await ExportEntityAsync(zip, counts, "company_settings.json",
                FilterBase(db.CompanySettings.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId), since), ct);
            var reportPdfQuery = db.ReportPdfs.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId);
            if (since.HasValue)
            {
                var s = since.Value;
                reportPdfQuery = reportPdfQuery.Where(x =>
                    x.CreatedAt >= s || (x.UpdatedAt != null && x.UpdatedAt >= s));
            }

            await ExportEntityAsync(zip, counts, "report_pdfs.json", reportPdfQuery, ct);

            // Audit / activity (read-only compliance history for this tenant)
            var auditQuery = db.AuditLogs.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId);
            if (since.HasValue)
                auditQuery = auditQuery.Where(x => x.Timestamp >= since.Value);
            await ExportEntityAsync(zip, counts, "audit_logs.json", auditQuery, ct);

            var activityQuery = db.ActivityEvents.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId);
            if (since.HasValue)
                activityQuery = activityQuery.Where(x => x.CreatedAtUtc >= since.Value);
            await ExportEntityAsync(zip, counts, "activity_events.json", activityQuery, ct);

            var manifest = new TenantBackupData
            {
                TenantId = tenantId,
                TenantSlug = tenant.Slug,
                TenantName = tenant.Name,
                ExportedAtUtc = DateTime.UtcNow,
                Format = format,
                TableRowCounts = counts,
            };
            await WriteJsonEntryAsync(zip, "manifest.json", manifest, ct);
        }

        var fi = new FileInfo(absoluteZipPath);
        return new TenantScopedBackupExportResult
        {
            Manifest = new TenantBackupData
            {
                TenantId = tenantId,
                TenantSlug = string.IsNullOrWhiteSpace(tenantSlug) ? tenant.Slug : tenantSlug,
                TenantName = tenant.Name,
                ExportedAtUtc = DateTime.UtcNow,
                Format = format,
                TableRowCounts = counts,
            },
            ByteSize = fi.Exists ? fi.Length : 0
        };
    }

    private static IQueryable<T> FilterBase<T>(IQueryable<T> query, DateTime? sinceUtc)
        where T : BaseEntity
    {
        if (!sinceUtc.HasValue)
            return query;
        var s = sinceUtc.Value;
        return query.Where(x => x.CreatedAt >= s || (x.UpdatedAt != null && x.UpdatedAt >= s));
    }

    private static IQueryable<T> FilterCreatedAt<T>(
        IQueryable<T> query,
        DateTime? sinceUtc,
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdAtSelector)
    {
        if (!sinceUtc.HasValue)
            return query;
        var s = sinceUtc.Value;
        var param = createdAtSelector.Parameters[0];
        var body = System.Linq.Expressions.Expression.GreaterThanOrEqual(createdAtSelector.Body, System.Linq.Expressions.Expression.Constant(s));
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param);
        return query.Where(lambda);
    }

    private static IQueryable<T> FilterUtcCreated<T>(
        IQueryable<T> query,
        DateTime? sinceUtc,
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdAtUtcSelector) =>
        FilterCreatedAt(query, sinceUtc, createdAtUtcSelector);

    private async Task ExportEntityAsync<T>(
        ZipArchive zip,
        IDictionary<string, int> counts,
        string entryName,
        IQueryable<T> query,
        CancellationToken ct)
    {
        var rows = await query.ToListAsync(ct);
        counts[entryName] = rows.Count;
        await WriteJsonEntryAsync(zip, entryName, rows, ct);
    }

    private async Task WriteJsonEntryAsync<T>(
        ZipArchive zip,
        string entryName,
        T payload,
        CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, _compression.ResolveZipEntryLevel(entryName));
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct);
    }
}
