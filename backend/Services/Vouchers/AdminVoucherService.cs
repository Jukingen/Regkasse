using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Vouchers;

public sealed class AdminVoucherService : IAdminVoucherService
{
    private const string ExpiryDefaultOneYear = "DefaultOneYear";
    private const string ExpiryCustom = "Custom";
    private static readonly string[] AllowedCurrencies = ["EUR"];

    private readonly AppDbContext _context;
    private readonly ILogger<AdminVoucherService> _logger;

    public AdminVoucherService(AppDbContext context, ILogger<AdminVoucherService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AdminVoucherListResponse> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? q,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Vouchers.AsNoTracking().Where(v => v.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var safe = new string(q.Trim().Where(c => c is not '%' and not '_' and not '\\').Take(48).ToArray());
            if (safe.Length > 0)
                query = query.Where(v => EF.Functions.ILike(v.MaskedCode, $"%{safe}%"));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(v => v.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new AdminVoucherListItemDto
            {
                Id = v.Id,
                MaskedCode = v.MaskedCode,
                InitialAmount = v.InitialAmount,
                RemainingAmount = v.RemainingAmount,
                Currency = v.Currency,
                Status = StatusToApi(v.Status),
                ValidFromUtc = v.ValidFromUtc,
                ExpiresAtUtc = v.ExpiresAtUtc,
                CreatedByUserId = v.CreatedByUserId,
                CreatedAtUtc = v.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminVoucherListResponse
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AdminVoucherDetailDto?> GetDetailAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Vouchers.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.Id == id)
            .Select(v => new AdminVoucherDetailDto
            {
                Id = v.Id,
                MaskedCode = v.MaskedCode,
                InitialAmount = v.InitialAmount,
                RemainingAmount = v.RemainingAmount,
                Currency = v.Currency,
                Status = StatusToApi(v.Status),
                ValidFromUtc = v.ValidFromUtc,
                ExpiresAtUtc = v.ExpiresAtUtc,
                CreatedByUserId = v.CreatedByUserId,
                CreatedAtUtc = v.CreatedAtUtc,
                CancelledAtUtc = v.CancelledAtUtc,
                CancellationReason = v.CancellationReason,
                InternalNote = v.InternalNote,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AdminVoucherLedgerLineDto>> GetLedgerAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var exists = await _context.Vouchers.AsNoTracking()
            .AnyAsync(v => v.TenantId == tenantId && v.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return Array.Empty<AdminVoucherLedgerLineDto>();

        var rows = await (
                from e in _context.VoucherLedgerEntries.AsNoTracking()
                join r in _context.Receipts.AsNoTracking() on e.ReceiptId equals r.ReceiptId into rj
                from r in rj.DefaultIfEmpty()
                where e.TenantId == tenantId && e.VoucherId == id
                orderby e.CreatedAtUtc, e.Id
                select new AdminVoucherLedgerLineDto
                {
                    Id = e.Id,
                    Type = LedgerTypeToApi(e.Type),
                    Amount = e.Amount,
                    BalanceAfter = e.BalanceAfter,
                    PaymentId = e.PaymentId,
                    ReceiptId = e.ReceiptId,
                    ReceiptNumber = r != null ? r.ReceiptNumber : null,
                    CreatedByUserId = e.CreatedByUserId,
                    CreatedAtUtc = e.CreatedAtUtc,
                    CorrelationId = e.CorrelationId,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows;
    }

    public async Task<(CreateAdminVoucherResponse? Response, string? ErrorCode)> CreateAsync(
        Guid tenantId,
        string userId,
        CreateAdminVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        var currency = (request.Currency ?? "EUR").Trim().ToUpperInvariant();
        if (!AllowedCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase))
            return (null, "UNSUPPORTED_CURRENCY");

        var mode = (request.ExpiryMode ?? ExpiryDefaultOneYear).Trim();
        var utcNow = DateTime.UtcNow;
        DateTime expiresAtUtc;
        if (string.Equals(mode, ExpiryCustom, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.ExpiresAtUtc.HasValue)
                return (null, "EXPIRY_REQUIRED");
            expiresAtUtc = NormalizeUtc(request.ExpiresAtUtc.Value);
            if (expiresAtUtc <= utcNow)
                return (null, "EXPIRY_INVALID");
        }
        else if (string.Equals(mode, ExpiryDefaultOneYear, StringComparison.OrdinalIgnoreCase))
        {
            expiresAtUtc = utcNow.AddYears(1);
        }
        else
            return (null, "EXPIRY_MODE_INVALID");

        var validFromUtc = utcNow;
        if (expiresAtUtc <= validFromUtc)
            return (null, "EXPIRY_INVALID");

        const int maxAttempts = 8;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var plain = GeneratePlainVoucherCode();
            var normalized = VoucherCodeHasher.NormalizeCode(plain);
            var hash = VoucherCodeHasher.HashNormalized(normalized);
            var masked = BuildMaskedCode(normalized);

            var exists = await _context.Vouchers.AsNoTracking()
                .AnyAsync(v => v.TenantId == tenantId && v.CodeHash == hash, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                continue;

            var initial = decimal.Round(request.InitialAmount, 2, MidpointRounding.AwayFromZero);
            var voucher = new Voucher
            {
                TenantId = tenantId,
                CodeHash = hash,
                MaskedCode = masked,
                InitialAmount = initial,
                RemainingAmount = initial,
                Currency = currency,
                Status = VoucherStatus.Active,
                ValidFromUtc = validFromUtc,
                ExpiresAtUtc = expiresAtUtc,
                CreatedByUserId = userId,
                CreatedAtUtc = utcNow,
                InternalNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            };

            var issueId = Guid.NewGuid();
            var issueLedger = new VoucherLedgerEntry
            {
                Id = issueId,
                TenantId = tenantId,
                VoucherId = voucher.Id,
                PaymentId = null,
                ReceiptId = null,
                Type = VoucherTransactionType.Issue,
                Amount = initial,
                BalanceAfter = initial,
                CreatedByUserId = userId,
                CreatedAtUtc = utcNow,
                IdempotencyKey = $"admin-issue:{voucher.Id:N}",
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
                _logger.LogWarning(ex, "Voucher create race on code hash (attempt {Attempt})", attempt + 1);
                _context.ChangeTracker.Clear();
                continue;
            }

            return (new CreateAdminVoucherResponse
            {
                Id = voucher.Id,
                PlaintextCode = plain,
                MaskedCode = masked,
                InitialAmount = initial,
                Currency = currency,
                ValidFromUtc = validFromUtc,
                ExpiresAtUtc = expiresAtUtc,
            }, null);
        }

        return (null, "CODE_GENERATION_FAILED");
    }

    public async Task<(bool Ok, string? ErrorCode)> CancelAsync(
        Guid tenantId,
        string userId,
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var trimmedReason = reason.Trim();
        if (trimmedReason.Length < 5)
            return (false, "REASON_TOO_SHORT");

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var v = await _context.Vouchers
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (v == null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (false, "NOT_FOUND");
            }

            if (v.Status == VoucherStatus.Cancelled)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (false, "ALREADY_CANCELLED");
            }

            if (v.Status == VoucherStatus.Redeemed || v.RemainingAmount <= 0)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (false, "NOT_CANCELLABLE");
            }

            var remaining = v.RemainingAmount;
            var utcNow = DateTime.UtcNow;
            v.RemainingAmount = 0;
            v.Status = VoucherStatus.Cancelled;
            v.CancelledAtUtc = utcNow;
            v.CancellationReason = trimmedReason;

            var cancelKey = $"admin-cancel:{v.Id:N}:{Guid.NewGuid():N}";
            var ledger = new VoucherLedgerEntry
            {
                TenantId = tenantId,
                VoucherId = v.Id,
                PaymentId = null,
                ReceiptId = null,
                Type = VoucherTransactionType.Cancel,
                Amount = -remaining,
                BalanceAfter = 0,
                CreatedByUserId = userId,
                CreatedAtUtc = utcNow,
                IdempotencyKey = cancelKey.Length <= 128 ? cancelKey : cancelKey[..128],
            };

            _context.VoucherLedgerEntries.Add(ledger);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Voucher cancel failed for {VoucherId}", id);
            return (false, "SAVE_FAILED");
        }
    }

    private static DateTime NormalizeUtc(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt;
        if (dt.Kind == DateTimeKind.Local)
            return dt.ToUniversalTime();
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    private static string GeneratePlainVoucherCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[14];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(20);
        sb.Append("GUT-");
        foreach (var b in bytes)
            sb.Append(alphabet[b % alphabet.Length]);
        return sb.ToString();
    }

    private static string BuildMaskedCode(string normalized)
    {
        if (normalized.Length <= 4)
            return "****" + normalized;
        return "****" + normalized[^4..];
    }

    private static string StatusToApi(VoucherStatus s) =>
        Enum.GetName(s) ?? s.ToString();

    private static string LedgerTypeToApi(VoucherTransactionType t) =>
        Enum.GetName(t) ?? t.ToString();
}
