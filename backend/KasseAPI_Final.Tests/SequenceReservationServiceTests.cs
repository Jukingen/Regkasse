using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class SequenceReservationServiceTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public SequenceReservationServiceTests(PostgreSqlReplayFixture fixture)
    {
        _fixture = fixture;
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseAppNpgsql(_fixture.ConnectionString)
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static async Task<Guid> SeedRegisterAsync(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var registerId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = registerId,
            RegisterNumber = "SEQ-K01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        return registerId;
    }

    [SkippableFact]
    public async Task ReserveSequencesAsync_AllocatesConsecutiveCounters()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var ctx = CreateContext();
        var registerId = await SeedRegisterAsync(ctx);
        var service = new SequenceReservationService(ctx, NullLogger<SequenceReservationService>.Instance);

        var first = await service.ReserveSequencesAsync(3, registerId);
        var second = await service.ReserveSequencesAsync(2, registerId);

        Assert.Equal(new[] { 1, 2, 3 }, first);
        Assert.Equal(new[] { 4, 5 }, second);

        var row = await ctx.ReceiptSequences.AsNoTracking()
            .SingleAsync(r => r.CashRegisterId == registerId && r.SequenceDate == DateTime.UtcNow.Date);
        Assert.Equal(6, row.NextSequence);
    }

    [SkippableFact]
    public async Task ReleaseSequencesAsync_RollsBackUnusedTail()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var ctx = CreateContext();
        var registerId = await SeedRegisterAsync(ctx);
        var service = new SequenceReservationService(ctx, NullLogger<SequenceReservationService>.Instance);

        var sequences = await service.ReserveSequencesAsync(3, registerId);
        await service.ReleaseSequencesAsync(new List<int> { sequences[2], sequences[1] }, registerId);

        var row = await ctx.ReceiptSequences.AsNoTracking()
            .SingleAsync(r => r.CashRegisterId == registerId && r.SequenceDate == DateTime.UtcNow.Date);
        Assert.Equal(3, row.NextSequence);

        var next = await service.ReserveSequencesAsync(1, registerId);
        Assert.Equal(new[] { 3 }, next);
    }

    [SkippableFact]
    public async Task IsSequenceAvailableAsync_ReturnsFalseWhenReceiptExists()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var ctx = CreateContext();
        var registerId = await SeedRegisterAsync(ctx);
        var service = new SequenceReservationService(ctx, NullLogger<SequenceReservationService>.Instance);

        var sequences = await service.ReserveSequencesAsync(1, registerId);
        var belegNr = await service.ToBelegNrAsync(registerId, sequences[0]);

        Assert.True(await service.IsSequenceAvailableAsync(sequences[0], registerId));

        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = registerId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test",
            TableNumber = 1,
            CashierId = "u1",
            PaymentMethodRaw = "cash",
            TotalAmount = 1m,
            TaxAmount = 0.1m,
            ReceiptNumber = belegNr,
            CreatedBy = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        Assert.False(await service.IsSequenceAvailableAsync(sequences[0], registerId));
    }
}
