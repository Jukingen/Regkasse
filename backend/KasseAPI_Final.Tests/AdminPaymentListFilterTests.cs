using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminPaymentListFilterTests
{
    [Fact]
    public void BuildFilterSummary_CountsActiveFilters()
    {
        var filter = new PaymentFilterDto
        {
            MinAmount = 10m,
            PaymentMethods = ["Cash", "Card"],
            Statuses = ["Success"],
            CustomerName = "Anna",
        };

        var summary = PaymentQueryExtensions.BuildFilterSummary(filter, ["Cash", "Card"], usedDefaultDateWindow: true);

        Assert.Equal(4, summary.ActiveFilterCount);
        Assert.Contains("minAmount", summary.AppliedFilters.Keys);
        Assert.Equal(["Cash", "Card"], summary.AvailablePaymentMethods);
        Assert.Equal(PaymentQueryExtensions.KnownStatuses.ToList(), summary.AvailableStatuses);
    }

    [Fact]
    public async Task ApplyStatusFilter_ReturnsOnlyMatchingRows()
    {
        await using var db = CreateContext();
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = LegacyDefaultTenantIds.Primary,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        var customerId = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Test",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        var successId = Guid.NewGuid();
        var cancelledId = Guid.NewGuid();
        db.PaymentDetails.Add(CreatePayment(successId, customerId, regId, isActive: true, isStorno: false));
        db.PaymentDetails.Add(CreatePayment(cancelledId, customerId, regId, isActive: true, isStorno: true));
        await db.SaveChangesAsync();

        var query = db.PaymentDetails.AsNoTracking().ApplyStatusFilter(["Cancelled"]);
        var ids = await query.Select(p => p.Id).ToListAsync();

        Assert.Single(ids);
        Assert.Equal(cancelledId, ids[0]);
    }

    private static PaymentDetails CreatePayment(Guid id, Guid customerId, Guid regId, bool isActive, bool isStorno) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            CustomerName = "Test",
            TableNumber = 1,
            CashierId = "cashier1",
            TotalAmount = 12.34m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "sig",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = $"R-{id:N}",
            CreatedAt = DateTime.UtcNow,
            IsActive = isActive,
            IsStorno = isStorno,
        };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
