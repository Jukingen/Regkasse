using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using SaasLicenseType = KasseAPI_Final.Models.Enums.LicenseType;

namespace KasseAPI_Final.Services.Billing;

public sealed class SubscriptionInvoiceService : ISubscriptionInvoiceService
{
    public const string NotFoundCode = "NOT_FOUND";
    public const string AlreadyPaidCode = "ALREADY_PAID";
    public const string AlreadyVoidCode = "ALREADY_VOID";
    public const string PaidCannotVoidCode = "PAID_CANNOT_VOID";
    public const string InvalidStatusCode = "INVALID_STATUS";
    public const string ValidationCode = "VALIDATION_ERROR";

    private const string StorageRelativePath = "data/subscription-invoices";
    private const decimal DefaultVatRate = 20m;
    private const int MaxVoidReasonLength = 500;
    private const int MaxPaymentReferenceLength = 100;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly BillingOptions _options;
    private readonly ILogger<SubscriptionInvoiceService> _logger;
    private readonly ISubscriptionInvoiceEmailService? _email;
    private readonly IBillingAuditService? _audit;

    public SubscriptionInvoiceService(
        AppDbContext db,
        IWebHostEnvironment environment,
        IOptions<BillingOptions> options,
        ILogger<SubscriptionInvoiceService> logger,
        ISubscriptionInvoiceEmailService? email = null,
        IBillingAuditService? audit = null)
    {
        _db = db;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
        _email = email;
        _audit = audit;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public async Task<IReadOnlyList<SubscriptionInvoiceDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        string? status = null,
        Guid? tenantId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from inv in _db.SubscriptionInvoices.AsNoTracking().IgnoreQueryFilters()
            join t in _db.Tenants.AsNoTracking().IgnoreQueryFilters() on inv.TenantId equals t.Id
            select new { inv, t };

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusKey = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.inv.Status.ToLower() == statusKey);
        }

        if (tenantId is Guid tid && tid != Guid.Empty)
            query = query.Where(x => x.inv.TenantId == tid);

        if (fromUtc.HasValue)
            query = query.Where(x => x.inv.IssuedAtUtc >= ToUtc(fromUtc.Value));

        if (toUtc.HasValue)
            query = query.Where(x => x.inv.IssuedAtUtc <= ToUtc(toUtc.Value));

        var rows = await query
            .OrderByDescending(x => x.inv.IssuedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { Invoice = x.inv, x.t.Name, x.t.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(x => MapEntity(x.Invoice, x.Name, x.Slug)).ToList();
    }

    public async Task<SubscriptionInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await (
                from inv in _db.SubscriptionInvoices.AsNoTracking().IgnoreQueryFilters()
                join t in _db.Tenants.AsNoTracking().IgnoreQueryFilters() on inv.TenantId equals t.Id
                where inv.Id == id
                select new { Invoice = inv, t.Name, t.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row == null ? null : MapEntity(row.Invoice, row.Name, row.Slug);
    }

    public async Task<byte[]?> GetPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var inv = await _db.SubscriptionInvoices.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (inv == null)
            return null;

        if (!string.IsNullOrWhiteSpace(inv.PdfPath) && File.Exists(inv.PdfPath))
            return await File.ReadAllBytesAsync(inv.PdfPath, cancellationToken).ConfigureAwait(false);

        var dto = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return dto == null ? null : BuildPdf(dto);
    }

    public async Task<bool> ShouldGenerateInvoiceAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.TrialStatus, t.Status, t.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenant == null || tenant.Status != TenantStatuses.Active || !tenant.IsActive)
            return false;

        if (_options.SkipTrialTenants && TrialStatuses.IsOpenTrial(tenant.TrialStatus))
        {
            _logger.LogInformation(
                "Tenant {TenantId} is in trial ({TrialStatus}), skipping monthly invoice",
                tenantId,
                tenant.TrialStatus);
            return false;
        }

        if (_options.SkipPrepaidTenants
            && await HasPrepaidLicenseCoveringPeriodAsync(tenantId, periodStartUtc, periodEndUtc, cancellationToken)
                .ConfigureAwait(false))
        {
            _logger.LogInformation(
                "Tenant {TenantId} has prepaid license covering {PeriodStart:o}–{PeriodEnd:o}, skipping monthly invoice",
                tenantId,
                periodStartUtc,
                periodEndUtc);
            return false;
        }

        return true;
    }

    public async Task<MonthlyInvoiceGenerationResult> GenerateMonthlyInvoicesAsync(
        DateTime? periodMonthUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = new MonthlyInvoiceGenerationResult();
        var anchor = periodMonthUtc ?? DateTime.UtcNow.AddMonths(-1);
        var periodStart = new DateTime(anchor.Year, anchor.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var activeTenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Status == TenantStatuses.Active && t.IsActive)
            .Select(t => new { t.Id, t.Name, t.Slug, t.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var tenant in activeTenants)
        {
            try
            {
                var exists = await _db.SubscriptionInvoices.IgnoreQueryFilters()
                    .AnyAsync(
                        i => i.TenantId == tenant.Id
                             && i.PeriodStartUtc == periodStart
                             && i.PeriodEndUtc == periodEnd,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (exists)
                {
                    result.Skipped++;
                    continue;
                }

                if (!await ShouldGenerateInvoiceAsync(tenant.Id, periodStart, periodEnd, cancellationToken)
                        .ConfigureAwait(false))
                {
                    result.Skipped++;
                    continue;
                }

                var licenseType = await ResolveLicenseTypeAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
                var amountNet = ResolveMonthlyNet(licenseType);
                if (amountNet <= 0)
                {
                    result.Skipped++;
                    continue;
                }

                var vatRate = DefaultVatRate;
                var amountVat = Math.Round(amountNet * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
                var amountGross = amountNet + amountVat;
                var invoiceNumber =
                    $"SUB-{periodStart:yyyyMM}-{tenant.Slug.ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

                var entity = new SubscriptionInvoice
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    InvoiceNumber = invoiceNumber,
                    PeriodStartUtc = periodStart,
                    PeriodEndUtc = periodEnd,
                    LicenseType = licenseType,
                    AmountNet = amountNet,
                    VatRate = vatRate,
                    AmountVat = amountVat,
                    AmountGross = amountGross,
                    Currency = "EUR",
                    Status = SubscriptionInvoiceStatuses.Issued,
                    IssuedAtUtc = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                };

                var dto = MapEntity(entity, tenant.Name, tenant.Slug);
                entity.PdfPath = await PersistPdfAsync(dto, cancellationToken).ConfigureAwait(false);
                _db.SubscriptionInvoices.Add(entity);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                result.Created++;

                var pdf = await File.ReadAllBytesAsync(entity.PdfPath, cancellationToken).ConfigureAwait(false);
                var emailed = _email != null
                    && await _email.TrySendIssuedAsync(dto, pdf, tenant.Email, cancellationToken)
                        .ConfigureAwait(false);
                if (emailed)
                {
                    entity.EmailSentAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                _logger.LogError(ex, "Failed to generate subscription invoice for tenant {TenantId}", tenant.Id);
            }
        }

        return result;
    }

    public async Task<SubscriptionInvoiceActionResult> MarkPaidAsync(
        Guid id,
        MarkPaidRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.SubscriptionInvoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity == null)
            return SubscriptionInvoiceActionResult.Fail(NotFoundCode, "Invoice not found.");

        if (string.Equals(entity.Status, SubscriptionInvoiceStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            return SubscriptionInvoiceActionResult.Fail(AlreadyPaidCode, "Invoice is already paid.");

        if (string.Equals(entity.Status, SubscriptionInvoiceStatuses.Void, StringComparison.OrdinalIgnoreCase))
            return SubscriptionInvoiceActionResult.Fail(AlreadyVoidCode, "Cannot mark a void invoice as paid.");

        if (!string.Equals(entity.Status, SubscriptionInvoiceStatuses.Issued, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entity.Status, SubscriptionInvoiceStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionInvoiceActionResult.Fail(InvalidStatusCode, "Invoice cannot be marked as paid.");
        }

        var method = string.IsNullOrWhiteSpace(request.PaymentMethod)
            ? SubscriptionInvoicePaymentMethods.BankTransfer
            : request.PaymentMethod.Trim().ToLowerInvariant();
        if (!SubscriptionInvoicePaymentMethods.IsValid(method))
        {
            return SubscriptionInvoiceActionResult.Fail(
                ValidationCode,
                "PaymentMethod must be bank_transfer, card, or cash.");
        }

        var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        if (reference != null && reference.Length > MaxPaymentReferenceLength)
        {
            return SubscriptionInvoiceActionResult.Fail(
                ValidationCode,
                $"Reference must be at most {MaxPaymentReferenceLength} characters.");
        }

        var paidAt = request.PaidAt.HasValue ? ToUtc(request.PaidAt.Value) : DateTime.UtcNow;
        entity.Status = SubscriptionInvoiceStatuses.Paid;
        entity.PaidAtUtc = paidAt;
        entity.PaymentMethod = method;
        entity.PaymentReference = reference;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (dto == null)
            return SubscriptionInvoiceActionResult.Fail(NotFoundCode, "Invoice not found.");

        if (_audit != null)
        {
            var details = JsonSerializer.Serialize(new
            {
                invoiceId = entity.Id,
                invoiceNumber = entity.InvoiceNumber,
                paidAtUtc = entity.PaidAtUtc,
                paymentMethod = entity.PaymentMethod,
                paymentReference = entity.PaymentReference,
            });
            await _audit.LogAsync(
                    BillingAuditEventTypes.SubscriptionInvoicePaid,
                    actorUserId,
                    entity.TenantId,
                    saleId: null,
                    details,
                    ipAddress: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var tenantEmail = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == entity.TenantId)
            .Select(t => t.Email)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var pdf = await GetPdfAsync(id, cancellationToken).ConfigureAwait(false);
        if (_email != null && pdf != null)
        {
            await _email.TrySendPaidConfirmationAsync(dto, pdf, tenantEmail, cancellationToken)
                .ConfigureAwait(false);
        }

        return SubscriptionInvoiceActionResult.Ok(dto);
    }

    public async Task<SubscriptionInvoiceActionResult> VoidAsync(
        Guid id,
        VoidInvoiceRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.SubscriptionInvoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity == null)
            return SubscriptionInvoiceActionResult.Fail(NotFoundCode, "Invoice not found.");

        if (string.Equals(entity.Status, SubscriptionInvoiceStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            return SubscriptionInvoiceActionResult.Fail(PaidCannotVoidCode, "Cannot void a paid invoice.");

        if (string.Equals(entity.Status, SubscriptionInvoiceStatuses.Void, StringComparison.OrdinalIgnoreCase))
            return SubscriptionInvoiceActionResult.Fail(AlreadyVoidCode, "Invoice is already void.");

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return SubscriptionInvoiceActionResult.Fail(ValidationCode, "Reason is required.");
        if (reason.Length > MaxVoidReasonLength)
        {
            return SubscriptionInvoiceActionResult.Fail(
                ValidationCode,
                $"Reason must be at most {MaxVoidReasonLength} characters.");
        }

        entity.Status = SubscriptionInvoiceStatuses.Void;
        entity.VoidReason = reason;
        entity.VoidedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (dto == null)
            return SubscriptionInvoiceActionResult.Fail(NotFoundCode, "Invoice not found.");

        if (_audit != null)
        {
            var details = JsonSerializer.Serialize(new
            {
                invoiceId = entity.Id,
                invoiceNumber = entity.InvoiceNumber,
                voidReason = entity.VoidReason,
                voidedAtUtc = entity.VoidedAtUtc,
            });
            await _audit.LogAsync(
                    BillingAuditEventTypes.SubscriptionInvoiceVoided,
                    actorUserId,
                    entity.TenantId,
                    saleId: null,
                    details,
                    ipAddress: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return SubscriptionInvoiceActionResult.Ok(dto);
    }

    private async Task<bool> HasPrepaidLicenseCoveringPeriodAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken ct)
    {
        return await _db.LicenseSales.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(
                s => s.TenantId == tenantId
                     && s.Status == LicenseSaleStatuses.Active
                     && s.ValidFromUtc < periodEndUtc
                     && s.ValidUntilUtc > periodStartUtc,
                ct)
            .ConfigureAwait(false);
    }

    private async Task<SaasLicenseType> ResolveLicenseTypeAsync(Guid tenantId, CancellationToken ct)
    {
        var type = await _db.LicenseSales.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.Status == LicenseSaleStatuses.Active)
            .OrderByDescending(s => s.ValidUntilUtc)
            .Select(s => (SaasLicenseType?)s.LicenseType)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return type ?? SaasLicenseType.Starter;
    }

    private decimal ResolveMonthlyNet(SaasLicenseType type) => type switch
    {
        SaasLicenseType.Trial => 0m,
        SaasLicenseType.Starter => _options.MonthlyNetStarter,
        SaasLicenseType.Business => _options.MonthlyNetBusiness,
        SaasLicenseType.Plus => _options.MonthlyNetPlus,
        _ => _options.MonthlyNetStarter,
    };

    private async Task<string> PersistPdfAsync(SubscriptionInvoiceDto dto, CancellationToken ct)
    {
        var root = Path.Combine(_environment.ContentRootPath, StorageRelativePath);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{dto.InvoiceNumber}.pdf");
        var bytes = BuildPdf(dto);
        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
        return path;
    }

    private static byte[] BuildPdf(SubscriptionInvoiceDto dto)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text($"Rechnung {dto.InvoiceNumber}").SemiBold().FontSize(18);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Mandant: {dto.TenantName} ({dto.TenantSlug})");
                    col.Item().Text(
                        $"Zeitraum: {dto.PeriodStartUtc:yyyy-MM-dd} – {dto.PeriodEndUtc.AddDays(-1):yyyy-MM-dd} UTC");
                    col.Item().Text($"Paket: {dto.LicenseType}");
                    col.Item().PaddingTop(12).Text(
                        $"Netto: {dto.AmountNet.ToString("0.00", CultureInfo.InvariantCulture)} {dto.Currency}");
                    col.Item().Text(
                        $"USt ({dto.VatRate.ToString("0.##", CultureInfo.InvariantCulture)}%): {dto.AmountVat.ToString("0.00", CultureInfo.InvariantCulture)} {dto.Currency}");
                    col.Item().Text(
                        $"Brutto: {dto.AmountGross.ToString("0.00", CultureInfo.InvariantCulture)} {dto.Currency}").SemiBold();
                    col.Item().PaddingTop(8).Text($"Status: {dto.Status}");
                });
                page.Footer().AlignCenter().Text("Regkasse SaaS — kein fiskalischer Beleg").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });

        return doc.GeneratePdf();
    }

    private static SubscriptionInvoiceDto MapEntity(SubscriptionInvoice inv, string tenantName, string tenantSlug) =>
        new()
        {
            Id = inv.Id,
            TenantId = inv.TenantId,
            TenantName = tenantName,
            TenantSlug = tenantSlug,
            InvoiceNumber = inv.InvoiceNumber,
            PeriodStartUtc = inv.PeriodStartUtc,
            PeriodEndUtc = inv.PeriodEndUtc,
            LicenseType = inv.LicenseType,
            AmountNet = inv.AmountNet,
            VatRate = inv.VatRate,
            AmountVat = inv.AmountVat,
            AmountGross = inv.AmountGross,
            Currency = inv.Currency,
            Status = inv.Status,
            IssuedAtUtc = inv.IssuedAtUtc,
            PaidAtUtc = inv.PaidAtUtc,
            PaymentMethod = inv.PaymentMethod,
            PaymentReference = inv.PaymentReference,
            VoidReason = inv.VoidReason,
            VoidedAtUtc = inv.VoidedAtUtc,
            EmailSentAtUtc = inv.EmailSentAtUtc,
        };

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
