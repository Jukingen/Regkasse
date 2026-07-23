using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.TenantSettings;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantSettingsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantSettings_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(null));
    }

    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Test Tenant",
            Slug = "test-tenant",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CompanyName = "Demo GmbH",
            CompanyAddress = "Wien 1",
            CompanyTaxNumber = "ATU12345678",
            Currency = "EUR",
            Country = "AT",
            Language = "de",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm",
            TaxCalculationMethod = "inclusive",
            InvoiceNumbering = "INV-{yyyy}-{seq}",
            ReceiptNumbering = "R-{seq}",
            DefaultPaymentMethod = "Cash",
            BusinessHours = new Dictionary<string, string>(),
            WorkingHours = WorkingHoursSettings.CreateDefault(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private static TenantSettingsService CreateService(AppDbContext db)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        var notifications = new Mock<ITenantSettingsNotificationService>();
        notifications.Setup(n => n.NotifySettingsChangeAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<string>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new TenantSettingsService(db, audit.Object, notifications.Object);
    }

    [Fact]
    public async Task Request_CreatesPendingChange()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var result = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Timezone,
            JsonSerializer.SerializeToElement("Europe/Berlin"),
            "Move HQ timezone",
            "super-1");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ChangeId);

        var row = await db.TenantSettingsHistory.IgnoreQueryFilters()
            .SingleAsync(h => h.Id == result.ChangeId);
        Assert.Equal(TenantSettingStatuses.Pending, row.Status);
        Assert.Equal(TenantSettingTypes.Timezone, row.SettingType);
        Assert.Contains("Europe/Vienna", row.OldValue);
        Assert.Contains("Europe/Berlin", row.NewValue);
    }

    [Fact]
    public async Task Approve_AppliesTimezone_FourEyesRequired()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var requested = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Timezone,
            JsonSerializer.SerializeToElement("Europe/Zurich"),
            "Switch timezone",
            "super-1");
        Assert.True(requested.Succeeded);

        var selfApprove = await service.ApproveSettingsChangeAsync(requested.ChangeId!.Value, "super-1");
        Assert.False(selfApprove.Succeeded);
        Assert.Equal(TenantSettingsErrorCodes.SelfApproval, selfApprove.ErrorCode);

        var approved = await service.ApproveSettingsChangeAsync(requested.ChangeId.Value, "super-2");
        Assert.True(approved.Succeeded);

        var settings = await db.CompanySettings.IgnoreQueryFilters()
            .SingleAsync(c => c.TenantId == TenantId);
        Assert.Equal("Europe/Zurich", settings.TimeZone);

        var row = await db.TenantSettingsHistory.IgnoreQueryFilters()
            .SingleAsync(h => h.Id == requested.ChangeId);
        Assert.Equal(TenantSettingStatuses.Approved, row.Status);
        Assert.Equal("super-2", row.ApprovedBy);
        Assert.NotNull(row.EffectiveAt);
    }

    [Fact]
    public async Task Reject_DoesNotApplyChange()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var requested = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Timezone,
            JsonSerializer.SerializeToElement("Europe/Berlin"),
            "Move HQ",
            "super-1");

        var rejected = await service.RejectSettingsChangeAsync(
            requested.ChangeId!.Value,
            "super-2",
            "Not approved by compliance");
        Assert.True(rejected.Succeeded);

        var settings = await db.CompanySettings.IgnoreQueryFilters()
            .SingleAsync(c => c.TenantId == TenantId);
        Assert.Equal("Europe/Vienna", settings.TimeZone);
    }

    [Fact]
    public async Task Revert_RestoresOldValue()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var requested = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Country,
            JsonSerializer.SerializeToElement("DE"),
            "Expand to DE",
            "super-1");
        await service.ApproveSettingsChangeAsync(requested.ChangeId!.Value, "super-2");

        var reverted = await service.RevertSettingsChangeAsync(
            requested.ChangeId.Value,
            "super-2",
            "Rollback expansion");
        Assert.True(reverted.Succeeded);

        var settings = await db.CompanySettings.IgnoreQueryFilters()
            .SingleAsync(c => c.TenantId == TenantId);
        Assert.Equal("AT", settings.Country);

        var row = await db.TenantSettingsHistory.IgnoreQueryFilters()
            .SingleAsync(h => h.Id == requested.ChangeId);
        Assert.Equal(TenantSettingStatuses.Reverted, row.Status);
    }

    [Fact]
    public async Task Request_NoOp_Fails()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var result = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Currency,
            JsonSerializer.SerializeToElement("EUR"),
            "noop",
            "super-1");

        Assert.False(result.Succeeded);
        Assert.Equal(TenantSettingsErrorCodes.NoChange, result.ErrorCode);
    }

    [Fact]
    public async Task Request_NonEurCurrency_FailsRksv()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var result = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Currency,
            JsonSerializer.SerializeToElement("USD"),
            "Customer wants USD",
            "super-1");

        Assert.False(result.Succeeded);
        Assert.Equal(TenantSettingsErrorCodes.CurrencyNotRksvCompatible, result.ErrorCode);
    }

    [Fact]
    public async Task Request_NonRksvCountry_Fails()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var result = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Country,
            JsonSerializer.SerializeToElement("US"),
            "Expand to US",
            "super-1");

        Assert.False(result.Succeeded);
        Assert.Equal(TenantSettingsErrorCodes.CountryNotRksvCompatible, result.ErrorCode);
    }

    [Fact]
    public async Task Request_CountryChange_WithFiscalData_Fails()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        await SeedSignedPaymentAsync(db);
        var service = CreateService(db);

        var result = await service.RequestSettingsChangeAsync(
            TenantId,
            TenantSettingType.Country,
            JsonSerializer.SerializeToElement("DE"),
            "Move after go-live",
            "super-1");

        Assert.False(result.Succeeded);
        Assert.Equal(TenantSettingsErrorCodes.CountryLockedFiscal, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrent_HasInvoices_WhenInvoiceExists()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        db.Invoices.Add(CreateMinimalInvoice());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var current = await service.GetCurrentSettingsAsync(TenantId);
        Assert.NotNull(current);
        Assert.True(current!.HasInvoices);
        Assert.False(current.HasFiscalData);
    }

    [Fact]
    public async Task GetCurrent_HasFiscalData_WhenSignedPaymentExists()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        await SeedSignedPaymentAsync(db);
        var service = CreateService(db);

        var current = await service.GetCurrentSettingsAsync(TenantId);
        Assert.NotNull(current);
        Assert.True(current!.HasFiscalData);
    }

    private static async Task SeedSignedPaymentAsync(AppDbContext db)
    {
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = TenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Closed,
            CreatedAt = now,
            IsActive = true,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Customer",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "eyJhbGciOiJFUzI1NiJ9.payload.signature",
            TseTimestamp = now,
            TaxDetails = JsonDocument.Parse("{\"standard\":20}"),
            PaymentItems = JsonDocument.Parse("[]"),
            ReceiptNumber = "AT-TSE-20260723-0001",
            FinanzOnlineStatus = "Pending",
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private static Invoice CreateMinimalInvoice()
    {
        var now = DateTime.UtcNow;
        return new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            InvoiceNumber = "INV-1",
            InvoiceDate = now,
            DueDate = now.AddDays(14),
            Status = InvoiceStatus.Draft,
            Subtotal = 10m,
            TaxAmount = 2m,
            TotalAmount = 12m,
            PaidAmount = 0m,
            RemainingAmount = 12m,
            CompanyName = "Demo GmbH",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien 1",
            TseSignature = "sig",
            KassenId = "KASSE-001",
            TseTimestamp = now,
            CashRegisterId = Guid.NewGuid(),
            CreatedAt = now,
            IsActive = true,
        };
    }
}
