using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tickets;

public interface ITicketRedemptionService
{
    Task<TicketValidationDto?> ValidateAsync(string code, CancellationToken cancellationToken);
    Task<TicketRedemptionResult> RedeemAsync(string code, string? userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IssuedTicketDto>> IssueForSaleAsync(
        Guid tenantId,
        Guid paymentId,
        IReadOnlyList<(Product Product, int Quantity)> ticketLines,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketRedemptionListRowDto>> ListAsync(
        Guid? tenantId,
        TicketRedemptionStatus? status,
        CancellationToken cancellationToken);
}

public abstract record TicketRedemptionResult
{
    public sealed record Ok(TicketValidationDto Ticket) : TicketRedemptionResult;
    public sealed record NotFound : TicketRedemptionResult;
    public sealed record Error(int StatusCode, string Code, string Message) : TicketRedemptionResult;
}

public sealed class TicketRedemptionService : ITicketRedemptionService
{
    private readonly AppDbContext _db;

    public TicketRedemptionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TicketValidationDto?> ValidateAsync(string code, CancellationToken cancellationToken)
    {
        var row = await FindByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToValidation(row, DateTime.UtcNow);
    }

    public async Task<TicketRedemptionResult> RedeemAsync(
        string code,
        string? userId,
        CancellationToken cancellationToken)
    {
        var row = await FindByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return new TicketRedemptionResult.NotFound();

        var now = DateTime.UtcNow;
        if (row.Status == TicketRedemptionStatus.Redeemed || row.RedeemedAtUtc is not null)
        {
            return new TicketRedemptionResult.Error(
                StatusCodes.Status409Conflict,
                TicketErrorCodes.AlreadyRedeemed,
                "Ticket already redeemed.");
        }

        if (row.Status == TicketRedemptionStatus.Cancelled)
        {
            return new TicketRedemptionResult.Error(
                StatusCodes.Status400BadRequest,
                TicketErrorCodes.Cancelled,
                "Ticket is cancelled.");
        }

        if (IsExpired(row, now))
        {
            row.Status = TicketRedemptionStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new TicketRedemptionResult.Error(
                StatusCodes.Status400BadRequest,
                TicketErrorCodes.Expired,
                "Ticket is expired.");
        }

        row.Status = TicketRedemptionStatus.Redeemed;
        row.RedeemedAtUtc = now;
        row.RedeemedByUserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TicketRedemptionResult.Ok(ToValidation(row, now));
    }

    public async Task<IReadOnlyList<IssuedTicketDto>> IssueForSaleAsync(
        Guid tenantId,
        Guid paymentId,
        IReadOnlyList<(Product Product, int Quantity)> ticketLines,
        CancellationToken cancellationToken)
    {
        if (ticketLines.Count == 0)
            return [];

        var days = TicketCodeHasher.DefaultValidityDays;
        var setting = await _db.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.Key == TicketCodeHasher.TenantSettingValidityDaysKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (setting is not null)
            days = TicketCodeHasher.ParseValidityDays(setting.Value);

        var now = DateTime.UtcNow;
        var until = now.AddDays(days);
        var issued = new List<IssuedTicketDto>();
        var usedHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (product, quantity) in ticketLines)
        {
            if (!product.IsTicket)
                continue;
            var count = Math.Max(1, quantity);
            for (var i = 0; i < count; i++)
            {
                string plaintext;
                string hash;
                do
                {
                    plaintext = TicketCodeHasher.GeneratePlaintext();
                    hash = TicketCodeHasher.HashRaw(plaintext);
                }
                while (!usedHashes.Add(hash)
                       || await _db.TicketRedemptions.AnyAsync(
                           t => t.TenantId == tenantId && t.TicketCodeHash == hash,
                           cancellationToken)
                       .ConfigureAwait(false));

                var display = TicketCodeHasher.DisplayPrefix(hash);
                _db.TicketRedemptions.Add(new TicketRedemption
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PaymentDetailId = paymentId,
                    TicketCode = display,
                    TicketCodeHash = hash,
                    Status = TicketRedemptionStatus.Valid,
                    ValidFromUtc = now,
                    ValidUntilUtc = until,
                    CreatedAtUtc = now,
                });
                issued.Add(new IssuedTicketDto
                {
                    Code = plaintext,
                    DisplayCode = display,
                    ValidUntilUtc = until,
                });
            }
        }

        return issued;
    }

    public async Task<IReadOnlyList<TicketRedemptionListRowDto>> ListAsync(
        Guid? tenantId,
        TicketRedemptionStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _db.TicketRedemptions.AsNoTracking().AsQueryable();
        if (tenantId is Guid filterTenant && filterTenant != Guid.Empty)
            query = query.Where(row => row.TenantId == filterTenant);
        if (status is TicketRedemptionStatus filterStatus)
            query = query.Where(row => row.Status == filterStatus);

        var rows = await query
            .OrderByDescending(row => row.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(row => new TicketRedemptionListRowDto
        {
            Id = row.Id,
            TenantId = row.TenantId,
            DisplayCode = row.TicketCode,
            Status = row.Status.ToString(),
            ValidUntilUtc = row.ValidUntilUtc,
            RedeemedAtUtc = row.RedeemedAtUtc,
            RedeemedByUserId = row.RedeemedByUserId,
        }).ToList();
    }

    private Task<TicketRedemption?> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var hash = TicketCodeHasher.HashRaw(code);
        return _db.TicketRedemptions.FirstOrDefaultAsync(row => row.TicketCodeHash == hash, cancellationToken);
    }

    private static bool IsExpired(TicketRedemption row, DateTime now) =>
        row.Status == TicketRedemptionStatus.Expired
        || (row.ValidUntilUtc is DateTime until && until < now);

    private static TicketValidationDto ToValidation(TicketRedemption row, DateTime now)
    {
        var expired = IsExpired(row, now);
        var canRedeem = row.Status == TicketRedemptionStatus.Valid
                        && row.RedeemedAtUtc is null
                        && !expired
                        && (row.ValidFromUtc is null || row.ValidFromUtc <= now);
        return new TicketValidationDto
        {
            DisplayCode = row.TicketCode,
            Status = expired && row.Status == TicketRedemptionStatus.Valid
                ? TicketRedemptionStatus.Expired.ToString()
                : row.Status.ToString(),
            ValidFromUtc = row.ValidFromUtc,
            ValidUntilUtc = row.ValidUntilUtc,
            IsValid = canRedeem,
            CanRedeem = canRedeem,
        };
    }
}
