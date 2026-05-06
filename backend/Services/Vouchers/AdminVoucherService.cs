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
        var creators = await ResolveCreatorInfoAsync(items.Select(i => i.CreatedByUserId), cancellationToken).ConfigureAwait(false);
        var enrichedItems = items.Select(item =>
        {
            creators.TryGetValue(item.CreatedByUserId, out var info);
            return new AdminVoucherListItemDto
            {
                Id = item.Id,
                MaskedCode = item.MaskedCode,
                InitialAmount = item.InitialAmount,
                RemainingAmount = item.RemainingAmount,
                Currency = item.Currency,
                Status = item.Status,
                ValidFromUtc = item.ValidFromUtc,
                ExpiresAtUtc = item.ExpiresAtUtc,
                CreatedByUserId = item.CreatedByUserId,
                CreatedByDisplayName = info?.DisplayName,
                CreatedByEmail = info?.Email,
                CreatedByRoles = info?.Roles,
                CreatedAtUtc = item.CreatedAtUtc,
            };
        }).ToList();

        return new AdminVoucherListResponse
        {
            Items = enrichedItems,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AdminVoucherDetailDto?> GetDetailAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _context.Vouchers.AsNoTracking()
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
        if (dto == null)
            return null;
        var creators = await ResolveCreatorInfoAsync([dto.CreatedByUserId], cancellationToken).ConfigureAwait(false);
        creators.TryGetValue(dto.CreatedByUserId, out var creator);
        return new AdminVoucherDetailDto
        {
            Id = dto.Id,
            MaskedCode = dto.MaskedCode,
            InitialAmount = dto.InitialAmount,
            RemainingAmount = dto.RemainingAmount,
            Currency = dto.Currency,
            Status = dto.Status,
            ValidFromUtc = dto.ValidFromUtc,
            ExpiresAtUtc = dto.ExpiresAtUtc,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedByDisplayName = creator?.DisplayName,
            CreatedByEmail = creator?.Email,
            CreatedByRoles = creator?.Roles,
            CreatedAtUtc = dto.CreatedAtUtc,
            CancelledAtUtc = dto.CancelledAtUtc,
            CancellationReason = dto.CancellationReason,
            InternalNote = dto.InternalNote,
        };
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

        var creatorLookup = await ResolveCreatorInfoAsync(rows.Select(r => r.CreatedByUserId), cancellationToken).ConfigureAwait(false);
        return rows.Select(row =>
        {
            creatorLookup.TryGetValue(row.CreatedByUserId, out var info);
            return new AdminVoucherLedgerLineDto
            {
                Id = row.Id,
                Type = row.Type,
                Amount = row.Amount,
                BalanceAfter = row.BalanceAfter,
                PaymentId = row.PaymentId,
                ReceiptId = row.ReceiptId,
                ReceiptNumber = row.ReceiptNumber,
                CreatedByUserId = row.CreatedByUserId,
                CreatedByDisplayName = info?.DisplayName,
                CreatedByEmail = info?.Email,
                CreatedByRoles = info?.Roles,
                CreatedAtUtc = row.CreatedAtUtc,
                CorrelationId = row.CorrelationId,
            };
        }).ToList();
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
            var plain = VoucherPlainCodeFactory.GeneratePlainVoucherCode();
            var normalized = VoucherCodeHasher.NormalizeCode(plain);
            var hash = VoucherCodeHasher.HashNormalized(normalized);
            var masked = VoucherPlainCodeFactory.BuildMaskedCode(normalized);

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

    public async Task<(VerifyAdminVoucherCodeResponse? Response, string? ErrorCode)> VerifyCodeMatchesAsync(
        Guid tenantId,
        Guid id,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalized = VoucherCodeHasher.NormalizeCode(code);
        if (string.IsNullOrEmpty(normalized))
            return (null, "CODE_REQUIRED");

        var hash = VoucherCodeHasher.HashNormalized(normalized);
        var storedHash = await _context.Vouchers.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.Id == id)
            .Select(v => v.CodeHash)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (storedHash == null)
            return (null, "NOT_FOUND");

        return (new VerifyAdminVoucherCodeResponse { Matches = storedHash == hash }, null);
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

    private static string StatusToApi(VoucherStatus s) =>
        Enum.GetName(s) ?? s.ToString();

    private static string LedgerTypeToApi(VoucherTransactionType t) =>
        Enum.GetName(t) ?? t.ToString();

    private sealed record CreatorInfo(string? DisplayName, string? Email, IReadOnlyList<string> Roles);

    private async Task<Dictionary<string, CreatorInfo>> ResolveCreatorInfoAsync(
        IEnumerable<string> rawUserIds,
        CancellationToken cancellationToken)
    {
        var userIds = rawUserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (userIds.Count == 0)
            return new Dictionary<string, CreatorInfo>(StringComparer.Ordinal);

        var users = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIdSet = users.Select(u => u.Id).ToList();
        var roleRows = await (
                from ur in _context.UserRoles.AsNoTracking()
                join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where userIdSet.Contains(ur.UserId)
                select new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rolesByUser = roleRows
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.UserId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Name!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.Ordinal);

        var result = new Dictionary<string, CreatorInfo>(StringComparer.Ordinal);
        foreach (var u in users)
        {
            var displayName = BuildDisplayName(u.FirstName, u.LastName, u.UserName, u.Email, u.Id);
            rolesByUser.TryGetValue(u.Id, out var roles);
            result[u.Id] = new CreatorInfo(displayName, u.Email, roles ?? Array.Empty<string>());
        }

        return result;
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string? userName, string? email, string fallbackId)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName;
        if (!string.IsNullOrWhiteSpace(userName))
            return userName;
        if (!string.IsNullOrWhiteSpace(email))
            return email;
        return fallbackId;
    }
}
