using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Npgsql write path: <c>timestamptz</c> requires UTC <see cref="DateTime"/>; <see cref="AppDbContext"/> normalizes before save.
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlTimestamptzWritePersistenceTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public PostgreSqlTimestamptzWritePersistenceTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options);

    private static async Task AddMinimalUserAsync(AppDbContext ctx, string id)
    {
        if (await ctx.Users.AnyAsync(u => u.Id == id))
            return;

        ctx.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            Email = $"{id}@x.test",
            NormalizedEmail = $"{id}@x.test".ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FirstName = "T",
            LastName = "U"
        });
    }

    [SkippableFact]
    public async Task SaveChanges_DailyClosing_UnspecifiedViennaMidnightClosingDate_PersistsAndNormalizesToUtc()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        const string userId = "u-tz-write-dc";
        var regId = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            await AddMinimalUserAsync(seed, userId);
            seed.CashRegisters.Add(new CashRegister
            {
                Id = regId,
                RegisterNumber = "TZ-DC-1",
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        var unspecifiedViennaMidnight = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2025, 6, 15);

        await using var ctx = CreateContext();
        var closing = new DailyClosing
        {
            Id = Guid.NewGuid(),
            CashRegisterId = regId,
            UserId = userId,
            ClosingDate = unspecifiedViennaMidnight,
            ClosingType = "Daily",
            TotalAmount = 10,
            TotalTaxAmount = 2,
            TransactionCount = 1,
            TseSignature = "test-sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        ctx.DailyClosings.Add(closing);
        ctx.ChangeTracker.DetectChanges();
        ctx.ApplyTimestamptzWriteNormalizationForTests();

        var expectedUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(unspecifiedViennaMidnight);
        Assert.Equal(DateTimeKind.Utc, closing.ClosingDate.Kind);
        Assert.Equal(expectedUtc, closing.ClosingDate);

        await ctx.SaveChangesAsync();

        var reloaded = await ctx.DailyClosings.AsNoTracking().SingleAsync(c => c.Id == closing.Id);
        Assert.Equal(expectedUtc, reloaded.ClosingDate);
    }

    [SkippableFact]
    public async Task SaveChanges_FinanzOnlineError_UnspecifiedOccurredAt_PersistsAndNormalizesToUtc()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var ctx = CreateContext();
        var unspecified = new DateTime(2026, 2, 10, 15, 30, 0, DateTimeKind.Unspecified);
        var row = new FinanzOnlineError
        {
            Id = Guid.NewGuid(),
            ErrorType = "Validation",
            ErrorMessage = "test",
            OccurredAt = unspecified
        };
        ctx.FinanzOnlineErrors.Add(row);
        ctx.ChangeTracker.DetectChanges();
        ctx.ApplyTimestamptzWriteNormalizationForTests();

        Assert.Equal(DateTimeKind.Utc, row.OccurredAt.Kind);
        Assert.Equal(unspecified.Ticks, row.OccurredAt.Ticks);

        await ctx.SaveChangesAsync();

        var reloaded = await ctx.FinanzOnlineErrors.AsNoTracking().SingleAsync(e => e.Id == row.Id);
        Assert.Equal(row.OccurredAt, reloaded.OccurredAt);
    }

    [SkippableFact]
    public async Task SaveChanges_AuditLog_UnspecifiedTimestamp_PersistsAndNormalizesToUtc()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var ctx = CreateContext();
        var unspecified = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Unspecified);
        var log = new AuditLog
        {
            SessionId = "sess-tz",
            UserId = "u-audit-tz",
            UserRole = "Admin",
            Action = "READ",
            EntityType = "Test",
            Status = AuditLogStatus.Success,
            Timestamp = unspecified
        };
        ctx.AuditLogs.Add(log);
        ctx.ChangeTracker.DetectChanges();
        ctx.ApplyTimestamptzWriteNormalizationForTests();

        Assert.Equal(DateTimeKind.Utc, log.Timestamp.Kind);
        Assert.Equal(unspecified.Ticks, log.Timestamp.Ticks);

        await ctx.SaveChangesAsync();

        var reloaded = await ctx.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == log.Id);
        Assert.Equal(log.Timestamp, reloaded.Timestamp);
    }

    [SkippableFact]
    public async Task SaveChanges_Receipt_UnspecifiedIssuedAt_PersistsAndNormalizesToUtc()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        const string userId = "u-tz-rcpt";
        var regId = Guid.NewGuid();
        var custId = Guid.NewGuid();
        var payId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            await AddMinimalUserAsync(seed, userId);
            seed.CashRegisters.Add(new CashRegister
            {
                Id = regId,
                RegisterNumber = "TZ-R-1",
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            seed.Customers.Add(new Customer
            {
                Id = custId,
                Name = "Cust",
                Email = "cust@tz.test",
                Phone = "1",
                IsActive = true
            });
            seed.PaymentDetails.Add(new PaymentDetails
            {
                Id = payId,
                CustomerId = custId,
                CustomerName = "Cust",
                TableNumber = 1,
                CashierId = userId,
                TotalAmount = 5,
                TaxAmount = 0.5m,
                Steuernummer = "ATU12345678",
                CashRegisterId = regId,
                TseSignature = "pay-sig",
                TseTimestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        await using var ctx = CreateContext();
        var unspecifiedIssued = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var receipt = new Receipt
        {
            PaymentId = payId,
            ReceiptNumber = "AT-TZ-20260401-1",
            IssuedAt = unspecifiedIssued,
            CashRegisterId = regId,
            SubTotal = 5,
            TaxTotal = 0.5m,
            GrandTotal = 5.5m,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Receipts.Add(receipt);
        ctx.ChangeTracker.DetectChanges();
        ctx.ApplyTimestamptzWriteNormalizationForTests();

        Assert.Equal(DateTimeKind.Utc, receipt.IssuedAt.Kind);
        Assert.Equal(unspecifiedIssued.Ticks, receipt.IssuedAt.Ticks);

        await ctx.SaveChangesAsync();

        var reloaded = await ctx.Receipts.AsNoTracking().SingleAsync(r => r.ReceiptId == receipt.ReceiptId);
        Assert.Equal(receipt.IssuedAt, reloaded.IssuedAt);
    }
}
