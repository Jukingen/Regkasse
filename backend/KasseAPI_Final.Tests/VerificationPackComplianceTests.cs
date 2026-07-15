using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Filters;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Packaged verification scenarios: high-volume daily closing, Monatsbeleg POS reminder (after 7th Vienna day),
/// and fiscal export disclaimer gate.
/// </summary>
public sealed class VerificationPackComplianceTests
{
    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateTime utcInstant)
        {
            _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc));
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VerifyPack_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>VERIFICATION: daily closing succeeds with more than ten reconciled (invoiced) payments for Vienna “today”.</summary>
    [Fact]
    public async Task DailyClosing_WithElevenInvoicedPaymentsToday_Succeeds()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var custId = WalkInCustomerConstants.GuestCustomerId;
        if (!await ctx.Customers.AsNoTracking().AnyAsync(c => c.Id == custId))
        {
            ctx.Customers.Add(new Customer
            {
                Id = custId,
                Name = "Gast",
                Email = "gast@test",
                Phone = "0",
                IsActive = true
            });
        }

        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-VERIFY",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (dayStartUtc, dayEndExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
        var stamp = dayStartUtc.AddHours(12);

        for (var i = 0; i < 11; i++)
        {
            var payId = Guid.NewGuid();
            var invId = Guid.NewGuid();
            var receiptNumber = $"AT-VERIFY-{viennaToday:yyyyMMdd}-{i:D4}";
            ctx.PaymentDetails.Add(new PaymentDetails
            {
                Id = payId,
                CustomerId = custId,
                CustomerName = "Gast",
                TableNumber = 1,
                CashierId = "cashier",
                TotalAmount = 10m,
                TaxAmount = 1m,
                PaymentMethodRaw = "0",
                Steuernummer = "ATU12345678",
                TseSignature = "sig",
                TseTimestamp = stamp,
                CashRegisterId = regId,
                ReceiptNumber = receiptNumber,
                CreatedAt = stamp.AddMinutes(i),
                UpdatedAt = stamp.AddMinutes(i),
                IsActive = true
            });
            ctx.Invoices.Add(new Invoice
            {
                Id = invId,
                SourcePaymentId = payId,
                InvoiceNumber = receiptNumber,
                InvoiceDate = stamp.AddMinutes(i),
                DueDate = stamp.AddMinutes(i),
                Status = InvoiceStatus.Paid,
                Subtotal = 9m,
                TaxAmount = 1m,
                TotalAmount = 10m,
                PaidAmount = 10m,
                RemainingAmount = 0,
                CompanyName = "Test",
                CompanyTaxNumber = "ATU12345678",
                CompanyAddress = "A",
                TseSignature = "sig",
                KassenId = "KASSE-VERIFY",
                TseTimestamp = stamp.AddMinutes(i),
                CashRegisterId = regId,
                TaxDetails = JsonDocument.Parse("{}"),
                InvoiceItems = JsonDocument.Parse("[]"),
                CreatedAt = stamp.AddMinutes(i),
                IsActive = true
            });
        }

        await ctx.SaveChangesAsync();

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.GetTseStatusAsync()).ReturnsAsync(new TseStatus { IsConnected = false });
        tseMock
            .Setup(x => x.CreateDailyClosingSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<int>()))
            .ReturnsAsync("daily-closing-jws-test");

        var finanz = new Mock<IFinanzOnlineService>();
        finanz.Setup(f => f.IsEnabledAsync()).ReturnsAsync(false);

        var hostEnv = new Mock<IHostEnvironment>();
        hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);

        var sut = new TagesabschlussService(
            ctx,
            tseMock.Object,
            new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
            new SoftwareTseKeyProvider(),
            finanz.Object,
            Options.Create(new TseOptions { AllowSimulatedDailyClosing = true, Mode = "Fake" }),
            hostEnv.Object,
            NullLogger<TagesabschlussService>.Instance,
            Mock.Of<IReportPdfCaptureService>(),
            Mock.Of<IReportPdfStorageService>());

        var result = await sut.PerformDailyClosingAsync("user-1", regId);

        Assert.True(result.Success);
        Assert.Equal(11, result.TransactionCount);
    }

    /// <summary>VERIFICATION: Monatsbeleg POS banner level is yellow once Vienna calendar day is past the 7th when a prior month is still missing (not yet overdue).</summary>
    [Fact]
    public async Task MonatsbelegStatus_AfterSeventhViennaDay_WhenPriorMonthMissing_ReturnsYellow()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var custId = WalkInCustomerConstants.GuestCustomerId;
        if (!await ctx.Customers.AsNoTracking().AnyAsync(c => c.Id == custId))
        {
            ctx.Customers.Add(new Customer
            {
                Id = custId,
                Name = "Gast",
                Email = "gast@test",
                Phone = "0",
                IsActive = true
            });
        }

        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-MB",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = custId,
            CustomerName = "Gast",
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = 1m,
            TaxAmount = 0.1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "sig",
            TseTimestamp = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc),
            CashRegisterId = regId,
            ReceiptNumber = "AT-MB-20260415-0001",
            CreatedAt = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        // Vienna local 10 May 2026 → prior April Monatsbeleg missing; April deadline end May → not overdue on 10 May.
        var time = new FixedUtcTimeProvider(new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc));
        var policy = new RksvMonatsbelegPolicy(ctx, Options.Create(new TseOptions()));
        var sut = new MonatsbelegReminderService(ctx, TenantTestDoubles.PrimaryTenantResolver, time, policy);

        var status = await sut.GetMonatsbelegStatusAsync(regId, CancellationToken.None);

        Assert.NotNull(status);
        Assert.True(status.RequiresAttention);
        Assert.Equal("yellow", status.WarningLevel);
        Assert.True(status.DaysUntilDeadline > 0);
    }

    /// <summary>VERIFICATION: fiscal export pipeline rejects requests without disclaimer acknowledgment when required.</summary>
    [Fact]
    public async Task FiscalExport_DisclaimerRequired_MissingHeader_BlocksWith400()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), authenticationType: "mock"));

        var actionCtx = new ActionContext(httpCtx, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(
            actionCtx,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());

        var ranNext = false;
        var filter = new RequireDisclaimerAcknowledgmentFilter(
            Options.Create(new FiscalExportOptions { RequireDisclaimerAcknowledgment = true, LogFailedAttempts = false }),
            new DisclaimerService(),
            NullLogger<RequireDisclaimerAcknowledgmentFilter>.Instance);

        await filter.OnActionExecutionAsync(
            executing,
            async () =>
            {
                ranNext = true;
                await Task.CompletedTask;
                return new ActionExecutedContext(executing, new List<IFilterMetadata>(), executing.Controller);
            });

        Assert.False(ranNext);
        var obj = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        var dto = Assert.IsType<FiscalExportDisclaimerRequiredResponseDto>(obj.Value);
        Assert.Equal("disclaimer_required", dto.Error);
    }
}
