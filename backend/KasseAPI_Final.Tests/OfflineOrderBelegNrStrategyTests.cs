using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Paket 30-b: offline replay BelegNr goes through <see cref="IInvoiceStrategy.AllocateReceiptNumberAsync"/>.
/// AT stays on <see cref="SequenceReservationService.FormatBelegNr"/>. CH/EU allocation stays unimplemented.
/// </summary>
public sealed class OfflineOrderBelegNrStrategyTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    [Fact]
    public async Task AtReplay_ReservedBelegNr_MatchesFormatBelegNr()
    {
        var tenantId = SystemTenantIds.Platform;
        var registerId = Guid.NewGuid();
        const string registerNumber = "KASSE-01";
        const int sequence = 7;
        var expected = SequenceReservationService.FormatBelegNr(
            registerNumber,
            DateTime.UtcNow.Date,
            sequence);

        await using var db = CreateDb(tenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedRegister(db, tenantId, registerId, registerNumber);
        SeedCountrySettings(db, CountryProfileCodes.Austria, VatRegime.AT_RKSV_STANDARD, "ATU12345678");
        var orderId = SeedPendingOrder(db, tenantId, registerId);
        await db.SaveChangesAsync();

        string? capturedBelegNr = null;
        var paymentId = Guid.NewGuid();
        var payments = new Mock<IPaymentService>();
        payments
            .Setup(p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((
                CreatePaymentRequest request,
                string _,
                Guid? _,
                Guid? _) =>
            {
                capturedBelegNr = request.ReservedReceiptNumber;
                return new PaymentResult
                {
                    Success = true,
                    PaymentId = paymentId,
                    Payment = new PaymentDetails
                    {
                        Id = paymentId,
                        ReceiptNumber = request.ReservedReceiptNumber ?? expected,
                    },
                };
            });

        var sequences = new Mock<ISequenceReservationService>();
        sequences
            .Setup(s => s.ReserveNextReceiptNumberAsync(registerId, It.IsAny<CancellationToken>(), 3))
            .ReturnsAsync(expected);

        var sut = CreateSut(db, payments.Object, sequences.Object);

        var result = await sut.ReplayOrderByIdAsync(orderId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(expected, capturedBelegNr);
        Assert.Equal(expected, result.InvoiceNumber);
        sequences.Verify(
            s => s.ReserveNextReceiptNumberAsync(registerId, It.IsAny<CancellationToken>(), 3),
            Times.Once);
        sequences.Verify(
            s => s.ToBelegNrAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(CountryProfileCodes.Switzerland, VatRegime.CH_MWST_STANDARD, CountryStrategyDocs.Switzerland)]
    [InlineData(CountryProfileCodes.EuDefault, VatRegime.EU_OSS, CountryStrategyDocs.EuDefault)]
    public async Task NonAtReplay_AllocateReceiptNumber_ThrowsNotImplemented(
        string country,
        VatRegime regime,
        string docsPath)
    {
        var tenantId = SystemTenantIds.Platform;
        var registerId = Guid.NewGuid();

        await using var db = CreateDb(tenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedRegister(db, tenantId, registerId, "K1");
        SeedCountrySettings(db, country, regime, "DE123456789");
        var orderId = SeedPendingOrder(db, tenantId, registerId);
        await db.SaveChangesAsync();

        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        var sut = CreateSut(db, payments.Object, Mock.Of<ISequenceReservationService>());

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => sut.ReplayOrderByIdAsync(orderId));

        Assert.Contains("AllocateReceiptNumberAsync", ex.Message, StringComparison.Ordinal);
        Assert.Contains(docsPath, ex.Message, StringComparison.Ordinal);
        payments.Verify(
            p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task DeReplay_WithoutSequenceService_DoesNotCallPayment()
    {
        var tenantId = SystemTenantIds.Platform;
        var registerId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedRegister(db, tenantId, registerId, "K1");
        SeedCountrySettings(db, CountryProfileCodes.Germany, VatRegime.DE_USTG_STANDARD, "DE123456789");
        var orderId = SeedPendingOrder(db, tenantId, registerId);
        await db.SaveChangesAsync();

        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        var sut = CreateSut(db, payments.Object, Mock.Of<ISequenceReservationService>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReplayOrderByIdAsync(orderId));
        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
        payments.Verify(
            p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    private static OfflineOrderService CreateSut(
        AppDbContext db,
        IPaymentService payments,
        ISequenceReservationService sequences)
    {
        var countryContext = new CountryStrategyContext(db, Profiles, TenantTestDoubles.PrimaryTenantResolver);
        return new OfflineOrderService(
            db,
            payments,
            Mock.Of<IAuditLogService>(),
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform),
            TenantTestDoubles.SuperAdminHttpAccessor("cashier-1"),
            NullLogger<OfflineOrderService>.Instance,
            countryStrategyContext: countryContext,
            invoiceStrategyResolver: CountryStrategyWiring.CreateInvoiceResolver(sequences: sequences));
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OfflineOrderBelegNr_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static void SeedRegister(AppDbContext db, Guid tenantId, Guid registerId, string registerNumber)
    {
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = registerNumber,
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static void SeedCountrySettings(
        AppDbContext db,
        string country,
        VatRegime vatRegime,
        string taxNumber)
    {
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Test GmbH",
            CompanyAddress = "S, 1010 Wien",
            CompanyTaxNumber = taxNumber,
            Country = country,
            VatRegime = vatRegime,
            BusinessHours = new Dictionary<string, string>(),
            Currency = country == CountryProfileCodes.Switzerland ? "CHF" : "EUR",
            Language = "de-DE",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm:ss",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
        });
    }

    private static Guid SeedPendingOrder(AppDbContext db, Guid tenantId, Guid registerId)
    {
        var orderId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            CashRegisterId = registerId,
            TableNumber = 1,
            TotalAmount = 10m,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
        });

        db.OfflineOrders.Add(new OfflineOrder
        {
            Id = orderId,
            TenantId = tenantId,
            CashRegisterId = registerId,
            OfflineOrderId = $"OFFLINE-{DateTime.UtcNow:yyyyMMddHHmmss}-0001",
            OrderData = payload,
            OrderTotal = 10m,
            PaymentMethod = "cash",
            Status = OfflineOrderStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(72),
        });
        return orderId;
    }
}
