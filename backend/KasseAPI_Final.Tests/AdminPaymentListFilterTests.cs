using System.Text.Json;
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

    [Fact]
    public async Task QueryAsync_WithCalendarRangeAndEnrichment_ReturnsPaymentRows()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateContext(TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureDefaultTenant(db);
        var regId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        const string cashierId = "cashier-enrich-1";
        var createdAt = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        db.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = createdAt,
            Status = RegisterStatus.Open,
            CreatedAt = createdAt,
            IsActive = true,
        });

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Test",
            CreatedAt = createdAt,
            IsActive = true,
        });

        db.Users.Add(new ApplicationUser
        {
            Id = cashierId,
            UserName = cashierId,
            NormalizedUserName = cashierId.ToUpperInvariant(),
            Email = "cashier@test.local",
            NormalizedEmail = "CASHIER@TEST.LOCAL",
            FirstName = "Max",
            LastName = "Kassier",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = customerId,
            CustomerName = "Test",
            TableNumber = 1,
            CashierId = cashierId,
            TotalAmount = 12.34m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "sig",
            TseTimestamp = createdAt,
            ReceiptNumber = "R-ENRICH-1",
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
            CreatedAt = createdAt,
            IsActive = true,
            IsStorno = false,
        });

        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            PaymentId = paymentId,
            ReceiptNumber = "R-ENRICH-1",
            IssuedAt = createdAt,
            CashierId = cashierId,
            CashRegisterId = regId,
            SubTotal = 10.34m,
            TaxTotal = 2m,
            GrandTotal = 12.34m,
            CreatedAt = createdAt,
        });

        await db.SaveChangesAsync();

        var tenantResolver = TenantTestDoubles.SettingsResolverReturning(tenantId);
        var service = new AdminPaymentListService(
            db,
            tenantResolver,
            new PaymentMethodCatalogService(db, tenantResolver));

        var filter = new PaymentFilterDto
        {
            StartDate = new DateTime(2026, 5, 20),
            EndDate = new DateTime(2026, 6, 19),
            Page = 1,
            PageSize = 50,
            SortBy = "CreatedAt",
            SortDirection = "desc",
            IncludeTotalCount = true,
        };

        var (response, errorCode, errorMessage) = await service.QueryAsync(filter, stornoReason: null);

        Assert.Null(errorCode);
        Assert.Null(errorMessage);
        Assert.Equal(1, response.TotalCount);
        var item = Assert.Single(response.Items);
        Assert.Equal(paymentId, item.Id);
        Assert.Equal(receiptId, item.ReceiptId);
        Assert.Equal("Max Kassier", item.CashierDisplayName);
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

    private static AppDbContext CreateContext(ICurrentTenantAccessor? tenantAccessor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(
            options,
            tenantAccessor ?? TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }
}
