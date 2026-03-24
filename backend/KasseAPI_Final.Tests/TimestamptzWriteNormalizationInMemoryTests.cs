using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Verifies <see cref="AppDbContext"/> SaveChanges timestamptz normalization without PostgreSQL.
/// EF Core InMemory often forces <see cref="DateTime.Kind"/> to <see cref="DateTimeKind.Unspecified"/> on tracked snapshots;
/// tests assert the normalized UTC instant via <see cref="EntityEntry.CurrentValues"/> ticks (and that SaveChanges does not throw).
/// </summary>
public sealed class TimestamptzWriteNormalizationInMemoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tz_write_norm_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static void AssertTimestamptzInstantNormalized(
        AppDbContext ctx,
        object entity,
        string propertyName,
        DateTime expectedPersistUtc)
    {
        var actual = (DateTime)ctx.Entry(entity).CurrentValues[propertyName]!;
        Assert.Equal(expectedPersistUtc.Ticks, actual.Ticks);
        Assert.Equal(DateTimeKind.Utc, expectedPersistUtc.Kind);
    }

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

    [Fact]
    public async Task SaveChanges_FinanzOnlineError_UnspecifiedOccurredAt_NormalizesInstantAndPersists()
    {
        await using var ctx = CreateContext();
        var unspecified = new DateTime(2026, 3, 10, 11, 0, 0, DateTimeKind.Unspecified);
        var expected = PostgreSqlUtcDateTime.InstantToPersistUtc(unspecified);

        var row = new FinanzOnlineError
        {
            Id = Guid.NewGuid(),
            ErrorType = "X",
            ErrorMessage = "m",
            OccurredAt = unspecified
        };
        ctx.FinanzOnlineErrors.Add(row);
        ctx.ChangeTracker.DetectChanges();
        ctx.ApplyTimestamptzWriteNormalizationForTests();

        AssertTimestamptzInstantNormalized(ctx, row, nameof(FinanzOnlineError.OccurredAt), expected);

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveChanges_AuditLog_UnspecifiedTimestamp_NormalizesInstantAndPersists()
    {
        await using var ctx = CreateContext();
        var unspecified = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Unspecified);
        var expected = PostgreSqlUtcDateTime.InstantToPersistUtc(unspecified);

        var log = new AuditLog
        {
            SessionId = "s-inmem",
            UserId = "u1",
            UserRole = "Admin",
            Action = "READ",
            EntityType = "T",
            Status = AuditLogStatus.Success,
            Timestamp = unspecified
        };
        ctx.AuditLogs.Add(log);
        ctx.ChangeTracker.DetectChanges();
        ctx.ApplyTimestamptzWriteNormalizationForTests();

        AssertTimestamptzInstantNormalized(ctx, log, nameof(AuditLog.Timestamp), expected);

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveChanges_DailyClosing_UnspecifiedViennaMidnightClosingDate_NormalizesAnchorAndPersists()
    {
        await using var ctx = CreateContext();
        const string userId = "u-inmem-dc";
        var regId = Guid.NewGuid();
        await AddMinimalUserAsync(ctx, userId);
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            RegisterNumber = "INMEM-DC",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        var unspecifiedViennaMidnight = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2025, 6, 15);
        var expected = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(unspecifiedViennaMidnight);

        var closing = new DailyClosing
        {
            Id = Guid.NewGuid(),
            CashRegisterId = regId,
            UserId = userId,
            ClosingDate = unspecifiedViennaMidnight,
            ClosingType = "Daily",
            TotalAmount = 1,
            TotalTaxAmount = 0.2m,
            TransactionCount = 1,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        ctx.DailyClosings.Add(closing);
        ctx.ChangeTracker.DetectChanges();
        ctx.ApplyTimestamptzWriteNormalizationForTests();

        AssertTimestamptzInstantNormalized(ctx, closing, nameof(DailyClosing.ClosingDate), expected);

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveChanges_Receipt_UnspecifiedIssuedAt_NormalizesInstantAndPersists()
    {
        await using var ctx = CreateContext();
        const string userId = "u-inmem-rcpt";
        var regId = Guid.NewGuid();
        var custId = Guid.NewGuid();
        var payId = Guid.NewGuid();

        await AddMinimalUserAsync(ctx, userId);
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            RegisterNumber = "INMEM-R",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.Customers.Add(new Customer
        {
            Id = custId,
            Name = "Cust",
            Email = "c@x.test",
            Phone = "1",
            IsActive = true
        });
        ctx.PaymentDetails.Add(new PaymentDetails
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

        var unspecifiedIssued = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var expected = PostgreSqlUtcDateTime.InstantToPersistUtc(unspecifiedIssued);

        var receipt = new Receipt
        {
            PaymentId = payId,
            ReceiptNumber = "AT-INMEM-20260401-1",
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

        AssertTimestamptzInstantNormalized(ctx, receipt, nameof(Receipt.IssuedAt), expected);

        await ctx.SaveChangesAsync();
    }
}
