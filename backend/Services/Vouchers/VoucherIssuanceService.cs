using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Vouchers;

/// <summary>
/// Mehrzweckgutschein issuance bookkeeping (hash-only storage).
/// Fiscal law posture: issuance is not an RKSV Beleg/TSE-signable event; gated by voucher.issue permission.
/// </summary>
public sealed class VoucherIssuanceService : IVoucherIssuanceService
{
    private static readonly string[] AllowedCurrencies = ["EUR"];

    private readonly AppDbContext _context;
    private readonly IAuditLogService _audit;
    private readonly ILogger<VoucherIssuanceService> _logger;

    public VoucherIssuanceService(
        AppDbContext context,
        IAuditLogService audit,
        ILogger<VoucherIssuanceService> logger)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
    }

    public async Task<(IssueVoucherResponse? Response, string? ErrorCode)> IssueAsync(
        Guid tenantId,
        string userId,
        string userRole,
        IssueVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        var currency = (request.Currency ?? "EUR").Trim().ToUpperInvariant();
        if (!AllowedCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase))
            return (null, "UNSUPPORTED_CURRENCY");

        var validFrom = NormalizeUtc(request.ValidFrom);
        var validUntil = NormalizeUtc(request.ValidUntil);
        if (validUntil <= validFrom)
            return (null, "INVALID_DATE_RANGE");

        var initial = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);

        Guid? customerId = null;
        if (request.CustomerId is { } cid && cid != Guid.Empty)
        {
            var custOk = await _context.Customers.AsNoTracking()
                .AnyAsync(c => c.Id == cid && c.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!custOk)
                return (null, "CUSTOMER_NOT_FOUND");
            customerId = cid;
        }

        const int maxAttempts = 8;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var plain = VoucherPlainCodeFactory.GeneratePlainVoucherCode();
            var normalized = VoucherCodeHasher.NormalizeCode(plain);
            var hash = VoucherCodeHasher.HashNormalized(normalized);
            var masked = VoucherPlainCodeFactory.BuildMaskedCode(normalized);

            var exists = await _context.Vouchers.AsNoTracking()
                .AnyAsync(v => v.TenantId == tenantId && v.CodeHash == hash, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                continue;

            var utcNow = DateTime.UtcNow;
            var voucher = new Voucher
            {
                TenantId = tenantId,
                CodeHash = hash,
                MaskedCode = masked,
                InitialAmount = initial,
                RemainingAmount = initial,
                Currency = currency,
                Status = VoucherStatus.Active,
                ValidFromUtc = validFrom,
                ExpiresAtUtc = validUntil,
                CreatedByUserId = userId,
                CreatedAtUtc = utcNow,
                CustomerId = customerId,
            };

            var issueLedger = new VoucherLedgerEntry
            {
                TenantId = tenantId,
                VoucherId = voucher.Id,
                PaymentId = null,
                ReceiptId = null,
                Type = VoucherTransactionType.Issue,
                Amount = initial,
                BalanceAfter = initial,
                CreatedByUserId = userId,
                CreatedAtUtc = utcNow,
                IdempotencyKey = $"pos-issue:{voucher.Id:N}",
            };

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _context.Vouchers.Add(voucher);
                _context.VoucherLedgerEntries.Add(issueLedger);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning(ex, "Voucher issuance hash collision retry {Attempt}", attempt + 1);
                _context.ChangeTracker.Clear();
                continue;
            }

            var auditNewValues = new
            {
                voucherId = voucher.Id,
                amount = initial,
                currency,
                validFromUtc = validFrom,
                expiresAtUtc = validUntil,
                customerId,
                maskedCode = masked,
                tseRequired = false,
                channel = "pos_issue_non_fiscal",
            };

            try
            {
                await _audit.LogEntityChangeAsync(
                        "ISSUE_NON_FISCAL_VOUCHER",
                        nameof(Voucher),
                        voucher.Id,
                        userId,
                        userRole ?? "Unknown",
                        oldValues: null,
                        newValues: auditNewValues,
                        description: "Mehrzweckgutschein issuance (no TSE/RKSV Beleg)",
                        notes: "Plain voucher code intentionally omitted from audit storage.",
                        status: AuditLogStatus.Success)
                    .ConfigureAwait(false);
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "Audit log failed after voucher {VoucherId} issuance — voucher persisted", voucher.Id);
            }

            _logger.LogInformation(
                "Non-fiscal voucher issued voucherId={VoucherId}, masked={Masked}",
                voucher.Id,
                masked);

            var response = new IssueVoucherResponse
            {
                VoucherId = voucher.Id,
                MaskedCode = masked,
                FullCode = plain,
                Amount = initial,
            };

            return (response, null);
        }

        return (null, "CODE_GENERATION_FAILED");
    }

    private static DateTime NormalizeUtc(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt;
        if (dt.Kind == DateTimeKind.Local)
            return dt.ToUniversalTime();
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
